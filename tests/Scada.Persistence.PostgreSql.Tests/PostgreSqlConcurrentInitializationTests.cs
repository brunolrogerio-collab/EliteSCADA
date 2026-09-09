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

        await using var engineering = new PostgreSqlEngineeringProjectStore(connectionString);
        await using var audit = new PostgreSqlAuditStore(connectionString);
        await using var identity = new PostgreSqlLocalIdentityStore(connectionString);
        await using var serverMemory = new PostgreSqlServerMemoryRetentionStore(connectionString);

        var operationalEventQuery = new HistoricalQueryExecution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.OperationalEvents),
            new HistoricalResolvedRange(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow),
            [],
            null,
            new HistoricalSort(),
            1,
            null);

        var initializers = new Func<Task>[]
        {
            () => engineering.InitializeAsync(),
            () => audit.InitializeAsync(),
            () => identity.InitializeAsync(),
            () => serverMemory.InitializeAsync(),
            async () =>
            {
                await using var operationalEvents = new PostgreSqlOperationalEventHistoryStore(connectionString);
                _ = await operationalEvents.QueryAsync(operationalEventQuery);
            }
        };

        var tasks = Enumerable.Range(0, 4)
            .SelectMany(_ => initializers)
            .Select(initialize => Task.Run(initialize))
            .ToArray();

        await Task.WhenAll(tasks);

        _ = await identity.CountAsync();
    }
}
