using Npgsql;
using NpgsqlTypes;
using Scada.Historian.TimescaleDb;

namespace Scada.Historian.TimescaleDb.Tests;

public sealed class TimescaleInfrastructureConcurrencyTests
{
    // Must match the database-wide EliteSCADA schema DDL lock used by
    // Scada.Persistence.PostgreSql initializers.
    private const long SharedInfrastructureLockKey = 4993446713136202561;

    [Fact]
    public async Task EnsureInfrastructure_WaitsForSharedEliteScadaSchemaDdlLock()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var blockerDataSource = NpgsqlDataSource.Create(connectionString);
        await using var blockerConnection = await blockerDataSource.OpenConnectionAsync();
        await using var blockerTransaction = await blockerConnection.BeginTransactionAsync();
        await using (var acquire = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@lock_key);",
            blockerConnection,
            blockerTransaction))
        {
            acquire.Parameters.AddWithValue("lock_key", NpgsqlDbType.Bigint, SharedInfrastructureLockKey);
            await acquire.ExecuteNonQueryAsync();
        }

        await using var store = new TimescaleDbHistorianRetentionDownsamplingStore(connectionString);
        var ensureTask = store.EnsureInfrastructureAsync();

        var first = await Task.WhenAny(ensureTask, Task.Delay(TimeSpan.FromMilliseconds(300)));
        var waitedForSharedLock = !ReferenceEquals(first, ensureTask);

        // Commit releases the transaction-scoped lock used by the persistence stores.
        // Do this before asserting so a failure cannot leave the ensure task blocked.
        await blockerTransaction.CommitAsync();
        await ensureTask.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.True(
            waitedForSharedLock,
            "Timescale infrastructure initialization must coordinate with the shared EliteSCADA schema DDL advisory lock.");
    }

    [Fact]
    public async Task ConcurrentTimescaleInfrastructureEnsures_RemainIdempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var first = new TimescaleDbHistorianRetentionDownsamplingStore(connectionString);
        await using var second = new TimescaleDbHistorianRetentionDownsamplingStore(connectionString);

        await Task.WhenAll(
            first.EnsureInfrastructureAsync(),
            second.EnsureInfrastructureAsync());
    }
}
