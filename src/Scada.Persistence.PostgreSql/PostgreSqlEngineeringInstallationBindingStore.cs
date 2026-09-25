using Npgsql;
using NpgsqlTypes;
using Scada.Engineering.Persistence;

namespace Scada.Persistence.PostgreSql;

public sealed class PostgreSqlEngineeringInstallationBindingStore : IEngineeringInstallationBindingStore, IAsyncDisposable
{
    private const long BindingMutationAdvisoryLock = 4993446713136202566;
    private const string StateKey = "engineering-installation-binding-v1";
    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlEngineeringInstallationBindingStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE SCHEMA IF NOT EXISTS elitescada;
            CREATE TABLE IF NOT EXISTS elitescada.schema_migrations (
                migration_key text PRIMARY KEY,
                applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp());
            CREATE TABLE IF NOT EXISTS elitescada.engineering_installation_binding (
                state_key text PRIMARY KEY,
                state text NOT NULL CONSTRAINT engineering_installation_binding_state_allowed
                    CHECK (state IN ('Legacy', 'Neutral', 'AttachInProgress', 'Attached', 'DetachInProgress')),
                project_key varchar(200) NULL,
                generation bigint NOT NULL CHECK (generation > 0),
                updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT engineering_installation_binding_project_shape CHECK (
                    (state IN ('Legacy', 'Neutral') AND project_key IS NULL) OR
                    (state IN ('AttachInProgress', 'Attached', 'DetachInProgress') AND project_key IS NOT NULL)));
            INSERT INTO elitescada.schema_migrations (migration_key)
            VALUES ('006_engineering_installation_binding')
            ON CONFLICT (migration_key) DO NOTHING;
            INSERT INTO elitescada.engineering_installation_binding (state_key, state, project_key, generation)
            VALUES ('engineering-installation-binding-v1', 'Legacy', NULL, 1)
            ON CONFLICT (state_key) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await PostgreSqlSharedSchemaInitialization.AcquireLockAsync(connection, transaction, cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<EngineeringInstallationBindingSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await LoadAsync(connection, null, cancellationToken);
    }

    public Task<EngineeringInstallationBindingSnapshot> AdoptLegacyAsync(
        string? projectKey,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeOptional(projectKey);
        return TransitionAsync(
            expected: EngineeringInstallationBindingState.Legacy,
            next: normalized is null ? EngineeringInstallationBindingState.Neutral : EngineeringInstallationBindingState.Attached,
            expectedProjectKey: null,
            nextProjectKey: normalized,
            advanceGeneration: false,
            cancellationToken);
    }

    public Task<EngineeringInstallationBindingSnapshot> BeginAttachAsync(
        string projectKey,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            EngineeringInstallationBindingState.Neutral,
            EngineeringInstallationBindingState.AttachInProgress,
            expectedProjectKey: null,
            nextProjectKey: NormalizeRequired(projectKey),
            advanceGeneration: false,
            cancellationToken);

    public Task<EngineeringInstallationBindingSnapshot> CompleteAttachAsync(
        string projectKey,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            EngineeringInstallationBindingState.AttachInProgress,
            EngineeringInstallationBindingState.Attached,
            expectedProjectKey: NormalizeRequired(projectKey),
            nextProjectKey: NormalizeRequired(projectKey),
            advanceGeneration: true,
            cancellationToken);

    public Task<EngineeringInstallationBindingSnapshot> AbortAttachAsync(
        string projectKey,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            EngineeringInstallationBindingState.AttachInProgress,
            EngineeringInstallationBindingState.Neutral,
            expectedProjectKey: NormalizeRequired(projectKey),
            nextProjectKey: null,
            advanceGeneration: true,
            cancellationToken);

    public Task<EngineeringInstallationBindingSnapshot> BeginDetachAsync(
        string projectKey,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            EngineeringInstallationBindingState.Attached,
            EngineeringInstallationBindingState.DetachInProgress,
            expectedProjectKey: NormalizeRequired(projectKey),
            nextProjectKey: NormalizeRequired(projectKey),
            advanceGeneration: false,
            cancellationToken);

    public Task<EngineeringInstallationBindingSnapshot> CompleteDetachAsync(
        string projectKey,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            EngineeringInstallationBindingState.DetachInProgress,
            EngineeringInstallationBindingState.Neutral,
            expectedProjectKey: NormalizeRequired(projectKey),
            nextProjectKey: null,
            advanceGeneration: true,
            cancellationToken);

    private async Task<EngineeringInstallationBindingSnapshot> TransitionAsync(
        EngineeringInstallationBindingState expected,
        EngineeringInstallationBindingState next,
        string? expectedProjectKey,
        string? nextProjectKey,
        bool advanceGeneration,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var mutation = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@binding_key);",
            connection,
            transaction))
        {
            mutation.Parameters.AddWithValue("binding_key", NpgsqlDbType.Bigint, BindingMutationAdvisoryLock);
            await mutation.ExecuteNonQueryAsync(cancellationToken);
        }

        var current = await LoadAsync(connection, transaction, cancellationToken);
        if (current.State == next &&
            SameProject(current.ProjectKey, nextProjectKey))
        {
            await transaction.CommitAsync(cancellationToken);
            return current;
        }

        if (current.State != expected || !SameProject(current.ProjectKey, expectedProjectKey))
        {
            throw new InvalidOperationException(
                $"Engineering installation binding cannot transition from '{current.State}'/" +
                $"'{current.ProjectKey ?? "none"}' to '{next}'/'{nextProjectKey ?? "none"}'.");
        }

        var generation = advanceGeneration ? checked(current.Generation + 1) : current.Generation;
        await using (var update = new NpgsqlCommand(
            """
            UPDATE elitescada.engineering_installation_binding
            SET state = @state,
                project_key = @project_key,
                generation = @generation,
                updated_at_utc = clock_timestamp()
            WHERE state_key = @state_key;
            """,
            connection,
            transaction))
        {
            update.Parameters.AddWithValue("state", next.ToString());
            update.Parameters.AddWithValue("project_key", NpgsqlDbType.Varchar, (object?)nextProjectKey ?? DBNull.Value);
            update.Parameters.AddWithValue("generation", NpgsqlDbType.Bigint, generation);
            update.Parameters.AddWithValue("state_key", StateKey);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Engineering installation binding state is missing.");
        }

        await transaction.CommitAsync(cancellationToken);
        return new EngineeringInstallationBindingSnapshot(next, nextProjectKey, generation, DateTimeOffset.UtcNow);
    }

    private static async Task<EngineeringInstallationBindingSnapshot> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT state, project_key, generation, updated_at_utc
            FROM elitescada.engineering_installation_binding
            WHERE state_key = @state_key;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("state_key", StateKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Engineering installation binding state is missing.");

        var stateText = reader.GetString(0);
        if (!Enum.TryParse<EngineeringInstallationBindingState>(stateText, ignoreCase: false, out var state))
            throw new InvalidOperationException("Engineering installation binding state is incompatible.");
        var projectKey = reader.IsDBNull(1) ? null : reader.GetString(1);
        var generation = reader.GetInt64(2);
        var updated = reader.GetFieldValue<DateTime>(3);
        return new EngineeringInstallationBindingSnapshot(
            state,
            projectKey,
            generation,
            new DateTimeOffset(updated.Kind == DateTimeKind.Utc ? updated : updated.ToUniversalTime()));
    }

    private static bool SameProject(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeRequired(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
