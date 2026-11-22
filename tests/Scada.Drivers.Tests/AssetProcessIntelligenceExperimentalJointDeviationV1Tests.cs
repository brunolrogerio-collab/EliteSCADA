using Scada.Api.Licensing;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using System.Text.Json;

namespace Scada.Drivers.Tests;

public sealed class AssetProcessIntelligenceExperimentalJointDeviationV1Tests
{
    private static readonly DateTimeOffset Epoch = DateTimeOffset.Parse("2026-10-14T00:00:00Z");

    [Fact]
    public void CatalogExplicitlyLeavesFieldValidationAndClassificationOpen()
    {
        var catalog = System.Text.Json.JsonSerializer.SerializeToElement(
            AssetProcessIntelligenceExperimentalJointDeviationV1Catalog.Describe());

        Assert.Equal("experimental_not_field_validated", catalog.GetProperty("validationStatus").GetString());
        Assert.Equal("none", catalog.GetProperty("classificationStatus").GetString());
        Assert.Equal(JsonValueKind.Null, catalog.GetProperty("decisionThreshold").ValueKind);
        Assert.Equal(40, catalog.GetProperty("windowLength").GetInt32());
        Assert.Contains("inside the requested window", catalog.GetProperty("duplicateTimestampRule").GetString());
    }

    [Fact]
    public void SyntheticReferenceAndEvaluationProduceDeterministicContextBoundJointScores()
    {
        var (targets, contexts, predicates, referenceRows, evaluationRows, referenceWindow, evaluationWindow) =
            BuildSimulation(referenceCount: 240, evaluationCount: 70);

        var result = AssetProcessIntelligenceExperimentalJointDeviationV1Calculator.Calculate(
            targets, contexts, predicates, Guid.NewGuid(), Guid.NewGuid(), 3,
            referenceWindow, evaluationWindow, 10, referenceRows, evaluationRows);

        Assert.Equal("available", result.ReferenceStatus);
        Assert.Null(result.ReferenceUnavailableReason);
        Assert.Equal(240, result.ReferenceCompleteCohortCount);
        Assert.Equal(1, result.ReferenceFitSegmentCount);
        Assert.Equal(70, result.EvaluationCompleteCohortCount);
        Assert.Equal(30, result.EvaluationScoredCohortCount);
        Assert.Equal(40, result.EvaluationWarmupCohortCount);
        Assert.Equal(30, result.Scores.Count);
        Assert.All(result.Scores, point =>
        {
            Assert.Equal("available", point.Status);
            Assert.True(point.Score > 0);
            Assert.Equal(2, point.TargetContributions!.Count);
            Assert.True(point.CrossSignalInteraction >= 0);
        });
        var repeat = AssetProcessIntelligenceExperimentalJointDeviationV1Calculator.Calculate(
            targets, contexts, predicates, Guid.NewGuid(), Guid.NewGuid(), 3,
            referenceWindow, evaluationWindow, 10, referenceRows, evaluationRows);
        Assert.Equal(result.Scores.Select(point => point.Score), repeat.Scores.Select(point => point.Score));
    }

    [Fact]
    public void ContextMismatchUnsynchronizedRowsAndDuplicateTimesCannotEnterJointCohort()
    {
        var (targets, contexts, predicates, referenceRows, evaluationRows, referenceWindow, evaluationWindow) =
            BuildSimulation(referenceCount: 240, evaluationCount: 70);
        var targetId = targets[0].Signal.Id;
        var contextId = contexts[0].Signal.Id;
        var targetRows = evaluationRows[targetId].ToList();
        var contextRows = evaluationRows[contextId].ToList();
        var excludedTimestamp = targetRows[4].SourceTimestampUtc!.Value;
        targetRows[4] = targetRows[4] with { SourceTimestampEvidence = "Unsynchronized" };
        targetRows.Add(targetRows[6] with { Value = HistoricalQueryValue.FromDouble(999) });
        var outOfWindowTimestamp = evaluationWindow.FromUtc.AddSeconds(-10);
        targetRows.Add(targetRows[10] with { SourceTimestampUtc = outOfWindowTimestamp });
        targetRows.Add(targetRows[11] with { SourceTimestampUtc = outOfWindowTimestamp });
        contextRows[7] = contextRows[7] with { Value = HistoricalQueryValue.FromDouble(70) };
        evaluationRows[targetId] = targetRows;
        evaluationRows[contextId] = contextRows;

        var result = AssetProcessIntelligenceExperimentalJointDeviationV1Calculator.Calculate(
            targets, contexts, predicates, Guid.NewGuid(), Guid.NewGuid(), 3,
            referenceWindow, evaluationWindow, 10, referenceRows, evaluationRows);

        Assert.Equal(67, result.EvaluationCompleteCohortCount);
        Assert.Equal(1, result.ReferenceFitSegmentCount);
        Assert.DoesNotContain(result.Scores, point => point.SourceTimestampUtc == excludedTimestamp);
        Assert.True(result.EvaluationWarmupCohortCount >= 42);
        var targetEvidence = result.SignalEvidence.Single(item => item.SignalId == targetId).Evaluation;
        Assert.Equal(73, targetEvidence.RawObservationCount);
        Assert.Equal(73, targetEvidence.UtcSourceTimestampCount);
        Assert.Equal(71, targetEvidence.InWindowSourceTimestampCount);
        Assert.Equal(70, targetEvidence.SynchronizedSourceTimestampCount);
        Assert.Equal(1, targetEvidence.AmbiguousSourceTimestampGroupCount);
        Assert.Equal(2, targetEvidence.AmbiguousSourceTimestampRowCount);
        Assert.True(targetEvidence.AcceptedSynchronizedUniqueCount == 68,
            $"accepted={targetEvidence.AcceptedSynchronizedUniqueCount}; good={targetEvidence.GoodQualityCount}; compatible={targetEvidence.CompatibleValueCount}; synchronized={targetEvidence.SynchronizedSourceTimestampCount}; duplicateGroups={targetEvidence.AmbiguousSourceTimestampGroupCount}; duplicateRows={targetEvidence.AmbiguousSourceTimestampRowCount}");
        var contextEvidence = result.SignalEvidence.Single(item => item.SignalId == contextId).Evaluation;
        Assert.Equal(70, contextEvidence.RawObservationCount);
        Assert.Equal(70, contextEvidence.AcceptedSynchronizedUniqueCount);
        Assert.Equal(69, contextEvidence.PredicateMatchCount);
    }

