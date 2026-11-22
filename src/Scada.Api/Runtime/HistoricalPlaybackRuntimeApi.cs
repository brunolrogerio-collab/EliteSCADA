using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Scada.Api.Persistence;
using Scada.Api.Security;
using Scada.Engineering.Persistence;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public sealed record HistoricalPlaybackScopeRequest(
    string ScreenKey,
    IReadOnlyCollection<string>? PopupKeys = null);

public sealed record HistoricalPlaybackResolvedTag(
    Guid Id,
    string Path,
    string DataType,
    string RetrievalMode);

public sealed record HistoricalPlaybackUnresolvedReference(
    Guid? Id,
    string? Path);

public sealed record HistoricalPlaybackScopeResponse(
    string ScreenKey,
    IReadOnlyCollection<string> PopupKeys,
    IReadOnlyCollection<HistoricalPlaybackResolvedTag> Tags,
    IReadOnlyCollection<HistoricalPlaybackUnresolvedReference> UnresolvedReferences);

public static class HistoricalPlaybackRuntimeApi
{
    public const string ResolveRoute = "/api/runtime/historical-playback/resolve";

    public static IEndpointRouteBuilder MapHistoricalPlaybackRuntimeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(ResolveRoute, async (
            HistoricalPlaybackScopeRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            [FromServices] IEngineeringProjectPersistenceService? persistence,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.ScreenKey))
                return Results.BadRequest(new { error = "Current Screen key is required." });

            if (security.AuthenticationEnabled)
            {
                var view = await security.CheckRuntimeAsync(
                    context, runtime, SecurityCapability.View, cancellationToken: cancellationToken);
                var viewFailure = view.FailureResult();
                if (viewFailure is not null) return viewFailure;

                var history = await security.CheckRuntimeAsync(
                    context, runtime, SecurityCapability.TrendUse, cancellationToken: cancellationToken);
                var historyFailure = history.FailureResult();
                if (historyFailure is not null) return historyFailure;
            }

