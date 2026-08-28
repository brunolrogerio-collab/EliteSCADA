using Npgsql;
using NpgsqlTypes;

namespace Scada.Historian.TimescaleDb;

internal static class TimescaleHistorianInfrastructure
{
    // Database-scoped infrastructure lock shared with Scada.Persistence.PostgreSql.
    // Every subsystem that creates or mutates objects in the shared `elitescada`
    // schema must coordinate on this key. PostgreSQL `IF NOT EXISTS` is not itself
    // race-free when two sessions concurrently create the same catalog object.
    // Hex: 0x454C495445534341 == "ELITESCA".
    private const long InfrastructureLockKey = 4993446713136202561;

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
