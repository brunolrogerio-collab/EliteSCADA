using Scada.Api.Runtime;
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
    IReadOnlyCollection<SecurityScopeEngineeringDto> Scopes);

public static class AuthorityPolicyAdministrationApi
{
    public const string WireSchema = "elitescada.authority-policy";
    public const int WireSchemaVersion = 1;
    private const string ReadAction = "auth.authority_policy.read";
    private const string PreviewAction = "auth.authority_policy.preview";
    private const string ApplyAction = "auth.authority_policy.apply";

    public static IEndpointRouteBuilder MapAuthorityPolicyAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/authority-policy", async (HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, IAuthorityPolicyStore store, CancellationToken ct) =>
        {
            var authorization = await AuthorizeReadAsync(context, runtime, security, audit, ReadAction, ct);
            if (authorization.Failure is not null) return authorization.Failure;
            var snapshot = store.Snapshot();
            await audit.RecordAsync(context, authorization.Check!.Principal, ReadAction, AuditOutcome.Succeeded, "authority-policy", snapshot.Version.ToString(), new Dictionary<string, string> { ["roleCount"] = snapshot.Roles.Count.ToString(), ["scopeCount"] = snapshot.Scopes.Count.ToString() });
            return Results.Ok(ToDocument(snapshot));
        });

        endpoints.MapPost("/api/auth/authority-policy/preview", async (AuthorityPolicyMutationRequest request, HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, ILocalIdentityStore identities, IAuthorityPolicyStore store, CancellationToken ct) =>
        {
            var authorization = await AuthorizeMutationAsync(context, runtime, security, audit, PreviewAction, ct);
            if (authorization.Failure is not null) return authorization.Failure;
            var validation = await ValidateMutationAsync(request, identities, store, ct);
            if (validation.Error is not null) return Results.BadRequest(new { error = validation.Error });
            await audit.RecordAsync(context, authorization.Check!.Principal, PreviewAction, AuditOutcome.Succeeded, "authority-policy", request.ExpectedVersion.ToString(), AuditDetails(store.Snapshot(), validation.Roles!, validation.Scopes!));
            return Results.Ok(new { schema = WireSchema, schemaVersion = WireSchemaVersion, valid = true, expectedVersion = request.ExpectedVersion, policy = ToDocument(new AuthorityPolicySnapshot(request.ExpectedVersion, validation.Roles!, validation.Scopes!)) });
        });

        endpoints.MapPut("/api/auth/authority-policy", async (AuthorityPolicyMutationRequest request, HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, ILocalIdentityStore identities, IAuthorityPolicyStore store, CancellationToken ct) =>
        {
            var authorization = await AuthorizeMutationAsync(context, runtime, security, audit, ApplyAction, ct);
            if (authorization.Failure is not null) return authorization.Failure;
            var validation = await ValidateMutationAsync(request, identities, store, ct);
            if (validation.Error is not null) return Results.BadRequest(new { error = validation.Error });
            var before = store.Snapshot();
            var result = await store.TryReplaceAsync(request.ExpectedVersion, validation.Roles!, validation.Scopes!, ct);
            if (!result.Applied) return Results.Conflict(new { error = result.Error, currentVersion = result.Snapshot.Version });
            await audit.RecordAsync(context, authorization.Check!.Principal, ApplyAction, AuditOutcome.Succeeded, "authority-policy", result.Snapshot.Version.ToString(), AuditDetails(before, result.Snapshot.Roles, result.Snapshot.Scopes));
            return Results.Ok(ToDocument(result.Snapshot));
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
    private static string CapabilityIds(IEnumerable<SecurityRoleEngineeringDto> roles) => string.Join(",", roles.SelectMany(role => role.Grants ?? Array.Empty<CapabilityGrantEngineeringDto>()).Select(grant => AuthorityPolicyContract.GetCapabilityId(grant.Capability)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    private static AuthorityPolicyDocument ToDocument(AuthorityPolicySnapshot snapshot) => new(WireSchema, WireSchemaVersion, snapshot.Version, snapshot.Roles, snapshot.Scopes);

    private static async Task<(ApiAuthorizationCheck? Check, IResult? Failure)> AuthorizeReadAsync(HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, string action, CancellationToken ct)
    {
        var admin = await security.CheckRuntimeAsync(context, runtime, SecurityCapability.UserRoleAdmin, cancellationToken: ct);
        if (admin.Allowed) return (admin, null);
        var failure = admin.FailureResult() ?? Results.StatusCode(StatusCodes.Status403Forbidden);
        await audit.RecordAuthorizationDeniedAsync(context, admin, action, "authority-policy", "read", new Dictionary<string, string> { ["requiredCapabilities"] = "UserRoleAdmin" });
        return (admin, failure);
    }

    private static async Task<(ApiAuthorizationCheck? Check, IResult? Failure)> AuthorizeMutationAsync(HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security, ApiAuditService audit, string action, CancellationToken ct)
    {
        var roleAdmin = await security.CheckRuntimeAsync(context, runtime, SecurityCapability.UserRoleAdmin, cancellationToken: ct);
        var adminSatisfied = roleAdmin.Allowed;
        if (!adminSatisfied && roleAdmin.IsAuthenticated)
            adminSatisfied = (await security.CheckRuntimeAsync(context, runtime, SecurityCapability.SystemAdmin, cancellationToken: ct)).Allowed;
        var engineering = await security.CheckRuntimeAsync(context, runtime, SecurityCapability.EngineeringModify, cancellationToken: ct);
        if (adminSatisfied && engineering.Allowed) return (roleAdmin, null);
        var failure = roleAdmin.FailureResult() ?? engineering.FailureResult() ?? Results.StatusCode(StatusCodes.Status403Forbidden);
        await audit.RecordAuthorizationDeniedAsync(context, roleAdmin, action, "authority-policy", "mutation", new Dictionary<string, string> { ["requiredCapabilities"] = "UserRoleAdmin|SystemAdmin AND EngineeringModify" });
        return (roleAdmin, failure);
    }
}
