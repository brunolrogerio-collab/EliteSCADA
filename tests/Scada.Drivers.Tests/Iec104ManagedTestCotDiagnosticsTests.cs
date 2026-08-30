using System.Runtime.CompilerServices;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104ManagedTestCotDiagnosticsTests
{
    [Fact]
    public async Task ManagedDiagnostics_RetainIgnoredTestAsduCountDuringReconnectBackoff()
    {
        using var cancellation = new CancellationTokenSource();
        var delayEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var incoming = new[]
        {
            CreateGiResponse(Iec104GeneralInterrogationTransaction.ActivationConfirmationCause),
            CreateTestSinglePoint(ioa: 77)
        };
        var client = new Iec104ManagedClient(
            () => new FailingAdapter(incoming),
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
            delayAsync: (_, token) =>
            {
                delayEntered.TrySetResult(true);
                return Task.Delay(Timeout.InfiniteTimeSpan, token);
            });

        var runTask = client.RunAsync(
            static (_, _) => ValueTask.CompletedTask,
            cancellationToken: cancellation.Token);

        await delayEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var diagnostics = client.GetDiagnostics();

        Assert.Equal(1, diagnostics.TestAsdusIgnored);
        Assert.Equal(0, diagnostics.ObservedPointUpdates);
        Assert.Equal(1, diagnostics.SessionFailures);
        Assert.Equal(Iec104SessionState.Stopped, diagnostics.SessionState);
        Assert.Null(diagnostics.Transport);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    private static Iec104AsduEnvelope CreateGiResponse(byte cause) =>
        Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.CIcNa1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(cause),
                CommonAddress: 1),
            new byte[] { 0, 0, 0, Iec104GeneralInterrogationTransaction.GlobalQoi });

    private static Iec104AsduEnvelope CreateTestSinglePoint(int ioa)
    {
        var payload = new byte[4];
        new Iec104InformationObjectAddress(ioa).WriteTo(payload.AsSpan(0, 3));
        payload[3] = 1;
        return Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.MSpNa1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(causeCode: 3, isTest: true),
                CommonAddress: 1),
            payload);
    }

    private sealed class FailingAdapter : IIec104ClientAdapter
    {
        private readonly IReadOnlyList<Iec104AsduEnvelope> _incoming;

        public FailingAdapter(IReadOnlyList<Iec104AsduEnvelope> incoming)
        {
            _incoming = incoming;
        }

        public bool IsConnected { get; private set; }

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

        public ValueTask SendAsync(Iec104AsduEnvelope asdu, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public async IAsyncEnumerable<Iec104AsduEnvelope> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            foreach (var asdu in _incoming)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return asdu;
            }

            throw new IOException("Synthetic IEC-104 session failure after TEST data.");
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
