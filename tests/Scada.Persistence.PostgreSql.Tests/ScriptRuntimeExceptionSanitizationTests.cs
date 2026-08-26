using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class ScriptRuntimeExceptionSanitizationTests
{
    [Fact]
    public async Task ArbitraryExecutorException_DoesNotExposeRawMessage()
    {
        var entry = new PythonScriptEntryPoint(
            PythonScriptEventKind.ObjectInteraction,
            "on_click",
            "object:first");
        var script = CreateScript(entry);

        await using var coordinator = new ScriptRuntimeExecutionCoordinator(
            script,
            "runtime-1",
            ScriptExecutionPolicy.SafeDefault,
            new RawFaultExecutor());

        coordinator.Enqueue(
            new ScriptEventIdentity(
                entry.EventKind,
                entry.HandlerName,
                entry.TargetReference));

        var result = await coordinator.ProcessNextAsync();

        Assert.Equal(ScriptExecutionStatus.Faulted, result.Execution!.Status);
        Assert.Equal(nameof(InvalidOperationException), result.Execution.SanitizedError);
        Assert.DoesNotContain(
            "password=super-secret",
            result.Execution.SanitizedError!,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitSanitizedDiagnostic_IsAllowedThroughNarrowExceptionContract()
    {
        var entry = new PythonScriptEntryPoint(
            PythonScriptEventKind.ObjectInteraction,
            "on_click",
            "object:first");
        var script = CreateScript(entry);

        await using var coordinator = new ScriptRuntimeExecutionCoordinator(
            script,
            "runtime-1",
            ScriptExecutionPolicy.SafeDefault,
            new SanitizedFaultExecutor());

        coordinator.Enqueue(
            new ScriptEventIdentity(
                entry.EventKind,
                entry.HandlerName,
                entry.TargetReference));

        var result = await coordinator.ProcessNextAsync();

        Assert.Equal(ScriptExecutionStatus.Faulted, result.Execution!.Status);
        Assert.Contains(
            "line 4: invalid visual value",
            result.Execution.SanitizedError!,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\n", result.Execution.SanitizedError!, StringComparison.Ordinal);
    }

    private static PythonScriptDefinition CreateScript(
        PythonScriptEntryPoint entry) =>
        new(
            Guid.NewGuid(),
            "screens/main/scripts/fault",
            "Fault",
            PythonScriptScope.ClientVisual,
            "def on_click():\n    pass",
            entryPoints: [entry]);

    private sealed class RawFaultExecutor : IPythonScriptHandlerExecutor
    {
        public ValueTask ExecuteAsync(
            PythonScriptDefinition script,
            ScriptEventEnvelope scriptEvent,
            ScriptExecutionLease lease) =>
            throw new InvalidOperationException(
                "password=super-secret\nhost=/private/path");
    }

    private sealed class SanitizedFaultExecutor : IPythonScriptHandlerExecutor
    {
        public ValueTask ExecuteAsync(
            PythonScriptDefinition script,
            ScriptEventEnvelope scriptEvent,
            ScriptExecutionLease lease) =>
            throw new ScriptExecutionDiagnosticException(
                "line 4: invalid visual value");
    }
}
