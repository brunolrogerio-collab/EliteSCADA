using Scada.Engineering.Persistence;
using Scada.Persistence.PostgreSql;
using Scada.Security.Authentication;

namespace Scada.Api.Security;

public sealed record LocalIdentityRuntimeOptions(
    bool AuthenticationEnabled,
    bool Enabled,
    bool SecureCookie,
    string CookieName,
    bool DurableStore = false);

public sealed class LocalLoginAttemptLimiter
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AttemptWindow> _windows = new(StringComparer.Ordinal);
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly TimeSpan _cleanupInterval;
    private DateTimeOffset? _nextCleanupAtUtc;

    public LocalLoginAttemptLimiter(
        int permitLimit = 10,
        TimeSpan? window = null,
        TimeSpan? cleanupInterval = null)
    {
        if (permitLimit < 1) throw new ArgumentOutOfRangeException(nameof(permitLimit));

        _permitLimit = permitLimit;
        _window = window ?? TimeSpan.FromMinutes(1);
        if (_window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));

        _cleanupInterval = cleanupInterval ?? _window;
        if (_cleanupInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cleanupInterval));
    }

    public bool TryAcquire(string key, DateTimeOffset? nowUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var now = nowUtc ?? DateTimeOffset.UtcNow;

        lock (_gate)
        {
            CleanupExpiredWindowsIfDue(now);

            if (!_windows.TryGetValue(key, out var attemptWindow))
            {
                attemptWindow = new AttemptWindow(now, 0);
                _windows.Add(key, attemptWindow);
            }
            else if (now - attemptWindow.StartedAtUtc >= _window)
            {
                attemptWindow.StartedAtUtc = now;
                attemptWindow.Count = 0;
            }

            if (attemptWindow.Count >= _permitLimit) return false;
            attemptWindow.Count++;
            return true;
        }
    }

    internal int TrackedKeyCount
    {
        get
        {
            lock (_gate) return _windows.Count;
        }
    }

    private void CleanupExpiredWindowsIfDue(DateTimeOffset now)
    {
        if (_nextCleanupAtUtc is not null && now < _nextCleanupAtUtc.Value) return;

        _nextCleanupAtUtc = now + _cleanupInterval;
        if (_windows.Count == 0) return;

        List<string>? expiredKeys = null;
        foreach (var pair in _windows)
        {
            if (now - pair.Value.StartedAtUtc < _window) continue;
            expiredKeys ??= new List<string>();
            expiredKeys.Add(pair.Key);
        }

        if (expiredKeys is null) return;
        foreach (var expiredKey in expiredKeys)
            _windows.Remove(expiredKey);
    }

    private sealed class AttemptWindow(DateTimeOffset startedAtUtc, int count)
    {
        public DateTimeOffset StartedAtUtc { get; set; } = startedAtUtc;
        public int Count { get; set; } = count;
    }
}

public static class LocalIdentityConfiguration
{
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
                DefaultCookieName,
                false));
            return false;
        }

        if (!authenticationEnabled)
            throw new InvalidOperationException("Authentication:Local:Enabled requires Authentication:Enabled=true.");

        var cookieName = local["CookieName"]?.Trim();
        if (string.IsNullOrWhiteSpace(cookieName)) cookieName = DefaultCookieName;
        if (cookieName.Any(char.IsWhiteSpace) || cookieName.Contains(';'))
            throw new InvalidOperationException("Authentication:Local:CookieName contains invalid characters.");

        var connectionString = builder.Configuration.GetConnectionString("EliteScada");
        var durableStore = !string.IsNullOrWhiteSpace(connectionString);
        var secureCookie = local.GetValue<bool?>("SecureCookie") ?? true;
        builder.Services.AddSingleton(new LocalIdentityRuntimeOptions(
            true,
            true,
            secureCookie,
            cookieName,
            durableStore));
        builder.Services.AddSingleton<JwtTokenIssuer>();
        builder.Services.AddSingleton<LocalLoginAttemptLimiter>();
        builder.Services.AddSingleton<ILocalIdentityStore>(_ =>
            durableStore
                ? new PostgreSqlLocalIdentityStore(connectionString!)
                : new InMemoryLocalIdentityStore());
        builder.Services.AddSingleton<LocalIdentityBootstrapService>();

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

        var hasAnyConfiguredBootstrapValue =
            !string.IsNullOrWhiteSpace(username) ||
            !string.IsNullOrWhiteSpace(displayName) ||
            !string.IsNullOrWhiteSpace(password) ||
            roles.Length > 0;

        if (!hasAnyConfiguredBootstrapValue)
        {
            if (!runtime.DurableStore)
            {
                throw new InvalidOperationException(
                    "Secure anonymous first-run requires a durable local identity store. Configure ConnectionStrings:EliteScada, or provide the explicit Authentication:Local:Bootstrap configuration for a non-persistent development host.");
            }

            var catalog = app.Services.GetService<IEngineeringProjectCatalog>();
            if (catalog is null || await catalog.HasAnyAsync())
            {
                app.Logger.LogWarning(
                    "Local identity store is empty, but anonymous first-run is blocked because the server cannot prove that the installation is empty. Restore an Administrator or provide explicit secured bootstrap configuration.");
                return;
            }

            app.Logger.LogInformation(
                "Local identity and Engineering project stores are empty. Secure first-run setup is available until the first Administrator is created.");
            return;
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || roles.Length == 0)
        {
            throw new InvalidOperationException(
                "Authentication:Local:Bootstrap is incomplete. Configure Username, Password and at least one Roles entry, or remove the Bootstrap section to use secure first-run setup with durable persistence.");
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

        await using (var mutationLease = await store.AcquireMutationLeaseAsync())
        {
            if (await store.CountAsync() > 0) return;
            await store.CreateAsync(account);
        }

        app.Logger.LogWarning(
            "Created first local EliteSCADA identity '{Username}' from bootstrap configuration. Remove the bootstrap password from deployment configuration after successful initialization.",
            account.Username);
    }
}
