using System.Runtime.CompilerServices;
using Scada.Core.Events;
using Scada.DriverHost.Runtime;
using Scada.Engineering.VisualScripting;

namespace Scada.Api.Runtime;

/// <summary>
/// Binds one shared ServerScriptRuntimeManager to the canonical C14 Operational
/// Event authority and serializes ordinary emissions against Active revision
/// activation/recovery. Activation owns a scoped re-entrant lease so Initialize
/// handlers of the newly activated generation may emit after the canonical runtime
/// swap without deadlocking on the gate held by their own activation flow.
/// </summary>
internal static class ServerScriptOperationalEventBridge
{
    private static readonly ConditionalWeakTable<ServerScriptRuntimeManager, BindingSlot> Bindings = new();
    private static readonly AsyncLocal<LeaseContext?> CurrentLease = new();

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

            var token = new object();
            var previous = CurrentLease.Value;
            slot.ActiveLeaseToken = token;
            CurrentLease.Value = new LeaseContext(slot, token);
            return new ActivationLease(slot, token, previous);
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

        var lease = CurrentLease.Value;
        var ownsActivationLease = lease is not null &&
                                  ReferenceEquals(lease.Slot, slot) &&
                                  ReferenceEquals(lease.Token, slot.ActiveLeaseToken);

        if (!ownsActivationLease)
            await slot.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await EmitBoundAsync(
                    slot,
                    projectKey,
                    revision,
                    definitionId,
                    message,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (!ownsActivationLease)
                slot.Gate.Release();
        }
    }

    private static async ValueTask<OperationalEventOccurred> EmitBoundAsync(
        BindingSlot slot,
        string projectKey,
        long revision,
        Guid definitionId,
        string? message,
        IReadOnlyDictionary<string, string>? context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var runtime = slot.Runtime
            ?? throw new ScriptExecutionDiagnosticException("Engineering runtime authority is unavailable.");
        var operationalEvents = slot.OperationalEvents
            ?? throw new ScriptExecutionDiagnosticException("Operational Event runtime authority is unavailable.");

        var descriptor = runtime.Describe();
        if (!string.Equals(descriptor.ProjectKey, projectKey, StringComparison.Ordinal) ||
            descriptor.Revision != revision)
        {
            throw new ScriptExecutionDiagnosticException(
                "Server Script Operational Event emission belongs to an obsolete Active runtime revision.");
        }

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

    private sealed class BindingSlot
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public IEngineeringRuntimeCoordinator? Runtime { get; set; }
        public IOperationalEventRuntime? OperationalEvents { get; set; }
        public object? ActiveLeaseToken { get; set; }
    }

    private sealed record LeaseContext(BindingSlot Slot, object Token);

    private sealed class ActivationLease(
        BindingSlot slot,
        object token,
        LeaseContext? previous) : IAsyncDisposable
    {
        private BindingSlot? _slot = slot;

        public ValueTask DisposeAsync()
        {
            var releasedSlot = Interlocked.Exchange(ref _slot, null);
            if (releasedSlot is null) return ValueTask.CompletedTask;

            if (ReferenceEquals(releasedSlot.ActiveLeaseToken, token))
                releasedSlot.ActiveLeaseToken = null;

            var current = CurrentLease.Value;
            if (current is not null &&
                ReferenceEquals(current.Slot, releasedSlot) &&
                ReferenceEquals(current.Token, token))
            {
                CurrentLease.Value = previous;
            }

            releasedSlot.Gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
