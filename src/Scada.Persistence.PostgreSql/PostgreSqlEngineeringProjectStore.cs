using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Scada.Engineering.Persistence;

namespace Scada.Persistence.PostgreSql;

public sealed class PostgreSqlEngineeringProjectStore : IEngineeringProjectStore, IAsyncDisposable
{
    private const string InitializeSql = """
        SELECT pg_advisory_xact_lock(4993446713136202561);

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
            based_on_revision bigint NULL,
            payload jsonb NOT NULL
        );

        ALTER TABLE elitescada.engineering_revisions
            ADD COLUMN IF NOT EXISTS based_on_revision bigint NULL;

        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conname = 'fk_engineering_revisions_based_on_revision'
                  AND conrelid = 'elitescada.engineering_revisions'::regclass
            ) THEN
                ALTER TABLE elitescada.engineering_revisions
                    ADD CONSTRAINT fk_engineering_revisions_based_on_revision
                    FOREIGN KEY (based_on_revision)
                    REFERENCES elitescada.engineering_revisions(revision);
            END IF;
        END
        $$;

        CREATE INDEX IF NOT EXISTS ix_engineering_revisions_project_revision
            ON elitescada.engineering_revisions (project_key, revision DESC);

        CREATE INDEX IF NOT EXISTS ix_engineering_revisions_based_on
            ON elitescada.engineering_revisions (based_on_revision)
            WHERE based_on_revision IS NOT NULL;

        CREATE TABLE IF NOT EXISTS elitescada.project_publications (
            project_key varchar(200) PRIMARY KEY,
            published_revision bigint NOT NULL REFERENCES elitescada.engineering_revisions(revision),
            published_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
            published_by varchar(300) NULL
        );

        CREATE TABLE IF NOT EXISTS elitescada.project_activations (
            project_key varchar(200) PRIMARY KEY,
            active_revision bigint NOT NULL REFERENCES elitescada.engineering_revisions(revision),
            activated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
            activated_by varchar(300) NULL
        );

        CREATE TABLE IF NOT EXISTS elitescada.engineering_asset_blobs (
            sha256 char(64) PRIMARY KEY,
            media_type varchar(100) NOT NULL,
            byte_length bigint NOT NULL CHECK (byte_length > 0),
            payload bytea NOT NULL,
            created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
        );

        CREATE TABLE IF NOT EXISTS elitescada.engineering_revision_assets (
            revision bigint NOT NULL REFERENCES elitescada.engineering_revisions(revision) ON DELETE CASCADE,
            project_key varchar(200) NOT NULL,
            asset_id uuid NOT NULL,
            sha256 char(64) NOT NULL REFERENCES elitescada.engineering_asset_blobs(sha256),
            PRIMARY KEY (revision, asset_id)
        );

        CREATE INDEX IF NOT EXISTS ix_engineering_revision_assets_project_revision
            ON elitescada.engineering_revision_assets (project_key, revision);

        CREATE INDEX IF NOT EXISTS ix_engineering_revision_assets_sha256
            ON elitescada.engineering_revision_assets (sha256);

        INSERT INTO elitescada.schema_migrations (migration_key)
        VALUES ('001_engineering_revisions')
        ON CONFLICT (migration_key) DO NOTHING;

        INSERT INTO elitescada.schema_migrations (migration_key)
        VALUES ('002_project_publications')
        ON CONFLICT (migration_key) DO NOTHING;

        INSERT INTO elitescada.schema_migrations (migration_key)
        VALUES ('003_project_activations')
        ON CONFLICT (migration_key) DO NOTHING;

        INSERT INTO elitescada.schema_migrations (migration_key)
        VALUES ('004_engineering_revision_lineage')
        ON CONFLICT (migration_key) DO NOTHING;

        INSERT INTO elitescada.schema_migrations (migration_key)
        VALUES ('005_engineering_visual_asset_blobs')
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
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(InitializeSql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<EngineeringProjectSnapshot> SaveAsync(
        string projectKey,
        string projectName,
        string engineeringSchema,
        int engineeringSchemaVersion,
        string engineeringJson,
        string? savedBy = null,
        CancellationToken cancellationToken = default) =>
        SaveDerivedWithAssetsAsync(
            projectKey,
            projectName,
            engineeringSchema,
            engineeringSchemaVersion,
            engineeringJson,
            null,
            Array.Empty<EngineeringRevisionAssetPayload>(),
            savedBy,
            cancellationToken);

    public Task<EngineeringProjectSnapshot> SaveDerivedAsync(
        string projectKey,
        string projectName,
        string engineeringSchema,
        int engineeringSchemaVersion,
        string engineeringJson,
        long? basedOnRevision,
        string? savedBy = null,
        CancellationToken cancellationToken = default) =>
        SaveDerivedWithAssetsAsync(
            projectKey,
            projectName,
            engineeringSchema,
            engineeringSchemaVersion,
            engineeringJson,
            basedOnRevision,
            Array.Empty<EngineeringRevisionAssetPayload>(),
            savedBy,
            cancellationToken);

    public async Task<EngineeringProjectSnapshot> SaveDerivedWithAssetsAsync(
        string projectKey,
        string projectName,
        string engineeringSchema,
        int engineeringSchemaVersion,
        string engineeringJson,
        long? basedOnRevision,
        IReadOnlyCollection<EngineeringRevisionAssetPayload> assets,
        string? savedBy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(projectKey, projectName, engineeringSchema, engineeringSchemaVersion);
        ValidateJson(engineeringJson);
        ArgumentNullException.ThrowIfNull(assets);
        if (basedOnRevision is < 1)
            throw new ArgumentOutOfRangeException(nameof(basedOnRevision));

        var normalizedProjectKey = projectKey.Trim();
        var normalizedAssets = NormalizeAssets(assets);

        const string insertRevisionSql = """
            INSERT INTO elitescada.engineering_revisions (
                project_key,
                project_name,
                engineering_schema,
                engineering_schema_version,
                saved_by,
                based_on_revision,
                payload)
            SELECT
                @project_key,
                @project_name,
                @engineering_schema,
                @engineering_schema_version,
                @saved_by,
                @based_on_revision,
                @payload
            WHERE @based_on_revision IS NULL
               OR EXISTS (
                    SELECT 1
                    FROM elitescada.engineering_revisions parent
                    WHERE parent.project_key = @project_key
                      AND parent.revision = @based_on_revision)
            RETURNING revision, saved_at_utc;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(insertRevisionSql, connection, transaction);
        command.Parameters.AddWithValue("project_key", normalizedProjectKey);
        command.Parameters.AddWithValue("project_name", projectName.Trim());
        command.Parameters.AddWithValue("engineering_schema", engineeringSchema.Trim());
        command.Parameters.AddWithValue("engineering_schema_version", engineeringSchemaVersion);
        command.Parameters.AddWithValue("saved_by", NpgsqlDbType.Varchar, (object?)NormalizeOptional(savedBy) ?? DBNull.Value);
        command.Parameters.AddWithValue("based_on_revision", NpgsqlDbType.Bigint, (object?)basedOnRevision ?? DBNull.Value);
        command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, engineeringJson);

        long revision;
        DateTimeOffset savedAtUtc;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    basedOnRevision.HasValue
                        ? $"Engineering base revision {basedOnRevision} does not belong to project '{normalizedProjectKey}'."
                        : "PostgreSQL did not return the saved engineering revision.");
            }

            revision = reader.GetInt64(0);
            savedAtUtc = ReadTimestamp(reader, 1);
        }

        foreach (var payload in normalizedAssets
                     .GroupBy(x => x.Sha256, StringComparer.OrdinalIgnoreCase)
                     .Select(x => x.First()))
            await EnsureBlobAsync(connection, transaction, payload, cancellationToken);

        const string linkSql = """
            INSERT INTO elitescada.engineering_revision_assets (
                revision,
                project_key,
                asset_id,
                sha256)
            VALUES (@revision, @project_key, @asset_id, @sha256);
            """;

        foreach (var payload in normalizedAssets)
        {
            await using var link = new NpgsqlCommand(linkSql, connection, transaction);
            link.Parameters.AddWithValue("revision", revision);
            link.Parameters.AddWithValue("project_key", normalizedProjectKey);
            link.Parameters.AddWithValue("asset_id", payload.AssetId);
            link.Parameters.AddWithValue("sha256", payload.Sha256);
            await link.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new EngineeringProjectSnapshot(
            revision,
            normalizedProjectKey,
            projectName.Trim(),
            engineeringSchema.Trim(),
            engineeringSchemaVersion,
            savedAtUtc,
            engineeringJson,
            NormalizeOptional(savedBy),
            basedOnRevision);
    }

    public async Task<IReadOnlyCollection<EngineeringRevisionAssetPayload>> LoadRevisionAssetsAsync(
        string projectKey,
        long revision,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectKey(projectKey);
        if (revision < 1)
            throw new ArgumentOutOfRangeException(nameof(revision));

        const string sql = """
            SELECT
                links.asset_id,
                links.sha256,
                blobs.media_type,
                blobs.payload
            FROM elitescada.engineering_revision_assets links
            INNER JOIN elitescada.engineering_asset_blobs blobs ON blobs.sha256 = links.sha256
            INNER JOIN elitescada.engineering_revisions revisions ON revisions.revision = links.revision
            WHERE links.project_key = @project_key
              AND links.revision = @revision
              AND revisions.project_key = @project_key
            ORDER BY links.asset_id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_key", projectKey.Trim());
        command.Parameters.AddWithValue("revision", revision);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var assets = new List<EngineeringRevisionAssetPayload>();
        while (await reader.ReadAsync(cancellationToken))
        {
            assets.Add(new EngineeringRevisionAssetPayload(
                reader.GetGuid(0),
                reader.GetString(1).Trim(),
                reader.GetString(2),
                reader.GetFieldValue<byte[]>(3)));
        }

        return assets;
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
                saved_by,
                based_on_revision
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
                saved_by,
                based_on_revision
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
                saved_by,
                based_on_revision
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

    public async Task<EngineeringProjectActivation?> GetActivationAsync(
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectKey(projectKey);

        const string sql = """
            SELECT project_key, active_revision, activated_at_utc, activated_by
            FROM elitescada.project_activations
            WHERE project_key = @project_key;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_key", projectKey.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadActivation(reader) : null;
    }

    public async Task<EngineeringProjectActivation?> RecordActivationAsync(
        string projectKey,
        long revision,
        string? activatedBy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectKey(projectKey);
        if (revision < 1)
            throw new ArgumentOutOfRangeException(nameof(revision));

        const string sql = """
            INSERT INTO elitescada.project_activations (
                project_key,
                active_revision,
                activated_at_utc,
                activated_by)
            SELECT
                project_key,
                published_revision,
                clock_timestamp(),
                @activated_by
            FROM elitescada.project_publications
            WHERE project_key = @project_key AND published_revision = @revision
            ON CONFLICT (project_key) DO UPDATE SET
                active_revision = EXCLUDED.active_revision,
                activated_at_utc = EXCLUDED.activated_at_utc,
                activated_by = EXCLUDED.activated_by
            RETURNING project_key, active_revision, activated_at_utc, activated_by;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_key", projectKey.Trim());
        command.Parameters.AddWithValue("revision", revision);
        command.Parameters.AddWithValue("activated_by", NpgsqlDbType.Varchar, (object?)NormalizeOptional(activatedBy) ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadActivation(reader) : null;
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    private static async Task EnsureBlobAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        EngineeringRevisionAssetPayload payload,
        CancellationToken cancellationToken)
    {
        const string insertSql = """
            INSERT INTO elitescada.engineering_asset_blobs (sha256, media_type, byte_length, payload)
            VALUES (@sha256, @media_type, @byte_length, @payload)
            ON CONFLICT (sha256) DO NOTHING;
            """;

        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            insert.Parameters.AddWithValue("sha256", payload.Sha256);
            insert.Parameters.AddWithValue("media_type", payload.MediaType);
            insert.Parameters.AddWithValue("byte_length", payload.ByteLength);
            insert.Parameters.AddWithValue("payload", NpgsqlDbType.Bytea, payload.Content);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        const string verifySql = """
            SELECT media_type, byte_length, payload
            FROM elitescada.engineering_asset_blobs
            WHERE sha256 = @sha256;
            """;
        await using var verify = new NpgsqlCommand(verifySql, connection, transaction);
        verify.Parameters.AddWithValue("sha256", payload.Sha256);
        await using var reader = await verify.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidDataException($"Visual asset blob '{payload.Sha256}' could not be persisted.");

        var storedMediaType = reader.GetString(0);
        var storedLength = reader.GetInt64(1);
        var storedPayload = reader.GetFieldValue<byte[]>(2);
        if (!storedMediaType.Equals(payload.MediaType, StringComparison.OrdinalIgnoreCase) ||
            storedLength != payload.ByteLength ||
            !storedPayload.AsSpan().SequenceEqual(payload.Content))
            throw new InvalidDataException($"Visual asset blob '{payload.Sha256}' conflicts with previously persisted content.");
    }

    private static EngineeringRevisionAssetPayload[] NormalizeAssets(
        IReadOnlyCollection<EngineeringRevisionAssetPayload> assets)
    {
        var duplicateAssetId = assets.GroupBy(x => x.AssetId).FirstOrDefault(x => x.Count() > 1);
        if (duplicateAssetId is not null)
            throw new InvalidDataException($"Visual asset ID '{duplicateAssetId.Key}' appears more than once in the revision payload set.");

        return assets.Select(asset =>
        {
            if (asset.AssetId == Guid.Empty)
                throw new InvalidDataException("Visual asset revision payload requires a non-empty asset ID.");
            if (string.IsNullOrWhiteSpace(asset.MediaType))
                throw new InvalidDataException($"Visual asset '{asset.AssetId}' media type is required.");
            if (asset.Content is null || asset.Content.Length == 0)
                throw new InvalidDataException($"Visual asset '{asset.AssetId}' content is required.");

            var actualHash = Convert.ToHexString(SHA256.HashData(asset.Content)).ToLowerInvariant();
            if (!actualHash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Visual asset '{asset.AssetId}' SHA-256 does not match its content.");

            return asset with
            {
                Sha256 = actualHash,
                Content = asset.Content.ToArray()
            };
        }).ToArray();
    }

    private static EngineeringProjectSnapshot ReadSnapshot(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetInt32(4),
        ReadTimestamp(reader, 5),
        reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetInt64(8));

    private static EngineeringProjectPublication ReadPublication(NpgsqlDataReader reader) => new(
        reader.GetString(0),
        reader.GetInt64(1),
        ReadTimestamp(reader, 2),
        reader.IsDBNull(3) ? null : reader.GetString(3));

    private static EngineeringProjectActivation ReadActivation(NpgsqlDataReader reader) => new(
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