using Microsoft.AspNetCore.RateLimiting;
using Scada.Security.Audit;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed record LocalLoginRequest(string Username, string Password);

public sealed record AuthProfileResponse(
    string SubjectId,
    string? Username,
    string? DisplayName,
    IReadOnlyCollection<string> Roles,
    DateTimeOffset? ExpiresAtUtc = null);

public static class LocalIdentityApi
{
    public static IEndpointRouteBuilder MapLocalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var runtime = endpoints.ServiceProvider.GetRequiredService<LocalIdentityRuntimeOptions>();

        endpoints.MapGet("/api/auth/config", () => Results.Ok(new
        {
            authenticationEnabled = runtime.AuthenticationEnabled,
            localLoginEnabled = runtime.Enabled
        }));

        if (!runtime.Enabled) return endpoints;

        endpoints.MapPost("/api/auth/login", async (
            LocalLoginRequest request,
            HttpContext context,
            ILocalIdentityStore store,
            JwtTokenIssuer issuer,
            ApiAuditService audit,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                await audit.RecordAsync(
                    context,
                    new SecurityPrincipal("anonymous", null, Array.Empty<string>(), false),
                    "auth.login",
                    AuditOutcome.Denied,
                    "identity",
                    "local");
                return Results.Unauthorized();
            }

            LocalUserAccount? account;
            try
            {
                account = await store.FindByUsernameAsync(request.Username, ct);
            }
            catch (ArgumentException)
            {
                account = null;
            }

            if (account is null || !account.IsEnabled || !LocalPasswordHasher.Verify(request.Password, account.Credential))
            {
                await audit.RecordAsync(
                    context,
                    new SecurityPrincipal("anonymous", null, Array.Empty<string>(), false),
                    "auth.login",
                    AuditOutcome.Denied,
                    "identity",
                    "local");
                return Results.Unauthorized();
            }

            var issued = issuer.Issue(account);
            context.Response.Cookies.Append(runtime.CookieName, issued.Token, CookieOptions(runtime, issued.ExpiresAtUtc));

            var principal = new SecurityPrincipal(
                account.Id.ToString(),
                account.DisplayName,
                account.Roles,
                true);
            await audit.RecordAsync(
                context,
                principal,
                "auth.login",
                AuditOutcome.Succeeded,
                "user",
                account.Id.ToString(),
                new Dictionary<string, string> { ["username"] = account.Username });

            return Results.Ok(new AuthProfileResponse(
                account.Id.ToString(),
                account.Username,
                account.DisplayName,
                account.Roles,
                issued.ExpiresAtUtc));
        }).RequireRateLimiting(LocalIdentityConfiguration.LoginRateLimitPolicy);

        endpoints.MapPost("/api/auth/logout", async (
            HttpContext context,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
        {
            var principal = security.GetPrincipal(context);
            context.Response.Cookies.Delete(runtime.CookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = runtime.SecureCookie,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true
            });

            await audit.RecordAsync(
                context,
                principal,
                "auth.logout",
                AuditOutcome.Succeeded,
                "identity",
                principal.IsAuthenticated && !string.IsNullOrWhiteSpace(principal.SubjectId)
                    ? principal.SubjectId
                    : "anonymous");
            return Results.NoContent();
        });

        return endpoints;
    }

    private static CookieOptions CookieOptions(LocalIdentityRuntimeOptions runtime, DateTimeOffset expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = runtime.SecureCookie,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expiresAtUtc,
        IsEssential = true
    };
}
