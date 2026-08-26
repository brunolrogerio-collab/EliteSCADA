using Scada.Core.Sources;
using Scada.Core.Tags;

namespace Scada.Core.InternalMemory;

/// <summary>
/// Client Memory state for exactly one Runtime Client instance. Each provider
/// instance owns a separate value dictionary and has no server retention store.
/// </summary>
public sealed class ClientMemorySourceProvider : ISourceProvider
{
    private readonly TimeProvider _timeProvider;
    private readonly object _stateGate = new();
    private readonly Dictionary<Guid, MemoryTagDefinition> _definitions;
    private readonly Dictionary<Guid, TagValue> _values;

    internal ClientMemorySourceProvider(
        string instanceKey,
        string runtimeClientId,
        IEnumerable<MemoryTagDefinition> definitions,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(instanceKey))
            throw new ArgumentException("Source provider instance key is required.", nameof(instanceKey));
        if (string.IsNullOrWhiteSpace(runtimeClientId))
            throw new ArgumentException("Runtime Client ID is required for Client Memory.", nameof(runtimeClientId));

        ArgumentNullException.ThrowIfNull(timeProvider);

        InstanceKey = instanceKey;
        RuntimeClientId = runtimeClientId;
        _timeProvider = timeProvider;
        _definitions = MemoryTagDefinitionSet.Materialize(definitions);
        _values = new Dictionary<Guid, TagValue>(_definitions.Count);

        var initializedAt = _timeProvider.GetUtcNow();
        foreach (var definition in _definitions.Values)
        {
            _values.Add(
                definition.Tag.Id,
                new TagValue(
                    definition.Tag.Id,
                    definition.InitialValue.Value,
                    initializedAt,
                    TagQuality.Good,
                    InstanceKey));
        }
    }

    public SourceProviderDescriptor Descriptor => BuiltInSourceProviderDescriptors.ClientMemory;
    public string InstanceKey { get; }
    public string RuntimeClientId { get; }

    public IReadOnlyCollection<TagDefinition> Tags =>
        _definitions.Values
            .Select(x => x.Tag)
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateGate)
        {
            _values.TryGetValue(tagId, out var value);
            return ValueTask.FromResult<TagValue?>(value);
        }
    }

    public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_definitions.TryGetValue(tagId, out var definition))
            throw new KeyNotFoundException($"Client Memory TAG '{tagId}' is not defined in source '{InstanceKey}'.");
        if (definition.Tag.ReadOnly)
            throw new InvalidOperationException($"Client Memory TAG '{definition.Tag.Path}' is read-only.");

        var typedValue = new TypedTagValue(definition.Tag.DataType, value);
        var current = new TagValue(
            tagId,
            typedValue.Value,
            _timeProvider.GetUtcNow(),
            TagQuality.Good,
            InstanceKey);

        lock (_stateGate)
        {
            _values[tagId] = current;
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Forces Client Memory creation to name its owning Runtime Client. The factory
/// does not cache providers, preventing accidental promotion into one global
/// server scalar store.
/// </summary>
public sealed class ClientMemorySourceProviderFactory(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public ClientMemorySourceProvider Create(
        string instanceKey,
        string runtimeClientId,
        IEnumerable<MemoryTagDefinition> definitions) =>
        new(instanceKey, runtimeClientId, definitions, _timeProvider);
}
