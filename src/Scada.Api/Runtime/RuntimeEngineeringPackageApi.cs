using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Scada.Api.Persistence;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public static class RuntimeEngineeringPackageApi
{
    public static IEndpointRouteBuilder MapRuntimeEngineeringPackageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDistributedRuntimeFoundationEndpoints();
        endpoints.MapContextualHelpEndpoints();
        endpoints.MapHistoricalPlaybackRuntimeEndpoints();
        endpoints.MapRuntimeMediaSourceEndpoints();

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
                var unrestricted = all.Select(AuthorityPolicyContract.GetCapabilityId).ToArray();
                return Results.Ok(new
                {
                    authorityPolicy = new
                    {
                        schema = AuthorityPolicyContract.Schema,
                        schemaVersion = AuthorityPolicyContract.SchemaVersion
                    },
                    authenticationEnabled = false,
                    runtime = unrestricted,
                    workspace = unrestricted
                });
            }

            var runtimeCapabilities = new List<string>();
            foreach (var capability in all)
            {
                var check = await security.CheckRuntimeAsync(
                    context,
                    runtime,
                    capability,
                    cancellationToken: cancellationToken);
                if (check.Allowed)
                    runtimeCapabilities.Add(AuthorityPolicyContract.GetCapabilityId(capability));
            }

            var workspaceCapabilities = all
                .Where(capability => security.CheckWorkspace(context, capability).Allowed)
                .Select(AuthorityPolicyContract.GetCapabilityId)
                .ToArray();

            return Results.Ok(new
            {
                authorityPolicy = new
                {
                    schema = AuthorityPolicyContract.Schema,
                    schemaVersion = AuthorityPolicyContract.SchemaVersion
                },
                authenticationEnabled = true,
                runtime = runtimeCapabilities,
                workspace = workspaceCapabilities
            });
        });

        endpoints.MapGet("/api/runtime/application", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            IAuthorityPolicyStore authorityPolicies,
            RuntimeHighAvailabilityService highAvailability,
            HighAvailabilityRuntimeCoordinator runtimeCoordinator,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = await AuthorizeRuntimeViewAsync(
                context,
                runtime,
                security,
                cancellationToken);
            if (authorizationFailure is not null) return authorizationFailure;

            var before = runtime.Describe();
            var publicVisitor = PublicRuntimeSessionSecurity.TryRead(context.User, out var visitorIdentity)
                ? visitorIdentity
                : null;
            var authoritySnapshot = authorityPolicies.Snapshot();
            var publicPolicy = authoritySnapshot.PublicRuntime;
            if (publicVisitor is not null &&
                (publicVisitor.PolicyVersion != authoritySnapshot.Version ||
                 !RuntimePublicSessionApi.MatchesActiveRuntime(publicPolicy, before)))
                return Results.Json(new { error = "PUBLIC_RUNTIME_POLICY_REVOKED" }, statusCode: StatusCodes.Status403Forbidden);
            if (!IsEngineering(before))
            {
                return Results.Ok(new
                {
                    mode = "neutral",
                    projectKey = (string?)null,
                    projectName = (string?)null,
                    revision = (long?)null,
                    activatedAtUtc = (DateTimeOffset?)null,
                    package = (object?)null
                });
            }

            var consistencyFailure = await ValidateApplicationAuthorityAsync(context, before, cancellationToken);
            if (consistencyFailure is not null) return consistencyFailure;

            var persistence = context.RequestServices.GetService<IEngineeringProjectPersistenceService>();
            if (persistence is null) return PersistenceUnavailable();

            var snapshot = await persistence.LoadActiveAsync(before.ProjectKey!, cancellationToken);
            if ((snapshot is null || snapshot.Revision != before.Revision) &&
                highAvailability.HasReadyPassiveRuntimeProjection(before))
            {
                // Prefer a matching local published copy when one exists. A standby
                // may have a separate database, however, so the authenticated peer's
                // in-memory application projection is also a valid read-only HMI source
                // while HA confirms that this exact runtime is ReadyStandby.
                snapshot = await persistence.LoadPublishedAsync(before.ProjectKey!, cancellationToken);
            }

            if ((snapshot is null ||
                 !snapshot.ProjectKey.Equals(before.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
                 snapshot.Revision != before.Revision) &&
                highAvailability.HasReadyPassiveRuntimeProjection(before))
            {
                var passiveApplication = runtimeCoordinator.CaptureApplication();
                if (passiveApplication is null)
                {
                    return Results.Conflict(new
                    {
                        error = "Ready Standby Runtime has no authenticated peer application projection."
                    });
                }

                var afterPassiveLoad = runtime.Describe();
                if (!SameRuntime(before, afterPassiveLoad) ||
                    !highAvailability.HasReadyPassiveRuntimeProjection(afterPassiveLoad))
                {
                    return RuntimeChanged();
                }

                try
                {
                    var root = JsonSerializer.SerializeToElement(
                        passiveApplication,
                        RuntimeHaPeerReplicationCoordinator.WireJson);
                    var projection = publicVisitor is null
                        ? ProjectHmiPackage(root)
                        : ProjectPublicHmiPackage(root, publicPolicy!);
                    var localProjectName = snapshot is not null &&
                        snapshot.ProjectKey.Equals(before.ProjectKey, StringComparison.OrdinalIgnoreCase)
                            ? snapshot.ProjectName
                            : before.ProjectKey;
                    var afterPassiveProjection = runtime.Describe();
                    if (!SameRuntime(before, afterPassiveProjection) ||
                        !highAvailability.HasReadyPassiveRuntimeProjection(afterPassiveProjection))
                    {
                        return RuntimeChanged();
                    }

                    if (publicVisitor is not null && !security.IsPublicRuntimePolicyCurrent(publicVisitor, runtime))
                        return Results.Json(new { error = "PUBLIC_RUNTIME_POLICY_REVOKED" }, statusCode: StatusCodes.Status403Forbidden);

                    return Results.Ok(new
                    {
                        mode = "engineering",
                        projectKey = before.ProjectKey,
                        projectName = localProjectName,
                        revision = before.Revision,
                        activatedAtUtc = before.ActivatedAtUtc,
                        package = projection
                    });
                }
                catch (JsonException ex)
                {
                    return InvalidActivePackage(ex.Message);
                }
            }

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

                var projection = publicVisitor is null
                    ? ProjectHmiPackage(root)
                    : ProjectPublicHmiPackage(root, publicPolicy!);
                var afterProjection = runtime.Describe();
                if (!SameRuntime(before, afterProjection))
                    return RuntimeChanged();
                if (publicVisitor is not null && !security.IsPublicRuntimePolicyCurrent(publicVisitor, runtime))
                    return Results.Json(new { error = "PUBLIC_RUNTIME_POLICY_REVOKED" }, statusCode: StatusCodes.Status403Forbidden);

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
            IAuthorityPolicyStore authorityPolicies,
            CancellationToken cancellationToken) =>
        {
            var authorizationFailure = await AuthorizeRuntimeViewAsync(
                context,
                runtime,
                security,
                cancellationToken);
            if (authorizationFailure is not null) return authorizationFailure;

            if (PublicRuntimeSessionSecurity.TryRead(context.User, out var publicVisitor))
            {
                var currentPolicy = authorityPolicies.Snapshot();
                if (currentPolicy.Version != publicVisitor.PolicyVersion ||
                    currentPolicy.PublicRuntime?.VisualAssetIds.Contains(id) != true ||
                    !security.IsPublicRuntimePolicyCurrent(publicVisitor, runtime))
                    return Results.NotFound();
            }

            var before = runtime.Describe();
            if (!IsEngineering(before))
                return Results.NotFound();

            var consistencyFailure = await ValidateApplicationAuthorityAsync(context, before, cancellationToken);
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
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (payload.MediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; base-uri 'none'; form-action 'none'";
                context.Response.Headers["Content-Disposition"] = "inline";
            }
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

    private static async Task<IResult?> ValidateApplicationAuthorityAsync(
        HttpContext context,
        ScadaRuntimeDescriptor live,
        CancellationToken cancellationToken)
    {
        var authority = await EngineeringInstallationApplicationAuthorityResolver.ResolveAsync(
            context.RequestServices.GetService<IEngineeringInstallationBindingStore>(),
            context.RequestServices.GetRequiredService<IConfiguration>(),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(authority.ProjectKey))
        {
            if (authority.UsesLegacyConfiguration)
            {
                return Results.Conflict(new
                {
                    error = "An Engineering runtime is active but EngineeringRuntime:ProjectKey is not configured."
                });
            }

            return Results.Conflict(new
            {
                error = $"Active Runtime projection is unavailable while installation Application binding is '{authority.BindingState}'.",
                applicationBindingState = authority.BindingState?.ToString(),
                liveProjectKey = live.ProjectKey
            });
        }

        return authority.Matches(live.ProjectKey)
            ? null
            : Results.Conflict(new
            {
                error = "Installation Application binding does not match the active Runtime project.",
                configuredProjectKey = authority.ProjectKey,
                applicationBindingState = authority.BindingState?.ToString(),
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
        startupScreenId = OptionalString(root, "startupScreenId"),
        screens = ArrayProperty(root, "screens"),
        popups = ArrayProperty(root, "popups"),
        dynamos = ArrayProperty(root, "dynamos"),
        equipment = ArrayProperty(root, "equipment"),
        templates = ArrayProperty(root, "templates"),
        scripts = ArrayProperty(root, "scripts"),
        scriptVisualEventReferences = ArrayProperty(root, "scriptVisualEventReferences"),
        visualAssets = ArrayProperty(root, "visualAssets"),
        branding = OptionalObjectProperty(root, "branding"),
        runtimePresentation = OptionalObjectProperty(root, "runtimePresentation")
    };

    internal static JsonObject ProjectPublicHmiPackage(JsonElement root, RuntimePublicAccessPolicy policy)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Active Engineering package root must be an object.");

        // Construct the public projection from an allowlist. Starting with a clone of the
        // Engineering package would carry fields such as credentials, data sources, alarms,
        // unexposed TAG catalogs, and future schema additions into an anonymous response.
        var package = new JsonObject
        {
            ["schema"] = JsonValue.Create(RequiredString(root, "schema")),
            ["schemaVersion"] = JsonValue.Create(RequiredInt32(root, "schemaVersion")),
            ["exportedAt"] = CloneOptionalProperty(root, "exportedAt"),
            ["startupScreenId"] = JsonValue.Create(policy.StartScreenId?.ToString("D")),
            ["screens"] = CloneOptionalProperty(root, "screens") ?? new JsonArray(),
            ["popups"] = CloneOptionalProperty(root, "popups") ?? new JsonArray(),
            ["dynamos"] = CloneOptionalProperty(root, "dynamos") ?? new JsonArray(),
            ["equipment"] = CloneOptionalProperty(root, "equipment") ?? new JsonArray(),
            ["templates"] = CloneOptionalProperty(root, "templates") ?? new JsonArray(),
            ["scripts"] = new JsonArray(),
            ["scriptVisualEventReferences"] = new JsonArray(),
            ["visualAssets"] = CloneOptionalProperty(root, "visualAssets") ?? new JsonArray(),
            ["branding"] = CloneOptionalProperty(root, "branding"),
            ["runtimePresentation"] = CloneOptionalProperty(root, "runtimePresentation")
        };
        var publicScreenIds = policy.ScreenIds.ToHashSet();
        var publicPopupIds = policy.PopupIds.ToHashSet();
        var publicAssetIds = policy.VisualAssetIds.ToHashSet();
        var publicTagIds = policy.TagIds.ToHashSet();

        package["screens"] = FilterByStableIds(package["screens"], publicScreenIds);
        package["popups"] = FilterByStableIds(package["popups"], publicPopupIds);
        package["visualAssets"] = FilterByStableIds(package["visualAssets"], publicAssetIds);
        var publicScreenKeys = ReadPublicScreenKeys(package["screens"]);

        var dependencies = new PublicRuntimeVisualDependencies();
        CollectPublicVisualDependencies(package["screens"], dependencies);
        CollectPublicVisualDependencies(package["popups"], dependencies);
        package["dynamos"] = FilterByReferences(package["dynamos"], "id", dependencies.DynamoIds, "key", dependencies.DynamoKeys);
        CollectPublicVisualDependencies(package["dynamos"], dependencies);
        package["equipment"] = FilterByReferences(package["equipment"], "id", dependencies.EquipmentIds, "path", dependencies.EquipmentPaths);
        CollectPublicVisualDependencies(package["equipment"], dependencies);
        package["templates"] = FilterByReferences(package["templates"], "id", dependencies.TemplateIds, "key", dependencies.TemplateKeys);

        ScrubPublicVisualNode(package, publicTagIds);
        package["branding"] = ProjectPublicBranding(package["branding"], publicAssetIds);
        package["runtimePresentation"] = ProjectPublicRuntimePresentation(
            package["runtimePresentation"], publicScreenKeys, publicAssetIds);
        return package;
    }

    private static JsonNode? CloneOptionalProperty(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? JsonNode.Parse(value.GetRawText())
            : null;

    private static HashSet<string> ReadPublicScreenKeys(JsonNode? screens)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (screens is not JsonArray array) return keys;
        foreach (var screen in array.OfType<JsonObject>())
        {
            var key = ReadNodeText(screen["key"]);
            if (!string.IsNullOrWhiteSpace(key)) keys.Add(key);
        }
        return keys;
    }

    private static JsonNode? ProjectPublicBranding(JsonNode? source, HashSet<Guid> publicAssetIds)
    {
        if (source is not JsonObject branding) return null;
        var projected = new JsonObject();
        CopyKnownProperties(branding, projected, "mode", "text", "subtitle");
        if (TryGuid(branding["visualAssetId"], out var assetId) && publicAssetIds.Contains(assetId))
            projected["visualAssetId"] = assetId.ToString("D");
        else if (branding["mode"] is JsonValue mode && mode.TryGetValue<string>(out var modeValue) &&
                 string.Equals(modeValue, "image", StringComparison.OrdinalIgnoreCase))
            projected["mode"] = "default";
        return projected;
    }

    private static JsonNode? ProjectPublicRuntimePresentation(
        JsonNode? source,
        HashSet<string> publicScreenKeys,
        HashSet<Guid> publicAssetIds)
    {
        if (source is not JsonObject presentation) return null;
        var projected = new JsonObject
        {
            ["historicalPlaybackEnabled"] = false,
            ["version"] = CloneNode(presentation["version"]) ?? JsonValue.Create(1)
        };
        CopyKnownProperties(presentation, projected, "mobileOrientation");

        if (presentation["mobileScreens"] is JsonObject mobileScreens)
        {
            var safeMobileScreens = new JsonObject();
            foreach (var entry in mobileScreens)
            {
                var screenKey = ReadNodeText(entry.Value);
                if (!string.IsNullOrWhiteSpace(screenKey) && publicScreenKeys.Contains(screenKey))
                    safeMobileScreens[entry.Key] = screenKey;
            }
            projected["mobileScreens"] = safeMobileScreens;
        }

        if (presentation["header"] is JsonObject header)
        {
            var safeHeader = new JsonObject
            {
                ["historyVisible"] = false,
                ["alarmsVisible"] = false,
                ["playbackVisible"] = false
            };
            CopyKnownProperties(header, safeHeader,
                "enabled", "height", "backgroundColor", "titlePosition", "controlsPosition", "controlsOrder",
                "showScreenName", "overviewVisible");
            safeHeader["titleStyle"] = ProjectPublicHeaderTextStyle(header["titleStyle"]);
            safeHeader["screenNameStyle"] = ProjectPublicHeaderTextStyle(header["screenNameStyle"]);
            safeHeader["dateTime"] = ProjectPublicHeaderDateTime(header["dateTime"]);
            if (header["links"] is JsonArray links)
            {
                var safeLinks = new JsonArray();
                foreach (var link in links.OfType<JsonObject>())
                {
                    var screenKey = ReadNodeText(link["screenKey"]);
                    if (string.IsNullOrWhiteSpace(screenKey) || !publicScreenKeys.Contains(screenKey)) continue;
                    var safeLink = new JsonObject();
                    CopyKnownProperties(link, safeLink, "label", "screenKey");
                    if (TryGuid(link["visualAssetId"], out var assetId) && publicAssetIds.Contains(assetId))
                        safeLink["visualAssetId"] = assetId.ToString("D");
                    safeLinks.Add(safeLink);
                }
                safeHeader["links"] = safeLinks;
            }
            projected["header"] = safeHeader;
        }
        return projected;
    }

    private static JsonNode? ProjectPublicHeaderTextStyle(JsonNode? source)
    {
        if (source is not JsonObject style) return null;
        var projected = new JsonObject();
        CopyKnownProperties(style, projected, "fontFamily", "fontSize", "fontWeight", "color");
        return projected;
    }

    private static JsonNode? ProjectPublicHeaderDateTime(JsonNode? source)
    {
        if (source is not JsonObject dateTime) return null;
        var projected = new JsonObject();
        CopyKnownProperties(dateTime, projected, "mode", "position", "order", "dateFormat", "timeFormat");
        return projected;
    }

    private static void CopyKnownProperties(JsonObject source, JsonObject destination, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            var property = source.FirstOrDefault(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (property.Key is not null && property.Value is not null)
                destination[name] = property.Value.DeepClone();
        }
    }

    private static JsonNode? CloneNode(JsonNode? node) => node?.DeepClone();

    private static JsonArray FilterByStableIds(JsonNode? source, HashSet<Guid> allowedIds)
    {
        var result = new JsonArray();
        if (source is not JsonArray values) return result;
        foreach (var value in values)
        {
            if (value is JsonObject item && TryGuid(item["id"], out var id) && allowedIds.Contains(id))
                result.Add(item.DeepClone());
        }
        return result;
    }

    private static JsonArray FilterByReferences(
        JsonNode? source,
        string idField,
        HashSet<Guid> allowedIds,
        string keyField,
        HashSet<string> allowedKeys)
    {
        var result = new JsonArray();
        if (source is not JsonArray values) return result;
        foreach (var value in values)
        {
            if (value is not JsonObject item) continue;
            var idAllowed = TryGuid(item[idField], out var id) && allowedIds.Contains(id);
            var key = ReadNodeText(item[keyField]);
            if (idAllowed || (!string.IsNullOrWhiteSpace(key) && allowedKeys.Contains(key)))
                result.Add(item.DeepClone());
        }
        return result;
    }

    private static void CollectPublicVisualDependencies(JsonNode? node, PublicRuntimeVisualDependencies dependencies)
    {
        if (node is JsonObject obj)
        {
            AddGuid(obj, "dynamoDefinitionId", dependencies.DynamoIds);
            AddText(obj, "dynamoKey", dependencies.DynamoKeys);
            AddGuid(obj, "equipmentId", dependencies.EquipmentIds);
            AddText(obj, "equipmentPath", dependencies.EquipmentPaths);
            AddGuid(obj, "templateId", dependencies.TemplateIds);
            AddText(obj, "templateKey", dependencies.TemplateKeys);
            foreach (var value in obj.Select(pair => pair.Value))
                CollectPublicVisualDependencies(value, dependencies);
        }
        else if (node is JsonArray array)
        {
            foreach (var value in array) CollectPublicVisualDependencies(value, dependencies);
        }
    }

    private static void ScrubPublicVisualNode(JsonNode? node, HashSet<Guid> publicTagIds)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (property.Key.Equals("actions", StringComparison.OrdinalIgnoreCase))
                {
                    obj.Remove(property.Key);
                    continue;
                }
                if (property.Key.Equals("bindings", StringComparison.OrdinalIgnoreCase) && property.Value is JsonArray bindings)
                {
                    var safeBindings = new JsonArray();
                    foreach (var binding in bindings)
                    {
                        if (binding is JsonObject bindingObject &&
                            TryGuid((bindingObject["tagReference"] as JsonObject)?["tagId"], out var tagId) &&
                            publicTagIds.Contains(tagId))
                        {
                            var copy = (JsonObject)bindingObject.DeepClone();
                            ScrubPublicVisualNode(copy, publicTagIds);
                            safeBindings.Add(copy);
                        }
                    }
                    obj[property.Key] = safeBindings;
                    continue;
                }
                if (property.Key.Equals("tagReference", StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value is not JsonObject reference ||
                        !TryGuid(reference["tagId"], out var referencedId) ||
                        !publicTagIds.Contains(referencedId))
                        obj.Remove(property.Key);
                    else
                        ScrubPublicVisualNode(property.Value, publicTagIds);
                    continue;
                }
                if (property.Key.Equals("tagId", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGuid(property.Value, out var referencedId) || !publicTagIds.Contains(referencedId))
                        obj.Remove(property.Key);
                    continue;
                }
                if (property.Key.Contains("tagPath", StringComparison.OrdinalIgnoreCase) ||
                    property.Key.Equals("targetTagPath", StringComparison.OrdinalIgnoreCase))
                {
                    obj.Remove(property.Key);
                    continue;
                }
                ScrubPublicVisualNode(property.Value, publicTagIds);
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = array.Count - 1; index >= 0; index--)
            {
                var value = array[index];
                if (value is JsonObject item && TryGuid(item["tagId"], out var tagId) && !publicTagIds.Contains(tagId))
                {
                    array.RemoveAt(index);
                    continue;
                }
                ScrubPublicVisualNode(value, publicTagIds);
            }
        }
    }

    private static void AddGuid(JsonObject source, string property, HashSet<Guid> target)
    {
        if (TryGuid(source[property], out var id)) target.Add(id);
    }

    private static void AddText(JsonObject source, string property, HashSet<string> target)
    {
        var value = ReadNodeText(source[property]);
        if (!string.IsNullOrWhiteSpace(value)) target.Add(value);
    }

    private static string? ReadNodeText(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static bool TryGuid(JsonNode? node, out Guid value) => Guid.TryParse(ReadNodeText(node), out value);

    private sealed class PublicRuntimeVisualDependencies
    {
        public HashSet<Guid> DynamoIds { get; } = [];
        public HashSet<string> DynamoKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<Guid> EquipmentIds { get; } = [];
        public HashSet<string> EquipmentPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<Guid> TemplateIds { get; } = [];
        public HashSet<string> TemplateKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

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

    private static JsonElement? OptionalObjectProperty(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Active Engineering payload property '{name}' must be an object when present.");
        return value.Clone();
    }

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
