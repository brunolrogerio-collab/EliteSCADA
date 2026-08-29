using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Tests;

public sealed class Dnp3DriverTests
{
    [Fact]
    public async Task StartAndMeasurement_PublishCanonicalTagValueWithSourceTimestamp()
    {
        var tag = TagDefinition.Create("BI1", "DNP3.BI1", TagDataType.Boolean, source: "dnp3", readOnly: true);
        var point = new Dnp3Point(
            tag,
            new Dnp3PointBinding(
                Dnp3PointKind.BinaryInput,
                10,
                TagDataType.Boolean,
                new Dnp3ObjectVariation(1, 2),
                new Dnp3ObjectVariation(2, 2)));

        var (driver, session, cache, registry) = CreateDriver(point);
        await driver.StartAsync();

        var sourceTimestamp = new DateTimeOffset(2026, 8, 29, 14, 30, 0, TimeSpan.Zero);
        await session.EmitAsync(new Dnp3Measurement(
            Dnp3PointKind.BinaryInput,
            10,
            true,
            new Dnp3ObjectVariation(2, 2),
            IsEvent: true,
            Dnp3PointFlagSet.Nominal,
            sourceTimestamp));

        Assert.True(registry.TryGet(tag.Id, out var registered));
        Assert.Equal(tag, registered);
        Assert.True(cache.TryGet(tag.Id, out var value));
        Assert.NotNull(value);
        Assert.Equal(true, value.Value);
        Assert.Equal(TagQuality.Good, value.Quality);
        Assert.Equal(sourceTimestamp, value.SourceTimestamp);
        Assert.Equal("dnp3-test", value.Source);

        await driver.DisposeAsync();
    }

    [Fact]
    public async Task DoubleBitBinary_PreservesFourStateMeaningAsCanonicalEnumText()
    {
        var tag = TagDefinition.Create("DBI", "DNP3.DBI", TagDataType.Enum, source: "dnp3", readOnly: true);
        var point = new Dnp3Point(
            tag,
            new Dnp3PointBinding(Dnp3PointKind.DoubleBitBinaryInput, 4, TagDataType.Enum, new Dnp3ObjectVariation(3, 2)));

        var (driver, session, cache, _) = CreateDriver(point);
        await driver.StartAsync();

        await session.EmitAsync(new Dnp3Measurement(
            Dnp3PointKind.DoubleBitBinaryInput,
            4,
            Dnp3DoubleBitState.Indeterminate,
            new Dnp3ObjectVariation(3, 2),
            IsEvent: false,
            Dnp3PointFlagSet.Nominal));

        Assert.True(cache.TryGet(tag.Id, out var value));
        Assert.Equal("Indeterminate", value?.Value);

        await driver.DisposeAsync();
    }

    [Fact]
    public async Task IncompatibleReceivedVariation_FailsConfiguredPointQualityWithoutCrashingSession()
    {
        var tag = TagDefinition.Create("AI", "DNP3.AI", TagDataType.Int32, source: "dnp3", readOnly: true);
        var point = new Dnp3Point(
            tag,
            new Dnp3PointBinding(Dnp3PointKind.AnalogInput, 7, TagDataType.Int32, new Dnp3ObjectVariation(30, 1)));

        var (driver, session, cache, _) = CreateDriver(point);
        await driver.StartAsync();

        await session.EmitAsync(new Dnp3Measurement(
            Dnp3PointKind.AnalogInput,
            7,
            42,
            new Dnp3ObjectVariation(2, 2),
            IsEvent: false,
            Dnp3PointFlagSet.Nominal));

        Assert.True(cache.TryGet(tag.Id, out var value));
        Assert.Equal(TagQuality.BadConfiguration, value?.Quality);
        Assert.Null(value?.Value);
        Assert.Equal("1", driver.GetCommunicationDiagnostics().ProtocolDetails?["rejectedMeasurements"]);

        await driver.DisposeAsync();
    }

