using System.Text.Json;
using Scada.Core.Intelligence.Experimental;

internal static class UnitChecks
{
    private static int passed;
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    public static void Run()
    {
        Check("identity covariance oracle", () => Near(0, InnovationWindowContrast.Calculate(Fourier(3)).Score!.Value));
        Check("level term oracle", () =>
        {
            var result = InnovationWindowContrast.Calculate(Fourier(3).Select(row => new[] { row[0] + 3, row[1], row[2] }).ToArray());
            Near(360, result.Level!.Value); Near(0, result.Covariance!.Value);
        });
        foreach (var multiplier in new[] { .5, 2d })
            Check($"dispersion {multiplier} formula oracle", () =>
            {
                var result = InnovationWindowContrast.Calculate(Fourier(3).Select(row => row.Select(v => v * multiplier).ToArray()).ToArray());
                Near(120 * (multiplier * multiplier - 1 - Math.Log(multiplier * multiplier)), result.Covariance!.Value);
                Near(0, result.Level!.Value);
            });
        Check("correlation sign contrast oracle", () =>
        {
            const double r = .65;
            double[][] before = [[1, 0], [r, Math.Sqrt(1 - r * r)]];
            var values = Fourier(2).Select(row => LinearAlgebra.SolveLower(before,
                [row[0], -r * row[0] + Math.Sqrt(1 - r * r) * row[1]])).ToArray();
            Near(40 * 4 * r * r / (1 - r * r), InnovationWindowContrast.Calculate(values).Covariance!.Value);
        });
        foreach (var dimensions in new[] { 2, 8 })
        {
            Check($"p={dimensions} independent full window", () => Near(0, InnovationWindowContrast.Calculate(Fourier(dimensions)).Score!.Value));
            Check($"p={dimensions} learned model finite", () =>
            {
                var reference = InnovationReference.Fit("unit", Runs(dimensions));
                var score = LastScore(reference, Rows(dimensions, 160, 991));
                True(score > 0 && double.IsFinite(score), "score unavailable");
            });
        }
        Check("positive affine units including offset", () =>
        {
            var fit = Runs(3); var evaluation = Rows(3, 100, 901);
            double[] factors = [1000, .01, 2]; double[] offsets = [200, -40, 7];
            double[] Convert(double[] row) => row.Select((value, j) => value * factors[j] + offsets[j]).ToArray();
            var a = LastScore(InnovationReference.Fit("unit", fit), evaluation);
            var b = LastScore(InnovationReference.Fit("unit", fit.Select(run => (IReadOnlyList<double[]>)run.Select(Convert).ToArray()).ToArray()), evaluation.Select(Convert).ToArray());
            Near(a, b, 1e-7);
        });
        Check("signal order invariance", () =>
        {
            var fit = Runs(3); var evaluation = Rows(3, 100, 902);
            double[] Permute(double[] row) => [row[2], row[0], row[1]];
            Near(LastScore(InnovationReference.Fit("unit", fit), evaluation),
                LastScore(InnovationReference.Fit("unit", fit.Select(run => (IReadOnlyList<double[]>)run.Select(Permute).ToArray()).ToArray()), evaluation.Select(Permute).ToArray()), 1e-7);
        });
        Check("input and snapshot cannot mutate fitted reference", () =>
        {
            var fit = Runs(3); var reference = InnovationReference.Fit("unit", fit); var evaluation = Rows(3, 100, 903);
            var before = LastScore(reference, evaluation); var data = reference.Describe();
            data.Centers[0] = 1e20; data.Lower[0][0] = 1e20; fit[0][0][0] = 1e20;
            Near(before, LastScore(reference, evaluation));
        });
        Check("snapshot restoration is deterministic", () =>
        {
            var reference = InnovationReference.Fit("unit", Runs(3)); var evaluation = Rows(3, 100, 904);
            Near(LastScore(reference, evaluation), LastScore(InnovationReference.Restore(reference.Describe()), evaluation));
        });
        Check("zero and collinear covariance fail", () =>
        {
            Throws<ArgumentException>(() => InnovationWindowContrast.Calculate(Enumerable.Range(0, 40).Select(_ => new double[3]).ToArray()));
            Throws<ArgumentException>(() => InnovationWindowContrast.Calculate(Fourier(2).Select(row => new[] { row[0], row[0] }).ToArray()));
            Throws<ArgumentException>(() => InnovationReference.Fit("unit", [Enumerable.Range(0, 80).Select(_ => new double[2]).ToArray()]));
        });
        Check("nonstationary reference fails", () => Throws<ArgumentException>(() => InnovationReference.Fit("unit",
            [Enumerable.Range(0, 1000).Select(i => new[] { (double)i, i * i * 1d }).ToArray()])));
        Check("nonfinite and overflow fail", () =>
        {
            var rows = Fourier(2); rows[0][0] = double.NaN;
            Throws<ArgumentException>(() => InnovationWindowContrast.Calculate(rows));
            Throws<ArgumentException>(() => InnovationWindowContrast.Calculate(Fourier(2).Select(row => row.Select(v => v * 1e300).ToArray()).ToArray()));
        });
        Check("incompatible persisted factor fails", () =>
        {
            var data = InnovationReference.Fit("unit", Runs(2)).Describe(); data.Lower[0][1] = .1;
            Throws<ArgumentException>(() => InnovationReference.Restore(data));
        });
        Check("warmup requires forty innovations and prior cohort", () =>
        {
            var detector = Detector(); var rows = Rows(2, 41, 905);
            for (var i = 0; i < 40; i++) True(detector.Push(Observation(i, rows[i])).Score is null, "partial window scored");
            True(detector.Push(Observation(40, rows[40])).Score.HasValue, "full window missing");
        });
        foreach (var invalid in new[] { "quality", "sync", "binding", "nan", "offset" })
            Check($"{invalid} evidence resets entire window", () =>
            {
                var detector = WarmDetector();
                var row = new[] { .5, .2 }; var at = Observation(81, row);
                at = invalid switch
                {
                    "quality" => at with { Good = false }, "sync" => at with { Synchronized = false },
                    "binding" => at with { Binding = "changed-context" }, "nan" => at with { Residuals = [double.NaN, .2] },
                    _ => at with { SourceTime = at.SourceTime.ToOffset(TimeSpan.FromHours(1)) }
                };
                True(detector.Push(at).Score is null, "invalid evidence scored");
                var rows = Rows(2, 41, 906);
                for (var i = 0; i < 40; i++) True(detector.Push(Observation(82 + i, rows[i])).Score is null, "reset history leaked");
                True(detector.Push(Observation(122, rows[40])).Score.HasValue, "recovery failed");
            });
        Check("source gap resets AR and ring", () =>
        {
            var detector = WarmDetector();
            True(detector.Push(Observation(100, [.5, .2])).Reason == "warmup", "gap did not reset");
        });
        Check("duplicate and reverse source times rejected", () =>
        {
            foreach (var time in new[] { 80, 79 })
                True(WarmDetector().Push(Observation(time, [.5, .2])).Reason == "duplicate_or_out_of_order_source_time", "bad source order accepted");
        });
        Check("complete five strict entry and retained-history rearm", () =>
        {
            var tracker = new InnovationEpisodeTracker();
            foreach (var value in new[] { 2d, 2, 2, 0 }) True(!tracker.Push(value, 1), "premature entry");
            True(tracker.Push(1, 1), "strict third above should enter in complete five");
            for (var i = 0; i < 5; i++) True(!tracker.Push(1, 1), "below/equal should terminate without entry");
            True(!tracker.Push(2, 1) && !tracker.Push(2, 1) && tracker.Push(2, 1), "rearm history lost");
            tracker.Push(null, 1);
            for (var i = 0; i < 4; i++) True(!tracker.Push(2, 1), "no-score failed to reset history");
            True(tracker.Push(2, 1), "entry after reset failed");
        });
        Check("run peak is exact 3-of-5 threshold statistic", () =>
        {
            double[] scores = [2, 7, 6, 1, 8, 3, 0, 9, 4, 5];
            var peak = Enumerable.Range(0, scores.Length - 4).Max(i => scores.Skip(i).Take(5).OrderDescending().ElementAt(2));
            for (var threshold = 0d; threshold < 10; threshold += .25)
            {
                var tracker = new InnovationEpisodeTracker(); var any = false;
                foreach (var score in scores) any |= tracker.Push(score, threshold);
                True(any == (peak > threshold), "episode calibration mismatch");
            }
        });
        Check("atomic claim does not overwrite or replay", () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "innovation-unit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "claim.json");
            try
            {
                Program.WriteNew(path, new { state = "claimed-before-generation", identity = Program.UnitIdentity });
                var before = File.ReadAllText(path);
                Throws<IOException>(() => Program.WriteNew(path, new { state = "replacement" }));
                True(File.ReadAllText(path) == before, "claim overwritten");
                True(JsonDocument.Parse(before).RootElement.GetProperty("state").GetString() == "claimed-before-generation", "claim state changed");
            }
            finally { File.Delete(path); Directory.Delete(directory); }
        });
        Check("context fit rejects rank-deficient source design", () => Throws<ArgumentException>(() =>
            Program.FitRegression(Enumerable.Range(0, 50).Select(i => new Program.SyntheticRow([i, i * 2, i * 3], [1, .5, 0])).ToArray())));
        Check("new namespace independent and deterministic", () =>
        {
            var a = Program.Generate(999, 0, space: Program.UnitIdentity); var b = Program.Generate(999, 0, space: Program.UnitIdentity);
            var c = Program.Generate(999, 0, space: Program.UnitIdentity + "-other");
            True(a[0].Targets.SequenceEqual(b[0].Targets), "seed not deterministic");
            True(!a[0].Targets.SequenceEqual(c[0].Targets), "namespaces overlap");
        });
        Check("fit/calibration/holdout identities and seeds disjoint", Program.AuditPartitions);
        Check("outlier sensitivity is explicit limitation", () =>
        {
            var rows = Fourier(2); rows[0][0] = 100;
            True(InnovationWindowContrast.Calculate(rows).Score > 1000, "Gaussian contrast unexpectedly treated as robust");
        });
        Check("nonlinear dependence not universally detected", () =>
        {
            // cos(2t) is a deterministic quadratic function of cos(t), yet mean/covariance are unchanged.
            Near(0, InnovationWindowContrast.Calculate(Fourier(2)).Score!.Value);
        });
        Console.WriteLine($"UNIT: {passed}/{passed} passed; {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
    }

