using Scada.Core.Intelligence.Experimental;

namespace Scada.Core.Tests;

public sealed class ExperimentalJointDeviationTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public void IdentityWindowHasNoMaterialJointDeviation(int signalCount)
    {
        var result = ExperimentalJointDeviationDetector.CalculateWindow(Orthogonal(signalCount));

        Assert.Equal("available", result.Status);
        Assert.InRange(result.Score!.Value, 0, 1e-8);
        Assert.InRange(result.LevelComponent!.Value, 0, 1e-8);
        Assert.InRange(result.RelationshipComponent!.Value, 0, 1e-8);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2.0)]
    public void JointScoreDetectsScaleChangeAgainstIndependentFormula(double multiplier)
    {
        var result = ExperimentalJointDeviationDetector.CalculateWindow(
            Orthogonal(3).Select(row => row.Select(value => value * multiplier).ToArray()).ToArray());
        var expected = 120 * (multiplier * multiplier - 1 - Math.Log(multiplier * multiplier));

        Assert.InRange(Math.Abs(result.RelationshipComponent!.Value - expected), 0, 1e-8);
        Assert.InRange(result.LevelComponent!.Value, 0, 1e-8);
    }

    [Fact]
    public void JointScoreDetectsChangedCrossSignalRelationshipWhenMarginalsStayFixed()
    {
        const double correlation = 0.65;
        var independent = Orthogonal(2);
        var correlated = independent.Select(row => new[]
        {
            row[0], correlation * row[0] + Math.Sqrt(1 - correlation * correlation) * row[1]
        }).ToArray();

        var result = ExperimentalJointDeviationDetector.CalculateWindow(correlated);
        var expected = -ExperimentalJointDeviationDetector.WindowLength * Math.Log(1 - correlation * correlation);

        Assert.InRange(Math.Abs(result.RelationshipComponent!.Value - expected), 0, 1e-8);
        Assert.InRange(result.LevelComponent!.Value, 0, 1e-8);
    }

    [Fact]
    public void LevelComponentIsSeparateFromRelationshipComponent()
    {
        var shifted = Orthogonal(3).Select(row => new[] { row[0] + 3, row[1], row[2] }).ToArray();
        var result = ExperimentalJointDeviationDetector.CalculateWindow(shifted);

        Assert.InRange(Math.Abs(result.LevelComponent!.Value - 360), 0, 1e-8);
        Assert.InRange(result.RelationshipComponent!.Value, 0, 1e-8);
    }

    [Fact]
    public void SingularAndNonfiniteWindowsFailClosedInsteadOfInventingAScore()
    {
        Assert.Throws<ArgumentException>(() => ExperimentalJointDeviationDetector.CalculateWindow(
            Enumerable.Range(0, ExperimentalJointDeviationDetector.WindowLength).Select(_ => new double[2]).ToArray()));
        var rows = Orthogonal(2);
        rows[0][0] = double.NaN;
        Assert.Throws<ArgumentException>(() => ExperimentalJointDeviationDetector.CalculateWindow(rows));
    }

    [Fact]
    public void FitRequiresAContiguousContextBoundReferenceAndIsDeterministic()
    {
        var referenceRows = ReferenceRows(1729, 240);
        var first = ExperimentalJointDeviationReference.Fit("profile-42:v3", [referenceRows]);
        var second = ExperimentalJointDeviationReference.Fit("profile-42:v3", [referenceRows]);
        Assert.Equal(2, first.SignalCount);
        Assert.Equal(first.SignalCount, second.SignalCount);

        Assert.Throws<ArgumentException>(() => ExperimentalJointDeviationReference.Fit("profile-42:v3", [referenceRows.Take(20).ToArray()]));
        Assert.Throws<ArgumentException>(() => ExperimentalJointDeviationReference.Fit("profile-42:v3", [
            referenceRows.Select(row => new[] { row[0], 1.0 }).ToArray()
        ]));
    }

    [Theory]
    [InlineData("quality")]
    [InlineData("synchronization")]
    [InlineData("context")]
    [InlineData("duplicate-time")]
    [InlineData("cadence-gap")]
    public void InvalidCohortOrTimeBreakResetsWarmupBeforeScoreCanResume(string failure)
    {
        var referenceRows = ReferenceRows(81, 240);
        var reference = ExperimentalJointDeviationReference.Fit("profile-42:v3", [referenceRows]);
        var detector = new ExperimentalJointDeviationDetector(reference, TimeSpan.FromSeconds(10));
        var sourceTime = DateTimeOffset.UnixEpoch;
        var index = 0;

        ExperimentalJointDeviationScore Push(bool good = true, bool synchronized = true, string? binding = null, int timeStep = 1)
        {
            sourceTime = sourceTime.AddSeconds(10 * timeStep);
            var values = referenceRows[index++ % referenceRows.Length].ToArray();
            return detector.Push(new ExperimentalJointDeviationObservation(sourceTime, values, good, synchronized, binding ?? "profile-42:v3"));
        }

        for (var row = 0; row < 50; row++) Push();
        var invalid = failure switch
        {
            "quality" => Push(good: false),
            "synchronization" => Push(synchronized: false),
            "context" => Push(binding: "profile-42:v4"),
            "duplicate-time" => detector.Push(new ExperimentalJointDeviationObservation(
                sourceTime, referenceRows[index++ % referenceRows.Length], true, true, "profile-42:v3")),
            _ => Push(timeStep: 2)
        };
        Assert.Null(invalid.Score);

        var rowsAfterBreak = failure == "cadence-gap"
            ? ExperimentalJointDeviationDetector.WindowLength - 1
            : ExperimentalJointDeviationDetector.WindowLength;
        for (var row = 0; row < rowsAfterBreak; row++)
            Assert.Null(Push().Score);
        Assert.Equal("available", Push().Status);
    }

    private static double[][] Orthogonal(int signalCount) =>
        Enumerable.Range(0, ExperimentalJointDeviationDetector.WindowLength)
            .Select(time => Enumerable.Range(1, signalCount)
                .Select(signal => Math.Sqrt(2) * Math.Cos(2 * Math.PI * signal * time / ExperimentalJointDeviationDetector.WindowLength))
                .ToArray()).ToArray();

    private static double[][] ReferenceRows(int seed, int count)
    {
        var random = new Random(seed);
        var state = new double[2];
        var rows = new double[count][];
        for (var index = 0; index < count; index++)
        {
            var firstNoise = random.NextDouble() - 0.5;
            var secondNoise = random.NextDouble() - 0.5;
            state[0] = 0.4 * state[0] + firstNoise;
            state[1] = 0.3 * state[1] + 0.35 * firstNoise + 0.65 * secondNoise;
            rows[index] = [state[0], state[1]];
        }
        return rows;
    }
}