    [Fact]
    public async Task UnconfiguredObservedPoint_IsIgnoredAndDoesNotCreateTag()
    {
        var tag = TagDefinition.Create("BI", "DNP3.BI", TagDataType.Boolean, source: "dnp3", readOnly: true);
        var point = new Dnp3Point(tag, new Dnp3PointBinding(Dnp3PointKind.BinaryInput, 1, TagDataType.Boolean));

        var (driver, session, cache, registry) = CreateDriver(point);
        await driver.StartAsync();

        await session.EmitAsync(new Dnp3Measurement(
            Dnp3PointKind.BinaryInput,
            999,
            true,
            new Dnp3ObjectVariation(1, 2),
            IsEvent: false,
            Dnp3PointFlagSet.Nominal));

        Assert.Empty(cache.Snapshot());
        Assert.Single(registry.Snapshot());
        Assert.Equal("1", driver.GetCommunicationDiagnostics().ProtocolDetails?["rejectedMeasurements"]);

        await driver.DisposeAsync();
    }

    [Fact]
    public async Task Reconnecting_MarksCurrentValueBadCommunicationAndPreservesDeviceTime()
    {
        var tag = TagDefinition.Create("BI", "DNP3.BI", TagDataType.Boolean, source: "dnp3", readOnly: true);
        var point = new Dnp3Point(tag, new Dnp3PointBinding(Dnp3PointKind.BinaryInput, 1, TagDataType.Boolean));
        var (driver, session, cache, _) = CreateDriver(point);
        await driver.StartAsync();

        var sourceTimestamp = new DateTimeOffset(2026, 8, 29, 15, 0, 0, TimeSpan.Zero);
        await session.EmitAsync(new Dnp3Measurement(
            Dnp3PointKind.BinaryInput,
            1,
            true,
            new Dnp3ObjectVariation(1, 2),
            IsEvent: false,
            Dnp3PointFlagSet.Nominal,
            sourceTimestamp));

        await session.EmitStateAsync(Dnp3SessionState.Reconnecting);

        Assert.True(cache.TryGet(tag.Id, out var sample));
        Assert.Equal(true, sample?.Value);
        Assert.Equal(TagQuality.BadCommunication, sample?.Quality);
        Assert.Equal(sourceTimestamp, sample?.SourceTimestamp);
        Assert.Equal(Scada.Drivers.Abstractions.CommunicationDriverOperationalState.Reconnecting, driver.GetCommunicationDiagnostics().State);

        await driver.DisposeAsync();
    }

    [Fact]
    public async Task SuccessfulCrob_DoesNotOptimisticallyChangeOutputFeedback()
    {
        var tag = TagDefinition.Create("BO", "DNP3.BO", TagDataType.Boolean, source: "dnp3", readOnly: false);
        var point = new Dnp3Point(
            tag,
            new Dnp3PointBinding(
                Dnp3PointKind.BinaryOutputStatus,
                3,
                TagDataType.Boolean,
                new Dnp3ObjectVariation(10, 2),
                new Dnp3ObjectVariation(11, 2),
                Writable: true),
            new Dnp3BinaryCommandProfile());

        var (driver, session, cache, _) = CreateDriver(point);
        await driver.StartAsync();
        await session.EmitAsync(new Dnp3Measurement(
            Dnp3PointKind.BinaryOutputStatus,
            3,
            false,
            new Dnp3ObjectVariation(10, 2),
            IsEvent: false,
            Dnp3PointFlagSet.Nominal));

        await driver.WriteAsync(tag.Id, true);

        Assert.Single(session.BinaryCommands);
        Assert.Equal(Dnp3BinaryOperation.LatchOn, session.BinaryCommands[0].Operation);
        Assert.True(cache.TryGet(tag.Id, out var feedback));
        Assert.Equal(false, feedback?.Value);

        await driver.DisposeAsync();
    }

    [Fact]
    public async Task WriteWhileReconnecting_IsRejectedAndNeverQueuedForReplay()
    {
        var tag = TagDefinition.Create("BO", "DNP3.BO", TagDataType.Boolean, source: "dnp3", readOnly: false);
        var point = new Dnp3Point(
            tag,
            new Dnp3PointBinding(Dnp3PointKind.BinaryOutputStatus, 8, TagDataType.Boolean, Writable: true),
            new Dnp3BinaryCommandProfile());

        var (driver, session, _, _) = CreateDriver(point);
        await driver.StartAsync();
        await session.EmitStateAsync(Dnp3SessionState.Reconnecting);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await driver.WriteAsync(tag.Id, true));
        Assert.Contains("not online", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(session.BinaryCommands);

        await driver.DisposeAsync();
    }

