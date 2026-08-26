using System.Text.Json;
using System.Threading.Channels;
using Npgsql;
using NpgsqlTypes;
using Scada.Core.Abstractions;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Historian.Abstractions;

namespace Scada.Historian.TimescaleDb;

public sealed class TimescaleDbHistorian : IHistorian
{
    private const string InitializeSql = """
        CREATE EXTENSION IF NOT EXISTS timescaledb;
        CREATE SCHEMA IF NOT EXISTS elitescada;

        CREATE TABLE IF NOT EXISTS elitescada.tag_history (
            tag_id uuid NOT NULL,
            ts timestamptz NOT NULL,
            quality integer NOT NULL,
            source text NULL,
            value jsonb NOT NULL
        );

        SELECT create_hypertable(
            'elitescada.tag_history',
            by_range('ts'),
            if_not_exists => TRUE);

        CREATE INDEX IF NOT EXISTS ix_tag_history_tag_time
            ON elitescada.tag_history (tag_id, ts DESC);
        """;

    private const string InsertSql = """
        INSERT INTO elitescada.tag_history (tag_id, ts, quality, source, value)
        VALUES (@tag_id, @ts, @quality, @source, @value);
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly Channel<TagValue> _queue;
    private readonly IDisposable _subscription;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _initializeTask;
    private readonly Task _writer;
    private readonly int _batchSize;
    private long _written;
    private long _pending;
    private long _dropped;
    private Exception? _lastWriteError;

    public TimescaleDbHistorian(
        IScadaEventBus eventBus,
        string connectionString,
        int capacity = 100_000,
        int batchSize = 500)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("TimescaleDB connection string is required.", nameof(connectionString));
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize));

        _batchSize = batchSize;
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _queue = Channel.CreateBounded<TagValue>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _subscription = eventBus.Subscribe<TagValueChanged>(OnTagValueChangedAsync);
        _initializeTask = InitializeAsync(_cts.Token);
        _writer = Task.Run(() => WriterLoopAsync(_cts.Token));
    }

    public long WrittenSamples => Interlocked.Read(ref _written);
    public long PendingSamples => Math.Max(0, Interlocked.Read(ref _pending));
    public long DroppedSamples => Interlocked.Read(ref _dropped);
    public Exception? LastWriteError => Volatile.Read(ref _lastWriteError);

    public IReadOnlyList<TagValue> Query(Guid tagId, DateTimeOffset from, DateTimeOffset to, int limit = 5000)
    {
        if (to < from) throw new ArgumentException("Historian query end must be greater than or equal to start.");
        var take = Math.Clamp(limit, 1, 50_000);
        _initializeTask.GetAwaiter().GetResult();

        const string sql = """
            SELECT value::text, ts, quality, source
            FROM elitescada.tag_history
            WHERE tag_id = @tag_id AND ts >= @from AND ts <= @to
            ORDER BY ts
            LIMIT @limit;
            """;

        using var connection = _dataSource.OpenConnection();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tag_id", tagId);
        command.Parameters.AddWithValue("from", from.UtcDateTime);
        command.Parameters.AddWithValue("to", to.UtcDateTime);
        command.Parameters.AddWithValue("limit", take);
        using var reader = command.ExecuteReader();

        var values = new List<TagValue>();
        while (reader.Read())
        {
            values.Add(new TagValue(
                tagId,
                DeserializeValue(reader.GetString(0)),
                ReadTimestamp(reader, 1),
                (TagQuality)reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
        return values;
    }

    private ValueTask OnTagValueChangedAsync(TagValueChanged evt)
    {
        if (_queue.Writer.TryWrite(evt.Current))
            Interlocked.Increment(ref _pending);
        else
            Interlocked.Increment(ref _dropped);
        return ValueTask.CompletedTask;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(InitializeSql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task WriterLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _initializeTask;
            var batch = new List<TagValue>(_batchSize);

            while (await _queue.Reader.WaitToReadAsync(cancellationToken))
            {
                batch.Clear();
                while (batch.Count < _batchSize && _queue.Reader.TryRead(out var sample))
                    batch.Add(sample);

                if (batch.Count == 0) continue;

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await WriteBatchAsync(batch, cancellationToken);
                        Volatile.Write(ref _lastWriteError, null);
                        Interlocked.Add(ref _written, batch.Count);
                        Interlocked.Add(ref _pending, -batch.Count);
                        break;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Volatile.Write(ref _lastWriteError, ex);
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task WriteBatchAsync(IReadOnlyCollection<TagValue> batch, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(InsertSql, connection, transaction);

        var tagId = command.Parameters.Add("tag_id", NpgsqlDbType.Uuid);
        var timestamp = command.Parameters.Add("ts", NpgsqlDbType.TimestampTz);
        var quality = command.Parameters.Add("quality", NpgsqlDbType.Integer);
        var source = command.Parameters.Add("source", NpgsqlDbType.Text);
        var value = command.Parameters.Add("value", NpgsqlDbType.Jsonb);

        foreach (var sample in batch)
        {
            tagId.Value = sample.TagId;
            timestamp.Value = sample.Timestamp.UtcDateTime;
            quality.Value = (int)sample.Quality;
            source.Value = (object?)sample.Source ?? DBNull.Value;
            value.Value = JsonSerializer.Serialize(sample.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static object? DeserializeValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return root.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => root.GetString(),
            JsonValueKind.Number when root.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => root.GetDouble(),
            _ => root.GetRawText()
        };
    }

    private static DateTimeOffset ReadTimestamp(NpgsqlDataReader reader, int ordinal)
    {
        var value = reader.GetFieldValue<DateTime>(ordinal);
        return new DateTimeOffset(value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime());
    }

    public async ValueTask DisposeAsync()
    {
        _subscription.Dispose();
        _queue.Writer.TryComplete();
        await _cts.CancelAsync();
        try { await _writer; } catch (OperationCanceledException) { }
        try { await _initializeTask; } catch (OperationCanceledException) { }
        await _dataSource.DisposeAsync();
        _cts.Dispose();
    }
}
