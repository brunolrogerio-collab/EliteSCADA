using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

/// <summary>
/// Applies a prevalidated Authority target only from a deliberately detached state.
/// Once attach intent is durable, failures remain closed until recovery returns to neutral.
/// </summary>
public sealed class AuthorityAttachService(
    IAuthorityLifecycleStore lifecycle,
    ILocalIdentityStore identities,
    IAuthorityPolicyStore policies)
{
    public async Task<AuthorityAttachResult> AttachAsync(
        AuthorityAttachTarget target,
        CancellationToken cancellationToken = default)
    {
        ValidateTarget(target);

        var before = await lifecycle.GetAsync(cancellationToken);
        if (before.State != AuthorityLifecycleState.DeliberatelyDetached)
            throw new InvalidOperationException("Authority attach requires a deliberately detached Security Authority.");

        await policies.InitializeAsync(cancellationToken);
        var existingUsers = await identities.ListAsync(cancellationToken);
        var existingPolicy = policies.Snapshot();
        if (existingUsers.Count != 0 || existingPolicy.Roles.Count != 0 || existingPolicy.Scopes.Count != 0)
        {
            await lifecycle.MarkInvalidAsync(cancellationToken);
            throw new InvalidOperationException("Detached Authority contains residual data and is invalid; attach is fail-closed.");
        }

        await lifecycle.BeginAttachAsync(cancellationToken);
        var policyApplied = await policies.TryReplaceAsync(
            existingPolicy.Version,
            target.Roles,
            target.Scopes,
            cancellationToken);
        if (!policyApplied.Applied)
            throw new InvalidOperationException("Authority attach policy write conflicted; recovery will return to a closed neutral Authority.");

        if (!await identities.TryReplaceAllIfEmptyAsync(target.Accounts, cancellationToken))
            throw new InvalidOperationException("Authority attach identity write conflicted; recovery will return to a closed neutral Authority.");

        if (await identities.CountAsync(cancellationToken) != target.Accounts.Count)
            throw new InvalidOperationException("Authority attach cannot complete while local identity verification fails.");
        var applied = policies.Snapshot();
        if (applied.Roles.Count != target.Roles.Count || applied.Scopes.Count != target.Scopes.Count)
            throw new InvalidOperationException("Authority attach cannot complete while canonical policy verification fails.");

        var after = await lifecycle.CompleteAttachAsync(cancellationToken);
        return new AuthorityAttachResult(before, after);
    }

    internal static void ValidateTarget(AuthorityAttachTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Accounts.Count == 0)
            throw new InvalidDataException("Authority attach target has no local identities.");
        InMemoryAuthorityPolicyStore.Validate(target.Roles, target.Scopes);
        if (!HasEnabledAdministrator(target))
            throw new InvalidDataException("Authority attach target requires an enabled UserRoleAdmin or SystemAdmin.");
    }

    private static bool HasEnabledAdministrator(AuthorityAttachTarget target)
    {
        if (!SecurityScopeGraph.TryCreate(target.Scopes, out var graph, out _)) return false;
        var authorization = new InMemoryCapabilityAuthorizationService(SecurityPolicyCompiler.Compile(target.Roles));
        return target.Accounts.Where(account => account.IsEnabled).Any(account =>
        {
            var principal = new SecurityPrincipal(
                account.Id.ToString(),
                account.DisplayName,
                LocalIdentityNormalization.NormalizeRoles(account.Roles),
                true);
            return authorization.Evaluate(principal, SecurityCapability.UserRoleAdmin, graph!.Enrich(new AuthorizationResource())).Allowed ||
                authorization.Evaluate(principal, SecurityCapability.SystemAdmin, graph.Enrich(new AuthorizationResource())).Allowed;
        });
    }
}

public sealed record AuthorityAttachTarget(
    IReadOnlyCollection<LocalUserAccount> Accounts,
    IReadOnlyCollection<SecurityRoleEngineeringDto> Roles,
    IReadOnlyCollection<SecurityScopeEngineeringDto> Scopes);

public sealed record AuthorityAttachResult(
    AuthorityLifecycleSnapshot Before,
    AuthorityLifecycleSnapshot After);
