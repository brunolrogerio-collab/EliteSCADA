using Npgsql;

namespace Scada.Persistence.PostgreSql;

internal static class PostgreSqlSharedSchemaInitialization
{
    private const string AdvisoryLockSql = "SELECT pg_advisory_xact_lock(4993446713136202561);";

    /// <summary>
    /// Acquires the transaction-scoped lock before any shared-schema DDL is sent.
    /// Keeping this as a separate completed command prevents PostgreSQL from
    /// interleaving CREATE SCHEMA with another initializer in the same batch.
    /// </summary>
    public static async Task AcquireLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var lockCommand = new NpgsqlCommand(AdvisoryLockSql, connection, transaction);
        await lockCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
