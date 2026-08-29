using System.Runtime.CompilerServices;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104ReconnectTests
{
    [Fact]
    public void BackoffUsesConfiguredSequenceCapsAndResets()
    {
        var backoff = new Iec104ReconnectBackoff(new Iec104ReconnectPolicy());

        Assert.Equal(TimeSpan.FromSeconds(1), backoff.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(2), backoff.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(5), backoff.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(10), backoff.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(30), backoff.NextDelay());
        Assert.Equal(TimeSpan.FromSeconds(30), backoff.NextDelay());

        backoff.Reset();

        Assert.Equal(TimeSpan.FromSeconds(1), backoff.NextDelay());
    }

    [Fact]
    public async Task ReconnectingRunnerCreatesFreshSessionAndRepeatsOnlyBootstrap()
    {
        var adapters = new List<FailingAdapter>();
        var failures = new List<Iec104ReconnectFailure>();
        var delays = new List<TimeSpan>();
        using var cancellation = new CancellationTokenSource();

        var runner = new Iec104ReconnectingSessionRunner(
            adapterFactory: () =>
            {
                var adapter = new FailingAdapter();
                adapters.Add(adapter);
                return adapter;
            },
            host: "127.0.0.1",
            port: 2404,
            sessionOptions: new Iec104SessionOptions(),
            stationTimeZone: TimeZoneInfo.Utc,
            commonAddresses: new ushort[] { 1 },
            reconnectPolicy: new Iec104ReconnectPolicy
            {
                StableSessionThreshold = TimeSpan.FromMinutes(1)
            },
            delayAsync: (delay, token) =>
            {
                delays.Add(delay);
                return token.IsCancellationRequested
                    ? Task.FromCanceled(token)
                    : Task.CompletedTask;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(
            static (_, _) => ValueTask.CompletedTask,
            (failure, _) =>
            {
                failures.Add(failure);
                if (failure.Attempt == 3)
                    cancellation.Cancel();
                return ValueTask.CompletedTask;
            },
            cancellation.Token));

        Assert.Equal(3, adapters.Count);
        Assert.Equal(3, failures.Count);
        Assert.Equal(
            new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) },
            delays);

        Assert.All(adapters, adapter =>
        {
            Assert.Equal(1, adapter.ConnectCount);
            Assert.Equal(1, adapter.StartCount);
            Assert.Equal(1, adapter.SendCount);
            Assert.Equal(Iec104TypeId.CIcNa1, adapter.LastSent?.Header.TypeId);
            Assert.Equal(1, adapter.StopCount);
            Assert.Equal(1, adapter.DisconnectCount);
            Assert.Equal(1, adapter.DisposeCount);
        });
    }

    private sealed class FailingAdapter : IIec104ClientAdapter
    {
        private bool _connected;

        public bool IsConnected => _connected;
        public int ConnectCount { get; private set; }
        public int StartCount { get; private set; }
        public int SendCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisconnectCount { get; private set; }
        public int DisposeCount { get; private set; }
        public Iec104AsduEnvelope? LastSent { get; private set; }

        public Task ConnectAsync(
            string host,
            int port,
            Iec104SessionOptions options,
            CancellationToken cancellationToken = default)
        {
            ConnectCount++;
            _connected = true;
            return Task.CompletedTask;
        }

        public Task StartDataTransferAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopDataTransferAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public ValueTask SendAsync(Iec104AsduEnvelope asdu, CancellationToken cancellationToken = default)
        {
            SendCount++;
            LastSent = asdu;
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<Iec104AsduEnvelope> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            if (_connected)
                throw new IOException("Simulated IEC-104 transport failure.");

            yield return Iec104AsduEnvelope.Create(
                new Iec104AsduHeader(
                    Iec104TypeId.CIcNa1,
                    ObjectCount: 1,
                    IsSequence: false,
                    new Iec104CauseOfTransmission(3),
                    CommonAddress: 1),
                new byte[4]);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            DisconnectCount++;
            _connected = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
