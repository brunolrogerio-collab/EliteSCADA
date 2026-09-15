using Scada.Engineering.Security;
using Scada.Security.Authentication;

namespace Scada.Api.Security;

/// <summary>
/// Completes the Security Authority half of a detach through a durable, fail-closed journal.
/// Application lifecycle, attach/restore and other installation subsystems remain out of scope.
/// </summary>
public sealed class AuthorityDetachService(
    IAuthorityLifecycleStore lifecycle,
    ILocalIdentityStore identities,
    IAuthorityPolicyStore policies)
{
    public async Task<AuthorityDetachResult> DetachAsync(CancellationToken cancellationToken = default)
    {
        var before = await lifecycle.GetAsync(cancellationToken);
        if (before.State == AuthorityLifecycleState.DeliberatelyDetached)
            return new AuthorityDetachResult(before, before, Array.Empty<string>(), AlreadyDetached: true);
        if (before.State == AuthorityLifecycleState.InitialInstallation || before.State == AuthorityLifecycleState.Invalid)
            throw new InvalidOperationException("Authority detach requires an attached, valid Security Authority.");

        if (before.State != AuthorityLifecycleState.DetachInProgress)
            await lifecycle.BeginDetachAsync(cancellationToken);
        var subjects = (await identities.ListAsync(cancellationToken))
            .Select(account => account.Id.ToString())
            .ToArray();

        await CompleteJournalAsync(cancellationToken);
        var after = await lifecycle.GetAsync(cancellationToken);
        return new AuthorityDetachResult(before, after, subjects, AlreadyDetached: false);
    }

    /// <summary>Runs before the HTTP pipeline. A persisted intent is authoritative and resumes idempotently.</summary>
    public async Task<bool> RecoverIfInProgressAsync(CancellationToken cancellationToken = default)
    {
        var state = await lifecycle.GetAsync(cancellationToken);
        if (state.State != AuthorityLifecycleState.DetachInProgress) return false;

        await CompleteJournalAsync(cancellationToken);
        return true;
    }

    private async Task CompleteJournalAsync(CancellationToken cancellationToken)
    {
        var journal = await lifecycle.GetAsync(cancellationToken);
        if (journal.State != AuthorityLifecycleState.DetachInProgress)
            throw new InvalidOperationException("Authority detach journal is not in progress.");

        await identities.ClearAllAsync(cancellationToken);
        await policies.InitializeAsync(cancellationToken);
        await ClearCanonicalPolicyAsync(cancellationToken);

        if (await identities.CountAsync(cancellationToken) != 0)
            throw new InvalidOperationException("Authority detach cannot complete while local identities remain.");
        var policy = policies.Snapshot();
        if (policy.Roles.Count != 0 || policy.Scopes.Count != 0)
            throw new InvalidOperationException("Authority detach cannot complete while canonical policy remains.");

        await lifecycle.CompleteDetachAsync(cancellationToken);
    }

    private async Task ClearCanonicalPolicyAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var current = policies.Snapshot();
            if (current.Roles.Count == 0 && current.Scopes.Count == 0) return;

            var cleared = await policies.TryReplaceAsync(
                current.Version,
                Array.Empty<Scada.Engineering.Contracts.SecurityRoleEngineeringDto>(),
                Array.Empty<Scada.Engineering.Contracts.SecurityScopeEngineeringDto>(),
                cancellationToken);
            if (cleared.Applied) return;
        }

        throw new InvalidOperationException("Authority detach policy clear conflicted repeatedly; the journal remains fail-closed for recovery.");
    }
}

public sealed record AuthorityDetachResult(
    AuthorityLifecycleSnapshot Before,
    AuthorityLifecycleSnapshot After,
    IReadOnlyCollection<string> RevokedSubjects,
    bool AlreadyDetached);
