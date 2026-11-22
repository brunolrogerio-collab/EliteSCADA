using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Scada.Api.Licensing;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Intelligence;

namespace Scada.Drivers.Tests;

public sealed partial class AssetProcessIntelligenceAnalyticsTests
{
    private const int Stage6BSeed = 20261022;
    private const int Stage6BWarmupIterations = 2;
    private const int Stage6BMeasuredIterations = 10;
    private const int Stage6BPageSize = AssetProcessIntelligenceKpiCatalogV1.QueryPageSize;

    [Fact]
    public async Task Stage6BResourceCharacterization_ExercisesBoundedProfileCoveragePathAndEmitsEvidence()
    {
        var scenarios = new[]
        {
            new Stage6BScenario("active-one-signal-small", 1, 64, TimeSpan.FromHours(1), true, true),
            new Stage6BScenario("active-sixteen-signals-small", 16, 64, TimeSpan.FromHours(1), true, true),
            new Stage6BScenario("active-one-signal-large", 1, 2048, TimeSpan.FromDays(30), true, true),
            new Stage6BScenario("active-sixteen-signals-large", 16, 512, TimeSpan.FromDays(30), true, true),
            new Stage6BScenario("inactive-sixteen-signals-large", 16, 512, TimeSpan.FromDays(30), false, true),
            new Stage6BScenario("no-permit-sixteen-signals-large", 16, 512, TimeSpan.FromDays(30), true, false)
        };

        var measurements = new List<Stage6BScenarioResult>(scenarios.Length);
        foreach (var scenario in scenarios)
            measurements.Add(await MeasureStage6BScenarioAsync(scenario));

        var report = new Stage6BResourceReport(
            SchemaVersion: 1,
            Stage: "6B",
            SourceSha: ReadGitValue("rev-parse HEAD") ?? Environment.GetEnvironmentVariable("ELITESCADA_SOURCE_SHA") ?? "unavailable",
            GitTrackedFilesClean: ReadGitValue("status --porcelain --untracked-files=no") is { } gitStatus && string.IsNullOrWhiteSpace(gitStatus),
            DotNetSdk: Environment.GetEnvironmentVariable("ELITESCADA_DOTNET_SDK") ?? "not-provided",
            Runtime: RuntimeInformation.FrameworkDescription,
            OperatingSystem: RuntimeInformation.OSDescription,
            RunnerOs: Environment.GetEnvironmentVariable("RUNNER_OS") ?? "local",
            RunnerName: Environment.GetEnvironmentVariable("RUNNER_NAME") ?? Environment.MachineName,
            ProcessorCount: Environment.ProcessorCount,
            Seed: Stage6BSeed,
            ProfileMaximumSignals: AssetProcessIntelligenceProfileCoverageCatalogV1.MaximumSignals,
            ProfileMaximumRowsPerWindow: AssetProcessIntelligenceProfileCoverageCatalogV1.MaximumRowsPerWindow,
            HistorianPageSize: Stage6BPageSize,
            MaximumWindowDays: 31,
            WarmupIterations: Stage6BWarmupIterations,
            MeasuredIterations: Stage6BMeasuredIterations,
            WorkingSetSampleIntervalMilliseconds: 5,
            Provider: "existing RecordingHistoricalQueryService test double; deterministic in-memory paging",
            Limitations: new[]
            {
                "Measures the production AssetProcessIntelligenceAnalyticsService profile-coverage and gate path, not PostgreSQL storage, indexes, network I/O, or database seed time.",
                "Process CPU, allocation, and sampled working-set figures are runner-process measurements; do not compare unlike runner classes as universal performance claims.",
                "Synthetic engineering/Historian rows establish software-path characterization only; no field performance, hardware floor, SLO, diagnosis, or operating-hour claim is made."
            },
            Scenarios: measurements);

        var requestedOutputPath = Environment.GetEnvironmentVariable("ELITESCADA_INTELLIGENCE_CHARACTERIZATION_OUTPUT");
        var jsonPath = string.IsNullOrWhiteSpace(requestedOutputPath)
            ? Path.Combine(Path.GetTempPath(), $"elitescada-intelligence-stage6b-{Environment.ProcessId}.json")
            : Path.GetFullPath(requestedOutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json);
        await File.WriteAllTextAsync(Path.ChangeExtension(jsonPath, ".md"), BuildStage6BMarkdownReport(report));

        Assert.Equal(6, report.Scenarios.Count);
        Assert.All(report.Scenarios, scenario => Assert.Equal(0, scenario.RuntimeWrites));
        Assert.Equal(0, Assert.Single(report.Scenarios, item => item.Name == "inactive-sixteen-signals-large").QueriesPerIteration);
        Assert.Equal(0, Assert.Single(report.Scenarios, item => item.Name == "no-permit-sixteen-signals-large").QueriesPerIteration);
    }

