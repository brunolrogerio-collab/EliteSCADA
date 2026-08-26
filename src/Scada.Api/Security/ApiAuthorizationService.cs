using Scada.Api.Runtime;
using Scada.Core.Tags;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed record ApiAuthorizationCheck(
    SecurityPrincipal Principal,
    AuthorizationDecision? Decision)
{
    public bool IsAuthenticated =>
        Principal.IsAuthenticated && !string.IsNullOrWhiteSpace(Principal.SubjectId);

    public bool Allowed => IsAuthenticated && Decision?.Allowed == true;

    public IResult? FailureResult()
    {
        if (!IsAuthenticated) return Results.Unauthorized();
        if (Decision?.Allowed != true)
            return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
        return null;
    }
}

public sealed class ApiAuthorizationService(
    IServiceProvider services,
    EngineeringWorkspace workspace,
    IEngineeringExchangeService exchange)
{
    private readonly object _activeCacheGate = new();
    private string? _cachedProjectKey;
    private long? _cachedRevision;
    private InMemoryCapabilityAuthorizationService? _cachedActivePolicies;

    public SecurityPrincipal GetPrincipal(HttpContext context) =>
        ClaimsPrincipalMapper.Map(context.User);

    public ApiAuthorizationCheck CheckWorkspace(
        HttpContext context,
        SecurityCapability capability,
        AuthorizationResource? resource = null)
    {
        var principal = GetPrincipal(context);
        if (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.SubjectId))
            return new ApiAuthorizationCheck(principal, null);

        var policies = new InMemoryCapabilityAuthorizationService(
            SecurityPolicyCompiler.Compile(workspace.SecurityPolicies.SnapshotRoles()));
        return new ApiAuthorizationCheck(principal, policies.Evaluate(principal, capability, resource));
    }

    public async Task<ApiAuthorizationCheck> CheckRuntimeAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        SecurityCapability capability,
        AuthorizationResource? resource = null,
        CancellationToken cancellationToken = default)
    {
        var principal = GetPrincipal(context);
        if (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.SubjectId))
            return new ApiAuthorizationCheck(principal, null);

        var policies = await ResolveRuntimePoliciesAsync(runtime, capability, cancellationToken);
        if (policies is null)
        {
            return new ApiAuthorizationCheck(
                principal,
                AuthorizationDecision.Denied(
                    capability,
                    "The active runtime authorization policy could not be resolved safely."));
        }

        return new ApiAuthorizationCheck(principal, policies.Evaluate(principal, capability, resource));
    }

    public async Task<ApiAuthorizationCheck> CheckRuntimeTagAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        TagDefinition tag,
        TagAccessOperation operation,
        CancellationToken cancellationToken = default)
    {
        var principal = GetPrincipal(context);
        var capability = operation switch
        {
            TagAccessOperation.Read => SecurityCapability.TagRead,
            TagAccessOperation.Write => SecurityCapability.ProcessValueWrite,
            TagAccessOperation.Configure => SecurityCapability.EngineeringModify,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        if (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.SubjectId))
            return new ApiAuthorizationCheck(principal, null);

        var policies = await ResolveRuntimePoliciesAsync(runtime, capability, cancellationToken);
        if (policies is null)
        {
            return new ApiAuthorizationCheck(
                principal,
                AuthorizationDecision.Denied(
                    capability,
                    "The active runtime authorization policy could not be resolved safely."));
        }

        var decision = new TagAccessAuthorization(policies).Evaluate(principal, tag, operation);
        return new ApiAuthorizationCheck(principal, decision);
    }

    private async Task<InMemoryCapabilityAuthorizationService?> ResolveRuntimePoliciesAsync(
        ScadaRuntimeFacade runtime,
        SecurityCapability requestedCapability,
        CancellationToken cancellationToken)
    {
        var descriptor = runtime.Describe();
        if (!descriptor.Revision.HasValue)
        {
            return new InMemoryCapabilityAuthorizationService(
                SecurityPolicyCompiler.Compile(workspace.SecurityPolicies.SnapshotRoles()));
        }

        if (string.IsNullOrWhiteSpace(descriptor.ProjectKey)) return null;

        lock (_activeCacheGate)
        {
            if (_cachedActivePolicies is not null &&
                _cachedRevision == descriptor.Revision &&
                string.Equals(_cachedProjectKey, descriptor.ProjectKey, StringComparison.OrdinalIgnoreCase))
            {
                return _cachedActivePolicies;
            }
        }

        var persistence = services.GetService<IEngineeringProjectPersistenceService>();
        if (persistence is null) return null;

        var snapshot = await persistence.LoadActiveAsync(descriptor.ProjectKey, cancellationToken);
        if (snapshot is null || snapshot.Revision != descriptor.Revision) return null;

        var package = exchange.ParseJson(snapshot.EngineeringJson);
        var compiled = new InMemoryCapabilityAuthorizationService(
            SecurityPolicyCompiler.Compile(package.SecurityRoles ?? Array.Empty<Scada.Engineering.Contracts.SecurityRoleEngineeringDto>()));

        // Fail closed if the live runtime changed while the persisted policy was being resolved.
        var afterLoad = runtime.Describe();
        if (afterLoad.Revision != descriptor.Revision ||
            !string.Equals(afterLoad.ProjectKey, descriptor.ProjectKey, StringComparison.OrdinalIgnoreCase))
            return null;

        lock (_activeCacheGate)
        {
            _cachedProjectKey = descriptor.ProjectKey;
            _cachedRevision = descriptor.Revision;
            _cachedActivePolicies = compiled;
        }

        return compiled;
    }
}
