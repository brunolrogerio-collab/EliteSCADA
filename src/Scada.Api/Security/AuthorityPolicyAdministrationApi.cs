using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Engineering.Persistence;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Security.Audit;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed record AuthorityPolicyMutationRequest(
    string Schema,
    int SchemaVersion,
    long ExpectedVersion,
    IReadOnlyCollection<SecurityRoleEngineeringDto>? Roles,
    IReadOnlyCollection<SecurityScopeEngineeringDto>? Scopes);

public sealed record AuthorityPolicyDocument(
    string Schema,
    int SchemaVersion,
    long Version,
    IReadOnlyCollection<SecurityRoleEngineeringDto> Roles,
    IReadOnlyCollection<SecurityScopeEngineeringDto> Scopes,
    Scada.Security.Authorization.RuntimePublicAccessPolicy? PublicRuntime = null);

public sealed record RuntimePublicAccessPolicyMutationRequest(
    string Schema,
    int SchemaVersion,
    long ExpectedVersion,
    Scada.Security.Authorization.RuntimePublicAccessPolicy? Policy);

public static class AuthorityPolicyAdministrationApi
{
    public const string WireSchema = "elitescada.authority-policy";
    public const int WireSchemaVersion = 1;
    private const string ReadAction = "auth.authority_policy.read";
    private const string PreviewAction = "auth.authority_policy.preview";
    private const string ApplyAction = "auth.authority_policy.apply";

