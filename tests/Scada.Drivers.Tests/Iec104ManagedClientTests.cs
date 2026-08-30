using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104ManagedClientTests
{
    [Fact]
    public async Task ActiveManagedSession_ExecutesCommandThroughCurrentReadySession()
    {
        var adapter = new InteractiveAdapter();
        var client = CreateClient(() => adapter);
        using var cts = new CancellationTokenSource();

        var runTask = client.RunAsync(static (_, _) => ValueTask.CompletedTask, cancellationToken: cts.Token);
        var gi = await adapter.NextSentAsync();
        Assert.Equal(Iec104TypeId.CIcNa1, gi.Header.TypeId);
        await adapter.PublishAsync(CreateGeneralInterrogationResponse(gi, Iec104GeneralInterrogationTransaction.ActivationConfirmationCause));
        await adapter.PublishAsync(CreateGeneralInterrogationResponse(gi, Iec104GeneralInterrogationTransaction.ActivationTerminationCause));
        await WaitUntilAsync(() => client.GetReadiness().State == Iec104ReadinessState.Ready);

        var transaction = Iec104CommandTransaction.Single(1, 500, true, Iec104CommandMode.DirectOperate);
        var commandTask = client.ExecuteCommandAsync(transaction);
        var command = await adapter.NextSentAsync();

        Assert.Equal(Iec104TypeId.CScNa1, command.Header.TypeId);
        await adapter.PublishAsync(CreateCommandResponse(transaction, command, Iec104CommandTransaction.ActivationConfirmationCause));
        await adapter.PublishAsync(CreateCommandResponse(transaction, command, Iec104CommandTransaction.ActivationTerminationCause));

        var result = await commandTask;

        Assert.Equal(Iec104CommandOutcome.Completed, result.Outcome);
        Assert.True(result.ExecuteWasTransmitted);
        Assert.True(result.WasAccepted);
        Assert.Equal(Iec104SessionState.Running, client.SessionState);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
        Assert.Equal(Iec104SessionState.Stopped, client.SessionState);
        Assert.Equal(Iec104ReadinessState.Stopped, client.GetReadiness().State);
    }

    [Fact]
    public async Task CommandDuringStartupBeforeGeneralInterrogationCompletes_IsRejectedWithoutTransmission()
    {
        var adapter = new InteractiveAdapter();
        var client = CreateClient(() => adapter);
        using var cts = new CancellationTokenSource();

        var runTask = client.RunAsync(static (_, _) => ValueTask.CompletedTask, cancellationToken: cts.Token);
        var gi = await adapter.NextSentAsync();
        Assert.Equal(Iec104TypeId.CIcNa1, gi.Header.TypeId);
        Assert.Equal(Iec104ReadinessState.Starting, client.GetReadiness().State);

        var transaction = Iec104CommandTransaction.Single(1, 503, true, Iec104CommandMode.DirectOperate);
        var result = await client.ExecuteCommandAsync(transaction);

        Assert.Equal(Iec104CommandOutcome.Rejected, result.Outcome);
        Assert.False(result.ExecuteWasTransmitted);
        Assert.Contains("startup is not Ready", result.Detail ?? string.Empty);
        Assert.Equal(1, adapter.TotalSent);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    [Fact]
    public async Task RejectedStartupGeneralInterrogation_FaultsReadinessAndBlocksCommand()
    {
        var adapter = new InteractiveAdapter();
        var client = CreateClient(() => adapter);
        using var cts = new CancellationTokenSource();

        var runTask = client.RunAsync(static (_, _) => ValueTask.CompletedTask, cancellationToken: cts.Token);
        var gi = await adapter.NextSentAsync();
        await adapter.PublishAsync(CreateGeneralInterrogationResponse(
            gi,
            Iec104GeneralInterrogationTransaction.ActivationConfirmationCause,
            negative: true));
        await WaitUntilAsync(() => client.GetReadiness().State == Iec104ReadinessState.Faulted);

        var readiness = client.GetReadiness();
        Assert.True(readiness.IsTransportConnected);
        Assert.True(readiness.IsDataTransferStarted);
        Assert.False(readiness.StartupGeneralInterrogationCompleted);
        Assert.True(readiness.StartupGeneralInterrogationRejected);
        Assert.Equal(Iec104GeneralInterrogationState.Rejected, readiness.GeneralInterrogationStates[1]);

        var transaction = Iec104CommandTransaction.Single(1, 504, true, Iec104CommandMode.DirectOperate);
        var result = await client.ExecuteCommandAsync(transaction);

        Assert.Equal(Iec104CommandOutcome.Rejected, result.Outcome);
        Assert.False(result.ExecuteWasTransmitted);
        Assert.Equal(1, adapter.TotalSent);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    [Fact]
    public async Task CommandDuringReconnectDelay_IsRejectedAndNeverReplayed()
    {
        var first = new InteractiveAdapter();
        var second = new InteractiveAdapter();
        var adapters = new ConcurrentQueue<InteractiveAdapter>(new[] { first, second });
        var delayEntered = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelay = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = CreateClient(
            () => adapters.TryDequeue(out var adapter)
                ? adapter
                : throw new InvalidOperationException("No IEC-104 test adapter remains."),
            async (delay, cancellationToken) =>
            {
                delayEntered.TrySetResult(delay);
                await releaseDelay.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            });
        using var cts = new CancellationTokenSource();

        var runTask = client.RunAsync(static (_, _) => ValueTask.CompletedTask, cancellationToken: cts.Token);
        var firstGi = await first.NextSentAsync();
        Assert.Equal(Iec104TypeId.CIcNa1, firstGi.Header.TypeId);

        first.FailRead(new IOException("synthetic IEC-104 link loss"));
        var reconnectDelay = await delayEntered.Task;
        Assert.Equal(TimeSpan.FromSeconds(1), reconnectDelay);
        Assert.Equal(Iec104SessionState.Stopped, client.SessionState);
        Assert.Equal(Iec104ReadinessState.Faulted, client.GetReadiness().State);

        var transaction = Iec104CommandTransaction.Single(1, 501, false, Iec104CommandMode.DirectOperate);
        var rejected = await client.ExecuteCommandAsync(transaction);

        Assert.Equal(Iec104CommandOutcome.Rejected, rejected.Outcome);
        Assert.False(rejected.ExecuteWasTransmitted);

        releaseDelay.TrySetResult(true);
        var secondGi = await second.NextSentAsync();
        Assert.Equal(Iec104TypeId.CIcNa1, secondGi.Header.TypeId);
        Assert.Equal(Iec104ReadinessState.Starting, client.GetReadiness().State);
        Assert.Equal(1, second.TotalSent);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    [Fact]
    public async Task CommandWithoutManagedRun_IsRejectedWithoutCreatingAdapter()
    {
        var factoryCalls = 0;
        var client = CreateClient(() =>
        {
            factoryCalls++;
            return new InteractiveAdapter();
        });
        var transaction = Iec104CommandTransaction.Single(1, 502, true, Iec104CommandMode.DirectOperate);

        var readiness = client.GetReadiness();
        var result = await client.ExecuteCommandAsync(transaction);

        Assert.Equal(Iec104ReadinessState.NotStarted, readiness.State);
        Assert.Equal(Iec104CommandOutcome.Rejected, result.Outcome);
        Assert.False(result.ExecuteWasTransmitted);
        Assert.Equal(0, factoryCalls);
    }

    private static Iec104ManagedClient CreateClient(
        Func<IIec104ClientAdapter> factory,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null) =>
        new(
            factory,
            "127.0.0.1",
            2404,
            new Iec104SessionOptions(),
            TimeZoneInfo.Utc,
            new ushort[] { 1 },
            reconnectPolicy: new Iec104ReconnectPolicy
            {
                Delays = new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) },
                StableSessionThreshold = TimeSpan.FromSeconds(30)
            },
            commandOptions: new Iec104CommandExecutionOptions
            {
                ConfirmationTimeout = TimeSpan.FromSeconds(2),
                CompletionTimeout = TimeSpan.FromSeconds(2),
                MaxConcurrentCommands = 4
            },
            delayAsync: delayAsync);

    private static Iec104AsduEnvelope CreateGeneralInterrogationResponse(
        Iec104AsduEnvelope request,
        byte cause,
        bool negative = false)
    {
        var requestCause = request.Header.CauseOfTransmission;
        var header = new Iec104AsduHeader(
            Iec104TypeId.CIcNa1,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(
                cause,
                requestCause.OriginatorAddress,
                isNegativeConfirmation: negative),
            request.Header.CommonAddress);
        return Iec104AsduEnvelope.Create(header, request.Payload.Span);
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

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!predicate())
            await Task.Delay(10, timeout.Token);
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
            _incoming.Writer.TryComplete();
            _sentSignal.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync(Iec104AsduEnvelope asdu) => _incoming.Writer.WriteAsync(asdu);

        public void FailRead(Exception failure) => _incoming.Writer.TryComplete(failure);

        public async Task<Iec104AsduEnvelope> NextSentAsync(CancellationToken cancellationToken = default)
        {
            await _sentSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!_sent.TryDequeue(out var asdu))
                throw new InvalidOperationException("IEC-104 managed-client fake adapter signaled a send without a queued ASDU.");
            return asdu;
        }
    }
}
