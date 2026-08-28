namespace Scada.Engineering.Scripts;

public interface IScriptEngineeringRegistry
{
    IReadOnlyCollection<ScriptEngineeringDefinition> SnapshotScripts();
    IReadOnlyCollection<ScriptVisualEventReference> SnapshotVisualEventReferences();
    ScriptEngineeringDefinition? Find(Guid id);
    ScriptEngineeringDefinition? FindByPath(string path);
    void Upsert(ScriptEngineeringDefinition script);
    void ReplaceVisualEventReferences(Guid scriptId, IReadOnlyCollection<ScriptVisualEventReference> references);
    void Clear();
}

public sealed class InMemoryScriptEngineeringRegistry : IScriptEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, ScriptEngineeringDefinition> _byId = new();
    private readonly Dictionary<string, Guid> _byPath = new(StringComparer.Ordinal);
    private readonly List<ScriptVisualEventReference> _visualEventReferences = new();
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

    public void Clear()
    {
        lock (_sync)
        {
            _byId.Clear();
            _byPath.Clear();
            _visualEventReferences.Clear();
        }

        _changed?.Invoke();
    }
}
