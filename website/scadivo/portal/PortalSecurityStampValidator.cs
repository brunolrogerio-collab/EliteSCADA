using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed class PortalSecurityStampValidator(
    IOptions<SecurityStampValidatorOptions> options,
    SignInManager<PortalUser> signInManager,
    ILoggerFactory logger) : SecurityStampValidator<PortalUser>(options, signInManager, logger)
{
    protected override async Task<PortalUser?> VerifySecurityStamp(ClaimsPrincipal? principal)
    {
        var user = await base.VerifySecurityStamp(principal);
        return user is { IsSuspended: false } ? user : null;
    }
}
