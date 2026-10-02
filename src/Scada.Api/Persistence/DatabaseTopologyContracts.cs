using System.Text.Json.Serialization;

namespace Scada.Api.Persistence;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DatabaseTopologyMode
{
    LocalManaged,
    Remote
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DatabaseTlsMode
{
    Disable,
    Prefer,
    Require,
    VerifyCa,
    VerifyFull
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DatabaseMigrationPhase
{
    Candidate,
    Tested,
    Compatible,
    Prepared,
    Quiescing,
    Copying,
    Copied,
    Verifying,
    Verified,
    Switching,
    Readiness,
    Completed,
    RollbackRequired,
    RolledBack,
    Failed
}

public sealed record DatabaseRemoteEndpoint(
    string Host,
    int Port,
    string Database,
    string Username,
    string? CredentialReference,
    DatabaseTlsMode TlsMode,
    string? RootCertificatePath,
    bool TrustServerCertificate,
    int TimeoutSeconds)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new ArgumentException("Database host/FQDN is required.", nameof(Host));
        if (Host.Length > 253 || Host.Any(char.IsControl))
            throw new ArgumentException("Database host/FQDN is invalid.", nameof(Host));
        if (Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port), "Database port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(Database) || Database.Length > 128)
            throw new ArgumentException("Database name is required and must not exceed 128 characters.", nameof(Database));
        if (string.IsNullOrWhiteSpace(Username) || Username.Length > 256)
            throw new ArgumentException("Database authentication identity is required and must not exceed 256 characters.", nameof(Username));
        if (TimeoutSeconds is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(TimeoutSeconds), "Database timeout must be between 1 and 120 seconds.");
        if (TlsMode is DatabaseTlsMode.VerifyCa or DatabaseTlsMode.VerifyFull &&
            string.IsNullOrWhiteSpace(RootCertificatePath))
        {
            throw new ArgumentException(
                "TLS VerifyCa/VerifyFull requires a host-owned root CA path.",
                nameof(RootCertificatePath));
        }
        if (TrustServerCertificate)
            throw new ArgumentException(
                "TrustServerCertificate bypass is not supported. Configure VerifyCa/VerifyFull with a host-owned root CA instead.",
                nameof(TrustServerCertificate));
    }
}

public sealed record DatabaseHistorianTopology(
    bool UsePrimary,
    DatabaseRemoteEndpoint? Override)
{
    public static DatabaseHistorianTopology Primary { get; } = new(true, null);

    public void Validate()
    {
        if (UsePrimary && Override is not null)
            throw new ArgumentException("Historian cannot use primary and an override simultaneously.");
        if (!UsePrimary && Override is null)
            throw new ArgumentException("Historian override is required when UsePrimary is false.");
        Override?.Validate();
    }
}

public sealed record DatabaseTopologyProfile(
    DatabaseTopologyMode Mode,
    DatabaseRemoteEndpoint? Primary,
    DatabaseHistorianTopology Historian)
{
    public static DatabaseTopologyProfile LocalManaged { get; } =
        new(DatabaseTopologyMode.LocalManaged, null, DatabaseHistorianTopology.Primary);

    public void Validate()
    {
        Historian.Validate();
        if (Mode == DatabaseTopologyMode.LocalManaged)
        {
            if (Primary is not null || !Historian.UsePrimary)
                throw new ArgumentException("Local Managed topology is owned by deployment provisioning and cannot embed remote endpoints.");
            return;
        }

        if (Primary is null)
            throw new ArgumentException("Remote topology requires a primary database endpoint.");
        Primary.Validate();
    }
}

public sealed record DatabaseMigrationPlan(
    Guid OperationId,
    DateTimeOffset PreparedAtUtc,
    bool HistorianUsesPrimary,
    bool TimescaleRequired,
    IReadOnlyList<string> DurableDomains,
    string SourceMode,
    string TargetMode);

public sealed record DatabaseMigrationVerification(
    bool Succeeded,
    IReadOnlyDictionary<string, long> SourceRows,
    IReadOnlyDictionary<string, long> TargetRows,
    string? ActiveProjectKey,
    long? ActiveRevision,
    string? FailureCode = null,
    string? Diagnostic = null);

public sealed record DatabasePendingMigration(
    Guid OperationId,
    DatabaseTopologyProfile Candidate,
    DatabaseMigrationPhase Phase,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DatabaseMigrationPlan? Plan = null,
    DatabaseMigrationVerification? Verification = null,
    DateTimeOffset? MaintenanceLeaseExpiresAtUtc = null,
    string? FailureCode = null,
    string? Diagnostic = null);

public sealed record DatabaseTopologyOperationSummary(
    Guid OperationId,
    DatabaseMigrationPhase Phase,
    DateTimeOffset CompletedAtUtc,
    string? FailureCode = null,
    string? Diagnostic = null);

