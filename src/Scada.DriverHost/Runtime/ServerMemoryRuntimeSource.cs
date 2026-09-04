using System.Text.Json;
using Scada.Core.InternalMemory;
using Scada.Core.Sources;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;

namespace Scada.DriverHost.Runtime;

internal sealed class ServerMemoryRuntimeSource
{
    private readonly ServerMemorySourceProvider _provider;
    private readonly ServerAuthoritativeSamplePublisher _qualifiedPublisher;
    private readonly CurrentTagCache _cache;
    private readonly InMemoryTagRegistry _registry;
    private readonly IReadOnlyCollection<MemoryTagDefinition> _definitions;

    public ServerMemoryRuntimeSource(
        InternalMemoryRuntimePlan plan,
        IServerMemoryRetentionStore retentionStore,
        CurrentTagCache cache,
        InMemoryTagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(retentionStore);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        if (plan.IsClientMemory)
            throw new ArgumentException("Client Memory cannot be composed as a shared server runtime source.", nameof(plan));

        _definitions = plan.Tags;
        _provider = new ServerMemorySourceProvider(plan.DataSourceKey, retentionStore);
        _qualifiedPublisher = new ServerAuthoritativeSamplePublisher(_provider, cache);
        _cache = cache;
        _registry = registry;
    }

    public string InstanceKey => _provider.InstanceKey;
    public IReadOnlyCollection<TagDefinition> Tags => _definitions.Select(x => x.Tag).ToArray();

    public async ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        await _provider.ActivateAsync(_definitions, cancellationToken);
        foreach (var definition in _definitions)
        {
            _registry.Register(definition.Tag);
            var current = await _provider.ReadAsync(definition.Tag.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Server Memory source '{InstanceKey}' did not initialize TAG '{definition.Tag.Path}'.");
            await _cache.UpdateAsync(definition.Tag, current, cancellationToken);
        }
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        var definition = _definitions.FirstOrDefault(x => x.Tag.Id == tagId)
            ?? throw new KeyNotFoundException($"Server Memory TAG '{tagId}' is not active in source '{InstanceKey}'.");

        if (value is QualifiedSourceSample qualifiedSample)
        {
            var normalizedSample = qualifiedSample with
            {
                Value = NormalizeJsonValue(definition.Tag.DataType, qualifiedSample.Value)
            };
            await _qualifiedPublisher.PublishAsync(definition.Tag, normalizedSample, cancellationToken);
            return;
        }

        await _provider.WriteAsync(tagId, NormalizeJsonValue(definition.Tag.DataType, value), cancellationToken);
        var current = await _provider.ReadAsync(tagId, cancellationToken)
            ?? throw new InvalidOperationException($"Server Memory source '{InstanceKey}' lost TAG '{definition.Tag.Path}' after a successful write.");
        await _cache.UpdateAsync(definition.Tag, current, cancellationToken);
    }

    public async ValueTask ResetRetainedValueAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        var definition = _definitions.FirstOrDefault(x => x.Tag.Id == tagId)
            ?? throw new KeyNotFoundException($"Server Memory TAG '{tagId}' is not active in source '{InstanceKey}'.");

        await _provider.ResetRetainedValueAsync(tagId, cancellationToken);
        var current = await _provider.ReadAsync(tagId, cancellationToken)
            ?? throw new InvalidOperationException($"Server Memory source '{InstanceKey}' lost TAG '{definition.Tag.Path}' after reset.");
        await _cache.UpdateAsync(definition.Tag, current, cancellationToken);
    }

    private static object? NormalizeJsonValue(TagDataType dataType, object? value)
    {
        if (value is not JsonElement json) return value;
        try
        {
            return dataType switch
            {
                TagDataType.Boolean => json.GetBoolean(),
                TagDataType.Int16 => json.GetInt16(),
                TagDataType.Int32 => json.GetInt32(),
                TagDataType.Int64 => json.GetInt64(),
                TagDataType.Float => json.GetSingle(),
                TagDataType.Double => json.GetDouble(),
                TagDataType.String => json.GetString(),
                TagDataType.DateTime => json.GetDateTimeOffset(),
                TagDataType.Enum => json.GetInt32(),
                _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unsupported TAG data type.")
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or OverflowException)
        {
            throw new ArgumentException($"JSON value is incompatible with Server Memory TAG data type '{dataType}'.", nameof(value), ex);
        }
    }
}
