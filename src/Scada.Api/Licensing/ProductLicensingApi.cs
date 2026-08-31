using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Api.Security;
using Scada.Core.Abstractions;
using Scada.Core.Product;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Security.Authorization;
using Scada.Security.Licensing;

namespace Scada.Api.Licensing;

public static class ProductLicensingApi
{
    public static void AddProductLicensedEngineeringRuntime(this WebApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<IHardwareFingerprintProvider, DefaultHardwareFingerprintProvider>();
        builder.Services.TryAddSingleton<IProductLicenseManager>(sp =>
        {
            var configuredPath = builder.Configuration["ProductLicense:Path"];
            var licensePath = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(AppContext.BaseDirectory, "license.escadalicense")
                : configuredPath;

            var publicKeyPem = builder.Configuration["ProductLicense:TrustedPublicKeyPem"];
            return new FileProductLicenseService(
                sp.GetRequiredService<IHardwareFingerprintProvider>(),
                licensePath,
                publicKeyPem);
        });
        builder.Services.TryAddSingleton<IProductLicenseService>(sp =>
            sp.GetRequiredService<IProductLicenseManager>());

        builder.Services.AddSingleton<ProductLicensedRuntimeCoordinator>(sp =>
            new ProductLicensedRuntimeCoordinator(
                () => new GatewayEngineeringRuntimeCoordinator(
                    new EngineeringRuntimeCoordinator(
                        sp.GetRequiredService<IScadaEventBus>(),
                        sp.GetRequiredService<IEngineeringDriverCompiler>(),
                        TimeSpan.FromSeconds(Math.Max(
                            1,
                            builder.Configuration.GetValue<double?>("EngineeringRuntime:ActivationTimeoutSeconds") ?? 10)),
                        sp.GetRequiredService<IServerMemoryRetentionStore>()),
                    sp.GetRequiredService<IScadaEventBus>()),
                sp.GetRequiredService<IProductLicenseService>()));

        builder.Services.AddSingleton<IEngineeringRuntimeCoordinator>(sp =>
            sp.GetRequiredService<ProductLicensedRuntimeCoordinator>());
        builder.Services.AddSingleton<IGatewayRuntimeDiagnosticsProvider>(sp =>
            sp.GetRequiredService<ProductLicensedRuntimeCoordinator>());
        builder.Services.AddSingleton<IProductRuntimeLicenseStatusProvider>(sp =>
            sp.GetRequiredService<ProductLicensedRuntimeCoordinator>());
    }

    public static void MapProductLicensingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/product/license");

        group.MapGet("/status", (
            IProductLicenseManager licenses,
            IProductRuntimeLicenseStatusProvider runtime) =>
        {
            var current = licenses.Current();
            var runtimeStatus = runtime.LicenseStatus();
            return Results.Ok(new
            {
                mode = current.Mode.ToString(),
                current.HardwareRequestCode,
                current.MaxTags,
                current.UnlimitedTags,
                maxContinuousRuntimeMinutes = current.MaxContinuousRuntime?.TotalMinutes,
                current.LicenseId,
                current.Customer,
                current.Message,
                runtime = new
                {
                    runtimeStatus.RuntimeActive,
                    runtimeStatus.RuntimeStartedAtUtc,
                    runtimeStatus.RuntimeExpiresAtUtc,
                    runtimeStatus.LastRuntimeIssueCode,
                    runtimeStatus.LastRuntimeMessage
                }
            });
        });

        group.MapPost("/install", (
            ProductLicenseInstallRequest request,
            HttpContext context,
            IProductLicenseManager licenses,
            ApiAuthorizationService security) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            if (string.IsNullOrWhiteSpace(request.LicenseCode))
                return Results.BadRequest(new { error = "License code is required." });

            var result = licenses.Install(request.LicenseCode);
            return result.Installed
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status422UnprocessableEntity);
        });

        group.MapDelete("/installed", (
            HttpContext context,
            IProductLicenseManager licenses,
            ApiAuthorizationService security) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            return Results.Ok(licenses.Remove());
        });
    }
}

public sealed record ProductLicenseInstallRequest(string LicenseCode);
