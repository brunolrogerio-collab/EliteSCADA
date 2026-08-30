using System.Collections.Concurrent;
using System.Diagnostics;
using Scada.Core.Tags;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104Lib60870L2IntegrationTests
{
    [Fact]
    [Trait("Category", "Iec104Lib60870Integration")]
    public async Task ManagedClient_CompletesStartDtGiAndDecodesOfficialPeerPoints()
    {
        if (!TryGetEndpoint(out var host, out var port)) return;

        var observed = new ConcurrentDictionary<(ushort Ca, int Ioa, Iec104TypeId Type), Iec104DecodedPoint>();
        using var runCts = new CancellationTokenSource();
        var client = CreateClient(host, port);
        var runTask = client.RunAsync(
            (point, _) =>
            {
                observed[(point.CommonAddress, point.InformationObjectAddress.Value, point.TypeId)] = point;
                return ValueTask.CompletedTask;
            },
            cancellationToken: runCts.Token);

        try
        {
            await WaitUntilAsync(
                () => client.GetReadiness().State == Iec104ReadinessState.Ready,
                runTask,
                TimeSpan.FromSeconds(10));

            await WaitUntilAsync(
                () => HasExpectedGiPointSet(observed),
                runTask,
                TimeSpan.FromSeconds(10));

            var readiness = client.GetReadiness();
            Assert.Equal(Iec104ReadinessState.Ready, readiness.State);
            Assert.True(readiness.IsTransportConnected);
            Assert.True(readiness.IsDataTransferStarted);
            Assert.True(readiness.StartupGeneralInterrogationCompleted);
            Assert.False(readiness.StartupGeneralInterrogationRejected);
            Assert.Equal(Iec104GeneralInterrogationState.Completed, readiness.GeneralInterrogationStates[1]);

            AssertScaled(observed, 100, -1);
            AssertScaled(observed, 101, 23);
            AssertScaled(observed, 102, 2300);
            AssertSinglePoint(observed, 104, true);
            AssertSinglePoint(observed, 105, false);
            AssertBitString(observed, 500, 0x0000aaaa);

            await WaitUntilAsync(
                () => observed.Keys.Any(static key => key.Ca == 1 && key.Ioa == 110 && key.Type == Iec104TypeId.MMeNb1),
                runTask,
                TimeSpan.FromSeconds(6));

            var periodic = observed[(1, 110, Iec104TypeId.MMeNb1)];
            Assert.Equal(TagQuality.Good, periodic.Quality);
            Assert.IsType<short>(periodic.Value);

            var diagnostics = client.GetDiagnostics();
            Assert.Equal(Iec104SessionState.Running, diagnostics.SessionState);
            Assert.Equal(0, diagnostics.SessionFailures);
            Assert.True(diagnostics.ObservedPointUpdates >= 7);
            Assert.NotNull(diagnostics.Transport);
            Assert.Null(diagnostics.LastError);
        }
        finally
        {
            await StopManagedClientAsync(runCts, runTask);
        }
    }

    [Fact]
    [Trait("Category", "Iec104Lib60870Integration")]
    public async Task ManagedClient_ReconnectsAndRepeatsGiAfterRealPeerRestart()
    {
        if (!TryGetEndpoint(out var host, out var port)) return;
        var container = Environment.GetEnvironmentVariable("ELITESCADA_IEC104_L2_RESTART_CONTAINER")?.Trim();
        if (string.IsNullOrWhiteSpace(container)) return;

        var observed = new ConcurrentDictionary<(ushort Ca, int Ioa, Iec104TypeId Type), Iec104DecodedPoint>();
        using var runCts = new CancellationTokenSource();
        var client = CreateClient(host, port);
        var runTask = client.RunAsync(
            (point, _) =>
            {
                observed[(point.CommonAddress, point.InformationObjectAddress.Value, point.TypeId)] = point;
                return ValueTask.CompletedTask;
            },
            cancellationToken: runCts.Token);

        try
        {
            await WaitUntilAsync(
                () => client.GetReadiness().State == Iec104ReadinessState.Ready && HasExpectedGiPointSet(observed),
                runTask,
                TimeSpan.FromSeconds(10));

            var before = client.GetDiagnostics();
            Assert.Equal(0, before.SessionFailures);
            Assert.True(before.LastSessionAttemptAt.HasValue);
            Assert.True(before.ObservedPointUpdates >= 6);

            observed.Clear();
            await RunDockerAsync("stop", "-t", "1", container);
            try
            {
                await WaitUntilAsync(
                    () => client.GetDiagnostics().SessionFailures > before.SessionFailures,
                    runTask,
                    TimeSpan.FromSeconds(10));
            }
            finally
            {
                await RunDockerAsync("start", container);
            }

            await WaitUntilAsync(
                () => client.GetReadiness().State == Iec104ReadinessState.Ready && HasExpectedGiPointSet(observed),
                runTask,
                TimeSpan.FromSeconds(20));

            var after = client.GetDiagnostics();
            Assert.Equal(Iec104SessionState.Running, after.SessionState);
            Assert.True(after.SessionFailures > before.SessionFailures);
            Assert.True(after.ReconnectAttempt > before.ReconnectAttempt);
            Assert.True(after.LastFailureAt > before.LastSessionAttemptAt);
            Assert.True(after.LastSessionAttemptAt > before.LastSessionAttemptAt);
            Assert.True(after.ObservedPointUpdates > before.ObservedPointUpdates);
            Assert.NotNull(after.Transport);

            var readiness = client.GetReadiness();
            Assert.True(readiness.IsTransportConnected);
            Assert.True(readiness.IsDataTransferStarted);
            Assert.True(readiness.StartupGeneralInterrogationCompleted);
            Assert.Equal(Iec104GeneralInterrogationState.Completed, readiness.GeneralInterrogationStates[1]);

            AssertScaled(observed, 100, -1);
            AssertSinglePoint(observed, 104, true);
            AssertBitString(observed, 500, 0x0000aaaa);
        }
        finally
        {
            await StopManagedClientAsync(runCts, runTask);
        }
    }

    private static Iec104ManagedClient CreateClient(string host, int port) =>
        new(
            static () => new Iec104TcpClientAdapter(),
            host,
            port,
            new Iec104SessionOptions
            {
                T0 = TimeSpan.FromSeconds(5),
                T1 = TimeSpan.FromSeconds(4),
                T2 = TimeSpan.FromSeconds(1),
                T3 = TimeSpan.FromSeconds(8),
                K = 12,
                W = 8
            },
            TimeZoneInfo.Utc,
            new ushort[] { 1 },
            reconnectPolicy: new Iec104ReconnectPolicy
            {
                Delays = new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500) },
                StableSessionThreshold = TimeSpan.FromSeconds(5)
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

    private static bool HasExpectedGiPointSet(
        ConcurrentDictionary<(ushort Ca, int Ioa, Iec104TypeId Type), Iec104DecodedPoint> observed) =>
        observed.ContainsKey((1, 100, Iec104TypeId.MMeNb1)) &&
        observed.ContainsKey((1, 101, Iec104TypeId.MMeNb1)) &&
        observed.ContainsKey((1, 102, Iec104TypeId.MMeNb1)) &&
        observed.ContainsKey((1, 104, Iec104TypeId.MSpNa1)) &&
        observed.ContainsKey((1, 105, Iec104TypeId.MSpNa1)) &&
        observed.ContainsKey((1, 500, Iec104TypeId.MBoNa1));

    private static void AssertScaled(
        ConcurrentDictionary<(ushort Ca, int Ioa, Iec104TypeId Type), Iec104DecodedPoint> observed,
        int ioa,
        short expected)
    {
        var point = observed[(1, ioa, Iec104TypeId.MMeNb1)];
        Assert.Equal(expected, Assert.IsType<short>(point.Value));
        Assert.Equal(TagQuality.Good, point.Quality);
        Assert.Null(point.SourceTimestamp);
    }

    private static void AssertSinglePoint(
        ConcurrentDictionary<(ushort Ca, int Ioa, Iec104TypeId Type), Iec104DecodedPoint> observed,
        int ioa,
        bool expected)
    {
        var point = observed[(1, ioa, Iec104TypeId.MSpNa1)];
        Assert.Equal(expected, Assert.IsType<bool>(point.Value));
        Assert.Equal(TagQuality.Good, point.Quality);
        Assert.Null(point.SourceTimestamp);
    }

    private static void AssertBitString(
        ConcurrentDictionary<(ushort Ca, int Ioa, Iec104TypeId Type), Iec104DecodedPoint> observed,
        int ioa,
        int expected)
    {
        var point = observed[(1, ioa, Iec104TypeId.MBoNa1)];
        Assert.Equal(expected, Assert.IsType<int>(point.Value));
        Assert.Equal(TagQuality.Good, point.Quality);
        Assert.Null(point.SourceTimestamp);
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

    private static async Task StopManagedClientAsync(CancellationTokenSource runCts, Task runTask)
    {
        runCts.Cancel();
        try
        {
            await runTask;
        }
        catch (OperationCanceledException) when (runCts.IsCancellationRequested)
        {
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        Task runTask,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            if (runTask.IsCompleted)
                await runTask;
            await Task.Delay(50);
        }

        throw new TimeoutException("Timed out waiting for the lib60870 IEC-104 L2 acceptance condition.");
    }
}
