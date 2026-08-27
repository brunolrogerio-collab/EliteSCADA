using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Runtime;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
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

public sealed record RuntimeReadableTagsResult(
    SecurityPrincipal Principal,
    bool AuthenticationRequired,
    bool PolicyResolved,
    IReadOnlyCollection<TagDefinition> Tags)
{
    public IResult? FailureResult()
    {
        if (!AuthenticationRequired) return null;
        if (!Principal.IsAuthenticated || string.IsNullOrWhiteSpace(Principal.SubjectId))
            return Results.Unauthorized();
        if (!PolicyResolved)
            return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
        return null;
    }
}

public sealed class ApiAuthorizationService(
    IServiceProvider services,
    EngineeringWorkspace workspace,
    IEngineeringExchangeService exchange,
    IConfiguration configuration)
{
    private readonly object _activeCacheGate = new();
    private readonly bool _authenticationEnabled = configuration.GetValue<bool>("Authentication:Enabled");
    private string? _cachedProjectKey;
    private long? _cachedRevision;
    private InMemoryCapabilityAuthorizationService? _cachedActivePolicies;

    public bool AuthenticationEnabled => _authenticationEnabled;

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

    public Task<ApiAuthorizationCheck> CheckRuntimeAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        SecurityCapability capability,
        AuthorizationResource? resource = null,
        CancellationToken cancellationToken = default) =>
        CheckRuntimeAsync(GetPrincipal(context), runtime, capability, resource, cancellationToken);

    public async Task<ApiAuthorizationCheck> CheckRuntimeAsync(
        SecurityPrincipal principal,
        ScadaRuntimeFacade runtime,
        SecurityCapability capability,
        AuthorizationResource? resource = null,
        CancellationToken cancellationToken = default)
    {
        if (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.SubjectId))
            return new ApiAuthorizationCheck(principal, null);

        var before = runtime.Describe();
        var policies = await ResolveRuntimePoliciesAsync(runtime, cancellationToken);
        var after = runtime.Describe();
        if (policies is null || !SameRuntime(before, after))
        {
            return new ApiAuthorizationCheck(
                principal,
                AuthorizationDecision.Denied(
                    capability,
                    "The active runtime authorization policy could not be resolved safely."));
        }

        return new ApiAuthorizationCheck(principal, policies.Evaluate(principal, capability, resource));
    }

    public Task<ApiAuthorizationCheck> CheckRuntimeTagAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        TagDefinition tag,
        TagAccessOperation operation,
        CancellationToken cancellationToken = default) =>
        CheckRuntimeTagAsync(GetPrincipal(context), runtime, tag, operation, cancellationToken);

    public async Task<ApiAuthorizationCheck> CheckRuntimeTagAsync(
        SecurityPrincipal principal,
        ScadaRuntimeFacade runtime,
        TagDefinition tag,
        TagAccessOperation operation,
        CancellationToken cancellationToken = default)
    {
        var capability = operation switch
        {
            TagAccessOperation.Read => SecurityCapability.TagRead,
            TagAccessOperation.Write => SecurityCapability.ProcessValueWrite,
            TagAccessOperation.Configure => SecurityCapability.EngineeringModify,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        if (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.SubjectId))
            return new ApiAuthorizationCheck(principal, null);

        var before = runtime.Describe();
        if (!runtime.TryGetTag(tag.Id, out var runtimeTag) ||
            runtimeTag is null ||
            !runtimeTag.Path.Equals(tag.Path, StringComparison.OrdinalIgnoreCase))
        {
            return new ApiAuthorizationCheck(
                principal,
                AuthorizationDecision.Denied(
                    capability,
                    "The TAG is not part of the current active runtime."));
        }

        var policies = await ResolveRuntimePoliciesAsync(runtime, cancellationToken);
        var after = runtime.Describe();
        if (policies is null || !SameRuntime(before, after))
        {
            return new ApiAuthorizationCheck(
                principal,
                AuthorizationDecision.Denied(
                    capability,
                    "The active runtime authorization policy could not be resolved safely."));
        }

        var decision = new TagAccessAuthorization(policies).Evaluate(principal, runtimeTag, operation);
        return new ApiAuthorizationCheck(principal, decision);
    }

    public async Task<RuntimeReadableTagsResult> GetReadableRuntimeTagsAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        CancellationToken cancellationToken = default) =>
        await GetReadableRuntimeTagDefinitionsAsync(
            context,
            runtime,
            runtime.Tags(),
            cancellationToken);

    public async Task<RuntimeReadableTagsResult> GetReadableRuntimeTagDefinitionsAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        IReadOnlyCollection<TagDefinition> tags,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var principal = GetPrincipal(context);
        var before = runtime.Describe();
        if (!_authenticationEnabled)
            return new RuntimeReadableTagsResult(principal, false, true, tags);

        if (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.SubjectId))
        {
            return new RuntimeReadableTagsResult(
                principal,
                true,
                false,
                Array.Empty<TagDefinition>());
        }

        var policies = await ResolveRuntimePoliciesAsync(runtime, cancellationToken);
        var after = runtime.Describe();
        if (policies is null || !SameRuntime(before, after))
        {
            return new RuntimeReadableTagsResult(
                principal,
                true,
                false,
                Array.Empty<TagDefinition>());
        }

        var access = new TagAccessAuthorization(policies);
        var readable = tags
            .Where(tag => access.Evaluate(principal, tag, TagAccessOperation.Read).Allowed)
            .ToArray();
        return new RuntimeReadableTagsResult(principal, true, true, readable);
    }

    public async Task<ApiAuthorizationCheck?> CheckRuntimeTagReadAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        TagDefinition tag,
        CancellationToken cancellationToken = default)
    {
        if (!_authenticationEnabled) return null;
        return await CheckRuntimeTagAsync(
            context,
            runtime,
            tag,
            TagAccessOperation.Read,
            cancellationToken);
    }

    public async Task<bool> CanReadRuntimeTagAsync(
        SecurityPrincipal principal,
        ScadaRuntimeFacade runtime,
        TagDefinition tag,
        CancellationToken cancellationToken = default)
    {
        if (!_authenticationEnabled) return true;
        var check = await CheckRuntimeTagAsync(
            principal,
            runtime,
            tag,
            TagAccessOperation.Read,
            cancellationToken);
        return check.Allowed;
    }

    private async Task<InMemoryCapabilityAuthorizationService?> ResolveRuntimePoliciesAsync(
        ScadaRuntimeFacade runtime,
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
            SecurityPolicyCompiler.Compile(package.SecurityRoles ?? Array.Empty<SecurityRoleEngineeringDto>()));

        // Fail closed if the live runtime changed while the persisted policy was being resolved.
        var afterLoad = runtime.Describe();
        if (!SameRuntime(descriptor, afterLoad))
            return null;

        lock (_activeCacheGate)
        {
            _cachedProjectKey = descriptor.ProjectKey;
            _cachedRevision = descriptor.Revision;
            _cachedActivePolicies = compiled;
        }

        return compiled;
    }

    private static bool SameRuntime(ScadaRuntimeDescriptor left, ScadaRuntimeDescriptor right) =>
        left.Revision == right.Revision &&
        left.ActivatedAtUtc == right.ActivatedAtUtc &&
        left.Mode.Equals(right.Mode, StringComparison.Ordinal) &&
        string.Equals(left.ProjectKey, right.ProjectKey, StringComparison.OrdinalIgnoreCase);
}
