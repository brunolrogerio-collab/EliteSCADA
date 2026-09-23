using Scada.Core.Product.Licensing;
using Scada.Security.Authorization;

namespace Scada.Api.Licensing;

/// <summary>
/// Bridges the canonical machine-license file and the durable Runtime authority epoch.
/// A pending transition blocks lease use until local Runtime and remote leases agree.
/// </summary>
public sealed class ProductLicenseLifecycleCoordinator(
    IProductLicenseService licensing,
    IRuntimeSessionLeaseStore leaseStore,
    IProductRuntimeAuthorityReevaluator runtime,
    TimeProvider timeProvider)
{
    public async Task<ProductLicenseLifecycleResult> InstallOrReplaceAsync(
        string licenseCode,
        CancellationToken cancellationToken = default)
    {
        var previous = licensing.CurrentVerification.State;
        var candidate = licensing.VerifyCandidate(licenseCode);
        if (candidate.State != LicenseState.Valid)
            return await RejectedCandidateAsync(previous, cancellationToken);

        return await ChangeAsync(
            previous == LicenseState.Valid ? "replace" : "install",
            previous,
            () => licensing.InstallLicense(licenseCode),
            cancellationToken);
    }

    public async Task<ProductLicenseLifecycleResult> RemoveAsync(CancellationToken cancellationToken = default)
    {
        var previous = licensing.CurrentVerification.State;
        if (previous != LicenseState.Demo)
            return await ChangeAsync("remove", previous, licensing.RemoveLicense, cancellationToken);

        var authority = await leaseStore.GetAuthorityStateAsync(cancellationToken);
        if (authority.TransitionPending)
            throw new InvalidOperationException("Runtime authority transition is pending.");

        // Removing an absent machine license cannot create a new authority epoch. In
        // particular, it must not reset the bounded Demo allowance or fence leases
        // already admitted under the existing Demo revision.
        return new ProductLicenseLifecycleResult(
            true, "already-demo", LicenseState.Demo, LicenseState.Demo,
            authority.AuthorityRevision, authority.AuthorityRevision,
            authority.AuthorityChangedAtUtc, 0, "unchanged");
    }

    internal async Task<(LicenseState LicenseState, RuntimeAuthorityState Authority)> ReadAuditStateAsync() =>
        (licensing.CurrentVerification.State, await leaseStore.GetAuthorityStateAsync());

    /// <summary>
    /// Complete an interrupted transition before persisted Runtime recovery. An unresolved
    /// pending marker deliberately remains fail-closed if any step fails again.
    /// </summary>
    public async Task ReconcilePendingAsync(CancellationToken cancellationToken = default)
    {
        var state = await leaseStore.GetAuthorityStateAsync(cancellationToken);
        if (!state.TransitionPending)
            return;
        if (state.TransitionId is not { } transitionId ||
            state.TransitionStartedAtUtc is not { } startedAtUtc ||
            state.TransitionBaseAuthorityRevision is not { } transitionBase)
            throw new InvalidOperationException("Runtime authority transition metadata is incomplete.");

        if (state.AuthorityRevision == transitionBase)
        {
            // W1/W3 cannot establish whether file I/O occurred. The canonical file is truth;
            // conservatively advance/fence and use transition start as no-later-than anchor.
            var authority = licensing.CurrentVerification;
            state = await leaseStore.CommitAuthorityChangeAsync(
                transitionId,
                transitionBase,
                startedAtUtc,
                authority.State == LicenseState.Demo ? startedAtUtc : null,
                cancellationToken);
        }
        else if (state.AuthorityRevision != checked(transitionBase + 1))
        {
            throw new InvalidOperationException("Runtime authority transition revision is incoherent.");
        }

        if (state.AuthorityChangedAtUtc is not { } changedAtUtc)
            throw new InvalidOperationException("Committed Runtime authority transition has no change timestamp.");
        await runtime.ReevaluateForAuthorityChangeAsync(changedAtUtc, cancellationToken);
        await leaseStore.FenceLeasesBeforeAuthorityRevisionAsync(
            transitionId, state.AuthorityRevision, cancellationToken);
        await leaseStore.CompleteAuthorityTransitionAsync(
            transitionId, state.AuthorityRevision, cancellationToken);
    }

    private async Task<ProductLicenseLifecycleResult> RejectedCandidateAsync(
        LicenseState previous,
        CancellationToken cancellationToken)
    {
        var state = await leaseStore.GetAuthorityStateAsync(cancellationToken);
        return new ProductLicenseLifecycleResult(
            false, "invalid-candidate", previous, previous,
            state.AuthorityRevision, state.AuthorityRevision, null, 0, "unchanged");
    }

    private async Task<ProductLicenseLifecycleResult> ChangeAsync(
        string operation,
        LicenseState previous,
        Action mutateFile,
        CancellationToken cancellationToken)
    {
        var transition = await leaseStore.BeginAuthorityTransitionAsync(
            operation, timeProvider.GetUtcNow(), cancellationToken);

        // The file operation is outside every database transaction. A known file failure
        // aborts without revision advance; an unknown post-commit failure stays pending.
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            mutateFile();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or
                                   InvalidOperationException or IOException or UnauthorizedAccessException or
                                   OperationCanceledException)
        {
            var aborted = await leaseStore.AbortAuthorityTransitionAsync(
                transition.TransitionId, transition.BaseAuthorityRevision, CancellationToken.None);
            if (!aborted)
                throw new InvalidOperationException("Runtime authority transition could not be aborted.");

            return new ProductLicenseLifecycleResult(
                false, "file-mutation-failed", previous, licensing.CurrentVerification.State,
                transition.BaseAuthorityRevision, transition.BaseAuthorityRevision,
                null, 0, "unchanged");
        }

        // Once the file changes, cancellation must not abandon the safety sequence.
        // A failure here leaves pending=true for startup reconciliation.
        var changedAtUtc = timeProvider.GetUtcNow();
        var current = licensing.CurrentVerification.State;
        var authority = await leaseStore.CommitAuthorityChangeAsync(
            transition.TransitionId,
            transition.BaseAuthorityRevision,
            changedAtUtc,
            current == LicenseState.Demo ? changedAtUtc : null,
            CancellationToken.None);
        var local = await runtime.ReevaluateForAuthorityChangeAsync(changedAtUtc, CancellationToken.None);
        var fenced = await leaseStore.FenceLeasesBeforeAuthorityRevisionAsync(
            transition.TransitionId, authority.AuthorityRevision, CancellationToken.None);
        await leaseStore.CompleteAuthorityTransitionAsync(
            transition.TransitionId, authority.AuthorityRevision, CancellationToken.None);

        return new ProductLicenseLifecycleResult(
            true, "completed", previous, current,
            transition.BaseAuthorityRevision, authority.AuthorityRevision,
            changedAtUtc, fenced,
            local.RuntimeStopped ? "stopped" : local.RuntimeRetained ? "retained" : "not-active");
    }
}

public sealed record ProductLicenseLifecycleResult(
    bool Succeeded,
    string ReasonCode,
    LicenseState PreviousLicenseState,
    LicenseState CurrentLicenseState,
    long PreviousAuthorityRevision,
    long CurrentAuthorityRevision,
    DateTimeOffset? AuthorityChangedAtUtc,
    int FencedLeaseCount,
    string LocalRuntimeOutcome);
