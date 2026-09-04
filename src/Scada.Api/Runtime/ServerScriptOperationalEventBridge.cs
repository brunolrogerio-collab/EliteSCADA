using System.Runtime.CompilerServices;
using Scada.Core.Events;
using Scada.DriverHost.Runtime;

namespace Scada.Api.Runtime;

/// <summary>
/// Binds one shared ServerScriptRuntimeManager to the canonical C14 Operational
/// Event runtime authority and serializes event emission against Active revision
/// activation/recovery. The manager itself remains the stable per-runtime key, so
/// diagnostics resolving the shared host do not create another event authority.
/// </summary>
internal static class ServerScriptOperationalEventBridge
{
    private static readonly ConditionalWeakTable<ServerScriptRuntimeManager, BindingSlot> Bindings = new();

    public static async ValueTask<IAsyncDisposable> BindForActivationAsync(
        ServerScriptRuntimeManager host,
        IEngineeringRuntimeCoordinator runtime,
        IOperationalEventRuntime operationalEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(operationalEvents);

        var slot = Bindings.GetValue(host, static _ => new BindingSlot());
        await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (slot.Runtime is not null && !ReferenceEquals(slot.Runtime, runtime))
            {
                throw new InvalidOperationException(
                    "Server Script Operational Event bridge cannot be rebound to a different Engineering runtime.");
            }

            if (slot.OperationalEvents is not null && !ReferenceEquals(slot.OperationalEvents, operationalEvents))
            {
                throw new InvalidOperationException(
                    "Server Script Operational Event bridge cannot be rebound to a different Operational Event runtime.");
            }

            slot.Runtime = runtime;
            slot.OperationalEvents = operationalEvents;
            return new ActivationLease(slot.Gate);
        }
        catch
        {
            slot.Gate.Release();
            throw;
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

        if (!Bindings.TryGetValue(host, out var slot) || slot.Runtime is null || slot.OperationalEvents is null)
        {
            throw new ScriptExecutionDiagnosticException(
                "Operational Event runtime authority is unavailable for this Server Script host.");
        }

        await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var descriptor = slot.Runtime.Describe();
            if (!string.Equals(descriptor.ProjectKey, projectKey, StringComparison.Ordinal) ||
                descriptor.Revision != revision)
            {
                throw new ScriptExecutionDiagnosticException(
                    "Server Script Operational Event emission belongs to an obsolete Active runtime revision.");
            }

            if (!slot.OperationalEvents.TryGetOperationalEvent(definitionId, out var definition) || definition is null)
            {
                throw new ScriptExecutionDiagnosticException(
                    $"Operational Event definition '{definitionId:D}' is not active in the current Engineering revision.");
            }

            try
            {
                return await slot.OperationalEvents.EmitOperationalEventAsync(
                        definition.Id,
                        new OperationalEventEmissionContext(
                            Message: message,
                            Context: context),
                        cancellationToken)
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
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    private sealed class BindingSlot
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public IEngineeringRuntimeCoordinator? Runtime { get; set; }
        public IOperationalEventRuntime? OperationalEvents { get; set; }
    }

    private sealed class ActivationLease(SemaphoreSlim gate) : IAsyncDisposable
    {
        private SemaphoreSlim? _gate = gate;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _gate, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
