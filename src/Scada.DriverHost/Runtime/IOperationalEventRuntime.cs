using Scada.Core.Events;

namespace Scada.DriverHost.Runtime;

/// <summary>
/// Active Runtime authority for engineer-authored operational process Events.
/// Consumers emit only definitions present in the currently activated revision.
/// </summary>
public interface IOperationalEventRuntime
{
    IReadOnlyCollection<OperationalEventDefinition> OperationalEventDefinitions();
    bool TryGetOperationalEvent(Guid definitionId, out OperationalEventDefinition? definition);
    ValueTask<OperationalEventOccurred> EmitOperationalEventAsync(
        Guid definitionId,
        OperationalEventEmissionContext? context = null,
        CancellationToken cancellationToken = default);
}