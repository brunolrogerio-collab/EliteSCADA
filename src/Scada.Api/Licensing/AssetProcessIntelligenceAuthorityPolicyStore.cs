using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Licensing;

/// <summary>
/// Invalidates process-local Intelligence permits when an Authority mutation removes
/// an activation capability or changes a scope relationship. Re-granting a capability
/// never rearms a permit; activation must be explicit again.
/// </summary>
public sealed class AssetProcessIntelligenceAuthorityPolicyStore(
    IAuthorityPolicyStore inner,
    AssetProcessIntelligenceExecutionGate executionGate) : IAuthorityPolicyStore, IAsyncDisposable
{
    public AuthorityPolicySnapshot Snapshot() => inner.Snapshot();

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        inner.InitializeAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (inner is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (inner is IDisposable disposable)
            disposable.Dispose();
    }

    public async Task<AuthorityPolicyWriteResult> TryReplaceAsync(
        long expectedVersion,
        IReadOnlyCollection<SecurityRoleEngineeringDto> roles,
        IReadOnlyCollection<SecurityScopeEngineeringDto> scopes,
        CancellationToken cancellationToken = default)
    {
        var before = inner.Snapshot();
        var result = await inner.TryReplaceAsync(expectedVersion, roles, scopes, cancellationToken);
        if (result.Applied &&
            (before.Version != expectedVersion || RevokesIntelligenceActivation(before, result.Snapshot)))
            executionGate.InvalidateAll();
        return result;
    }

    private static bool RevokesIntelligenceActivation(
        AuthorityPolicySnapshot before,
        AuthorityPolicySnapshot after)
    {
        var remainingGrants = ActivationGrantKeys(after.Roles).ToHashSet();
        if (ActivationGrantKeys(before.Roles).Any(grant => !remainingGrants.Contains(grant)))
            return true;

        var currentScopes = after.Scopes.ToDictionary(scope => scope.Id);
        return before.Scopes.Any(previous =>
            !currentScopes.TryGetValue(previous.Id, out var current) ||
            previous.Kind != current.Kind ||
            previous.ParentId != current.ParentId ||
            previous.ResourceId != current.ResourceId);
    }

    private static IEnumerable<ActivationGrantKey> ActivationGrantKeys(
        IEnumerable<SecurityRoleEngineeringDto> roles)
    {
        foreach (var role in roles)
        foreach (var grant in role.Grants ?? Array.Empty<CapabilityGrantEngineeringDto>())
        {
            if (grant.Capability is not (SecurityCapability.AssetProcessIntelligenceManage or SecurityCapability.EngineeringModify))
                continue;

            var scope = grant.Scope;
            yield return new ActivationGrantKey(
                role.Key,
                grant.Capability,
                scope?.Area,
                scope?.EquipmentPath,
                scope?.ScreenKey,
                scope?.TagPath,
                scope?.CommandKey,
                scope?.ScopeNodeId,
                scope?.IncludeDescendants ?? false);
        }
    }

    private readonly record struct ActivationGrantKey(
        string RoleKey,
        SecurityCapability Capability,
        string? Area,
        string? EquipmentPath,
        string? ScreenKey,
        string? TagPath,
        string? CommandKey,
        Guid? ScopeNodeId,
        bool IncludeDescendants);
}
