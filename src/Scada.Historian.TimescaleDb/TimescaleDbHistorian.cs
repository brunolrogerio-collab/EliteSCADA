using System.Text.Json;
using Scada.Engineering.Historian;
using Scada.Core.Persistence;
using System.Threading.Channels;
using Npgsql;
using NpgsqlTypes;
using Scada.Core.Abstractions;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Historian.Abstractions;
using Scada.Historian.Policies;

namespace Scada.Historian.TimescaleDb;

public sealed class TimescaleDbHistorian : IHistorian
{
    private const string InsertSql = """
        INSERT INTO elitescada.tag_history (
            tag_id, ts, quality, source, value, data_type,
            source_ts, source_ts_evidence, server_ts, server_ts_evidence,
            capture_profile_id, capture_policy_state, capture_policy_fingerprint)
        VALUES (
            @tag_id, @ts, @quality, @source, @value, @data_type,
            @source_ts, @source_ts_evidence, @server_ts, @server_ts_evidence,
            @capture_profile_id, @capture_policy_state, @capture_policy_fingerprint);
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IDurableWriteAdmission? _writeAdmission;
    private readonly Channel<HistorianWriteSample> _queue;
    private readonly IDisposable _subscription;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _initializeTask;
    private readonly Task _writer;
    private readonly int _batchSize;
    private readonly HistorianCaptureAdmission _admission = new();
    private long _written;
    private long _pending;
    private long _dropped;
    private long _accepted;
    private long _skipped;
    private long _coalesced;
    private Exception? _lastWriteError;

    public TimescaleDbHistorian(
        IScadaEventBus eventBus,
        string connectionString,
        int capacity = 100_000,
        int batchSize = 500,
        IDurableWriteAdmission? writeAdmission = null)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("TimescaleDB connection string is required.", nameof(connectionString));
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize));

        _batchSize = batchSize;
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _writeAdmission = writeAdmission;
        _queue = Channel.CreateBounded<HistorianWriteSample>(new BoundedChannelOptions(capacity)
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
    public long AcceptedSamples => Interlocked.Read(ref _accepted);
    public long SkippedSamples => Interlocked.Read(ref _skipped);
    public long CoalescedSamples => Interlocked.Read(ref _coalesced);
    public long DroppedSamples => Interlocked.Read(ref _dropped);
    public Exception? LastWriteError => Volatile.Read(ref _lastWriteError);

    public IReadOnlyList<TagValue> Query(Guid tagId, DateTimeOffset from, DateTimeOffset to, int limit = 5000)
    {
        if (to < from) throw new ArgumentException("Historian query end must be greater than or equal to start.");
        var take = Math.Clamp(limit, 1, 50_000);
        _initializeTask.GetAwaiter().GetResult();

        const string sql = """
            SELECT value::text, ts, quality, source,
                   source_ts, source_ts_evidence, server_ts, server_ts_evidence
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
                reader.IsDBNull(3) ? null : reader.GetString(3))
            {
                SourceTimestamp = reader.IsDBNull(4) ? null : ReadTimestamp(reader, 4),
                SourceTimestampEvidenceState = ReadEvidence(reader, 5, timestampPresent: !reader.IsDBNull(4)),
                ServerTimestamp = reader.IsDBNull(6) ? null : ReadTimestamp(reader, 6),
                ServerTimestampEvidenceState = ReadEvidence(
                    reader,
                    7,
                    timestampPresent: !reader.IsDBNull(6),
                    server: true)
            });
        }
        return values;
    }

    private ValueTask OnTagValueChangedAsync(TagValueChanged evt)
    {
        var decision = _admission.Evaluate(evt);
        switch (decision.Disposition)
        {
            case HistorianCaptureDisposition.Accepted:
                Interlocked.Increment(ref _accepted);
                var profileId = HistorianCaptureProfileMetadata.ReadProfileId(evt.Tag.Metadata);
                var policyFingerprint = !decision.Policy.IsExplicit
                    ? null
                    : decision.Policy.StableFingerprintBytes;
                if (_queue.Writer.TryWrite(new HistorianWriteSample(
                        evt.Current,
                        evt.Tag.DataType,
                        profileId,
                        decision.Policy.IsExplicit
                            ? HistorianCapturePolicyEvidenceState.Explicit
                            : HistorianCapturePolicyEvidenceState.LegacyDefault,
                        policyFingerprint)))
                    Interlocked.Increment(ref _pending);
                else
                    Interlocked.Increment(ref _dropped);
                break;
            case HistorianCaptureDisposition.Skipped:
                Interlocked.Increment(ref _skipped);
                break;
            case HistorianCaptureDisposition.Coalesced:
                Interlocked.Increment(ref _coalesced);
                break;
        }
        return ValueTask.CompletedTask;
    }

    private Task InitializeAsync(CancellationToken cancellationToken) =>
        TimescaleHistorianInfrastructure.EnsureRawAsync(_dataSource, cancellationToken);

    private async Task WriterLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _initializeTask;
            var batch = new List<HistorianWriteSample>(_batchSize);

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

    private async Task WriteBatchAsync(
        IReadOnlyCollection<HistorianWriteSample> batch,
        CancellationToken cancellationToken)
    {
        await using var durableWrite = await _writeAdmission.AcquireOptionalAsync("historian", cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(InsertSql, connection, transaction);

        var tagId = command.Parameters.Add("tag_id", NpgsqlDbType.Uuid);
        var timestamp = command.Parameters.Add("ts", NpgsqlDbType.TimestampTz);
        var quality = command.Parameters.Add("quality", NpgsqlDbType.Integer);
        var source = command.Parameters.Add("source", NpgsqlDbType.Text);
        var value = command.Parameters.Add("value", NpgsqlDbType.Jsonb);
        var dataType = command.Parameters.Add("data_type", NpgsqlDbType.Smallint);
        var sourceTimestamp = command.Parameters.Add("source_ts", NpgsqlDbType.TimestampTz);
        var sourceEvidence = command.Parameters.Add("source_ts_evidence", NpgsqlDbType.Smallint);
        var serverTimestamp = command.Parameters.Add("server_ts", NpgsqlDbType.TimestampTz);
        var serverEvidence = command.Parameters.Add("server_ts_evidence", NpgsqlDbType.Smallint);
        var captureProfileId = command.Parameters.Add("capture_profile_id", NpgsqlDbType.Uuid);
        var capturePolicyState = command.Parameters.Add("capture_policy_state", NpgsqlDbType.Smallint);
        var captureFingerprint = command.Parameters.Add("capture_policy_fingerprint", NpgsqlDbType.Bytea);

        foreach (var sample in batch)
        {
            sample.Value.ValidateTimestampEvidence();
            tagId.Value = sample.Value.TagId;
            timestamp.Value = sample.Value.Timestamp.UtcDateTime;
            quality.Value = (int)sample.Value.Quality;
            source.Value = (object?)sample.Value.Source ?? DBNull.Value;
            value.Value = JsonSerializer.Serialize(sample.Value.Value);
            dataType.Value = (short)sample.DataType;
            sourceTimestamp.Value = sample.Value.SourceTimestamp?.UtcDateTime ?? (object)DBNull.Value;
            sourceEvidence.Value = (short)sample.Value.SourceTimestampEvidenceState;
            serverTimestamp.Value = sample.Value.ServerTimestamp?.UtcDateTime ?? (object)DBNull.Value;
            serverEvidence.Value = (short)sample.Value.ServerTimestampEvidenceState;
            captureProfileId.Value = (object?)sample.CaptureProfileId ?? DBNull.Value;
            capturePolicyState.Value = (short)sample.CapturePolicyState;
            captureFingerprint.Value = (object?)sample.CapturePolicyFingerprint ?? DBNull.Value;
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
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when root.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => root.GetDouble(),
            JsonValueKind.String => root.GetString(),
            JsonValueKind.Null => null,
            _ => root.GetRawText()
        };
    }

    private static DateTimeOffset ReadTimestamp(NpgsqlDataReader reader, int ordinal)
    {
        try
        {
            return reader.GetFieldValue<DateTimeOffset>(ordinal);
        }
        catch (InvalidCastException)
        {
            var value = reader.GetDateTime(ordinal);
            if (value.Kind != DateTimeKind.Utc) value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return new DateTimeOffset(value);
        }
    }

    private static TimestampEvidenceState ReadEvidence(
        NpgsqlDataReader reader,
        int ordinal,
        bool timestampPresent,
        bool server = false)
    {
        if (reader.IsDBNull(ordinal))
            return timestampPresent
                ? TimestampEvidenceState.PresentUnverified
                : TimestampEvidenceState.NotRecorded;
        var raw = reader.GetInt16(ordinal);
        if (!Enum.IsDefined(typeof(TimestampEvidenceState), raw))
            return timestampPresent
                ? TimestampEvidenceState.PresentUnverified
                : TimestampEvidenceState.NotRecorded;
        var state = (TimestampEvidenceState)raw;
        if (state == TimestampEvidenceState.NotRecorded)
            return state;
        if (state == TimestampEvidenceState.NotProvided)
            return timestampPresent ? TimestampEvidenceState.PresentUnverified : state;
        if (!timestampPresent)
            return TimestampEvidenceState.NotRecorded;
        return server && state is TimestampEvidenceState.Synchronized or TimestampEvidenceState.Unsynchronized
            ? TimestampEvidenceState.PresentUnverified
            : state;
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

    private sealed record HistorianWriteSample(
        TagValue Value,
        TagDataType DataType,
        Guid? CaptureProfileId,
        HistorianCapturePolicyEvidenceState CapturePolicyState,
        byte[]? CapturePolicyFingerprint);
}
