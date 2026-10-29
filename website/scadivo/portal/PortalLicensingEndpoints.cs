using Microsoft.AspNetCore.RateLimiting;

namespace Scadivo.Portal;

public sealed class PortalDeviceLicenseEndpoint { }

public static class PortalLicensingEndpoints
{
    public static void MapPortalLicensingEndpoints(this WebApplication app)
    {
        var customer = app.MapGroup("/api/licensing").RequireAuthorization().RequireRateLimiting("writes");
        customer.MapGet("/status", (Licensing.IOnlineLeaseSigner signer) => Results.Ok(new { onlineActivationAvailable = signer.IsEnabled }));
        customer.MapGet("/organizations", (HttpContext context, PortalLicensingOperations operations) => operations.Organizations(context));
        customer.MapPost("/organizations", (CreateOrganizationInput input, HttpContext context, PortalLicensingOperations operations) => operations.CreateOrganization(input, context));
        customer.MapGet("/organizations/{organizationId:guid}/applications", (Guid organizationId, HttpContext context, PortalLicensingOperations operations) => operations.Applications(organizationId, context));
        customer.MapPost("/organizations/{organizationId:guid}/applications", (Guid organizationId, CreateProductApplicationInput input, HttpContext context, PortalLicensingOperations operations) => operations.CreateApplication(organizationId, input, context));
        customer.MapGet("/organizations/{organizationId:guid}/members", (Guid organizationId, HttpContext context, PortalLicensingOperations operations) => operations.Members(organizationId, context));
        customer.MapPost("/organizations/{organizationId:guid}/members", (Guid organizationId, AddOrganizationMemberInput input, HttpContext context, PortalLicensingOperations operations) => operations.AddMember(organizationId, input, context));
        customer.MapDelete("/organizations/{organizationId:guid}/members/{memberUserId}", (Guid organizationId, string memberUserId, HttpContext context, PortalLicensingOperations operations) => operations.RemoveMember(organizationId, memberUserId, context));
        customer.MapGet("/licenses", (HttpContext context, PortalLicensingOperations operations) => operations.MyLicenses(context));
        customer.MapGet("/activations", (HttpContext context, PortalLicensingOperations operations) => operations.MyActivations(context));
        customer.MapDelete("/activations/{activationId:guid}", (Guid activationId, HttpContext context, PortalLicensingOperations operations) => operations.DeactivateMyActivation(activationId, context));

        var admin = app.MapGroup("/api/admin/licensing").RequireAuthorization(PortalAccess.AdministratorPolicy).RequireRateLimiting("writes");
        admin.MapGet("/organizations", (PortalLicensingOperations operations) => operations.AdminOrganizations());
        admin.MapGet("/licenses", (PortalLicensingOperations operations) => operations.AdminLicenses());
        admin.MapPost("/licenses", (IssueProductLicenseInput input, HttpContext context, PortalLicensingOperations operations) => operations.IssueLicense(input, context));
        admin.MapPost("/licenses/{licenseId:guid}/extend", (Guid licenseId, ExtendProductLicenseInput input, HttpContext context, PortalLicensingOperations operations) => operations.ExtendLicense(licenseId, input, context));
        admin.MapPost("/licenses/{licenseId:guid}/revoke", (Guid licenseId, RevokeProductLicenseInput input, HttpContext context, PortalLicensingOperations operations) => operations.RevokeLicense(licenseId, input, context));
        admin.MapGet("/licenses/{licenseId:guid}/activations", (Guid licenseId, PortalLicensingOperations operations) => operations.AdminActivations(licenseId));
        admin.MapPost("/activations/{activationId:guid}/revoke", (Guid activationId, RevokeProductLicenseInput input, HttpContext context, PortalLicensingOperations operations) => operations.RevokeAdminActivation(activationId, input, context));

        var device = app.MapGroup("/api/licensing/device").WithMetadata(new PortalDeviceLicenseEndpoint());
        device.MapPost("/prepare", (DevicePrepareInput input, HttpContext context, PortalLicensingOperations operations) => operations.Prepare(input, context)).RequireRateLimiting("licensing-activate");
        device.MapPost("/confirm", (DeviceConfirmInput input, HttpContext context, PortalLicensingOperations operations) => operations.Confirm(input, context)).RequireRateLimiting("licensing-renew");
        device.MapPost("/cancel", (DeviceCancelInput input, HttpContext context, PortalLicensingOperations operations) => operations.Cancel(input, context)).RequireRateLimiting("licensing-renew");
        device.MapPost("/activate", () => Results.Json(new { error = "activation-confirmation-required" }, statusCode: 410));
        device.MapPost("/activations/{activationId:guid}/renew", (Guid activationId, DeviceRenewalInput input, HttpContext context, PortalLicensingOperations operations) => operations.Renew(activationId, input, context))
            .RequireRateLimiting("licensing-renew");
    }
}
