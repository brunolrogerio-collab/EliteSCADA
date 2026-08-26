using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class ScriptRuntimeDiagnosticsInvariantTests
{
    [Fact]
    public void InvalidNegativeDuration_DoesNotMutateDiagnosticsCounters()
    {
        var scriptId = Guid.NewGuid();
        const string runtimeId = "runtime-1";
        var policy = new ScriptExecutionPolicy(
            TimeSpan.FromMilliseconds(200),
            maxQueuedEvents: 4,
            minimumTimerInterval: TimeSpan.FromMilliseconds(50),
            maxConsecutiveFailuresBeforeThrottle: 2);
        var diagnostics = new ScriptRuntimeDiagnosticsTracker(
            scriptId,
            runtimeId,
            policy);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            diagnostics.RecordExecution(
                new ScriptExecutionResult(
                    scriptId,
                    runtimeId,
                    "handler",
                    ScriptExecutionStatus.Faulted,
                    TimeSpan.FromMilliseconds(-1),
                    DateTimeOffset.UtcNow)));

        var snapshot = diagnostics.Snapshot(
            activeSubscriptions: 0,
            queuedEvents: 0);

        Assert.Equal(0L, snapshot.ExecutionCount);
        Assert.Equal(0L, snapshot.FaultedCount);
        Assert.Equal(0, snapshot.ConsecutiveFailures);
        Assert.False(snapshot.IsThrottled);
        Assert.Null(snapshot.LastStatus);
        Assert.Null(snapshot.LastCompletedAt);
    }
}