    private static async Task<Stage6BScenarioResult> MeasureStage6BScenarioAsync(Stage6BScenario scenario)
    {
        Assert.InRange(scenario.SignalCount, 1, AssetProcessIntelligenceProfileCoverageCatalogV1.MaximumSignals);
        Assert.InRange(scenario.RowsPerSignal, 1, AssetProcessIntelligenceProfileCoverageCatalogV1.MaximumRowsPerWindow);
        Assert.InRange((long)scenario.SignalCount * scenario.RowsPerSignal,
            1L,
            (long)AssetProcessIntelligenceProfileCoverageCatalogV1.MaximumRowsPerWindow);

        using var fixture = Fixture.Create(grantPermit: scenario.Permit, activeScope: scenario.ActiveScope);
        var tags = new List<TagDefinition>(scenario.SignalCount) { fixture.Tag };
        for (var signal = 1; signal < scenario.SignalCount; signal++)
            tags.Add(fixture.AddProfileSignal($"stage6b-{signal:00}", $"stage6b-role-{signal:00}", TagDataType.Double, "bar"));

        var rowsByTag = tags.ToDictionary(tag => tag.Id, tag => CreateStage6BSyntheticRows(
            tag.Id, scenario.RowsPerSignal, scenario.Window));
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            var rows = rowsByTag[tagId];
            var page = request.Page ?? throw new InvalidOperationException("Profile coverage must request a bounded page.");
            var offset = string.IsNullOrWhiteSpace(page.Cursor)
                ? 0
                : int.Parse(page.Cursor, NumberStyles.None, CultureInfo.InvariantCulture);
            var pageRows = rows.Skip(offset).Take(page.Size).ToArray();
            var nextOffset = offset + pageRows.Length;
            var nextCursor = nextOffset < rows.Length
                ? nextOffset.ToString(CultureInfo.InvariantCulture)
                : null;
            return Response(request, pageRows, nextCursor);
        };

