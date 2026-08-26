using Npgsql;
using NpgsqlTypes;

namespace Scada.Historian.TimescaleDb;

internal static class TimescaleHistorianInfrastructure
{
    // Stable database-scoped advisory lock key used only while reconciling historian DDL.
    // PostgreSQL IF NOT EXISTS checks are not sufficient to make concurrent CREATE TABLE
    // calls race-free because catalog object creation itself can still collide.
    private const long InfrastructureLockKey = 0x454C495445484953; // "ELITEHIS"

    public static Task EnsureRawAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken = default) =>
        EnsureAsync(dataSource, includeAggregates: false, cancellationToken);

    public static Task EnsureAllAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken = default) =>
        EnsureAsync(dataSource, includeAggregates: true, cancellationToken);

    private static async Task EnsureAsync(
        NpgsqlDataSource dataSource,
        bool includeAggregates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await AcquireLockAsync(connection, cancellationToken);
        try
        {
            await using (var command = new NpgsqlCommand(
                TimescaleHistorianSchema.RawInfrastructureSql,
                connection))
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (!includeAggregates) return;

            foreach (var bucket in TimescaleHistorianSchema.SupportedBuckets)
            {
                await using var command = new NpgsqlCommand(
                    TimescaleHistorianSchema.BuildAggregateInfrastructureSql(bucket),
                    connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            // Session advisory locks are also released automatically if the connection
            // dies. Use a non-cancelled token here so ordinary caller cancellation does
            // not leave a pooled connection holding the lock.
            await ReleaseLockAsync(connection);
        }
    }

    private static async Task AcquireLockAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_lock(@lock_key);",
            connection);
        command.Parameters.AddWithValue("lock_key", NpgsqlDbType.Bigint, InfrastructureLockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReleaseLockAsync(NpgsqlConnection connection)
    {
        if (connection.FullState != System.Data.ConnectionState.Open) return;

        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_unlock(@lock_key);",
            connection);
        command.Parameters.AddWithValue("lock_key", NpgsqlDbType.Bigint, InfrastructureLockKey);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