            if (persistence is null)
                return Results.Json(
                    new { error = "Historical Playback requires configured project persistence." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            var before = runtime.Describe();
            if (!before.Mode.Equals("engineering", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(before.ProjectKey) ||
                !before.Revision.HasValue)
                return Results.Conflict(new { error = "Historical Playback requires an Active Engineering Runtime." });

            var snapshot = await persistence.LoadActiveAsync(before.ProjectKey, cancellationToken);
            if (snapshot is null ||
                snapshot.Revision != before.Revision ||
                !snapshot.ProjectKey.Equals(before.ProjectKey, StringComparison.OrdinalIgnoreCase))
                return Results.Conflict(new { error = "Historical Playback authority no longer matches the Active revision." });

            try
            {
                using var document = JsonDocument.Parse(snapshot.EngineeringJson);
                if (!HistoricalPlaybackScopeResolver.IsEnabled(document.RootElement))
                    return Results.NotFound();

                var resolved = HistoricalPlaybackScopeResolver.Resolve(document.RootElement, request);
                var after = runtime.Describe();
                if (after.Revision != before.Revision ||
                    after.ActivatedAtUtc != before.ActivatedAtUtc ||
                    !string.Equals(after.ProjectKey, before.ProjectKey, StringComparison.OrdinalIgnoreCase))
                    return Results.Conflict(new { error = "Active Runtime changed while Historical Playback scope was being resolved." });

                return Results.Ok(resolved);
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (JsonException ex)
            {
                return Results.Problem(
                    $"Active Engineering payload is invalid: {ex.Message}",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        return endpoints;
    }
}

public static class HistoricalPlaybackScopeResolver
{
    private sealed record TagCatalogEntry(Guid Id, string Path, string DataType);

    public static bool IsEnabled(JsonElement root)
    {
        if (!root.TryGetProperty("runtimePresentation", out var settings) ||
            settings.ValueKind != JsonValueKind.Object)
            return false;
        return settings.TryGetProperty("historicalPlaybackEnabled", out var enabled) &&
            enabled.ValueKind is JsonValueKind.True;
    }

    public static HistoricalPlaybackScopeResponse Resolve(
        JsonElement root,
        HistoricalPlaybackScopeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var screenKey = request.ScreenKey?.Trim();
        if (string.IsNullOrWhiteSpace(screenKey))
            throw new InvalidDataException("Current Screen key is required.");

        var tags = ReadTagCatalog(root);
        var tagsById = tags.ToDictionary(x => x.Id);
        var tagsByPath = tags
            .Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var screens = ReadArray(root, "screens");
        var screen = FindByKey(screens, screenKey)
            ?? throw new InvalidDataException($"Active Screen '{screenKey}' was not found.");

        var requestedPopupKeys = (request.PopupKeys ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var popups = ReadArray(root, "popups");
        var selectedPopups = requestedPopupKeys
            .Select(key => FindByKey(popups, key)
                ?? throw new InvalidDataException($"Active Popup '{key}' was not found."))
            .ToArray();

        var dynamos = ReadArray(root, "dynamos");
        var equipment = ReadArray(root, "equipment");
        var templates = ReadArray(root, "templates");
        var resolved = new Dictionary<Guid, HistoricalPlaybackResolvedTag>();
        var unresolved = new Dictionary<string, HistoricalPlaybackUnresolvedReference>(StringComparer.OrdinalIgnoreCase);
        var visitedDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddTag(Guid? tagId, string? path)
        {
            var normalizedPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
            TagCatalogEntry? tag = null;

            // Stable TAG identity is authoritative. If a stable ID is present but
            // does not resolve in the Active package, never silently retarget by path.
            if (tagId.HasValue && tagId.Value != Guid.Empty)
            {
                if (!tagsById.TryGetValue(tagId.Value, out tag))
                {
                    unresolved[$"id:{tagId.Value:D}"] =
                        new HistoricalPlaybackUnresolvedReference(tagId, normalizedPath);
                    return;
                }
            }
            else if (normalizedPath is not null)
            {
                tagsByPath.TryGetValue(normalizedPath, out tag);
            }

            if (tag is null)
            {
                unresolved[$"path:{normalizedPath ?? "<unknown>"}"] =
                    new HistoricalPlaybackUnresolvedReference(null, normalizedPath);
                return;
            }

            resolved[tag.Id] = new HistoricalPlaybackResolvedTag(
                tag.Id,
                tag.Path,
                tag.DataType,
                IsAnalog(tag.DataType) ? "interpolated" : "atOrBefore");
        }

        string? ResolveBindingTarget(string? path, string? equipmentPath)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var normalized = path.Trim();
            if (string.IsNullOrWhiteSpace(equipmentPath)) return normalized;
            return normalized.Replace(
                "{equipmentPath}",
                equipmentPath.Trim(),
                StringComparison.Ordinal);
        }

        void Walk(JsonElement node, string? equipmentPath = null)
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray()) Walk(item, equipmentPath);
                return;
            }
            if (node.ValueKind != JsonValueKind.Object) return;

            if (node.TryGetProperty("tagReference", out var tagReference) &&
                tagReference.ValueKind == JsonValueKind.Object)
                AddTag(ReadGuid(tagReference, "tagId"), null);

            if (TryString(node, "kind", out var kind) &&
                kind.Equals("tag", StringComparison.OrdinalIgnoreCase))
            {
                var id = node.TryGetProperty("tagReference", out var reference) &&
                         reference.ValueKind == JsonValueKind.Object
                    ? ReadGuid(reference, "tagId")
                    : null;
                var target = TryString(node, "target", out var rawTarget)
                    ? ResolveBindingTarget(rawTarget, equipmentPath)
                    : null;
                AddTag(id, target);
            }

            FollowDefinition(node, "dynamoDefinitionId", "dynamoKey", "dynamo", dynamos, "key", equipmentPath);
            FollowEquipmentDefinition(node, equipmentPath);
            FollowDefinition(node, "templateId", "templateKey", "template", templates, "key", equipmentPath);

            foreach (var property in node.EnumerateObject())
                Walk(property.Value, equipmentPath);
        }

        void FollowDefinition(
            JsonElement node,
            string idProperty,
            string keyProperty,
            string kind,
            IReadOnlyList<JsonElement> catalog,
            string catalogKeyProperty,
            string? equipmentPath)
        {
            var id = ReadGuid(node, idProperty);
            var hasId = id.HasValue && id.Value != Guid.Empty;
            var key = TryString(node, keyProperty, out var keyValue) ? keyValue : null;
            if (!hasId && string.IsNullOrWhiteSpace(key))
                return;

            var identity = hasId ? id!.Value.ToString("D") : key!;
            var token = $"{kind}:{identity}:{equipmentPath ?? string.Empty}";
            if (!visitedDefinitions.Add(token)) return;

            JsonElement? definition = hasId
                ? FindById(catalog, id!.Value)
                : FindByProperty(catalog, catalogKeyProperty, key!);
            if (definition.HasValue) Walk(definition.Value, equipmentPath);
        }

        void FollowEquipmentDefinition(JsonElement node, string? equipmentPath)
        {
            var id = ReadGuid(node, "equipmentId");
            var hasId = id.HasValue && id.Value != Guid.Empty;
            var path = TryString(node, "equipmentPath", out var pathValue) ? pathValue : null;
            if (!hasId && string.IsNullOrWhiteSpace(path))
                return;

            JsonElement? definition = hasId
                ? FindById(equipment, id!.Value)
                : FindByProperty(equipment, "path", path!);
            if (!definition.HasValue) return;

            var resolvedEquipmentPath =
                TryString(definition.Value, "path", out var definitionPath) &&
                !string.IsNullOrWhiteSpace(definitionPath)
                    ? definitionPath.Trim()
                    : ResolveBindingTarget(path, equipmentPath);
            var identity = hasId ? id!.Value.ToString("D") : path!;
            var token = $"equipment:{identity}:{resolvedEquipmentPath ?? string.Empty}";
            if (!visitedDefinitions.Add(token)) return;

            Walk(definition.Value, resolvedEquipmentPath ?? equipmentPath);
        }

        Walk(screen);
        foreach (var popup in selectedPopups) Walk(popup);

        return new HistoricalPlaybackScopeResponse(
            screenKey,
            requestedPopupKeys,
            resolved.Values.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToArray(),
            unresolved.Values
                .OrderBy(x => x.Path ?? x.Id?.ToString("D"), StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static IReadOnlyList<TagCatalogEntry> ReadTagCatalog(JsonElement root)
    {
        var result = new List<TagCatalogEntry>();
        foreach (var item in ReadArray(root, "tags"))
        {
            var id = ReadGuid(item, "id");
            if (!id.HasValue || id.Value == Guid.Empty) continue;
            if (!TryString(item, "path", out var path) || string.IsNullOrWhiteSpace(path)) continue;
            var dataType = TryString(item, "dataType", out var type) ? type : "string";
            result.Add(new TagCatalogEntry(id.Value, path, dataType));
        }
        return result;
    }

    private static IReadOnlyList<JsonElement> ReadArray(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            return Array.Empty<JsonElement>();
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Active Engineering property '{property}' must be an array.");
        return value.EnumerateArray().Select(x => x.Clone()).ToArray();
    }

    private static JsonElement? FindByKey(IReadOnlyList<JsonElement> items, string key) =>
        FindByProperty(items, "key", key);

    private static JsonElement? FindById(IReadOnlyList<JsonElement> items, Guid id)
    {
        foreach (var item in items)
            if (ReadGuid(item, "id") == id) return item;
        return null;
    }

    private static JsonElement? FindByProperty(
        IReadOnlyList<JsonElement> items,
        string property,
        string value)
    {
        foreach (var item in items)
            if (TryString(item, property, out var candidate) &&
                candidate.Equals(value, StringComparison.OrdinalIgnoreCase))
                return item;
        return null;
    }

    private static Guid? ReadGuid(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        return Guid.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    private static bool TryString(JsonElement node, string property, out string value)
    {
        value = string.Empty;
        if (!node.TryGetProperty(property, out var raw) || raw.ValueKind != JsonValueKind.String)
            return false;
        value = raw.GetString() ?? string.Empty;
        return true;
    }

    private static bool IsAnalog(string dataType) =>
        dataType.Equals("float", StringComparison.OrdinalIgnoreCase) ||
        dataType.Equals("double", StringComparison.OrdinalIgnoreCase);
}
