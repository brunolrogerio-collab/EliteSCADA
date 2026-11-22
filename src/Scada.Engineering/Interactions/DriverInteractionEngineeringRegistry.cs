namespace Scada.Engineering.Interactions;

public interface IDriverInteractionEngineeringRegistry
{
    IReadOnlyCollection<TransientEventDefinitionEngineeringDto> SnapshotTransientEventDefinitions();
    IReadOnlyCollection<CapabilityEventReferenceEngineeringDto> SnapshotCapabilityEventReferences();
    IReadOnlyCollection<RichCommandDefinitionEngineeringDto> SnapshotRichCommandDefinitions();
    IReadOnlyCollection<DriverCommandBindingEngineeringDto> SnapshotDriverCommandBindings();

    TransientEventDefinitionEngineeringDto? FindTransientEventDefinition(Guid definitionId);
    CapabilityEventReferenceEngineeringDto? FindCapabilityEventReference(Guid equipmentId, string capabilityId, string role);
    RichCommandDefinitionEngineeringDto? FindRichCommandDefinition(Guid commandId);
    DriverCommandBindingEngineeringDto? FindDriverCommandBinding(Guid commandId);

    void UpsertTransientEventDefinition(TransientEventDefinitionEngineeringDto definition);
    void UpsertCapabilityEventReference(CapabilityEventReferenceEngineeringDto reference);
    void UpsertRichCommandDefinition(RichCommandDefinitionEngineeringDto definition);
    void UpsertDriverCommandBinding(DriverCommandBindingEngineeringDto binding);
}

public sealed class InMemoryDriverInteractionEngineeringRegistry : IDriverInteractionEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, TransientEventDefinitionEngineeringDto> _eventDefinitions = new();
    private readonly Dictionary<EventReferenceIdentity, CapabilityEventReferenceEngineeringDto> _eventReferences = new();
    private readonly Dictionary<Guid, RichCommandDefinitionEngineeringDto> _richCommandDefinitions = new();
    private readonly Dictionary<Guid, DriverCommandBindingEngineeringDto> _driverCommandBindings = new();
    private readonly Action? _changed;

    public InMemoryDriverInteractionEngineeringRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public IReadOnlyCollection<TransientEventDefinitionEngineeringDto> SnapshotTransientEventDefinitions()
    {
        lock (_sync)
            return _eventDefinitions.Values
                .OrderBy(item => item.SemanticKey, StringComparer.Ordinal)
                .ThenBy(item => item.DefinitionId)
                .ToArray();
    }

    public IReadOnlyCollection<CapabilityEventReferenceEngineeringDto> SnapshotCapabilityEventReferences()
    {
        lock (_sync)
            return _eventReferences.Values
                .OrderBy(item => item.EquipmentId)
                .ThenBy(item => item.CapabilityId, StringComparer.Ordinal)
                .ThenBy(item => item.Role, StringComparer.Ordinal)
                .ToArray();
    }

    public IReadOnlyCollection<RichCommandDefinitionEngineeringDto> SnapshotRichCommandDefinitions()
    {
        lock (_sync)
            return _richCommandDefinitions.Values
                .OrderBy(item => item.SemanticKey, StringComparer.Ordinal)
                .ThenBy(item => item.CommandId)
                .ToArray();
    }

    public IReadOnlyCollection<DriverCommandBindingEngineeringDto> SnapshotDriverCommandBindings()
    {
        lock (_sync)
            return _driverCommandBindings.Values
                .OrderBy(item => item.CommandId)
                .ToArray();
    }

    public TransientEventDefinitionEngineeringDto? FindTransientEventDefinition(Guid definitionId)
    {
        lock (_sync)
            return _eventDefinitions.GetValueOrDefault(definitionId);
    }

    public CapabilityEventReferenceEngineeringDto? FindCapabilityEventReference(
        Guid equipmentId,
        string capabilityId,
        string role)
    {
        lock (_sync)
            return _eventReferences.GetValueOrDefault(new EventReferenceIdentity(equipmentId, capabilityId, role));
    }

    public RichCommandDefinitionEngineeringDto? FindRichCommandDefinition(Guid commandId)
    {
        lock (_sync)
            return _richCommandDefinitions.GetValueOrDefault(commandId);
    }

    public DriverCommandBindingEngineeringDto? FindDriverCommandBinding(Guid commandId)
    {
        lock (_sync)
            return _driverCommandBindings.GetValueOrDefault(commandId);
    }

    public void UpsertTransientEventDefinition(TransientEventDefinitionEngineeringDto definition)
    {
        var normalized = DriverInteractionEngineeringMapper.ToEngineering(
            DriverInteractionEngineeringMapper.ToCore(definition));
        lock (_sync)
            _eventDefinitions[normalized.DefinitionId] = normalized;
        _changed?.Invoke();
    }

    public void UpsertCapabilityEventReference(CapabilityEventReferenceEngineeringDto reference)
    {
        var normalized = DriverInteractionEngineeringMapper.ToEngineering(
            DriverInteractionEngineeringMapper.ToCore(reference));
        lock (_sync)
            _eventReferences[new EventReferenceIdentity(
                normalized.EquipmentId,
                normalized.CapabilityId,
                normalized.Role)] = normalized;
        _changed?.Invoke();
    }

    public void UpsertRichCommandDefinition(RichCommandDefinitionEngineeringDto definition)
    {
        var normalized = DriverInteractionEngineeringMapper.ToEngineering(
            DriverInteractionEngineeringMapper.ToCore(definition));
        lock (_sync)
            _richCommandDefinitions[normalized.CommandId] = normalized;
        _changed?.Invoke();
    }

    public void UpsertDriverCommandBinding(DriverCommandBindingEngineeringDto binding)
    {
        var normalized = DriverInteractionEngineeringMapper.ToEngineering(
            DriverInteractionEngineeringMapper.ToCore(binding));
        lock (_sync)
            _driverCommandBindings[normalized.CommandId] = normalized;
        _changed?.Invoke();
    }

    public void Clear()
    {
        lock (_sync)
        {
            _eventDefinitions.Clear();
            _eventReferences.Clear();
            _richCommandDefinitions.Clear();
            _driverCommandBindings.Clear();
        }

        _changed?.Invoke();
    }

    private readonly record struct EventReferenceIdentity(
        Guid EquipmentId,
        string CapabilityId,
        string Role);
}