    [Fact]
    public void InadequateReferenceReturnsNoExperimentalScoreAndStableInsufficiency()
    {
        var (targets, contexts, predicates, referenceRows, evaluationRows, referenceWindow, evaluationWindow) =
            BuildSimulation(referenceCount: 80, evaluationCount: 70);

        var result = AssetProcessIntelligenceExperimentalJointDeviationV1Calculator.Calculate(
            targets, contexts, predicates, Guid.NewGuid(), Guid.NewGuid(), 3,
            referenceWindow, evaluationWindow, 10, referenceRows, evaluationRows);

        Assert.Equal("insufficient_data", result.ReferenceStatus);
        Assert.Equal("reference_fit_insufficient_or_invalid", result.ReferenceUnavailableReason);
        Assert.Empty(result.Scores);
        Assert.Equal(0, result.EvaluationScoredCohortCount);
        Assert.Equal(70, result.EvaluationWarmupCohortCount);
    }

    private static (
        IReadOnlyList<(AssetProcessIntelligenceSignalEngineeringDto Signal, TagDefinition Tag)> Targets,
        IReadOnlyList<(AssetProcessIntelligenceSignalEngineeringDto Signal, TagDefinition Tag)> Contexts,
        IReadOnlyList<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto> Predicates,
        Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>> ReferenceRows,
        Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>> EvaluationRows,
        AssetProcessIntelligenceAnalyticsWindow ReferenceWindow,
        AssetProcessIntelligenceAnalyticsWindow EvaluationWindow) BuildSimulation(int referenceCount, int evaluationCount)
    {
        var targetTags = new[] { NewTag("Current"), NewTag("Flow") };
        var contextTag = NewTag("DriveFrequency", "Hz");
        var targetSignals = targetTags.Select((tag, index) => new AssetProcessIntelligenceSignalEngineeringDto(
            Guid.Parse($"a4000000-0000-0000-0000-{index + 1:000000000000}"), $"target-{index + 1}", "simulation",
            tag.Id, Required: true)).ToArray();
        var contextSignal = new AssetProcessIntelligenceSignalEngineeringDto(
            Guid.Parse("a4000000-0000-0000-0000-000000000003"), "drive-frequency-context", "simulation",
            contextTag.Id, Required: true);
        var targets = targetSignals.Select((signal, index) => (signal, targetTags[index])).ToArray();
        var contexts = new[] { (contextSignal, contextTag) };
        var predicates = new[]
        {
            new AssetProcessIntelligenceOperatingContextPredicateEngineeringDto(
                contextTag.Id, AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval,
                Minimum: 0, Maximum: 60, EngineeringUnit: "Hz", EngineeringUnitConfirmed: true)
        };
        var referenceWindow = new AssetProcessIntelligenceAnalyticsWindow(Epoch, Epoch.AddHours(1));
        var evaluationWindow = new AssetProcessIntelligenceAnalyticsWindow(Epoch.AddHours(2), Epoch.AddHours(3));
        var reference = BuildRows(targets, contexts[0], referenceCount, referenceWindow, seed: 712);
        var evaluation = BuildRows(targets, contexts[0], evaluationCount, evaluationWindow, seed: 912);
        return (targets, contexts, predicates, reference, evaluation, referenceWindow, evaluationWindow);
    }

    private static Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>> BuildRows(
        IReadOnlyList<(AssetProcessIntelligenceSignalEngineeringDto Signal, TagDefinition Tag)> targets,
        (AssetProcessIntelligenceSignalEngineeringDto Signal, TagDefinition Tag) context,
        int count,
        AssetProcessIntelligenceAnalyticsWindow window,
        int seed)
    {
        var random = new Random(seed);
        var state = new double[2];
        var rows = targets.ToDictionary(item => item.Signal.Id,
            _ => (IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>)new List<AssetProcessIntelligenceContextualDeviationVectorRawObservation>());
        var contextRows = new List<AssetProcessIntelligenceContextualDeviationVectorRawObservation>();
        for (var index = 0; index < count; index++)
        {
            var timestamp = window.FromUtc.AddSeconds((index + 1) * 10);
            var shared = random.NextDouble() - 0.5;
            state[0] = 0.4 * state[0] + shared;
            state[1] = 0.3 * state[1] + 0.35 * shared + random.NextDouble() - 0.5;
            for (var target = 0; target < targets.Count; target++)
            {
                var value = HistoricalQueryValue.FromDouble(state[target]);
                ((List<AssetProcessIntelligenceContextualDeviationVectorRawObservation>)rows[targets[target].Signal.Id])
                    .Add(new(timestamp, "Good", value, timestamp, "Synchronized"));
            }
            contextRows.Add(new(timestamp, "Good", HistoricalQueryValue.FromDouble(30), timestamp, "Synchronized"));
        }
        rows.Add(context.Signal.Id, contextRows);
        return rows;
    }

    private static TagDefinition NewTag(string name, string? unit = "A") => new(
        Guid.NewGuid(), name, $"Plant.Pump.{name}", TagDataType.Double, null, unit, null, true);
}
