using Scada.Security.Audit;

namespace Scada.Persistence.PostgreSql;

/// <summary>
/// Canonical deployment-level preparation for an empty PostgreSQL target.
/// It reuses the product stores' migrations and never stores deployment credentials.
/// </summary>
public static class PostgreSqlDeploymentDatabasePreparation
{
    public static async Task InitializeCoreAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using (var engineering = new PostgreSqlEngineeringProjectStore(connectionString))
            await engineering.InitializeAsync(cancellationToken);

        await using (var binding = new PostgreSqlEngineeringInstallationBindingStore(connectionString))
            await binding.InitializeAsync(cancellationToken);

        await using (var identities = new PostgreSqlLocalIdentityStore(connectionString))
            await identities.InitializeAsync(cancellationToken);

        await using (var authority = new PostgreSqlAuthorityPolicyStore(connectionString))
            await authority.InitializeAsync(cancellationToken);

        await using (var lifecycle = new PostgreSqlAuthorityLifecycleStore(connectionString))
            await lifecycle.InitializeAsync(cancellationToken);

        await using (var audit = new PostgreSqlAuditStore(connectionString, new AuditQueryPolicy()))
            await audit.InitializeAsync(cancellationToken);

        await using (var serverMemory = new PostgreSqlServerMemoryRetentionStore(connectionString))
            await serverMemory.InitializeAsync(cancellationToken);

        await using (var sessions = new PostgreSqlRuntimeSessionLeaseStore(connectionString))
            await sessions.InitializeAsync(cancellationToken);
    }

    public static async Task InitializeHistoricalEventsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using (var alarms = new PostgreSqlAlarmHistoryStore(connectionString))
            await alarms.EnsureInitializedAsync(cancellationToken);

        await using (var events = new PostgreSqlOperationalEventHistoryStore(connectionString))
            await events.EnsureInitializedAsync(cancellationToken);
    }
}
