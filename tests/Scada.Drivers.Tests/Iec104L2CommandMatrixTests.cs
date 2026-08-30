using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104L2CommandMatrixTests
{
    [Theory]
    [InlineData("single", Iec104CommandMode.DirectOperate)]
    [InlineData("single", Iec104CommandMode.SelectBeforeOperate)]
    [InlineData("double", Iec104CommandMode.DirectOperate)]
    [InlineData("double", Iec104CommandMode.SelectBeforeOperate)]
    [InlineData("normalized", Iec104CommandMode.DirectOperate)]
    [InlineData("normalized", Iec104CommandMode.SelectBeforeOperate)]
    [InlineData("scaled", Iec104CommandMode.DirectOperate)]
    [InlineData("scaled", Iec104CommandMode.SelectBeforeOperate)]
    [InlineData("shortFloat", Iec104CommandMode.DirectOperate)]
    [InlineData("shortFloat", Iec104CommandMode.SelectBeforeOperate)]
    [Trait("Category", "Iec104L2Integration")]
    public async Task FirstReleaseCommandTypes_CompleteAgainstIndependentLib60870Peer(
        string commandKind,
        Iec104CommandMode mode)
    {
        if (!TryGetEndpoint(out var host, out var port)) return;

        var client = CreateManagedClient(host, port);
        using var runCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var runTask = client.RunAsync(
            static (_, _) => ValueTask.CompletedTask,
            cancellationToken: runCts.Token);

        try
        {
            await WaitForReadyAsync(client, TimeSpan.FromSeconds(8));

            var transaction = CreateTransaction(commandKind, mode);
            var result = await client.ExecuteCommandAsync(transaction, runCts.Token);

            Assert.Equal(Iec104CommandOutcome.Completed, result.Outcome);
            Assert.Equal(Iec104CommandState.Completed, result.ProtocolState);
            Assert.True(result.ExecuteWasTransmitted);
            Assert.True(result.WasAccepted);

            var diagnostics = client.GetDiagnostics();
            Assert.Equal(1, diagnostics.Commands.Requested);
            Assert.Equal(1, diagnostics.Commands.Completed);
            Assert.Equal(0, diagnostics.Commands.Rejected);
            Assert.Equal(0, diagnostics.Commands.TimedOut);
            Assert.Equal(0, diagnostics.Commands.Ambiguous);
        }
        finally
        {
            runCts.Cancel();
            try
            {
                await runTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static Iec104CommandTransaction CreateTransaction(string commandKind, Iec104CommandMode mode) =>
        commandKind switch
        {
            "single" => Iec104CommandTransaction.Single(1, 5000, true, mode),
            "double" => Iec104CommandTransaction.Double(1, 5000, Iec104DoublePointState.On, mode),
            "normalized" => Iec104CommandTransaction.NormalizedSetpoint(1, 5000, 0.25f, mode),
            "scaled" => Iec104CommandTransaction.ScaledSetpoint(1, 5000, 1234, mode),
            "shortFloat" => Iec104CommandTransaction.ShortFloatSetpoint(1, 5000, 12.5f, mode),
            _ => throw new ArgumentOutOfRangeException(nameof(commandKind), commandKind, "Unknown IEC-104 L2 command case.")
        };

    private static Iec104ManagedClient CreateManagedClient(string host, int port) =>
        new(
            static () => new Iec104TcpClientAdapter(),
            host,
            port,
            new Iec104SessionOptions
            {
                T0 = TimeSpan.FromSeconds(3),
                T1 = TimeSpan.FromSeconds(3),
                T2 = TimeSpan.FromSeconds(1),
                T3 = TimeSpan.FromSeconds(5),
                K = 12,
                W = 8
            },
            TimeZoneInfo.Utc,
            [1],
            new Iec104ReconnectPolicy
            {
                Delays = [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500)],
                StableSessionThreshold = TimeSpan.FromSeconds(10)
            },
            new Iec104CommandExecutionOptions
            {
                MaxConcurrentCommands = 2,
                ConfirmationTimeout = TimeSpan.FromSeconds(3),
                CompletionTimeout = TimeSpan.FromSeconds(3)
            });

    private static bool TryGetEndpoint(out string host, out int port)
    {
        host = Environment.GetEnvironmentVariable("ELITESCADA_IEC104_L2_HOST")?.Trim() ?? string.Empty;
        var rawPort = Environment.GetEnvironmentVariable("ELITESCADA_IEC104_L2_PORT")?.Trim();
        if (string.IsNullOrWhiteSpace(host) || !int.TryParse(rawPort, out port) || port is < 1 or > 65535)
        {
            port = 0;
            return false;
        }

        return true;
    }

    private static async Task WaitForReadyAsync(Iec104ManagedClient client, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var readiness = client.GetReadiness();
            if (readiness.State == Iec104ReadinessState.Ready)
                return;
            await Task.Delay(50);
        }

        var last = client.GetReadiness();
        throw new TimeoutException($"Timed out waiting for IEC-104 L2 command readiness. State={last.State}, session={last.SessionState}, error={last.LastFailure}");
    }
}
