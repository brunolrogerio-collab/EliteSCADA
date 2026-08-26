using System.Collections.ObjectModel;

namespace Scada.Engineering.VisualScripting;

public enum VisualRuntimeDefinitionKind
{
    Screen,
    Popup,
    Dynamo
}

public sealed class VisualScriptHandlerReference
{
    public VisualScriptHandlerReference(
        PythonScriptEventKind eventKind,
        Guid scriptId,
        string entryPoint)
    {
        if (scriptId == Guid.Empty)
            throw new ArgumentException("Script stable ID is required.", nameof(scriptId));
        if (string.IsNullOrWhiteSpace(entryPoint))
            throw new ArgumentException("Script entry point is required.", nameof(entryPoint));

        EventKind = eventKind;
        ScriptId = scriptId;
        EntryPoint = entryPoint;
    }

    public PythonScriptEventKind EventKind { get; }

    public Guid ScriptId { get; }

    public string EntryPoint { get; }
}

public sealed class VisualObjectRuntimeDefinition
{
    public VisualObjectRuntimeDefinition(
        Guid id,
        string developerKey,
        VisualEngineeringPropertySet engineering,
        Guid? parentObjectId = null,
        IReadOnlyCollection<VisualScriptHandlerReference>? eventHandlers = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Visual object stable ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(developerKey))
            throw new ArgumentException("Visual object developer key is required.", nameof(developerKey));

        ArgumentNullException.ThrowIfNull(engineering);

        if (parentObjectId == id)
            throw new ArgumentException("A visual object cannot be its own parent.", nameof(parentObjectId));

        Id = id;
        DeveloperKey = developerKey;
        Engineering = engineering;
        ParentObjectId = parentObjectId;
        EventHandlers = Array.AsReadOnly((eventHandlers ?? Array.Empty<VisualScriptHandlerReference>()).ToArray());
        Metadata = new ReadOnlyDictionary<string, string>(
            (metadata ?? new Dictionary<string, string>())
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

        ValidateEventHandlers(EventHandlers);
    }

    public Guid Id { get; }

    public string DeveloperKey { get; }

    public string ObjectTypeKey => Engineering.Schema.ObjectTypeKey;

    public VisualEngineeringPropertySet Engineering { get; }

    public Guid? ParentObjectId { get; }

    public IReadOnlyCollection<VisualScriptHandlerReference> EventHandlers { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static void ValidateEventHandlers(
        IReadOnlyCollection<VisualScriptHandlerReference> eventHandlers)
    {
        var seen = new HashSet<(PythonScriptEventKind EventKind, Guid ScriptId, string EntryPoint)>();

        foreach (var handler in eventHandlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            if (!seen.Add((handler.EventKind, handler.ScriptId, handler.EntryPoint)))
            {
                throw new ArgumentException(
                    $"Duplicate visual event handler '{handler.EventKind}:{handler.ScriptId}:{handler.EntryPoint}'.",
                    nameof(eventHandlers));
            }
        }
    }
}

public sealed class VisualRuntimeDefinition
{
    private readonly IReadOnlyDictionary<Guid, VisualObjectRuntimeDefinition> _objectsById;
    private readonly IReadOnlyDictionary<string, VisualObjectRuntimeDefinition> _objectsByKey;

    public VisualRuntimeDefinition(
        Guid id,
        string developerKey,
        VisualRuntimeDefinitionKind kind,
        IReadOnlyCollection<VisualObjectRuntimeDefinition> objects,
        IReadOnlyCollection<VisualScriptHandlerReference>? lifecycleHandlers = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Visual definition stable ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(developerKey))
            throw new ArgumentException("Visual definition developer key is required.", nameof(developerKey));

        ArgumentNullException.ThrowIfNull(objects);

        Id = id;
        DeveloperKey = developerKey;
        Kind = kind;

        var objectsById = new Dictionary<Guid, VisualObjectRuntimeDefinition>();
        var objectsByKey = new Dictionary<string, VisualObjectRuntimeDefinition>(StringComparer.Ordinal);

        foreach (var visualObject in objects)
        {
            ArgumentNullException.ThrowIfNull(visualObject);

            if (!objectsById.TryAdd(visualObject.Id, visualObject))
                throw new ArgumentException(
                    $"Duplicate visual object stable ID '{visualObject.Id}'.",
                    nameof(objects));

            if (!objectsByKey.TryAdd(visualObject.DeveloperKey, visualObject))
                throw new ArgumentException(
                    $"Duplicate visual object developer key '{visualObject.DeveloperKey}'.",
                    nameof(objects));
        }

