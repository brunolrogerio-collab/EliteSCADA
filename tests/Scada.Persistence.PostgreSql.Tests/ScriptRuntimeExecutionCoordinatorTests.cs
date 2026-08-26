using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class ScriptRuntimeExecutionCoordinatorTests
{
    [Fact]
    public async Task Coordinator_ContainsHandlerFaultAndContinuesWithNextQueuedEvent()
    {
        var firstEntry = new PythonScriptEntryPoint(
            PythonScriptEventKind.ObjectInteraction,
            "on_click",
            "object:first");
        var secondEntry = new PythonScriptEntryPoint(
            PythonScriptEventKind.ObjectInteraction,
            "on_click",
            "object:second");
        var script = CreateClientScript(firstEntry, secondEntry);
        var executor = new FaultThenCompleteExecutor();
        var policy = new ScriptExecutionPolicy(
            TimeSpan.FromMilliseconds(200),
            maxQueuedEvents: 8,
            minimumTimerInterval: TimeSpan.FromMilliseconds(50),
            maxConsecutiveFailuresBeforeThrottle: 3);

        await using var coordinator = new ScriptRuntimeExecutionCoordinator(
            script,
            "runtime-1",
            policy,
            executor);

        coordinator.Enqueue(
            new ScriptEventIdentity(
                firstEntry.EventKind,
                firstEntry.HandlerName,
                firstEntry.TargetReference));
        coordinator.Enqueue(
            new ScriptEventIdentity(
                secondEntry.EventKind,
                secondEntry.HandlerName,
                secondEntry.TargetReference));

        var first = await coordinator.ProcessNextAsync();
        var second = await coordinator.ProcessNextAsync();

        Assert.Equal(ScriptRuntimeDispatchStatus.Executed, first.Status);
        Assert.Equal(ScriptExecutionStatus.Faulted, first.Execution!.Status);
        Assert.Equal(ScriptRuntimeDispatchStatus.Executed, second.Status);
        Assert.Equal(ScriptExecutionStatus.Completed, second.Execution!.Status);
        Assert.Equal(2, executor.InvocationCount);
        Assert.Equal(0, coordinator.QueuedEventCount);

        var diagnostics = coordinator.GetDiagnostics();
        Assert.Equal(2L, diagnostics.ExecutionCount);
        Assert.Equal(1L, diagnostics.FaultedCount);
        Assert.Equal(1L, diagnostics.CompletedCount);
        Assert.NotNull(diagnostics.LastSanitizedError);
        Assert.DoesNotContain("\n", diagnostics.LastSanitizedError!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Coordinator_TimesOutCompliantHandlerAndStopsDispatchWhenThrottled()
    {
        var firstEntry = new PythonScriptEntryPoint(
            PythonScriptEventKind.ObjectInteraction,
            "on_click",
            "object:first");
        var secondEntry = new PythonScriptEntryPoint(
            PythonScriptEventKind.ObjectInteraction,
            "on_click",
            "object:second");
        var script = CreateClientScript(firstEntry, secondEntry);
        var executor = new WaitForCancellationExecutor();
        var policy = new ScriptExecutionPolicy(
            TimeSpan.FromMilliseconds(30),
            maxQueuedEvents: 8,
            minimumTimerInterval: TimeSpan.FromMilliseconds(50),
            maxConsecutiveFailuresBeforeThrottle: 1);

        await using var coordinator = new ScriptRuntimeExecutionCoordinator(
            script,
            "runtime-1",
            policy,
            executor);

        coordinator.Enqueue(
            new ScriptEventIdentity(
                firstEntry.EventKind,
                firstEntry.HandlerName,
                firstEntry.TargetReference));
        coordinator.Enqueue(
            new ScriptEventIdentity(
                secondEntry.EventKind,
                secondEntry.HandlerName,
                secondEntry.TargetReference));

        var first = await coordinator.ProcessNextAsync();
        var throttled = await coordinator.ProcessNextAsync();

        Assert.Equal(ScriptExecutionStatus.TimedOut, first.Execution!.Status);
        Assert.Equal(ScriptRuntimeDispatchStatus.Throttled, throttled.Status);
        Assert.Null(throttled.Execution);
        Assert.Equal(1, executor.InvocationCount);
        Assert.Equal(1, coordinator.QueuedEventCount);

        var diagnostics = coordinator.GetDiagnostics();
        Assert.Equal(1L, diagnostics.TimeoutCount);
        Assert.True(diagnostics.IsThrottled);
        Assert.Equal(1, diagnostics.ConsecutiveFailures);
    }

    [Fact]
    public async Task Coordinator_RejectsUndeclaredOrCrossScopeEventsBeforeQueueing()
    {
        var declaredEntry = new PythonScriptEntryPoint(
            PythonScriptEventKind.ObjectInteraction,
            "on_click",
            "object:first");
        var clientScript = CreateClientScript(declaredEntry);
        var policy = ScriptExecutionPolicy.SafeDefault;

        await using var clientCoordinator = new ScriptRuntimeExecutionCoordinator(
            clientScript,
            "client-runtime",
            policy,
            new NoOpExecutor());

        Assert.Throws<InvalidOperationException>(() =>
            clientCoordinator.Enqueue(
                new ScriptEventIdentity(
                    PythonScriptEventKind.ObjectInteraction,
                    "on_click",
                    "object:undeclared")));
        Assert.Equal(0, clientCoordinator.QueuedEventCount);

        var invalidServerEntry = new PythonScriptEntryPoint(
            PythonScriptEventKind.ObjectInteraction,
            "on_click",
            "object:first");
        var serverScript = new PythonScriptDefinition(
            Guid.NewGuid(),
            "server/scripts/invalid-visual",
            "Invalid Visual",
            PythonScriptScope.Server,
            "def on_click():\n    pass",
            entryPoints: [invalidServerEntry]);

        await using var serverCoordinator = new ScriptRuntimeExecutionCoordinator(
            serverScript,
            "server-runtime",
            policy,
            new NoOpExecutor());

        Assert.Throws<InvalidOperationException>(() =>
            serverCoordinator.Enqueue(
                new ScriptEventIdentity(
                    invalidServerEntry.EventKind,
                    invalidServerEntry.HandlerName,
                    invalidServerEntry.TargetReference)));
        Assert.Equal(0, serverCoordinator.QueuedEventCount);
    }

    [Fact]
    public async Task CallerCancellation_IsReportedAsCancelledInsteadOfTimeout()
    {
        var entry = new PythonScriptEntryPoint(
            PythonScriptEventKind.ObjectInteraction,
            "on_click",
            "object:first");
        var script = CreateClientScript(entry);
        var executor = new WaitForCancellationExecutor();
        var policy = new ScriptExecutionPolicy(
            TimeSpan.FromSeconds(2),
            maxQueuedEvents: 4,
            minimumTimerInterval: TimeSpan.FromMilliseconds(50),
            maxConsecutiveFailuresBeforeThrottle: 3);

        await using var coordinator = new ScriptRuntimeExecutionCoordinator(
            script,
            "runtime-1",
            policy,
            executor);

        coordinator.Enqueue(
            new ScriptEventIdentity(
                entry.EventKind,
                entry.HandlerName,
                entry.TargetReference));

        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(30));

        var result = await coordinator.ProcessNextAsync(cancellation.Token);

        Assert.Equal(ScriptExecutionStatus.Cancelled, result.Execution!.Status);
        Assert.Equal(0L, coordinator.GetDiagnostics().TimeoutCount);
        Assert.Equal(1L, coordinator.GetDiagnostics().CancelledCount);
    }

    private static PythonScriptDefinition CreateClientScript(
        params PythonScriptEntryPoint[] entryPoints) =>
        new(
            Guid.NewGuid(),
            "screens/main/scripts/runtime",
            "Runtime",
            PythonScriptScope.ClientVisual,
            "def on_click():\n    pass",
            entryPoints: entryPoints);

    private sealed class NoOpExecutor : IPythonScriptHandlerExecutor
    {
        public ValueTask ExecuteAsync(
            PythonScriptDefinition script,
            ScriptEventEnvelope scriptEvent,
            ScriptExecutionLease lease) =>
            ValueTask.CompletedTask;
    }

    private sealed class FaultThenCompleteExecutor : IPythonScriptHandlerExecutor
    {
        public int InvocationCount { get; private set; }

        public ValueTask ExecuteAsync(
            PythonScriptDefinition script,
            ScriptEventEnvelope scriptEvent,
            ScriptExecutionLease lease)
        {
            InvocationCount++;
            if (InvocationCount == 1)
                throw new InvalidOperationException("simulated\nhandler failure");

            return ValueTask.CompletedTask;
        }
    }

    private sealed class WaitForCancellationExecutor : IPythonScriptHandlerExecutor
    {
        public int InvocationCount { get; private set; }

        public async ValueTask ExecuteAsync(
            PythonScriptDefinition script,
            ScriptEventEnvelope scriptEvent,
            ScriptExecutionLease lease)
        {
            InvocationCount++;
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                lease.CancellationToken);
        }
    }
}
