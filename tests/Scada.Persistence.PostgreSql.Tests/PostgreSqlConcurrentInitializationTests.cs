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

        var initializers = new Func<Task>[]
        {
            () => engineering.InitializeAsync(),
            () => audit.InitializeAsync(),
            () => identity.InitializeAsync(),
            () => serverMemory.InitializeAsync()
        };

        var tasks = Enumerable.Range(0, 4)
            .SelectMany(_ => initializers)
            .Select(initialize => Task.Run(initialize))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(0, await identity.CountAsync());
    }
}
