using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104CommandCoordinatorTests
{
    [Fact]
    public async Task DirectOperate_CompletesAfterPositiveConfirmationAndTermination()
    {
        var adapter = new FakeCommandAdapter();
        using var coordinator = CreateCoordinator(adapter);
        var transaction = Iec104CommandTransaction.Single(1, 100, true, Iec104CommandMode.DirectOperate);

        var execution = coordinator.ExecuteAsync(transaction);
        var execute = await adapter.NextSentAsync();

        Assert.Equal(Iec104TypeId.CScNa1, execute.Header.TypeId);
        Assert.Equal(0, execute.Payload.Span[3] & 0x80);

        Assert.True(coordinator.TryObserveResponse(CreateResponse(transaction, execute, Iec104CommandTransaction.ActivationConfirmationCause)));
        Assert.True(coordinator.TryObserveResponse(CreateResponse(transaction, execute, Iec104CommandTransaction.ActivationTerminationCause)));

        var result = await execution;

        Assert.Equal(Iec104CommandOutcome.Completed, result.Outcome);
        Assert.Equal(Iec104CommandState.Completed, result.ProtocolState);
        Assert.True(result.ExecuteWasTransmitted);
        Assert.True(result.WasAccepted);
        Assert.Equal(0, coordinator.InFlightCount);
    }

    [Fact]
    public async Task SelectBeforeOperate_SendsExecuteOnlyAfterPositiveSelectionConfirmation()
    {
        var adapter = new FakeCommandAdapter();
        using var coordinator = CreateCoordinator(adapter);
        var transaction = Iec104CommandTransaction.Single(1, 101, false, Iec104CommandMode.SelectBeforeOperate);

        var execution = coordinator.ExecuteAsync(transaction);
        var select = await adapter.NextSentAsync();

        Assert.NotEqual(0, select.Payload.Span[3] & 0x80);
        Assert.True(coordinator.TryObserveResponse(CreateResponse(transaction, select, Iec104CommandTransaction.ActivationConfirmationCause)));

        var execute = await adapter.NextSentAsync();
        Assert.Equal(0, execute.Payload.Span[3] & 0x80);

        Assert.True(coordinator.TryObserveResponse(CreateResponse(transaction, execute, Iec104CommandTransaction.ActivationConfirmationCause)));
        Assert.True(coordinator.TryObserveResponse(CreateResponse(transaction, execute, Iec104CommandTransaction.ActivationTerminationCause)));

        var result = await execution;

        Assert.Equal(Iec104CommandOutcome.Completed, result.Outcome);
        Assert.True(result.ExecuteWasTransmitted);
        Assert.True(result.WasAccepted);
    }

    [Fact]
    public async Task NegativeActivationConfirmation_IsRejected()
    {
        var adapter = new FakeCommandAdapter();
        using var coordinator = CreateCoordinator(adapter);
        var transaction = Iec104CommandTransaction.Double(1, 102, Iec104DoublePointState.On, Iec104CommandMode.DirectOperate);

        var execution = coordinator.ExecuteAsync(transaction);
        var execute = await adapter.NextSentAsync();

        Assert.True(coordinator.TryObserveResponse(CreateResponse(
            transaction,
            execute,
            Iec104CommandTransaction.ActivationConfirmationCause,
            negative: true)));

        var result = await execution;

        Assert.Equal(Iec104CommandOutcome.Rejected, result.Outcome);
        Assert.True(result.ExecuteWasTransmitted);
        Assert.False(result.WasAccepted);
    }

    [Fact]
    public async Task MissingExecuteConfirmation_IsAmbiguousNotTimedOut()
    {
        var adapter = new FakeCommandAdapter();
        using var coordinator = new Iec104CommandCoordinator(adapter, new Iec104CommandExecutionOptions
        {
            ConfirmationTimeout = TimeSpan.FromMilliseconds(50),
            CompletionTimeout = TimeSpan.FromMilliseconds(50),
            MaxConcurrentCommands = 4
        });
        var transaction = Iec104CommandTransaction.Single(1, 103, true, Iec104CommandMode.DirectOperate);

        var execution = coordinator.ExecuteAsync(transaction);
        _ = await adapter.NextSentAsync();

        var result = await execution;

        Assert.Equal(Iec104CommandOutcome.Ambiguous, result.Outcome);
        Assert.True(result.ExecuteWasTransmitted);
        Assert.False(result.WasAccepted);
    }

    [Fact]
    public async Task SamePointCannotHaveTwoInflightCommands()
    {
        var adapter = new FakeCommandAdapter();
        using var coordinator = CreateCoordinator(adapter);
        var first = Iec104CommandTransaction.Single(7, 55, true, Iec104CommandMode.DirectOperate);
        var second = Iec104CommandTransaction.Single(7, 55, false, Iec104CommandMode.DirectOperate);

        var firstExecution = coordinator.ExecuteAsync(first);
        var firstRequest = await adapter.NextSentAsync();

        var secondResult = await coordinator.ExecuteAsync(second);

        Assert.Equal(Iec104CommandOutcome.Rejected, secondResult.Outcome);
        Assert.False(secondResult.ExecuteWasTransmitted);
        Assert.Equal(1, coordinator.InFlightCount);

        Assert.True(coordinator.TryObserveResponse(CreateResponse(
            first,
            firstRequest,
            Iec104CommandTransaction.ActivationConfirmationCause,
            negative: true)));
        _ = await firstExecution;
    }

    [Fact]
    public async Task GlobalCommandLimitRejectsInsteadOfQueueing()
    {
        var adapter = new FakeCommandAdapter();
        using var coordinator = new Iec104CommandCoordinator(adapter, new Iec104CommandExecutionOptions
        {
            ConfirmationTimeout = TimeSpan.FromSeconds(2),
            CompletionTimeout = TimeSpan.FromSeconds(2),
            MaxConcurrentCommands = 1
        });
        var first = Iec104CommandTransaction.Single(1, 1, true, Iec104CommandMode.DirectOperate);
        var second = Iec104CommandTransaction.Single(1, 2, true, Iec104CommandMode.DirectOperate);

        var firstExecution = coordinator.ExecuteAsync(first);
        var firstRequest = await adapter.NextSentAsync();

        var secondResult = await coordinator.ExecuteAsync(second);

        Assert.Equal(Iec104CommandOutcome.Rejected, secondResult.Outcome);
        Assert.False(secondResult.ExecuteWasTransmitted);
        Assert.Equal(1, coordinator.InFlightCount);

        Assert.True(coordinator.TryObserveResponse(CreateResponse(
            first,
            firstRequest,
            Iec104CommandTransaction.ActivationConfirmationCause,
            negative: true)));
        _ = await firstExecution;
    }

    [Fact]
    public async Task SessionFailureAfterAcceptance_IsAmbiguous()
    {
        var adapter = new FakeCommandAdapter();
        using var coordinator = CreateCoordinator(adapter);
        var transaction = Iec104CommandTransaction.ShortFloatSetpoint(3, 400, 12.5f, Iec104CommandMode.DirectOperate);

        var execution = coordinator.ExecuteAsync(transaction);
        var execute = await adapter.NextSentAsync();

        Assert.True(coordinator.TryObserveResponse(CreateResponse(
            transaction,
            execute,
            Iec104CommandTransaction.ActivationConfirmationCause)));
        coordinator.FailAll(new IOException("simulated link loss"));

        var result = await execution;

        Assert.Equal(Iec104CommandOutcome.Ambiguous, result.Outcome);
        Assert.True(result.ExecuteWasTransmitted);
        Assert.True(result.WasAccepted);
    }

    private static Iec104CommandCoordinator CreateCoordinator(FakeCommandAdapter adapter) =>
        new(adapter, new Iec104CommandExecutionOptions
        {
            ConfirmationTimeout = TimeSpan.FromSeconds(2),
            CompletionTimeout = TimeSpan.FromSeconds(2),
            MaxConcurrentCommands = 4
        });

    private static Iec104AsduEnvelope CreateResponse(
        Iec104CommandTransaction transaction,
        Iec104AsduEnvelope request,
        byte cause,
        bool negative = false)
    {
        var header = new Iec104AsduHeader(
            transaction.TypeId,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(cause, transaction.OriginatorAddress, negative),
            transaction.CommonAddress);
        return Iec104AsduEnvelope.Create(header, request.Payload.Span);
    }

    private sealed class FakeCommandAdapter : IIec104ClientAdapter
    {
        private readonly ConcurrentQueue<Iec104AsduEnvelope> _sent = new();
        private readonly SemaphoreSlim _sentSignal = new(0);

        public bool IsConnected { get; private set; } = true;

        public Task ConnectAsync(string host, int port, Iec104SessionOptions options, CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task StartDataTransferAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopDataTransferAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask SendAsync(Iec104AsduEnvelope asdu, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _sent.Enqueue(asdu);
            _sentSignal.Release();
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<Iec104AsduEnvelope> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _sentSignal.Dispose();
            return ValueTask.CompletedTask;
        }

        public async Task<Iec104AsduEnvelope> NextSentAsync(CancellationToken cancellationToken = default)
        {
            await _sentSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!_sent.TryDequeue(out var asdu))
                throw new InvalidOperationException("IEC-104 fake adapter send signal was raised without a queued ASDU.");
            return asdu;
        }
    }
}
