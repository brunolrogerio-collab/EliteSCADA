using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Tests;

public sealed class Dnp3LifecycleTests
{
    [Fact]
    public async Task CancellingStartCallerTokenAfterSuccessfulStart_DoesNotCancelRuntimeLifetime()
    {
        var tag = TagDefinition.Create("BI", "DNP3.BI", TagDataType.Boolean, source: "dnp3", readOnly: true);
        var point = new Dnp3Point(tag, new Dnp3PointBinding(Dnp3PointKind.BinaryInput, 1, TagDataType.Boolean));
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var session = new CapturingSession();
        await using var driver = new Dnp3Driver("dnp3-lifecycle", "DNP3 Lifecycle", cache, registry, [point], session);
        using var startCaller = new CancellationTokenSource();

        await driver.StartAsync(startCaller.Token);
        Assert.Equal(Dnp3SessionState.Online, session.State);
        Assert.False(session.RuntimeToken.IsCancellationRequested);

        startCaller.Cancel();

        Assert.False(session.RuntimeToken.IsCancellationRequested);
        Assert.Equal(Dnp3SessionState.Online, session.State);

        await driver.StopAsync();
        Assert.True(session.RuntimeToken.IsCancellationRequested);
        Assert.Equal(Dnp3SessionState.Stopped, session.State);
    }

    private sealed class CapturingSession : IDnp3MasterSession
    {
        public Dnp3SessionState State { get; private set; } = Dnp3SessionState.Stopped;
        public CancellationToken RuntimeToken { get; private set; }

        public async ValueTask StartAsync(
            Dnp3AssociationOptions options,
            Func<Dnp3Measurement, CancellationToken, ValueTask> measurementHandler,
            Func<Dnp3SessionState, CancellationToken, ValueTask> stateHandler,
            CancellationToken cancellationToken = default)
        {
            options.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            RuntimeToken = cancellationToken;
            State = Dnp3SessionState.Online;
            await stateHandler(State, cancellationToken);
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = Dnp3SessionState.Stopped;
            return ValueTask.CompletedTask;
        }

        public ValueTask<Dnp3CommandResult> ExecuteBinaryAsync(
            ushort index,
            Dnp3BinaryOperation operation,
            Dnp3BinaryCommandProfile profile,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<Dnp3CommandResult> ExecuteAnalogAsync(
            ushort index,
            object value,
            Dnp3AnalogCommandProfile profile,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Dnp3SessionDiagnosticSnapshot GetDiagnostics() => new(
            Endpoint: "127.0.0.1:20000",
            State,
            StateChangedAt: DateTimeOffset.UtcNow,
            LastSuccessfulCommunicationAt: State == Dnp3SessionState.Online ? DateTimeOffset.UtcNow : null);

        public ValueTask DisposeAsync()
        {
            State = Dnp3SessionState.Stopped;
            return ValueTask.CompletedTask;
        }
    }
}
