using System.Data.Common;
using System.IO;
using System.Net.Sockets;
using Scada.Core.Sources;
using Scada.Core.Tags;

namespace Scada.Core.InternalMemory;

public sealed class MemoryRetentionTypeMismatchException : InvalidOperationException
{
    public MemoryRetentionTypeMismatchException(Guid tagId, TagDataType retainedType, TagDataType activeType)
        : base($"Retained value for TAG '{tagId}' has data type '{retainedType}', but the active definition requires '{activeType}'. Explicit reset or migration is required.")
    {
        TagId = tagId;
        RetainedType = retainedType;
        ActiveType = activeType;
    }

    public Guid TagId { get; }
    public TagDataType RetainedType { get; }
    public TagDataType ActiveType { get; }
}

/// <summary>
/// Server-owned, shared, retentive Internal Memory provider. The provider only
/// restores TAGs present in the supplied active definition set; it never
/// enumerates retained state to construct runtime TAGs.
/// </summary>
public sealed class ServerMemorySourceProvider : IQualifiedSourceProvider
{
    private readonly IServerMemoryRetentionStore _retentionStore;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private readonly object _stateGate = new();
    private Dictionary<Guid, MemoryTagDefinition> _definitions = new();
    private Dictionary<Guid, TagValue> _values = new();
    private readonly Dictionary<Guid, RetainedMemoryValue> _pendingRetentionValues = new();
    private bool _retentionAvailable = true;
    private DateTimeOffset _retryRetentionAtUtc;
    private static readonly TimeSpan RetentionRetryDelay = TimeSpan.FromSeconds(2);

    public ServerMemorySourceProvider(
        string instanceKey,
        IServerMemoryRetentionStore retentionStore,
        TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(instanceKey))
            throw new ArgumentException("Source provider instance key is required.", nameof(instanceKey));

        ArgumentNullException.ThrowIfNull(retentionStore);
        InstanceKey = instanceKey;
        _retentionStore = retentionStore;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public SourceProviderDescriptor Descriptor => BuiltInSourceProviderDescriptors.ServerMemory;
    public string InstanceKey { get; }

    public IReadOnlyCollection<TagDefinition> Tags
    {
        get
        {
            lock (_stateGate)
            {
                return _definitions.Values
                    .Select(x => x.Tag)
                    .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }
    }

    /// <summary>
    /// Stages an active revision/restart view before replacing current provider
    /// state. Any retained type incompatibility fails closed and leaves the
    /// previously active provider state untouched.
    /// </summary>
    public async ValueTask ActivateAsync(
        IEnumerable<MemoryTagDefinition> definitions,
        CancellationToken cancellationToken = default)
    {
        var stagedDefinitions = MemoryTagDefinitionSet.Materialize(definitions);

        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            var stagedValues = new Dictionary<Guid, TagValue>(stagedDefinitions.Count);
            var retentionAvailable = true;

            foreach (var definition in stagedDefinitions.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RetainedMemoryValue? retained = null;
                if (retentionAvailable)
                {
                    try
                    {
                        retained = await _retentionStore.ReadAsync(definition.Tag.Id, cancellationToken);
                    }
                    catch (Exception exception) when (IsTransientRetentionFailure(exception))
                    {
                        // A database outage must not prevent the runtime from coming
                        // up. HA overlays the peer's authoritative snapshot after
                        // activation; until then, use engineered initial values.
                        retentionAvailable = false;
                        _retryRetentionAtUtc = _timeProvider.GetUtcNow() + RetentionRetryDelay;
                    }
                }

                if (retained is not null && retained.TypedValue.DataType != definition.Tag.DataType)
                {
                    throw new MemoryRetentionTypeMismatchException(
                        definition.Tag.Id,
                        retained.TypedValue.DataType,
                        definition.Tag.DataType);
                }

                var current = retained is null
                    ? CreateValue(definition.Tag.Id, definition.InitialValue.Value, _timeProvider.GetUtcNow(), TagQuality.Good)
                    : CreateValue(definition.Tag.Id, retained.TypedValue.Value, retained.StoredAt, TagQuality.Good);

                stagedValues.Add(definition.Tag.Id, current);
            }

            var activeTagIds = stagedDefinitions.Keys.ToHashSet();
            foreach (var pendingTagId in _pendingRetentionValues.Keys.Where(id => !activeTagIds.Contains(id)).ToArray())
                _pendingRetentionValues.Remove(pendingTagId);

            foreach (var pending in _pendingRetentionValues.Values)
            {
                if (!stagedDefinitions.TryGetValue(pending.TagId, out var definition)) continue;
                if (pending.TypedValue.DataType != definition.Tag.DataType)
                    throw new MemoryRetentionTypeMismatchException(
                        pending.TagId,
                        pending.TypedValue.DataType,
                        definition.Tag.DataType);

                stagedValues[pending.TagId] = CreateValue(
                    pending.TagId,
                    pending.TypedValue.Value,
                    pending.StoredAt,
                    TagQuality.Good);
            }

            lock (_stateGate)
            {
                _definitions = stagedDefinitions;
                _values = stagedValues;
                _retentionAvailable = retentionAvailable;
            }

            if (retentionAvailable && _pendingRetentionValues.Count > 0)
                await FlushPendingRetentionAsync(cancellationToken);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateGate)
        {
            _values.TryGetValue(tagId, out var value);
            return ValueTask.FromResult<TagValue?>(value);
        }
    }