    [Fact]
    public async Task RejectedCommand_IsReportedAsFailure()
    {
        var tag = TagDefinition.Create("BO", "DNP3.BO", TagDataType.Boolean, source: "dnp3", readOnly: false);
        var point = new Dnp3Point(
            tag,
            new Dnp3PointBinding(Dnp3PointKind.BinaryOutputStatus, 8, TagDataType.Boolean, Writable: true),
            new Dnp3BinaryCommandProfile());

        var (driver, session, _, _) = CreateDriver(point);
        session.NextCommandResult = Dnp3CommandResult.Failure("NO_SELECT", "Outstation rejected the command.");
        await driver.StartAsync();

        var exception = await Assert.ThrowsAsync<Dnp3CommandException>(async () => await driver.WriteAsync(tag.Id, true));
        Assert.Equal("NO_SELECT", exception.CommandStatus);

        await driver.DisposeAsync();
    }

    [Fact]
    public async Task UserRequestQueue_IsBoundedAndDoesNotSilentlyQueueExtraCommand()
    {
        var tag = TagDefinition.Create("BO", "DNP3.BO", TagDataType.Boolean, source: "dnp3", readOnly: false);
        var point = new Dnp3Point(
            tag,
            new Dnp3PointBinding(Dnp3PointKind.BinaryOutputStatus, 2, TagDataType.Boolean, Writable: true),
            new Dnp3BinaryCommandProfile());

        var options = new Dnp3AssociationOptions { MaxQueuedUserRequests = 1 };
        var (driver, session, _, _) = CreateDriver(point, options);
        session.BlockCommands = true;
        await driver.StartAsync();

        var first = driver.WriteAsync(tag.Id, true).AsTask();
        await session.CommandEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await driver.WriteAsync(tag.Id, false));

        session.ReleaseBlockedCommands();
        await first;
        Assert.Single(session.BinaryCommands);

