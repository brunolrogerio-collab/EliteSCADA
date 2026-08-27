using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;

namespace Scada.DriverHost.Runtime;

internal sealed class ServerMemoryRuntimeSource
{
    private readonly ServerMemorySourceProvider _provider;
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
                ?? throw new InvalidOperationException(
                    $"Server Memory source '{InstanceKey}' did not initialize TAG '{definition.Tag.Path}'.");
            await _cache.UpdateAsync(definition.Tag, current, cancellationToken);
        }
    }

    public async ValueTask WriteAsync(
        Guid tagId,
        object? value,
        CancellationToken cancellationToken = default)
    {
        var definition = _definitions.FirstOrDefault(x => x.Tag.Id == tagId)
            ?? throw new KeyNotFoundException($"Server Memory TAG '{tagId}' is not active in source '{InstanceKey}'.");

        await _provider.WriteAsync(tagId, value, cancellationToken);
        var current = await _provider.ReadAsync(tagId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Server Memory source '{InstanceKey}' lost TAG '{definition.Tag.Path}' after a successful write.");
        await _cache.UpdateAsync(definition.Tag, current, cancellationToken);
    }

    public async ValueTask ResetRetainedValueAsync(
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        var definition = _definitions.FirstOrDefault(x => x.Tag.Id == tagId)
            ?? throw new KeyNotFoundException($"Server Memory TAG '{tagId}' is not active in source '{InstanceKey}'.");

        await _provider.ResetRetainedValueAsync(tagId, cancellationToken);
        var current = await _provider.ReadAsync(tagId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Server Memory source '{InstanceKey}' lost TAG '{definition.Tag.Path}' after reset.");
        await _cache.UpdateAsync(definition.Tag, current, cancellationToken);
    }
}
