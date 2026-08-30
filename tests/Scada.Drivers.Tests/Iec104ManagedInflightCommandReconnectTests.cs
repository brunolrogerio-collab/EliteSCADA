using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104ManagedInflightCommandReconnectTests
{
    [Fact]
    public async Task LinkLossAfterExecuteTransmission_IsAmbiguousAndCommandIsNotReplayedAfterReconnect()
    {
        var first = new InteractiveAdapter();
        var second = new InteractiveAdapter();
        var adapters = new ConcurrentQueue<InteractiveAdapter>(new[] { first, second });
        var delayEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelay = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var client = new Iec104ManagedClient(
            () => adapters.TryDequeue(out var adapter)
                ? adapter
                : throw new InvalidOperationException("No IEC-104 test adapter remains."),
            "127.0.0.1",
            2404,
            new Iec104SessionOptions(),
            TimeZoneInfo.Utc,
            new ushort[] { 1 },
            reconnectPolicy: new Iec104ReconnectPolicy
            {
                Delays = new[] { TimeSpan.FromSeconds(1) },
                StableSessionThreshold = TimeSpan.FromSeconds(30)
            },
            commandOptions: new Iec104CommandExecutionOptions
            {
                ConfirmationTimeout = TimeSpan.FromSeconds(2),
                CompletionTimeout = TimeSpan.FromSeconds(2),
                MaxConcurrentCommands = 4
            },
            delayAsync: async (_, cancellationToken) =>
            {
                delayEntered.TrySetResult(true);
                await releaseDelay.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            });

        var runTask = client.RunAsync(static (_, _) => ValueTask.CompletedTask, cancellationToken: cts.Token);
        var firstGi = await first.NextSentAsync(cts.Token);
        Assert.Equal(Iec104TypeId.CIcNa1, firstGi.Header.TypeId);

        var transaction = Iec104CommandTransaction.Single(
            commonAddress: 1,
            informationObjectAddress: 900,
            value: true,
            Iec104CommandMode.DirectOperate);
        var commandTask = client.ExecuteCommandAsync(transaction, cts.Token);
        var execute = await first.NextSentAsync(cts.Token);
        Assert.Equal(Iec104TypeId.CScNa1, execute.Header.TypeId);
        Assert.Equal(2, first.TotalSent);

        first.FailRead(new IOException("synthetic link loss after execute transmission"));

        var result = await commandTask;
        Assert.Equal(Iec104CommandOutcome.Ambiguous, result.Outcome);
        Assert.True(result.ExecuteWasTransmitted);
        Assert.False(result.WasAccepted);

        await delayEntered.Task.WaitAsync(cts.Token);
        Assert.Equal(Iec104SessionState.Stopped, client.SessionState);
        var diagnostics = client.GetDiagnostics();
        Assert.Equal(1, diagnostics.Commands.Requested);
        Assert.Equal(1, diagnostics.Commands.Ambiguous);
        Assert.Equal(0, diagnostics.Commands.Completed);

        releaseDelay.TrySetResult(true);
        var secondGi = await second.NextSentAsync(cts.Token);
        Assert.Equal(Iec104TypeId.CIcNa1, secondGi.Header.TypeId);
        Assert.Equal(1, second.TotalSent);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    private sealed class InteractiveAdapter : IIec104ClientAdapter
    {
        private readonly Channel<Iec104AsduEnvelope> _incoming = Channel.CreateUnbounded<Iec104AsduEnvelope>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        private readonly ConcurrentQueue<Iec104AsduEnvelope> _sent = new();
        private readonly SemaphoreSlim _sentSignal = new(0);
        private int _totalSent;

        public bool IsConnected { get; private set; }
        public int TotalSent => Volatile.Read(ref _totalSent);

        public Task ConnectAsync(
            string host,
            int port,
            Iec104SessionOptions options,
            CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task StartDataTransferAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopDataTransferAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask SendAsync(
            Iec104AsduEnvelope asdu,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _sent.Enqueue(asdu);
            Interlocked.Increment(ref _totalSent);
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
            IsConnected = false;
            _incoming.Writer.TryComplete();
            _sentSignal.Dispose();
            return ValueTask.CompletedTask;
        }

        public void FailRead(Exception failure)
        {
            IsConnected = false;
            _incoming.Writer.TryComplete(failure);
        }

        public async Task<Iec104AsduEnvelope> NextSentAsync(CancellationToken cancellationToken)
        {
            await _sentSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!_sent.TryDequeue(out var asdu))
                throw new InvalidOperationException("IEC-104 in-flight reconnect fake signaled a send without a queued ASDU.");
            return asdu;
        }
    }
}