        await driver.DisposeAsync();
    }

    [Fact]
    public async Task AnalogOutput_RequiresExactCanonicalTypeAndUsesConfiguredVariation()
    {
        var tag = TagDefinition.Create("AO", "DNP3.AO", TagDataType.Double, source: "dnp3", readOnly: false);
        var profile = new Dnp3AnalogCommandProfile(Dnp3CommandMode.SelectBeforeOperate, Dnp3AnalogOutputVariation.Float64);
        var point = new Dnp3Point(
            tag,
            new Dnp3PointBinding(Dnp3PointKind.AnalogOutputStatus, 6, TagDataType.Double, Writable: true),
            AnalogCommandProfile: profile);

        var (driver, session, _, _) = CreateDriver(point);
        await driver.StartAsync();

        await Assert.ThrowsAsync<ArgumentException>(async () => await driver.WriteAsync(tag.Id, 12.5f));
        await driver.WriteAsync(tag.Id, 12.5d);

        Assert.Single(session.AnalogCommands);
        Assert.Equal(12.5d, session.AnalogCommands[0].Value);
        Assert.Equal(Dnp3AnalogOutputVariation.Float64, session.AnalogCommands[0].Profile.Variation);

        await driver.DisposeAsync();
    }

    [Fact]
    public async Task StopFailure_CleansLifecycleStateAndAllowsExplicitRestart()
    {
        var tag = TagDefinition.Create("BI", "DNP3.BI", TagDataType.Boolean, source: "dnp3", readOnly: true);
        var point = new Dnp3Point(tag, new Dnp3PointBinding(Dnp3PointKind.BinaryInput, 1, TagDataType.Boolean));
        var (driver, session, _, _) = CreateDriver(point);
        await driver.StartAsync();

        session.StopException = new IOException("simulated stop failure");
        await Assert.ThrowsAsync<IOException>(async () => await driver.StopAsync());
        Assert.Equal(Scada.Drivers.Abstractions.DriverState.Faulted, driver.Status.State);

        session.StopException = null;
        await driver.StartAsync();
        Assert.Equal(Scada.Drivers.Abstractions.DriverState.Running, driver.Status.State);

        await driver.DisposeAsync();
    }

    private static (Dnp3Driver Driver, FakeDnp3MasterSession Session, CurrentTagCache Cache, InMemoryTagRegistry Registry) CreateDriver(
        Dnp3Point point,
        Dnp3AssociationOptions? options = null)
    {
        var eventBus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(eventBus);
        var registry = new InMemoryTagRegistry();
        var session = new FakeDnp3MasterSession();
        var driver = new Dnp3Driver("dnp3-test", "DNP3 Test", cache, registry, [point], session, options);
        return (driver, session, cache, registry);
    }

    private sealed class FakeDnp3MasterSession : IDnp3MasterSession
    {
        private Func<Dnp3Measurement, CancellationToken, ValueTask>? _measurementHandler;
        private Func<Dnp3SessionState, CancellationToken, ValueTask>? _stateHandler;
        private TaskCompletionSource<bool>? _commandRelease;

        public Dnp3SessionState State { get; private set; } = Dnp3SessionState.Stopped;
        public Dnp3CommandResult NextCommandResult { get; set; } = Dnp3CommandResult.Success();
        public Exception? StopException { get; set; }
        public bool BlockCommands { get; set; }
        public TaskCompletionSource<bool> CommandEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<(ushort Index, Dnp3BinaryOperation Operation, Dnp3BinaryCommandProfile Profile)> BinaryCommands { get; } = [];
        public List<(ushort Index, object Value, Dnp3AnalogCommandProfile Profile)> AnalogCommands { get; } = [];

        public async ValueTask StartAsync(
            Dnp3AssociationOptions options,
            Func<Dnp3Measurement, CancellationToken, ValueTask> measurementHandler,
            Func<Dnp3SessionState, CancellationToken, ValueTask> stateHandler,
            CancellationToken cancellationToken = default)
        {
            options.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            _measurementHandler = measurementHandler;
            _stateHandler = stateHandler;
            await SetStateAsync(Dnp3SessionState.Online, cancellationToken);
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (StopException is not null) throw StopException;
            await SetStateAsync(Dnp3SessionState.Stopped, cancellationToken);
        }

        public async ValueTask<Dnp3CommandResult> ExecuteBinaryAsync(
            ushort index,
            Dnp3BinaryOperation operation,
            Dnp3BinaryCommandProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (State != Dnp3SessionState.Online)
                throw new InvalidOperationException("Fake association is not online.");
            BinaryCommands.Add((index, operation, profile));
            if (BlockCommands)
            {
                _commandRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                CommandEntered.TrySetResult(true);
                await _commandRelease.Task.WaitAsync(cancellationToken);
            }
            return NextCommandResult;
        }

        public ValueTask<Dnp3CommandResult> ExecuteAnalogAsync(
            ushort index,
            object value,
            Dnp3AnalogCommandProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (State != Dnp3SessionState.Online)
                throw new InvalidOperationException("Fake association is not online.");
            AnalogCommands.Add((index, value, profile));
            return ValueTask.FromResult(NextCommandResult);
        }

        public Dnp3SessionDiagnosticSnapshot GetDiagnostics() => new(
            Endpoint: "127.0.0.1:20000",
            State,
            StateChangedAt: DateTimeOffset.UtcNow,
            LastSuccessfulCommunicationAt: State == Dnp3SessionState.Online ? DateTimeOffset.UtcNow : null,
            Requests: BinaryCommands.Count + AnalogCommands.Count,
            SuccessfulOperations: BinaryCommands.Count + AnalogCommands.Count,
            WriteOperations: BinaryCommands.Count + AnalogCommands.Count,
            Connections: State == Dnp3SessionState.Online ? 1 : 0);

        public async ValueTask EmitAsync(Dnp3Measurement measurement)
        {
            var handler = _measurementHandler ?? throw new InvalidOperationException("Session has not been started.");
            await handler(measurement, CancellationToken.None);
        }

        public ValueTask EmitStateAsync(Dnp3SessionState state) => SetStateAsync(state, CancellationToken.None);

        private async ValueTask SetStateAsync(Dnp3SessionState state, CancellationToken cancellationToken)
        {
            State = state;
            if (_stateHandler is not null)
                await _stateHandler(state, cancellationToken);
        }

        public void ReleaseBlockedCommands() => _commandRelease?.TrySetResult(true);

        public ValueTask DisposeAsync()
        {
            State = Dnp3SessionState.Stopped;
            _commandRelease?.TrySetCanceled();
            return ValueTask.CompletedTask;
        }
    }
}
