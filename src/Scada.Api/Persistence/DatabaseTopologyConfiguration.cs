using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Scada.Core.Persistence;

namespace Scada.Api.Persistence;

public sealed record DatabaseTopologyOptions(
    string StatePath,
    string SecretStorePath,
    int MinimumPostgreSqlMajor,
    int MaximumPostgreSqlMajor,
    int MinimumTimescaleMajor,
    int MaintenanceLeaseSeconds)
{
    public static DatabaseTopologyOptions FromConfiguration(IConfiguration configuration, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var dataRoot = Path.Combine(baseDirectory, "data", "deployment");
        var statePath = configuration["DatabaseTopology:StateFile"];
        if (string.IsNullOrWhiteSpace(statePath)) statePath = Path.Combine(dataRoot, "database-topology.json");
        var secretPath = configuration["DatabaseTopology:Secrets:StoreFile"];
        if (string.IsNullOrWhiteSpace(secretPath)) secretPath = Path.Combine(dataRoot, "database-secrets.json");

        var minimumPg = configuration.GetValue<int?>("DatabaseTopology:Compatibility:MinimumPostgreSqlMajor") ?? 18;
        var maximumPg = configuration.GetValue<int?>("DatabaseTopology:Compatibility:MaximumPostgreSqlMajor") ?? 18;
        var minimumTimescale = configuration.GetValue<int?>("DatabaseTopology:Compatibility:MinimumTimescaleMajor") ?? 2;
        var maintenanceLeaseSeconds = configuration.GetValue<int?>("DatabaseTopology:Cutover:MaintenanceLeaseSeconds") ?? 300;

        if (minimumPg < 12 || maximumPg < minimumPg)
            throw new InvalidOperationException("DatabaseTopology PostgreSQL compatibility range is invalid.");
        if (minimumTimescale < 1)
            throw new InvalidOperationException("DatabaseTopology minimum TimescaleDB major version is invalid.");
        if (maintenanceLeaseSeconds is < 30 or > 1800)
            throw new InvalidOperationException("DatabaseTopology maintenance lease must be between 30 and 1800 seconds.");

        return new(
            Path.GetFullPath(statePath),
            Path.GetFullPath(secretPath),
            minimumPg,
            maximumPg,
            minimumTimescale,
            maintenanceLeaseSeconds);
    }
}

public sealed class DatabaseConnectionResolver
{
    private readonly IDeploymentDatabaseSecretStore _secrets;
    private readonly string? _localPrimary;
    private readonly string? _localHistorian;

    public DatabaseConnectionResolver(
        IDeploymentDatabaseSecretStore secrets,
        string? localPrimary,
        string? localHistorian)
    {
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _localPrimary = Normalize(localPrimary);
        _localHistorian = Normalize(localHistorian) ?? _localPrimary;
    }

    public async Task<DatabaseRuntimeConnectionSet> ResolveAsync(
        DatabaseTopologyProfile profile,
        bool requireDurable,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        if (profile.Mode == DatabaseTopologyMode.LocalManaged)
            return new(profile, _localPrimary, _localHistorian, requireDurable);

        var primary = await BuildRemoteConnectionStringAsync(profile.Primary!, cancellationToken);
        var historian = profile.Historian.UsePrimary
            ? primary
            : await BuildRemoteConnectionStringAsync(profile.Historian.Override!, cancellationToken);
        return new(profile, primary, historian, requireDurable);
    }

    public async Task<string> BuildTransientRemoteConnectionStringAsync(
        DatabaseRemoteEndpointRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var endpoint = new DatabaseRemoteEndpoint(
            request.Host,
            request.Port,
            request.Database,
            request.Username,
            request.CredentialReference,
            request.TlsMode,
            request.RootCertificatePath,
            request.TrustServerCertificate,
            request.TimeoutSeconds);
        endpoint.Validate();

        if (!string.IsNullOrEmpty(request.Password))
            return BuildConnectionString(endpoint, request.Password);

        if (!string.IsNullOrWhiteSpace(endpoint.CredentialReference))
            return await BuildRemoteConnectionStringAsync(endpoint, cancellationToken);

        return BuildConnectionString(endpoint, null);
    }

    public DatabaseEndpointStatus? DescribeLocalPrimary() => DescribeConnectionString(_localPrimary);
    public DatabaseEndpointStatus? DescribeLocalHistorian() => DescribeConnectionString(_localHistorian);

    public static DatabaseEndpointStatus? DescribeConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return new(
                builder.Host ?? "localhost",
                builder.Port,
                builder.Database ?? string.Empty,
                builder.Username ?? string.Empty,
                FromSslMode(builder.SslMode),
                !string.IsNullOrEmpty(builder.Password),
                false,
                !string.IsNullOrWhiteSpace(builder.RootCertificate),
                builder.Timeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private async Task<string> BuildRemoteConnectionStringAsync(
        DatabaseRemoteEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        endpoint.Validate();
        string? password = null;
        if (!string.IsNullOrWhiteSpace(endpoint.CredentialReference))
        {
            await using var lease = await _secrets.ResolveAsync(endpoint.CredentialReference, cancellationToken);
            password = Encoding.UTF8.GetString(lease.Material.Span);
        }
        return BuildConnectionString(endpoint, password);
    }

    private static string BuildConnectionString(DatabaseRemoteEndpoint endpoint, string? password)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = endpoint.Host.Trim(),
            Port = endpoint.Port,
            Database = endpoint.Database.Trim(),
            Username = endpoint.Username.Trim(),
            Timeout = endpoint.TimeoutSeconds,
            CommandTimeout = Math.Max(30, endpoint.TimeoutSeconds),
            SslMode = endpoint.TlsMode switch
            {
                DatabaseTlsMode.Disable => SslMode.Disable,
                DatabaseTlsMode.Prefer => SslMode.Prefer,
                DatabaseTlsMode.Require => SslMode.Require,
                DatabaseTlsMode.VerifyCa => SslMode.VerifyCA,
                DatabaseTlsMode.VerifyFull => SslMode.VerifyFull,
                _ => throw new ArgumentOutOfRangeException(nameof(endpoint.TlsMode))
            },
            RootCertificate = string.IsNullOrWhiteSpace(endpoint.RootCertificatePath)
                ? null
                : Path.GetFullPath(endpoint.RootCertificatePath),
            PersistSecurityInfo = false,
            IncludeErrorDetail = false,
            LogParameters = false,
            ApplicationName = "EliteSCADA",
            Pooling = true
        };
        if (!string.IsNullOrEmpty(password)) builder.Password = password;
        return builder.ConnectionString;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DatabaseTlsMode FromSslMode(SslMode mode) => mode switch
    {
        SslMode.Disable => DatabaseTlsMode.Disable,
        SslMode.Prefer => DatabaseTlsMode.Prefer,
        SslMode.Require => DatabaseTlsMode.Require,
        SslMode.VerifyCA => DatabaseTlsMode.VerifyCa,
        SslMode.VerifyFull => DatabaseTlsMode.VerifyFull,
        _ => DatabaseTlsMode.Prefer
    };
}

