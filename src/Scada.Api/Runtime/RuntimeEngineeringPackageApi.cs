using System.Security.Cryptography;
using System.Text.Json;
using Scada.Api.Security;
using Scada.Engineering.Persistence;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public static class RuntimeEngineeringPackageApi
{
    public static IEndpointRouteBuilder MapRuntimeEngineeringPackageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/effective-capabilities", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken cancellationToken) =>
        {
            var principal = security.GetPrincipal(context);
            if (security.AuthenticationEnabled &&
                (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.SubjectId)))
            {
                return Results.Unauthorized();
            }

            var all = Enum.GetValues<SecurityCapability>();
            if (!security.AuthenticationEnabled)
            {
                var unrestricted = all.Select(capability => capability.ToString()).ToArray();
                return Results.Ok(new
                {
                    authenticationEnabled = false,
                    runtime = unrestricted,
                    workspace = unrestricted
                });
            }

            var runtimeCapabilities = new List<string>();
            foreach (var capability in all)
            {
                var check = await security.CheckRuntimeAsync(
                    principal,
                    runtime,
                    capability,
                    cancellationToken: cancellationToken);
                if (check.Allowed) runtimeCapabilities.Add(capability.ToString());
            }

            var workspaceCapabilities = all
                .Where(capability => security.CheckWorkspace(context, capability).Allowed)
                .Select(capability => capability.ToString())
                .ToArray();

            return Results.Ok(new
            {
                authenticationEnabled = true,
                runtime = runtimeCapabilities,
                workspace = workspaceCapabilities
            });
        });

        endpoints.MapGet("/api/runtime/application", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = await AuthorizeRuntimeViewAsync(
                context,
                runtime,
                security,
                cancellationToken);
            if (authorizationFailure is not null) return authorizationFailure;

            var before = runtime.Describe();
            if (!IsEngineering(before))
            {
                return Results.Ok(new
                {
                    mode = "simulation",
                    projectKey = (string?)null,
                    projectName = (string?)null,
                    revision = (long?)null,
                    activatedAtUtc = (DateTimeOffset?)null,
                    package = (object?)null
                });
            }

            var consistencyFailure = ValidateConfiguredProject(context, before);
            if (consistencyFailure is not null) return consistencyFailure;

            var persistence = context.RequestServices.GetService<IEngineeringProjectPersistenceService>();
            if (persistence is null) return PersistenceUnavailable();

            var snapshot = await persistence.LoadActiveAsync(before.ProjectKey!, cancellationToken);
            if (snapshot is null)
            {
                return Results.Conflict(new
                {
                    error = $"Active runtime project '{before.ProjectKey}' has no persisted Active revision."
                });
            }

            var afterLoad = runtime.Describe();
            if (!SameRuntime(before, afterLoad))
                return RuntimeChanged();

            if (!snapshot.ProjectKey.Equals(before.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
                snapshot.Revision != before.Revision)
            {
                return Results.Conflict(new
                {
                    error = "Live Runtime identity does not match the persisted Active Engineering revision.",
                    liveProjectKey = before.ProjectKey,
                    liveRevision = before.Revision,
                    persistedProjectKey = snapshot.ProjectKey,
                    persistedRevision = snapshot.Revision
                });
            }

            try
            {
                using var document = JsonDocument.Parse(snapshot.EngineeringJson);
                var root = document.RootElement;
                ValidateSnapshotPayload(snapshot, root);

                var projection = ProjectHmiPackage(root);
                var afterProjection = runtime.Describe();
                if (!SameRuntime(before, afterProjection))
                    return RuntimeChanged();

                return Results.Ok(new
                {
                    mode = "engineering",
                    snapshot.ProjectKey,
                    snapshot.ProjectName,
                    snapshot.Revision,
                    activatedAtUtc = before.ActivatedAtUtc,
                    package = projection
                });
            }
            catch (JsonException ex)
            {
                return InvalidActivePackage(ex.Message);
            }
            catch (InvalidDataException ex)
            {
                return InvalidActivePackage(ex.Message);
            }
        });

        endpoints.MapGet("/api/runtime/visual-assets/{id:guid}/content", async (
            Guid id,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = await AuthorizeRuntimeViewAsync(
                context,
                runtime,
                security,
                cancellationToken);
            if (authorizationFailure is not null) return authorizationFailure;

            var before = runtime.Describe();
            if (!IsEngineering(before))
                return Results.NotFound();

            var consistencyFailure = ValidateConfiguredProject(context, before);
            if (consistencyFailure is not null) return consistencyFailure;

            var persistence = context.RequestServices.GetService<IEngineeringProjectPersistenceService>();
            var store = context.RequestServices.GetService<IEngineeringProjectStore>();
            if (persistence is null || store is null) return PersistenceUnavailable();

            var snapshot = await persistence.LoadActiveAsync(before.ProjectKey!, cancellationToken);
            if (snapshot is null || snapshot.Revision != before.Revision)
                return Results.Conflict(new { error = "Active visual asset revision does not match the live Runtime revision." });

            using var document = JsonDocument.Parse(snapshot.EngineeringJson);
            ValidateSnapshotPayload(snapshot, document.RootElement);
            if (!TryFindVisualAssetMetadata(document.RootElement, id, out var expectedSha256, out var expectedMediaType, out var expectedByteLength))
                return Results.NotFound();

            var assets = await store.LoadRevisionAssetsAsync(snapshot.ProjectKey, snapshot.Revision, cancellationToken);
            var payload = assets.FirstOrDefault(candidate => candidate.AssetId == id);
            if (payload is null) return Results.Problem(
                "Active visual asset content is missing from the persisted revision.",
                statusCode: StatusCodes.Status500InternalServerError);

            var actualSha256 = Convert.ToHexString(SHA256.HashData(payload.Content)).ToLowerInvariant();
            if (!actualSha256.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase) ||
                payload.ByteLength != expectedByteLength ||
                !payload.MediaType.Equals(expectedMediaType, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    "Active visual asset content is inconsistent with the canonical Active Engineering metadata.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var after = runtime.Describe();
            if (!SameRuntime(before, after))
                return RuntimeChanged();

            context.Response.Headers.ETag = $"\"{actualSha256}\"";
            context.Response.Headers.CacheControl = "private, no-cache";
            return Results.File(payload.Content, payload.MediaType);
        });

        return endpoints;
    }

    private static async Task<IResult?> AuthorizeRuntimeViewAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        ApiAuthorizationService security,
        CancellationToken cancellationToken)
    {
        if (!security.AuthenticationEnabled) return null;
        var authorization = await security.CheckRuntimeAsync(
            context,
            runtime,
            SecurityCapability.View,
            cancellationToken: cancellationToken);
        return authorization.FailureResult();
    }

    private static bool IsEngineering(ScadaRuntimeDescriptor descriptor) =>
        descriptor.Mode.Equals("engineering", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(descriptor.ProjectKey) &&
        descriptor.Revision.HasValue;

    private static IResult? ValidateConfiguredProject(HttpContext context, ScadaRuntimeDescriptor live)
    {
        var configured = context.RequestServices.GetRequiredService<IConfiguration>()["EngineeringRuntime:ProjectKey"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Results.Conflict(new
            {
                error = "An Engineering runtime is active but EngineeringRuntime:ProjectKey is not configured."
            });
        }

        return configured.Equals(live.ProjectKey, StringComparison.OrdinalIgnoreCase)
            ? null
            : Results.Conflict(new
            {
                error = "Configured Engineering project does not match the active Runtime project.",
                configuredProjectKey = configured,
                liveProjectKey = live.ProjectKey
            });
    }

    private static void ValidateSnapshotPayload(EngineeringProjectSnapshot snapshot, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Active Engineering payload root must be a JSON object.");

        if (!root.TryGetProperty("schema", out var schemaElement) || schemaElement.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Active Engineering payload does not declare a valid schema.");
        if (!root.TryGetProperty("schemaVersion", out var versionElement) || !versionElement.TryGetInt32(out var schemaVersion))
            throw new InvalidDataException("Active Engineering payload does not declare a valid schemaVersion.");

        var schema = schemaElement.GetString() ?? string.Empty;
        if (!schema.Equals(snapshot.EngineeringSchema, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Stored Engineering schema '{snapshot.EngineeringSchema}' does not match payload schema '{schema}'.");
        if (schemaVersion != snapshot.EngineeringSchemaVersion)
            throw new InvalidDataException($"Stored Engineering schema version {snapshot.EngineeringSchemaVersion} does not match payload version {schemaVersion}.");
    }

    private static object ProjectHmiPackage(JsonElement root) => new
    {
        schema = RequiredString(root, "schema"),
        schemaVersion = RequiredInt32(root, "schemaVersion"),
        exportedAt = OptionalString(root, "exportedAt"),
        screens = ArrayProperty(root, "screens"),
        popups = ArrayProperty(root, "popups"),
        dynamos = ArrayProperty(root, "dynamos"),
        scripts = ArrayProperty(root, "scripts"),
        scriptVisualEventReferences = ArrayProperty(root, "scriptVisualEventReferences"),
        visualAssets = ArrayProperty(root, "visualAssets")
    };

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Active Engineering payload property '{name}' is required and must be a string.");
        return value.GetString()!;
    }

    private static int RequiredInt32(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || !value.TryGetInt32(out var parsed))
            throw new InvalidDataException($"Active Engineering payload property '{name}' is required and must be an integer.");
        return parsed;
    }

    private static string? OptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static JsonElement ArrayProperty(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return JsonDocument.Parse("[]").RootElement.Clone();
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Active Engineering payload property '{name}' must be an array when present.");
        return value.Clone();
    }

    private static bool TryFindVisualAssetMetadata(
        JsonElement root,
        Guid assetId,
        out string sha256,
        out string mediaType,
        out long byteLength)
    {
        sha256 = string.Empty;
        mediaType = string.Empty;
        byteLength = 0;

        if (!root.TryGetProperty("visualAssets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(idElement.GetString(), out var parsedId) ||
                parsedId != assetId)
                continue;

            if (!asset.TryGetProperty("sha256", out var shaElement) || shaElement.ValueKind != JsonValueKind.String ||
                !asset.TryGetProperty("mediaType", out var mediaElement) || mediaElement.ValueKind != JsonValueKind.String ||
                !asset.TryGetProperty("byteLength", out var lengthElement) || !lengthElement.TryGetInt64(out byteLength))
                throw new InvalidDataException($"Active visual asset '{assetId}' has incomplete metadata.");

            sha256 = shaElement.GetString() ?? string.Empty;
            mediaType = mediaElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(sha256) && !string.IsNullOrWhiteSpace(mediaType) && byteLength >= 0;
        }

        return false;
    }

    private static bool SameRuntime(ScadaRuntimeDescriptor left, ScadaRuntimeDescriptor right) =>
        left.Revision == right.Revision &&
        left.ActivatedAtUtc == right.ActivatedAtUtc &&
        left.Mode.Equals(right.Mode, StringComparison.Ordinal) &&
        string.Equals(left.ProjectKey, right.ProjectKey, StringComparison.OrdinalIgnoreCase);

    private static IResult PersistenceUnavailable() => Results.Json(
        new
        {
            error = "Active Engineering Runtime projection requires configured Engineering persistence.",
            configuration = "ConnectionStrings:EliteScada"
        },
        statusCode: StatusCodes.Status503ServiceUnavailable);

    private static IResult RuntimeChanged() => Results.Conflict(new
    {
        error = "Active Runtime changed while the canonical Engineering projection was being resolved. Retry against the new Runtime revision."
    });

    private static IResult InvalidActivePackage(string diagnostic) => Results.Problem(
        $"Active canonical Engineering payload is invalid: {diagnostic}",
        statusCode: StatusCodes.Status500InternalServerError);
}