public sealed record DatabaseTopologyDocument(
    int SchemaVersion,
    DatabaseTopologyProfile Active,
    DatabaseTopologyProfile? PreviousActive,
    DatabasePendingMigration? Pending,
    DatabaseTopologyOperationSummary? LastOperation,
    DateTimeOffset UpdatedAtUtc)
{
    public const int CurrentSchemaVersion = 1;

    public static DatabaseTopologyDocument CreateDefault(DateTimeOffset nowUtc) =>
        new(
            CurrentSchemaVersion,
            DatabaseTopologyProfile.LocalManaged,
            null,
            null,
            null,
            nowUtc);

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported database topology schema version '{SchemaVersion}'.");
        Active.Validate();
        PreviousActive?.Validate();
        Pending?.Candidate.Validate();
    }
}

public sealed record DatabaseRemoteEndpointRequest(
    string Host,
    int Port,
    string Database,
    string Username,
    string? Password,
    string? CredentialReference,
    DatabaseTlsMode TlsMode = DatabaseTlsMode.Require,
    string? RootCertificatePath = null,
    bool TrustServerCertificate = false,
    int TimeoutSeconds = 15)
{
    public override string ToString() =>
        $"DatabaseRemoteEndpointRequest {{ Host = {Host}, Port = {Port}, Database = {Database}, Username = {Username}, CredentialConfigured = {!string.IsNullOrEmpty(Password) || !string.IsNullOrWhiteSpace(CredentialReference)}, TlsMode = {TlsMode} }}";
}

public sealed record DatabaseRemoteProfileRequest(
    DatabaseRemoteEndpointRequest Primary,
    DatabaseRemoteEndpointRequest? HistorianOverride = null);

public sealed record DatabaseEndpointStatus(
    string Host,
    int Port,
    string Database,
    string Username,
    DatabaseTlsMode TlsMode,
    bool CredentialConfigured,
    bool TrustServerCertificate,
    bool RootCertificateConfigured,
    int TimeoutSeconds);

public sealed record DatabaseProfileStatus(
    DatabaseTopologyMode Mode,
    DatabaseEndpointStatus? Primary,
    bool HistorianUsesPrimary,
    DatabaseEndpointStatus? HistorianOverride);

public sealed record DatabaseConnectionHealth(
    bool Reachable,
    string? PostgreSqlVersion,
    int? PostgreSqlMajor,
    string? TimescaleDbVersion,
    bool TimescaleCapable,
    bool SchemaCompatible,
    DateTimeOffset CheckedAtUtc,
    string? FailureCode = null,
    string? Diagnostic = null);

public sealed record DatabaseTopologyStatus(
    DatabaseProfileStatus ActiveTopology,
    DatabaseProfileStatus? PreviousTopology,
    DatabaseMigrationPhase? PendingPhase,
    Guid? PendingOperationId,
    bool RecoveryRequired,
    bool RestartRequired,
    DatabaseConnectionHealth? PrimaryHealth,
    DatabaseConnectionHealth? HistorianHealth,
    DateTimeOffset? LastHealthCheckUtc,
    DatabaseTopologyOperationSummary? LastOperation);

public sealed record DatabaseCompatibilityResult(
    bool Compatible,
    DatabaseConnectionHealth Primary,
    DatabaseConnectionHealth? Historian,
    string? FailureCode = null,
    string? Diagnostic = null);

public sealed record DatabaseCutoverResult(
    bool Succeeded,
    bool RolledBack,
    bool RestartRequired,
    DatabaseTopologyStatus Status,
    string? FailureCode = null,
    string? Diagnostic = null);

public sealed record DatabaseRuntimeConnectionSet(
    DatabaseTopologyProfile ActiveProfile,
    string? PrimaryConnectionString,
    string? HistorianConnectionString,
    bool RequireDurable,
    bool RestartRequired = false)
{
    [JsonIgnore]
    public bool HasDurablePrimary => !string.IsNullOrWhiteSpace(PrimaryConnectionString);
}

internal static class DatabaseTopologyStatusProjection
{
    public static DatabaseProfileStatus Project(DatabaseTopologyProfile profile) =>
        new(
            profile.Mode,
            profile.Primary is null ? null : Project(profile.Primary),
            profile.Historian.UsePrimary,
            profile.Historian.Override is null ? null : Project(profile.Historian.Override));

    public static DatabaseEndpointStatus Project(DatabaseRemoteEndpoint endpoint) =>
        new(
            endpoint.Host,
            endpoint.Port,
            endpoint.Database,
            endpoint.Username,
            endpoint.TlsMode,
            !string.IsNullOrWhiteSpace(endpoint.CredentialReference),
            endpoint.TrustServerCertificate,
            !string.IsNullOrWhiteSpace(endpoint.RootCertificatePath),
            endpoint.TimeoutSeconds);
}
