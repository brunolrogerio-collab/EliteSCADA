using Scada.Api.Security;
using Scada.Api.Runtime;
using Scada.Core.Product.Licensing;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Licensing;

public sealed record ProductLicenseInstallRequest(string LicenseCode);
internal sealed record ProductLicenseStatusResponse(ProductLicenseStatusProjection License, object Runtime,
    ProductRuntimeUsage? Usage, RuntimeSessionSeatCapacity? SessionLimits,
    ProductOnlineLicenseDeviceStatusProjection OnlineDevice);

internal sealed record ProductOnlineLicenseDeviceStatusProjection(
    bool Configured,
    bool Available,
    bool HasActivation,
    bool LeasePresent,
    DateTimeOffset? IssuedAtUtc,
    DateTimeOffset? NotAfterUtc,
    DateTimeOffset? RenewalDueUtc,
    bool RequestPending,
    bool ClockRollbackDetected,
    bool IsReset,
    bool ReauthenticationRequired,
    bool InstallationPending = false,
    bool ConfirmationPending = false);

public sealed record ProductLicenseStatusProjection(
    string State,
    string? Tier,
    int? MaximumTags,
    double? DemoMaximumContinuousMinutes,
    string? LicenseId,
    int? SchemaVersion,
    DateTimeOffset? IssuedAtUtc,
    DateTimeOffset? NotAfterUtc,
    string? KeyId,
    int? ViewOnlySeats,
    int? InteractiveSeats,
    bool? HaRuntime,
    string? Diagnostic,
    bool? AssetProcessIntelligence = null);

public static class ProductLicensingApi
{
    public static void MapProductLicensingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/licensing");

        group.MapGet("/status", async (
            IProductLicenseService licensing,
            IProductRuntimeStatusProvider runtimeStatus,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var onlineStateStore = context.RequestServices.GetService(typeof(FileOnlineLicenseStateStore))
                as FileOnlineLicenseStateStore;
            Func<CancellationToken, Task<OnlineLicenseDeviceStatus?>>? readOnlineState =
                onlineStateStore is null ? null : onlineStateStore.ReadStatusAsync;
            return Results.Ok(await CreateStatusAsync(licensing, runtimeStatus, cancellationToken,
                onlineStateStore is not null, readOnlineState));
        }).RequireWorkspaceEngineeringRead();

        group.MapGet("/request", (IProductLicenseService licensing) =>
            Results.Ok(new
            {
                schemaVersion = EliteScadaLicenseCodec.CurrentSchemaVersion,
                requestCode = licensing.MachineRequestCode,
                machineFingerprint = licensing.MachineFingerprint
            }))
            .RequireWorkspaceEngineeringRead();

