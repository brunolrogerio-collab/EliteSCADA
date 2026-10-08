using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Engineering.Contracts;
using Scada.Engineering.Interactions;

namespace Scada.DriverHost.Runtime;

/// <summary>
/// Immutable prepared S3 interaction graph. Preparation validates the complete
/// Engineering graph but does not alter the currently Active resolver authority.
/// </summary>
public sealed class PreparedDriverInteractionRuntimeGraph
{
    internal PreparedDriverInteractionRuntimeGraph(
        IReadOnlyDictionary<Guid, TransientEventDefinition> eventDefinitions,
        IReadOnlyCollection<CapabilityEventReference> eventReferences,
        IReadOnlyDictionary<Guid, RichCommandDefinition> richCommandDefinitions,
        IReadOnlyDictionary<Guid, DriverCommandBinding> driverCommandBindings)
    {
        EventDefinitions = eventDefinitions;
        EventReferences = eventReferences;
        RichCommandDefinitions = richCommandDefinitions;
        DriverCommandBindings = driverCommandBindings;
    }

    internal IReadOnlyDictionary<Guid, TransientEventDefinition> EventDefinitions { get; }
    internal IReadOnlyCollection<CapabilityEventReference> EventReferences { get; }
    internal IReadOnlyDictionary<Guid, RichCommandDefinition> RichCommandDefinitions { get; }
    internal IReadOnlyDictionary<Guid, DriverCommandBinding> DriverCommandBindings { get; }
}

public sealed record DriverInteractionRuntimeDescriptor(
    int EventDefinitionCount,
    int CapabilityEventReferenceCount,
    int RichCommandDefinitionCount,
    int DriverCommandBindingCount);

public sealed record ActiveTransientEventGraph(
    IReadOnlyCollection<TransientEventDefinition> Definitions,
    IReadOnlyCollection<CapabilityEventReference> References);

/// <summary>
/// Active-only authority for S1/S2 interaction resolvers. Working, Saved and
/// Published Engineering revisions can be prepared for validation, but resolvers
/// change only when Commit is called by the existing Active lifecycle boundary.
/// </summary>
public sealed class ActiveDriverInteractionRuntimeCatalog :
    ITransientEventDefinitionResolver,
    IRichCommandDefinitionResolver,
    IRichCommandBindingResolver
{
    private PreparedDriverInteractionRuntimeGraph _active = Empty();

    public PreparedDriverInteractionRuntimeGraph Prepare(EngineeringPackage package)
    {
        var graph = DriverInteractionEngineeringValidator.NormalizeActiveGraph(package);

        var eventDefinitions = graph.EventDefinitions.ToDictionary(
            definition => definition.DefinitionId);
        var richCommandDefinitions = graph.RichCommandDefinitions.ToDictionary(
            definition => definition.CommandId);
        var bindings = graph.DriverCommandBindings.ToDictionary(
            binding => binding.CommandId);

        return new PreparedDriverInteractionRuntimeGraph(
            eventDefinitions,
            graph.EventReferences.ToArray(),
            richCommandDefinitions,
            bindings);
    }

    public PreparedDriverInteractionRuntimeGraph CapturePrepared() =>
        Volatile.Read(ref _active);

    public void Commit(PreparedDriverInteractionRuntimeGraph prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        Volatile.Write(ref _active, prepared);
    }

    public void Replace(EngineeringPackage package) =>
        Commit(Prepare(package));

    public DriverInteractionRuntimeDescriptor Describe()
    {
        var active = Volatile.Read(ref _active);
        return new DriverInteractionRuntimeDescriptor(
            active.EventDefinitions.Count,
            active.EventReferences.Count,
            active.RichCommandDefinitions.Count,
            active.DriverCommandBindings.Count);
    }

    public IReadOnlyCollection<CapabilityEventReference> CapabilityEventReferences() =>
        Volatile.Read(ref _active).EventReferences.ToArray();

    public ActiveTransientEventGraph CaptureActiveTransientEvents()
    {
        var active = Volatile.Read(ref _active);
        return new ActiveTransientEventGraph(
            Array.AsReadOnly(active.EventDefinitions.Values.ToArray()),
            Array.AsReadOnly(active.EventReferences.ToArray()));
    }

    public bool TryResolve(Guid definitionId, out TransientEventDefinition? definition) =>
        Volatile.Read(ref _active).EventDefinitions.TryGetValue(definitionId, out definition);

    bool IRichCommandDefinitionResolver.TryResolve(
        Guid commandId,
        out RichCommandDefinition? definition) =>
        Volatile.Read(ref _active).RichCommandDefinitions.TryGetValue(commandId, out definition);

    bool IRichCommandBindingResolver.TryResolve(
        Guid commandId,
        out DriverCommandBinding? binding) =>
        Volatile.Read(ref _active).DriverCommandBindings.TryGetValue(commandId, out binding);

    private static PreparedDriverInteractionRuntimeGraph Empty() =>
        new(
            new Dictionary<Guid, TransientEventDefinition>(),
            Array.Empty<CapabilityEventReference>(),
            new Dictionary<Guid, RichCommandDefinition>(),
            new Dictionary<Guid, DriverCommandBinding>());
}
