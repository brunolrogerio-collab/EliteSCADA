using Scada.Engineering.Contracts;

namespace Scada.Engineering.Assets;

public interface IEngineeringAssetRegistry
{
    IReadOnlyCollection<EquipmentTemplateEngineeringDto> SnapshotTemplates();
    IReadOnlyCollection<EquipmentEngineeringDto> SnapshotEquipment();
    IReadOnlyCollection<DynamoEngineeringDto> SnapshotDynamos();

    EquipmentTemplateEngineeringDto? FindTemplate(Guid id);
    EquipmentTemplateEngineeringDto? FindTemplateByKey(string key);
    EquipmentEngineeringDto? FindEquipment(Guid id);
    EquipmentEngineeringDto? FindEquipmentByPath(string path);
    DynamoEngineeringDto? FindDynamo(Guid id);
    DynamoEngineeringDto? FindDynamoByKey(string key);

    void UpsertTemplate(EquipmentTemplateEngineeringDto template);
    void UpsertEquipment(EquipmentEngineeringDto equipment);
    void UpsertDynamo(DynamoEngineeringDto dynamo);
}

public sealed class InMemoryEngineeringAssetRegistry : IEngineeringAssetRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, EquipmentTemplateEngineeringDto> _templatesById = new();
    private readonly Dictionary<string, Guid> _templatesByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, EquipmentEngineeringDto> _equipmentById = new();
    private readonly Dictionary<string, Guid> _equipmentByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, DynamoEngineeringDto> _dynamosById = new();
    private readonly Dictionary<string, Guid> _dynamosByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;

    public InMemoryEngineeringAssetRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public IReadOnlyCollection<EquipmentTemplateEngineeringDto> SnapshotTemplates()
    {
        lock (_sync)
            return _templatesById.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyCollection<EquipmentEngineeringDto> SnapshotEquipment()
    {
        lock (_sync)
            return _equipmentById.Values.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyCollection<DynamoEngineeringDto> SnapshotDynamos()
    {
        lock (_sync)
            return _dynamosById.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public EquipmentTemplateEngineeringDto? FindTemplate(Guid id)
    {
        lock (_sync) return _templatesById.GetValueOrDefault(id);
    }

    public EquipmentTemplateEngineeringDto? FindTemplateByKey(string key)
    {
        lock (_sync) return _templatesByKey.TryGetValue(key, out var id) ? _templatesById.GetValueOrDefault(id) : null;
    }

    public EquipmentEngineeringDto? FindEquipment(Guid id)
    {
        lock (_sync) return _equipmentById.GetValueOrDefault(id);
    }

    public EquipmentEngineeringDto? FindEquipmentByPath(string path)
    {
        lock (_sync) return _equipmentByPath.TryGetValue(path, out var id) ? _equipmentById.GetValueOrDefault(id) : null;
    }

    public DynamoEngineeringDto? FindDynamo(Guid id)
    {
        lock (_sync) return _dynamosById.GetValueOrDefault(id);
    }

    public DynamoEngineeringDto? FindDynamoByKey(string key)
    {
        lock (_sync) return _dynamosByKey.TryGetValue(key, out var id) ? _dynamosById.GetValueOrDefault(id) : null;
    }

    public void UpsertTemplate(EquipmentTemplateEngineeringDto template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template.Key);
        var normalized = template with { Id = template.Id ?? Guid.NewGuid() };
        lock (_sync) UpsertByKey(normalized, normalized.Id!.Value, normalized.Key, _templatesById, _templatesByKey, x => x.Key);
        _changed?.Invoke();
    }

    public void UpsertEquipment(EquipmentEngineeringDto equipment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(equipment.Path);
        var normalized = equipment with { Id = equipment.Id ?? Guid.NewGuid() };
        lock (_sync) UpsertByKey(normalized, normalized.Id!.Value, normalized.Path, _equipmentById, _equipmentByPath, x => x.Path);
        _changed?.Invoke();
    }

    public void UpsertDynamo(DynamoEngineeringDto dynamo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dynamo.Key);
        var normalized = dynamo with { Id = dynamo.Id ?? Guid.NewGuid() };
        lock (_sync) UpsertByKey(normalized, normalized.Id!.Value, normalized.Key, _dynamosById, _dynamosByKey, x => x.Key);
        _changed?.Invoke();
    }

    public void Clear()
    {
        lock (_sync)
        {
            _templatesById.Clear();
            _templatesByKey.Clear();
            _equipmentById.Clear();
            _equipmentByPath.Clear();
            _dynamosById.Clear();
            _dynamosByKey.Clear();
        }
        _changed?.Invoke();
    }

    private static void UpsertByKey<T>(
        T value,
        Guid id,
        string key,
        Dictionary<Guid, T> byId,
        Dictionary<string, Guid> byKey,
        Func<T, string> keySelector)
    {
        if (byId.TryGetValue(id, out var previous) && !keySelector(previous).Equals(key, StringComparison.OrdinalIgnoreCase))
            byKey.Remove(keySelector(previous));

        if (byKey.TryGetValue(key, out var otherId) && otherId != id)
            byId.Remove(otherId);

        byId[id] = value;
        byKey[key] = id;
    }
}
