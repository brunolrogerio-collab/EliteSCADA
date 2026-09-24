using Scada.Core.HistoricalQueries;
using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlConcurrentInitializationTests
{
    [Fact]
    public async Task SharedSchemaStores_InitializeConcurrentlyWithoutDdlCollisions()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var operationalEventQuery = new HistoricalQueryExecution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.OperationalEvents),
            new HistoricalResolvedRange(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow),
            [],
            null,
            new HistoricalSort(),
            1,
            null);

        var alarmQuery = new HistoricalQueryExecution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.AlarmEvents),
            new HistoricalResolvedRange(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow),
            [],
            null,
            new HistoricalSort(),
            1,
            null);

        var initializers = new Func<Task>[]
        {
            async () =>
            {
                await using var engineering = new PostgreSqlEngineeringProjectStore(connectionString);
                await engineering.InitializeAsync();
            },
            async () =>
            {
                await using var audit = new PostgreSqlAuditStore(connectionString);
                await audit.InitializeAsync();
            },
            async () =>
            {
                await using var identity = new PostgreSqlLocalIdentityStore(connectionString);
                await identity.InitializeAsync();
            },
            async () =>
            {
                await using var serverMemory = new PostgreSqlServerMemoryRetentionStore(connectionString);
                await serverMemory.InitializeAsync();
            },
            async () =>
            {
                await using var authorityPolicy = new PostgreSqlAuthorityPolicyStore(connectionString);
                await authorityPolicy.InitializeAsync();
            },
            async () =>
            {
                await using var authorityLifecycle = new PostgreSqlAuthorityLifecycleStore(connectionString);
                await authorityLifecycle.InitializeAsync();
            },
            async () =>
            {
                await using var runtimeSessionLeases = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
                await runtimeSessionLeases.InitializeAsync();
            },
            async () =>
            {
                await using var operationalEvents = new PostgreSqlOperationalEventHistoryStore(connectionString);
                _ = await operationalEvents.QueryAsync(operationalEventQuery);
            },
            async () =>
            {
                await using var alarms = new PostgreSqlAlarmHistoryStore(connectionString);
                _ = await alarms.QueryAsync(alarmQuery);
            }
        };

        var tasks = Enumerable.Range(0, 4)
            .SelectMany(_ => initializers)
            .Select(initialize => Task.Run(initialize))
            .ToArray();

        await Task.WhenAll(tasks);

        await using var identity = new PostgreSqlLocalIdentityStore(connectionString);
        await identity.InitializeAsync();
        _ = await identity.CountAsync();
    }
}
