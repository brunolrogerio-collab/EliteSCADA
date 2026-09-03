using Scada.Engineering.Contracts;
using Scada.Engineering.Events;

namespace Scada.Engineering.Scripts;

public interface IScriptEngineeringRegistry
{
    IReadOnlyCollection<ScriptEngineeringDefinition> SnapshotScripts();
    IReadOnlyCollection<ScriptVisualEventReference> SnapshotVisualEventReferences();
    ScriptEngineeringDefinition? Find(Guid id);
    ScriptEngineeringDefinition? FindByPath(string path);
    void Upsert(ScriptEngineeringDefinition script);
    void ReplaceVisualEventReferences(Guid scriptId, IReadOnlyCollection<ScriptVisualEventReference> references);
    bool Remove(Guid id);
    void Clear();
}

/// <summary>
/// The workspace already owns one lifecycle-aware Script registry with a single
/// dirty callback and Clear boundary. Operational Event definitions use a separate
/// collection and public interface on the same workspace-owned object so checkout,
/// save and project switching cannot leave stale Event definitions behind.
/// </summary>
public sealed class InMemoryScriptEngineeringRegistry : IScriptEngineeringRegistry, IOperationalEventEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, ScriptEngineeringDefinition> _byId = new();
    private readonly Dictionary<string, Guid> _byPath = new(StringComparer.Ordinal);
    private readonly List<ScriptVisualEventReference> _visualEventReferences = new();
    private readonly Dictionary<Guid, OperationalEventEngineeringDto> _eventById = new();
    private readonly Dictionary<string, Guid> _eventByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;

    public InMemoryScriptEngineeringRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public IReadOnlyCollection<ScriptEngineeringDefinition> SnapshotScripts()
    {
        lock (_sync)
            return _byId.Values
                .OrderBy(script => script.Path, StringComparer.Ordinal)
                .ThenBy(script => script.Id)
                .ToArray();
    }

    public IReadOnlyCollection<ScriptVisualEventReference> SnapshotVisualEventReferences()
    {
        lock (_sync)
            return _visualEventReferences
                .OrderBy(reference => reference.VisualDefinitionId)
                .ThenBy(reference => reference.VisualObjectId)
                .ThenBy(reference => (int)reference.EventKind)
                .ThenBy(reference => reference.ScriptId)
                .ThenBy(reference => reference.EntryPoint, StringComparer.Ordinal)
                .ThenBy(reference => reference.TargetReference ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
    }

    public ScriptEngineeringDefinition? Find(Guid id)
    {
        lock (_sync)
            return _byId.GetValueOrDefault(id);
    }

    public ScriptEngineeringDefinition? FindByPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        lock (_sync)
            return _byPath.TryGetValue(path, out var id) ? _byId.GetValueOrDefault(id) : null;
    }

    public void Upsert(ScriptEngineeringDefinition script)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (script.Id == Guid.Empty)
            throw new ArgumentException("Script stable ID is required.", nameof(script));
        if (string.IsNullOrWhiteSpace(script.Path))
            throw new ArgumentException("Script path is required.", nameof(script));

        lock (_sync)
        {
            if (_byPath.TryGetValue(script.Path, out var pathOwner) && pathOwner != script.Id)
                throw new InvalidOperationException($"Script path '{script.Path}' is already owned by '{pathOwner:D}'.");

            if (_byId.TryGetValue(script.Id, out var previous) &&
                !string.Equals(previous.Path, script.Path, StringComparison.Ordinal))
            {
                _byPath.Remove(previous.Path);
            }

            _byId[script.Id] = script;
            _byPath[script.Path] = script.Id;
        }

        _changed?.Invoke();
    }

    public void ReplaceVisualEventReferences(
        Guid scriptId,
        IReadOnlyCollection<ScriptVisualEventReference> references)
    {
        if (scriptId == Guid.Empty)
            throw new ArgumentException("Script stable ID is required.", nameof(scriptId));
        ArgumentNullException.ThrowIfNull(references);
        if (references.Any(reference => reference.ScriptId != scriptId))
            throw new ArgumentException("All visual event references must belong to the supplied Script ID.", nameof(references));

        lock (_sync)
        {
            _visualEventReferences.RemoveAll(reference => reference.ScriptId == scriptId);
            _visualEventReferences.AddRange(references);
        }

        _changed?.Invoke();
    }

    public bool Remove(Guid id)
    {
        ScriptEngineeringDefinition? removed;
        lock (_sync)
        {
            if (!_byId.Remove(id, out removed))
                return false;

            _byPath.Remove(removed.Path);
            _visualEventReferences.RemoveAll(reference => reference.ScriptId == id);
        }

        _changed?.Invoke();
        return true;
    }

    public IReadOnlyCollection<OperationalEventEngineeringDto> SnapshotOperationalEvents()
    {
        lock (_sync)
            return _eventById.Values
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id)
                .ToArray();
    }

    public OperationalEventEngineeringDto? FindOperationalEvent(Guid id)
    {
        lock (_sync)
            return _eventById.GetValueOrDefault(id);
    }

    public OperationalEventEngineeringDto? FindOperationalEventByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        lock (_sync)
            return _eventByKey.TryGetValue(key, out var id) ? _eventById.GetValueOrDefault(id) : null;
    }

    public void UpsertOperationalEvent(OperationalEventEngineeringDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Key))
            throw new ArgumentException("Operational Event key is required.", nameof(definition));

        var normalized = definition with { Id = definition.Id ?? Guid.NewGuid() };
        var id = normalized.Id!.Value;
        if (id == Guid.Empty)
            throw new ArgumentException("Operational Event stable ID cannot be empty.", nameof(definition));

        lock (_sync)
        {
            if (_eventById.TryGetValue(id, out var previous) &&
                !previous.Key.Equals(normalized.Key, StringComparison.OrdinalIgnoreCase))
                _eventByKey.Remove(previous.Key);

            if (_eventByKey.TryGetValue(normalized.Key, out var otherId) && otherId != id)
                _eventById.Remove(otherId);

            _eventById[id] = normalized;
            _eventByKey[normalized.Key] = id;
        }

        _changed?.Invoke();
    }

    public void ClearOperationalEvents()
    {
        lock (_sync)
        {
            _eventById.Clear();
            _eventByKey.Clear();
        }
        _changed?.Invoke();
    }

    public void Clear()
    {
        lock (_sync)
        {
            _byId.Clear();
            _byPath.Clear();
            _visualEventReferences.Clear();
            _eventById.Clear();
            _eventByKey.Clear();
        }

        _changed?.Invoke();
    }
}