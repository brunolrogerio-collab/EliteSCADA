using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Scada.Persistence.PostgreSql;
using Scada.Security.Authentication;

namespace Scada.Api.Security;

public sealed record LocalIdentityRuntimeOptions(
    bool AuthenticationEnabled,
    bool Enabled,
    bool SecureCookie,
    string CookieName);

public static class LocalIdentityConfiguration
{
    public const string LoginRateLimitPolicy = "local-auth-login";
    public const string DefaultCookieName = "elitescada_access";

    public static bool AddLocalIdentity(this WebApplicationBuilder builder, bool authenticationEnabled)
    {
        var local = builder.Configuration.GetSection("Authentication:Local");
        var enabled = local.GetValue<bool>("Enabled");
        if (!enabled)
        {
            builder.Services.AddSingleton(new LocalIdentityRuntimeOptions(
                authenticationEnabled,
                false,
                true,
                DefaultCookieName));
            return false;
        }

        if (!authenticationEnabled)
            throw new InvalidOperationException("Authentication:Local:Enabled requires Authentication:Enabled=true.");

        var cookieName = local["CookieName"]?.Trim();
        if (string.IsNullOrWhiteSpace(cookieName)) cookieName = DefaultCookieName;
        if (cookieName.Any(char.IsWhiteSpace) || cookieName.Contains(';'))
            throw new InvalidOperationException("Authentication:Local:CookieName contains invalid characters.");

        var secureCookie = local.GetValue<bool?>("SecureCookie") ?? true;
        builder.Services.AddSingleton(new LocalIdentityRuntimeOptions(
            true,
            true,
            secureCookie,
            cookieName));
        builder.Services.AddSingleton<JwtTokenIssuer>();
        builder.Services.AddSingleton<ILocalIdentityStore>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("EliteScada");
            return string.IsNullOrWhiteSpace(connectionString)
                ? new InMemoryLocalIdentityStore()
                : new PostgreSqlLocalIdentityStore(connectionString);
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return true;
    }

    public static async Task InitializeLocalIdentityAsync(this WebApplication app)
    {
        var runtime = app.Services.GetRequiredService<LocalIdentityRuntimeOptions>();
        if (!runtime.Enabled) return;

        var store = app.Services.GetRequiredService<ILocalIdentityStore>();
        await store.InitializeAsync();
        if (await store.CountAsync() > 0) return;

        var bootstrap = app.Configuration.GetSection("Authentication:Local:Bootstrap");
        var username = bootstrap["Username"]?.Trim();
        var displayName = bootstrap["DisplayName"]?.Trim();
        var password = bootstrap["Password"];
        var roles = bootstrap.GetSection("Roles")
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || roles.Length == 0)
        {
            throw new InvalidOperationException(
                "Local identity store is empty. Configure Authentication:Local:Bootstrap:Username, Password and at least one Roles entry for first startup.");
        }

        LocalPasswordHasher.ValidatePassword(password);
        var now = DateTimeOffset.UtcNow;
        var account = new LocalUserAccount(
            Guid.NewGuid(),
            username,
            LocalIdentityNormalization.NormalizeUsername(username),
            string.IsNullOrWhiteSpace(displayName) ? username : displayName,
            true,
            LocalIdentityNormalization.NormalizeRoles(roles),
            LocalPasswordHasher.Hash(password),
            now,
            now);
        await store.CreateAsync(account);

        app.Logger.LogWarning(
            "Created first local EliteSCADA identity '{Username}' from bootstrap configuration. Remove the bootstrap password from deployment configuration after successful initialization.",
            account.Username);
    }
}
