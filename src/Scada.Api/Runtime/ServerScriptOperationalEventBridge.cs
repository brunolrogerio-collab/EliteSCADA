using System.Runtime.CompilerServices;
using Scada.Core.Events;
using Scada.DriverHost.Runtime;
using Scada.Engineering.VisualScripting;

namespace Scada.Api.Runtime;

/// <summary>
/// Binds one shared ServerScriptRuntimeManager to the canonical C14 Operational
/// Event authority. Emission is executed under the Server Script host's existing
/// revision gate so Operational Events have the same stale-generation protection
/// already proven for TAG and Server Memory access.
/// </summary>
internal static class ServerScriptOperationalEventBridge
{
    private static readonly ConditionalWeakTable<ServerScriptRuntimeManager, BindingSlot> Bindings = new();

    public static void Bind(
        ServerScriptRuntimeManager host,
        IOperationalEventRuntime operationalEvents)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(operationalEvents);

        var slot = Bindings.GetValue(host, static _ => new BindingSlot());
        lock (slot.Sync)
        {
            if (slot.OperationalEvents is not null &&
                !ReferenceEquals(slot.OperationalEvents, operationalEvents))
            {
                throw new InvalidOperationException(
                    "Server Script Operational Event bridge cannot be rebound to a different Operational Event runtime.");
            }

            slot.OperationalEvents = operationalEvents;
        }
    }

    public static async ValueTask<OperationalEventOccurred> EmitAsync(
        ServerScriptRuntimeManager host,
        string projectKey,
        long revision,
        Guid definitionId,
        string? message,
        IReadOnlyDictionary<string, string>? context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ScriptExecutionDiagnosticException("Operational Event emission requires an active project identity.");
        if (revision <= 0)
            throw new ScriptExecutionDiagnosticException("Operational Event emission requires an active revision identity.");
        if (definitionId == Guid.Empty)
            throw new ScriptExecutionDiagnosticException("Operational Event emission requires a stable non-empty definition ID.");

        if (!Bindings.TryGetValue(host, out var slot) || slot.OperationalEvents is null)
        {
            throw new ScriptExecutionDiagnosticException(
                "Operational Event runtime authority is unavailable for this Server Script host.");
        }

        return await host.ExecuteAgainstActiveRevisionAsync(
                projectKey,
                revision,
                async ct =>
                {
                    var operationalEvents = slot.OperationalEvents
                        ?? throw new ScriptExecutionDiagnosticException(
                            "Operational Event runtime authority is unavailable.");

                    if (!operationalEvents.TryGetOperationalEvent(definitionId, out var definition) || definition is null)
                    {
                        throw new ScriptExecutionDiagnosticException(
                            $"Operational Event definition '{definitionId:D}' is not active in the current Engineering revision.");
                    }

                    try
                    {
                        return await operationalEvents.EmitOperationalEventAsync(
                                definition.Id,
                                new OperationalEventEmissionContext(
                                    Message: message,
                                    Context: context),
                                ct)
                            .ConfigureAwait(false);
                    }
                    catch (ScriptExecutionDiagnosticException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
                    {
                        throw new ScriptExecutionDiagnosticException(
                            $"Operational Event emission was rejected by the active runtime ({ex.GetType().Name}).");
                    }
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class BindingSlot
    {
        public object Sync { get; } = new();
        public IOperationalEventRuntime? OperationalEvents { get; set; }
    }
}
