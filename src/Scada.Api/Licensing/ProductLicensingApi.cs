using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Licensing;

public sealed record ProductLicenseInstallRequest(string LicenseCode);

public static class ProductLicensingApi
{
    public static void MapProductLicensingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/licensing");

        group.MapGet("/status", (
            IProductLicenseService licensing,
            IProductRuntimeStatusProvider runtimeStatus) =>
        {
            var verification = licensing.CurrentVerification;
            return Results.Ok(new
            {
                license = DescribeLicense(verification),
                runtime = DescribeRuntime(runtimeStatus.GetProductRuntimeStatus())
            });
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

    private static object DescribeLicense(LicenseVerificationResult verification)
    {
        var license = verification.License;
        var maximumTags = verification.State switch
        {
            LicenseState.Demo => LicensingPolicy.DemoMaxTags,
            LicenseState.Valid when license is not null => LicensingPolicy.MaximumTags(license.Tier),
            _ => null
        };

        return new
        {
            state = verification.State.ToString(),
            tier = license?.Tier.ToString(),
            maximumTags,
            demoMaximumContinuousMinutes = verification.State == LicenseState.Demo
                ? LicensingPolicy.DemoMaxContinuousRun.TotalMinutes
                : (double?)null,
            licenseId = license?.LicenseId,
            issuedAtUtc = license?.IssuedAtUtc,
            notAfterUtc = license?.NotAfterUtc,
            keyId = license?.KeyId,
            diagnostic = verification.Diagnostic
        };
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