        ValidateParentGraph(objectsById);

        _objectsById = new ReadOnlyDictionary<Guid, VisualObjectRuntimeDefinition>(objectsById);
        _objectsByKey = new ReadOnlyDictionary<string, VisualObjectRuntimeDefinition>(objectsByKey);
        LifecycleHandlers = Array.AsReadOnly((lifecycleHandlers ?? Array.Empty<VisualScriptHandlerReference>()).ToArray());
        Metadata = new ReadOnlyDictionary<string, string>(
            (metadata ?? new Dictionary<string, string>())
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

        ValidateLifecycleHandlers(LifecycleHandlers);
    }

    public Guid Id { get; }

    public string DeveloperKey { get; }

    public VisualRuntimeDefinitionKind Kind { get; }

    public IReadOnlyDictionary<Guid, VisualObjectRuntimeDefinition> ObjectsById => _objectsById;

    public IReadOnlyDictionary<string, VisualObjectRuntimeDefinition> ObjectsByKey => _objectsByKey;

    public IReadOnlyCollection<VisualScriptHandlerReference> LifecycleHandlers { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public VisualObjectRuntimeDefinition GetRequiredObject(Guid objectId)
    {
        if (!_objectsById.TryGetValue(objectId, out var visualObject))
            throw new KeyNotFoundException(
                $"Visual definition '{DeveloperKey}' does not contain object '{objectId}'.");

        return visualObject;
    }

    public VisualObjectRuntimeDefinition GetRequiredObject(string developerKey)
    {
        if (string.IsNullOrWhiteSpace(developerKey))
            throw new ArgumentException("Visual object developer key is required.", nameof(developerKey));

        if (!_objectsByKey.TryGetValue(developerKey, out var visualObject))
            throw new KeyNotFoundException(
                $"Visual definition '{DeveloperKey}' does not contain object '{developerKey}'.");

        return visualObject;
    }

    private static void ValidateParentGraph(
        IReadOnlyDictionary<Guid, VisualObjectRuntimeDefinition> objects)
    {
        foreach (var visualObject in objects.Values)
        {
            if (visualObject.ParentObjectId is { } parentId && !objects.ContainsKey(parentId))
            {
                throw new ArgumentException(
                    $"Visual object '{visualObject.DeveloperKey}' references missing parent '{parentId}'.",
                    nameof(objects));
            }
        }

        foreach (var visualObject in objects.Values)
        {
            var visited = new HashSet<Guid>();
            var current = visualObject;

            while (current.ParentObjectId is { } ancestorId)
            {
                if (!visited.Add(current.Id))
                {
                    throw new ArgumentException(
                        $"Visual object parent graph contains a cycle involving '{visualObject.DeveloperKey}'.",
                        nameof(objects));
                }

                current = objects[ancestorId];
            }
        }
    }

    private static void ValidateLifecycleHandlers(
        IReadOnlyCollection<VisualScriptHandlerReference> lifecycleHandlers)
    {
        var seen = new HashSet<(PythonScriptEventKind EventKind, Guid ScriptId, string EntryPoint)>();

        foreach (var handler in lifecycleHandlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            if (handler.EventKind is not (PythonScriptEventKind.Initialize or PythonScriptEventKind.Dispose))
            {
                throw new ArgumentException(
                    $"Visual definition lifecycle handler cannot use event '{handler.EventKind}'.",
                    nameof(lifecycleHandlers));
            }

            if (!seen.Add((handler.EventKind, handler.ScriptId, handler.EntryPoint)))
            {
                throw new ArgumentException(
                    $"Duplicate visual lifecycle handler '{handler.EventKind}:{handler.ScriptId}:{handler.EntryPoint}'.",
                    nameof(lifecycleHandlers));
            }
        }
    }
}

public sealed class VisualRuntimeInstanceIdentity
{
    public VisualRuntimeInstanceIdentity(
        Guid definitionId,
        string clientSessionId,
        Guid runtimeInstanceId)
    {
        if (definitionId == Guid.Empty)
            throw new ArgumentException("Visual definition ID is required.", nameof(definitionId));
        if (string.IsNullOrWhiteSpace(clientSessionId))
            throw new ArgumentException("Client session ID is required.", nameof(clientSessionId));
        if (runtimeInstanceId == Guid.Empty)
            throw new ArgumentException("Runtime instance ID is required.", nameof(runtimeInstanceId));

        DefinitionId = definitionId;
        ClientSessionId = clientSessionId;
        RuntimeInstanceId = runtimeInstanceId;
    }

