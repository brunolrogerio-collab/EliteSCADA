using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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
            });

        return true;
    }
}
