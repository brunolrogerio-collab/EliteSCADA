using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scada.Security.Authentication;

namespace Scada.Api.Security;

public static class JwtAuthenticationConfiguration
{
    public static bool AddEliteScadaJwtAuthentication(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection("Authentication");
        if (!section.GetValue<bool>("Enabled")) return false;

        var jwt = section.GetSection("Jwt");
        var issuer = jwt["Issuer"]?.Trim();
        var audience = jwt["Audience"]?.Trim();
        var signingKey = jwt["SigningKey"];
        var cookieName = section.GetSection("Local")["CookieName"]?.Trim();
        if (string.IsNullOrWhiteSpace(cookieName)) cookieName = LocalIdentityConfiguration.DefaultCookieName;

        if (string.IsNullOrWhiteSpace(issuer))
            throw new InvalidOperationException("Authentication:Jwt:Issuer is required when authentication is enabled.");
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Authentication:Jwt:Audience is required when authentication is enabled.");
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Authentication:Jwt:SigningKey must contain at least 32 UTF-8 bytes when authentication is enabled.");

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    NameClaimType = "name",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var hasBearerHeader = context.Request.Headers.Authorization
                            .ToString()
                            .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
                        if (hasBearerHeader) return Task.CompletedTask;

                        // Native browser WebSocket clients cannot set an Authorization header.
                        // Explicit query-string tokens remain restricted to the realtime endpoint.
                        if (context.HttpContext.Request.Path.StartsWithSegments("/ws/tags") &&
                            context.Request.Query.TryGetValue("access_token", out var token) &&
                            !string.IsNullOrWhiteSpace(token))
                        {
                            context.Token = token.ToString();
                            return Task.CompletedTask;
                        }

                        // The local browser login stores the same signed JWT in an HttpOnly cookie.
                        // Only API/realtime routes consume it; static browser content never parses credentials.
                        if ((context.HttpContext.Request.Path.StartsWithSegments("/api") ||
                             context.HttpContext.Request.Path.StartsWithSegments("/ws/tags")) &&
                            context.Request.Cookies.TryGetValue(cookieName, out var cookieToken) &&
                            !string.IsNullOrWhiteSpace(cookieToken))
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var provider = context.Principal?.FindFirst(JwtTokenIssuer.IdentityProviderClaim)?.Value;
                        if (!string.Equals(provider, JwtTokenIssuer.LocalIdentityProvider, StringComparison.Ordinal))
                            return;

                        var subject = context.Principal?.FindFirst("sub")?.Value;
                        var versionText = context.Principal?.FindFirst(JwtTokenIssuer.LocalUserVersionClaim)?.Value;
                        if (!Guid.TryParse(subject, out var userId) ||
                            !long.TryParse(versionText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var tokenVersion))
                        {
                            context.Fail("Local identity token is missing required version information.");
                            return;
                        }

                        var store = context.HttpContext.RequestServices.GetService<ILocalIdentityStore>();
                        if (store is null)
                        {
                            context.Fail("Local identity validation is unavailable.");
                            return;
                        }

                        var account = await store.FindByIdAsync(userId, context.HttpContext.RequestAborted);
                        if (account is null ||
                            !account.IsEnabled ||
                            account.UpdatedAtUtc.ToUnixTimeMilliseconds() != tokenVersion)
                        {
                            context.Fail("Local identity token is no longer current.");
                        }
                    }
                };
            });

        return true;
    }
}