    public Guid DefinitionId { get; }

    public string ClientSessionId { get; }

    public Guid RuntimeInstanceId { get; }

    public string RuntimeKey =>
        $"{ClientSessionId}/{DefinitionId:N}/{RuntimeInstanceId:N}";
}

public sealed record VisualRuntimeObjectReference(
    Guid EngineeringObjectId,
    string DeveloperKey,
    string ObjectTypeKey);

public sealed class VisualRuntimeObjectInstance
{
    private readonly Func<bool> _ownerDisposed;
    private readonly VisualRuntimePropertyState _properties;

    internal VisualRuntimeObjectInstance(
        VisualObjectRuntimeDefinition definition,
        string runtimeInstanceKey,
        Func<bool> ownerDisposed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(ownerDisposed);

        Definition = definition;
        _ownerDisposed = ownerDisposed;
        _properties = new VisualRuntimePropertyState(
            $"{runtimeInstanceKey}/{definition.Id:N}",
            definition.Engineering);
    }

    public VisualObjectRuntimeDefinition Definition { get; }

    public VisualRuntimeObjectReference Reference =>
        new(Definition.Id, Definition.DeveloperKey, Definition.ObjectTypeKey);

    public VisualResolvedPropertyValue ReadProperty(string propertyKey)
    {
        ThrowIfDisposed();
        return _properties.Resolve(propertyKey);
    }

    public void WriteScriptProperty(
        string propertyKey,
        VisualPropertyValue value)
    {
        ThrowIfDisposed();
        _properties.SetScriptOverride(propertyKey, value);
    }

    public void ClearScriptProperty(string propertyKey)
    {
        ThrowIfDisposed();
        _properties.ClearScriptOverride(propertyKey);
    }

    internal VisualRuntimePropertyState PropertyState
    {
        get
        {
            ThrowIfDisposed();
            return _properties;
        }
    }

    internal void ResetRuntimeState() => _properties.ClearAllRuntimeOverrides();

    private void ThrowIfDisposed()
    {
        if (_ownerDisposed())
            throw new ObjectDisposedException(nameof(VisualRuntimeInstance));
    }
}

public sealed class VisualRuntimeInstance : IDisposable
{
    private readonly IReadOnlyDictionary<Guid, VisualRuntimeObjectInstance> _objectsById;
    private readonly IReadOnlyDictionary<string, VisualRuntimeObjectInstance> _objectsByKey;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;

    public VisualRuntimeInstance(
        VisualRuntimeDefinition definition,
        string clientSessionId)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Definition = definition;
        Identity = new VisualRuntimeInstanceIdentity(
            definition.Id,
            clientSessionId,
            Guid.NewGuid());

        var objectsById = new Dictionary<Guid, VisualRuntimeObjectInstance>();
        var objectsByKey = new Dictionary<string, VisualRuntimeObjectInstance>(StringComparer.Ordinal);

        foreach (var definitionObject in definition.ObjectsById.Values)
        {
            var runtimeObject = new VisualRuntimeObjectInstance(
                definitionObject,
                Identity.RuntimeKey,
                () => _disposed);

            objectsById.Add(definitionObject.Id, runtimeObject);
            objectsByKey.Add(definitionObject.DeveloperKey, runtimeObject);
        }

        _objectsById = new ReadOnlyDictionary<Guid, VisualRuntimeObjectInstance>(objectsById);
        _objectsByKey = new ReadOnlyDictionary<string, VisualRuntimeObjectInstance>(objectsByKey);
        Subscriptions = new ScriptEventSubscriptionRegistry(Identity.RuntimeKey);
    }

    public VisualRuntimeDefinition Definition { get; }

    public VisualRuntimeInstanceIdentity Identity { get; }

    public CancellationToken LifetimeCancellation => _lifetime.Token;

    public ScriptEventSubscriptionRegistry Subscriptions { get; }

    public bool IsDisposed => _disposed;

