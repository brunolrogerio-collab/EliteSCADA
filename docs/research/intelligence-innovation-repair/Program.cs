using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Core.Intelligence.Experimental;

internal static class Program
{
    internal const string Identity = "ELITESCADA-INTELLIGENCE-INNOVATION-REPAIR-20261020-v1";
    internal const string UnitIdentity = Identity + "-UNIT";
    internal const string Binding = "synthetic/profile-v1/context-v1/residual-model-v1";
    internal static readonly string[] Scenarios = ["nonfault", "current-plus-3", "all-plus-3", "relationship-flip", "dispersion-2x", "dispersion-0.5x", "mixed"];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly Stopwatch Wall = Stopwatch.StartNew();
    private static readonly string Root = Path.Combine(Directory.GetCurrentDirectory(), "docs/research/intelligence-innovation-repair");
    private static readonly string Core = Path.Combine(Directory.GetCurrentDirectory(), "src/Scada.Core/Intelligence/Experimental/InnovationWindowContrast.cs");
    internal static readonly double[][] R0 = [[1, .65, .25], [.65, 1, .2], [.25, .2, 1]];
    internal static readonly double[][] R1 = [[1, -.65, .25], [-.65, 1, .2], [.25, .2, 1]];

    private static int Main(string[] args)
    {
        try
        {
            switch (args.FirstOrDefault())
            {
                case "unit": UnitChecks.Run(); break;
                case "fit": FitCalibration(args[1]); break;
                case "freeze": Freeze(); break;
                case "holdout": Holdout(); break;
                default: throw new ArgumentException("Use unit, fit <new-output>, freeze or holdout.");
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    private static void FitCalibration(string output)
    {
        AuditPartitions();
        var fit = Enumerable.Range(0, 60).Select(group => Generate(group, 0)).ToArray();
        var regression = FitRegression(fit.SelectMany(run => run).ToArray());
        var residuals = fit.Select(run => (IReadOnlyList<double[]>)run.Select(row => Residual(row, regression)).ToArray()).ToArray();
        var scales = Enumerable.Range(0, 3).Select(j =>
        {
            var values = residuals.SelectMany(run => run).Select(row => row[j]).ToArray();
            var center = Median(values);
            return 1.4826 * Median(values.Select(value => Math.Abs(value - center)).ToArray());
        }).ToArray();
        if (scales.Any(value => !double.IsFinite(value) || value <= 0)) throw new InvalidOperationException("Invalid fit residual MAD.");
        var reference = InnovationReference.Fit(Binding, residuals);
        var peaks = new double[2400];
        for (var i = 0; i < peaks.Length; i++)
        {
            var summary = Evaluate(Generate(10000 + i, 0), regression, reference, double.MaxValue);
            peaks[i] = summary.EntryPeak;
            if (summary.Scored != 80 || summary.Invalid != 0) throw new InvalidOperationException("Calibration has no-score or incomplete windows.");
            Budget();
        }
        var ordered = peaks.Order().ToArray();
        var threshold = ordered[(int)Math.Ceiling(.999 * ordered.Length) - 1];
        var numerical = new Numerical(reference.Describe(), regression, scales, threshold, peaks.Length,
            peaks.Count(value => value > threshold), peaks.Average(), peaks.Max());
        var artifact = new FitArtifact(Identity, "fit-calibration-complete", Hashes(), SourceCommit(), numerical, Resources());
        WriteNew(output, artifact);
        Console.WriteLine(JsonSerializer.Serialize(new { stage = "fit", threshold, artifact.Resources, output }, Json));
    }

    private static void Freeze()
    {
        var windows = Read<FitArtifact>("fit-calibration-windows-v1.json");
        var linux = Read<FitArtifact>("fit-calibration-linux-v1.json");
        var currentHashes = Hashes();
        if (windows.Identity != Identity || linux.Identity != Identity ||
            !Same(windows.Hashes, currentHashes) || !Same(linux.Hashes, currentHashes))
            throw new InvalidOperationException("Fit artifacts have incompatible source provenance.");
        var maxDifference = Compare(JsonSerializer.SerializeToElement(windows.Numerical), JsonSerializer.SerializeToElement(linux.Numerical));
        WriteNew(Path.Combine(Root, "fit-calibration-comparison-v1.json"), new { state = "MATCH", maxAbsoluteDifference = maxDifference, tolerance = 1e-9 });
        var frozen = new Frozen(Identity, "frozen-before-holdout", currentHashes, SourceCommit(), windows.Numerical,
            FileHash(Path.Combine(Root, "fit-calibration-windows-v1.json")), FileHash(Path.Combine(Root, "fit-calibration-linux-v1.json")));
        WriteNew(Path.Combine(Root, "frozen-config-v1.json"), frozen);
        Console.WriteLine($"Frozen before holdout; maximum numerical difference {maxDifference:R}");
    }

    private static void Holdout()
    {
        var frozenPath = Path.Combine(Root, "frozen-config-v1.json");
        var frozen = Read<Frozen>("frozen-config-v1.json");
        var aggregatePath = Path.Combine(Root, "holdout-aggregate-v1.json");
        var receiptPath = Path.Combine(Root, "holdout-receipt-v1.json");
        if (frozen.Identity != Identity || frozen.State != "frozen-before-holdout" || !Same(frozen.Hashes, Hashes()) ||
            frozen.WindowsArtifactSha256 != FileHash(Path.Combine(Root, "fit-calibration-windows-v1.json")) ||
            frozen.LinuxArtifactSha256 != FileHash(Path.Combine(Root, "fit-calibration-linux-v1.json")))
            throw new InvalidOperationException("Frozen source/configuration mismatch.");
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Only the single Windows holdout is authorized.");
        if (Git("status --porcelain").Length != 0 || File.Exists(aggregatePath) || File.Exists(receiptPath))
            throw new InvalidOperationException("Holdout requires a clean committed freeze and no existing receipt.");
        var commit = SourceCommit();
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EliteSCADA/research-holdout-claims");
        Directory.CreateDirectory(directory);
        var claimPath = Path.Combine(directory, HashText(Identity) + ".json");
        // Atomic and identity-scoped, before the first holdout generation. An interrupted claim blocks retry.
        WriteNew(claimPath, new { identity = Identity, state = "claimed-before-generation", sourceCommit = commit, frozenSha256 = FileHash(frozenPath), replayPermitted = false });
        var reference = InnovationReference.Restore(frozen.Numerical.Reference);
        var results = Scenarios.Select(_ => new List<RunResult>(1000)).ToArray();
        for (var i = 0; i < 1000; i++)
        {
            for (var scenario = 0; scenario < Scenarios.Length; scenario++)
                results[scenario].Add(Evaluate(Generate(20000 + i, scenario, frozen.Numerical.InjectionScales),
                    frozen.Numerical.Regression, reference, frozen.Numerical.Threshold));
            Budget();
        }
        var intervals = Bootstrap(results);
        var summaries = new List<object>();
        var normal = results[0];
        var scored = normal.Sum(run => run.Scored);
        var fp = normal.Sum(run => run.Exceedances);
        var episodes = normal.Sum(run => run.Episodes);
        var pointRate = (double)fp / scored;
        var episodeRate = episodes * 86400d / (scored * 10d);
        var commonPass = pointRate <= .01 && episodeRate <= 1;
        for (var scenario = 0; scenario < results.Length; scenario++)
        {
            var runs = results[scenario];
            var detected = runs.Count(run => run.Latency.HasValue);
            var timely = runs.Count(run => run.Latency is <= 600);
            var latencies = runs.Where(run => run.Latency.HasValue).Select(run => (double)run.Latency!.Value).Order().ToArray();
            var independentEligible = runs.Count(run => run.Invalid == 0 && run.Scored == 80);
            summaries.Add(new
            {
                scenario = Scenarios[scenario], groups = runs.Count, independentEligibleGroups = independentEligible,
                rawEligibleSeconds = runs.Count * 1200, warmupCohorts = runs.Sum(run => run.Warmup), scoredDecisions = runs.Sum(run => run.Scored),
                scoredEligibleSeconds = runs.Sum(run => run.Scored) * 10, availability = runs.Sum(run => run.Scored) / (runs.Count * 120d),
                invalidWindows = runs.Sum(run => run.Invalid), exceedances = runs.Sum(run => run.Exceedances), episodes = runs.Sum(run => run.Episodes),
                detectedGroups = scenario == 0 ? (int?)null : detected, timelyGroups = scenario == 0 ? (int?)null : timely,
                recall = scenario == 0 ? (double?)null : detected / 1000d, timelyFraction600s = scenario == 0 ? (double?)null : timely / 1000d,
                latencySeconds = latencies.Length == 0 ? null : new { p50 = Quantile(latencies, .5), p95 = Quantile(latencies, .95), max = latencies[^1] },
                interval95 = intervals[scenario],
                disposition = independentEligible < 100 ? "INSUFFICIENT" : scenario == 0 ? (commonPass ? "PASS_POINT" : "FAIL")
                    : (commonPass && detected >= 900 && timely >= 900 ? "PASS_POINT" : "FAIL")
            });
        }
        var artifact = new
        {
            identity = Identity, state = "single-consumption-complete", sourceCommitAtClaim = commit,
            frozenSha256 = FileHash(frozenPath), hashes = Hashes(),
            common = new { pointFalsePositiveRate = pointRate, falseEpisodesPer24ScoredHours = episodeRate, commonPass,
                groupOccurrenceWilson95 = Wilson(normal.Count(run => run.Episodes > 0), normal.Count) },
            scenarios = summaries, bootstrap = new { replicates = 2000, unit = "independent-source-group", pairedIndicesAcrossScenarios = true },
            resources = Resources(), limitation = "SYNTHETIC_ONLY; experimental internal core; product API, field performance, security and persistence NOT VALIDATED"
        };
        WriteNew(aggregatePath, artifact);
        WriteNew(receiptPath, new { identity = Identity, state = "consumed-once", consumptionCount = 1, replayPermitted = false,
            sourceCommitAtClaim = commit, frozenSha256 = FileHash(frozenPath), aggregateSha256 = FileHash(aggregatePath), claimPathSha256 = HashText(claimPath) });
        Console.WriteLine(JsonSerializer.Serialize(new { completed = true, commonPass, pointRate, episodeRate, artifact.resources }, Json));
    }

    internal static SyntheticRow[] Generate(int group, int scenario, double[]? scales = null, string space = Identity)
    {
        if (scenario is < 0 or > 6 || (scenario > 0 && scales?.Length != 3)) throw new ArgumentException("Undeclared scenario or missing frozen scale.");
        var rng = new PortableRandom(space, group);
        var first = LinearAlgebra.Cholesky(R0);
        var flipped = LinearAlgebra.Cholesky(R1);
        var state = new double[3];
        var rows = new SyntheticRow[120];
        for (var index = 0; index < rows.Length; index++)
        {
            var noise = Enumerable.Range(0, 3).Select(_ => rng.Normal()).ToArray();
            var factor = scenario is 3 or 6 && index >= 12 ? flipped : first;
            for (var j = 0; j < 3; j++)
                state[j] = .65 * state[j] + Math.Sqrt(1 - .65 * .65) * Enumerable.Range(0, j + 1).Sum(k => factor[j][k] * noise[k]);
            var speed = 1430 + 5 * (group % 9);
            double[] offsets = [-.05, .05, 0, .07];
            var load = Math.Clamp(.30 + .05 * (group % 9) + offsets[index / 30], .25, .80);
            double[] expected = [4 + 20 * load + .012 * (speed - 1450), 3 + 30 * load + .020 * (speed - 1450), 1.2 + 3 * load - .0015 * (speed - 1450)];
            double[] sd = [.7, 1, .1];
            var multiplier = index < 12 ? 1 : scenario == 4 ? 2 : scenario == 5 ? .5 : 1;
            var targets = Enumerable.Range(0, 3).Select(j => expected[j] + sd[j] * state[j] * multiplier).ToArray();
            if (index >= 12 && scenario is 1 or 2 or 6) targets[0] += 3 * scales![0];
            if (index >= 12 && scenario == 2) for (var j = 1; j < 3; j++) targets[j] += 3 * scales![j];
            rows[index] = new(targets, [1, load, (speed - 1450d) / 20d]);
        }
        return rows;
    }

    internal static RunResult Evaluate(SyntheticRow[] rows, double[][] regression, InnovationReference reference, double threshold)
    {
        var detector = new InnovationWindowContrast(reference, TimeSpan.FromSeconds(10));
        var episodes = new InnovationEpisodeTracker();
        var lastScores = new Queue<double>(5);
        var scored = 0; var invalid = 0; var warmup = 0; var above = 0; var episodeCount = 0; int? latency = null; var peak = 0d;
        for (var index = 0; index < rows.Length; index++)
        {
            var result = detector.Push(new(DateTimeOffset.UnixEpoch.AddSeconds(index * 10), Residual(rows[index], regression), true, true, Binding));
            if (result.Score.HasValue)
            {
                scored++;
                if (result.Score.Value > threshold) above++;
                lastScores.Enqueue(result.Score.Value);
                if (lastScores.Count > 5) lastScores.Dequeue();
                if (lastScores.Count == 5) peak = Math.Max(peak, lastScores.OrderDescending().ElementAt(2));
            }
            else
            {
                lastScores.Clear();
                if (result.Reason == "warmup") warmup++; else invalid++;
            }
            if (episodes.Push(result.Score, threshold))
            {
                episodeCount++;
                if (index >= 12 && !latency.HasValue) latency = index * 10 - 120;
            }
        }
        return new(scored, invalid, warmup, above, episodeCount, latency, peak);
    }

    internal static double[][] FitRegression(SyntheticRow[] rows)
    {
        var gram = Enumerable.Range(0, 3).Select(_ => new double[3]).ToArray();
        var right = Enumerable.Range(0, 3).Select(_ => new double[3]).ToArray();
        foreach (var row in rows)
            for (var i = 0; i < 3; i++)
                for (var j = 0; j < 3; j++)
                {
                    gram[i][j] += row.Features[i] * row.Features[j];
                    right[j][i] += row.Features[i] * row.Targets[j];
                }
        return right.Select(vector => Solve(gram, vector)).ToArray();
    }

    private static double[] Solve(double[][] matrix, double[] right)
    {
        var a = matrix.Select((row, i) => row.Concat([right[i]]).ToArray()).ToArray();
        var scale = matrix.SelectMany(row => row).Max(Math.Abs);
        for (var i = 0; i < right.Length; i++)
        {
            var pivot = Enumerable.Range(i, right.Length - i).MaxBy(j => Math.Abs(a[j][i]));
            (a[i], a[pivot]) = (a[pivot], a[i]);
            if (Math.Abs(a[i][i]) <= scale * 1e-12) throw new ArgumentException("Rank-deficient contextual fit.");
            var divisor = a[i][i];
            for (var k = i; k <= right.Length; k++) a[i][k] /= divisor;
            for (var j = 0; j < right.Length; j++) if (j != i)
            {
                var factor = a[j][i];
                for (var k = i; k <= right.Length; k++) a[j][k] -= factor * a[i][k];
            }
        }
        var result = a.Select(row => row[^1]).ToArray();
        if (result.Any(value => !double.IsFinite(value))) throw new ArgumentException("Nonfinite contextual fit.");
        return result;
    }

    internal static double[] Residual(SyntheticRow row, double[][] regression) => Enumerable.Range(0, 3)
        .Select(j => row.Targets[j] - Enumerable.Range(0, 3).Sum(k => row.Features[k] * regression[j][k])).ToArray();

    internal static void AuditPartitions()
    {
        var groups = Enumerable.Range(0, 60).Concat(Enumerable.Range(10000, 2400)).Concat(Enumerable.Range(20000, 1000)).ToArray();
        if (groups.Distinct().Count() != groups.Length || groups.Select(group => PortableRandom.Seed(Identity, group)).Distinct().Count() != groups.Length)
            throw new InvalidOperationException("Source group or derived seed identity collision.");
    }

    private static object[] Bootstrap(List<RunResult>[] runs)
    {
        var values = Enumerable.Range(0, runs.Length).Select(_ => Enumerable.Range(0, 4).Select(_ => new double[2000]).ToArray()).ToArray();
        var rng = new PortableRandom(Identity + "-BOOTSTRAP", 0);
        for (var b = 0; b < 2000; b++)
        {
            var indices = Enumerable.Range(0, 1000).Select(_ => rng.Index(1000)).ToArray();
            for (var scenario = 0; scenario < runs.Length; scenario++)
            {
                var selected = indices.Select(index => runs[scenario][index]).ToArray();
                values[scenario][0][b] = selected.Count(run => run.Latency.HasValue) / 1000d;
                values[scenario][1][b] = selected.Count(run => run.Latency is <= 600) / 1000d;
                values[scenario][2][b] = selected.Sum(run => run.Exceedances) / (double)selected.Sum(run => run.Scored);
                values[scenario][3][b] = selected.Sum(run => run.Episodes) * 86400d / (selected.Sum(run => run.Scored) * 10d);
            }
        }
        double[] Interval(double[] values) { Array.Sort(values); return [Quantile(values, .025), Quantile(values, .975)]; }
        return values.Select((scenarios, index) => (object)new { recall = index == 0 ? null : Interval(scenarios[0]),
            timely = index == 0 ? null : Interval(scenarios[1]), pointExceedanceRate = Interval(scenarios[2]), episodesPer24ScoredHours = Interval(scenarios[3]) }).ToArray();
    }

    private static double[] Wilson(int count, int total)
    {
        const double z = 1.959963984540054;
        var p = count / (double)total;
        var denominator = 1 + z * z / total;
        var center = (p + z * z / (2 * total)) / denominator;
        var half = z * Math.Sqrt(p * (1 - p) / total + z * z / (4d * total * total)) / denominator;
        return [Math.Max(0, center - half), Math.Min(1, center + half)];
    }

    internal static double Quantile(double[] ordered, double q) => ordered[Math.Clamp((int)Math.Ceiling(q * ordered.Length) - 1, 0, ordered.Length - 1)];
    internal static double Median(double[] values) { var ordered = values.Order().ToArray(); var n = ordered.Length; return n % 2 == 1 ? ordered[n / 2] : ordered[n / 2 - 1] / 2 + ordered[n / 2] / 2; }

    private static double Compare(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) throw new InvalidOperationException("Platform shape mismatch.");
        if (a.ValueKind == JsonValueKind.Number)
        {
            var x = a.GetDouble(); var y = b.GetDouble(); var difference = Math.Abs(x - y);
            if (!double.IsFinite(x) || !double.IsFinite(y) || difference > 1e-9 || difference > 1e-9 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y))))
                throw new InvalidOperationException("Platform numerical mismatch.");
            return difference;
        }
        if (a.ValueKind == JsonValueKind.Array)
        {
            if (a.GetArrayLength() != b.GetArrayLength()) throw new InvalidOperationException("Platform array mismatch.");
            return a.EnumerateArray().Zip(b.EnumerateArray(), Compare).DefaultIfEmpty().Max();
        }
        if (a.ValueKind == JsonValueKind.Object)
        {
            if (!a.EnumerateObject().Select(p => p.Name).SequenceEqual(b.EnumerateObject().Select(p => p.Name))) throw new InvalidOperationException("Platform property mismatch.");
            return a.EnumerateObject().Select(p => Compare(p.Value, b.GetProperty(p.Name))).DefaultIfEmpty().Max();
        }
        if (a.GetRawText() != b.GetRawText()) throw new InvalidOperationException("Platform value mismatch.");
        return 0;
    }

    private static Dictionary<string, string> Hashes() => new()
    {
        ["protocol"] = FileHash(Path.Combine(Root, "protocol-v1.md")), ["core"] = FileHash(Core),
        ["harness"] = FileHash(Path.Combine(Root, "Program.cs")), ["unit"] = FileHash(Path.Combine(Root, "UnitChecks.cs"))
    };
    private static bool Same(Dictionary<string, string> a, Dictionary<string, string> b) => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var value) && value == p.Value);
    private static T Read<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(Root, name)))!;
    private static string HashText(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    internal static void WriteNew(string path, object value)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(file, value, Json);
    }
    private static string SourceCommit() => Environment.GetEnvironmentVariable("STUDY_SOURCE_COMMIT") ?? Git("rev-parse HEAD");
    private static string Git(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments) { RedirectStandardOutput = true, UseShellExecute = false })!;
        var value = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Git evidence failed.");
        return value;
    }
    private static void Budget()
    {
        var process = Process.GetCurrentProcess();
        if (Wall.Elapsed.TotalMinutes > 20 || process.TotalProcessorTime.TotalMinutes > 15 || process.PeakWorkingSet64 > 512L * 1024 * 1024)
            throw new InvalidOperationException("Preregistered resource budget exceeded.");
    }
    private static object Resources()
    {
        Budget(); var process = Process.GetCurrentProcess();
        return new { wallSeconds = Wall.Elapsed.TotalSeconds, cpuSeconds = process.TotalProcessorTime.TotalSeconds,
            peakRssBytes = process.PeakWorkingSet64, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            linuxImage = Environment.GetEnvironmentVariable("STUDY_IMAGE_DIGEST") };
    }

    internal sealed record SyntheticRow(double[] Targets, double[] Features);
    internal sealed record RunResult(int Scored, int Invalid, int Warmup, int Exceedances, int Episodes, int? Latency, double EntryPeak);
    private sealed record Numerical(InnovationReferenceData Reference, double[][] Regression, double[] InjectionScales,
        double Threshold, int CalibrationGroups, int CalibrationGroupsAboveThreshold, double MeanRunEntryPeak, double MaxRunEntryPeak);
    private sealed record FitArtifact(string Identity, string State, Dictionary<string, string> Hashes, string SourceCommit, Numerical Numerical, object Resources);
    private sealed record Frozen(string Identity, string State, Dictionary<string, string> Hashes, string SourceCommit, Numerical Numerical,
        string WindowsArtifactSha256, string LinuxArtifactSha256);
}

internal sealed class PortableRandom
{
    private ulong state;
    public PortableRandom(string space, int group) => state = Seed(space, group);
    internal static ulong Seed(string space, int group) => BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"{space}:{group}")));
    private ulong Next()
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15;
            var z = state; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9; z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
            return z ^ (z >> 31);
        }
    }
    public double Uniform() => ((Next() >> 11) + .5) / (1UL << 53);
    public double Normal() { var sum = 0d; for (var i = 0; i < 12; i++) sum += Uniform(); return sum - 6; }
    public int Index(int count) => (int)(Uniform() * count);
}