    /// <summary>
    /// Ordinary Internal Memory writes intentionally remain Good. Explicit
    /// quality is available only through IQualifiedSourceProvider.
    /// </summary>
    public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) =>
        WriteCoreAsync(tagId, value, TagQuality.Good, null, null, cancellationToken);

    public ValueTask PublishSampleAsync(
        Guid tagId,
        QualifiedSourceSample sample,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (!Enum.IsDefined(sample.Quality))
            throw new ArgumentOutOfRangeException(nameof(sample), $"Unknown TAG quality '{sample.Quality}'.");

        return WriteCoreAsync(
            tagId,
            sample.Value,
            sample.Quality,
            sample.SourceTimestamp,
            sample.ServerTimestamp,
            cancellationToken);
    }

    private async ValueTask WriteCoreAsync(
        Guid tagId,
        object? value,
        TagQuality quality,
        DateTimeOffset? sourceTimestamp,
        DateTimeOffset? serverTimestamp,
        CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            MemoryTagDefinition definition;
            lock (_stateGate)
            {
                if (!_definitions.TryGetValue(tagId, out definition!))
                    throw new KeyNotFoundException($"Server Memory TAG '{tagId}' is not active in source '{InstanceKey}'.");
            }

            if (definition.Tag.ReadOnly)
                throw new InvalidOperationException($"Server Memory TAG '{definition.Tag.Path}' is read-only.");

            var typedValue = new TypedTagValue(definition.Tag.DataType, value);
            var timestamp = _timeProvider.GetUtcNow();

            // Retention stores the typed process value, not transient runtime
            // communication quality. During a transient database outage, keep
            // values in this process and periodically retry persistence. This is
            // explicitly degraded/non-durable behavior.
            var retainedValue = new RetainedMemoryValue(tagId, typedValue, timestamp);
            if (_retentionAvailable || _timeProvider.GetUtcNow() >= _retryRetentionAtUtc)
            {
                try
                {
                    await _retentionStore.WriteAsync(retainedValue, cancellationToken);
                    _retentionAvailable = true;
                    _pendingRetentionValues.Remove(tagId);
                    await FlushPendingRetentionAsync(cancellationToken);
                }
                catch (Exception exception) when (IsTransientRetentionFailure(exception))
                {
                    _retentionAvailable = false;
                    _retryRetentionAtUtc = _timeProvider.GetUtcNow() + RetentionRetryDelay;
                    _pendingRetentionValues[tagId] = retainedValue;
                }
            }
            else
            {
                _pendingRetentionValues[tagId] = retainedValue;
            }

            var current = CreateValue(
                tagId,
                typedValue.Value,
                timestamp,
                quality,
                sourceTimestamp,
                serverTimestamp);
            lock (_stateGate)
            {
                _values[tagId] = current;
            }
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    /// <summary>
    /// Explicit destructive resolution for an incompatible retained value. The
    /// durable row is removed first and the active value is reset to the current
    /// engineered initial/default value. A later activation may then change the
    /// TAG data type without any implicit conversion of the old retained value.
    /// This operation is deliberately limited to an active engineered TAG ID.
    /// </summary>
    public async ValueTask ResetRetainedValueAsync(
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            MemoryTagDefinition definition;
            lock (_stateGate)
            {
                if (!_definitions.TryGetValue(tagId, out definition!))
                    throw new KeyNotFoundException($"Server Memory TAG '{tagId}' is not active in source '{InstanceKey}'.");
            }

            await _retentionStore.DeleteAsync(tagId, cancellationToken);
            var current = CreateValue(
                tagId,
                definition.InitialValue.Value,
                _timeProvider.GetUtcNow(),
                TagQuality.Good);
            lock (_stateGate)
            {
                _values[tagId] = current;
            }
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private TagValue CreateValue(
        Guid tagId,
        object? value,
        DateTimeOffset timestamp,
        TagQuality quality,
        DateTimeOffset? sourceTimestamp = null,
        DateTimeOffset? serverTimestamp = null) =>
        new TagValue(tagId, value, timestamp, quality, InstanceKey)
        {
            SourceTimestamp = sourceTimestamp,
            ServerTimestamp = serverTimestamp
        };

    private static bool IsTransientRetentionFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException { IsTransient: true } or TimeoutException or IOException or SocketException)
                return true;

            // PostgreSQL reports an administrative shutdown as SQLSTATE 57P01;
            // some provider versions do not classify that exception as transient.
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
            if (sqlState is "57P01" || sqlState?.StartsWith("08", StringComparison.Ordinal) == true)
                return true;
        }

        return false;
    }

    private async ValueTask FlushPendingRetentionAsync(CancellationToken cancellationToken)
    {
        foreach (var pending in _pendingRetentionValues.Values.ToArray())
        {
            try
            {
                await _retentionStore.WriteAsync(pending, cancellationToken);
                _pendingRetentionValues.Remove(pending.TagId);
            }
            catch (Exception exception) when (IsTransientRetentionFailure(exception))
            {
                _retentionAvailable = false;
                _retryRetentionAtUtc = _timeProvider.GetUtcNow() + RetentionRetryDelay;
                return;
            }
        }
    }
}