public interface IDatabaseRuntimeRebinder
{
    bool RestartRequested { get; }
    void RequestRestart();
}

public sealed class HostRestartDatabaseRuntimeRebinder(IHostApplicationLifetime lifetime) : IDatabaseRuntimeRebinder
{
    private int _restartRequested;
    public bool RestartRequested => Volatile.Read(ref _restartRequested) != 0;

    public void RequestRestart()
    {
        if (Interlocked.Exchange(ref _restartRequested, 1) == 0)
            lifetime.StopApplication();
    }
}

public static class DatabaseTopologyConfiguration
{
    public static DatabaseRuntimeConnectionSet AddDatabaseTopologyCore(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = DatabaseTopologyOptions.FromConfiguration(builder.Configuration, AppContext.BaseDirectory);

        byte[] masterKey = Array.Empty<byte>();
        var encodedKey = builder.Configuration["DatabaseTopology:Secrets:MasterKeyBase64"];
        if (!string.IsNullOrWhiteSpace(encodedKey))
        {
            try { masterKey = Convert.FromBase64String(encodedKey.Trim()); }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("DatabaseTopology:Secrets:MasterKeyBase64 must be valid Base64.", ex);
            }
            if (masterKey.Length != 32)
            {
                CryptographicOperations.ZeroMemory(masterKey);
                throw new InvalidOperationException("DatabaseTopology:Secrets:MasterKeyBase64 must decode to exactly 32 bytes.");
            }
        }

        EncryptedFileDeploymentDatabaseSecretStore secretStore;
        try { secretStore = new(options.SecretStorePath, masterKey); }
        finally { if (masterKey.Length > 0) CryptographicOperations.ZeroMemory(masterKey); }

        var topologyStore = new FileDatabaseTopologyStore(options.StatePath);
        var document = topologyStore.GetAsync().GetAwaiter().GetResult();
        var maintenanceGate = new DatabaseMaintenanceGate();
        if (document.Pending is { } recoveredPending &&
            RequiresRecoveredMaintenance(recoveredPending.Phase))
        {
            maintenanceGate.Recover(
                recoveredPending.OperationId,
                recoveredPending.MaintenanceLeaseExpiresAtUtc);
        }

        var localPrimary = builder.Configuration.GetConnectionString("EliteScada");
        var localHistorian = builder.Configuration.GetConnectionString("Historian") ?? localPrimary;
        var resolver = new DatabaseConnectionResolver(secretStore, localPrimary, localHistorian);

        var requireDurable = builder.Configuration.GetValue<bool?>("DatabaseTopology:RequireDurable")
            ?? builder.Environment.IsProduction();
        var runtime = resolver.ResolveAsync(document.Active, requireDurable).GetAwaiter().GetResult();
        if (requireDurable && !runtime.HasDurablePrimary)
        {
            secretStore.Dispose();
            throw new InvalidOperationException(
                "Production database durability is required, but no active PostgreSQL topology is configured. EliteSCADA will not silently fall back to in-memory persistence.");
        }

        builder.Services.TryAddSingleton(options);
        builder.Services.TryAddSingleton<IDatabaseTopologyStore>(topologyStore);
        builder.Services.TryAddSingleton<IDeploymentDatabaseSecretStore>(secretStore);
        builder.Services.TryAddSingleton(resolver);
        builder.Services.TryAddSingleton(runtime);
        builder.Services.TryAddSingleton<IDatabaseTopologyOperations, PostgreSqlDatabaseTopologyOperations>();
        builder.Services.TryAddSingleton(maintenanceGate);
        builder.Services.TryAddSingleton<IDurableWriteAdmission>(maintenanceGate);
        builder.Services.TryAddSingleton<IDatabaseRuntimeRebinder, HostRestartDatabaseRuntimeRebinder>();
        builder.Services.TryAddSingleton<DatabaseTopologyAdministrationService>();
        return runtime;
    }

    private static bool RequiresRecoveredMaintenance(DatabaseMigrationPhase phase) =>
        phase is DatabaseMigrationPhase.Quiescing
            or DatabaseMigrationPhase.Copying
            or DatabaseMigrationPhase.Copied
            or DatabaseMigrationPhase.Verifying
            or DatabaseMigrationPhase.Verified
            or DatabaseMigrationPhase.Switching
            or DatabaseMigrationPhase.Readiness
            or DatabaseMigrationPhase.RollbackRequired;
}
