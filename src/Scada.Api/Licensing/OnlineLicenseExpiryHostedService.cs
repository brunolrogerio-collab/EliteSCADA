using System.Text.Json;
using Scada.Core.Product.Licensing;

namespace Scada.Api.Licensing;

internal sealed record OnlineLicenseExpiryGuardResult(string Code, TimeSpan NextCheck);

/// <summary>
/// Local safety guard independent from network renewal. Uses the canonical license and
/// authority lifecycle; it never reads credentials, assigns roles or calls the Portal.
/// </summary>
public sealed class OnlineLicenseExpiryHostedService(
    IProductLicenseService licensing,
    ProductLicenseLifecycleCoordinator lifecycle,
    IProductRuntimeAuthorityReevaluator runtime,
    TimeProvider clock,
    ILogger<OnlineLicenseExpiryHostedService> logger) : BackgroundService
{
    internal static readonly TimeSpan MaximumCheckInterval = TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan CheckpointInterval = TimeSpan.FromMinutes(1);
    private readonly SemaphoreSlim tick = new(1, 1);
    private readonly FileProductLicenseService? source = licensing as FileProductLicenseService;
    private bool observedOnline;
    private string? lastInvalidIdentity;
    private bool invalidationApplied;
    private DateTimeOffset? lastCheckpoint;
    private string? lastReportedError;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (source is null) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await RunOnceAsync(stoppingToken);
            try { await Task.Delay(result.NextCheck, clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    internal async Task<OnlineLicenseExpiryGuardResult> RunOnceAsync(CancellationToken ct = default)
    {
        await tick.WaitAsync(ct);
        try
        {
            if (source is null) return Result("not-applicable");
            // Never reconcile a transition merely because it is pending: another API or
            // process may still own it. Only failed operations tracked by this instance.
            if (await lifecycle.ReconcileFailedOnlineAsync(ct) == "online-invalid")
            {
                lastInvalidIdentity = source.ReadInstallationChangeIdentity();
                invalidationApplied = true;
            }
            if (!source.HasSelectedOnlineInstallation())
            {
                if (observedOnline && licensing.CurrentVerification.State == LicenseState.Invalid)
                    return await InvalidateAsync(ct);
                observedOnline = false;
                invalidationApplied = false;
                lastCheckpoint = null;
                lastReportedError = null;
                return Result("not-online");
            }
            observedOnline = true;
            var authority = await lifecycle.ReadAuditStateAsync();
            if (authority.Authority.TransitionPending) return Result("transition-in-progress");
            OnlineProductLicenseInstallation selected;
            try { selected = source.ReadOnlineInstallation()!; }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                return await InvalidateAsync(ct);
            }
            if (selected.Status == OnlineLeaseVerificationStatus.Invalid ||
                selected.ExpiryApplied && selected.Status != OnlineLeaseVerificationStatus.Expired)
                return await InvalidateAsync(ct);
            invalidationApplied = false;
            lastInvalidIdentity = null;
            if (selected.Status == OnlineLeaseVerificationStatus.Expired && !selected.ExpiryApplied)
            {
                var result = await lifecycle.ExpireOnlineAsync(selected.Revision, ct);
                if (!result.Succeeded) return Result("selection-changed");
                lastCheckpoint = null;
                lastReportedError = null;
                return Result("expired-to-demo");
            }
            if (lastCheckpoint is null || selected.ObservedUtc - lastCheckpoint >= CheckpointInterval)
            {
                source.CheckpointOnlineClock(selected.Revision);
                lastCheckpoint = selected.ObservedUtc;
            }
            lastReportedError = null;
            var remaining = selected.NotAfterUtc - selected.ObservedUtc;
            return new(selected.ExpiryApplied ? "online-demo" : "valid",
                remaining > TimeSpan.Zero && remaining < MaximumCheckInterval ? remaining : MaximumCheckInterval);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // A shared-authority failure must not leave a locally invalid commercial Run
            // executing. Do not manufacture Demo when canonical evidence is unavailable.
            if (observedOnline && licensing.CurrentVerification.State == LicenseState.Invalid)
            {
                lifecycle.CancelUnlicensedIntelligence();
                try { await runtime.ReevaluateForAuthorityChangeAsync(clock.GetUtcNow(), CancellationToken.None); }
                catch (Exception localFailure)
                {
                    ReportError("local-fence-" + localFailure.GetType().Name);
                    return Result("local-fence-unavailable");
                }
            }
            ReportError(ex is OnlineLicenseProtocolException protocol ? protocol.Code : ex.GetType().Name);
            return Result("maintenance-unavailable");
        }
        finally { tick.Release(); }
    }

    private async Task<OnlineLicenseExpiryGuardResult> InvalidateAsync(CancellationToken ct)
    {
        var identity = source!.ReadInstallationChangeIdentity();
        if (invalidationApplied && identity == lastInvalidIdentity) return Result("online-invalid");
        var result = await lifecycle.InvalidateOnlineAsync(identity, ct);
        if (!result.Succeeded) return Result("selection-changed");
        lastInvalidIdentity = identity;
        invalidationApplied = true;
        ReportError("ONLINE_LICENSE_INVALID");
        return Result("online-invalid");
    }

    private void ReportError(string code)
    {
        if (lastReportedError == code) return;
        lastReportedError = code;
        logger.LogWarning("Online license expiry guard could not complete maintenance: {Code}. It will recheck canonical state.", code);
    }

    private static OnlineLicenseExpiryGuardResult Result(string code) => new(code, MaximumCheckInterval);
}
