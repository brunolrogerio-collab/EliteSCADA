using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.DriverHost.Engineering;

namespace Scada.Api.Licensing;

public sealed record ProductLicenseInstallRequest(string LicenseCode);

public static class ProductLicensingApi
{
    public static void MapProductLicensingEndpoints(this WebApplication app)
    {
        // Host-level Engineering metadata is mapped here because this product bootstrap
        // extension is already invoked by Program. The route itself remains an Engineering
        // read boundary and the catalog is resolved from the authoritative runtime registry.
        app.MapGet("/api/engineering/data-source-types", (EngineeringDataSourceTypeCatalog catalog) =>
            Results.Ok(catalog.Describe()))
            .RequireWorkspaceEngineeringRead();

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

        group.MapPost("/install", (
            ProductLicenseInstallRequest request,
            IProductLicenseService licensing) =>
        {
            try
            {
                licensing.InstallLicense(request.LicenseCode);
                return Results.Ok(new
                {
                    installed = true,
                    license = DescribeLicense(licensing.CurrentVerification)
                });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException)
            {
                return Results.BadRequest(new
                {
                    installed = false,
                    error = ex.Message
                });
            }
        }).RequireWorkspaceEngineeringRead();

        group.MapDelete("/license", (IProductLicenseService licensing) =>
        {
            licensing.RemoveLicense();
            return Results.Ok(new
            {
                removed = true,
                license = DescribeLicense(licensing.CurrentVerification)
            });
        }).RequireWorkspaceEngineeringRead();
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
