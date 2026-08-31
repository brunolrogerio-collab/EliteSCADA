using System.Runtime.CompilerServices;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104CommandTransmissionAmbiguityTests
{
    [Fact]
    public async Task DirectExecute_AmbiguousTransmissionExceptionIsReportedAsPhysicallyAmbiguous()
    {
        var adapter = new AmbiguousExecuteAdapter();
        using var coordinator = new Iec104CommandCoordinator(adapter, new Iec104CommandExecutionOptions
        {
            ConfirmationTimeout = TimeSpan.FromSeconds(1),
            CompletionTimeout = TimeSpan.FromSeconds(1),
            MaxConcurrentCommands = 1
        });
        var transaction = Iec104CommandTransaction.Single(
            commonAddress: 1,
            informationObjectAddress: 77,
            value: true,
            Iec104CommandMode.DirectOperate);

        var result = await coordinator.ExecuteAsync(transaction);

        Assert.Equal(Iec104CommandOutcome.Ambiguous, result.Outcome);
        Assert.True(result.ExecuteWasTransmitted);
        Assert.False(result.WasAccepted);
        Assert.Equal(Iec104CommandState.AwaitingExecutionConfirmation, result.ProtocolState);
        Assert.Contains("sequence was reserved", result.Detail ?? string.Empty);
        Assert.Equal(1, adapter.SendCount);
    }

    private sealed class AmbiguousExecuteAdapter : IIec104ClientAdapter
    {
        public bool IsConnected => true;
        public int SendCount { get; private set; }

        public Task ConnectAsync(
            string host,
            int port,
            Iec104SessionOptions options,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StartDataTransferAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopDataTransferAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask SendAsync(
            Iec104AsduEnvelope asdu,
            CancellationToken cancellationToken = default)
        {
            SendCount++;
            throw new Iec104AmbiguousTransmissionException(
                "IEC-104 I-format transmission failed after a send sequence was reserved; peer delivery is ambiguous.",
                new IOException("simulated write failure after sequence reservation"));
        }

        public async IAsyncEnumerable<Iec104AsduEnvelope> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