    public IReadOnlyCollection<VisualRuntimeObjectReference> ListObjects()
    {
        ThrowIfDisposed();
        return Array.AsReadOnly(
            _objectsById.Values
                .OrderBy(item => item.Definition.DeveloperKey, StringComparer.Ordinal)
                .Select(item => item.Reference)
                .ToArray());
    }

    public VisualRuntimeObjectInstance GetRequiredObject(Guid objectId)
    {
        ThrowIfDisposed();

        if (!_objectsById.TryGetValue(objectId, out var runtimeObject))
            throw new KeyNotFoundException(
                $"Runtime visual instance does not contain object '{objectId}'.");

        return runtimeObject;
    }

    public VisualRuntimeObjectInstance GetRequiredObject(string developerKey)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(developerKey))
            throw new ArgumentException("Visual object developer key is required.", nameof(developerKey));

        if (!_objectsByKey.TryGetValue(developerKey, out var runtimeObject))
            throw new KeyNotFoundException(
                $"Runtime visual instance does not contain object '{developerKey}'.");

        return runtimeObject;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _lifetime.Cancel();
        Subscriptions.Dispose();

        foreach (var runtimeObject in _objectsById.Values)
            runtimeObject.ResetRuntimeState();

        _lifetime.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(VisualRuntimeInstance));
    }
}

public interface IClientVisualObjectApi
{
    VisualRuntimeInstanceIdentity InstanceIdentity { get; }

    IReadOnlyCollection<VisualRuntimeObjectReference> ListObjects();

    VisualRuntimeObjectReference GetObject(Guid objectId);

    VisualRuntimeObjectReference GetObject(string developerKey);

    VisualResolvedPropertyValue ReadProperty(
        Guid objectId,
        string propertyKey);

    void WriteProperty(
        Guid objectId,
        string propertyKey,
        VisualPropertyValue value);

    void ClearScriptOverride(
        Guid objectId,
        string propertyKey);

    ValueTask<VisualTweenHandle> AnimateAsync(
        Guid objectId,
        VisualTweenRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Safe client-side adapter exposed to a future Python runtime. It only addresses declared objects
/// in the current visual instance and declared visual properties; it has no renderer/DOM access.
/// </summary>
public sealed class ClientVisualObjectApi : IClientVisualObjectApi
{
    private readonly VisualRuntimeInstance _instance;
    private readonly IVisualTweenScheduler _tweenScheduler;

    public ClientVisualObjectApi(
        VisualRuntimeInstance instance,
        IVisualTweenScheduler tweenScheduler)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(tweenScheduler);

        _instance = instance;
        _tweenScheduler = tweenScheduler;
    }

    public VisualRuntimeInstanceIdentity InstanceIdentity => _instance.Identity;

    public IReadOnlyCollection<VisualRuntimeObjectReference> ListObjects() =>
        _instance.ListObjects();

    public VisualRuntimeObjectReference GetObject(Guid objectId) =>
        _instance.GetRequiredObject(objectId).Reference;

    public VisualRuntimeObjectReference GetObject(string developerKey) =>
        _instance.GetRequiredObject(developerKey).Reference;

    public VisualResolvedPropertyValue ReadProperty(
        Guid objectId,
        string propertyKey) =>
        _instance.GetRequiredObject(objectId).ReadProperty(propertyKey);

    public void WriteProperty(
        Guid objectId,
        string propertyKey,
        VisualPropertyValue value) =>
        _instance.GetRequiredObject(objectId).WriteScriptProperty(propertyKey, value);

    public void ClearScriptOverride(
        Guid objectId,
        string propertyKey) =>
        _instance.GetRequiredObject(objectId).ClearScriptProperty(propertyKey);

    public ValueTask<VisualTweenHandle> AnimateAsync(
        Guid objectId,
        VisualTweenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runtimeObject = _instance.GetRequiredObject(objectId);
        var schema = runtimeObject.Definition.Engineering.Schema;
        var property = schema.GetRequired(request.PropertyKey);

        if (!property.RuntimeWritable)
        {
            throw new InvalidOperationException(
                $"Property '{request.PropertyKey}' is not writable by a client visual script.");
        }

        request.ValidateFor(schema);

        return _tweenScheduler.StartAsync(
            _instance.Identity.RuntimeKey,
            objectId.ToString("D"),
            schema,
            request,
            cancellationToken);
    }
}
