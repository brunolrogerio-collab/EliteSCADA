using Scada.Security.Audit;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed record LocalLoginRequest(string Username, string Password);
public sealed record InitialAdministratorRequest(string Username, string? DisplayName, string Password);

public sealed record AuthProfileResponse(
    string SubjectId,
    string? Username,
    string? DisplayName,
    IReadOnlyCollection<string> Roles,
    DateTimeOffset? ExpiresAtUtc = null,
    string IdentityProvider = JwtTokenIssuer.LocalIdentityProvider);

public static class LocalIdentityApi
{
    public static IEndpointRouteBuilder MapLocalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var runtime = endpoints.ServiceProvider.GetRequiredService<LocalIdentityRuntimeOptions>();

        if (!runtime.Enabled)
        {
            endpoints.MapGet("/api/auth/config", () => Results.Ok(new
            {
                authenticationEnabled = runtime.AuthenticationEnabled,
                localLoginEnabled = false,
                initialAdministratorRequired = false,
                passwordPolicy = new
                {
                    minimumLength = LocalPasswordHasher.MinimumPasswordLength,
                    maximumLength = LocalPasswordHasher.MaximumPasswordLength
                }
            }));
            return endpoints;
        }

        endpoints.MapGet("/api/auth/config", async (
            LocalIdentityBootstrapService bootstrap,
            CancellationToken ct) => Results.Ok(new
            {
                authenticationEnabled = runtime.AuthenticationEnabled,
                localLoginEnabled = true,
                initialAdministratorRequired = await bootstrap.IsInitialAdministratorRequiredAsync(ct),
                passwordPolicy = new
                {
                    minimumLength = LocalPasswordHasher.MinimumPasswordLength,
                    maximumLength = LocalPasswordHasher.MaximumPasswordLength
                }
            }));

        endpoints.MapGet("/api/auth/local-session", (HttpContext context) =>
        {
            var isLocal = string.Equals(
                context.User.FindFirst(JwtTokenIssuer.IdentityProviderClaim)?.Value,
                JwtTokenIssuer.LocalIdentityProvider,
                StringComparison.Ordinal);
            return Results.Ok(new
            {
                authenticated = isLocal,
                username = isLocal ? context.User.FindFirst("unique_name")?.Value : null
            });
        });

        endpoints.MapPost("/api/auth/bootstrap", async (
            InitialAdministratorRequest request,
            HttpContext context,
            LocalIdentityBootstrapService bootstrap,
            JwtTokenIssuer issuer,
            LocalLoginAttemptLimiter limiter,
            ApiAuditService audit,
            CancellationToken ct) =>
        {
            var remoteKey = $"bootstrap:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
            if (!limiter.TryAcquire(remoteKey))
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);

            try
            {
                var result = await bootstrap.CreateInitialAdministratorAsync(
                    request.Username,
                    request.DisplayName,
                    request.Password,
                    ct);
                if (!result.Created || result.Account is null)
                {
                    return Results.Conflict(new
                    {
                        error = "Initial Administrator bootstrap is already closed. Sign in with an existing account."
                    });
                }

                var account = result.Account;
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
                    "auth.bootstrap",
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
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        endpoints.MapPost("/api/auth/login", async (
            LocalLoginRequest request,
            HttpContext context,
            ILocalIdentityStore store,
            JwtTokenIssuer issuer,
            LocalLoginAttemptLimiter limiter,
            ApiAuditService audit,
            CancellationToken ct) =>
        {
            var remoteKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!limiter.TryAcquire(remoteKey))
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);

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
        });

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

        endpoints.MapLocalUserAdministrationEndpoints();
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
