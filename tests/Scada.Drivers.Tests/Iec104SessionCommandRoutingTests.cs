using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104SessionCommandRoutingTests
{
    [Fact]
    public async Task RunningSession_RoutesCommandResponsesThroughSingleReadLoop()
    {
        var adapter = new InteractiveFakeAdapter();
        var runner = new Iec104ClientSessionRunner(
            adapter,
            "127.0.0.1",
            2404,
            new Iec104SessionOptions(),
            TimeZoneInfo.Utc,
            new ushort[] { 1 },
            commandOptions: new Iec104CommandExecutionOptions
            {
                ConfirmationTimeout = TimeSpan.FromSeconds(2),
                CompletionTimeout = TimeSpan.FromSeconds(2),
                MaxConcurrentCommands = 4
            });
        using var sessionCts = new CancellationTokenSource();

        var runTask = runner.RunAsync(static (_, _) => ValueTask.CompletedTask, sessionCts.Token);

        var gi = await adapter.NextSentAsync();
        Assert.Equal(Iec104TypeId.CIcNa1, gi.Header.TypeId);
        await adapter.PublishAsync(CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause));
        await adapter.PublishAsync(CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationTerminationCause));

        var transaction = Iec104CommandTransaction.Single(1, 900, true, Iec104CommandMode.DirectOperate);
        var commandTask = runner.ExecuteCommandAsync(transaction);
        var commandRequest = await adapter.NextSentAsync();

        Assert.Equal(Iec104TypeId.CScNa1, commandRequest.Header.TypeId);
        await adapter.PublishAsync(CreateCommandResponse(
            transaction,
            commandRequest,
            Iec104CommandTransaction.ActivationConfirmationCause));
        await adapter.PublishAsync(CreateCommandResponse(
            transaction,
            commandRequest,
            Iec104CommandTransaction.ActivationTerminationCause));

        var result = await commandTask;

        Assert.Equal(Iec104CommandOutcome.Completed, result.Outcome);
        Assert.True(result.ExecuteWasTransmitted);
        Assert.True(result.WasAccepted);
        Assert.Equal(0, runner.InFlightCommandCount);

        sessionCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
        Assert.Equal(Iec104SessionState.Stopped, runner.State);
    }

    [Fact]
    public async Task CommandBeforeSessionIsRunning_IsRejectedWithoutTransmission()
    {
        var adapter = new InteractiveFakeAdapter();
        var runner = new Iec104ClientSessionRunner(
            adapter,
            "127.0.0.1",
            2404,
            new Iec104SessionOptions(),
            TimeZoneInfo.Utc,
            new ushort[] { 1 });
        var transaction = Iec104CommandTransaction.Single(1, 901, false, Iec104CommandMode.DirectOperate);

        var result = await runner.ExecuteCommandAsync(transaction);

        Assert.Equal(Iec104CommandOutcome.Rejected, result.Outcome);
        Assert.False(result.ExecuteWasTransmitted);
        Assert.Equal(0, adapter.SentCount);
    }

    private static Iec104AsduEnvelope CreateGiResponse(byte cause)
    {
        var header = new Iec104AsduHeader(
            Iec104TypeId.CIcNa1,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(cause),
            CommonAddress: 1);
        return Iec104AsduEnvelope.Create(
            header,
            new byte[] { 0, 0, 0, Iec104GeneralInterrogationTransaction.GlobalQoi });
    }

    private static Iec104AsduEnvelope CreateCommandResponse(
        Iec104CommandTransaction transaction,
        Iec104AsduEnvelope request,
        byte cause)
    {
        var header = new Iec104AsduHeader(
            transaction.TypeId,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(cause, transaction.OriginatorAddress),
            transaction.CommonAddress);
        return Iec104AsduEnvelope.Create(header, request.Payload.Span);
    }

    private sealed class InteractiveFakeAdapter : IIec104ClientAdapter
    {
        private readonly Channel<Iec104AsduEnvelope> _incoming = Channel.CreateUnbounded<Iec104AsduEnvelope>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        private readonly ConcurrentQueue<Iec104AsduEnvelope> _sent = new();
        private readonly SemaphoreSlim _sentSignal = new(0);

        public bool IsConnected { get; private set; }
        public int SentCount => _sent.Count;

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
            await foreach (var asdu in _incoming.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                yield return asdu;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            _incoming.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _incoming.Writer.TryComplete();
            _sentSignal.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync(Iec104AsduEnvelope asdu) => _incoming.Writer.WriteAsync(asdu);

        public async Task<Iec104AsduEnvelope> NextSentAsync(CancellationToken cancellationToken = default)
        {
            await _sentSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!_sent.TryDequeue(out var asdu))
                throw new InvalidOperationException("IEC-104 interactive fake adapter signaled a send without a queued ASDU.");
            return asdu;
        }
    }
}
