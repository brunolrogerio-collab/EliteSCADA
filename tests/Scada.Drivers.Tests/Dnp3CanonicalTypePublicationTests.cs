using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Tests;

public sealed class Dnp3CanonicalTypePublicationTests
{
    [Fact]
    public async Task G30V1_Int32Measurement_PublishesInt32WithoutNumericWidening()
    {
        var tag = TagDefinition.Create(
            "AI0",
            "DNP3.AI0",
            TagDataType.Int32,
            source: "dnp3",
            readOnly: true);
        var point = new Dnp3Point(
            tag,
            new Dnp3PointBinding(
                Dnp3PointKind.AnalogInput,
                0,
                TagDataType.Int32,
                StaticVariation: new Dnp3ObjectVariation(30, 1)));

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var session = new EmittingSession();
        await using var driver = new Dnp3Driver(
            "dnp3-canonical-type-test",
            "DNP3 Canonical Type Test",
            cache,
            registry,
            [point],
            session);

        await driver.StartAsync();
        await session.EmitAsync(new Dnp3Measurement(
            Dnp3PointKind.AnalogInput,
            0,
            4242,
            new Dnp3ObjectVariation(30, 1),
            IsEvent: false,
            Dnp3PointFlagSet.Nominal));

        Assert.True(cache.TryGet(tag.Id, out var sample));
        Assert.NotNull(sample);
        Assert.Equal(TagQuality.Good, sample.Quality);
        var published = Assert.IsType<int>(sample.Value);
        Assert.Equal(4242, published);
    }

    private sealed class EmittingSession : IDnp3MasterSession
    {
        private Func<Dnp3Measurement, CancellationToken, ValueTask>? _measurementHandler;
        private Func<Dnp3SessionState, CancellationToken, ValueTask>? _stateHandler;

        public Dnp3SessionState State { get; private set; } = Dnp3SessionState.Stopped;

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
            State = Dnp3SessionState.Online;
            await stateHandler(State, cancellationToken);
        }

        public async ValueTask EmitAsync(Dnp3Measurement measurement, CancellationToken cancellationToken = default)
        {
            var handler = _measurementHandler ?? throw new InvalidOperationException("Session has not been started.");
            await handler(measurement, cancellationToken);
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = Dnp3SessionState.Stopped;
            if (_stateHandler is not null)
                await _stateHandler(State, cancellationToken);
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
            LastSuccessfulCommunicationAt: State == Dnp3SessionState.Online ? DateTimeOffset.UtcNow : null,
            StartupIntegrityScans: 1);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
