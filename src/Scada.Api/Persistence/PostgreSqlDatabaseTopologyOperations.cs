using Npgsql;
using Scada.Historian.TimescaleDb;
using Scada.Persistence.PostgreSql;

namespace Scada.Api.Persistence;

public interface IDatabaseTopologyOperations
{
    Task<DatabaseConnectionHealth> TestAsync(string connectionString, bool requireTimescale, CancellationToken cancellationToken = default);
    Task<DatabaseCompatibilityResult> ValidateCompatibilityAsync(
        string primaryConnectionString,
        string historianConnectionString,
        bool historianUsesPrimary,
        CancellationToken cancellationToken = default);
    Task<DatabaseMigrationPlan> PrepareAsync(
        string sourceConnectionString,
        string targetConnectionString,
        string historianSourceConnectionString,
        string historianTargetConnectionString,
        bool historianUsesPrimary,
        CancellationToken cancellationToken = default);
    Task CopyAsync(
        DatabaseMigrationPlan plan,
        string sourceConnectionString,
        string targetConnectionString,
        string historianSourceConnectionString,
        string historianTargetConnectionString,
        CancellationToken cancellationToken = default);
    Task<DatabaseMigrationVerification> VerifyAsync(
        DatabaseMigrationPlan plan,
        string sourceConnectionString,
        string targetConnectionString,
        string historianSourceConnectionString,
        string historianTargetConnectionString,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlDatabaseTopologyOperations(DatabaseTopologyOptions options)
    : IDatabaseTopologyOperations
{
    private static readonly string[] MainTables =
    [
        "elitescada.schema_migrations",
        "elitescada.engineering_revisions",
        "elitescada.project_publications",
        "elitescada.project_activations",
        "elitescada.engineering_asset_blobs",
        "elitescada.engineering_revision_assets",
        "elitescada.engineering_installation_binding",
        "elitescada.local_users",
        "elitescada.authority_policy_state",
        "elitescada.authority_lifecycle_state",
        "elitescada.audit_events",
        "elitescada.server_memory_retained_values",
        "elitescada.runtime_session_leases",
        "elitescada.runtime_session_lease_tombstones",
        "elitescada.runtime_session_authority_state"
    ];

    private static readonly string[] MeaningfulMainTables =
    [
        "elitescada.engineering_revisions",
        "elitescada.project_publications",
        "elitescada.project_activations",
        "elitescada.engineering_asset_blobs",
        "elitescada.engineering_revision_assets",
        "elitescada.local_users",
        "elitescada.audit_events",
        "elitescada.server_memory_retained_values",
        "elitescada.runtime_session_leases",
        "elitescada.runtime_session_lease_tombstones"
    ];

    private static readonly string[] HistorianTables =
    [
        "elitescada.tag_history",
        "elitescada.historian_storage_policy_state",
        "elitescada.alarm_history",
        "elitescada.operational_event_history"
    ];

    private static readonly string[] SeedStateTables =
    [
        "elitescada.engineering_installation_binding",
        "elitescada.authority_policy_state",
        "elitescada.authority_lifecycle_state",
        "elitescada.runtime_session_authority_state"
    ];

    public async Task<DatabaseConnectionHealth> TestAsync(
        string connectionString,
        bool requireTimescale,
        CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            string version;
            int versionNumber;
            string timescale;
            await using (var command = new NpgsqlCommand("""
                SELECT
                    current_setting('server_version'),
                    current_setting('server_version_num')::integer,
                    COALESCE(
                        (SELECT extversion FROM pg_extension WHERE extname = 'timescaledb'),
                        (SELECT default_version FROM pg_available_extensions WHERE name = 'timescaledb'),
                        '');
                """, connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                    throw new InvalidDataException("PostgreSQL compatibility probe returned no row.");
                version = reader.GetString(0);
                versionNumber = reader.GetInt32(1);
                timescale = reader.GetString(2);
            }

            var major = versionNumber / 10000;
            var timescaleCapable = !string.IsNullOrWhiteSpace(timescale);
            var schemaCompatible = await HasCompatibleSchemaAsync(connection, cancellationToken);
            var postgresCompatible = major >= options.MinimumPostgreSqlMajor && major <= options.MaximumPostgreSqlMajor;
            var timescaleCompatible = !requireTimescale ||
                (timescaleCapable && ParseMajor(timescale) >= options.MinimumTimescaleMajor);
            var compatible = postgresCompatible && timescaleCompatible && schemaCompatible;

            return new(
                true,
                version,
                major,
                string.IsNullOrWhiteSpace(timescale) ? null : timescale,
                timescaleCapable,
                schemaCompatible,
                checkedAt,
                compatible ? null : "database-incompatible",
                compatible ? null : BuildCompatibilityDiagnostic(major, timescale, requireTimescale, schemaCompatible));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var code = Classify(ex);
            return new(
                false,
                null,
                null,
                null,
                false,
                false,
                checkedAt,
                code,
                DiagnosticFor(code));
        }
    }

    public async Task<DatabaseCompatibilityResult> ValidateCompatibilityAsync(
        string primaryConnectionString,
        string historianConnectionString,
        bool historianUsesPrimary,
        CancellationToken cancellationToken = default)
    {
        var primary = await TestAsync(primaryConnectionString, historianUsesPrimary, cancellationToken);
        DatabaseConnectionHealth? historian = null;
        if (!historianUsesPrimary)
            historian = await TestAsync(historianConnectionString, true, cancellationToken);

        var compatible = primary is { Reachable: true, FailureCode: null, SchemaCompatible: true } &&
            (historianUsesPrimary || historian is { Reachable: true, FailureCode: null, SchemaCompatible: true });

        return new(
            compatible,
            primary,
            historian,
            compatible ? null : "database-incompatible",
            compatible ? null : "One or more database compatibility checks failed.");
    }

    public async Task<DatabaseMigrationPlan> PrepareAsync(
        string sourceConnectionString,
        string targetConnectionString,
        string historianSourceConnectionString,
        string historianTargetConnectionString,
        bool historianUsesPrimary,
        CancellationToken cancellationToken = default)
    {
        EnsureDifferentDatabase(sourceConnectionString, targetConnectionString, "primary");
        if (!historianUsesPrimary)
            EnsureDifferentDatabase(historianSourceConnectionString, historianTargetConnectionString, "historian");

        await EnsureNoExistingRowsAsync(targetConnectionString, MeaningfulMainTables, cancellationToken);
        var historianTarget = historianUsesPrimary ? targetConnectionString : historianTargetConnectionString;
        await EnsureNoExistingRowsAsync(historianTarget, HistorianTables, cancellationToken);

        await PostgreSqlDeploymentDatabasePreparation.InitializeCoreAsync(targetConnectionString, cancellationToken);
        await TimescaleDbDeploymentPreparation.InitializeAsync(historianTarget, cancellationToken);
        await PostgreSqlDeploymentDatabasePreparation.InitializeHistoricalEventsAsync(historianTarget, cancellationToken);

        return new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            historianUsesPrimary,
            true,
            MainTables.Concat(HistorianTables).Distinct(StringComparer.Ordinal).ToArray(),
            DatabaseTopologyMode.LocalManaged.ToString(),
            DatabaseTopologyMode.Remote.ToString());
    }

    public async Task CopyAsync(
        DatabaseMigrationPlan plan,
        string sourceConnectionString,
        string targetConnectionString,
        string historianSourceConnectionString,
        string historianTargetConnectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        await CopyTableSetAsync(sourceConnectionString, targetConnectionString, MainTables, clearSeedState: true, cancellationToken);
        var historianTarget = plan.HistorianUsesPrimary ? targetConnectionString : historianTargetConnectionString;
        await CopyTableSetAsync(historianSourceConnectionString, historianTarget, HistorianTables, clearSeedState: false, cancellationToken);
        await ResetEngineeringRevisionSequenceAsync(targetConnectionString, cancellationToken);
    }

    public async Task<DatabaseMigrationVerification> VerifyAsync(
        DatabaseMigrationPlan plan,
        string sourceConnectionString,
        string targetConnectionString,
        string historianSourceConnectionString,
        string historianTargetConnectionString,
        CancellationToken cancellationToken = default)
    {
        var sourceRows = new Dictionary<string, long>(StringComparer.Ordinal);
        var targetRows = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var table in MainTables)
        {
            sourceRows[table] = await CountAsync(sourceConnectionString, table, cancellationToken);
            targetRows[table] = await CountAsync(targetConnectionString, table, cancellationToken);
        }

        var historianTarget = plan.HistorianUsesPrimary ? targetConnectionString : historianTargetConnectionString;
        foreach (var table in HistorianTables)
        {
            sourceRows[table] = await CountAsync(historianSourceConnectionString, table, cancellationToken);
            targetRows[table] = await CountAsync(historianTarget, table, cancellationToken);
        }

        var mismatches = sourceRows
            .Where(pair => targetRows.TryGetValue(pair.Key, out var target) && target != pair.Value)
            .Select(pair => pair.Key)
            .ToArray();
        var (projectKey, revision) = await ReadActiveRevisionAsync(targetConnectionString, cancellationToken);
        var succeeded = mismatches.Length == 0;

        return new(
            succeeded,
            sourceRows,
            targetRows,
            projectKey,
            revision,
            succeeded ? null : "migration-count-mismatch",
            succeeded ? null : "Row-count verification failed for: " + string.Join(", ", mismatches));
    }