        var expectedStatus = scenario.ActiveScope && scenario.Permit
            ? StatusCodes.Status200OK
            : StatusCodes.Status409Conflict;
        var requestWindow = Window(FromUtc, FromUtc.Add(scenario.Window));
        for (var warmup = 0; warmup < Stage6BWarmupIterations; warmup++)
            AssertStatus(expectedStatus, await fixture.ExecuteProfileCoverageAsync(window: requestWindow));
        Assert.Equal(0, fixture.Runtime.WriteCalls);
        fixture.Historian.ResetCounters();

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var startCpu = process.TotalProcessorTime;
        var startAllocated = GC.GetTotalAllocatedBytes(precise: false);
        var sampledPeakWorkingSet = process.WorkingSet64;
        using var samplingCancellation = new CancellationTokenSource();
        var sampleTask = Task.Run(async () =>
        {
            while (!samplingCancellation.IsCancellationRequested)
            {
                process.Refresh();
                UpdateMaximum(ref sampledPeakWorkingSet, process.WorkingSet64);
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(5), samplingCancellation.Token);
                }
                catch (OperationCanceledException) when (samplingCancellation.IsCancellationRequested)
                {
                    break;
                }
            }
        });

        var elapsedMilliseconds = new List<double>(Stage6BMeasuredIterations);
        try
        {
            for (var iteration = 0; iteration < Stage6BMeasuredIterations; iteration++)
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await fixture.ExecuteProfileCoverageAsync(window: requestWindow);
                stopwatch.Stop();
                AssertStatus(expectedStatus, result);
                elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
                Assert.Equal(0, fixture.Runtime.WriteCalls);
            }
        }
        finally
        {
            samplingCancellation.Cancel();
            await sampleTask;
        }

        process.Refresh();
        UpdateMaximum(ref sampledPeakWorkingSet, process.WorkingSet64);
        var cpuMilliseconds = (process.TotalProcessorTime - startCpu).TotalMilliseconds;
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: false) - startAllocated;
        var expectedRowsPerIteration = scenario.ActiveScope && scenario.Permit
            ? scenario.SignalCount * scenario.RowsPerSignal
            : 0;
        var expectedQueriesPerIteration = scenario.ActiveScope && scenario.Permit
            ? scenario.SignalCount * (int)Math.Ceiling(scenario.RowsPerSignal / (double)Stage6BPageSize)
            : 0;
        Assert.Equal((long)expectedRowsPerIteration * Stage6BMeasuredIterations, fixture.Historian.RowsReturned);
        Assert.Equal(expectedQueriesPerIteration * Stage6BMeasuredIterations, fixture.Historian.Calls);
        Assert.Equal(expectedQueriesPerIteration * Stage6BMeasuredIterations, fixture.Historian.PageRequests);

        return new Stage6BScenarioResult(
            scenario.Name,
            scenario.SignalCount,
            scenario.RowsPerSignal,
            (long)scenario.Window.TotalSeconds,
            expectedStatus,
            Stage6BWarmupIterations,
            Stage6BMeasuredIterations,
            elapsedMilliseconds,
            Median(elapsedMilliseconds),
            Percentile(elapsedMilliseconds, 0.95),
            cpuMilliseconds,
            cpuMilliseconds / Stage6BMeasuredIterations,
            allocatedBytes,
            allocatedBytes / Stage6BMeasuredIterations,
            sampledPeakWorkingSet,
            fixture.Historian.Calls,
            fixture.Historian.Calls / Stage6BMeasuredIterations,
            fixture.Historian.PageRequests,
            fixture.Historian.PageRequests / Stage6BMeasuredIterations,
            fixture.Historian.RowsReturned,
            fixture.Historian.RowsReturned / Stage6BMeasuredIterations,
            fixture.Runtime.WriteCalls);
    }

    private static HistoricalQueryRow[] CreateStage6BSyntheticRows(Guid tagId, int count, TimeSpan window)
    {
        return Enumerable.Range(0, count)
            .Select(index =>
            {
                var timestamp = FromUtc.AddTicks(window.Ticks * index / count);
                var value = ((index * 7919L + Stage6BSeed) % 100000L) / 100d;
                return Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(value));
            })
            .ToArray();
    }

    private static double Percentile(IReadOnlyList<double> values, double quantile)
    {
        var sorted = values.Order().ToArray();
        var index = Math.Clamp((int)Math.Ceiling(quantile * sorted.Length) - 1, 0, sorted.Length - 1);
        return sorted[index];
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2d
            : sorted[middle];
    }

    private static void UpdateMaximum(ref long target, long observed)
    {
        var current = Interlocked.Read(ref target);
        while (observed > current)
        {
            var replaced = Interlocked.CompareExchange(ref target, observed, current);
            if (replaced == current) return;
            current = replaced;
        }
    }

    private static string? ReadGitValue(string arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null) return null;

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null || !process.WaitForExit(5000)) return null;
            var output = process.StandardOutput.ReadToEnd().Trim();
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var gitEntry = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitEntry) || File.Exists(gitEntry)) return directory.FullName;
        }

        return null;
    }

    private static string BuildStage6BMarkdownReport(Stage6BResourceReport report)
    {
        var text = new StringBuilder()
            .AppendLine("# Intelligence Stage 6B resource characterization")
            .AppendLine()
            .AppendLine($"- Source SHA: `{report.SourceSha}`; .NET SDK: `{report.DotNetSdk}`; runtime: `{report.Runtime}`.")
            .AppendLine($"- Tracked Git files clean: `{report.GitTrackedFilesClean?.ToString() ?? "unknown"}`.")
            .AppendLine($"- OS/runner: `{report.OperatingSystem}` / `{report.RunnerOs}` / `{report.RunnerName}`; logical processors: {report.ProcessorCount}.")
            .AppendLine($"- Deterministic seed: `{report.Seed}`; measured iterations: {report.MeasuredIterations} after {report.WarmupIterations} warmups.")
            .AppendLine($"- Production bounds unchanged: {report.ProfileMaximumSignals} signals, {report.ProfileMaximumRowsPerWindow} total rows/window, 200 rows/page, {report.MaximumWindowDays}-day maximum window.")
            .AppendLine()
            .AppendLine("| Scenario | Signals × rows/signal / window seconds | HTTP | Queries/pages per run | Rows/run | Median ms | p95 ms | CPU ms/run | Alloc bytes/run | Sampled peak WS bytes | Runtime writes |")
            .AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");

        foreach (var scenario in report.Scenarios)
        {
            text.AppendLine(string.Join(" | ",
                $"| {scenario.Name}",
                $"{scenario.SignalCount} × {scenario.RowsPerSignal} / {scenario.WindowSeconds}s",
                scenario.ExpectedStatusCode.ToString(CultureInfo.InvariantCulture),
                $"{scenario.QueriesPerIteration}/{scenario.PagesPerIteration}",
                scenario.RowsPerIteration.ToString(CultureInfo.InvariantCulture),
                scenario.MedianLatencyMilliseconds.ToString("F3", CultureInfo.InvariantCulture),
                scenario.P95LatencyMilliseconds.ToString("F3", CultureInfo.InvariantCulture),
                scenario.ProcessCpuMillisecondsPerIteration.ToString("F3", CultureInfo.InvariantCulture),
                scenario.AllocatedBytesPerIteration.ToString(CultureInfo.InvariantCulture),
                scenario.PeakSampledWorkingSetBytes.ToString(CultureInfo.InvariantCulture),
                $"{scenario.RuntimeWrites} |"));
        }

        text.AppendLine();
        foreach (var limitation in report.Limitations)
            text.AppendLine($"- Limitation: {limitation}");
        text.AppendLine("- Peak working set is sampled every 5 ms and may miss shorter transients; timing/resource values are descriptive, not CI thresholds.");
        return text.ToString();
    }

    private sealed record Stage6BScenario(
        string Name,
        int SignalCount,
        int RowsPerSignal,
        TimeSpan Window,
        bool ActiveScope,
        bool Permit);

    private sealed record Stage6BScenarioResult(
        string Name,
        int SignalCount,
        int RowsPerSignal,
        long WindowSeconds,
        int ExpectedStatusCode,
        int WarmupIterations,
        int MeasuredIterations,
        IReadOnlyList<double> LatencyMilliseconds,
        double MedianLatencyMilliseconds,
        double P95LatencyMilliseconds,
        double ProcessCpuMillisecondsTotal,
        double ProcessCpuMillisecondsPerIteration,
        long AllocatedBytesTotal,
        long AllocatedBytesPerIteration,
        long PeakSampledWorkingSetBytes,
        int QueriesTotal,
        int QueriesPerIteration,
        int PagesTotal,
        int PagesPerIteration,
        long RowsTotal,
        long RowsPerIteration,
        int RuntimeWrites);

    private sealed record Stage6BResourceReport(
        int SchemaVersion,
        string Stage,
        string SourceSha,
        bool? GitTrackedFilesClean,
        string DotNetSdk,
        string Runtime,
        string OperatingSystem,
        string RunnerOs,
        string RunnerName,
        int ProcessorCount,
        int Seed,
        int ProfileMaximumSignals,
        int ProfileMaximumRowsPerWindow,
        int HistorianPageSize,
        int MaximumWindowDays,
        int WarmupIterations,
        int MeasuredIterations,
        int WorkingSetSampleIntervalMilliseconds,
        string Provider,
        IReadOnlyList<string> Limitations,
        IReadOnlyList<Stage6BScenarioResult> Scenarios);
}
