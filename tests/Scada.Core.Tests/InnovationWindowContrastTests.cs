using Scada.Core.Intelligence.Experimental;

namespace Scada.Core.Tests;

public sealed class InnovationWindowContrastTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public void IdentityCovarianceHasZeroContrast(int dimensions)
    {
        var result = InnovationWindowContrast.Calculate(Orthogonal(dimensions));
        Assert.Equal("available", result.Status);
        Assert.InRange(result.Score!.Value, 0, 1e-9);
    }

    [Theory]
    [InlineData(.5)]
    [InlineData(2)]
    public void CovarianceTermDetectsBothDirectionsWithIndependentFormula(double multiplier)
    {
        var rows = Orthogonal(3).Select(row => row.Select(value => value * multiplier).ToArray()).ToArray();
        var result = InnovationWindowContrast.Calculate(rows);
        var expected = 120 * (multiplier * multiplier - 1 - Math.Log(multiplier * multiplier));
        Assert.InRange(Math.Abs(result.Covariance!.Value - expected), 0, 1e-9);
        Assert.InRange(result.Level!.Value, 0, 1e-9);
    }

    [Fact]
    public void LevelContrastDoesNotAbsorbOrHideCovarianceTerm()
    {
        var rows = Orthogonal(3).Select(row => new[] { row[0] + 3, row[1], row[2] }).ToArray();
        var result = InnovationWindowContrast.Calculate(rows);
        Assert.InRange(Math.Abs(result.Level!.Value - 360), 0, 1e-9);
        Assert.InRange(result.Covariance!.Value, 0, 1e-9);
    }

    [Fact]
    public void OppositeCorrelationProducesExpectedJointContrastWithUnchangedMarginals()
    {
        const double r = .65;
        double[][] lower = [[1, 0], [r, Math.Sqrt(1 - r * r)]];
        var rows = Orthogonal(2).Select(row => LinearAlgebra.SolveLower(lower,
            [row[0], -r * row[0] + Math.Sqrt(1 - r * r) * row[1]])).ToArray();
        var result = InnovationWindowContrast.Calculate(rows);
        Assert.InRange(Math.Abs(result.Score!.Value - 160 * r * r / (1 - r * r)), 0, 1e-9);
    }

    [Fact]
    public void SingularAndNonfiniteWindowsReturnNoInventedScore()
    {
        Assert.Throws<ArgumentException>(() => InnovationWindowContrast.Calculate(Enumerable.Range(0, 40).Select(_ => new double[2]).ToArray()));
        var rows = Orthogonal(2); rows[0][0] = double.NaN;
        Assert.Throws<ArgumentException>(() => InnovationWindowContrast.Calculate(rows));
    }

    [Fact]
    public void FittedReferenceOwnsItsArraysAndRestoresWithoutNumericalChange()
    {
        var rows = ReferenceRows();
        var reference = InnovationReference.Fit("context-v1", [rows]);
        var restored = InnovationReference.Restore(reference.Describe());
        var snapshot = reference.Describe();
        snapshot.Centers[0] = 1e20; snapshot.Lower[0][0] = 1e20; rows[0][0] = 1e20;
        Assert.Equal(restored.Describe().Centers, reference.Describe().Centers);
        Assert.Equal(restored.Describe().Scales, reference.Describe().Scales);
    }

    [Theory]
    [InlineData("quality")]
    [InlineData("sync")]
    [InlineData("context")]
    [InlineData("time")]
    public void BadEvidenceClearsAllTemporalStateBeforeRecovery(string failure)
    {
        var reference = InnovationReference.Fit("context-v1", [ReferenceRows()]);
        var detector = new InnovationWindowContrast(reference, TimeSpan.FromSeconds(10));
        var rows = ReferenceRows();
        InnovationObservation At(int i) => new(DateTimeOffset.UnixEpoch.AddSeconds(i * 10), rows[i % rows.Length], true, true, "context-v1");
        for (var i = 0; i <= 50; i++) detector.Push(At(i));
        var invalid = failure switch
        {
            "quality" => At(51) with { Good = false }, "sync" => At(51) with { Synchronized = false },
            "context" => At(51) with { Binding = "context-v2" }, _ => At(50)
        };
        Assert.Null(detector.Push(invalid).Score);
        for (var i = 52; i < 92; i++) Assert.Null(detector.Push(At(i)).Score);
        Assert.NotNull(detector.Push(At(92)).Score);
    }

    [Fact]
    public void EpisodeEntryRequiresCompleteFiveAndStrictThreshold()
    {
        var tracker = new InnovationEpisodeTracker();
        Assert.False(tracker.Push(2, 1)); Assert.False(tracker.Push(2, 1));
        Assert.False(tracker.Push(2, 1)); Assert.False(tracker.Push(1, 1));
        Assert.True(tracker.Push(1, 1));
        for (var i = 0; i < 5; i++) Assert.False(tracker.Push(1, 1));
        Assert.False(tracker.Push(2, 1)); Assert.False(tracker.Push(2, 1)); Assert.True(tracker.Push(2, 1));
        Assert.False(tracker.Push(null, 1));
        for (var i = 0; i < 4; i++) Assert.False(tracker.Push(2, 1));
        Assert.True(tracker.Push(2, 1));
    }

    private static double[][] Orthogonal(int dimensions) => Enumerable.Range(0, 40).Select(t => Enumerable.Range(1, dimensions)
        .Select(j => Math.Sqrt(2) * Math.Cos(2 * Math.PI * j * t / 40)).ToArray()).ToArray();

    private static double[][] ReferenceRows()
    {
        var rng = new Random(1729);
        var state = new double[2];
        return Enumerable.Range(0, 240).Select(_ =>
        {
            for (var j = 0; j < 2; j++) state[j] = .4 * state[j] + rng.NextDouble() - .5;
            return state.ToArray();
        }).ToArray();
    }
}
