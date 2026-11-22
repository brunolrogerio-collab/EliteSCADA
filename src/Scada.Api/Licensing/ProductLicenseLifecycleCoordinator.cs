using Scada.Core.Product.Licensing;
using Scada.Security.Authorization;
using Scada.Security.Audit;
using System.Collections.Concurrent;

namespace Scada.Api.Licensing;

/// <summary>
/// Bridges the canonical machine-license installation and the durable Runtime authority epoch.
/// A pending transition blocks lease use until local Runtime and remote leases agree.
/// </summary>
public sealed class ProductLicenseLifecycleCoordinator(
    IProductLicenseService licensing,
    IRuntimeSessionLeaseStore leaseStore,
    IProductRuntimeAuthorityReevaluator runtime,
    TimeProvider timeProvider,
    IAuditStore? auditStore = null,
    AssetProcessIntelligenceExecutionGate? intelligenceGate = null,
    FileOnlineLicenseStateStore? onlineJournal = null)
{
    private readonly ConcurrentDictionary<Guid, byte> failedOnlineTransitions = new();
    public async Task<ProductLicenseLifecycleResult> InstallOrReplaceAsync(
        string licenseCode,
        CancellationToken cancellationToken = default)
    {
        var previous = licensing.CurrentVerification.State;
        var candidate = licensing.VerifyCandidate(licenseCode);
        if (candidate.State != LicenseState.Valid)
            return await RejectedCandidateAsync(previous, cancellationToken);

        await using var deviceOperation = onlineJournal?.AcquireOperation(cancellationToken);
        return await ChangeAsync(
            previous == LicenseState.Valid ? "replace" : "install",
            previous,
            () => licensing.InstallLicense(licenseCode),
            cancellationToken);
    }

    public async Task<ProductLicenseLifecycleResult> RemoveAsync(CancellationToken cancellationToken = default)
    {
        await using var deviceOperation = onlineJournal?.AcquireOperation(cancellationToken);
        return await RemoveWhileDeviceOperationOwnedAsync(cancellationToken);
    }

    internal async Task<ProductLicenseLifecycleResult> RemoveWhileDeviceOperationOwnedAsync(CancellationToken cancellationToken)
    {
        var previous = licensing.CurrentVerification.State;
        // An expired online selection is still an installed mode. Explicit removal must
        // clear it, even after the coordinated transition has applied Demo entitlements.
        if (previous != LicenseState.Demo ||
            licensing is FileProductLicenseService file && file.ReadOnlineInstallation() is not null)
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

    // Only the future authorized device/lifecycle caller supplies this candidate. Public
    // offline import stays on InstallOrReplaceAsync and cannot install online artifacts.
    internal async Task<ProductLicenseLifecycleResult> InstallOnlineAsync(
        OnlineProductLicenseCandidate candidate, CancellationToken cancellationToken = default)
    {
        DemandOnlineAudit();
        var source = RequireOnlineSource();
        var previous = licensing.CurrentVerification.State;
        if (source.VerifyOnlineCandidate(candidate).State != LicenseState.Valid)
            return await RejectedCandidateAsync(previous, cancellationToken);
        return await ChangeAsync("online-select", previous, () => source.SelectOnline(candidate), cancellationToken);
    }

    internal async Task<ProductLicenseLifecycleResult> RenewOnlineAsync(
        OnlineProductLicenseCandidate candidate, Guid expectedRevision, CancellationToken cancellationToken = default)
    {
        DemandOnlineAudit();
        var source = RequireOnlineSource();
        var previous = licensing.CurrentVerification.State;
        if (source.VerifyOnlineCandidate(candidate).State != LicenseState.Valid)
            return await RejectedCandidateAsync(previous, cancellationToken);
        return await ChangeAsync("online-renew", previous,
            () => source.SelectOnline(candidate, expectedRevision), cancellationToken);
    }

    internal async Task<ProductLicenseLifecycleResult> ExpireOnlineAsync(
        Guid expectedRevision, CancellationToken cancellationToken = default)
    {
        DemandOnlineAudit();
        CancelUnlicensedIntelligence();
        var source = RequireOnlineSource();
        var previous = licensing.CurrentVerification.State;
        var selected = source.ReadOnlineInstallation();
        if (selected is null || selected.Revision != expectedRevision ||
            selected.Status != OnlineLeaseVerificationStatus.Expired)
            return await RejectedCandidateAsync(previous, cancellationToken);
        if (selected.ExpiryApplied)
        {
            var state = await leaseStore.GetAuthorityStateAsync(cancellationToken);
            if (state.TransitionPending)
                throw new InvalidOperationException("Runtime authority transition is pending.");
            return new(true, "already-online-demo", previous, previous,
                state.AuthorityRevision, state.AuthorityRevision, state.AuthorityChangedAtUtc, 0, "unchanged");
        }
        return await ChangeAsync("online-expire-to-demo", previous,
            () => source.ApplyOnlineExpiry(expectedRevision), cancellationToken);
    }

    internal async Task<ProductLicenseLifecycleResult> InvalidateOnlineAsync(
        string? expectedChangeIdentity, CancellationToken cancellationToken = default)
    {
        DemandOnlineAudit();
        CancelUnlicensedIntelligence();
        var source = RequireOnlineSource();
        var previous = licensing.CurrentVerification.State;
        if (previous != LicenseState.Invalid)
            return await RejectedCandidateAsync(previous, cancellationToken);
        return await ChangeAsync("online-invalid", previous, () =>
        {
            if (source.ReadInstallationChangeIdentity() != expectedChangeIdentity ||
                licensing.CurrentVerification.State != LicenseState.Invalid)
                throw new InvalidOperationException("ONLINE_LICENSE_INSTALLATION_CONFLICT");
            // No fabricated Demo or replacement file. The currently invalid canonical
            // evidence denies Run; the authority epoch fences previously admitted leases.
        }, cancellationToken);
    }

    /// <summary>Recover only online operations that this instance observed failing after publication.</summary>
    internal async Task<string?> ReconcileFailedOnlineAsync(CancellationToken cancellationToken = default)
    {
        if (failedOnlineTransitions.IsEmpty) return null;
        var state = await leaseStore.GetAuthorityStateAsync(cancellationToken);
        string? recoveredKind = null;
        if (state.TransitionPending && state.TransitionId is { } id && failedOnlineTransitions.ContainsKey(id))
        {
            await ReconcilePendingAsync(cancellationToken);
            recoveredKind = state.TransitionKind;
        }
        foreach (var key in failedOnlineTransitions.Keys)
            if (key != state.TransitionId || !state.TransitionPending)
                failedOnlineTransitions.TryRemove(key, out _);
        return recoveredKind;
    }

    private void DemandOnlineAudit()
    {
        if (auditStore is null)
            throw new InvalidOperationException("Online licensing requires the canonical audit store.");
    }

    internal void CancelUnlicensedIntelligence()
    {
        if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
            intelligenceGate?.InvalidateAll();
    }

    private FileProductLicenseService RequireOnlineSource() =>
        licensing as FileProductLicenseService ??
        throw new InvalidOperationException("Online installation is not available from this license source.");

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
            string.IsNullOrWhiteSpace(state.TransitionKind) ||
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
        if (state.TransitionKind!.StartsWith("online-", StringComparison.Ordinal)) CancelUnlicensedIntelligence();
        await runtime.ReevaluateForAuthorityChangeAsync(changedAtUtc, cancellationToken);
        await leaseStore.FenceLeasesBeforeAuthorityRevisionAsync(
            transitionId, state.AuthorityRevision, cancellationToken);
        await RecordOnlineTransitionAsync(state, recovered: true, cancellationToken);
        await leaseStore.CompleteAuthorityTransitionAsync(
            transitionId, state.AuthorityRevision, cancellationToken);
        failedOnlineTransitions.TryRemove(transitionId, out _);
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
            bool aborted;
            try
            {
                aborted = await leaseStore.AbortAuthorityTransitionAsync(
                    transition.TransitionId, transition.BaseAuthorityRevision, CancellationToken.None);
            }
            catch
            {
                RememberFailedOnlineTransition(operation, transition.TransitionId);
                throw;
            }
            if (!aborted)
            {
                RememberFailedOnlineTransition(operation, transition.TransitionId);
                throw new InvalidOperationException("Runtime authority transition could not be aborted.");
            }

            return new ProductLicenseLifecycleResult(
                false, "file-mutation-failed", previous, licensing.CurrentVerification.State,
                transition.BaseAuthorityRevision, transition.BaseAuthorityRevision,
                null, 0, "unchanged");
        }

        // Once the file changes, cancellation must not abandon the safety sequence.
        // A failure here leaves pending=true for startup reconciliation.
        var changedAtUtc = timeProvider.GetUtcNow();
        var current = licensing.CurrentVerification.State;
        try
        {
            if (operation.StartsWith("online-", StringComparison.Ordinal)) CancelUnlicensedIntelligence();
            var authority = await leaseStore.CommitAuthorityChangeAsync(
                transition.TransitionId,
                transition.BaseAuthorityRevision,
                changedAtUtc,
                current == LicenseState.Demo ? changedAtUtc : null,
                CancellationToken.None);
            var local = await runtime.ReevaluateForAuthorityChangeAsync(changedAtUtc, CancellationToken.None);
            var fenced = await leaseStore.FenceLeasesBeforeAuthorityRevisionAsync(
                transition.TransitionId, authority.AuthorityRevision, CancellationToken.None);
            await RecordOnlineTransitionAsync(authority, recovered: false, CancellationToken.None, previous);
            await leaseStore.CompleteAuthorityTransitionAsync(
                transition.TransitionId, authority.AuthorityRevision, CancellationToken.None);

            return new ProductLicenseLifecycleResult(
                true, "completed", previous, current,
                transition.BaseAuthorityRevision, authority.AuthorityRevision,
                changedAtUtc, fenced,
                local.RuntimeStopped ? "stopped" : local.RuntimeRetained ? "retained" : "not-active");
        }
        catch
        {
            RememberFailedOnlineTransition(operation, transition.TransitionId);
            throw;
        }
    }

    private void RememberFailedOnlineTransition(string operation, Guid id)
    {
        if (operation.StartsWith("online-", StringComparison.Ordinal))
            failedOnlineTransitions.TryAdd(id, 0);
    }

    private async Task RecordOnlineTransitionAsync(RuntimeAuthorityState state, bool recovered, CancellationToken ct,
        LicenseState? previous = null)
    {
        if (state.TransitionKind?.StartsWith("online-", StringComparison.Ordinal) != true) return;
        DemandOnlineAudit();
        // Startup reconciliation precedes ordinary foundation initialization. Reuse the
        // store's idempotent initializer; do not reorder the frozen startup boundary.
        await auditStore!.InitializeAsync(ct);
        var transitionId = state.TransitionId!.Value.ToString("D");
        var activationId = (licensing as FileProductLicenseService)?.ReadOnlineActivationForAudit();
        var verification = licensing.CurrentVerification;
        await auditStore.WriteAsync(new AuditEvent(
            Guid.NewGuid(), state.AuthorityChangedAtUtc ?? timeProvider.GetUtcNow(),
            "host:product-online-licensing", "Product online licensing", "product-license.online-transition",
            AuditOutcome.Succeeded, "runtime-authority-transition", transitionId,
            new Dictionary<string, string>
            {
                ["operation"] = state.TransitionKind!,
                ["evidenceSource"] = "online",
                ["previousVerificationState"] = previous?.ToString() ?? "unavailable",
                ["target"] = verification.State switch
                {
                    LicenseState.Demo => "demo",
                    LicenseState.Invalid => "invalid",
                    _ => "online"
                },
                ["previousEpoch"] = state.TransitionBaseAuthorityRevision!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["currentEpoch"] = state.AuthorityRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["activationId"] = activationId?.ToString("D") ?? "unavailable",
                ["licenseState"] = verification.State.ToString(),
                ["recovered"] = recovered ? "true" : "false"
            }, transitionId, Roles: [], Source: "product-license-lifecycle"), ct);
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
