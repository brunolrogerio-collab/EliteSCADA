using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Scada.Engineering.Persistence;

namespace Scada.Persistence.PostgreSql;

public sealed class PostgreSqlEngineeringProjectStore : IEngineeringProjectStore, IAsyncDisposable
{
    private const string InitializeSql = """
        CREATE SCHEMA IF NOT EXISTS elitescada;

        CREATE TABLE IF NOT EXISTS elitescada.schema_migrations (
            migration_key text PRIMARY KEY,
            applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
        );

        CREATE TABLE IF NOT EXISTS elitescada.engineering_revisions (
            revision bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            project_key varchar(200) NOT NULL,
            project_name varchar(300) NOT NULL,
            engineering_schema varchar(100) NOT NULL,
            engineering_schema_version integer NOT NULL CHECK (engineering_schema_version > 0),
            saved_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
            saved_by varchar(300) NULL,
            payload jsonb NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_engineering_revisions_project_revision
            ON elitescada.engineering_revisions (project_key, revision DESC);

        CREATE TABLE IF NOT EXISTS elitescada.project_publications (
            project_key varchar(200) PRIMARY KEY,
            published_revision bigint NOT NULL REFERENCES elitescada.engineering_revisions(revision),
            published_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
            published_by varchar(300) NULL
        );

        INSERT INTO elitescada.schema_migrations (migration_key)
        VALUES ('001_engineering_revisions')
        ON CONFLICT (migration_key) DO NOTHING;

        INSERT INTO elitescada.schema_migrations (migration_key)
        VALUES ('002_project_publications')
        ON CONFLICT (migration_key) DO NOTHING;
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlEngineeringProjectStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));

        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(InitializeSql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<EngineeringProjectSnapshot> SaveAsync(
        string projectKey,
        string projectName,
        string engineeringSchema,
        int engineeringSchemaVersion,
        string engineeringJson,
        string? savedBy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(projectKey, projectName, engineeringSchema, engineeringSchemaVersion);
        ValidateJson(engineeringJson);

        const string sql = """
            INSERT INTO elitescada.engineering_revisions (
                project_key,
                project_name,
                engineering_schema,
                engineering_schema_version,
                saved_by,
                payload)
            VALUES (
                @project_key,
                @project_name,
                @engineering_schema,
                @engineering_schema_version,
                @saved_by,
                @payload)
            RETURNING revision, saved_at_utc;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_key", projectKey.Trim());
        command.Parameters.AddWithValue("project_name", projectName.Trim());
        command.Parameters.AddWithValue("engineering_schema", engineeringSchema.Trim());
        command.Parameters.AddWithValue("engineering_schema_version", engineeringSchemaVersion);
        command.Parameters.AddWithValue("saved_by", NpgsqlDbType.Varchar, (object?)NormalizeOptional(savedBy) ?? DBNull.Value);
        command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, engineeringJson);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("PostgreSQL did not return the saved engineering revision.");

        return new EngineeringProjectSnapshot(
            reader.GetInt64(0),
            projectKey.Trim(),
            projectName.Trim(),
            engineeringSchema.Trim(),
            engineeringSchemaVersion,
            ReadTimestamp(reader, 1),
            engineeringJson,
            NormalizeOptional(savedBy));
    }

    public async Task<EngineeringProjectSnapshot?> LoadLatestAsync(
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectKey(projectKey);

        const string sql = """
            SELECT
                revision,
                project_key,
                project_name,
                engineering_schema,
                engineering_schema_version,
                saved_at_utc,
                payload::text,
                saved_by
            FROM elitescada.engineering_revisions
            WHERE project_key = @project_key
            ORDER BY revision DESC
            LIMIT 1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_key", projectKey.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadSnapshot(reader) : null;
    }

    public async Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
        string projectKey,
        long revision,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectKey(projectKey);
        if (revision < 1)
            throw new ArgumentOutOfRangeException(nameof(revision));

        const string sql = """
            SELECT
                revision,
                project_key,
                project_name,
                engineering_schema,
                engineering_schema_version,
                saved_at_utc,
                payload::text,
                saved_by
            FROM elitescada.engineering_revisions
            WHERE project_key = @project_key AND revision = @revision
            LIMIT 1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_key", projectKey.Trim());
        command.Parameters.AddWithValue("revision", revision);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadSnapshot(reader) : null;
    }

    public async Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
        string projectKey,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectKey(projectKey);
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit), "Revision list limit must be between 1 and 500.");

        const string sql = """
            SELECT
                revision,
                project_key,
                project_name,
                engineering_schema,
                engineering_schema_version,
                saved_at_utc,
                payload::text,
                saved_by
            FROM elitescada.engineering_revisions
            WHERE project_key = @project_key
            ORDER BY revision DESC
            LIMIT @limit;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_key", projectKey.Trim());
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var revisions = new List<EngineeringProjectSnapshot>();
        while (await reader.ReadAsync(cancellationToken))
            revisions.Add(ReadSnapshot(reader));

        return revisions;
    }

    public async Task<EngineeringProjectPublication?> GetPublicationAsync(
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectKey(projectKey);

        const string sql = """
            SELECT project_key, published_revision, published_at_utc, published_by
            FROM elitescada.project_publications
            WHERE project_key = @project_key;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_key", projectKey.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadPublication(reader) : null;
    }

    public async Task<EngineeringProjectPublication?> PublishRevisionAsync(
        string projectKey,
        long revision,
        string? publishedBy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectKey(projectKey);
        if (revision < 1)
            throw new ArgumentOutOfRangeException(nameof(revision));

        const string sql = """
            INSERT INTO elitescada.project_publications (
                project_key,
                published_revision,
                published_at_utc,
                published_by)
            SELECT
                @project_key,
                revision,
                clock_timestamp(),
                @published_by
            FROM elitescada.engineering_revisions
            WHERE project_key = @project_key AND revision = @revision
            ON CONFLICT (project_key) DO UPDATE SET
                published_revision = EXCLUDED.published_revision,
                published_at_utc = EXCLUDED.published_at_utc,
                published_by = EXCLUDED.published_by
            RETURNING project_key, published_revision, published_at_utc, published_by;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_key", projectKey.Trim());
        command.Parameters.AddWithValue("revision", revision);
        command.Parameters.AddWithValue("published_by", NpgsqlDbType.Varchar, (object?)NormalizeOptional(publishedBy) ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadPublication(reader) : null;
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    private static EngineeringProjectSnapshot ReadSnapshot(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetInt32(4),
        ReadTimestamp(reader, 5),
        reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7));

    private static EngineeringProjectPublication ReadPublication(NpgsqlDataReader reader) => new(
        reader.GetString(0),
        reader.GetInt64(1),
        ReadTimestamp(reader, 2),
        reader.IsDBNull(3) ? null : reader.GetString(3));

    private static DateTimeOffset ReadTimestamp(NpgsqlDataReader reader, int ordinal)
    {
        var value = reader.GetFieldValue<DateTime>(ordinal);
        return new DateTimeOffset(value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime());
    }

    private static void ValidateIdentity(
        string projectKey,
        string projectName,
        string engineeringSchema,
        int engineeringSchemaVersion)
    {
        ValidateProjectKey(projectKey);
        if (string.IsNullOrWhiteSpace(projectName))
            throw new ArgumentException("Project name is required.", nameof(projectName));
        if (string.IsNullOrWhiteSpace(engineeringSchema))
            throw new ArgumentException("Engineering schema is required.", nameof(engineeringSchema));
        if (engineeringSchemaVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(engineeringSchemaVersion));
    }

    private static void ValidateProjectKey(string projectKey)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));
    }

    private static void ValidateJson(string engineeringJson)
    {
        if (string.IsNullOrWhiteSpace(engineeringJson))
            throw new ArgumentException("Engineering JSON is required.", nameof(engineeringJson));

        try
        {
            using var document = JsonDocument.Parse(engineeringJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Engineering JSON root must be an object.", nameof(engineeringJson));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Engineering JSON is invalid.", nameof(engineeringJson), ex);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
