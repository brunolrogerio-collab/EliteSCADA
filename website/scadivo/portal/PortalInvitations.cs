using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public static class PortalInvitations
{
    public static async Task<string> LinkAsync(UserManager<PortalUser> users, PortalUser user, string publicUrl)
    {
        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user)));
        var payload = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { userId = user.Id, token }));
        return $"{publicUrl.TrimEnd('/')}/#invite={payload}";
    }

    public static async Task<string> ConfirmationLinkAsync(UserManager<PortalUser> users, PortalUser user, string publicUrl)
    {
        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GenerateEmailConfirmationTokenAsync(user)));
        var payload = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { userId = user.Id, token }));
        return $"{publicUrl.TrimEnd('/')}/#confirm-email={payload}";
    }

    public static async Task<string> PasswordResetLinkAsync(UserManager<PortalUser> users, PortalUser user, string publicUrl)
    {
        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user)));
        var payload = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { userId = user.Id, token }));
        return $"{publicUrl.TrimEnd('/')}/#reset-password={payload}";
    }
}