    private static double[][] Fourier(int dimensions) => Enumerable.Range(0, 40).Select(t =>
        Enumerable.Range(1, dimensions).Select(j => Math.Sqrt(2) * Math.Cos(2 * Math.PI * j * t / 40)).ToArray()).ToArray();
    private static double[][] Rows(int dimensions, int count, int group)
    {
        var rng = new PortableRandom(Program.UnitIdentity, group); var state = new double[dimensions];
        return Enumerable.Range(0, count).Select(_ =>
        {
            for (var j = 0; j < dimensions; j++) state[j] = .4 * state[j] + rng.Normal();
            return state.ToArray();
        }).ToArray();
    }
    private static IReadOnlyList<IReadOnlyList<double[]>> Runs(int dimensions) =>
        Enumerable.Range(0, 4).Select(group => (IReadOnlyList<double[]>)Rows(dimensions, 160, group)).ToArray();
    private static InnovationObservation Observation(int index, double[] values) => new(Start.AddSeconds(index * 10), values, true, true, "unit");
    private static InnovationWindowContrast Detector() => new(InnovationReference.Fit("unit", Runs(2)), TimeSpan.FromSeconds(10));
    private static InnovationWindowContrast WarmDetector()
    {
        var detector = Detector(); var rows = Rows(2, 81, 909);
        for (var i = 0; i < rows.Length; i++) detector.Push(Observation(i, rows[i]));
        return detector;
    }
    private static double LastScore(InnovationReference reference, double[][] rows)
    {
        var detector = new InnovationWindowContrast(reference, TimeSpan.FromSeconds(10)); InnovationContrastResult? last = null;
        for (var i = 0; i < rows.Length; i++) last = detector.Push(Observation(i, rows[i]));
        return last?.Score ?? throw new InvalidOperationException("Expected complete score.");
    }
    private static void Check(string name, Action check) { check(); passed++; Console.WriteLine("PASS " + name); }
    private static void True(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static void Near(double expected, double actual, double tolerance = 1e-8) => True(double.IsFinite(actual) && Math.Abs(expected - actual) <= tolerance, $"Expected {expected:R}, actual {actual:R}");
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
