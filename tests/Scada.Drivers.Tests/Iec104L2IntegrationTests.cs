using System.Collections.Concurrent;
using System.Diagnostics;
using Scada.Core.Tags;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

[Collection("Iec104L2")]
public sealed class Iec104L2IntegrationTests
{
    [Fact]
    [Trait("Category", "Iec104L2Integration")]
    public async Task TcpAdapter_ExchangesStartGiAndSpontaneousDataWithIndependentLib60870Peer()
    {
        if (!TryGetEndpoint(out var host, out var port)) return;

        await using var adapter = new Iec104TcpClientAdapter();
        var options = CreateSessionOptions();
        await adapter.ConnectAsync(host, port, options);
        await adapter.StartDataTransferAsync();

        var gi = new Iec104GeneralInterrogationTransaction(1);
        await adapter.SendAsync(gi.CreateActivation());

        var sawGiScaled = false;
        var sawGiSingle = false;
        var sawSpontaneous = false;
        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));

        await foreach (var asdu in adapter.ReadAsync(readCts.Token))
        {
            if (asdu.Header.TypeId == Iec104TypeId.CIcNa1)
            {
                gi.ObserveControlResponse(asdu);
            }
            else if (Iec104InformationObjectDecoder.IsSupported(asdu.Header.TypeId))
            {
                var points = Iec104InformationObjectDecoder.Decode(asdu, TimeZoneInfo.Utc);
                if (asdu.Header.CauseOfTransmission.CauseCode == 20)
                {
                    sawGiScaled |= points.Any(static point =>
                        point.InformationObjectAddress.Value == 100 &&
                        point.Value is short value && value == 23 &&
                        point.Quality == TagQuality.Good);
                    sawGiSingle |= points.Any(static point =>
                        point.InformationObjectAddress.Value == 104 &&
                        point.Value is bool value && value &&
                        point.Quality == TagQuality.Good);
                }

                if (asdu.Header.CauseOfTransmission.CauseCode == 3)
                {
                    sawSpontaneous |= points.Any(static point =>
                        point.InformationObjectAddress.Value == 110 &&
                        point.Value is short &&
                        point.Quality == TagQuality.Good);
                }
            }

            if (gi.State == Iec104GeneralInterrogationState.Completed && sawGiScaled && sawGiSingle && sawSpontaneous)
                break;
        }

        Assert.Equal(Iec104GeneralInterrogationState.Completed, gi.State);
        Assert.True(sawGiScaled);
        Assert.True(sawGiSingle);
        Assert.True(sawSpontaneous);

        var diagnostics = adapter.GetTransportDiagnostics();
        Assert.True(diagnostics.IsConnected);
        Assert.True(diagnostics.IsDataTransferStarted);
        Assert.True(diagnostics.IFramesSent > 0);
        Assert.True(diagnostics.IFramesReceived > 0);
        Assert.Equal(0, diagnostics.ProtocolErrors);
        Assert.Equal(0, diagnostics.SessionFailures);

        await adapter.StopDataTransferAsync();
        await adapter.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "Iec104L2Integration")]
    public async Task ManagedClient_ReachesReadyAndCompletesDirectCommandAgainstIndependentLib60870Peer()
    {
        if (!TryGetEndpoint(out var host, out var port)) return;

        var points = new ConcurrentQueue<Iec104DecodedPoint>();
        var client = CreateManagedClient(host, port);
        using var runCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var runTask = client.RunAsync(
            (point, _) =>
            {
                points.Enqueue(point);
                return ValueTask.CompletedTask;
            },
            cancellationToken: runCts.Token);

        try
        {
            await WaitForReadinessAsync(
                client,
                static readiness => readiness.State == Iec104ReadinessState.Ready,
                TimeSpan.FromSeconds(10));

            await WaitForPointAsync(
                points,
                static point => point.InformationObjectAddress.Value == 100 && point.Value is short value && value == 23,
                TimeSpan.FromSeconds(5));
            await WaitForPointAsync(
                points,
                static point => point.InformationObjectAddress.Value == 110 && point.Value is short,
                TimeSpan.FromSeconds(5));

            var command = Iec104CommandTransaction.Single(
                1,
                5000,
                true,
                Iec104CommandMode.DirectOperate);
            var result = await client.ExecuteCommandAsync(command, runCts.Token);

            Assert.Equal(Iec104CommandOutcome.Completed, result.Outcome);
            Assert.Equal(Iec104CommandState.Completed, result.ProtocolState);
            Assert.True(result.ExecuteWasTransmitted);
            Assert.True(result.WasAccepted);

            var feedback = await WaitForPointAsync(
                points,
                static point => point.InformationObjectAddress.Value == 5001 && point.Value is bool value && value,
                TimeSpan.FromSeconds(5));
            Assert.Equal(TagQuality.Good, feedback.Quality);

            var diagnostics = client.GetDiagnostics();
            Assert.Equal(1, diagnostics.Commands.Requested);
            Assert.Equal(1, diagnostics.Commands.Completed);
            Assert.Equal(0, diagnostics.Commands.Ambiguous);
            Assert.Equal(0, diagnostics.Commands.Rejected);
            Assert.True(diagnostics.ObservedPointUpdates >= 3);
        }
        finally
        {
            await StopManagedClientAsync(runCts, runTask);
        }
    }

    [Fact]
    [Trait("Category", "Iec104L2Integration")]
    public async Task ManagedClient_ReconnectsAndRepeatsStartupGiAfterRealIndependentPeerRestart()
    {
        if (!TryGetEndpoint(out var host, out var port)) return;
        var container = Environment.GetEnvironmentVariable("ELITESCADA_IEC104_L2_RESTART_CONTAINER")?.Trim();
        if (string.IsNullOrWhiteSpace(container)) return;

        var points = new ConcurrentQueue<Iec104DecodedPoint>();
        var client = CreateManagedClient(host, port);
        using var runCts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var runTask = client.RunAsync(
            (point, _) =>
            {
                points.Enqueue(point);
                return ValueTask.CompletedTask;
            },
            cancellationToken: runCts.Token);

        try
        {
            var initialReady = await WaitForReadinessAsync(
                client,
                static readiness => readiness.State == Iec104ReadinessState.Ready,
                TimeSpan.FromSeconds(10));
            var initialAttempt = initialReady.ReconnectAttempt;
            var giCountBefore = CountPoint(points, 100);
            Assert.True(giCountBefore >= 1);

            await RunDockerAsync("stop", "-t", "1", container);
            try
            {
                await WaitForDiagnosticsAsync(
                    client,
                    diagnostics => diagnostics.SessionFailures >= 1 && diagnostics.ReconnectAttempt > initialAttempt,
                    TimeSpan.FromSeconds(12));
            }
            finally
            {
                await RunDockerAsync("start", container);
            }

            var recovered = await WaitForReadinessAsync(
                client,
                readiness => readiness.State == Iec104ReadinessState.Ready && readiness.ReconnectAttempt > initialAttempt,
                TimeSpan.FromSeconds(15));
            Assert.True(recovered.GeneralInterrogationStates.TryGetValue(1, out var giState));
            Assert.Equal(Iec104GeneralInterrogationState.Completed, giState);

            await WaitForConditionAsync(
                () => CountPoint(points, 100) > giCountBefore,
                TimeSpan.FromSeconds(6),
                "startup GI point was not observed again after peer restart");

            var postReconnectCommand = Iec104CommandTransaction.Single(
                1,
                5000,
                false,
                Iec104CommandMode.DirectOperate);
            var result = await client.ExecuteCommandAsync(postReconnectCommand, runCts.Token);
            Assert.Equal(Iec104CommandOutcome.Completed, result.Outcome);
            Assert.True(result.WasAccepted);

            await WaitForPointAsync(
                points,
                static point => point.InformationObjectAddress.Value == 5001 && point.Value is bool value && !value,
                TimeSpan.FromSeconds(5));

            var diagnostics = client.GetDiagnostics();
            Assert.True(diagnostics.SessionFailures >= 1);
            Assert.True(diagnostics.ReconnectAttempt > initialAttempt);
            Assert.Equal(1, diagnostics.Commands.Completed);
        }
        finally
        {
            await StopManagedClientAsync(runCts, runTask);
        }
    }

    private static Iec104ManagedClient CreateManagedClient(string host, int port) =>
        new(
            static () => new Iec104TcpClientAdapter(),
            host,
            port,
            CreateSessionOptions(),
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

    private static Iec104SessionOptions CreateSessionOptions() =>
        new()
        {
            T0 = TimeSpan.FromSeconds(3),
            T1 = TimeSpan.FromSeconds(3),
            T2 = TimeSpan.FromSeconds(1),
            T3 = TimeSpan.FromSeconds(5),
            K = 12,
            W = 8
        };

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

    private static async Task<Iec104ReadinessSnapshot> WaitForReadinessAsync(
        Iec104ManagedClient client,
        Func<Iec104ReadinessSnapshot, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var readiness = client.GetReadiness();
            if (predicate(readiness)) return readiness;
            await Task.Delay(50);
        }

        var last = client.GetReadiness();
        throw new TimeoutException($"Timed out waiting for IEC-104 L2 readiness. Last state: {last.State}, session: {last.SessionState}, attempt: {last.ReconnectAttempt}, error: {last.LastFailure}");
    }

    private static async Task<Iec104ManagedDiagnosticSnapshot> WaitForDiagnosticsAsync(
        Iec104ManagedClient client,
        Func<Iec104ManagedDiagnosticSnapshot, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var diagnostics = client.GetDiagnostics();
            if (predicate(diagnostics)) return diagnostics;
            await Task.Delay(50);
        }

        var last = client.GetDiagnostics();
        throw new TimeoutException($"Timed out waiting for IEC-104 L2 diagnostics. Attempt: {last.ReconnectAttempt}, failures: {last.SessionFailures}, error: {last.LastError}");
    }

    private static async Task<Iec104DecodedPoint> WaitForPointAsync(
        ConcurrentQueue<Iec104DecodedPoint> points,
        Func<Iec104DecodedPoint, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var match = points.FirstOrDefault(predicate);
            if (match is not null) return match;
            await Task.Delay(50);
        }

        throw new TimeoutException("Timed out waiting for expected IEC-104 L2 process point.");
    }

    private static int CountPoint(ConcurrentQueue<Iec104DecodedPoint> points, int ioa) =>
        points.Count(point => point.InformationObjectAddress.Value == ioa);

    private static async Task WaitForConditionAsync(Func<bool> predicate, TimeSpan timeout, string failure)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(50);
        }

        throw new TimeoutException(failure);
    }

    private static async Task RunDockerAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start docker for IEC-104 L2 restart orchestration.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"docker {string.Join(' ', arguments)} failed with exit code {process.ExitCode}: {stderr.Trim()} {stdout.Trim()}".Trim());
    }

    private static async Task StopManagedClientAsync(CancellationTokenSource cancellation, Task runTask)
    {
        cancellation.Cancel();
        try
        {
            await runTask;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
