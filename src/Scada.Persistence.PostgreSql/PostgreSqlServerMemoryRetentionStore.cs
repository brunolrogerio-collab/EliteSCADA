using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;

namespace Scada.Persistence.PostgreSql;

/// <summary>
/// Durable, write-through storage for Server Memory values. Rows are keyed only
/// by stable TAG ID and are intentionally independent from Engineering revision
/// ownership. A successful WriteAsync means PostgreSQL has acknowledged the value.
/// </summary>
public sealed class PostgreSqlServerMemoryRetentionStore : IServerMemoryRetentionStore, IAsyncDisposable
{
    private const string InitializeSql = """
        SELECT pg_advisory_xact_lock(4993446713136202562);

        CREATE SCHEMA IF NOT EXISTS elitescada;

        CREATE TABLE IF NOT EXISTS elitescada.schema_migrations (
            migration_key text PRIMARY KEY,
            applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
        );

        CREATE TABLE IF NOT EXISTS elitescada.server_memory_retained_values (
            tag_id uuid PRIMARY KEY,
            data_type smallint NOT NULL,
            value jsonb NOT NULL,
            stored_at_utc timestamptz NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_server_memory_retained_values_stored_at
            ON elitescada.server_memory_retained_values (stored_at_utc DESC);

        INSERT INTO elitescada.schema_migrations (migration_key)
        VALUES ('008_server_memory_retention')
        ON CONFLICT (migration_key) DO NOTHING;
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlServerMemoryRetentionStore(string connectionString)
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

    public async ValueTask<RetainedMemoryValue?> ReadAsync(
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT data_type, value::text, stored_at_utc
            FROM elitescada.server_memory_retained_values
            WHERE tag_id = @tag_id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, tagId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var dataTypeRaw = reader.GetInt16(0);
        if (!Enum.IsDefined(typeof(TagDataType), (int)dataTypeRaw))
            throw new InvalidDataException($"Retained Server Memory TAG '{tagId}' has unknown data type value {dataTypeRaw}.");

        var dataType = (TagDataType)dataTypeRaw;
        var json = reader.GetString(1);
        var storedAtUtc = reader.GetDateTime(2);
        if (storedAtUtc.Kind != DateTimeKind.Utc)
            storedAtUtc = DateTime.SpecifyKind(storedAtUtc, DateTimeKind.Utc);

        return new RetainedMemoryValue(
            tagId,
            new TypedTagValue(dataType, DeserializeValue(dataType, json)),
            new DateTimeOffset(storedAtUtc));
    }

    public async ValueTask WriteAsync(
        RetainedMemoryValue value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        var json = JsonSerializer.Serialize(value.TypedValue.Value, value.TypedValue.Value.GetType());

        const string sql = """
            INSERT INTO elitescada.server_memory_retained_values (
                tag_id,
                data_type,
                value,
                stored_at_utc)
            VALUES (
                @tag_id,
                @data_type,
                @value,
                @stored_at_utc)
            ON CONFLICT (tag_id) DO UPDATE SET
                data_type = EXCLUDED.data_type,
                value = EXCLUDED.value,
                stored_at_utc = EXCLUDED.stored_at_utc;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, value.TagId);
        command.Parameters.AddWithValue("data_type", NpgsqlDbType.Smallint, (short)value.TypedValue.DataType);
        command.Parameters.AddWithValue("value", NpgsqlDbType.Jsonb, json);
        command.Parameters.AddWithValue("stored_at_utc", NpgsqlDbType.TimestampTz, value.StoredAt.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DeleteAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM elitescada.server_memory_retained_values
            WHERE tag_id = @tag_id;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, tagId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    private static object DeserializeValue(TagDataType dataType, string json)
    {
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement;

        return dataType switch
        {
            TagDataType.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
            TagDataType.Int16 when value.ValueKind == JsonValueKind.Number && value.TryGetInt16(out var int16Value) => int16Value,
            TagDataType.Int32 when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var int32Value) => int32Value,
            TagDataType.Int64 when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var int64Value) => int64Value,
            TagDataType.Float when value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var floatValue) && float.IsFinite(floatValue) => floatValue,
            TagDataType.Double when value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var doubleValue) && double.IsFinite(doubleValue) => doubleValue,
            TagDataType.String when value.ValueKind == JsonValueKind.String => value.GetString()!,
            TagDataType.DateTime when value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var dateTimeValue) => dateTimeValue,
            TagDataType.Enum when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var enumValue) => enumValue,
            _ => throw new InvalidDataException($"Retained Server Memory value is invalid for declared data type {dataType}.")
        };
    }
}
