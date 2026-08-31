using System.Runtime.CompilerServices;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104DiagnosticsTests
{
    [Fact]
    public async Task DiagnosticsTrackRejectedCommandWithoutCreatingSession()
    {
        var factoryCalls = 0;
        var client = new Iec104ManagedClient(
            () =>
            {
                factoryCalls++;
                return new ConnectFailingAdapter(null);
            },
            "10.20.30.40",
            2404,
            new Iec104SessionOptions
            {
                T0 = TimeSpan.FromSeconds(12),
                T1 = TimeSpan.FromSeconds(6),
                T2 = TimeSpan.FromSeconds(3),
                T3 = TimeSpan.FromSeconds(15),
                K = 12,
                W = 6
            },
            TimeZoneInfo.Utc,
            new ushort[] { 9, 3, 9 });

        var before = client.GetDiagnostics();
        var result = await client.ExecuteCommandAsync(
            Iec104CommandTransaction.Single(3, 100, true, Iec104CommandMode.DirectOperate));
        var after = client.GetDiagnostics();

        Assert.Equal(Iec104CommandOutcome.Rejected, result.Outcome);
        Assert.Equal("10.20.30.40", before.Host);
        Assert.Equal(2404, before.Port);
        Assert.Equal(new ushort[] { 3, 9 }, before.CommonAddresses);
        Assert.Equal(TimeSpan.FromSeconds(12), before.T0);
        Assert.Equal(12, before.K);
        Assert.Equal(6, before.W);
        Assert.Equal(Iec104SessionState.Stopped, before.SessionState);
        Assert.Equal(0, before.Commands.Requested);
        Assert.Equal(1, after.Commands.Requested);
        Assert.Equal(1, after.Commands.Rejected);
        Assert.Equal(0, after.Commands.Ambiguous);
        Assert.Equal(0, factoryCalls);
        Assert.Equal(32, before.RuntimeInstanceId.Length);
    }

    [Fact]
    public async Task DiagnosticsSanitizeReconnectFailureAndRecordBackoffEvidence()
    {
        var adapter = new ConnectFailingAdapter(new IOException("line one\nline two\r\nline three"));
        using var cancellation = new CancellationTokenSource();
        var client = new Iec104ManagedClient(
            () => adapter,
            "127.0.0.1",
            2404,
            new Iec104SessionOptions(),
            TimeZoneInfo.Utc,
            new ushort[] { 1 },
            reconnectPolicy: new Iec104ReconnectPolicy
            {
                Delays = new[] { TimeSpan.FromSeconds(1) },
                StableSessionThreshold = TimeSpan.FromMinutes(1)
            },
            delayAsync: static (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RunAsync(
            static (_, _) => ValueTask.CompletedTask,
            (_, _) =>
            {
                cancellation.Cancel();
                return ValueTask.CompletedTask;
            },
            cancellation.Token));

        var diagnostics = client.GetDiagnostics();

        Assert.Equal(1, diagnostics.SessionFailures);
        Assert.Equal(1, diagnostics.ReconnectAttempt);
        Assert.Equal(1, diagnostics.LastFailedAttempt);
        Assert.Equal(TimeSpan.FromSeconds(1), diagnostics.LastReconnectDelay);
        Assert.False(diagnostics.LastBackoffWasReset);
        Assert.Equal("line one line two  line three", diagnostics.LastError);
        Assert.NotNull(diagnostics.LastFailureAt);
        Assert.NotNull(diagnostics.LastSessionAttemptAt);
        Assert.Equal(Iec104SessionState.Stopped, diagnostics.SessionState);
    }

    private sealed class ConnectFailingAdapter : IIec104ClientAdapter
    {
        private readonly Exception? _connectFailure;

        public ConnectFailingAdapter(Exception? connectFailure)
        {
            _connectFailure = connectFailure;
        }

        public bool IsConnected { get; private set; }

        public Task ConnectAsync(string host, int port, Iec104SessionOptions options, CancellationToken cancellationToken = default)
        {
            if (_connectFailure is not null)
                return Task.FromException(_connectFailure);
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task StartDataTransferAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopDataTransferAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask SendAsync(Iec104AsduEnvelope asdu, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