    private static async Task CopyTableSetAsync(
        string sourceConnectionString,
        string targetConnectionString,
        IEnumerable<string> tables,
        bool clearSeedState,
        CancellationToken cancellationToken)
    {
        await using var sourceDataSource = NpgsqlDataSource.Create(sourceConnectionString);
        await using var targetDataSource = NpgsqlDataSource.Create(targetConnectionString);
        await using var source = await sourceDataSource.OpenConnectionAsync(cancellationToken);
        await using var target = await targetDataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await target.BeginTransactionAsync(cancellationToken);

        if (clearSeedState)
        {
            foreach (var table in SeedStateTables)
            {
                if (!await TableExistsAsync(target, table, cancellationToken)) continue;
                await using var delete = new NpgsqlCommand($"DELETE FROM {QuoteQualified(table)};", target, transaction);
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        foreach (var table in tables)
        {
            if (!await TableExistsAsync(source, table, cancellationToken) ||
                !await TableExistsAsync(target, table, cancellationToken))
                continue;
            await CopyRowsAsync(source, target, transaction, table, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task CopyRowsAsync(
        NpgsqlConnection source,
        NpgsqlConnection target,
        NpgsqlTransaction transaction,
        string table,
        CancellationToken cancellationToken)
    {
        var (schema, name) = SplitQualifiedName(table);
        await using var metadata = new NpgsqlCommand("""
            SELECT column_name, data_type, udt_name, is_identity
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @name
            ORDER BY ordinal_position;
            """, source);
        metadata.Parameters.AddWithValue("schema", schema);
        metadata.Parameters.AddWithValue("name", name);

        var columns = new List<ColumnMetadata>();
        await using (var reader = await metadata.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                columns.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3) == "YES"));
        }
        if (columns.Count == 0) return;

        var quotedColumns = string.Join(", ", columns.Select(column => QuoteIdentifier(column.Name)));
        await using var select = new NpgsqlCommand($"SELECT {quotedColumns} FROM {QuoteQualified(table)};", source);
        await using var sourceReader = await select.ExecuteReaderAsync(cancellationToken);

        var values = string.Join(", ", columns.Select((column, index) =>
            column.DataType is "json" or "jsonb" ? $"@p{index}::{column.DataType}" : $"@p{index}"));
        var overrideIdentity = columns.Any(column => column.IsIdentity) ? " OVERRIDING SYSTEM VALUE" : string.Empty;
        var insertSql =
            $"INSERT INTO {QuoteQualified(table)} ({quotedColumns}){overrideIdentity} VALUES ({values}) ON CONFLICT DO NOTHING;";

        while (await sourceReader.ReadAsync(cancellationToken))
        {
            await using var insert = new NpgsqlCommand(insertSql, target, transaction);
            for (var index = 0; index < columns.Count; index++)
            {
                object value;
                if (sourceReader.IsDBNull(index))
                {
                    value = DBNull.Value;
                }
                else if (columns[index].DataType is "json" or "jsonb")
                {
                    value = sourceReader.GetString(index);
                }
                else
                {
                    value = sourceReader.GetValue(index);
                }
                insert.Parameters.AddWithValue("p" + index, value);
            }
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ResetEngineeringRevisionSequenceAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await TableExistsAsync(connection, "elitescada.engineering_revisions", cancellationToken)) return;

        await using var sequence = new NpgsqlCommand(
            "SELECT pg_get_serial_sequence('elitescada.engineering_revisions', 'revision');",
            connection);
        var sequenceName = await sequence.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(sequenceName)) return;

        await using var maximum = new NpgsqlCommand(
            "SELECT COALESCE(max(revision), 0)::bigint FROM elitescada.engineering_revisions;",
            connection);
        var max = Convert.ToInt64(await maximum.ExecuteScalarAsync(cancellationToken));
        await using var reset = new NpgsqlCommand(
            max > 0
                ? "SELECT setval(@sequence::regclass, @value, true);"
                : "SELECT setval(@sequence::regclass, 1, false);",
            connection);
        reset.Parameters.AddWithValue("sequence", sequenceName);
        if (max > 0) reset.Parameters.AddWithValue("value", max);
        await reset.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureNoExistingRowsAsync(
        string connectionString,
        IEnumerable<string> tables,
        CancellationToken cancellationToken)
    {
        foreach (var table in tables)
        {
            if (await CountAsync(connectionString, table, cancellationToken) > 0)
                throw new InvalidOperationException(
                    $"Target already contains EliteSCADA data in '{table}'. Prepare will not overwrite an existing deployment.");
        }
    }

    private static async Task<long> CountAsync(
        string connectionString,
        string table,
        CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await TableExistsAsync(connection, table, cancellationToken)) return 0;
        await using var command = new NpgsqlCommand($"SELECT count(*)::bigint FROM {QuoteQualified(table)};", connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT to_regclass(@qualified) IS NOT NULL;", connection);
        command.Parameters.AddWithValue("qualified", table);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> HasCompatibleSchemaAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT to_regclass('elitescada.schema_migrations') IS NOT NULL
                OR NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'elitescada');
            """, connection);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<(string? ProjectKey, long? Revision)> ReadActiveRevisionAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await TableExistsAsync(connection, "elitescada.project_activations", cancellationToken))
            return (null, null);
        await using var command = new NpgsqlCommand("""
            SELECT project_key, active_revision
            FROM elitescada.project_activations
            ORDER BY activated_at_utc DESC
            LIMIT 1;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetString(0), reader.GetInt64(1))
            : (null, null);
    }