        group.MapPost("/install", async (
            ProductLicenseInstallRequest request,
            HttpContext context,
            ProductLicenseLifecycleCoordinator lifecycle,
            IProductLicenseService licensing,
            AssetProcessIntelligenceExecutionGate intelligenceGate,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
        {
            var authorization = CheckMutationAuthorization(context, security);
            var action = licensing.CurrentVerification.State == LicenseState.Valid
                ? AuditActions.ProductLicenseReplace
                : AuditActions.ProductLicenseInstall;
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await RecordAuthorizationDeniedAsync(context, audit, authorization, action, "install-or-replace", lifecycle);
                return failure;
            }

            try
            {
                var result = await lifecycle.InstallOrReplaceAsync(request.LicenseCode, context.RequestAborted);
                if (result.Succeeded) intelligenceGate.InvalidateAll();
                await RecordLifecycleAsync(context, audit, authorization, action, result);
                return result.Succeeded ? Results.Ok(new
                {
                    installed = true,
                    license = DescribeLicense(licensing.CurrentVerification)
                }) : Results.BadRequest(new { installed = false, error = result.ReasonCode });
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                await RecordLifecycleFailureAsync(context, audit, authorization, action, lifecycle);
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        });

        group.MapDelete("/license", async (
            HttpContext context,
            ProductLicenseLifecycleCoordinator lifecycle,
            IProductLicenseService licensing,
            AssetProcessIntelligenceExecutionGate intelligenceGate,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
        {
            var authorization = CheckMutationAuthorization(context, security);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await RecordAuthorizationDeniedAsync(context, audit, authorization,
                    AuditActions.ProductLicenseRemove, "remove", lifecycle);
                return failure;
            }

            try
            {
                var result = await lifecycle.RemoveAsync(context.RequestAborted);
                if (result.Succeeded) intelligenceGate.InvalidateAll();
                await RecordLifecycleAsync(context, audit, authorization,
                    AuditActions.ProductLicenseRemove, result);
                return result.Succeeded ? Results.Ok(new
                {
                    removed = true,
                    license = DescribeLicense(licensing.CurrentVerification)
                }) : Results.BadRequest(new { removed = false, error = result.ReasonCode });
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                await RecordLifecycleFailureAsync(context, audit, authorization,
                    AuditActions.ProductLicenseRemove, lifecycle);
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        });
        app.MapOnlineLicenseConfirmationEndpoints();
    }

    internal static ApiAuthorizationCheck CheckMutationAuthorization(
        HttpContext context,
        ApiAuthorizationService security) =>
        security.CheckWorkspace(context, SecurityCapability.EngineeringModify);

    internal static async ValueTask RecordAuthorizationDeniedAsync(
        HttpContext context,
        ApiAuditService audit,
        ApiAuthorizationCheck authorization,
        string action,
        string operation,
        ProductLicenseLifecycleCoordinator? lifecycle = null)
    {
        var details = await ReadBoundedAuditStateAsync(lifecycle);
        details["operation"] = operation;
        details["resultCode"] = "authorization-denied";
        details["previousLicenseState"] = details["currentLicenseState"];
        details["previousAuthorityRevision"] = details["currentAuthorityRevision"];
        details["remoteLeasesFenced"] = "0";
        details["localRuntimeOutcome"] = "unchanged";
        await audit.RecordAuthorizationDeniedAsync(context, authorization, action,
            "product-license", "machine", details);
    }

    internal static ValueTask RecordLifecycleAsync(
        HttpContext context,
        ApiAuditService audit,
        ApiAuthorizationCheck authorization,
        string action,
        ProductLicenseLifecycleResult result) =>
        audit.RecordAsync(context, authorization.Principal, action,
            result.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            "product-license", "machine", new Dictionary<string, string>
            {
                ["operation"] = action,
                ["resultCode"] = result.ReasonCode,
                ["previousLicenseState"] = result.PreviousLicenseState.ToString(),
                ["currentLicenseState"] = result.CurrentLicenseState.ToString(),
                ["previousAuthorityRevision"] = result.PreviousAuthorityRevision.ToString(),
                ["currentAuthorityRevision"] = result.CurrentAuthorityRevision.ToString(),
                ["authorityChangedAtUtc"] = result.AuthorityChangedAtUtc?.ToString("O") ?? "none",
                ["remoteLeasesFenced"] = result.FencedLeaseCount.ToString(),
                ["localRuntimeOutcome"] = result.LocalRuntimeOutcome
            });

    internal static async ValueTask RecordLifecycleFailureAsync(
        HttpContext context,
        ApiAuditService audit,
        ApiAuthorizationCheck authorization,
        string action,
        ProductLicenseLifecycleCoordinator? lifecycle = null)
    {
        var details = await ReadBoundedAuditStateAsync(lifecycle);
        details["operation"] = action;
        details["resultCode"] = "transition-incomplete";
        details["previousLicenseState"] = "unknown";
        details["previousAuthorityRevision"] = "unknown";
        await audit.RecordAsync(context, authorization.Principal, action, AuditOutcome.Failed,
            "product-license", "machine", details);
    }

    private static async Task<Dictionary<string, string>> ReadBoundedAuditStateAsync(
        ProductLicenseLifecycleCoordinator? lifecycle)
    {
        var details = new Dictionary<string, string>
        {
            ["currentLicenseState"] = "unknown",
            ["currentAuthorityRevision"] = "unknown",
            ["authorityChangedAtUtc"] = "unknown",
            ["remoteLeasesFenced"] = "unknown",
            ["localRuntimeOutcome"] = "unknown"
        };
        if (lifecycle is null) return details;

        try
        {
            var state = await lifecycle.ReadAuditStateAsync();
            details["currentLicenseState"] = state.LicenseState.ToString();
            details["currentAuthorityRevision"] = state.Authority.AuthorityRevision.ToString();
            details["authorityChangedAtUtc"] = state.Authority.AuthorityChangedAtUtc?.ToString("O") ?? "none";
        }
        catch (Exception)
        {
            // Audit must still record the bounded failure/denial if state storage is unavailable.
        }
        return details;
    }

    internal static async Task<ProductLicenseStatusResponse> CreateStatusAsync(
        IProductLicenseService licensing, IProductRuntimeStatusProvider runtimeStatus,
        CancellationToken cancellationToken = default, bool onlineDeviceConfigured = false,
        Func<CancellationToken, Task<OnlineLicenseDeviceStatus?>>? readOnlineState = null)
    {
        var before = DescribeLicense(licensing.CurrentVerification);
        ProductRuntimeUsage? usage;
        try { usage = await runtimeStatus.GetProductRuntimeUsageAsync(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { usage = null; }
        var verification = licensing.CurrentVerification;
        var license = DescribeLicense(verification);
        if (before != license) usage = null;
        return new(license, DescribeRuntime(runtimeStatus.GetProductRuntimeStatus()), usage,
            RuntimeSessionSeatCapacityPolicy.Resolve(verification).Capacity,
            await CreateOnlineDeviceStatusAsync(onlineDeviceConfigured, readOnlineState, cancellationToken));
    }

    internal static async Task<ProductOnlineLicenseDeviceStatusProjection> CreateOnlineDeviceStatusAsync(
        bool configured, Func<CancellationToken, Task<OnlineLicenseDeviceStatus?>>? readStatus,
        CancellationToken cancellationToken = default)
    {
        if (!configured)
            return new(false, true, false, false, null, null, null, false, false, false, false);
        if (readStatus is null)
            return new(true, false, false, false, null, null, null, false, false, false, false);

        try
        {
            var status = await readStatus(cancellationToken);
            return new(
                Configured: true,
                Available: true,
                HasActivation: status is not null && !status.IsReset,
                LeasePresent: status?.HasLease == true,
                IssuedAtUtc: status?.IssuedAtUtc,
                NotAfterUtc: status?.NotAfterUtc,
                RenewalDueUtc: status?.RenewalDueUtc,
                RequestPending: status?.PendingRequestId is not null,
                ClockRollbackDetected: status?.ClockRollbackDetected == true,
                IsReset: status?.IsReset == true,
                ReauthenticationRequired: status?.ReauthenticationRequired == true,
                InstallationPending: status?.InstallationPending == true,
                ConfirmationPending: status?.ConfirmationPending == true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The online journal is optional for the licensing page. Never turn an unreadable
            // journal into an empty state or disclose its storage/cryptographic error.
            return new(true, false, false, false, null, null, null, false, false, false, false);
        }
    }

    internal static ProductLicenseStatusProjection DescribeLicense(LicenseVerificationResult verification)
    {
        ArgumentNullException.ThrowIfNull(verification);

        var license = verification.License;
        var sessionEntitlements = verification.SessionEntitlements;
        var maximumTags = verification.State switch
        {
            LicenseState.Demo => LicensingPolicy.DemoMaxTags,
            LicenseState.Valid when license is not null => LicensingPolicy.MaximumTags(license.Tier),
            _ => null
        };

        return new ProductLicenseStatusProjection(
            verification.State.ToString(),
            license?.Tier.ToString(),
            maximumTags,
            null,
            license?.LicenseId,
            license?.SchemaVersion,
            license?.IssuedAtUtc,
            license?.NotAfterUtc,
            license?.KeyId,
            sessionEntitlements?.ViewOnlySeats,
            sessionEntitlements?.InteractiveSeats,
            sessionEntitlements?.HaRuntime,
            verification.Diagnostic,
            verification.V3Entitlements?.AssetProcessIntelligence);
    }

    private static object DescribeRuntime(ProductRuntimeEntitlementStatus status) => new
    {
        state = status.State.ToString(),
        activeLicenseState = status.ActiveLicenseState?.ToString(),
        activeTier = status.ActiveTier?.ToString(),
        status.MaximumTags,
        status.DemoStartedAtUtc,
        status.DemoExpiresAtUtc,
        status.DemoRemaining,
        status.LastDiagnostic
    };
}
