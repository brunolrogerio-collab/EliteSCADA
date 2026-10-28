using Scada.Api.Security;
using Scada.Core.Product.Licensing;

namespace Scada.Api.Licensing;

internal sealed record OnlineLicensePrepareRequest(string Username, string Password, string LicenseCode);
internal sealed record OnlineLicenseConfirmationRequest(Guid ActivationId);

internal static class OnlineLicenseProductApi
{
    internal static void MapOnlineLicenseConfirmationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/licensing/online");
        group.MapPost("/prepare", async (OnlineLicensePrepareRequest request, HttpContext context, ApiAuthorizationService security) =>
        {
            var authorization = ProductLicensingApi.CheckMutationAuthorization(context, security);
            if (authorization.FailureResult() is { } failure) return failure;
            context.Response.Headers.CacheControl = "no-store";
            var device = context.RequestServices.GetService<OnlineLicenseDeviceOrchestrator>();
            if (device is null) return Unavailable();
            try
            {
                var prepared = await device.PrepareConfirmationAsync(request.Username, request.Password, request.LicenseCode,
                    new(authorization.Principal, authorization.Decision!), context.RequestAborted);
                return Results.Ok(Project(prepared));
            }
            catch (OnlineLicenseProtocolException ex) { return ProtocolFailure(ex); }
        });

        group.MapGet("/preparation", async (HttpContext context, ApiAuthorizationService security) =>
        {
            var authorization = ProductLicensingApi.CheckMutationAuthorization(context, security);
            if (authorization.FailureResult() is { } failure) return failure;
            context.Response.Headers.CacheControl = "no-store";
            var device = context.RequestServices.GetService<OnlineLicenseDeviceOrchestrator>();
            if (device is null) return Unavailable();
            try { return Results.Ok(Project(await device.ReadPreparedConfirmationAsync(new(authorization.Principal, authorization.Decision!), context.RequestAborted))); }
            catch (OnlineLicenseProtocolException ex) { return ProtocolFailure(ex); }
        });

        group.MapPost("/confirm", async (OnlineLicenseConfirmationRequest request, HttpContext context,
            ApiAuthorizationService security, IProductLicenseService licensing, AssetProcessIntelligenceExecutionGate intelligenceGate) =>
        {
            var authorization = ProductLicensingApi.CheckMutationAuthorization(context, security);
            if (authorization.FailureResult() is { } failure) return failure;
            context.Response.Headers.CacheControl = "no-store";
            var device = context.RequestServices.GetService<OnlineLicenseDeviceOrchestrator>();
            if (device is null) return Unavailable();
            try
            {
                await device.ConfirmAsync(request.ActivationId, new(authorization.Principal, authorization.Decision!), context.RequestAborted);
                intelligenceGate.InvalidateAll();
                return Results.Ok(new { installed = true, license = ProductLicensingApi.DescribeLicense(licensing.CurrentVerification) });
            }
            catch (OnlineLicenseProtocolException ex) { return ProtocolFailure(ex); }
        });

        group.MapPost("/cancel", async (OnlineLicenseConfirmationRequest request, HttpContext context, ApiAuthorizationService security) =>
        {
            var authorization = ProductLicensingApi.CheckMutationAuthorization(context, security);
            if (authorization.FailureResult() is { } failure) return failure;
            context.Response.Headers.CacheControl = "no-store";
            var device = context.RequestServices.GetService<OnlineLicenseDeviceOrchestrator>();
            if (device is null) return Unavailable();
            try
            {
                await device.CancelPreparedConfirmationAsync(request.ActivationId, new(authorization.Principal, authorization.Decision!), context.RequestAborted);
                return Results.NoContent();
            }
            catch (OnlineLicenseProtocolException ex) { return ProtocolFailure(ex); }
        });

        group.MapPost("/recover", async (HttpContext context, ApiAuthorizationService security,
            IProductLicenseService licensing, AssetProcessIntelligenceExecutionGate intelligenceGate) =>
        {
            var authorization = ProductLicensingApi.CheckMutationAuthorization(context, security);
            if (authorization.FailureResult() is { } failure) return failure;
            context.Response.Headers.CacheControl = "no-store";
            var device = context.RequestServices.GetService<OnlineLicenseDeviceOrchestrator>();
            var journal = context.RequestServices.GetService<FileOnlineLicenseStateStore>();
            if (device is null || journal is null) return Unavailable();
            try
            {
                var status = await journal.ReadStatusAsync(context.RequestAborted);
                if (status is not { InstallationPending: true })
                    throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
                await device.ConfirmAsync(status.ActivationId, new(authorization.Principal, authorization.Decision!), context.RequestAborted);
                intelligenceGate.InvalidateAll();
                return Results.Ok(new { installed = true, license = ProductLicensingApi.DescribeLicense(licensing.CurrentVerification) });
            }
            catch (OnlineLicenseProtocolException ex) { return ProtocolFailure(ex); }
        });

        group.MapPost("/reset", async (HttpContext context, ApiAuthorizationService security,
            AssetProcessIntelligenceExecutionGate intelligenceGate) =>
        {
            var authorization = ProductLicensingApi.CheckMutationAuthorization(context, security);
            if (authorization.FailureResult() is { } failure) return failure;
            context.Response.Headers.CacheControl = "no-store";
            var device = context.RequestServices.GetService<OnlineLicenseDeviceOrchestrator>();
            var journal = context.RequestServices.GetService<FileOnlineLicenseStateStore>();
            if (device is null || journal is null) return Unavailable();
            try
            {
                var status = await journal.ReadStatusAsync(context.RequestAborted);
                if (status is null || status.IsReset || status.PendingRequestId is not null || status.InstallationPending)
                    throw new OnlineLicenseProtocolException("ONLINE_LICENSE_STATE_CONFLICT");
                await device.ResetAsync(status.ActivationId, new(authorization.Principal, authorization.Decision!), context.RequestAborted);
                intelligenceGate.InvalidateAll();
                return Results.Ok(new { reset = true, remoteRevocation = false });
            }
            catch (OnlineLicenseProtocolException ex) { return ProtocolFailure(ex); }
        });
    }

    private static object Project(OnlineLicensePrepared prepared) => new {
        prepared.ActivationId, prepared.ExpiresAtUtc, prepared.OnlineActivationAvailable, prepared.ConfirmationRequested,
        summary = new { prepared.Summary.ApplicationName, prepared.Summary.LicenseCode, prepared.Summary.Mode,
            prepared.Summary.Term, prepared.Summary.EntitlementExpiresAtUtc, prepared.Summary.Tier,
            prepared.Summary.InteractiveSeats, prepared.Summary.ViewOnlySeats, prepared.Summary.HaRuntime,
            prepared.Summary.AssetProcessIntelligence, prepared.Summary.MaxActivations }
    };

    private static IResult ProtocolFailure(OnlineLicenseProtocolException ex) => Results.Json(new { error = ex.Code },
        statusCode: ex.Code is "ONLINE_LICENSE_PORTAL_UNAVAILABLE" or "ONLINE_LICENSE_ISSUANCE_UNAVAILABLE" ? 503 : 409);
    private static IResult Unavailable() => Results.Json(new { error = "ONLINE_LICENSE_NOT_CONFIGURED" }, statusCode: 503);
}