    private static void EnsureDifferentDatabase(string source, string target, string role)
    {
        var a = new NpgsqlConnectionStringBuilder(source);
        var b = new NpgsqlConnectionStringBuilder(target);
        if (string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase) &&
            a.Port == b.Port &&
            string.Equals(a.Database, b.Database, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Database migration {role} source and target must be different databases.");
    }

    private string BuildCompatibilityDiagnostic(int postgresMajor, string timescaleVersion, bool requireTimescale, bool schemaCompatible)
    {
        var parts = new List<string>();
        if (postgresMajor < options.MinimumPostgreSqlMajor || postgresMajor > options.MaximumPostgreSqlMajor)
            parts.Add($"PostgreSQL {postgresMajor} is outside supported range {options.MinimumPostgreSqlMajor}-{options.MaximumPostgreSqlMajor}.");
        if (requireTimescale && (string.IsNullOrWhiteSpace(timescaleVersion) || ParseMajor(timescaleVersion) < options.MinimumTimescaleMajor))
            parts.Add($"TimescaleDB {options.MinimumTimescaleMajor}+ is required.");
        if (!schemaCompatible) parts.Add("EliteSCADA schema is incompatible.");
        return string.Join(" ", parts);
    }

    private static int ParseMajor(string version)
    {
        var token = version.Split('.', '-', '+', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(token, out var major) ? major : 0;
    }

    private static string Classify(Exception ex) => ex switch
    {
        PostgresException postgres when postgres.SqlState == "28P01" => "authentication-failed",
        PostgresException postgres when postgres.SqlState == "28000" => "authentication-failed",
        NpgsqlException npgsql when npgsql.InnerException is System.Security.Authentication.AuthenticationException => "tls-validation-failed",
        TimeoutException => "connection-timeout",
        FileNotFoundException => "tls-validation-failed",
        _ => "connection-failed"
    };

    private static string DiagnosticFor(string code) => code switch
    {
        "authentication-failed" => "Database authentication failed.",
        "tls-validation-failed" => "Database TLS/trust validation failed.",
        "connection-timeout" => "Database connection timed out.",
        _ => "Database connection failed."
    };

    private static (string Schema, string Name) SplitQualifiedName(string table)
    {
        var split = table.Split('.', 2);
        if (split.Length != 2) throw new ArgumentException("Qualified table name is required.", nameof(table));
        return (split[0], split[1]);
    }

    private static string QuoteQualified(string value)
    {
        var (schema, name) = SplitQualifiedName(value);
        return QuoteIdentifier(schema) + "." + QuoteIdentifier(name);
    }

    private static string QuoteIdentifier(string value) =>
        string.Concat("\"", value.Replace("\"", "\"\"", StringComparison.Ordinal), "\"");

    private sealed record ColumnMetadata(string Name, string DataType, string UdtName, bool IsIdentity);
}
