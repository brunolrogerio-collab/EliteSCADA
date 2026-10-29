using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Licensing;

internal sealed record OnlineLicenseMaintenanceResult(string Code, int CollectedSnapshots = 0);

/// <summary>Owns the journal operation lease through HTTP completion and canonical entitlement application.</summary>
public sealed class OnlineLicenseDeviceOrchestrator(
    FileOnlineLicenseStateStore journal,
    OnlineLicenseDeviceClient client,
    IProductLicenseService licensing,
    ProductLicenseLifecycleCoordinator lifecycle,
    IAuditStore audit,
    TimeProvider clock)
{
    internal Task<OnlineLicensePrepared> PrepareConfirmationAsync(string username, string password, string licenseCode,
        ProtectedMaterialMutationContext administration, CancellationToken ct = default)
    {
        DemandDeviceAdministration(administration);
        var source = RequireSource();
        return client.PrepareConfirmationAsync(username, password, licenseCode, administration, ct,
            status => {
                if (status is { IsReset: false } &&
                    source.ReadOnlineInstallation()?.ActivationId != status.ActivationId)
                    throw new OnlineLicenseProtocolException("ONLINE_LICENSE_SELECTION_CHANGED");
                return source.ReadInstallationChangeIdentityStrict();
            });
    }

    internal async Task<OnlineLicensePrepared> ReadPreparedConfirmationAsync(ProtectedMaterialMutationContext administration,
        CancellationToken ct = default)
    {
        DemandDeviceAdministration(administration);
        var state = await journal.ReadTransportAsync("confirmation", ct);
        var pending = state.Pending!;
        return new(state.Binding.ActivationId, pending.Presentation!, pending.ConfirmationExpiresAtUtc!.Value,
            pending.OnlineActivationAvailable, pending.ConfirmationRequested, pending.Reactivation);
    }

    internal async Task<OnlineLicenseDeviceStatus> ConfirmAsync(Guid activationId,
        ProtectedMaterialMutationContext administration, CancellationToken ct = default)
    {
        DemandDeviceAdministration(administration);
        await using var operation = journal.AcquireOperation(ct);
        var existing = await journal.ReadStatusAsync(ct);
        if (existing is null || existing.ActivationId != activationId || existing.IsReset)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        var snapshot = await journal.ReadInstallationSnapshotAsync(activationId, ct);
        var source = RequireSource();
        if (!snapshot.HasSnapshot || source.ReadInstallationChangeIdentityStrict() != snapshot.ExpectedIdentity)
        {
            if (!existing.HasLease || !source.IsSelectedOnlineCandidate(await journal.ReadEntitlementCandidateAsync(ct)))
                throw new OnlineLicenseProtocolException("ONLINE_LICENSE_SELECTION_CHANGED");
        }
        var result = existing.ConfirmationPending ? await client.ConfirmWhileOwnedAsync(activationId, administration, ct)
            : existing.HasLease ? existing : throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        await ApplyConfirmedCandidateWhileOwnedAsync(activationId, administration, snapshot, ct);
        return result;
    }

    private async Task ApplyConfirmedCandidateWhileOwnedAsync(Guid activationId,
        ProtectedMaterialMutationContext administration, (bool HasSnapshot, string? ExpectedIdentity) snapshot,
        CancellationToken ct)
    {
        var candidate = await journal.ReadEntitlementCandidateAsync(ct);
        var source = RequireSource();
        if (!source.IsSelectedOnlineCandidate(candidate))
        {
            if (source.ReadInstallationChangeIdentityStrict() != snapshot.ExpectedIdentity)
                throw new OnlineLicenseProtocolException("ONLINE_LICENSE_SELECTION_CHANGED");
            var selected = source.ReadOnlineInstallation();
            var applied = selected?.ActivationId == activationId
                ? await lifecycle.RenewOnlineAsync(candidate, selected.Revision, ct)
                : await lifecycle.InstallOnlineAsync(candidate, ct);
            if (!applied.Succeeded) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_APPLICATION_PENDING");
        }
        else await lifecycle.ReconcilePendingAsync(ct);
        if (snapshot.HasSnapshot) await journal.ClearInstallationSnapshotAsync(activationId, administration, ct);
        await journal.CollectObsoleteSnapshotsWhileOwnedAsync(administration, ct);
    }

    internal async Task<OnlineLicenseCancelResult> CancelPreparedConfirmationAsync(Guid activationId,
        ProtectedMaterialMutationContext administration, CancellationToken ct = default)
    {
        DemandDeviceAdministration(administration);
        await using var operation = journal.AcquireOperation(ct);
        var state = await journal.ReadStatusAsync(ct);
        if (state is null || state.ActivationId != activationId || state.IsReset)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
        var snapshot = await journal.ReadInstallationSnapshotAsync(activationId, ct);
        var result = await client.CancelPreparedWhileOwnedAsync(activationId, administration, ct);
        if (result == OnlineLicenseCancelResult.AlreadyActivated)
        {
            await ApplyConfirmedCandidateWhileOwnedAsync(activationId, administration, snapshot, ct);
        }
        return result;
    }

    internal async Task AbandonAmbiguousConfirmationAsync(Guid activationId, ProtectedMaterialMutationContext administration,
        CancellationToken ct = default)
    {
        DemandDeviceAdministration(administration);
        await using var operation = journal.AcquireOperation(ct);
        await journal.AbandonAmbiguousConfirmationWhileOwnedAsync(activationId, administration, ct);
        await audit.WriteAsync(new AuditEvent(Guid.NewGuid(), clock.GetUtcNow(), administration.Principal.SubjectId,
            administration.Principal.DisplayName, "product-license.online-abandon-ambiguous", AuditOutcome.Succeeded,
            "online-activation", activationId.ToString("D"), new Dictionary<string, string> { ["remoteRevocation"] = "not-requested" },
            activationId.ToString("D"), Roles: administration.Principal.Roles, Source: "product-license-device"), CancellationToken.None);
        await journal.CollectObsoleteSnapshotsWhileOwnedAsync(administration, CancellationToken.None);
    }

    internal async Task<OnlineLicenseDeviceStatus> ActivateAsync(OnlineLicenseSelection selection, string username,
        string password, ProtectedMaterialMutationContext administration, CancellationToken ct = default)
    {
        DemandDeviceAdministration(administration);
        await using var operation = journal.AcquireOperation(ct);
        var existing = await journal.ReadStatusAsync(ct);
        OnlineLicenseDeviceStatus result;
        if (existing is { HasLease: true, IsReset: false, ReauthenticationRequired: false })
        {
            var committed = await journal.ReadEntitlementCandidateAsync(ct);
            if (committed.Binding.LicenseId != selection.LicenseId || committed.Binding.OrganizationId != selection.OrganizationId ||
                committed.Binding.ApplicationId != selection.ApplicationId)
                throw new OnlineLicenseProtocolException("ONLINE_LICENSE_SELECTION_CHANGED");
            result = existing;
        }
        else result = await client.ActivateWhileOwnedAsync(selection, username, password, administration, ct);
        var candidate = await journal.ReadEntitlementCandidateAsync(ct);
        if (RequireSource().IsSelectedOnlineCandidate(candidate)) return result;
        var applied = await lifecycle.InstallOnlineAsync(candidate, ct);
        if (!applied.Succeeded) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_APPLICATION_PENDING");
        await journal.CollectObsoleteSnapshotsWhileOwnedAsync(administration, ct);
        return result;
    }

    internal async Task<OnlineLicenseDeviceStatus> ReauthenticateAsync(OnlineLicenseSelection selection, string username,
        string password, ProtectedMaterialMutationContext administration, CancellationToken ct = default)
    {
        DemandDeviceAdministration(administration);
        await using var operation = journal.AcquireOperation(ct);
        var existing = await journal.ReadStatusAsync(ct);
        var result = existing is { HasLease: true, IsReset: false, ReauthenticationRequired: false }
            ? existing : await client.ReauthenticateWhileOwnedAsync(selection, username, password, administration, ct);
        var source = RequireSource();
        var selected = source.ReadOnlineInstallation();
        if (selected is null || selected.ActivationId != result.ActivationId)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_SELECTION_CHANGED");
        var candidate = await journal.ReadEntitlementCandidateAsync(ct);
        if (candidate.Binding.LicenseId != selection.LicenseId || candidate.Binding.OrganizationId != selection.OrganizationId ||
            candidate.Binding.ApplicationId != selection.ApplicationId)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_SELECTION_CHANGED");
        if (source.IsSelectedOnlineCandidate(candidate)) return result;
        await ApplyCommittedCandidateAsync(source, selected, ct);
        return result;
    }

    internal async Task ResetAsync(Guid expectedActivationId, ProtectedMaterialMutationContext administration, CancellationToken ct = default)
    {
        DemandDeviceAdministration(administration);
        await using var operation = journal.AcquireOperation(ct);
        var status = await journal.ReadStatusAsync(ct);
        var source = RequireSource();
        var selected = source.ReadOnlineInstallation();
        if (status?.ActivationId != expectedActivationId ||
            (selected is null ? source.ReadInstallationChangeIdentity() is not null : selected.ActivationId != expectedActivationId))
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_SELECTION_CHANGED");
        // Rights disappear first. Interrupted journal reset cannot re-select an absent mode.
        var removed = await lifecycle.RemoveWhileDeviceOperationOwnedAsync(ct);
        if (!removed.Succeeded) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_APPLICATION_PENDING");
        await journal.ResetWhileOwnedAsync(expectedActivationId, administration, CancellationToken.None);
        await audit.WriteAsync(new AuditEvent(Guid.NewGuid(), clock.GetUtcNow(), administration.Principal.SubjectId,
            administration.Principal.DisplayName, "product-license.online-reset", AuditOutcome.Succeeded,
            "online-activation", expectedActivationId.ToString("D"), new Dictionary<string, string>
            {
                ["previousEpoch"] = removed.PreviousAuthorityRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["currentEpoch"] = removed.CurrentAuthorityRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["remoteRevocation"] = "not-requested"
            }, expectedActivationId.ToString("D"), Roles: administration.Principal.Roles, Source: "product-license-device"), CancellationToken.None);
        await journal.CollectObsoleteSnapshotsWhileOwnedAsync(administration, CancellationToken.None);
    }

    internal async Task<OnlineLicenseMaintenanceResult> RunOnceAsync(CancellationToken ct = default)
    {
        await using var operation = journal.AcquireOperation(ct);
        var source = RequireSource();
        var selected = source.ReadOnlineInstallation();
        if (selected is null) return new("not-selected");
        var observed = await journal.ReadMaintenanceStatusAsync(ct);
        if (observed.Status.IsReset || observed.Status.ActivationId != selected.ActivationId)
            return new("selection-changed");
        var maintenance = await journal.ReadMaintenanceDelegationAsync(ct);

        // Persisted response takes precedence over another credential rotation. Applying
        // a response may have failed after HTTP or the process may have stopped there.
        try
        {
            var candidate = await journal.ReadEntitlementCandidateAsync(ct);
            if (!source.IsSelectedOnlineCandidate(candidate))
            {
                await ApplyCommittedCandidateAsync(source, selected, ct);
                return new("applied-persisted-response", await journal.CollectObsoleteSnapshotsWhileOwnedAsync(maintenance, ct));
            }
        }
        catch (OnlineLicenseProtocolException ex) when (ex.Code == "ONLINE_LICENSE_LEASE_REJECTED")
        {
            // An expired journal is still renewable; it provides no current authority.
        }

        if (observed.Status.ReauthenticationRequired) return new("reauthentication-required");
        if (observed.Status.RenewalDueUtc is not { } due || selected.ObservedUtc < due) return new("not-due");
        if (observed.NextAttemptUtc is { } next && selected.ObservedUtc < next) return new("retry-delayed");
        // Commit pacing before network so response loss/restart cannot create a rapid loop.
        // This timestamp controls transport attempts, never entitlement validity.
        await journal.DelayMaintenanceAsync(clock.GetUtcNow().AddMinutes(5), maintenance, ct);
        await journal.CollectObsoleteSnapshotsWhileOwnedAsync(maintenance, ct);
        var correlation = Guid.NewGuid().ToString("D");
        try
        {
            await client.RenewWhileOwnedAsync(maintenance, ct);
            // The independent guard may have expired the previous lease while HTTP ran.
            var current = source.ReadOnlineInstallation();
            if (current is null || current.ActivationId != observed.Status.ActivationId)
                throw new OnlineLicenseProtocolException("ONLINE_LICENSE_SELECTION_CHANGED");
            await ApplyCommittedCandidateAsync(source, current, ct);
            var collected = await journal.CollectObsoleteSnapshotsWhileOwnedAsync(maintenance, ct);
            await RecordAttemptAsync(observed.Status.ActivationId, correlation, "renewed", AuditOutcome.Succeeded, ct);
            return new("renewed", collected);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var code = ex is OnlineLicenseProtocolException protocol ? protocol.Code : ex.GetType().Name;
            await RecordAttemptAsync(observed.Status.ActivationId, correlation, code, AuditOutcome.Failed, CancellationToken.None);
            throw;
        }
    }

    private async Task ApplyCommittedCandidateAsync(FileProductLicenseService source, OnlineProductLicenseInstallation selected, CancellationToken ct)
    {
        var candidate = await journal.ReadEntitlementCandidateAsync(ct);
        if (candidate.Binding.ActivationId != selected.ActivationId)
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_SELECTION_CHANGED");
        var applied = await lifecycle.RenewOnlineAsync(candidate, selected.Revision, ct);
        if (!applied.Succeeded) throw new OnlineLicenseProtocolException("ONLINE_LICENSE_APPLICATION_PENDING");
    }

    private FileProductLicenseService RequireSource() => licensing as FileProductLicenseService ??
        throw new InvalidOperationException("Canonical online license source is unavailable.");

    private static void DemandDeviceAdministration(ProtectedMaterialMutationContext administration)
    {
        administration.DemandAuthorized();
        if (administration.Authorization.Capability is not (SecurityCapability.EngineeringModify or SecurityCapability.SystemAdmin))
            throw new OnlineLicenseProtocolException("ONLINE_LICENSE_ADMINISTRATION_REQUIRED");
    }

    private async Task RecordAttemptAsync(Guid activationId, string correlation, string result, AuditOutcome outcome, CancellationToken ct) =>
        await audit.WriteAsync(new AuditEvent(Guid.NewGuid(), clock.GetUtcNow(), "host:online-license-maintenance",
            "Online license maintenance", "product-license.online-renewal", outcome, "online-activation", activationId.ToString("D"),
            new Dictionary<string, string> { ["result"] = result }, correlation, Roles: [], Source: "product-license-maintenance"), ct);
}

public sealed class OnlineLicenseRenewalHostedService(OnlineLicenseDeviceOrchestrator device, TimeProvider clock,
    ILogger<OnlineLicenseRenewalHostedService> logger) : BackgroundService
{
    private string? lastError;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await device.RunOnceAsync(stoppingToken); lastError = null; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                var code = ex is OnlineLicenseProtocolException protocol ? protocol.Code : ex.GetType().Name;
                if (lastError != code)
                    logger.LogWarning("Online license renewal could not complete: {Code}. The local expiry guard remains independent.", code);
                lastError = code;
            }
            try { await Task.Delay(TimeSpan.FromSeconds(30), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