    public static IEndpointRouteBuilder MapAuthorityPolicyAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/authority-policy", async (HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, IAuthorityPolicyStore store, AuthorityPolicyHaCoordinator haPolicy, CancellationToken ct) =>
        {
            try { await haPolicy.ReadCurrentSnapshotAsync(ct); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Results.Json(new { error = "AUTHORITY_POLICY_STORE_UNAVAILABLE" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            var authorization = await AuthorizeReadAsync(context, runtime, security, audit, ReadAction, ct);
            if (authorization.Failure is not null) return authorization.Failure;
            AuthorityPolicySnapshot snapshot;
            try { snapshot = await haPolicy.ReadCurrentSnapshotAsync(ct); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Results.Json(new { error = "AUTHORITY_POLICY_STORE_UNAVAILABLE" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            await audit.RecordAsync(context, authorization.Check!.Principal, ReadAction, AuditOutcome.Succeeded, "authority-policy", snapshot.Version.ToString(), new Dictionary<string, string> { ["roleCount"] = snapshot.Roles.Count.ToString(), ["scopeCount"] = snapshot.Scopes.Count.ToString() });
            return Results.Ok(ToDocument(snapshot));
        });

        endpoints.MapPost("/api/auth/authority-policy/preview", async (AuthorityPolicyMutationRequest request, HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, ILocalIdentityStore identities, IAuthorityPolicyStore store, AuthorityPolicyHaCoordinator haPolicy, CancellationToken ct) =>
        {
            try { await haPolicy.ReadCurrentSnapshotAsync(ct); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Results.Json(new { error = "AUTHORITY_POLICY_STORE_UNAVAILABLE" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            var authorization = await AuthorizeMutationAsync(context, runtime, security, audit, PreviewAction, ct);
            if (authorization.Failure is not null) return authorization.Failure;
            var validation = await ValidateMutationAsync(request, identities, store, ct);
            if (validation.Error is not null) return Results.BadRequest(new { error = validation.Error });
            await audit.RecordAsync(context, authorization.Check!.Principal, PreviewAction, AuditOutcome.Succeeded, "authority-policy", request.ExpectedVersion.ToString(), AuditDetails(store.Snapshot(), validation.Roles!, validation.Scopes!));
            return Results.Ok(new { schema = WireSchema, schemaVersion = WireSchemaVersion, valid = true, expectedVersion = request.ExpectedVersion, policy = ToDocument(new AuthorityPolicySnapshot(request.ExpectedVersion, validation.Roles!, validation.Scopes!)) });
        });

        endpoints.MapPut("/api/auth/authority-policy", async (AuthorityPolicyMutationRequest request, HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, ILocalIdentityStore identities, IAuthorityPolicyStore store, AuthorityPolicyHaCoordinator haPolicy, CancellationToken ct) =>
        {
            try { await haPolicy.ReadCurrentSnapshotAsync(ct); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Results.Json(new { error = "AUTHORITY_POLICY_STORE_UNAVAILABLE" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            var authorization = await AuthorizeMutationAsync(context, runtime, security, audit, ApplyAction, ct);
            if (authorization.Failure is not null) return authorization.Failure;
            var validation = await ValidateMutationAsync(request, identities, store, ct);
            if (validation.Error is not null) return Results.BadRequest(new { error = validation.Error });
            var before = store.Snapshot();
            var result = await haPolicy.ApplyMutationAsync(request.ExpectedVersion, validation.Roles!, validation.Scopes!, ct);
            if (!result.Applied)
            {
                var status = result.Error?.StartsWith("AUTHORITY_POLICY_HA_", StringComparison.Ordinal) == true
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status409Conflict;
                return Results.Json(new { error = result.Error, currentVersion = result.Snapshot.Version }, statusCode: status);
            }
            await audit.RecordAsync(context, authorization.Check!.Principal, ApplyAction, AuditOutcome.Succeeded, "authority-policy", result.Snapshot.Version.ToString(), AuditDetails(before, result.Snapshot.Roles, result.Snapshot.Scopes));
            return Results.Ok(ToDocument(result.Snapshot));
        });

        endpoints.MapPut("/api/auth/authority-policy/public-runtime", async (RuntimePublicAccessPolicyMutationRequest request, HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, AuthorityPolicyHaCoordinator haPolicy, CancellationToken ct) =>
        {
            try { await haPolicy.ReadCurrentSnapshotAsync(ct); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Results.Json(new { error = "AUTHORITY_POLICY_STORE_UNAVAILABLE" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            var authorization = await AuthorizeMutationAsync(context, runtime, security, audit, "auth.authority_policy.public_runtime.apply", ct);
            if (authorization.Failure is not null) return authorization.Failure;
            if (!string.Equals(request.Schema, WireSchema, StringComparison.Ordinal) || request.SchemaVersion != WireSchemaVersion)
                return Results.BadRequest(new { error = "AUTHORITY_POLICY_WIRE_SCHEMA_UNSUPPORTED" });
            if (request.ExpectedVersion < 0)
                return Results.BadRequest(new { error = "ExpectedVersion must be non-negative." });
            try { Scada.Security.Authorization.RuntimePublicAccessPolicy.Validate(request.Policy); }
            catch (InvalidDataException exception) { return Results.BadRequest(new { error = exception.Message }); }
            var resourceValidation = await ValidatePublicRuntimeResourcesAsync(request.Policy, runtime, context.RequestServices, ct);
            if (resourceValidation is not null) return resourceValidation;

            var before = await haPolicy.ReadCurrentSnapshotAsync(ct);
            if (before.Version != request.ExpectedVersion)
                return Results.Conflict(new { error = "AUTHORITY_POLICY_CONCURRENCY_CONFLICT", currentVersion = before.Version });
            var result = await haPolicy.ApplyPublicRuntimeMutationAsync(request.ExpectedVersion, request.Policy, ct);
            if (!result.Applied)
            {
                var status = result.Error?.StartsWith("AUTHORITY_POLICY_HA_", StringComparison.Ordinal) == true
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status409Conflict;
                return Results.Json(new { error = result.Error, currentVersion = result.Snapshot.Version }, statusCode: status);
            }
            var currentPolicy = result.Snapshot.PublicRuntime;
            await audit.RecordAsync(
                context,
                authorization.Check!.Principal,
                "auth.authority_policy.public_runtime.apply",
                AuditOutcome.Succeeded,
                "authority-public-runtime-policy",
                result.Snapshot.Version.ToString(),
                new Dictionary<string, string>
                {
                    ["beforeVersion"] = before.Version.ToString(),
                    ["enabled"] = (currentPolicy?.Enabled ?? false).ToString(),
                    ["screenIds"] = StableResourceIds(currentPolicy?.ScreenIds),
                    ["popupIds"] = StableResourceIds(currentPolicy?.PopupIds),
                    ["tagIds"] = StableResourceIds(currentPolicy?.TagIds),
                    ["visualAssetIds"] = StableResourceIds(currentPolicy?.VisualAssetIds)
                });
            return Results.Ok(ToDocument(result.Snapshot));
        });

        endpoints.MapGet("/api/auth/authority-policy/public-runtime/resources", async (HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, IEngineeringProjectPersistenceService? persistence, CancellationToken ct) =>
        {
            var authorization = await AuthorizeReadAsync(context, runtime, security, audit, "auth.authority_policy.public_runtime.resources.read", ct);
            if (authorization.Failure is not null) return authorization.Failure;
            var descriptor = runtime.Describe();
            if (!descriptor.Mode.Equals("engineering", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(descriptor.ProjectKey) || !descriptor.Revision.HasValue || persistence is null)
                return Results.Conflict(new { error = "PUBLIC_RUNTIME_ACTIVE_REVISION_REQUIRED" });
            var active = await persistence.LoadActiveAsync(descriptor.ProjectKey, ct);
            if (active is null || active.Revision != descriptor.Revision)
                return Results.Conflict(new { error = "PUBLIC_RUNTIME_ACTIVE_REVISION_UNAVAILABLE" });
            try
            {
                using var document = JsonDocument.Parse(active.EngineeringJson);
                var root = document.RootElement;
                var resourceCatalog = new
                {
                    projectKey = descriptor.ProjectKey,
                    revision = descriptor.Revision,
                    startupScreenId = ReadOptionalGuid(root, "startupScreenId"),
                    screens = ReadResourceOptions(root, "screens"),
                    popups = ReadResourceOptions(root, "popups"),
                    visualAssets = ReadResourceOptions(root, "visualAssets"),
                    tags = runtime.Tags().Select(tag => new { id = tag.Id, key = tag.Path, name = tag.Name, dataType = tag.DataType.ToString(), engineeringUnit = tag.EngineeringUnit }).OrderBy(tag => tag.key, StringComparer.OrdinalIgnoreCase).ToArray()
                };
                await audit.RecordAsync(context, authorization.Check!.Principal, "auth.authority_policy.public_runtime.resources.read", AuditOutcome.Succeeded, "authority-public-runtime-policy", descriptor.Revision.Value.ToString(), new Dictionary<string, string> { ["screenCount"] = resourceCatalog.screens.Length.ToString(), ["tagCount"] = resourceCatalog.tags.Length.ToString() });
                return Results.Ok(resourceCatalog);
            }
            catch (JsonException)
            {
                return Results.Problem("The Active Engineering revision could not be read safely.", statusCode: StatusCodes.Status500InternalServerError);
            }
        });
        return endpoints;
    }

    private static async Task<(IReadOnlyCollection<SecurityRoleEngineeringDto>? Roles, IReadOnlyCollection<SecurityScopeEngineeringDto>? Scopes, string? Error)> ValidateMutationAsync(AuthorityPolicyMutationRequest request, ILocalIdentityStore identities, IAuthorityPolicyStore store, CancellationToken ct)
    {
        if (!string.Equals(request.Schema, WireSchema, StringComparison.Ordinal) || request.SchemaVersion != WireSchemaVersion)
            return (null, null, "AUTHORITY_POLICY_WIRE_SCHEMA_UNSUPPORTED");
        if (request.ExpectedVersion < 0) return (null, null, "ExpectedVersion must be non-negative.");
        var roles = request.Roles?.ToArray() ?? Array.Empty<SecurityRoleEngineeringDto>();
        var scopes = request.Scopes?.ToArray() ?? Array.Empty<SecurityScopeEngineeringDto>();
        try { InMemoryAuthorityPolicyStore.Validate(roles, scopes); }
        catch (InvalidDataException exception) { return (null, null, exception.Message); }
        var defined = roles.Select(role => role.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var users = await identities.ListAsync(ct);
        var orphan = users.FirstOrDefault(user => user.Roles.Any(role => !defined.Contains(role)));
        if (orphan is not null) return (null, null, $"AUTHORITY_POLICY_ORPHANED_ASSIGNMENT: local user '{orphan.Id}' has a role that would no longer be defined.");
        if (!SecurityScopeGraph.TryCreate(scopes, out var graph, out _)) return (null, null, "Authority scope hierarchy is invalid.");
        var policies = new InMemoryCapabilityAuthorizationService(SecurityPolicyCompiler.Compile(roles));
        var administratorRemains = users.Where(user => user.IsEnabled).Any(user =>
        {
            var principal = new SecurityPrincipal(user.Id.ToString(), user.DisplayName, LocalIdentityNormalization.NormalizeRoles(user.Roles), true);
            return policies.Evaluate(principal, SecurityCapability.UserRoleAdmin, graph!.Enrich(new AuthorizationResource())).Allowed ||
                policies.Evaluate(principal, SecurityCapability.SystemAdmin, graph.Enrich(new AuthorizationResource())).Allowed;
        });
        if (!administratorRemains) return (null, null, "AUTHORITY_POLICY_SELF_LOCKOUT: the resulting policy would leave no enabled local administrator.");
        if (store.Snapshot().Version != request.ExpectedVersion) return (null, null, "AUTHORITY_POLICY_CONCURRENCY_CONFLICT");
        return (roles, scopes, null);
    }

    private static Dictionary<string, string> AuditDetails(AuthorityPolicySnapshot before, IReadOnlyCollection<SecurityRoleEngineeringDto> roles, IReadOnlyCollection<SecurityScopeEngineeringDto> scopes) => new()
    {
        ["beforeVersion"] = before.Version.ToString(),
        ["beforeRoleIds"] = StableIds(before.Roles.Select(role => role.Id)),
        ["beforeScopeIds"] = StableIds(before.Scopes.Select(scope => scope.Id)),
        ["beforeCapabilityIds"] = CapabilityIds(before.Roles),
        ["afterRoleIds"] = StableIds(roles.Select(role => role.Id)),
        ["afterScopeIds"] = StableIds(scopes.Select(scope => scope.Id)),
        ["afterCapabilityIds"] = CapabilityIds(roles),
        ["roleCount"] = roles.Count.ToString(),
        ["scopeCount"] = scopes.Count.ToString()
    };

    private static string StableIds(IEnumerable<Guid?> ids) => string.Join(",", ids.Where(id => id.HasValue).Select(id => id!.Value.ToString("D")).Order());
    private static string StableIds(IEnumerable<Guid> ids) => string.Join(",", ids.Select(id => id.ToString("D")).Order());
    private static string StableResourceIds(IEnumerable<Guid>? ids) => ids is null
        ? string.Empty
        : string.Join(",", ids.Select(id => id.ToString("D")).Order(StringComparer.Ordinal));

    private static async Task<IResult?> ValidatePublicRuntimeResourcesAsync(
        Scada.Security.Authorization.RuntimePublicAccessPolicy? policy,
        ScadaRuntimeFacade runtime,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (policy?.Enabled != true) return null;
        var descriptor = runtime.Describe();
        if (!RuntimePublicSessionApi.MatchesActiveRuntime(policy, descriptor))
            return Results.Conflict(new { error = "PUBLIC_RUNTIME_POLICY_MUST_MATCH_ACTIVE_PROJECT_REVISION" });
        var persistence = services.GetService<IEngineeringProjectPersistenceService>();
        if (persistence is null) return Results.Problem("Engineering persistence is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        var active = await persistence.LoadActiveAsync(descriptor.ProjectKey!, cancellationToken);
        if (active is null || active.Revision != descriptor.Revision)
            return Results.Conflict(new { error = "PUBLIC_RUNTIME_ACTIVE_REVISION_UNAVAILABLE" });

        using var document = JsonDocument.Parse(active.EngineeringJson);
        var root = document.RootElement;
        if (!TryResourceIds(root, "screens", out var screens) ||
            !TryResourceIds(root, "popups", out var popups) ||
            !TryResourceIds(root, "visualAssets", out var assets))
            return Results.Conflict(new { error = "PUBLIC_RUNTIME_ACTIVE_RESOURCES_INVALID" });
        var availableTags = runtime.Tags().Select(tag => tag.Id).ToHashSet();
        if (policy.ScreenIds.Any(id => !screens.Contains(id)) ||
            policy.PopupIds.Any(id => !popups.Contains(id)) ||
            policy.VisualAssetIds.Any(id => !assets.Contains(id)) ||
            policy.TagIds.Any(id => !availableTags.Contains(id)))
            return Results.Conflict(new { error = "PUBLIC_RUNTIME_RESOURCE_NOT_IN_ACTIVE_REVISION" });
        return null;
    }

    private static object[] ReadResourceOptions(JsonElement root, string property) =>
        root.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Select(item => new
            {
                id = ReadGuid(item, "id"),
                key = OptionalText(item, "key") ?? OptionalText(item, "path"),
                name = OptionalText(item, "name") ?? OptionalText(item, "key") ?? OptionalText(item, "path")
            }).Where(item => item.id.HasValue).Cast<object>().ToArray()
            : [];

    private static bool TryResourceIds(JsonElement root, string property, out HashSet<Guid> ids)
    {
        ids = [];
        if (!root.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array) return true;
        foreach (var item in array.EnumerateArray())
        {
            var id = ReadGuid(item, "id");
            if (!id.HasValue || !ids.Add(id.Value)) return false;
        }
        return true;
    }

    private static Guid? ReadOptionalGuid(JsonElement root, string property) => OptionalText(root, property) is { } value && Guid.TryParse(value, out var id) ? id : null;
    private static Guid? ReadGuid(JsonElement root, string property) => ReadOptionalGuid(root, property);
    private static string? OptionalText(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string CapabilityIds(IEnumerable<SecurityRoleEngineeringDto> roles) => string.Join(",", roles.SelectMany(role => role.Grants ?? Array.Empty<CapabilityGrantEngineeringDto>()).Select(grant => AuthorityPolicyContract.GetCapabilityId(grant.Capability)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    private static AuthorityPolicyDocument ToDocument(AuthorityPolicySnapshot snapshot) => new(WireSchema, WireSchemaVersion, snapshot.Version, snapshot.Roles, snapshot.Scopes, snapshot.PublicRuntime);

    private static async Task<(ApiAuthorizationCheck? Check, IResult? Failure)> AuthorizeReadAsync(HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, string action, CancellationToken ct)
    {
        var admin = security.CheckWorkspace(context, SecurityCapability.UserRoleAdmin);
        if (admin.Allowed) return (admin, null);
        var failure = admin.FailureResult() ?? Results.StatusCode(StatusCodes.Status403Forbidden);
        await audit.RecordAuthorizationDeniedAsync(context, admin, action, "authority-policy", "read", new Dictionary<string, string> { ["requiredCapabilities"] = "UserRoleAdmin" });
        return (admin, failure);
    }

    private static async Task<(ApiAuthorizationCheck? Check, IResult? Failure)> AuthorizeMutationAsync(HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, string action, CancellationToken ct)
    {
        var roleAdmin = security.CheckWorkspace(context, SecurityCapability.UserRoleAdmin);
        var adminSatisfied = roleAdmin.Allowed;
        if (!adminSatisfied && roleAdmin.IsAuthenticated)
            adminSatisfied = security.CheckWorkspace(context, SecurityCapability.SystemAdmin).Allowed;
        var engineering = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
        if (adminSatisfied && engineering.Allowed) return (roleAdmin, null);
        var failure = roleAdmin.FailureResult() ?? engineering.FailureResult() ?? Results.StatusCode(StatusCodes.Status403Forbidden);
        await audit.RecordAuthorizationDeniedAsync(context, roleAdmin, action, "authority-policy", "mutation", new Dictionary<string, string> { ["requiredCapabilities"] = "UserRoleAdmin|SystemAdmin AND EngineeringModify" });
        return (roleAdmin, failure);
    }
}
