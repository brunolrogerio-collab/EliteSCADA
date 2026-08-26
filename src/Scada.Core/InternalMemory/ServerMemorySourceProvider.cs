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
public sealed class ServerMemorySourceProvider : ISourceProvider
{
    private readonly IServerMemoryRetentionStore _retentionStore;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private readonly object _stateGate = new();
    private Dictionary<Guid, MemoryTagDefinition> _definitions = new();
    private Dictionary<Guid, TagValue> _values = new();

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

            foreach (var definition in stagedDefinitions.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var retained = await _retentionStore.ReadAsync(definition.Tag.Id, cancellationToken);

                if (retained is not null && retained.TypedValue.DataType != definition.Tag.DataType)
                {
                    throw new MemoryRetentionTypeMismatchException(
                        definition.Tag.Id,
                        retained.TypedValue.DataType,
                        definition.Tag.DataType);
                }

                var current = retained is null
                    ? CreateGoodValue(definition.Tag.Id, definition.InitialValue.Value, _timeProvider.GetUtcNow())
                    : CreateGoodValue(definition.Tag.Id, retained.TypedValue.Value, retained.StoredAt);

                stagedValues.Add(definition.Tag.Id, current);
            }

            lock (_stateGate)
            {
                _definitions = stagedDefinitions;
                _values = stagedValues;
            }
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

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
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

            // Persist before publishing the new current value. A retention failure
            // therefore leaves the provider's visible state unchanged.
            await _retentionStore.WriteAsync(
                new RetainedMemoryValue(tagId, typedValue, timestamp),
                cancellationToken);

            var current = CreateGoodValue(tagId, typedValue.Value, timestamp);
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
            var current = CreateGoodValue(tagId, definition.InitialValue.Value, _timeProvider.GetUtcNow());
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

    private TagValue CreateGoodValue(Guid tagId, object value, DateTimeOffset timestamp) =>
        new(tagId, value, timestamp, TagQuality.Good, InstanceKey);
}
