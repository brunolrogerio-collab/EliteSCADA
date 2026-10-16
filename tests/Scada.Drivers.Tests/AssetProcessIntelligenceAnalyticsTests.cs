using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Historian;
using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;
using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class AssetProcessIntelligenceAnalyticsTests
{
    private static readonly DateTimeOffset FromUtc = DateTimeOffset.Parse("2026-10-14T00:00:00Z");

    [Fact]
    public void Catalog_IsVersionedAndBackedByImmutableCollections()
    {
        Assert.Equal(1, AssetProcessIntelligenceKpiCatalogV1.CatalogVersion);
        Assert.Equal("1.0.0", AssetProcessIntelligenceKpiCatalogV1.RuleVersion);
        Assert.IsNotType<HashSet<int>>(AssetProcessIntelligenceKpiCatalogV1.AllowedBucketSizesSeconds);
        var buckets = Assert.IsAssignableFrom<ISet<int>>(AssetProcessIntelligenceKpiCatalogV1.AllowedBucketSizesSeconds);
        Assert.Throws<NotSupportedException>(() => buckets.Add(123));
        Assert.DoesNotContain(123, buckets);
        Assert.Contains(900, AssetProcessIntelligenceKpiCatalogV1.AllowedBucketSizesSeconds);
        Assert.Contains(AssetProcessIntelligenceKpiCatalogV1.Rules, rule => rule.Id == "sample.mean.good.v1");
        Assert.Contains(AssetProcessIntelligenceKpiCatalogV1.Rules, rule => rule.Kind == "raw");
        Assert.All(
            AssetProcessIntelligenceKpiCatalogV1.Rules.Where(rule => rule.Kind == "derived"),
            rule => Assert.Equal("1.0.0", rule.Version));
    }

    [Fact]
    public void ContextualDeviationVectorV1Catalog_IsBoundedAndDisclaimsInference()
    {
        var catalog = JsonSerializer.SerializeToElement(AssetProcessIntelligenceContextualDeviationVectorV1Catalog.Describe());

        Assert.Equal(1, catalog.GetProperty("contractVersion").GetInt32());
        Assert.Equal("contextual-deviation-vector.v1", catalog.GetProperty("ruleId").GetString());
        Assert.Equal(2, catalog.GetProperty("minimumTargetSignals").GetInt32());
        Assert.Equal(8, catalog.GetProperty("maximumTargetSignals").GetInt32());
        Assert.Equal(50000, catalog.GetProperty("maximumRawRowsAcrossAllSignalsAndWindows").GetInt32());
        Assert.Equal(500, catalog.GetProperty("maximumReturnedEvaluationVectors").GetInt32());
        Assert.Equal("not_assessed", catalog.GetProperty("regimeEquivalence").GetString());
        Assert.Equal("unknown", catalog.GetProperty("completeness").GetString());
        Assert.Contains("no nearest join", catalog.GetProperty("alignment").GetString(), StringComparison.Ordinal);
        Assert.Contains("source timestamp ascending", catalog.GetProperty("ordering").GetString(), StringComparison.Ordinal);
        Assert.Contains("no combined anomaly", catalog.GetProperty("interpretation").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ContextualDeviationVectorV1_UsesOneAlignedCohortAndStableTargetAndTimestampOrdering()
    {
        var targetA = VectorSignal(Guid.NewGuid(), "target-a");
        var targetB = VectorSignal(Guid.NewGuid(), "target-b");
        var condition = VectorSignal(Guid.NewGuid(), "condition");
        var referenceWindow = Window(FromUtc, FromUtc.AddHours(1));
        var evaluationWindow = Window(FromUtc.AddHours(2), FromUtc.AddHours(3));
        var referenceTimes = Enumerable.Range(1, 30).Select(index => FromUtc.AddMinutes(index)).ToArray();
        var evaluationTimes = new[] { FromUtc.AddHours(2).AddMinutes(2), FromUtc.AddHours(2).AddMinutes(1) };
        var referenceRows = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] = referenceTimes.Select((timestamp, index) => VectorRow(timestamp, index + 1d)).ToArray(),
            [targetB.SignalId] = referenceTimes.Select((timestamp, index) => VectorRow(timestamp, index + 100d)).ToArray(),
            [condition.SignalId] = referenceTimes.Select(timestamp => VectorRow(timestamp, 50d)).ToArray()
        };
        var evaluationRows = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] = [VectorRow(evaluationTimes[0], 40d), VectorRow(evaluationTimes[1], 999d)],
            [targetB.SignalId] = [VectorRow(evaluationTimes[0], 140d), VectorRow(evaluationTimes[1], 999d)],
            [condition.SignalId] = [VectorRow(evaluationTimes[0], 50d), VectorRow(evaluationTimes[1], 51d)]
        };

        var result = AssetProcessIntelligenceContextualDeviationVectorV1Calculator.Calculate(
            [targetB, targetA], condition, referenceWindow, evaluationWindow, referenceRows, evaluationRows,
            new ControlledEngineeringUnitDefinition("unit.frequency.hertz", "frequency", "Hz", 1d), 50d, 51d);

        Assert.Equal(30, result.CompleteReferenceVectorCount);
        Assert.Equal(30, result.InRangeReferenceVectorCount);
        Assert.Equal(2, result.CompleteEvaluationVectorCount);
        Assert.Equal(new[] { evaluationTimes[1], evaluationTimes[0] }.Order(), result.EvaluationVectors.Select(vector => vector.SourceTimestampUtc));
        Assert.Equal(new[] { targetB.SignalId, targetA.SignalId }, result.EvaluationVectors[0].Targets.Select(value => value.TargetSignalId));
        var targetBStats = Assert.Single(result.TargetStatistics, item => item.Signal.SignalId == targetB.SignalId);
        Assert.Equal(114.5d, targetBStats.ReferenceMedian);
        Assert.Equal(7.5d, targetBStats.ReferenceMad);
        var targetAStats = Assert.Single(result.TargetStatistics, item => item.Signal.SignalId == targetA.SignalId);
        Assert.Equal(15.5d, targetAStats.ReferenceMedian);
        Assert.Equal(7.5d, targetAStats.ReferenceMad);
        Assert.False(result.EvaluationVectors[0].ConditionInRange);
        var firstA = Assert.Single(result.EvaluationVectors[1].Targets, value => value.TargetSignalId == targetA.SignalId);
        Assert.Equal(24.5d, firstA.RawDifferenceFromReferenceMedian);
        Assert.Equal(0.67448975d * 24.5d / 7.5d, firstA.RobustScore!.Value, precision: 10);
        Assert.True(result.EvaluationVectors[1].ConditionInRange);
    }

    [Fact]
    public void ContextualDeviationVectorV1_RejectsBadEvidenceAndAppliesInclusiveExclusiveConditionAndWindowBounds()
    {
        var targetA = VectorSignal(Guid.NewGuid(), "target-a");
        var targetB = VectorSignal(Guid.NewGuid(), "target-b");
        var condition = VectorSignal(Guid.NewGuid(), "condition");
        var referenceWindow = Window(FromUtc, FromUtc.AddMinutes(10));
        var evaluationWindow = Window(FromUtc.AddHours(2), FromUtc.AddHours(2).AddMinutes(10));
        var duplicate = FromUtc.AddMinutes(5);
        var referenceRows = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] =
            [
                VectorRow(referenceWindow.FromUtc, 1d), VectorRow(referenceWindow.ToUtc, 2d),
                VectorRow(duplicate, "Good", 3d, duplicate, "Synchronized"),
                VectorRow(duplicate, "Good", 4d, duplicate, "Synchronized"),
                VectorRow(FromUtc.AddMinutes(1), "Good", 5d, null, "Synchronized"),
                VectorRow(FromUtc.AddMinutes(2), "Good", 6d, FromUtc.AddMinutes(2), "NotRecorded"),
                VectorRow(FromUtc.AddMinutes(3), "Good", 7d, referenceWindow.ToUtc.AddSeconds(1), "Synchronized"),
                VectorRow(FromUtc.AddMinutes(4), "BadCommunication", 8d, FromUtc.AddMinutes(4), "Synchronized"),
                new(FromUtc.AddMinutes(6), "Good", new HistoricalQueryValue(HistoricalValueKind.Double, "NaN"),
                    FromUtc.AddMinutes(6), "Synchronized")
            ],
            [targetB.SignalId] = [VectorRow(referenceWindow.FromUtc, 10d), VectorRow(referenceWindow.ToUtc, 20d), VectorRow(duplicate, 30d)],
            [condition.SignalId] = [VectorRow(referenceWindow.FromUtc, 10d), VectorRow(referenceWindow.ToUtc, 20d), VectorRow(duplicate, 15d)]
        };
        var evaluationRows = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] = [VectorRow(evaluationWindow.FromUtc, 11d), VectorRow(evaluationWindow.ToUtc, 12d)],
            [targetB.SignalId] = [VectorRow(evaluationWindow.FromUtc, 21d), VectorRow(evaluationWindow.ToUtc, 22d)],
            [condition.SignalId] = [VectorRow(evaluationWindow.FromUtc, 10d), VectorRow(evaluationWindow.ToUtc, 20d)]
        };

        var result = AssetProcessIntelligenceContextualDeviationVectorV1Calculator.Calculate(
            [targetA, targetB], condition, referenceWindow, evaluationWindow, referenceRows, evaluationRows,
            new ControlledEngineeringUnitDefinition("unit.frequency.hertz", "frequency", "Hz", 1d), 10d, 20d);

        Assert.Equal(2, result.CompleteReferenceVectorCount);
        Assert.Equal(1, result.InRangeReferenceVectorCount);
        Assert.Equal(2, result.CompleteEvaluationVectorCount);
        Assert.Equal(2, result.EvaluationVectors.Count);
        Assert.True(result.EvaluationVectors[0].ConditionInRange);
        Assert.False(result.EvaluationVectors[1].ConditionInRange);
        Assert.NotNull(Assert.Single(result.EvaluationVectors[0].Targets, value => value.TargetSignalId == targetA.SignalId).RawDifferenceFromReferenceMedian);
        var outsideRange = Assert.Single(result.EvaluationVectors[1].Targets, value => value.TargetSignalId == targetA.SignalId);
        Assert.Null(outsideRange.RawDifferenceFromReferenceMedian);
        Assert.Equal("condition_outside_requested_range", outsideRange.ScoreUnavailableReason);
        var evidence = Assert.Single(result.ReferenceEvidence, item => item.SignalId == targetA.SignalId);
        Assert.Equal(9, evidence.RawObservationCount);
        Assert.Equal(2, evidence.AcceptedSynchronizedCount);
        Assert.Equal(7, evidence.RejectedObservationCount);
        Assert.Equal(2, evidence.AmbiguousSourceTimestampRowCount);
        Assert.Equal(2, evidence.RejectionCounts["duplicate_source_timestamp"]);
        Assert.Equal(1, evidence.RejectionCounts["source_timestamp_missing"]);
        Assert.Equal(1, evidence.RejectionCounts["source_timestamp_not_synchronized"]);
        Assert.Equal(1, evidence.RejectionCounts["source_timestamp_outside_requested_window"]);
        Assert.Equal(1, evidence.RejectionCounts["quality_not_good"]);
        Assert.Equal(1, evidence.RejectionCounts["value_not_finite_numeric"]);
    }

    [Fact]
    public void ContextualDeviationVectorV1_ReportsSparseCohortAndZeroMadWithoutScoring()
    {
        var targetA = VectorSignal(Guid.NewGuid(), "target-a");
        var targetB = VectorSignal(Guid.NewGuid(), "target-b");
        var condition = VectorSignal(Guid.NewGuid(), "condition");
        var referenceWindow = Window(FromUtc, FromUtc.AddHours(1));
        var evaluationWindow = Window(FromUtc.AddHours(2), FromUtc.AddHours(3));
        var sparseTimeA = FromUtc.AddMinutes(1);
        var sparseTimeB = FromUtc.AddMinutes(2);
        var sparseReference = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] = [VectorRow(sparseTimeA, 10d)],
            [targetB.SignalId] = [VectorRow(sparseTimeB, 20d)],
            [condition.SignalId] = [VectorRow(sparseTimeA, 50d), VectorRow(sparseTimeB, 50d)]
        };
        var oneEvaluationTime = evaluationWindow.FromUtc.AddMinutes(1);
        var evaluation = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] = [VectorRow(oneEvaluationTime, 15d)],
            [targetB.SignalId] = [VectorRow(oneEvaluationTime, 25d)],
            [condition.SignalId] = [VectorRow(oneEvaluationTime, 50d)]
        };
        var unit = new ControlledEngineeringUnitDefinition("unit.frequency.hertz", "frequency", "Hz", 1d);
        var sparse = AssetProcessIntelligenceContextualDeviationVectorV1Calculator.Calculate(
            [targetA, targetB], condition, referenceWindow, evaluationWindow, sparseReference, evaluation, unit, 49d, 51d);

        Assert.Equal(0, sparse.CompleteReferenceVectorCount);
        Assert.Equal(0, sparse.InRangeReferenceVectorCount);
        var sparseValue = Assert.Single(sparse.EvaluationVectors[0].Targets, item => item.TargetSignalId == targetA.SignalId);
        Assert.Null(sparseValue.RawDifferenceFromReferenceMedian);
        Assert.Equal("no_complete_in_range_reference_vectors", sparseValue.ScoreUnavailableReason);

        var smallTimes = new[] { FromUtc.AddMinutes(1), FromUtc.AddMinutes(2) };
        var smallReference = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] = [VectorRow(smallTimes[0], 10d), VectorRow(smallTimes[1], 12d)],
            [targetB.SignalId] = [VectorRow(smallTimes[0], 20d), VectorRow(smallTimes[1], 22d)],
            [condition.SignalId] = [VectorRow(smallTimes[0], 50d), VectorRow(smallTimes[1], 50d)]
        };
        var small = AssetProcessIntelligenceContextualDeviationVectorV1Calculator.Calculate(
            [targetA, targetB], condition, referenceWindow, evaluationWindow, smallReference, evaluation, unit, 49d, 51d);
        var smallValue = Assert.Single(small.EvaluationVectors[0].Targets, item => item.TargetSignalId == targetA.SignalId);
        Assert.Equal(2, small.CompleteReferenceVectorCount);
        Assert.Equal(11d, Assert.Single(small.TargetStatistics, item => item.Signal.SignalId == targetA.SignalId).ReferenceMedian);
        Assert.Equal(4d, smallValue.RawDifferenceFromReferenceMedian);
        Assert.Null(smallValue.RobustScore);
        Assert.Equal("fewer_than_30_complete_reference_vectors", smallValue.ScoreUnavailableReason);

        var constantTimes = Enumerable.Range(1, 30).Select(index => FromUtc.AddMinutes(index)).ToArray();
        var constantReference = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] = constantTimes.Select(timestamp => VectorRow(timestamp, 10d)).ToArray(),
            [targetB.SignalId] = constantTimes.Select(timestamp => VectorRow(timestamp, 20d)).ToArray(),
            [condition.SignalId] = constantTimes.Select(timestamp => VectorRow(timestamp, 50d)).ToArray()
        };
        var constant = AssetProcessIntelligenceContextualDeviationVectorV1Calculator.Calculate(
            [targetA, targetB], condition, referenceWindow, evaluationWindow, constantReference, evaluation, unit, 49d, 51d);

        var constantValue = Assert.Single(constant.EvaluationVectors[0].Targets, item => item.TargetSignalId == targetA.SignalId);
        Assert.Equal(0d, Assert.Single(constant.TargetStatistics, item => item.Signal.SignalId == targetA.SignalId).ReferenceMad);
        Assert.Equal(5d, constantValue.RawDifferenceFromReferenceMedian);
        Assert.Null(constantValue.RobustScore);
        Assert.Equal("reference_mad_zero", constantValue.ScoreUnavailableReason);
    }

    [Fact]
    public void ContextualDeviationVectorV1_CapsReturnedVectorsAndKeepsStableTimeOrder()
    {
        var targetA = VectorSignal(Guid.NewGuid(), "target-a");
        var targetB = VectorSignal(Guid.NewGuid(), "target-b");
        var condition = VectorSignal(Guid.NewGuid(), "condition");
        var referenceWindow = Window(FromUtc, FromUtc.AddHours(1));
        var evaluationWindow = Window(FromUtc.AddHours(2), FromUtc.AddHours(3));
        var referenceTimes = Enumerable.Range(1, 30).Select(index => FromUtc.AddMinutes(index)).ToArray();
        var evaluationTimes = Enumerable.Range(1, 501).Select(index => evaluationWindow.FromUtc.AddSeconds(index)).ToArray();
        var referenceRows = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] = referenceTimes.Select((timestamp, index) => VectorRow(timestamp, index + 1d)).ToArray(),
            [targetB.SignalId] = referenceTimes.Select((timestamp, index) => VectorRow(timestamp, index + 10d)).ToArray(),
            [condition.SignalId] = referenceTimes.Select(timestamp => VectorRow(timestamp, 50d)).ToArray()
        };
        var evaluationRows = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] = evaluationTimes.Reverse().Select((timestamp, index) => VectorRow(timestamp, index + 100d)).ToArray(),
            [targetB.SignalId] = evaluationTimes.Reverse().Select((timestamp, index) => VectorRow(timestamp, index + 200d)).ToArray(),
            [condition.SignalId] = evaluationTimes.Reverse().Select(timestamp => VectorRow(timestamp, 50d)).ToArray()
        };

        var result = AssetProcessIntelligenceContextualDeviationVectorV1Calculator.Calculate(
            [targetA, targetB], condition, referenceWindow, evaluationWindow, referenceRows, evaluationRows,
            new ControlledEngineeringUnitDefinition("unit.frequency.hertz", "frequency", "Hz", 1d), 49d, 51d);

        Assert.Equal(501, result.CompleteEvaluationVectorCount);
        Assert.Equal(500, result.EvaluationVectors.Count);
        Assert.Equal(1, result.OmittedEvaluationVectors);
        Assert.Equal(evaluationTimes[0], result.EvaluationVectors[0].SourceTimestampUtc);
        Assert.Equal(evaluationTimes[499], result.EvaluationVectors[^1].SourceTimestampUtc);
    }

    [Fact]
    public void ContextualDeviationVectorV1_ConvertsConditionToCanonicalBeforeApplyingRange()
    {
        var targetA = VectorSignal(Guid.NewGuid(), "target-a");
        var targetB = VectorSignal(Guid.NewGuid(), "target-b");
        var condition = VectorSignal(Guid.NewGuid(), "condition");
        var referenceWindow = Window(FromUtc, FromUtc.AddMinutes(10));
        var evaluationWindow = Window(FromUtc.AddHours(2), FromUtc.AddHours(2).AddMinutes(10));
        var referenceTimes = new[] { FromUtc.AddMinutes(1), FromUtc.AddMinutes(2) };
        var evaluationTimes = new[] { evaluationWindow.FromUtc.AddMinutes(1), evaluationWindow.FromUtc.AddMinutes(2) };
        var reference = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] = referenceTimes.Select(timestamp => VectorRow(timestamp, 10d)).ToArray(),
            [targetB.SignalId] = referenceTimes.Select(timestamp => VectorRow(timestamp, 20d)).ToArray(),
            [condition.SignalId] = referenceTimes.Select(timestamp => VectorRow(timestamp, 0.05d)).ToArray()
        };
        var evaluation = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetA.SignalId] = evaluationTimes.Select((timestamp, index) => VectorRow(timestamp, 11d + index)).ToArray(),
            [targetB.SignalId] = evaluationTimes.Select((timestamp, index) => VectorRow(timestamp, 21d + index)).ToArray(),
            [condition.SignalId] =
            [
                VectorRow(evaluationTimes[0], "Good", 0.05d, evaluationTimes[0], "Synchronized"),
                VectorRow(evaluationTimes[1], "Good", 0.051d, evaluationTimes[1], "Synchronized")
            ]
        };

        var result = AssetProcessIntelligenceContextualDeviationVectorV1Calculator.Calculate(
            [targetA, targetB], condition, referenceWindow, evaluationWindow, reference, evaluation,
            new ControlledEngineeringUnitDefinition("unit.power.kilowatt", "power", "W", 1000d), 49d, 51d);

        Assert.Equal(2, result.CompleteReferenceVectorCount);
        Assert.Equal(2, result.InRangeReferenceVectorCount);
        Assert.Equal(2, result.CompleteEvaluationVectorCount);
        Assert.True(result.EvaluationVectors[0].ConditionInRange);
        Assert.Equal(50d, result.EvaluationVectors[0].ConditionValueCanonical);
        Assert.False(result.EvaluationVectors[1].ConditionInRange);
        Assert.Equal(51d, result.EvaluationVectors[1].ConditionValueCanonical);
    }

    [Fact]
    public void NetObservedRateV1Catalog_IsSeparateVersionedAndImmutable()
    {
        Assert.Equal(1, AssetProcessIntelligenceNetObservedRateV1Catalog.ContractVersion);
        Assert.Equal(1, AssetProcessIntelligenceNetObservedRateV1Catalog.CatalogVersion);
        Assert.Equal("net-observed-rate.v1", AssetProcessIntelligenceNetObservedRateV1Catalog.RuleId);
        Assert.Equal("1.0.0", AssetProcessIntelligenceNetObservedRateV1Catalog.RuleVersion);
        var rule = Assert.Single(AssetProcessIntelligenceNetObservedRateV1Catalog.Rules);
        Assert.Equal("net-observed-rate.v1", rule.Id);
        Assert.Contains("Historian observation timestamps", rule.Formula, StringComparison.Ordinal);
        Assert.Contains("without inference, conversion, or normalization", rule.UnitSemantics, StringComparison.Ordinal);
        var rules = Assert.IsAssignableFrom<IList<AssetProcessIntelligenceKpiRule>>(
            AssetProcessIntelligenceNetObservedRateV1Catalog.Rules);
        Assert.Throws<NotSupportedException>(() => rules.Add(rule));

        var described = JsonSerializer.SerializeToElement(AssetProcessIntelligenceNetObservedRateV1Catalog.Describe());
        Assert.Equal(1, described.GetProperty("catalogVersion").GetInt32());
        Assert.Equal("Good", described.GetProperty("acceptedQuality").GetString());
        Assert.Equal(31, described.GetProperty("maximumWindowDays").GetInt32());
        Assert.Equal(50000, described.GetProperty("maximumRowsPerWindow").GetInt32());
        Assert.Equal(200, described.GetProperty("pageSize").GetInt32());
        Assert.Equal(
            new[] { "available", "insufficient_data", "unsupported" },
            described.GetProperty("statuses").EnumerateArray().Select(item => item.GetString()));
    }

    [Theory]
    [InlineData(2d, 8d, 3d)]
    [InlineData(8d, 2d, -3d)]
    [InlineData(4d, 4d, 0d)]
    public void NetObservedRateV1_CalculatesOnlyTheNetEndpointRate(double firstValue, double lastValue, double expectedRate)
    {
        var metric = AssetProcessIntelligenceNetObservedRateV1Calculator.Calculate(
        [
            Observation(FromUtc.AddSeconds(2), "Good", HistoricalQueryValue.FromDouble(lastValue)),
            Observation(FromUtc, "Good", HistoricalQueryValue.FromDouble(firstValue))
        ],
        "bar");

        Assert.Equal("available", metric.Status);
        Assert.Empty(metric.ReasonCodes);
        Assert.Equal(expectedRate, metric.Value);
        Assert.Equal("bar/s", metric.Unit);
        Assert.Equal(2d, metric.ElapsedSeconds);
        Assert.Equal(2, metric.RawObservationCount);
        Assert.Equal(2, metric.AcceptedSampleCount);
        Assert.Equal(0, metric.RejectedSampleCount);
        Assert.Equal(FromUtc, metric.FirstAccepted!.TimestampUtc);
        Assert.Equal(firstValue, metric.FirstAccepted.Value);
        Assert.Equal(FromUtc.AddSeconds(2), metric.LastAccepted!.TimestampUtc);
        Assert.Equal(lastValue, metric.LastAccepted.Value);
    }

    [Fact]
    public void NetObservedRateV1_RejectsPoorUnknownAndNonnumericObservationsWithoutLosingCounts()
    {
        var metric = AssetProcessIntelligenceNetObservedRateV1Calculator.Calculate(
        [
            Observation(FromUtc, "Good", HistoricalQueryValue.FromDouble(10)),
            Observation(FromUtc.AddSeconds(1), "Uncertain", HistoricalQueryValue.FromDouble(500)),
            Observation(FromUtc.AddSeconds(2), "Goodish", HistoricalQueryValue.FromDouble(500)),
            Observation(FromUtc.AddSeconds(3), "Good", HistoricalQueryValue.FromString("not numeric")),
            Observation(FromUtc.AddSeconds(4), "Good", new HistoricalQueryValue(HistoricalValueKind.Number, "NaN")),
            Observation(FromUtc.AddSeconds(5), "Good", new HistoricalQueryValue(HistoricalValueKind.Number, "Infinity")),
            Observation(FromUtc.AddSeconds(6), null, HistoricalQueryValue.FromDouble(900))
        ],
        "bar");

        Assert.Equal("insufficient_data", metric.Status);
        Assert.Null(metric.Value);
        Assert.Contains("fewer_than_two_accepted_good_finite_samples", metric.ReasonCodes);
        Assert.Equal(7, metric.RawObservationCount);
        Assert.Equal(1, metric.AcceptedSampleCount);
        Assert.Equal(6, metric.RejectedSampleCount);
        Assert.Equal(4, metric.QualityCounts["Good"]);
        Assert.Equal(1, metric.QualityCounts["Uncertain"]);
        Assert.Equal(1, metric.QualityCounts["Missing"]);
        Assert.Equal(1, metric.QualityCounts["Unrecognized"]);
        Assert.Equal(10d, metric.FirstAccepted!.Value);
        Assert.Equal(10d, metric.LastAccepted!.Value);
    }

    [Fact]
    public void NetObservedRateV1_MissingUnitIsUnsupportedAndNeverFabricatesOne()
    {
        var metric = AssetProcessIntelligenceNetObservedRateV1Calculator.Calculate(
        [
            Observation(FromUtc, "Good", HistoricalQueryValue.FromDouble(1)),
            Observation(FromUtc.AddSeconds(2), "Good", HistoricalQueryValue.FromDouble(5))
        ],
        " ");

        Assert.Equal("unsupported", metric.Status);
        Assert.Contains("engineering_unit_missing", metric.ReasonCodes);
        Assert.Null(metric.Value);
        Assert.Null(metric.Unit);
        Assert.Equal(2, metric.AcceptedSampleCount);
        Assert.Equal(FromUtc, metric.FirstAccepted!.TimestampUtc);
        Assert.Equal(FromUtc.AddSeconds(2), metric.LastAccepted!.TimestampUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NetObservedRateV1_NonPositiveEndpointElapsedTimeIsInsufficient(int elapsedSeconds)
    {
        var metric = AssetProcessIntelligenceNetObservedRateV1Calculator.CalculateFromEndpoints(
            2,
            2,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["Good"] = 2 },
            new AssetProcessIntelligenceNetObservedRateV1Endpoint(FromUtc.AddSeconds(10), 1),
            new AssetProcessIntelligenceNetObservedRateV1Endpoint(FromUtc.AddSeconds(10 + elapsedSeconds), 3),
            "bar");

        Assert.Equal("insufficient_data", metric.Status);
        Assert.Contains("non_positive_endpoint_elapsed_seconds", metric.ReasonCodes);
        Assert.Null(metric.Value);
        Assert.Equal((double)elapsedSeconds, metric.ElapsedSeconds);
    }

    [Fact]
    public void NetObservedRateV1_NonFiniteArithmeticIsInsufficientRatherThanInfinity()
    {
        var metric = AssetProcessIntelligenceNetObservedRateV1Calculator.Calculate(
        [
            Observation(FromUtc, "Good", HistoricalQueryValue.FromDouble(-double.MaxValue)),
            Observation(FromUtc.AddSeconds(1), "Good", HistoricalQueryValue.FromDouble(double.MaxValue))
        ],
        "bar");

        Assert.Equal("insufficient_data", metric.Status);
        Assert.Contains("non_finite_calculated_rate", metric.ReasonCodes);
        Assert.Null(metric.Value);
    }

    [Fact]
    public async Task NetObservedRateV1_UsesHistorianTimestampAndPreservesStablePagingTieOrder()
    {
        using var fixture = Fixture.Create();
        var firstTimestamp = FromUtc.AddSeconds(1);
        var lastTimestamp = FromUtc.AddSeconds(601);
        fixture.Historian.Respond = request => request.Page!.Cursor is null
            ? Response(request, Enumerable.Range(0, 200)
                .Select(index => Row(fixture.Tag.Id, firstTimestamp, "Good",
                    HistoricalQueryValue.FromInt32(index == 0 ? 2 : index == 199 ? 4 : 3)))
                .ToArray(), "stable-next")
            : Response(request,
            [
                Row(fixture.Tag.Id, lastTimestamp, "Good", HistoricalQueryValue.FromInt32(20)),
                Row(fixture.Tag.Id, lastTimestamp, "Good", HistoricalQueryValue.FromInt32(22))
            ]);

        var result = await fixture.ExecuteNetObservedRateV1Async();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceNetObservedRateV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(2, fixture.Historian.Calls);
        Assert.Equal(HistoricalDatasets.HistorianSamples, fixture.Historian.Requests[0].Dataset);
        Assert.Equal("timestamp", Assert.Single(fixture.Historian.Requests[0].OrderBy!).Field);
        Assert.Equal(HistoricalSortDirection.Ascending, fixture.Historian.Requests[0].OrderBy![0].Direction);
        Assert.Equal(200, fixture.Historian.Requests[0].Page!.Size);
        Assert.Equal("stable-next", fixture.Historian.Requests[1].Page!.Cursor);
        Assert.Equal("available", payload.Metric.Status);
        Assert.Equal(fixture.Request.Window, payload.Window);
        Assert.Equal(Fixture.ProjectKey, payload.ProjectKey);
        Assert.Equal(fixture.Request.TagId, payload.TagId);
        Assert.Equal(22d, payload.Metric.LastAccepted!.Value);
        Assert.Equal(2d, payload.Metric.FirstAccepted!.Value);
        Assert.Equal(20d / 600d, payload.Metric.Value);
        Assert.Equal("bar/s", payload.Metric.Unit);
        Assert.Equal(202, payload.Metric.RawObservationCount);
        Assert.Equal("timestamp", payload.Provenance.TimestampBasis.Split('`')[1]);
        Assert.Contains("source/server timestamps are not used", payload.Provenance.TimestampBasis, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, true, true, true, 403)]
    [InlineData(true, false, true, true, 403)]
    [InlineData(true, true, false, true, 409)]
    [InlineData(true, true, true, false, 409)]
    public async Task NetObservedRateV1_RequiresEntitlementTrendUseTagReadPermitAndActiveScope(
        bool licensed,
        bool grantTagRead,
        bool grantPermit,
        bool activeScope,
        int expectedStatus)
    {
        using var fixture = Fixture.Create(licensed: licensed, grantTagRead: grantTagRead,
            grantPermit: grantPermit, activeScope: activeScope);

        var result = await fixture.ExecuteNetObservedRateV1Async();

        AssertStatus(expectedStatus, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task NetObservedRateV1_RequiresTrendUseAndCoherentSavedWorkspaceRevision()
    {
        using (var noTrend = Fixture.Create(grantTrendUse: false))
        {
            var denied = await noTrend.ExecuteNetObservedRateV1Async();
            AssertStatus(StatusCodes.Status403Forbidden, denied);
            Assert.Equal(0, noTrend.Historian.Calls);
        }

        using (var mismatch = Fixture.Create(workspaceRevision: 41, runtimeRevision: 42))
        {
            var rejected = await mismatch.ExecuteNetObservedRateV1Async();
            AssertStatus(StatusCodes.Status409Conflict, rejected);
            Assert.Equal(0, mismatch.Historian.Calls);
        }

        using (var dirty = Fixture.Create())
        {
            dirty.Workspace.Tags.Upsert(dirty.Tag with { Description = "unsaved" });
            var rejected = await dirty.ExecuteNetObservedRateV1Async();
            AssertStatus(StatusCodes.Status409Conflict, rejected);
            Assert.Equal(0, dirty.Historian.Calls);
        }
    }

    [Fact]
    public async Task NetObservedRateV1_PermitCancellationDuringReadReturnsNoPartialMetric()
    {
        using var fixture = Fixture.Create();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (request, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(request,
                [Row(fixture.Tag.Id, FromUtc.AddSeconds(1), "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var task = fixture.ExecuteNetObservedRateV1Async();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceNetObservedRateV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task NetObservedRateV1_TagReadRevocationDuringReadDiscardsRows()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request =>
        {
            var prior = fixture.Authority.Snapshot();
            var role = Assert.Single(prior.Roles);
            fixture.Authority.TryReplaceAsync(
                prior.Version,
                [role with { Grants = [new CapabilityGrantEngineeringDto(SecurityCapability.TrendUse)] }],
                prior.Scopes).GetAwaiter().GetResult();
            return Response(request,
            [
                Row(fixture.Tag.Id, FromUtc.AddSeconds(1), "Good", HistoricalQueryValue.FromDouble(5)),
                Row(fixture.Tag.Id, FromUtc.AddSeconds(2), "Good", HistoricalQueryValue.FromDouble(6))
            ]);
        };

        var result = await fixture.ExecuteNetObservedRateV1Async();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceNetObservedRateV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task NetObservedRateV1_AtRowLimitReturnsCompleteResultButNoPartialResultWhenMoreRemain()
    {
        using var complete = Fixture.Create();
        complete.Historian.Respond = request =>
        {
            var page = complete.Historian.Calls - 1;
            var cursor = page == 249 ? null : $"page-{page + 1}";
            return Response(request, Enumerable.Range(0, 200)
                .Select(_ => Row(complete.Tag.Id, FromUtc.AddSeconds(1), "Good", HistoricalQueryValue.FromInt32(1)))
                .ToArray(), cursor);
        };

        var completed = await complete.ExecuteNetObservedRateV1Async();

        AssertStatus(StatusCodes.Status200OK, completed);
        var completePayload = Assert.IsType<AssetProcessIntelligenceNetObservedRateV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(completed).Value);
        Assert.Equal(50000, completePayload.Metric.RawObservationCount);
        Assert.Equal("insufficient_data", completePayload.Metric.Status);
        Assert.Equal(250, complete.Historian.Calls);

        using var truncated = Fixture.Create();
        truncated.Historian.Respond = request =>
        {
            var page = truncated.Historian.Calls - 1;
            return Response(request, Enumerable.Range(0, 200)
                .Select(_ => Row(truncated.Tag.Id, FromUtc.AddSeconds(1), "Good", HistoricalQueryValue.FromInt32(1)))
                .ToArray(), $"page-{page + 1}");
        };

        var rejected = await truncated.ExecuteNetObservedRateV1Async();

        AssertStatus(StatusCodes.Status413PayloadTooLarge, rejected);
        Assert.Equal(250, truncated.Historian.Calls);
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(rejected).Value);
        Assert.Equal("INTELLIGENCE_NET_OBSERVED_RATE_WINDOW_TOO_DENSE", error.Code);
    }

    [Fact]
    public void KpiV1_FiltersQualityAndNonnumericValuesAndOrdersFirstLastByHistorianTimestamp()
    {
        var window = Window(FromUtc, FromUtc.AddHours(1));
        var rows = new[]
        {
            Observation(FromUtc.AddMinutes(20), "Good", HistoricalQueryValue.FromDouble(2)),
            Observation(FromUtc.AddMinutes(10), "Good", HistoricalQueryValue.FromDouble(4)),
            Observation(FromUtc.AddMinutes(15), "Uncertain", HistoricalQueryValue.FromDouble(99)),
            Observation(FromUtc.AddMinutes(30), "Good", HistoricalQueryValue.FromString("not numeric")),
            Observation(FromUtc.AddMinutes(40), "BadCommunication", HistoricalQueryValue.FromInt32(8)),
            Observation(FromUtc.AddMinutes(50), null, new HistoricalQueryValue(HistoricalValueKind.Number, "NaN"))
        };

        var summary = AssetProcessIntelligenceKpiV1.Calculate(rows, window, 900);

        Assert.Equal(6, summary.RawObservationCount);
        Assert.Equal(2, summary.AcceptedSampleCount);
        Assert.Equal(4, summary.RejectedSampleCount);
        Assert.Equal(3, summary.QualityCounts["Good"]);
        Assert.Equal(1, summary.QualityCounts["Uncertain"]);
        Assert.Equal(1, summary.QualityCounts["BadCommunication"]);
        Assert.Equal(1, summary.QualityCounts["Missing"]);
        Assert.Equal(2, summary.ValidSampleCount);
        Assert.Equal(2d, summary.Minimum);
        Assert.Equal(4d, summary.Maximum);
        Assert.Equal(3d, summary.SampleMean);
        Assert.Equal(FromUtc.AddMinutes(10), summary.FirstObserved!.TimestampUtc);
        Assert.Equal(4d, summary.FirstObserved.Value);
        Assert.Equal("sample.first.good.v1", summary.FirstObserved.RuleId);
        Assert.Equal(FromUtc.AddMinutes(20), summary.LastObserved!.TimestampUtc);
        Assert.Equal(2d, summary.LastObserved.Value);
        Assert.Equal("sample.last.good.v1", summary.LastObserved.RuleId);
        Assert.Equal(-2d, summary.Delta);
        Assert.Equal("unknown", summary.Completeness);
        Assert.Null(summary.ExpectedSampleCount);
        Assert.Equal(4, summary.Trend.Sum(bucket => bucket.RejectedSampleCount));
        Assert.Equal(2, summary.Trend.Sum(bucket => bucket.AcceptedSampleCount));
    }

    [Fact]
    public void KpiV1_PreservesStage3AQualityNormalizationWhenAcceptingGoodNumericSamples()
    {
        var window = Window(FromUtc, FromUtc.AddMinutes(15));
        var rows = new[]
        {
            Observation(FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromInt32(1)),
            Observation(FromUtc.AddMinutes(2), " good ", HistoricalQueryValue.FromInt32(2)),
            Observation(FromUtc.AddMinutes(3), "gOoD", HistoricalQueryValue.FromInt32(3)),
            Observation(FromUtc.AddMinutes(4), " Good ", HistoricalQueryValue.FromInt32(4)),
            Observation(FromUtc.AddMinutes(5), " Goodish ", HistoricalQueryValue.FromInt32(5))
        };

        var summary = AssetProcessIntelligenceKpiV1.Calculate(rows, window, 900);

        Assert.Equal(4, summary.AcceptedSampleCount);
        Assert.Equal(1, summary.RejectedSampleCount);
        Assert.Equal(4, summary.QualityCounts["Good"]);
        Assert.Equal(1, summary.QualityCounts["Unrecognized"]);
        Assert.True(AssetProcessIntelligenceKpiV1.TryGetAcceptedGoodNumericValue(rows[1], out var normalizedValue));
        Assert.Equal(2d, normalizedValue);
        Assert.False(AssetProcessIntelligenceKpiV1.TryGetAcceptedGoodNumericValue(rows[4], out _));
    }

    [Fact]
    public void KpiV1_EmptyAndSparseWindowsKeepEmptyBucketsAndUnknownCompleteness()
    {
        var empty = AssetProcessIntelligenceKpiV1.Calculate(
            Array.Empty<AssetProcessIntelligenceRawObservation>(),
            Window(FromUtc, FromUtc.AddMinutes(30)),
            900);

        Assert.Equal(0, empty.RawObservationCount);
        Assert.Equal(0, empty.ValidSampleCount);
        Assert.Null(empty.SampleMean);
        Assert.Null(empty.FirstObserved);
        Assert.Null(empty.LastObserved);
        Assert.Null(empty.Delta);
        Assert.Equal(2, empty.Trend.Count);
        Assert.All(empty.Trend, bucket => Assert.Equal(0, bucket.RawObservationCount));
        Assert.Equal("unknown", empty.Completeness);

        var sparse = AssetProcessIntelligenceKpiV1.Calculate(
            [Observation(FromUtc.AddMinutes(20), "Good", HistoricalQueryValue.FromInt32(7))],
            Window(FromUtc, FromUtc.AddMinutes(30)),
            900);
        Assert.Equal(2, sparse.Trend.Count);
        Assert.Equal(1, sparse.Trend[1].AcceptedSampleCount);
        Assert.Equal(0, sparse.Trend[0].AcceptedSampleCount);
    }

    [Fact]
    public void KpiV1_PreservesProviderOrderForEqualTimestampFirstAndLast()
    {
        var timestamp = FromUtc.AddMinutes(5);
        var summary = AssetProcessIntelligenceKpiV1.Calculate(
        [
            Observation(timestamp, "Good", HistoricalQueryValue.FromInt32(1)),
            Observation(timestamp, "Good", HistoricalQueryValue.FromInt32(2))
        ],
        Window(FromUtc, FromUtc.AddMinutes(15)),
        900);

        Assert.Equal(1d, summary.FirstObserved!.Value);
        Assert.Equal(2d, summary.LastObserved!.Value);
    }

    [Fact]
    public async Task DirtyWorkspaceReturnsConflictBeforeAnyHistorianQuery()
    {
        using var fixture = Fixture.Create();
        fixture.Workspace.Tags.Upsert(fixture.Tag with { Description = "unsaved" });

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task WorkspaceRuntimeRevisionMismatchReturnsConflictBeforeAnyHistorianQuery()
    {
        using var fixture = Fixture.Create(workspaceRevision: 41, runtimeRevision: 42);

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task NonnumericActiveTagIsNotQueried()
    {
        using var fixture = Fixture.Create(tagDataType: TagDataType.String);

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status404NotFound, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Theory]
    [InlineData(false, true, true, true, 403)]
    [InlineData(true, false, true, true, 403)]
    [InlineData(true, true, false, true, 409)]
    [InlineData(true, true, true, false, 409)]
    public async Task EntitlementTrendUseTagReadAndPermitAreIndependentlyRequired(
        bool licensed,
        bool tagRead,
        bool permit,
        bool activeScope,
        int expectedStatus)
    {
        using var fixture = Fixture.Create(licensed: licensed, grantTagRead: tagRead,
            grantPermit: permit, activeScope: activeScope);

        var result = await fixture.ExecuteAsync();

        AssertStatus(expectedStatus, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task MissingTrendUseStopsBeforeHistorianQuery()
    {
        using var fixture = Fixture.Create(grantTagRead: true, grantTrendUse: false);

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task SuccessfulReadUsesCanonicalDatasetAndReturnsProvenanceAndDerivedMetrics()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request => Response(
            request,
            [
                Row(fixture.Tag.Id, FromUtc.AddMinutes(20), "Good", HistoricalQueryValue.FromInt32(8)),
                Row(fixture.Tag.Id, FromUtc.AddMinutes(10), "Good", HistoricalQueryValue.FromInt32(2))
            ]);

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(HistoricalDatasets.HistorianSamples, fixture.Historian.Requests[0].Dataset);
        Assert.Equal(HistoricalFilterOperator.Eq, Assert.Single(fixture.Historian.Requests[0].Filters!).Operator);
        Assert.Equal("Plant.Pump.Pressure", payload.TagPath);
        Assert.Equal("bar", payload.Unit);
        Assert.Single(payload.Provenance.Assignments);
        Assert.Equal(2, payload.Period.Summary.RawObservationCount);
        Assert.Equal(2, payload.Period.Summary.AcceptedSampleCount);
        Assert.Equal(5d, payload.Period.Summary.SampleMean);
        Assert.Equal(6d, payload.Period.Summary.Delta);
        Assert.Equal("not_assessed", payload.Provenance.RegimeEquivalence);
        Assert.Equal("unknown", payload.Period.Summary.Completeness);
    }

    [Fact]
    public async Task ProfileCoverageReturnsPerSignalDescriptiveTrendsAndExactHistorianTimestampOverlap()
    {
        using var fixture = Fixture.Create();
        var secondTag = fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            if (tagId == fixture.Tag.Id)
                return Response(request,
                [
                    Row(tagId, FromUtc.AddMinutes(10), "Good", HistoricalQueryValue.FromDouble(1)),
                    Row(tagId, FromUtc.AddMinutes(20), "BadCommunication", HistoricalQueryValue.FromDouble(99))
                ]);
            Assert.Equal(secondTag.Id, tagId);
            return Response(request,
            [
                Row(tagId, FromUtc.AddMinutes(10), "Good", HistoricalQueryValue.FromDouble(1200)),
                Row(tagId, FromUtc.AddMinutes(30), "Good", HistoricalQueryValue.FromDouble(1300))
            ]);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(2, fixture.Historian.Calls);
        Assert.Equal(2, payload.Signals.Count);
        Assert.Equal("bar", payload.Signals[0].Unit);
        Assert.Equal("rpm", payload.Signals[1].Unit);
        Assert.Equal(1, payload.Signals[0].Summary!.AcceptedSampleCount);
        Assert.Equal(2, payload.Signals[1].Summary!.AcceptedSampleCount);
        Assert.Equal("metadata only; SignalRole is not interpreted", payload.Provenance.SignalRoleInterpretation);
        Assert.Equal("exact_historian_timestamp_overlap_observed", payload.TemporalAlignment.Status);
        Assert.Equal(1, payload.TemporalAlignment.ExactHistorianTimestampIntersectionCount);
        Assert.Equal("unverified", payload.TemporalAlignment.SourceAcquisitionAlignment);
        Assert.Equal("unknown", payload.TemporalAlignment.ExpectedCadence);
        Assert.Equal("unknown", payload.TemporalAlignment.Completeness);
        Assert.Contains("unsupported", payload.Provenance.ProcessKpiDisposition);
    }

    [Fact]
    public async Task ProfileCoverageMarksUnalignedAndPoorQualityRequiredSignalsInsufficient()
    {
        using var fixture = Fixture.Create();
        var secondTag = fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            var timestamp = tagId == fixture.Tag.Id ? FromUtc.AddMinutes(10) : FromUtc.AddMinutes(11);
            var quality = tagId == secondTag.Id ? "Uncertain" : "Good";
            return Response(request,
                [Row(tagId, timestamp, quality, HistoricalQueryValue.FromDouble(5))]);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_required_signal_data", payload.TemporalAlignment.Status);
        Assert.Equal(1, payload.TemporalAlignment.RequiredSignalsWithGoodSamples);
        Assert.Equal(0, payload.TemporalAlignment.ExactHistorianTimestampIntersectionCount);
        Assert.Equal("insufficient_no_good_numeric_samples", payload.Signals.Single(signal => signal.TagId == secondTag.Id).Status);
        Assert.All(payload.Signals, signal => Assert.Equal("unknown", signal.Summary!.Completeness));
    }

    [Fact]
    public async Task ProfileCoverageDoesNotCallDifferentHistorianTimestampsAligned()
    {
        using var fixture = Fixture.Create();
        var secondTag = fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            var timestamp = tagId == fixture.Tag.Id ? FromUtc.AddMinutes(10) : FromUtc.AddMinutes(11);
            return Response(request,
                [Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(2, payload.TemporalAlignment.RequiredSignalsWithGoodSamples);
        Assert.Equal(0, payload.TemporalAlignment.ExactHistorianTimestampIntersectionCount);
        Assert.Equal("insufficient_exact_historian_timestamp_overlap", payload.TemporalAlignment.Status);
        Assert.All(payload.Signals, signal => Assert.Equal("descriptive_trend_available", signal.Status));
        Assert.Equal(2, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ProfileCoverageReportsNonnumericRequiredSignalAsUnsupportedWithoutQueryingIt()
    {
        using var fixture = Fixture.Create();
        var secondTag = fixture.AddProfileSignal("running", "motor-state", TagDataType.Boolean, null);
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            return Response(request,
                [Row(tagId, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        var unsupported = Assert.Single(payload.Signals, signal => signal.TagId == secondTag.Id);
        Assert.Equal("unsupported_non_numeric_tag_type", unsupported.Status);
        Assert.Null(unsupported.Summary);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.Equal("insufficient_required_signal_data", payload.TemporalAlignment.Status);
    }

    [Fact]
    public async Task ProfileCoverageEnforcesOneTotalRowCapAcrossSignalsWithoutReturningPartialResults()
    {
        using var fixture = Fixture.Create();
        var secondTag = fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");
        var firstTagPage = 0;
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            if (tagId == secondTag.Id)
                return Response(request,
                    [Row(tagId, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(6))]);

            var cursor = firstTagPage == 249 ? null : $"first-{firstTagPage + 1}";
            firstTagPage++;
            return Response(request, Enumerable.Range(0, 200)
                .Select(_ => Row(tagId, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5)))
                .ToArray(), cursor);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status413PayloadTooLarge, result);
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("INTELLIGENCE_PROFILE_WINDOW_TOO_DENSE", error.Code);
        Assert.Equal(251, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ProfileCoverageStopsBeforeHistorianWhenTrendUseIsMissing()
    {
        using var fixture = Fixture.Create(grantTrendUse: false);
        fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public void ProfileCoverageCatalogStatesItsBoundsAndNonPhysicalTimestampRule()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            AssetProcessIntelligenceProfileCoverageCatalogV1.Describe()));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("catalogVersion").GetInt32());
        Assert.Equal(16, root.GetProperty("maximumSignals").GetInt32());
        Assert.Equal(50000, root.GetProperty("maximumRowsPerWindow").GetInt32());
        Assert.Equal("metadata_only", root.GetProperty("signalRoleInterpretation").GetString());
        Assert.Equal("none", root.GetProperty("unitConversion").GetString());
        Assert.Contains("unsupported", root.GetProperty("processKpiDisposition").GetString());
        Assert.Contains("exact Historian event-time equality only",
            root.GetProperty("rules")[0].GetProperty("formula").GetString());
    }

    [Fact]
    public async Task ProfileCoverageRejectsDuplicateTagReferencesBeforeHistorianAccess()
    {
        using var fixture = Fixture.Create();
        var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
        var profile = Assert.Single(configuration.Profiles!);
        var original = Assert.Single(profile.Signals!);
        var duplicate = original with { Id = Guid.NewGuid(), Key = "pressure-alias" };
        fixture.Workspace.AssetProcessIntelligence.Replace(configuration with
        {
            Profiles = [profile with { Signals = [original, duplicate] }]
        });
        var descriptor = fixture.Workspace.Describe();
        fixture.Workspace.SetCheckout(Fixture.ProjectKey, "Stage 3B duplicate reference test",
            descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status400BadRequest, result);
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("INTELLIGENCE_PROFILE_DUPLICATE_TAG_REFERENCE_UNSUPPORTED", error.Code);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ProfileCoveragePreauthorizesEveryTagBeforeTheFirstHistorianQuery()
    {
        using var fixture = Fixture.Create(grantTagRead: false);
        fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task PausingProfileScopeAfterFirstSignalDiscardsTheWholeProfileResponse()
    {
        using var fixture = Fixture.Create();
        fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
            return Response(request,
                [Row(tagId, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ProfileCoverageRejectsAProfileOutsideTheRequestedActiveScope()
    {
        using var fixture = Fixture.Create();

        var result = await fixture.ExecuteProfileCoverageAsync(scopeId: Guid.NewGuid());

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ProfileCoverageRejectsMissingIntelligenceEntitlementBeforeHistorianAccess()
    {
        using var fixture = Fixture.Create(licensed: false);
        fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task LocationScopeIncludesEquipmentByStableLocationAncestry()
    {
        using var fixture = Fixture.Create(scopeKind: AssetProcessIntelligenceScopeKind.Location, nestedLocation: true);
        fixture.Historian.Respond = request => Response(
            request,
            [Row(fixture.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))]);

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Single(payload.Provenance.Assignments);
        Assert.Equal(fixture.EquipmentId, payload.Provenance.Assignments[0].EquipmentId);
        Assert.Equal(1, fixture.Historian.Calls);
    }

    [Fact]
    public async Task UnequalComparisonWindowsAreRejectedBeforeAnyHistorianQuery()
    {
        using var fixture = Fixture.Create();
        var request = fixture.Request with
        {
            ComparisonWindow = Window(FromUtc.AddDays(-1), FromUtc.AddHours(1))
        };

        var result = await fixture.ExecuteAsync(request);

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task EqualDurationComparisonUsesTwoExactAbsoluteWindowsAndMakesNoRegimeClaim()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request =>
        {
            var page = fixture.Historian.Calls - 1;
            var value = page == 0 ? 8 : 3;
            return Response(request,
                [Row(fixture.Tag.Id, request.Range.FromUtc!.Value.AddMinutes(5), "Good",
                    HistoricalQueryValue.FromInt32(value))]);
        };
        var request = fixture.Request with
        {
            ComparisonWindow = Window(FromUtc.AddDays(-1), FromUtc.AddDays(-1).AddHours(1))
        };

        var result = await fixture.ExecuteAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(2, fixture.Historian.Calls);
        Assert.Equal(request.Window!.FromUtc, fixture.Historian.Requests[0].Range.FromUtc);
        Assert.Equal(request.Window.ToUtc, fixture.Historian.Requests[0].Range.ToUtc);
        Assert.Equal(request.ComparisonWindow!.FromUtc, fixture.Historian.Requests[1].Range.FromUtc);
        Assert.Equal(request.ComparisonWindow.ToUtc, fixture.Historian.Requests[1].Range.ToUtc);
        Assert.Equal(5d, payload.Comparison!.SampleMeanDelta);
        Assert.Equal("not_assessed", payload.Comparison.RegimeEquivalence);
    }

    [Fact]
    public async Task ActiveRuntimeRevisionChangeDuringReadDiscardsTheWholeResult()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request =>
        {
            fixture.Runtime.SetRevision(42);
            return Response(request,
                [Row(fixture.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task TagReadRevocationBetweenPagesDiscardsAllRowsAndDoesNotQueryNextPage()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request =>
        {
            var prior = fixture.Authority.Snapshot();
            var role = Assert.Single(prior.Roles);
            fixture.Authority.TryReplaceAsync(
                prior.Version,
                [role with { Grants = [new CapabilityGrantEngineeringDto(SecurityCapability.TrendUse)] }],
                prior.Scopes).GetAwaiter().GetResult();
            return Response(
                request,
                [Row(fixture.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))],
                "opaque-next");
        };

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task TrendUseRevocationBetweenPagesDiscardsAllRowsAndDoesNotQueryNextPage()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request =>
        {
            var prior = fixture.Authority.Snapshot();
            var role = Assert.Single(prior.Roles);
            fixture.Authority.TryReplaceAsync(
                prior.Version,
                [role with { Grants = [new CapabilityGrantEngineeringDto(SecurityCapability.TagRead)] }],
                prior.Scopes).GetAwaiter().GetResult();
            return Response(
                request,
                [Row(fixture.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))],
                "opaque-next");
        };

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public void EngineeringValidatorRejectsAmbiguousScopeIdentityAtRegistryBoundary()
    {
        var scopeId = Guid.NewGuid();
        var configuration = new AssetProcessIntelligenceEngineeringDto(
            Version: 1,
            Scopes:
            [
                new AssetProcessIntelligenceScopeEngineeringDto(AssetProcessIntelligenceScopeKind.Equipment, scopeId, true),
                new AssetProcessIntelligenceScopeEngineeringDto(AssetProcessIntelligenceScopeKind.Equipment, scopeId, true)
            ]);

        var issues = AssetProcessIntelligenceEngineeringValidator.Validate(configuration, null, null);

        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_SCOPE_DUPLICATE");
        Assert.Throws<InvalidDataException>(() =>
            new InMemoryAssetProcessIntelligenceEngineeringRegistry().Replace(configuration));
    }

    [Fact]
    public async Task PausingScopeCancelsInFlightReadAndReturnsNoKpi()
    {
        using var fixture = Fixture.Create();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (request, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(request, []);
        };

        var task = fixture.ExecuteAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
    }

    [Fact]
    public async Task AuthorityPermitRevocationCancelsInFlightReadAndDiscardsItsRows()
    {
        using var fixture = Fixture.Create();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (request, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(request,
                [Row(fixture.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var task = fixture.ExecuteAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        // AuthorityPolicyHaCoordinator invalidates all active permits before it persists
        // and replicates a policy mutation; the in-flight analytics request must observe it.
        fixture.Gate.InvalidateAll();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ExactObservationCapIsAcceptedOnlyWhenPaginationHasEnded()
    {
        using var complete = Fixture.Create();
        complete.Historian.Respond = request =>
        {
            var page = complete.Historian.Calls - 1;
            var cursor = page == 249 ? null : $"page-{page + 1}";
            return Response(request, Enumerable.Range(0, 200)
                .Select(_ => Row(complete.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromInt32(1)))
                .ToArray(), cursor);
        };

        var completed = await complete.ExecuteAsync();

        AssertStatus(StatusCodes.Status200OK, completed);
        var completePayload = Assert.IsType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(completed).Value);
        Assert.Equal(AssetProcessIntelligenceKpiCatalogV1.MaximumRowsPerWindow,
            completePayload.Period.Summary.RawObservationCount);
        Assert.Equal(250, complete.Historian.Calls);

        using var truncated = Fixture.Create();
        truncated.Historian.Respond = request =>
        {
            var page = truncated.Historian.Calls - 1;
            var cursor = $"page-{page + 1}";
            return Response(request, Enumerable.Range(0, 200)
                .Select(_ => Row(truncated.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromInt32(1)))
                .ToArray(), cursor);
        };

        var rejected = await truncated.ExecuteAsync();

        AssertStatus(StatusCodes.Status413PayloadTooLarge, rejected);
        Assert.Equal(250, truncated.Historian.Calls);
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(rejected).Value);
        Assert.Equal("INTELLIGENCE_ANALYTICS_WINDOW_TOO_DENSE", error.Code);
    }

    [Fact]
    public async Task EndpointSurfaceContainsOnlyCatalogAndReadOnlyQueries()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<AssetProcessIntelligenceAnalyticsService>(_ => null!);
        await using var app = builder.Build();
        app.MapAssetProcessIntelligenceAnalyticsEndpoints();

        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "/api/engineering/asset-process-intelligence/analytics/catalog",
                "/api/engineering/asset-process-intelligence/analytics/contextual-deviation-vector/v1/catalog",
                "/api/engineering/asset-process-intelligence/analytics/contextual-deviation-vector/v1/query",
                "/api/engineering/asset-process-intelligence/analytics/contextual-reference-deviation/v1/catalog",
                "/api/engineering/asset-process-intelligence/analytics/contextual-reference-deviation/v1/query",
                "/api/engineering/asset-process-intelligence/analytics/current-frequency-comparison/v1/catalog",
                "/api/engineering/asset-process-intelligence/analytics/current-frequency-comparison/v1/query",
                "/api/engineering/asset-process-intelligence/analytics/net-observed-rate/v1/catalog",
                "/api/engineering/asset-process-intelligence/analytics/net-observed-rate/v1/query",
                "/api/engineering/asset-process-intelligence/analytics/performance/v1/catalog",
                "/api/engineering/asset-process-intelligence/analytics/performance/v1/query",
                "/api/engineering/asset-process-intelligence/analytics/profile-coverage",
                "/api/engineering/asset-process-intelligence/analytics/profile-coverage/catalog",
                "/api/engineering/asset-process-intelligence/analytics/query"
            },
            routes);
        var methods = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => endpoint.Metadata.OfType<HttpMethodMetadata>().Single().HttpMethods)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(new[] { "GET", "POST" }, methods);
    }

    [Fact]
    public void PerformanceV1BCatalog_DeclaresThreeBoundedRecordedIndicators()
    {
        var catalog = JsonSerializer.SerializeToElement(AssetProcessIntelligencePerformanceV1BCatalog.Describe());

        Assert.Equal(1, catalog.GetProperty("contractVersion").GetInt32());
        Assert.Equal(1, catalog.GetProperty("catalogVersion").GetInt32());
        Assert.Equal("historian.samples.v2", catalog.GetProperty("dataset").GetString());
        Assert.Equal(50000, catalog.GetProperty("maximumRawRowsAcrossSelectedTags").GetInt32());
        Assert.Equal(200, catalog.GetProperty("pageSize").GetInt32());
        Assert.StartsWith("unverified", catalog.GetProperty("physicalSimultaneity").GetString());
        Assert.Equal(3, catalog.GetProperty("rules").GetArrayLength());
        var ratioRule = AssetProcessIntelligencePerformanceV1BCatalog.Rules.Single(
            rule => rule.Id == "recorded-pressure-power-input-ratio.v1");
        Assert.Equal("derived", ratioRule.Kind);
        Assert.Equal(2, ratioRule.MinimumQualifiedTuples);
        Assert.Contains("unit.volumetric_flow.cubic_metre_per_hour",
            ratioRule.InputUnits.Single(input => input.Role == "pump.discharge_volumetric_flow").AcceptedUnitCodes);
        Assert.Contains("unit.pressure.kilopascal",
            ratioRule.InputUnits.Single(input => input.Role == "pump.direct_differential_pressure").AcceptedUnitCodes);
        var specificEnergyRule = AssetProcessIntelligencePerformanceV1BCatalog.Rules.Single(
            rule => rule.Id == "specific-active-energy-at-recorded-production-rate.v1");
        Assert.Equal(3, specificEnergyRule.OutputUnitVariants.Count);
        Assert.Contains(specificEnergyRule.OutputUnitVariants, variant =>
            variant.ProductionRole == "process.net_good_production_volume_rate" && variant.OutputUnit == "kWh/m³");
        Assert.Contains(specificEnergyRule.OutputUnitVariants, variant =>
            variant.ProductionRole == "process.net_good_production_mass_rate" && variant.OutputUnit == "kWh/kg");
        Assert.Contains(specificEnergyRule.OutputUnitVariants, variant =>
            variant.ProductionRole == "process.net_good_production_item_rate" && variant.OutputUnit == "kWh/item");
    }

    [Fact]
    public async Task PerformanceV1B_SpecificEnergyUsesCanonicalPowerAndProductionRateAndExactSourceTimes()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals(
            "specific-active-energy-at-recorded-production-rate.v1", "process.net_good_production_mass_rate");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var signal = tags[tagId];
            var t1 = FromUtc.AddMinutes(5);
            var t2 = FromUtc.AddMinutes(10);
            var values = signal.Path.EndsWith("ActivePower", StringComparison.Ordinal)
                ? new[] { 3.6d, 7.2d } // kW -> 3600/7200 W
                : new[] { 3600d, 7200d }; // kg/h -> 1/2 kg/s
            return ResponseV2(query,
            [
                V2Row(tagId, t1, "Good", HistoricalQueryValue.FromDouble(values[0]), t1, "Synchronized"),
                V2Row(tagId, t2, "Good", HistoricalQueryValue.FromDouble(values[1]), t2, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("available", payload.Summary.Status);
        Assert.Equal(2, payload.Summary.QualifiedTupleCount);
        Assert.Equal(0.001d, payload.Summary.Minimum!.Value, 12);
        Assert.Equal(0.001d, payload.Summary.Maximum!.Value, 12);
        Assert.Equal(0.001d, payload.Summary.ArithmeticMean!.Value, 12);
        Assert.Equal("kWh/kg", payload.Summary.Unit);
        Assert.Equal(4, payload.TotalRawObservationCount);
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "equipment.active_input_power" &&
            signal.SignalKind == "measured_total_input");
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "process.net_good_production_mass_rate" &&
            signal.SignalKind == "measured_output" && signal.ControlledUnitCode == "unit.mass_flow.kilogram_per_hour");
        Assert.Equal(HistoricalDatasets.HistorianSamplesV2, fixture.Historian.Requests[0].Dataset);
        Assert.All(fixture.Historian.Requests, query =>
        {
            Assert.Equal(2, query.Version);
            Assert.Equal(200, query.Page!.Size);
            Assert.Equal("timestamp", Assert.Single(query.OrderBy!).Field);
        });
    }

    [Fact]
    public async Task PerformanceV1B_SpecificEnergyMatchesDocumentedVolumeRateExample()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("specific-active-energy-at-recorded-production-rate.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var value = tags[tagId].Path.EndsWith("ActivePower", StringComparison.Ordinal)
                ? 2d
                : 20d / 3600d; // 2 kW at 20 m³/h = 0.1 kWh/m³.
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(0.1d, payload.Summary.ArithmeticMean!.Value, 12);
        Assert.Equal("kWh/m³", payload.Summary.Unit);
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "process.net_good_production_volume_rate" &&
            signal.SignalKind == "measured_output" &&
            signal.CanonicalUnit == "m³/s" &&
            signal.ControlledUnitCode == "unit.volumetric_flow.cubic_metre_per_second");
    }

    [Fact]
    public async Task PerformanceV1B_SpecificEnergyAveragesPointwiseValuesNotRatioOfMeanRates()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("specific-active-energy-at-recorded-production-rate.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var isPower = tags[tagId].Path.EndsWith("ActivePower", StringComparison.Ordinal);
            var values = isPower ? new[] { 2d, 2d } : new[] { 10d / 3600d, 30d / 3600d };
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(values[0]), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(values[1]), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("available", payload.Summary.Status);
        Assert.Equal("kWh/m³", payload.Summary.Unit);
        Assert.Equal(0.2d, payload.Tuples[0].Value!.Value, 12);
        Assert.Equal(0.06666666666666667d, payload.Tuples[1].Value!.Value, 12);
        Assert.Equal(0.13333333333333333d, payload.Summary.ArithmeticMean!.Value, 12);
        Assert.NotEqual(0.1d, payload.Summary.ArithmeticMean!.Value);
        Assert.Equal(2, payload.Summary.QualifiedTupleCount);
    }

    [Theory]
    [InlineData("recorded-differential-pressure-power.v1", 1666.6666666666667d)]
    [InlineData("recorded-pressure-power-input-ratio.v1", 83.33333333333333d)]
    public async Task PerformanceV1B_MatchesDocumentedPressurePowerExamples(string ruleId, double expectedMean)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals(ruleId);
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var value = tags[tagId].Path switch
            {
                var path when path.EndsWith("Flow", StringComparison.Ordinal) => 20d,
                var path when path.EndsWith("DifferentialPressure", StringComparison.Ordinal) => 3d,
                _ => 2000d
            };
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(expectedMean, payload.Summary.ArithmeticMean!.Value, 8);
        Assert.Equal(ruleId == "recorded-pressure-power-input-ratio.v1" ? "% pressure-only power / measured electrical input" : "W",
            payload.Summary.Unit);
    }

    [Fact]
    public async Task PerformanceV1B_RatioRetainsAndFlagsFiniteResultsAboveOneHundredPercent()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-pressure-power-input-ratio.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var signal = tags[tagId];
            var (value1, value2) = signal.Path switch
            {
                var path when path.EndsWith("Flow", StringComparison.Ordinal) => (3600d, 7200d),
                var path when path.EndsWith("DifferentialPressure", StringComparison.Ordinal) => (1d, 4d),
                _ => (100d, 400d)
            };
            var t1 = FromUtc.AddMinutes(1);
            var t2 = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, t1, "Good", HistoricalQueryValue.FromDouble(value1), t1, "Synchronized"),
                V2Row(tagId, t2, "Good", HistoricalQueryValue.FromDouble(value2), t2, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("available", payload.Summary.Status);
        Assert.Equal(100000d, payload.Summary.Minimum);
        Assert.Equal(200000d, payload.Summary.Maximum);
        Assert.Equal(150000d, payload.Summary.ArithmeticMean);
        Assert.All(payload.Tuples, tuple => Assert.True(tuple.AboveOneHundredPercent));
        Assert.Contains("result_above_100_percent_observed_not_clipped", payload.Summary.ReasonCodes);
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "pump.discharge_volumetric_flow" &&
            signal.FlowDirection == "pump_to_process");
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "pump.direct_differential_pressure" &&
            signal.PressureReference == "differential_outlet_minus_inlet");
        Assert.Contains("not hydraulic, motor, or overall system efficiency", payload.Provenance.MeasurementInterpretation,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PerformanceV1B_ZeroPressureNumeratorIsAValidQualifiedResult()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp1 = FromUtc.AddMinutes(1);
            var timestamp2 = FromUtc.AddMinutes(2);
            var isPressure = tags[tagId].Path.EndsWith("DifferentialPressure", StringComparison.Ordinal);
            var value = isPressure ? 0d : 3600d;
            return ResponseV2(query,
            [
                V2Row(tagId, timestamp1, "Good", HistoricalQueryValue.FromDouble(value), timestamp1, "Synchronized"),
                V2Row(tagId, timestamp2, "Good", HistoricalQueryValue.FromDouble(value), timestamp2, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("available", payload.Summary.Status);
        Assert.Equal(2, payload.Summary.QualifiedTupleCount);
        Assert.Equal(0d, payload.Summary.ArithmeticMean);
    }

    [Fact]
    public async Task PerformanceV1B_DifferentialPressurePowerConvertsMixedV1InputsToWatts()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var value = tags[tagId].Path.EndsWith("Flow", StringComparison.Ordinal) ? 3600d : 1d;
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("W", payload.Summary.Unit);
        Assert.Equal(100000d, payload.Summary.Minimum);
        Assert.Equal(100000d, payload.Summary.ArithmeticMean);
    }

    [Fact]
    public async Task PerformanceV1B_UsesV2FlowAndPressureUnitsThroughControlledResolver()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-pressure-power-input-ratio.v1");
        var flow = tags.Values.Single(tag => tag.Path.EndsWith("Flow", StringComparison.Ordinal)) with
        {
            ControlledEngineeringUnit = new ControlledEngineeringUnitReference(2, "unit.volumetric_flow.cubic_metre_per_second")
        };
        var pressure = tags.Values.Single(tag => tag.Path.EndsWith("DifferentialPressure", StringComparison.Ordinal)) with
        {
            ControlledEngineeringUnit = new ControlledEngineeringUnitReference(2, "unit.pressure.kilopascal")
        };
        fixture.Workspace.Tags.Upsert(flow);
        fixture.Workspace.Tags.Upsert(pressure);
        fixture.Runtime.SetTag(flow);
        fixture.Runtime.SetTag(pressure);
        var descriptor = fixture.Workspace.Describe();
        fixture.Workspace.SetCheckout(Fixture.ProjectKey, "V2 unit resolver test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var path = tags[tagId].Path;
            var value = path.EndsWith("Flow", StringComparison.Ordinal) ? 1d
                : path.EndsWith("DifferentialPressure", StringComparison.Ordinal) ? 100d : 100d;
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(100000d, payload.Summary.ArithmeticMean);
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "pump.discharge_volumetric_flow" && signal.UnitCatalogVersion == 2);
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "pump.direct_differential_pressure" && signal.UnitCatalogVersion == 2);
    }

    [Theory]
    [InlineData("reverse-flow", "flow_not_forward_positive")]
    [InlineData("negative-pressure", "negative_differential_pressure")]
    public async Task PerformanceV1B_InvalidPhysicalDomainProducesUnavailableStatsNotFabricatedZero(
        string invalidInput,
        string expectedReason)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var isFlow = tags[tagId].Path.EndsWith("Flow", StringComparison.Ordinal);
            var value = invalidInput == "reverse-flow"
                ? isFlow ? -3600d : 1d
                : isFlow ? 3600d : -1d;
            var timestamp1 = FromUtc.AddMinutes(1);
            var timestamp2 = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, timestamp1, "Good", HistoricalQueryValue.FromDouble(value), timestamp1, "Synchronized"),
                V2Row(tagId, timestamp2, "Good", HistoricalQueryValue.FromDouble(value), timestamp2, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Equal("W", payload.Summary.Unit);
        Assert.Equal(0, payload.Summary.QualifiedTupleCount);
        Assert.Null(payload.Summary.Minimum);
        Assert.Null(payload.Summary.Maximum);
        Assert.Null(payload.Summary.ArithmeticMean);
        Assert.All(payload.Tuples, tuple => Assert.Equal(expectedReason, tuple.ReasonCode));
    }

    [Theory]
    [InlineData("zero-production-rate", "nonpositive_production_rate")]
    [InlineData("negative-power", "negative_power")]
    public async Task PerformanceV1B_SpecificEnergyRejectsInvalidProductionDenominatorAndNegativePower(
        string invalidInput,
        string expectedReason)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals(
            "specific-active-energy-at-recorded-production-rate.v1", "process.net_good_production_item_rate");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var isPower = tags[tagId].Path.EndsWith("ActivePower", StringComparison.Ordinal);
            var value = invalidInput == "zero-production-rate"
                ? isPower ? 3.6d : 0d
                : isPower ? -1d : 3600d;
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Equal("kWh/item", payload.Summary.Unit);
        Assert.Contains(expectedReason, payload.Summary.ReasonCodes);
        Assert.All(payload.Tuples, tuple => Assert.Equal(expectedReason, tuple.ReasonCode));
    }

    [Fact]
    public async Task PerformanceV1B_RatioRejectsNonpositiveMeasuredInputPower()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-pressure-power-input-ratio.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var path = tags[tagId].Path;
            var value = path.EndsWith("Flow", StringComparison.Ordinal) ? 3600d
                : path.EndsWith("DifferentialPressure", StringComparison.Ordinal) ? 1d : 0d;
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Contains("nonpositive_input_power", payload.Summary.ReasonCodes);
    }

    [Fact]
    public async Task PerformanceV1B_NonFiniteFormulaArithmeticIsRejectedNotClipped()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var path = tags[tagId].Path;
            var value = path.EndsWith("Flow", StringComparison.Ordinal) ? double.MaxValue : 1e303;
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Contains("non_finite_arithmetic", payload.Summary.ReasonCodes);
        Assert.All(payload.Tuples, tuple => Assert.Null(tuple.Value));
    }

    [Fact]
    public async Task PerformanceV1B_DoesNotInterpolateOrJoinNearestSourceTimestamps()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var offset = tags[tagId].Path.EndsWith("DifferentialPressure", StringComparison.Ordinal)
                ? TimeSpan.FromSeconds(1) : TimeSpan.Zero;
            var timestamps = new[] { FromUtc.AddMinutes(1), FromUtc.AddMinutes(2) };
            return ResponseV2(query, timestamps.Select(timestamp =>
                V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(1), timestamp + offset, "Synchronized")).ToArray());
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Empty(payload.Tuples);
        Assert.All(payload.Inputs, input => Assert.Equal(0, input.PairedCount));
        Assert.All(payload.Inputs, input => Assert.Equal(2, input.RejectionCounts["no_exact_source_timestamp_pair"]));
    }

    [Fact]
    public async Task PerformanceV1B_CountsNonnumericBadUnsynchronizedOutOfWindowAndMissingEvidence()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var times = Enumerable.Range(1, 6).Select(index => FromUtc.AddMinutes(index)).ToArray();
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            if (tags[tagId].Path.EndsWith("DifferentialPressure", StringComparison.Ordinal))
                return ResponseV2(query,
                    [V2Row(tagId, times[0], "Good", HistoricalQueryValue.FromDouble(1), times[0], "Synchronized")]);
            return ResponseV2(query,
            [
                V2Row(tagId, times[0], "Good", HistoricalQueryValue.FromDouble(3600), times[0], "Synchronized"),
                V2Row(tagId, times[1], "Good", HistoricalQueryValue.FromString("not numeric"), times[1], "Synchronized"),
                V2Row(tagId, times[2], "Good", HistoricalQueryValue.FromDouble(3600), times[2], "PresentUnverified"),
                V2Row(tagId, times[3], "Good", HistoricalQueryValue.FromDouble(3600), FromUtc.AddDays(32), "Synchronized"),
                V2Row(tagId, times[4], "BadCommunication", HistoricalQueryValue.FromDouble(3600), times[4], "Synchronized"),
                V2Row(tagId, times[5], "Good", HistoricalQueryValue.FromDouble(3600), null, "NotRecorded")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        var flowSignal = fixture.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!
            .Single(signal => signal.TagId == tags.Values.Single(tag => tag.Path.EndsWith("Flow", StringComparison.Ordinal)).Id);
        var evidence = payload.Inputs.Single(input => input.SignalId == flowSignal.Id);
        Assert.Equal(6, evidence.RawObservationCount);
        Assert.Equal(1, evidence.EligibleSynchronizedCount);
        Assert.Equal(1, evidence.PairedCount);
        Assert.Equal(1, evidence.QualifiedCount);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Null(payload.Summary.ArithmeticMean);
        Assert.Contains("value_not_finite_numeric", evidence.RejectionCounts.Keys);
        Assert.Contains("source_timestamp_not_synchronized", evidence.RejectionCounts.Keys);
        Assert.Contains("source_timestamp_outside_requested_window", evidence.RejectionCounts.Keys);
        Assert.Contains("quality_not_good", evidence.RejectionCounts.Keys);
        Assert.Contains("source_timestamp_missing", evidence.RejectionCounts.Keys);
    }

    [Fact]
    public async Task PerformanceV1B_ExcludesDuplicateSourceGroupSplitAcrossHistorianPages()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var flowTag = tags.Values.Single(tag => tag.Path.EndsWith("Flow", StringComparison.Ordinal));
        var pressureTag = tags.Values.Single(tag => tag.Path.EndsWith("DifferentialPressure", StringComparison.Ordinal));
        var duplicateTime = FromUtc.AddSeconds(200);
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            if (tagId == pressureTag.Id)
                return ResponseV2(query,
                    [V2Row(tagId, duplicateTime, "Good", HistoricalQueryValue.FromDouble(1), duplicateTime, "Synchronized")]);
            if (query.Page!.Cursor is null)
            {
                var rows = Enumerable.Range(0, 200).Select(index =>
                {
                    var observed = FromUtc.AddSeconds(index + 1);
                    var source = index == 199 ? duplicateTime : observed;
                    return V2Row(tagId, observed, "Good", HistoricalQueryValue.FromDouble(3600), source, "Synchronized");
                }).ToArray();
                return ResponseV2(query, rows, "second-flow-page");
            }
            return ResponseV2(query,
                [V2Row(tagId, FromUtc.AddSeconds(201), "BadCommunication", HistoricalQueryValue.FromDouble(3600), duplicateTime, "PresentUnverified")]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        var flowSignalId = fixture.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!
            .Single(signal => signal.TagId == flowTag.Id).Id;
        var flowEvidence = payload.Inputs.Single(input => input.SignalId == flowSignalId);
        Assert.Equal(201, flowEvidence.RawObservationCount);
        Assert.Equal(199, flowEvidence.EligibleSynchronizedCount);
        Assert.Equal(2, flowEvidence.RejectionCounts["ambiguous_source_timestamp"]);
        Assert.Equal(0, flowEvidence.PairedCount);
        Assert.Empty(payload.Tuples);
        Assert.Equal(202, payload.TotalRawObservationCount);
    }

    [Fact]
    public async Task PerformanceV1B_AuthorizesEverySelectedTagBeforeFirstHistorianRead()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
        var selectedFlow = configuration.Profiles!.Single().Signals!.Single(signal => signal.Id == request.SignalIds![0]);
        var tag = tags[selectedFlow.TagId] with { AccessPolicy = new TagAccessPolicy(ReadRoles: ["other-role"]) };
        fixture.Workspace.Tags.Upsert(tag);
        fixture.Runtime.SetTag(tag);
        var descriptor = fixture.Workspace.Describe();
        fixture.Workspace.SetCheckout(Fixture.ProjectKey, "tag authorization test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Theory]
    [InlineData(false, 200)]
    [InlineData(true, 413)]
    public async Task PerformanceV1B_AggregateRowLimitCountsRejectedRowsAndProbesRemainingInputs(
        bool extraRejectedRow,
        int expectedStatus)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var firstTagId = tags.Values.OrderBy(tag => tag.Path, StringComparer.Ordinal).First().Id;
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            if (tagId != firstTagId)
                return ResponseV2(query, extraRejectedRow
                    ? [V2Row(tagId, FromUtc.AddSeconds(1), "BadCommunication", HistoricalQueryValue.FromString("rejected"), null, "NotRecorded")]
                    : []);
            var page = fixture.Historian.Calls - 1;
            var cursor = page == 249 ? null : $"performance-page-{page + 1}";
            return ResponseV2(query, Enumerable.Range(0, 200)
                .Select(_ => V2Row(tagId, FromUtc.AddSeconds(1), "BadCommunication",
                    HistoricalQueryValue.FromString("rejected raw row"), null, "NotRecorded"))
                .ToArray(), cursor);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(expectedStatus == 200 ? StatusCodes.Status200OK : StatusCodes.Status413PayloadTooLarge, result);
        Assert.Equal(251, fixture.Historian.Calls);
        if (expectedStatus == 200)
        {
            var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
                Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
            Assert.Equal(50000, payload.TotalRawObservationCount);
            Assert.Equal("insufficient_data", payload.Summary.Status);
        }
        else
        {
            Assert.IsNotType<AssetProcessIntelligencePerformanceV1BResponse>(
                Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        }
    }

    [Fact]
    public async Task PerformanceV1B_PermitRevocationDuringHistorianReadDiscardsAllEvidence()
    {
        using var fixture = Fixture.Create();
        var (request, _) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (query, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ResponseV2(query, []);
        };

        var task = fixture.ExecutePerformanceV1BAsync(request);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Theory]
    [InlineData("tag-read", StatusCodes.Status403Forbidden)]
    [InlineData("workspace", StatusCodes.Status409Conflict)]
    [InlineData("scope", StatusCodes.Status409Conflict)]
    [InlineData("revision", StatusCodes.Status409Conflict)]
    [InlineData("entitlement", StatusCodes.Status403Forbidden)]
    public async Task PerformanceV1B_RevocationOrContextChangeBetweenPagesDiscardsTheWholeQuery(
        string mutationKind,
        int expectedStatus)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var firstSelectedSignal = fixture.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!
            .Single(signal => signal.Id == request.SignalIds![0]);
        fixture.Historian.Respond = query =>
        {
            switch (mutationKind)
            {
                case "tag-read":
                    fixture.Runtime.SetTag(tags[firstSelectedSignal.TagId] with
                    {
                        AccessPolicy = new TagAccessPolicy(ReadRoles: ["other-role"])
                    });
                    break;
                case "workspace":
                case "scope":
                    var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
                    var scopes = configuration.Scopes!.Select(scope => scope with { Active = mutationKind != "scope" }).ToArray();
                    fixture.Workspace.AssetProcessIntelligence.Replace(configuration with { Scopes = scopes });
                    break;
                case "revision":
                    fixture.Runtime.SetRevision(42);
                    break;
                case "entitlement":
                    fixture.Licensing.Licensed = false;
                    break;
            }
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = FromUtc.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(1), timestamp, "Synchronized")],
                "next-page");
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(expectedStatus, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Theory]
    [InlineData("specific-active-energy-at-recorded-production-rate.v1", 0)]
    [InlineData("specific-active-energy-at-recorded-production-rate.v1", 1)]
    [InlineData("recorded-differential-pressure-power.v1", 0)]
    [InlineData("recorded-differential-pressure-power.v1", 1)]
    [InlineData("recorded-pressure-power-input-ratio.v1", 0)]
    [InlineData("recorded-pressure-power-input-ratio.v1", 1)]
    [InlineData("recorded-pressure-power-input-ratio.v1", 2)]
    public async Task PerformanceV1B_RevokingEverySelectedTagWhileAnotherInputPageIsInFlightStopsAllReads(
        string ruleId,
        int revokedSignalIndex)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals(ruleId);
        var signals = fixture.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!
            .Where(signal => request.SignalIds!.Contains(signal.Id))
            .ToDictionary(signal => signal.Id);
        var selectedSignal = signals[request.SignalIds![revokedSignalIndex]];
        var revokedTag = tags[selectedSignal.TagId];
        var differentSignal = request.SignalIds
            .Select(signalId => signals[signalId])
            .First(signal => signal.TagId != revokedTag.Id);
        var inFlightTag = tags[differentSignal.TagId];
        var pageEntered = new TaskCompletionSource<HistoricalQueryRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Historian.RespondAsync = async (query, cancellationToken) =>
        {
            var queriedTagId = query.Filters!.Single().Values.Single().AsGuid();
            if (queriedTagId == inFlightTag.Id)
            {
                pageEntered.TrySetResult(query);
                await releasePage.Task.WaitAsync(cancellationToken);
            }
            return PerformanceV1BPage(query);
        };

        var task = fixture.ExecutePerformanceV1BAsync(request);
        var inFlightQuery = await pageEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var callsAtRevocation = fixture.Historian.Calls;
        Assert.Equal(inFlightTag.Id, inFlightQuery.Filters!.Single().Values.Single().AsGuid());
        Assert.NotEqual(revokedTag.Id, inFlightTag.Id);

        fixture.Runtime.SetTag(revokedTag with
        {
            AccessPolicy = new TagAccessPolicy(ReadRoles: ["different-role"])
        });
        releasePage.TrySetResult();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(callsAtRevocation, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Theory]
    [InlineData("specific-active-energy-at-recorded-production-rate.v1", "permit", StatusCodes.Status409Conflict)]
    [InlineData("specific-active-energy-at-recorded-production-rate.v1", "workspace", StatusCodes.Status409Conflict)]
    [InlineData("specific-active-energy-at-recorded-production-rate.v1", "revision", StatusCodes.Status409Conflict)]
    [InlineData("specific-active-energy-at-recorded-production-rate.v1", "entitlement", StatusCodes.Status403Forbidden)]
    [InlineData("specific-active-energy-at-recorded-production-rate.v1", "scope", StatusCodes.Status409Conflict)]
    [InlineData("recorded-differential-pressure-power.v1", "permit", StatusCodes.Status409Conflict)]
    [InlineData("recorded-differential-pressure-power.v1", "workspace", StatusCodes.Status409Conflict)]
    [InlineData("recorded-differential-pressure-power.v1", "revision", StatusCodes.Status409Conflict)]
    [InlineData("recorded-differential-pressure-power.v1", "entitlement", StatusCodes.Status403Forbidden)]
    [InlineData("recorded-differential-pressure-power.v1", "scope", StatusCodes.Status409Conflict)]
    [InlineData("recorded-pressure-power-input-ratio.v1", "permit", StatusCodes.Status409Conflict)]
    [InlineData("recorded-pressure-power-input-ratio.v1", "workspace", StatusCodes.Status409Conflict)]
    [InlineData("recorded-pressure-power-input-ratio.v1", "revision", StatusCodes.Status409Conflict)]
    [InlineData("recorded-pressure-power-input-ratio.v1", "entitlement", StatusCodes.Status403Forbidden)]
    [InlineData("recorded-pressure-power-input-ratio.v1", "scope", StatusCodes.Status409Conflict)]
    public async Task PerformanceV1B_ContextInvalidationWhileHistorianPageIsInFlightReturnsNoPartialResult(
        string ruleId,
        string invalidationKind,
        int expectedStatus)
    {
        using var fixture = Fixture.Create();
        var (request, _) = fixture.AddPerformanceSignals(ruleId);
        var pageEntered = new TaskCompletionSource<HistoricalQueryRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (query, cancellationToken) =>
        {
            pageEntered.TrySetResult(query);
            await releasePage.Task.WaitAsync(cancellationToken);
            return PerformanceV1BPage(query);
        };

        var task = fixture.ExecutePerformanceV1BAsync(request);
        var queryInFlight = await pageEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, fixture.Historian.Calls);
        switch (invalidationKind)
        {
            case "permit":
                fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
                break;
            case "workspace":
                fixture.Workspace.Tags.Upsert(fixture.Tag with { Description = "workspace changed during Historian read" });
                break;
            case "revision":
                fixture.Runtime.SetRevision(42);
                break;
            case "entitlement":
                fixture.Licensing.Licensed = false;
                break;
            case "scope":
                var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
                fixture.Workspace.AssetProcessIntelligence.Replace(configuration with
                {
                    Scopes = configuration.Scopes!.Select(scope => scope with { Active = false }).ToArray()
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidationKind));
        }

        var callsAtInvalidation = fixture.Historian.Calls;
        if (invalidationKind != "permit")
            releasePage.TrySetResult();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(queryInFlight, fixture.Historian.Requests[0]);
        AssertStatus(expectedStatus, result);
        Assert.Equal(callsAtInvalidation, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Theory]
    [InlineData("repeated-cursor")]
    [InlineData("malformed-row")]
    [InlineData("out-of-order-rows")]
    public async Task PerformanceV1B_MalformedOrUnorderedHistorianEvidenceFailsClosed(string failureKind)
    {
        using var fixture = Fixture.Create();
        var (request, _) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            if (failureKind == "repeated-cursor")
                return ResponseV2(query, [], "same-cursor");
            if (failureKind == "malformed-row")
                return ResponseV2(query, [null!]);
            var first = FromUtc.AddMinutes(2);
            var second = FromUtc.AddMinutes(1);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(1), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(1), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status503ServiceUnavailable, result);
        Assert.IsNotType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.True(fixture.Historian.Calls >= 1);
    }

    [Theory]
    [InlineData("duplicate-selection")]
    [InlineData("wrong-role")]
    [InlineData("unsupported-unit")]
    [InlineData("unsupported-unit-version")]
    [InlineData("missing-selected-signal")]
    public async Task PerformanceV1B_RejectsInvalidInputSelectionAndSemanticsBeforeHistorianRead(string invalidCase)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        if (invalidCase == "duplicate-selection")
            request = request with { SignalIds = [request.SignalIds![0], request.SignalIds[0]] };
        else if (invalidCase == "missing-selected-signal")
            request = request with { SignalIds = [request.SignalIds![0]] };
        else
        {
            var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            if (invalidCase == "wrong-role")
            {
                var invalidSignals = profile.Signals!.Select(signal => signal.Id == request.SignalIds![0]
                    ? signal with { SemanticBinding = signal.SemanticBinding! with { FlowDirection = "process_to_pump" } }
                    : signal).ToArray();
                fixture.Workspace.AssetProcessIntelligence.Replace(configuration with { Profiles = [profile with { Signals = invalidSignals }] });
            }
            else
            {
                var selectedSignal = profile.Signals!.Single(signal => signal.Id == request.SignalIds![0]);
                var tagId = selectedSignal.TagId;
                var tag = fixture.Runtime.Tags().Single(item => item.Id == tagId) with
                {
                    ControlledEngineeringUnit = invalidCase == "unsupported-unit-version"
                        ? new ControlledEngineeringUnitReference(99, "unit.volumetric_flow.cubic_metre_per_second")
                        : new ControlledEngineeringUnitReference(1, "unit.pressure.bar")
                };
                fixture.Workspace.Tags.Upsert(tag);
                fixture.Runtime.SetTag(tag);
            }
            var descriptor = fixture.Workspace.Describe();
            fixture.Workspace.SetCheckout(Fixture.ProjectKey, "invalid performance input", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
        }

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public void PerformanceV1B_UnsupportedSemanticCatalogVersionIsRejectedByEngineeringBeforeAnyHistorianRead()
    {
        using var fixture = Fixture.Create();
        var (request, _) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
        var profile = configuration.Profiles!.Single();
        var invalidSignals = profile.Signals!.Select(signal => signal.Id == request.SignalIds![0]
            ? signal with { SemanticBinding = signal.SemanticBinding! with { CatalogVersion = 99 } }
            : signal).ToArray();

        Assert.Throws<InvalidDataException>(() => fixture.Workspace.AssetProcessIntelligence.Replace(
            configuration with { Profiles = [profile with { Signals = invalidSignals }] }));
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public void CurrentFrequencyComparisonV1Catalog_DeclaresBoundedAAndHzRule()
    {
        var catalog = JsonSerializer.SerializeToElement(
            AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.Describe());

        Assert.Equal(1, catalog.GetProperty("contractVersion").GetInt32());
        Assert.Equal(1, catalog.GetProperty("catalogVersion").GetInt32());
        Assert.Equal("current-at-recorded-frequency.compare.v1", catalog.GetProperty("ruleId").GetString());
        Assert.Equal("historian.samples.v2", catalog.GetProperty("dataset").GetString());
        Assert.Equal(2, catalog.GetProperty("datasetContractVersion").GetInt32());
        Assert.Equal("unit.electric_current.ampere", catalog.GetProperty("currentUnit").GetProperty("code").GetString());
        Assert.Equal("unit.frequency.hertz", catalog.GetProperty("frequencyUnit").GetProperty("code").GetString());
        Assert.Equal(50000, catalog.GetProperty("maximumRawRowsAcrossBothTagsAndBothWindows").GetInt32());
        Assert.Equal(200, catalog.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public void ContextualReferenceDeviationV1Catalog_DeclaresUnknownCompletenessAndBoundedScoreContract()
    {
        var catalog = JsonSerializer.SerializeToElement(
            AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.Describe());

        Assert.Equal("contextual-reference-deviation.v1", catalog.GetProperty("ruleId").GetString());
        Assert.Equal("not_assessed", catalog.GetProperty("regimeEquivalence").GetString());
        Assert.Equal("unknown", catalog.GetProperty("completeness").GetString());
        Assert.Equal(50000, catalog.GetProperty("maximumRawRowsAcrossBothTagsAndBothWindows").GetInt32());
        Assert.Equal(200, catalog.GetProperty("pageSize").GetInt32());
        Assert.Equal(30, catalog.GetProperty("minimumReferencePairsForRobustScore").GetInt32());
        Assert.Equal(500, catalog.GetProperty("maximumReturnedEvaluationSamples").GetInt32());
        Assert.Contains("lowerInclusive <= canonical condition value < upperExclusive",
            catalog.GetProperty("conditionInterval").GetString());
    }

    [Fact]
    public void ContextualReferenceDeviationV1_CalculatesMedianMadAndExplicitlyExcludesUpperConditionBoundary()
    {
        var referenceWindow = Window(FromUtc, FromUtc.AddHours(1));
        var evaluationWindow = Window(FromUtc.AddHours(2), FromUtc.AddHours(3));
        var hertz = new ControlledEngineeringUnitReference(1, "unit.frequency.hertz");
        var referenceTargets = new List<AssetProcessIntelligenceContextualReferenceRawObservation>();
        var referenceConditions = new List<AssetProcessIntelligenceContextualReferenceRawObservation>();
        for (var index = 0; index < 30; index++)
        {
            var timestamp = FromUtc.AddMinutes(index + 1);
            referenceTargets.Add(ContextualReferenceRow(timestamp, "Good", HistoricalQueryValue.FromDouble(index + 1), timestamp, "Synchronized"));
            referenceConditions.Add(ContextualReferenceRow(timestamp, "Good", HistoricalQueryValue.FromDouble(50), timestamp, "Synchronized"));
        }
        var evalTime1 = evaluationWindow.FromUtc.AddMinutes(1);
        var evalTime2 = evaluationWindow.FromUtc.AddMinutes(2);
        var evaluationTargets = new[]
        {
            ContextualReferenceRow(evalTime1, "Good", HistoricalQueryValue.FromDouble(40), evalTime1, "Synchronized"),
            ContextualReferenceRow(evalTime2, "Good", HistoricalQueryValue.FromDouble(41), evalTime2, "Synchronized")
        };
        var evaluationConditions = new[]
        {
            ContextualReferenceRow(evalTime1, "Good", HistoricalQueryValue.FromDouble(50), evalTime1, "Synchronized"),
            ContextualReferenceRow(evalTime2, "Good", HistoricalQueryValue.FromDouble(60), evalTime2, "Synchronized")
        };

        var result = AssetProcessIntelligenceContextualReferenceDeviationV1Calculator.Calculate(
            referenceWindow, evaluationWindow,
            referenceTargets, referenceConditions, evaluationTargets, evaluationConditions,
            hertz, 50, 60);

        Assert.Equal("available", result.Status);
        Assert.Equal(15.5d, result.ReferenceMedian);
        Assert.Equal(7.5d, result.ReferenceMad);
        Assert.Equal(30, result.ReferenceEvidence.QualifiedPairCount);
        Assert.Equal(1, result.EvaluationEvidence.QualifiedPairCount);
        Assert.Equal(2, result.EvaluationEvidence.ExactSourceTimestampPairCount);
        Assert.Equal(1, result.EvaluationEvidence.ConditionOutOfRangePairCount);
        var first = Assert.Single(result.EvaluationSamples, sample => sample.SourceTimestampUtc == evalTime1);
        Assert.Equal(40d, first.TargetValue);
        Assert.Equal(24.5d, first.RawDifferenceFromReferenceMedian);
        Assert.Equal(0.67448975d * 24.5d / 7.5d, first.RobustScore!.Value, 12);
        var second = Assert.Single(result.EvaluationSamples, sample => sample.SourceTimestampUtc == evalTime2);
        Assert.False(second.ConditionInRange);
        Assert.Null(second.RawDifferenceFromReferenceMedian);
        Assert.Equal("condition_outside_requested_range", second.ScoreUnavailableReason);
    }

    [Fact]
    public void ContextualReferenceDeviationV1_ZeroMadKeepsRawDifferenceButWithholdsRobustScore()
    {
        var reference = Window(FromUtc, FromUtc.AddHours(1));
        var evaluation = Window(FromUtc.AddHours(2), FromUtc.AddHours(3));
        var unit = new ControlledEngineeringUnitReference(1, "unit.frequency.hertz");
        var referenceTargets = Enumerable.Range(0, 30).Select(index =>
        {
            var timestamp = reference.FromUtc.AddMinutes(index + 1);
            return ContextualReferenceRow(timestamp, "Good", HistoricalQueryValue.FromDouble(4), timestamp, "Synchronized");
        }).ToArray();
        var referenceConditions = referenceTargets.Select(row => ContextualReferenceRow(
            row.SourceTimestampUtc!.Value, "Good", HistoricalQueryValue.FromDouble(50), row.SourceTimestampUtc, "Synchronized")).ToArray();
        var evalTime = evaluation.FromUtc.AddMinutes(1);
        var evaluationTarget = new[] { ContextualReferenceRow(
            evalTime, "Good", HistoricalQueryValue.FromDouble(6), evalTime, "Synchronized") };
        var evaluationCondition = new[] { ContextualReferenceRow(
            evalTime, "Good", HistoricalQueryValue.FromDouble(50), evalTime, "Synchronized") };

        var result = AssetProcessIntelligenceContextualReferenceDeviationV1Calculator.Calculate(
            reference, evaluation, referenceTargets, referenceConditions, evaluationTarget, evaluationCondition,
            unit, 50, 51);

        var sample = Assert.Single(result.EvaluationSamples);
        Assert.Equal(4d, result.ReferenceMedian);
        Assert.Equal(0d, result.ReferenceMad);
        Assert.Equal(2d, sample.RawDifferenceFromReferenceMedian);
        Assert.Null(sample.RobustScore);
        Assert.Equal("reference_mad_zero", sample.ScoreUnavailableReason);
    }

    [Fact]
    public void ContextualReferenceDeviationV1_ExcludesAmbiguousAndInvalidSourceRowsAndOrdersSamplesStably()
    {
        var reference = Window(FromUtc, FromUtc.AddHours(1));
        var evaluation = Window(FromUtc.AddHours(2), FromUtc.AddHours(3));
        var unit = new ControlledEngineeringUnitReference(1, "unit.frequency.hertz");
        var referenceTime = reference.FromUtc.AddMinutes(1);
        var referenceTargets = new[]
        {
            ContextualReferenceRow(referenceTime, "Good", HistoricalQueryValue.FromDouble(10), referenceTime, "Synchronized"),
            ContextualReferenceRow(referenceTime.AddSeconds(1), "BadCommunication", HistoricalQueryValue.FromDouble(11), referenceTime, "PresentUnverified")
        };
        var referenceConditions = new[]
        {
            ContextualReferenceRow(referenceTime, "Good", HistoricalQueryValue.FromDouble(50), referenceTime, "Synchronized")
        };
        var evalFirst = evaluation.FromUtc.AddMinutes(1);
        var evalSecond = evaluation.FromUtc.AddMinutes(2);
        var evalThird = evaluation.FromUtc.AddMinutes(3);
        var evaluationTargets = new[]
        {
            ContextualReferenceRow(evalSecond, "Good", HistoricalQueryValue.FromDouble(22), evalSecond, "Synchronized"),
            ContextualReferenceRow(evalFirst, "Good", new HistoricalQueryValue(HistoricalValueKind.Double, "NaN"), evalFirst, "Synchronized"),
            ContextualReferenceRow(evalFirst.AddSeconds(1), "Good", HistoricalQueryValue.FromDouble(20), evalFirst, "Synchronized"),
            ContextualReferenceRow(evalThird, "Good", HistoricalQueryValue.FromDouble(23), evalThird, "Synchronized")
        };
        var evaluationConditions = new[]
        {
            ContextualReferenceRow(evalFirst, "Good", HistoricalQueryValue.FromDouble(50), evalFirst, "Synchronized"),
            ContextualReferenceRow(evalSecond, "Good", HistoricalQueryValue.FromDouble(50), evalSecond, "Synchronized"),
            ContextualReferenceRow(evalThird, "Good", HistoricalQueryValue.FromDouble(50), evalThird, "Synchronized")
        };

        var forward = AssetProcessIntelligenceContextualReferenceDeviationV1Calculator.Calculate(
            reference, evaluation, referenceTargets, referenceConditions, evaluationTargets, evaluationConditions, unit, 49, 51);
        var reversed = AssetProcessIntelligenceContextualReferenceDeviationV1Calculator.Calculate(
            reference, evaluation, referenceTargets.Reverse().ToArray(), referenceConditions,
            evaluationTargets.Reverse().ToArray(), evaluationConditions.Reverse().ToArray(), unit, 49, 51);

        Assert.Equal(1, forward.ReferenceEvidence.Target.AmbiguousSourceTimestampGroupCount);
        Assert.Equal(2, forward.ReferenceEvidence.Target.AmbiguousSourceTimestampRowCount);
        Assert.Equal(0, forward.ReferenceEvidence.ExactSourceTimestampPairCount);
        Assert.Equal(0, forward.ReferenceEvidence.QualifiedPairCount);
        Assert.Equal(1, forward.EvaluationEvidence.Target.AmbiguousSourceTimestampGroupCount);
        Assert.Equal(2, forward.EvaluationEvidence.Target.AmbiguousSourceTimestampRowCount);
        Assert.Equal(1, forward.EvaluationEvidence.Target.RejectionCounts["value_not_finite_numeric"]);
        Assert.Equal(2, forward.EvaluationSamples.Count);
        Assert.Equal(evalSecond, forward.EvaluationSamples[0].SourceTimestampUtc);
        Assert.Equal(evalThird, forward.EvaluationSamples[1].SourceTimestampUtc);
        Assert.Equal(22d, forward.EvaluationSamples[0].TargetValue);
        Assert.Equal(forward.EvaluationSamples, reversed.EvaluationSamples);
    }

    [Fact]
    public void ContextualReferenceDeviationV1_RejectsUnsupportedEvidenceAndSourceTimeOutsideWindow()
    {
        var reference = Window(FromUtc, FromUtc.AddHours(1));
        var evaluation = Window(FromUtc.AddHours(2), FromUtc.AddHours(3));
        var unit = new ControlledEngineeringUnitReference(1, "unit.frequency.hertz");
        var referenceTime = reference.FromUtc.AddMinutes(1);
        var evaluationTime = evaluation.FromUtc.AddMinutes(1);

        var result = AssetProcessIntelligenceContextualReferenceDeviationV1Calculator.Calculate(
            reference,
            evaluation,
            [ContextualReferenceRow(referenceTime, "Good", HistoricalQueryValue.FromDouble(10), referenceTime, "PresentUnverified")],
            [ContextualReferenceRow(referenceTime, "Good", HistoricalQueryValue.FromDouble(50), referenceTime, "Synchronized")],
            [ContextualReferenceRow(evaluationTime, "Good", HistoricalQueryValue.FromDouble(12), evaluation.ToUtc.AddSeconds(1), "Synchronized")],
            [ContextualReferenceRow(evaluationTime, "Good", HistoricalQueryValue.FromDouble(50), evaluationTime, "Synchronized")],
            unit,
            49,
            51);

        Assert.Equal(1, result.ReferenceEvidence.Target.RejectionCounts["source_timestamp_not_synchronized"]);
        Assert.Equal(1, result.EvaluationEvidence.Target.RejectionCounts["source_timestamp_outside_requested_window"]);
        Assert.Equal(0, result.ReferenceEvidence.QualifiedPairCount);
        Assert.Equal(0, result.EvaluationEvidence.QualifiedPairCount);
        Assert.Empty(result.EvaluationSamples);
    }

    [Fact]
    public void ContextualReferenceDeviationV1_EmptyEvaluationIsInsufficientAndInvalidWindowsNeverReachCalculation()
    {
        var reference = Window(FromUtc, FromUtc.AddHours(1));
        var evaluation = Window(FromUtc.AddHours(2), FromUtc.AddHours(3));
        var unit = new ControlledEngineeringUnitReference(1, "unit.frequency.hertz");
        var time = reference.FromUtc.AddMinutes(1);
        var target = new[] { ContextualReferenceRow(time, "Good", HistoricalQueryValue.FromDouble(3), time, "Synchronized") };
        var condition = new[] { ContextualReferenceRow(time, "Good", HistoricalQueryValue.FromDouble(50), time, "Synchronized") };

        var result = AssetProcessIntelligenceContextualReferenceDeviationV1Calculator.Calculate(
            reference, evaluation, target, condition, Array.Empty<AssetProcessIntelligenceContextualReferenceRawObservation>(),
            Array.Empty<AssetProcessIntelligenceContextualReferenceRawObservation>(), unit, 49, 51);

        Assert.Equal("insufficient_data", result.Status);
        Assert.Contains("no_qualified_evaluation_pairs", result.ReasonCodes);
        Assert.Empty(result.EvaluationSamples);
    }

    [Fact]
    public async Task ContextualDeviationVectorV1_ReadsAuthorizedTagsAndReturnsCommonCohortEvidence()
    {
        using var fixture = Fixture.Create();
        var (request, targetA, targetB, condition) = fixture.AddContextualDeviationVectorSignals();
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var isReference = query.Range.FromUtc == request.ReferenceWindow!.FromUtc;
            var count = isReference ? 30 : 2;
            var rows = Enumerable.Range(1, count).Select(index =>
            {
                var timestamp = query.Range.FromUtc!.Value.AddMinutes(index);
                var value = tagId == condition.Id ? 50d
                    : tagId == targetA.Id ? isReference ? index : 39 + index
                    : isReference ? 100 + index : 139 + index;
                return V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(value), timestamp, "Synchronized");
            }).ToArray();
            return ResponseV2(query, rows);
        };

        var result = await fixture.ExecuteContextualDeviationVectorV1Async(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceContextualDeviationVectorV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(6, fixture.Historian.Calls);
        Assert.Equal(96, payload.TotalRawObservationCount);
        Assert.Equal(new[] { request.TargetSignalIds![0], request.TargetSignalIds[1] }, payload.TargetSignals.Select(signal => signal.SignalId));
        Assert.Equal(condition.Id, payload.ConditionSignal.TagId);
        Assert.Equal("Hz", payload.ConditionCanonicalUnit);
        Assert.Equal(30, payload.CompleteReferenceVectorCount);
        Assert.Equal(30, payload.InRangeReferenceVectorCount);
        Assert.Equal(2, payload.CompleteEvaluationVectorCount);
        Assert.All(payload.TargetStatistics, statistics => Assert.Equal("available", statistics.Status));
        Assert.Equal(115.5d, Assert.Single(payload.TargetStatistics, item => item.Signal.TagId == targetB.Id).ReferenceMedian);
        Assert.Equal("not_assessed", payload.Provenance.RegimeEquivalence);
        Assert.Equal("unknown", payload.Provenance.Completeness);
        Assert.Contains("no combined anomaly", payload.Provenance.Interpretation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContextualDeviationVectorV1_RejectsInvalidSelectionAndWrongAssignmentBeforeHistorianRead()
    {
        using var fixture = Fixture.Create();
        var (request, _, _, _) = fixture.AddContextualDeviationVectorSignals();
        var duplicateTarget = await fixture.ExecuteContextualDeviationVectorV1Async(request with
        {
            TargetSignalIds = [request.TargetSignalIds![0], request.TargetSignalIds[0]]
        });
        AssertStatus(StatusCodes.Status400BadRequest, duplicateTarget);
        Assert.Equal(0, fixture.Historian.Calls);

        var overlappingWindow = await fixture.ExecuteContextualDeviationVectorV1Async(request with
        {
            EvaluationWindow = Window(request.ReferenceWindow!.FromUtc.AddMinutes(30), request.ReferenceWindow.ToUtc.AddHours(1))
        });
        AssertStatus(StatusCodes.Status400BadRequest, overlappingWindow);
        Assert.Equal(0, fixture.Historian.Calls);

        var wrongAssignment = await fixture.ExecuteContextualDeviationVectorV1Async(request with { AssignmentId = Guid.NewGuid() });
        AssertStatus(StatusCodes.Status404NotFound, wrongAssignment);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ContextualDeviationVectorV1_EnforcesTargetCountAndFiniteCanonicalRangeBeforeRead()
    {
        using var fixture = Fixture.Create();
        var (request, _, _, _) = fixture.AddContextualDeviationVectorSignals();
        var invalidRequests = new[]
        {
            request with { TargetSignalIds = [request.TargetSignalIds![0]] },
            request with { TargetSignalIds = Enumerable.Range(0, 9).Select(_ => Guid.NewGuid()).ToArray() },
            request with { TargetSignalIds = null },
            request with { ConditionLowerInclusiveCanonical = double.NaN },
            request with { ConditionUpperExclusiveCanonical = double.PositiveInfinity }
        };

        foreach (var invalidRequest in invalidRequests)
        {
            var result = await fixture.ExecuteContextualDeviationVectorV1Async(invalidRequest);
            AssertStatus(StatusCodes.Status400BadRequest, result);
        }

        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ContextualDeviationVectorV1_RejectsWrongProfileDuplicateTagAndMissingRuntimeTagBeforeRead()
    {
        using (var wrongProfile = Fixture.Create())
        {
            var (request, _, _, _) = wrongProfile.AddContextualDeviationVectorSignals();
            var configuration = wrongProfile.Workspace.AssetProcessIntelligence.Snapshot();
            var otherProfile = new AssetProcessIntelligenceProfileEngineeringDto(
                Guid.NewGuid(), "other.profile", "Other profile",
                [new AssetProcessIntelligenceSignalEngineeringDto(Guid.NewGuid(), "other", "other", wrongProfile.Tag.Id)]);
            wrongProfile.Workspace.AssetProcessIntelligence.Replace(configuration with
            {
                Profiles = configuration.Profiles!.Append(otherProfile).ToArray()
            });
            var descriptor = wrongProfile.Workspace.Describe();
            wrongProfile.Workspace.SetCheckout(Fixture.ProjectKey, "other profile test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
            var result = await wrongProfile.ExecuteContextualDeviationVectorV1Async(request with
            {
                TargetSignalIds = [request.TargetSignalIds![0], otherProfile.Signals!.Single().Id]
            });
            AssertStatus(StatusCodes.Status400BadRequest, result);
            Assert.Equal(0, wrongProfile.Historian.Calls);
        }

        using (var duplicateTag = Fixture.Create())
        {
            var (request, targetA, targetB, _) = duplicateTag.AddContextualDeviationVectorSignals();
            var configuration = duplicateTag.Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            var secondSignal = profile.Signals!.Single(signal => signal.TagId == targetB.Id);
            duplicateTag.Workspace.AssetProcessIntelligence.Replace(configuration with
            {
                Profiles = [profile with
                {
                    Signals = profile.Signals!.Select(signal => signal.Id == secondSignal.Id
                        ? signal with { TagId = targetA.Id }
                        : signal).ToArray()
                }]
            });
            var descriptor = duplicateTag.Workspace.Describe();
            duplicateTag.Workspace.SetCheckout(Fixture.ProjectKey, "duplicate tag identity test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
            var result = await duplicateTag.ExecuteContextualDeviationVectorV1Async(request);
            AssertStatus(StatusCodes.Status400BadRequest, result);
            Assert.Equal(0, duplicateTag.Historian.Calls);
        }

        using (var missingRuntimeTag = Fixture.Create())
        {
            var (request, _, targetB, _) = missingRuntimeTag.AddContextualDeviationVectorSignals();
            missingRuntimeTag.Runtime.RemoveTag(targetB.Id);
            var result = await missingRuntimeTag.ExecuteContextualDeviationVectorV1Async(request);
            AssertStatus(StatusCodes.Status404NotFound, result);
            Assert.Equal(0, missingRuntimeTag.Historian.Calls);
        }
    }

    [Fact]
    public async Task ContextualDeviationVectorV1_RejectsInactiveScopeUnsupportedConditionUnitAndDeniedTagBeforeReads()
    {
        using (var inactiveScope = Fixture.Create(activeScope: false))
        {
            var (request, _, _, _) = inactiveScope.AddContextualDeviationVectorSignals();
            var result = await inactiveScope.ExecuteContextualDeviationVectorV1Async(request);
            AssertStatus(StatusCodes.Status409Conflict, result);
            Assert.Equal(0, inactiveScope.Historian.Calls);
        }

        using (var unsupportedUnit = Fixture.Create())
        {
            var (request, _, _, condition) = unsupportedUnit.AddContextualDeviationVectorSignals();
            unsupportedUnit.Runtime.SetTag(condition with
            {
                ControlledEngineeringUnit = new ControlledEngineeringUnitReference(99, "unit.frequency.hertz")
            });
            var result = await unsupportedUnit.ExecuteContextualDeviationVectorV1Async(request);
            AssertStatus(StatusCodes.Status400BadRequest, result);
            Assert.Equal(0, unsupportedUnit.Historian.Calls);
        }

        using (var deniedTag = Fixture.Create())
        {
            var (request, _, targetB, _) = deniedTag.AddContextualDeviationVectorSignals(
                targetBAccessPolicy: new TagAccessPolicy(ReadRoles: ["different-role"]));
            var result = await deniedTag.ExecuteContextualDeviationVectorV1Async(request);
            AssertStatus(StatusCodes.Status403Forbidden, result);
            Assert.NotEqual(Guid.Empty, targetB.Id);
            Assert.Equal(0, deniedTag.Historian.Calls);
        }
    }

    [Fact]
    public async Task ContextualDeviationVectorV1_PermitOrTagRevocationAfterPageDiscardsEverythingAndStopsReads()
    {
        using (var revokedPermit = Fixture.Create())
        {
            var (request, _, _, _) = revokedPermit.AddContextualDeviationVectorSignals();
            revokedPermit.Historian.Respond = query =>
            {
                revokedPermit.Gate.Pause(Fixture.ProjectKey, revokedPermit.ScopeKind, revokedPermit.ScopeId);
                var tagId = query.Filters!.Single().Values.Single().AsGuid();
                var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
                return ResponseV2(query,
                    [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")]);
            };
            var result = await revokedPermit.ExecuteContextualDeviationVectorV1Async(request);
            AssertStatus(StatusCodes.Status409Conflict, result);
            Assert.Equal(1, revokedPermit.Historian.Calls);
            Assert.IsNotType<AssetProcessIntelligenceContextualDeviationVectorV1Response>(
                Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        }

        using (var revokedTag = Fixture.Create())
        {
            var (request, _, targetB, _) = revokedTag.AddContextualDeviationVectorSignals();
            revokedTag.Historian.Respond = query =>
            {
                revokedTag.Runtime.SetTag(targetB with { AccessPolicy = new TagAccessPolicy(ReadRoles: ["different-role"]) });
                var tagId = query.Filters!.Single().Values.Single().AsGuid();
                var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
                return ResponseV2(query,
                    [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")]);
            };
            var result = await revokedTag.ExecuteContextualDeviationVectorV1Async(request);
            AssertStatus(StatusCodes.Status403Forbidden, result);
            Assert.Equal(1, revokedTag.Historian.Calls);
            Assert.IsNotType<AssetProcessIntelligenceContextualDeviationVectorV1Response>(
                Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        }
    }

    [Fact]
    public async Task ContextualDeviationVectorV1_CancellationProviderCursorAndWorkspaceChangesNeverReturnPartialResults()
    {
        using (var cancelled = Fixture.Create())
        {
            var (request, _, _, _) = cancelled.AddContextualDeviationVectorSignals();
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cancelled.Historian.RespondAsync = async (query, cancellationToken) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return ResponseV2(query, []);
            };
            using var source = new CancellationTokenSource();
            var pending = cancelled.ExecuteContextualDeviationVectorV1Async(request, source.Token);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            source.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(1, cancelled.Historian.Calls);
        }

        using (var providerFailure = Fixture.Create())
        {
            var (request, _, _, _) = providerFailure.AddContextualDeviationVectorSignals();
            providerFailure.Historian.RespondAsync = (_, _) => throw new HistoricalQueryProviderException("test provider failure");
            var result = await providerFailure.ExecuteContextualDeviationVectorV1Async(request);
            AssertStatus(StatusCodes.Status503ServiceUnavailable, result);
            Assert.Equal(1, providerFailure.Historian.Calls);
            Assert.IsNotType<AssetProcessIntelligenceContextualDeviationVectorV1Response>(
                Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        }

        using (var repeatedCursor = Fixture.Create())
        {
            var (request, _, _, _) = repeatedCursor.AddContextualDeviationVectorSignals();
            repeatedCursor.Historian.Respond = query =>
            {
                var tagId = query.Filters!.Single().Values.Single().AsGuid();
                var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
                return ResponseV2(query,
                    [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")], "same-cursor");
            };
            var result = await repeatedCursor.ExecuteContextualDeviationVectorV1Async(request);
            AssertStatus(StatusCodes.Status503ServiceUnavailable, result);
            Assert.Equal(2, repeatedCursor.Historian.Calls);
            Assert.IsNotType<AssetProcessIntelligenceContextualDeviationVectorV1Response>(
                Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        }

        using (var invalidCursor = Fixture.Create())
        {
            var (request, _, _, _) = invalidCursor.AddContextualDeviationVectorSignals();
            invalidCursor.Historian.RespondAsync = (_, _) =>
                throw new HistoricalQueryCursorException("invalid test cursor");
            var result = await invalidCursor.ExecuteContextualDeviationVectorV1Async(request);
            AssertStatus(StatusCodes.Status400BadRequest, result);
            Assert.Equal(1, invalidCursor.Historian.Calls);
            Assert.IsNotType<AssetProcessIntelligenceContextualDeviationVectorV1Response>(
                Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        }

        using (var changedWorkspace = Fixture.Create())
        {
            var (request, _, _, _) = changedWorkspace.AddContextualDeviationVectorSignals();
            changedWorkspace.Historian.Respond = query =>
            {
                changedWorkspace.Workspace.Tags.Upsert(changedWorkspace.Tag with { Description = "changed during query" });
                var tagId = query.Filters!.Single().Values.Single().AsGuid();
                var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
                return ResponseV2(query,
                    [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")]);
            };
            var result = await changedWorkspace.ExecuteContextualDeviationVectorV1Async(request);
            AssertStatus(StatusCodes.Status409Conflict, result);
            Assert.Equal(1, changedWorkspace.Historian.Calls);
            Assert.IsNotType<AssetProcessIntelligenceContextualDeviationVectorV1Response>(
                Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        }
    }

    [Fact]
    public async Task ContextualDeviationVectorV1_AggregateRowCapRejectsRawRowsWithoutPartialResult()
    {
        using var fixture = Fixture.Create();
        var (request, _, _, _) = fixture.AddContextualDeviationVectorSignals();
        fixture.Historian.Respond = query =>
        {
            var page = fixture.Historian.Calls - 1;
            var cursor = page == 249 ? null : $"page-{page + 1}";
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query, Enumerable.Range(0, query.Page!.Size)
                .Select(_ => V2Row(tagId, timestamp, "BadCommunication", HistoricalQueryValue.FromDouble(1), timestamp, "NotRecorded"))
                .ToArray(), cursor);
        };

        var result = await fixture.ExecuteContextualDeviationVectorV1Async(request);

        AssertStatus(StatusCodes.Status413PayloadTooLarge, result);
        Assert.Equal(251, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceContextualDeviationVectorV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ContextualDeviationVectorV1_AllowsExactlyTheAggregateRowCapWhenRemainingReadsAreEmpty()
    {
        using var fixture = Fixture.Create();
        var (request, targetA, _, _) = fixture.AddContextualDeviationVectorSignals();
        var nonEmptyPages = 0;
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            if (tagId != targetA.Id || query.Range.FromUtc != request.ReferenceWindow!.FromUtc)
                return ResponseV2(query, []);

            var page = nonEmptyPages++;
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            var cursor = page == 249 ? null : $"data-{page + 1}";
            var rows = Enumerable.Range(0, query.Page!.Size)
                .Select(_ => V2Row(tagId, timestamp, "BadCommunication",
                    HistoricalQueryValue.FromDouble(1), timestamp, "NotRecorded"))
                .ToArray();
            return ResponseV2(query, rows, cursor);
        };

        var result = await fixture.ExecuteContextualDeviationVectorV1Async(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceContextualDeviationVectorV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(50000, payload.TotalRawObservationCount);
        Assert.Equal(250, nonEmptyPages);
        Assert.Equal(255, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_ReadsAuthorizedTagsAndReturnsAuditableEvidenceWithoutClaimingCompleteness()
    {
        using var fixture = Fixture.Create();
        var (targetTag, conditionTag, frequencyRequest) = fixture.AddCurrentFrequencySignals();
        var reference = Window(FromUtc, FromUtc.AddHours(1));
        var evaluation = Window(FromUtc.AddHours(2), FromUtc.AddHours(3));
        var request = new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
            fixture.ScopeKind, fixture.ScopeId, frequencyRequest.AssignmentId,
            frequencyRequest.CurrentSignalId, frequencyRequest.FrequencySignalId,
            reference, evaluation, 49, 51);
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var isReference = query.Range.FromUtc == reference.FromUtc;
            var timestamps = isReference ? new[] { query.Range.FromUtc!.Value.AddMinutes(1), query.Range.FromUtc.Value.AddMinutes(2), query.Range.FromUtc.Value.AddMinutes(3) }
                : new[] { query.Range.FromUtc!.Value.AddMinutes(1), query.Range.FromUtc.Value.AddMinutes(2) };
            return ResponseV2(query, timestamps.Select((timestamp, index) =>
            {
                var isTarget = tagId == targetTag.Id;
                var value = isTarget
                    ? isReference ? new[] { 10d, 12d, 14d }[index] : new[] { 20d, 22d }[index]
                    : !isReference && index == 1 ? 60d : 50d;
                return V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(value), timestamp, "Synchronized");
            }).ToArray());
        };

        var result = await fixture.ExecuteContextualReferenceDeviationV1Async(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceContextualReferenceDeviationV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(4, fixture.Historian.Calls);
        Assert.Equal(targetTag.Id, payload.TargetSignal.TagId);
        Assert.Equal(conditionTag.Id, payload.ConditionSignal.TagId);
        Assert.Equal("Hz", payload.ConditionCanonicalUnit);
        Assert.Equal("not_assessed", payload.Provenance.RegimeEquivalence);
        Assert.Equal("unknown", payload.Provenance.Completeness);
        Assert.Equal(12d, payload.ReferenceMedian);
        Assert.Equal(2d, payload.ReferenceMedianAbsoluteDeviation);
        Assert.Equal(3, payload.ReferenceEvidence.QualifiedPairCount);
        Assert.Equal(1, payload.EvaluationEvidence.QualifiedPairCount);
        var included = Assert.Single(payload.EvaluationSamples, sample => sample.ConditionInRange);
        Assert.Equal(8d, included.RawDifferenceFromReferenceMedian);
        Assert.Null(included.RobustScore);
        Assert.Equal("fewer_than_30_qualified_reference_pairs", included.ScoreUnavailableReason);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_DeniedConditionTagStopsBeforeAnyHistorianRead()
    {
        using var fixture = Fixture.Create();
        var (target, condition, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals(
            frequencyAccessPolicy: new TagAccessPolicy(ReadRoles: ["different-role"]));
        var request = new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
            fixture.ScopeKind, fixture.ScopeId, currentFrequencyRequest.AssignmentId,
            currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
            Window(FromUtc, FromUtc.AddHours(1)),
            Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51);

        var result = await fixture.ExecuteContextualReferenceDeviationV1Async(request);

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.NotEqual(target.Id, condition.Id);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_RejectsInvalidWindowsRangeAndUnsupportedConditionUnitBeforeRead()
    {
        using var fixture = Fixture.Create();
        var (target, condition, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals();
        var reference = Window(FromUtc, FromUtc.AddHours(1));
        var evaluation = Window(FromUtc.AddHours(2), FromUtc.AddHours(3));
        var validRequest = new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
            fixture.ScopeKind, fixture.ScopeId, currentFrequencyRequest.AssignmentId,
            currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
            reference, evaluation, 49, 51);

        var invalidRange = await fixture.ExecuteContextualReferenceDeviationV1Async(validRequest with
        {
            ConditionLowerInclusiveCanonical = 51,
            ConditionUpperExclusiveCanonical = 51
        });
        AssertStatus(StatusCodes.Status400BadRequest, invalidRange);
        Assert.Equal(0, fixture.Historian.Calls);

        var overlapping = await fixture.ExecuteContextualReferenceDeviationV1Async(validRequest with
        {
            EvaluationWindow = Window(FromUtc.AddMinutes(30), FromUtc.AddMinutes(90))
        });
        AssertStatus(StatusCodes.Status400BadRequest, overlapping);
        Assert.Equal(0, fixture.Historian.Calls);

        var evaluationBeforeReference = await fixture.ExecuteContextualReferenceDeviationV1Async(validRequest with
        {
            EvaluationWindow = Window(FromUtc.AddHours(-3), FromUtc.AddHours(-2))
        });
        AssertStatus(StatusCodes.Status400BadRequest, evaluationBeforeReference);
        Assert.Equal(0, fixture.Historian.Calls);

        fixture.Runtime.SetTag(condition with
        {
            ControlledEngineeringUnit = new ControlledEngineeringUnitReference(99, "unit.frequency.hertz")
        });
        var unsupportedUnit = await fixture.ExecuteContextualReferenceDeviationV1Async(validRequest);
        AssertStatus(StatusCodes.Status400BadRequest, unsupportedUnit);
        Assert.NotEqual(target.Id, condition.Id);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_RejectsWrongAssignmentAndInactiveScopeBeforeHistorianRead()
    {
        using (var wrongAssignment = Fixture.Create())
        {
            var (_, _, currentFrequencyRequest) = wrongAssignment.AddCurrentFrequencySignals();
            var result = await wrongAssignment.ExecuteContextualReferenceDeviationV1Async(
                new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
                    wrongAssignment.ScopeKind, wrongAssignment.ScopeId, Guid.NewGuid(),
                    currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
                    Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51));
            AssertStatus(StatusCodes.Status404NotFound, result);
            Assert.Equal(0, wrongAssignment.Historian.Calls);
        }

        using (var inactiveScope = Fixture.Create(activeScope: false))
        {
            var (_, _, currentFrequencyRequest) = inactiveScope.AddCurrentFrequencySignals();
            var result = await inactiveScope.ExecuteContextualReferenceDeviationV1Async(
                new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
                    inactiveScope.ScopeKind, inactiveScope.ScopeId, currentFrequencyRequest.AssignmentId,
                    currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
                    Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51));
            AssertStatus(StatusCodes.Status409Conflict, result);
            Assert.Equal(0, inactiveScope.Historian.Calls);
        }
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_RejectsMissingAndDuplicateSignalSelectionBeforeHistorianRead()
    {
        using var fixture = Fixture.Create();
        var (_, _, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals();
        var valid = new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
            fixture.ScopeKind, fixture.ScopeId, currentFrequencyRequest.AssignmentId,
            currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
            Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51);

        var missing = await fixture.ExecuteContextualReferenceDeviationV1Async(valid with
        {
            TargetSignalId = Guid.NewGuid()
        });
        AssertStatus(StatusCodes.Status400BadRequest, missing);
        Assert.Equal(0, fixture.Historian.Calls);

        var duplicate = await fixture.ExecuteContextualReferenceDeviationV1Async(valid with
        {
            ConditionSignalId = valid.TargetSignalId
        });
        AssertStatus(StatusCodes.Status400BadRequest, duplicate);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_RejectsSignalFromDifferentProfileThanAssignmentBeforeHistorianRead()
    {
        using var fixture = Fixture.Create(scopeKind: AssetProcessIntelligenceScopeKind.Location);
        var (_, _, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals();
        var foreignTag = new TagDefinition(
            Guid.NewGuid(), "Other drive frequency", "Plant.OtherPump.DriveFrequency", TagDataType.Double,
            null, "Hz", null, true,
            ControlledEngineeringUnit: new ControlledEngineeringUnitReference(1, "unit.frequency.hertz"));
        fixture.Workspace.Tags.Register(foreignTag);
        fixture.Runtime.AddTag(foreignTag);

        var foreignSignal = new AssetProcessIntelligenceSignalEngineeringDto(
            Guid.NewGuid(), "other-drive-frequency", "measured-drive-frequency", foreignTag.Id);
        var foreignProfile = new AssetProcessIntelligenceProfileEngineeringDto(
            Guid.NewGuid(), "other-pump.profile", "Other pump profile", [foreignSignal]);
        var foreignEquipmentId = Guid.NewGuid();
        fixture.Workspace.Assets.UpsertEquipment(new EquipmentEngineeringDto(
            foreignEquipmentId, "Plant.OtherPump", "Other pump", LocationId: fixture.ScopeId));
        var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
        fixture.Workspace.AssetProcessIntelligence.Replace(configuration with
        {
            Profiles = configuration.Profiles!.Append(foreignProfile).ToArray(),
            Assignments = configuration.Assignments!.Append(new AssetProcessIntelligenceAssignmentEngineeringDto(
                Guid.NewGuid(), foreignEquipmentId, foreignProfile.Id)).ToArray()
        });
        var descriptor = fixture.Workspace.Describe();
        fixture.Workspace.SetCheckout(Fixture.ProjectKey, "Contextual reference deviation profile-bound test",
            descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

        var result = await fixture.ExecuteContextualReferenceDeviationV1Async(
            new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
                fixture.ScopeKind,
                fixture.ScopeId,
                currentFrequencyRequest.AssignmentId,
                foreignSignal.Id,
                currentFrequencyRequest.FrequencySignalId,
                Window(FromUtc, FromUtc.AddHours(1)),
                Window(FromUtc.AddHours(2), FromUtc.AddHours(3)),
                49,
                51));

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_RejectsTwoSignalsPointingToTheSameTagBeforeHistorianRead()
    {
        using var fixture = Fixture.Create();
        var (target, _, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals();
        var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
        var profile = Assert.Single(configuration.Profiles!);
        var duplicateTagSignal = new AssetProcessIntelligenceSignalEngineeringDto(
            Guid.NewGuid(), "duplicate-current-tag", "duplicate", target.Id);
        fixture.Workspace.AssetProcessIntelligence.Replace(configuration with
        {
            Profiles = [profile with { Signals = profile.Signals!.Append(duplicateTagSignal).ToArray() }]
        });
        var descriptor = fixture.Workspace.Describe();
        fixture.Workspace.SetCheckout(Fixture.ProjectKey, "Contextual reference deviation duplicate tag test",
            descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

        var result = await fixture.ExecuteContextualReferenceDeviationV1Async(
            new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
                fixture.ScopeKind,
                fixture.ScopeId,
                currentFrequencyRequest.AssignmentId,
                currentFrequencyRequest.CurrentSignalId,
                duplicateTagSignal.Id,
                Window(FromUtc, FromUtc.AddHours(1)),
                Window(FromUtc.AddHours(2), FromUtc.AddHours(3)),
                49,
                51));

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_RejectsSignalTagOutsideChosenActiveScopeBeforeHistorianRead()
    {
        using var fixture = Fixture.Create();
        var (_, _, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals();
        var outsideEquipmentId = Guid.NewGuid();
        fixture.Workspace.Assets.UpsertEquipment(new EquipmentEngineeringDto(
            outsideEquipmentId, "Plant.OtherPump", "Other pump"));
        var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
        var assignment = Assert.Single(configuration.Assignments!);
        fixture.Workspace.AssetProcessIntelligence.Replace(configuration with
        {
            Assignments = [assignment with { EquipmentId = outsideEquipmentId }]
        });
        var descriptor = fixture.Workspace.Describe();
        fixture.Workspace.SetCheckout(Fixture.ProjectKey, "Contextual reference deviation scope link test",
            descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

        var result = await fixture.ExecuteContextualReferenceDeviationV1Async(
            new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
                fixture.ScopeKind,
                fixture.ScopeId,
                currentFrequencyRequest.AssignmentId,
                currentFrequencyRequest.CurrentSignalId,
                currentFrequencyRequest.FrequencySignalId,
                Window(FromUtc, FromUtc.AddHours(1)),
                Window(FromUtc.AddHours(2), FromUtc.AddHours(3)),
                49,
                51));

        AssertStatus(StatusCodes.Status404NotFound, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_RejectsMissingRuntimeTagAndMissingExecutionPermitBeforeRead()
    {
        using (var missingTagFixture = Fixture.Create())
        {
            var (_, condition, currentFrequencyRequest) = missingTagFixture.AddCurrentFrequencySignals();
            missingTagFixture.Runtime.RemoveTag(condition.Id);
            var result = await missingTagFixture.ExecuteContextualReferenceDeviationV1Async(
                new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
                    missingTagFixture.ScopeKind, missingTagFixture.ScopeId, currentFrequencyRequest.AssignmentId,
                    currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
                    Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51));
            AssertStatus(StatusCodes.Status404NotFound, result);
            Assert.Equal(0, missingTagFixture.Historian.Calls);
        }

        using (var noPermitFixture = Fixture.Create(grantPermit: false))
        {
            var (_, _, currentFrequencyRequest) = noPermitFixture.AddCurrentFrequencySignals();
            var result = await noPermitFixture.ExecuteContextualReferenceDeviationV1Async(
                new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
                    noPermitFixture.ScopeKind, noPermitFixture.ScopeId, currentFrequencyRequest.AssignmentId,
                    currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
                    Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51));
            AssertStatus(StatusCodes.Status409Conflict, result);
            Assert.Equal(0, noPermitFixture.Historian.Calls);
        }
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_PermitRevocationAfterFirstPageDiscardsAllResultsAndStopsFurtherReads()
    {
        using var fixture = Fixture.Create();
        var (target, _, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals();
        var request = new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
            fixture.ScopeKind, fixture.ScopeId, currentFrequencyRequest.AssignmentId,
            currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
            Window(FromUtc, FromUtc.AddHours(1)),
            Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51);
        fixture.Historian.Respond = query =>
        {
            fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")]);
        };

        var result = await fixture.ExecuteContextualReferenceDeviationV1Async(request);

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.NotEqual(Guid.Empty, target.Id);
        Assert.IsNotType<AssetProcessIntelligenceContextualReferenceDeviationV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_ConditionUnitChangeDuringReadDiscardsAllEvidence()
    {
        using var fixture = Fixture.Create();
        var (_, condition, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals();
        var request = new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
            fixture.ScopeKind, fixture.ScopeId, currentFrequencyRequest.AssignmentId,
            currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
            Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51);
        fixture.Historian.Respond = query =>
        {
            fixture.Runtime.SetTag(condition with
            {
                ControlledEngineeringUnit = new ControlledEngineeringUnitReference(1, "unit.ratio.percent")
            });
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(50), timestamp, "Synchronized")]);
        };

        var result = await fixture.ExecuteContextualReferenceDeviationV1Async(request);

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceContextualReferenceDeviationV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_TagReadRevocationAfterPageStopsBeforeNextRead()
    {
        using var fixture = Fixture.Create();
        var (_, condition, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals(
            frequencyAccessPolicy: new TagAccessPolicy(ReadRoles: ["operator"]));
        var request = new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
            fixture.ScopeKind, fixture.ScopeId, currentFrequencyRequest.AssignmentId,
            currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
            Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51);
        fixture.Historian.Respond = query =>
        {
            fixture.Runtime.SetTag(condition with { AccessPolicy = new TagAccessPolicy(ReadRoles: ["different-role"]) });
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")]);
        };

        var result = await fixture.ExecuteContextualReferenceDeviationV1Async(request);

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceContextualReferenceDeviationV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_RepeatedCursorReturnsNoPartialResult()
    {
        using var fixture = Fixture.Create();
        var (_, _, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals();
        var request = new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
            fixture.ScopeKind, fixture.ScopeId, currentFrequencyRequest.AssignmentId,
            currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
            Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51);
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")], "same-cursor");
        };

        var result = await fixture.ExecuteContextualReferenceDeviationV1Async(request);

        AssertStatus(StatusCodes.Status503ServiceUnavailable, result);
        Assert.Equal(2, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceContextualReferenceDeviationV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_HistorianProviderFailureReturnsUnavailableWithNoPartialPayload()
    {
        using var fixture = Fixture.Create();
        var (_, _, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals();
        var request = new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
            fixture.ScopeKind, fixture.ScopeId, currentFrequencyRequest.AssignmentId,
            currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
            Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51);
        fixture.Historian.RespondAsync = (_, _) => throw new HistoricalQueryProviderException("test provider failure");

        var result = await fixture.ExecuteContextualReferenceDeviationV1Async(request);

        AssertStatus(StatusCodes.Status503ServiceUnavailable, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceContextualReferenceDeviationV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_RequestCancellationStopsInFlightReadWithoutReturningPartialPayload()
    {
        using var fixture = Fixture.Create();
        var (_, _, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals();
        var request = new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
            fixture.ScopeKind, fixture.ScopeId, currentFrequencyRequest.AssignmentId,
            currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
            Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (query, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ResponseV2(query, []);
        };
        using var cancellation = new CancellationTokenSource();

        var pending = fixture.ExecuteContextualReferenceDeviationV1Async(request, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ContextualReferenceDeviationV1_GlobalRowCapCountsRawRowsAndReturnsNoPartialPayload()
    {
        using var fixture = Fixture.Create();
        var (_, _, currentFrequencyRequest) = fixture.AddCurrentFrequencySignals();
        var request = new AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
            fixture.ScopeKind, fixture.ScopeId, currentFrequencyRequest.AssignmentId,
            currentFrequencyRequest.CurrentSignalId, currentFrequencyRequest.FrequencySignalId,
            Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49, 51);
        fixture.Historian.Respond = query =>
        {
            var page = fixture.Historian.Calls - 1;
            var cursor = page == 249 ? null : $"page-{page + 1}";
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query, Enumerable.Range(0, 200)
                .Select(_ => V2Row(tagId, timestamp, "BadCommunication", HistoricalQueryValue.FromDouble(1), timestamp, "NotRecorded"))
                .ToArray(), cursor);
        };

        var result = await fixture.ExecuteContextualReferenceDeviationV1Async(request);

        AssertStatus(StatusCodes.Status413PayloadTooLarge, result);
        Assert.Equal(250, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceContextualReferenceDeviationV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public void CurrentFrequencyComparisonV1_RawDuplicateTimestampAcrossEvidenceStatesIsAmbiguous()
    {
        var window = Window(FromUtc, FromUtc.AddMinutes(10));
        var duplicateTime = FromUtc.AddMinutes(2);
        var ampere = new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere");
        var hertz = new ControlledEngineeringUnitReference(1, "unit.frequency.hertz");
        var currentRows = new[]
        {
            CurrentFrequencyRow(FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(10), FromUtc.AddMinutes(1), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(2), "Good", HistoricalQueryValue.FromDouble(99), duplicateTime, "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(2).AddSeconds(1), "Good", HistoricalQueryValue.FromDouble(101), duplicateTime, "PresentUnverified")
        };
        var frequencyRows = new[]
        {
            CurrentFrequencyRow(FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(50), FromUtc.AddMinutes(1), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(2), "Good", HistoricalQueryValue.FromDouble(50), duplicateTime, "Synchronized")
        };

        var forward = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
            window, currentRows, frequencyRows, ampere, hertz, 50, 50);
        var reverse = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
            window, currentRows.Reverse().ToArray(), frequencyRows.Reverse().ToArray(), ampere, hertz, 50, 50);

        Assert.Equal(1, forward.Evidence.Current.AmbiguousSourceTimestampGroupCount);
        Assert.Equal(2, forward.Evidence.Current.AmbiguousSourceTimestampRowCount);
        Assert.Equal(1, forward.Evidence.ExactSourceTimestampPairCount);
        Assert.Equal(1, forward.Evidence.SelectedPairCount);
        var metric = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateMetric(
            forward,
            new AssetProcessIntelligenceCurrentFrequencyWindowCalculation(
                forward.Evidence with { SelectedPairCount = 2 }, 1d),
            false);
        Assert.Equal("insufficient_data", metric.Status);
        Assert.Contains("fewer_than_two_primary_frequency_conditioned_pairs", metric.ReasonCodes);
        Assert.Equal(
            forward.Evidence.Pairs.Select(pair => pair.SourceTimestampUtc),
            reverse.Evidence.Pairs.Select(pair => pair.SourceTimestampUtc));
        Assert.Equal(10d, forward.MeanCurrentA);
    }

    [Fact]
    public void CurrentFrequencyComparisonV1_FrequencyBoundsAreInclusiveAndOutOfRangePairsAreCounted()
    {
        var window = Window(FromUtc, FromUtc.AddMinutes(10));
        var currentRows = new List<AssetProcessIntelligenceCurrentFrequencyRawObservation>();
        var frequencyRows = new List<AssetProcessIntelligenceCurrentFrequencyRawObservation>();
        var currentValues = new[] { 10d, 20d, 30d, 40d };
        var frequencies = new[] { 49d, 51d, 48.999d, 51.001d };
        for (var index = 0; index < currentValues.Length; index++)
        {
            var timestamp = FromUtc.AddMinutes(index + 1);
            currentRows.Add(CurrentFrequencyRow(timestamp, "Good", HistoricalQueryValue.FromDouble(currentValues[index]), timestamp, "Synchronized"));
            frequencyRows.Add(CurrentFrequencyRow(timestamp, "Good", HistoricalQueryValue.FromDouble(frequencies[index]), timestamp, "Synchronized"));
        }

        var calculation = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
            window,
            currentRows,
            frequencyRows,
            new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere"),
            new ControlledEngineeringUnitReference(1, "unit.frequency.hertz"),
            49,
            51);

        Assert.Equal(2, calculation.Evidence.FrequencyInRangePairCount);
        Assert.Equal(2, calculation.Evidence.FrequencyOutOfRangePairCount);
        Assert.Equal(2, calculation.Evidence.SelectedPairCount);
        Assert.Equal(15d, calculation.MeanCurrentA);
        Assert.Equal(2, calculation.Evidence.Frequency.RejectionCounts["frequency_outside_requested_range"]);
    }

    [Fact]
    public void CurrentFrequencyComparisonV1_ZeroComparisonMeanRetainsAbsoluteDifference()
    {
        var window = Window(FromUtc, FromUtc.AddMinutes(10));
        var primary = new AssetProcessIntelligenceCurrentFrequencyWindowCalculation(
            EmptyCurrentFrequencyEvidence(window) with { SelectedPairCount = 2 }, 4d);
        var comparison = new AssetProcessIntelligenceCurrentFrequencyWindowCalculation(
            EmptyCurrentFrequencyEvidence(Window(FromUtc.AddHours(2), FromUtc.AddHours(2).AddMinutes(10)))
                with { SelectedPairCount = 2 }, 0d);

        var metric = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateMetric(
            primary, comparison, false);

        Assert.Equal("available", metric.Status);
        Assert.Equal(4d, metric.DifferenceA);
        Assert.Null(metric.PercentChange);
        Assert.Equal("unsupported", metric.PercentChangeStatus);
        Assert.Equal("comparison_mean_zero_percent_change_unavailable", metric.PercentChangeReasonCode);
    }

    [Fact]
    public void CurrentFrequencyComparisonV1_RejectsMissingUnverifiedAndOutOfWindowSourceTimeEvidence()
    {
        var window = Window(FromUtc, FromUtc.AddMinutes(10));
        var currentRows = new[]
        {
            CurrentFrequencyRow(FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(10), FromUtc.AddMinutes(1), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(2), "Good", HistoricalQueryValue.FromDouble(10), FromUtc.AddMinutes(2), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(3), "Good", HistoricalQueryValue.FromDouble(10), FromUtc.AddMinutes(3), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(4), "Good", HistoricalQueryValue.FromDouble(10), FromUtc.AddMinutes(4), "Synchronized")
        };
        var frequencyRows = new[]
        {
            CurrentFrequencyRow(FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(50), FromUtc.AddMinutes(1), "PresentUnverified"),
            CurrentFrequencyRow(FromUtc.AddMinutes(2), "Good", HistoricalQueryValue.FromDouble(50), FromUtc.AddSeconds(-1), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(3), "Good", HistoricalQueryValue.FromDouble(50), null, "NotRecorded"),
            CurrentFrequencyRow(FromUtc.AddMinutes(4), "BadCommunication", HistoricalQueryValue.FromDouble(50), FromUtc.AddMinutes(4), "Synchronized")
        };

        var calculation = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
            window,
            currentRows,
            frequencyRows,
            new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere"),
            new ControlledEngineeringUnitReference(1, "unit.frequency.hertz"),
            49,
            51);

        Assert.Equal(0, calculation.Evidence.ExactSourceTimestampPairCount);
        Assert.Equal(0, calculation.Evidence.SelectedPairCount);
        Assert.Equal(1, calculation.Evidence.Frequency.RejectionCounts["source_timestamp_not_synchronized"]);
        Assert.Equal(1, calculation.Evidence.Frequency.RejectionCounts["source_timestamp_outside_requested_window"]);
        Assert.Equal(1, calculation.Evidence.Frequency.RejectionCounts["source_timestamp_missing"]);
        Assert.Equal(1, calculation.Evidence.Frequency.RejectionCounts["quality_not_good"]);
        Assert.Equal(1, calculation.Evidence.Frequency.QualityCounts["BadCommunication"]);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_UsesExactSynchronizedPairsAndReturnsReproducibleMeans()
    {
        using var fixture = Fixture.Create();
        var (currentTag, frequencyTag, request) = fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var from = query.Range.FromUtc!.Value;
            var isPrimary = from == request.PrimaryWindow!.FromUtc;
            var first = from.AddMinutes(5);
            var second = from.AddMinutes(10);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(tagId == currentTag.Id
                    ? isPrimary ? 4 : 2 : 50), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(tagId == currentTag.Id
                    ? isPrimary ? 6 : 4 : 50), second, "Synchronized"),
                V2Row(tagId, from.AddMinutes(15), "Good", HistoricalQueryValue.FromString("not numeric"),
                    from.AddMinutes(15), "Synchronized"),
                V2Row(tagId, from.AddMinutes(20), "Good", HistoricalQueryValue.FromBoolean(true),
                    from.AddMinutes(20), "Synchronized")
            ]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(4, fixture.Historian.Calls);
        Assert.All(fixture.Historian.Requests, query =>
        {
            Assert.Equal(HistoricalDatasets.HistorianSamplesV2, query.Dataset);
            Assert.Equal(2, query.Version);
            Assert.Equal("timestamp", Assert.Single(query.OrderBy!).Field);
            Assert.Equal(200, query.Page!.Size);
        });
        Assert.Equal(currentTag.Id, payload.CurrentSignal.TagId);
        Assert.Equal(frequencyTag.Id, payload.FrequencySignal.TagId);
        Assert.Equal(5d, payload.Metric.PrimaryMeanCurrentA);
        Assert.Equal(3d, payload.Metric.ComparisonMeanCurrentA);
        Assert.Equal(2d, payload.Metric.DifferenceA);
        Assert.Equal(200d / 3d, payload.Metric.PercentChange!.Value, 10);
        Assert.Equal(16, payload.TotalRawObservationCount);
        Assert.Equal(2, payload.Primary.SelectedPairCount);
        Assert.Equal(2, payload.Comparison.SelectedPairCount);
        Assert.Equal(4, payload.Primary.Current.RawObservationCount);
        Assert.Equal(2, payload.Primary.Current.RejectedObservationCount);
        Assert.Equal(4, payload.Primary.Frequency.RawObservationCount);
        Assert.Equal(2, payload.Primary.Frequency.RejectedObservationCount);
        Assert.Equal("A", payload.CurrentSignal.CanonicalUnit);
        Assert.Equal("Hz", payload.FrequencySignal.CanonicalUnit);
    }

    [Theory]
    [InlineData("unit.electric_current.milliampere", 1)]
    [InlineData("unit.electric_current.milliampere", 2)]
    [InlineData("unit.electric_current.ampere", 2)]
    public async Task CurrentFrequencyComparisonV1_RejectsUnsupportedControlledUnitBeforeHistorianRead(
        string currentUnitCode,
        int currentUnitVersion)
    {
        using var fixture = Fixture.Create();
        var (_, _, request) = fixture.AddCurrentFrequencySignals(
            currentUnit: new ControlledEngineeringUnitReference(currentUnitVersion, currentUnitCode));

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RejectsOverlappingWindowsBeforeHistorianRead()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        var request = fixture.CurrentFrequencyRequest! with
        {
            ComparisonWindow = Window(FromUtc.AddMinutes(30), FromUtc.AddMinutes(90))
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_OnlyStringAndBooleanRowsReturnInsufficientDataNotProviderFailure()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var first = query.Range.FromUtc!.Value.AddMinutes(5);
            var second = query.Range.FromUtc.Value.AddMinutes(10);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromString("legacy string"), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromBoolean(false), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Metric.Status);
        Assert.Null(payload.Metric.PrimaryMeanCurrentA);
        Assert.Null(payload.Metric.ComparisonMeanCurrentA);
        Assert.Equal(2, payload.Primary.Current.RejectedObservationCount);
        Assert.Equal(2, payload.Primary.Frequency.RejectedObservationCount);
        Assert.Equal(8, payload.TotalRawObservationCount);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RejectsSetpointSemanticBeforeHistorianRead()
    {
        using var fixture = Fixture.Create();
        var (_, _, request) = fixture.AddCurrentFrequencySignals();
        var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
        var profile = configuration.Profiles!.Single();
        var invalidSignals = profile.Signals!.Select(signal => signal.Id == request.FrequencySignalId
            ? signal with
            {
                SemanticBinding = signal.SemanticBinding! with { SignalKind = "command_setpoint" }
            }
            : signal).ToArray();
        fixture.Workspace.AssetProcessIntelligence.Replace(configuration with
        {
            Profiles = [profile with { Signals = invalidSignals }]
        });
        var descriptor = fixture.Workspace.Describe();
        fixture.Workspace.SetCheckout(Fixture.ProjectKey, "invalid measured output test",
            descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_AuthorizesBothSelectedTagsBeforeFirstHistorianRead()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals(frequencyAccessPolicy: new TagAccessPolicy(ReadRoles: ["different-role"]));

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_DuplicateRawTimestampSplitAcrossPagesIsExcludedDeterministically()
    {
        using var fixture = Fixture.Create();
        var (currentTag, _, request) = fixture.AddCurrentFrequencySignals();
        var primaryFirst = request.PrimaryWindow!.FromUtc.AddMinutes(5);
        var primaryDuplicate = request.PrimaryWindow.FromUtc.AddMinutes(6);
        var comparisonFirst = request.ComparisonWindow!.FromUtc.AddMinutes(5);
        var comparisonSecond = request.ComparisonWindow.FromUtc.AddMinutes(6);
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var from = query.Range.FromUtc!.Value;
            if (tagId == currentTag.Id && from == request.PrimaryWindow.FromUtc)
            {
                if (query.Page!.Cursor is null)
                    return ResponseV2(query,
                    [
                        V2Row(tagId, primaryFirst, "Good", HistoricalQueryValue.FromDouble(10), primaryFirst, "Synchronized"),
                        V2Row(tagId, primaryDuplicate, "Good", HistoricalQueryValue.FromDouble(99), primaryDuplicate, "Synchronized")
                    ], "current-primary-second-page");
                return ResponseV2(query,
                    [V2Row(tagId, primaryDuplicate.AddSeconds(1), "Good", HistoricalQueryValue.FromDouble(101), primaryDuplicate, "PresentUnverified")]);
            }

            var isPrimary = from == request.PrimaryWindow.FromUtc;
            var times = isPrimary ? new[] { primaryFirst, primaryDuplicate } : new[] { comparisonFirst, comparisonSecond };
            return ResponseV2(query, times.Select((timestamp, index) =>
            {
                var sourceTimestamp = timestamp;
                var value = tagId == currentTag.Id
                    ? isPrimary ? index == 0 ? 10d : 99d : index == 0 ? 3d : 5d
                    : 50d;
                return V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(value), sourceTimestamp, "Synchronized");
            }).ToArray());
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(5, fixture.Historian.Calls);
        Assert.Equal("current-primary-second-page", fixture.Historian.Requests[1].Page!.Cursor);
        Assert.Equal(1, payload.Primary.Current.AmbiguousSourceTimestampGroupCount);
        Assert.Equal(2, payload.Primary.Current.AmbiguousSourceTimestampRowCount);
        Assert.Equal(1, payload.Primary.ExactSourceTimestampPairCount);
        Assert.Equal(1, payload.Primary.SelectedPairCount);
        Assert.Equal(primaryFirst, Assert.Single(payload.Primary.Pairs).SourceTimestampUtc);
        Assert.Equal("insufficient_data", payload.Metric.Status);
        Assert.Contains("fewer_than_two_primary_frequency_conditioned_pairs", payload.Metric.ReasonCodes);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_GlobalRowCapCountsRejectedRowsAndReturnsNoPartialResult()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            var pageNumber = fixture.Historian.Calls - 1;
            var cursor = pageNumber == 249 ? null : $"global-page-{pageNumber + 1}";
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            return ResponseV2(query, Enumerable.Range(0, 200)
                .Select(_ => V2Row(tagId, query.Range.FromUtc!.Value.AddSeconds(1), "Good",
                    HistoricalQueryValue.FromString("not numeric"), null, "NotRecorded"))
                .ToArray(), cursor);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status413PayloadTooLarge, result);
        Assert.Equal(250, fixture.Historian.Calls);
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("INTELLIGENCE_CURRENT_FREQUENCY_WINDOW_TOO_DENSE", error.Code);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_PermitRevocationAfterAnyPageDiscardsAllEvidence()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(5), timestamp, "Synchronized")]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_FrequencyTagReadRevocationDuringCurrentPageStopsBeforeNextQuery()
    {
        using var fixture = Fixture.Create();
        var (currentTag, frequencyTag, _) = fixture.AddCurrentFrequencySignals(
            frequencyAccessPolicy: new TagAccessPolicy(ReadRoles: ["operator"]));

        fixture.Historian.Respond = query =>
        {
            fixture.Runtime.SetTag(frequencyTag with
            {
                AccessPolicy = new TagAccessPolicy(ReadRoles: ["different-role"])
            });
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.Equal(currentTag.Id, fixture.Historian.Requests[0].Filters!.Single().Values.Single().AsGuid());
        Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_PermitCancellationWhileHistorianQueryIsInFlightIsDeterministic()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (query, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ResponseV2(query, []);
        };

        var task = fixture.ExecuteCurrentFrequencyComparisonV1Async();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_WorkspaceMutationDuringPageDiscardsTheWholeCalculation()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            fixture.Workspace.Tags.Upsert(fixture.Tag with { Description = "unsaved during query" });
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RuntimeRevisionChangeDuringPageDiscardsTheWholeCalculation()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            fixture.Runtime.SetRevision(42);
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RejectsUnknownAssignmentAndInactiveScopeBeforeHistorianRead()
    {
        using (var invalidAssignment = Fixture.Create())
        {
            var (_, _, request) = invalidAssignment.AddCurrentFrequencySignals();
            var result = await invalidAssignment.ExecuteCurrentFrequencyComparisonV1Async(
                request with { AssignmentId = Guid.NewGuid() });

            AssertStatus(StatusCodes.Status404NotFound, result);
            Assert.Equal(0, invalidAssignment.Historian.Calls);
        }

        using (var invalidScope = Fixture.Create())
        {
            var (_, _, request) = invalidScope.AddCurrentFrequencySignals();
            var result = await invalidScope.ExecuteCurrentFrequencyComparisonV1Async(
                request with { ScopeId = Guid.NewGuid() });

            AssertStatus(StatusCodes.Status409Conflict, result);
            Assert.Equal(0, invalidScope.Historian.Calls);
        }
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_AggregateCapIsSharedAcrossTagsAndWindowsAndCountsRejectedRows()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        var pageCounts = new Dictionary<(Guid Tag, DateTimeOffset From), int>();
        var partitions = new List<(Guid Tag, DateTimeOffset From)>();
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var key = (Tag: tagId, From: query.Range.FromUtc!.Value);
            pageCounts.TryGetValue(key, out var localPage);
            if (!pageCounts.ContainsKey(key)) partitions.Add(key);
            pageCounts[key] = localPage + 1;
            var partitionIndex = partitions.IndexOf(key);
            var cursor = $"{partitionIndex}-{localPage + 1}";
            // The first three TAG/window partitions end at 12,400 rows each;
            // the fourth reaches the shared 50,000 cap while advertising another page.
            if (partitionIndex == 3 && localPage == 63)
                cursor = "still-more-after-global-cap";
            else if (partitionIndex < 3 && localPage == 61)
                cursor = null;
            var timestamp = query.Range.FromUtc.Value.AddSeconds(1);
            return ResponseV2(query, Enumerable.Range(0, 200)
                .Select(_ => V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromString("rejected raw row"), null, "NotRecorded"))
                .ToArray(), cursor);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status413PayloadTooLarge, result);
        Assert.Equal(250, fixture.Historian.Calls);
        Assert.Equal(4, pageCounts.Count);
        Assert.All(pageCounts.Values.Take(3), count => Assert.Equal(62, count));
        Assert.Equal(64, pageCounts.Values.Last());
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("INTELLIGENCE_CURRENT_FREQUENCY_WINDOW_TOO_DENSE", error.Code);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RepeatedCursorAndInvalidV2MetadataReturnNoPartialResult()
    {
        using (var repeated = Fixture.Create())
        {
            repeated.AddCurrentFrequencySignals();
            repeated.Historian.Respond = query =>
            {
                var tagId = query.Filters!.Single().Values.Single().AsGuid();
                var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
                return ResponseV2(query,
                    [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(1), timestamp, "Synchronized")],
                    "same-cursor");
            };

            var invalidCursor = await repeated.ExecuteCurrentFrequencyComparisonV1Async();

            AssertStatus(StatusCodes.Status503ServiceUnavailable, invalidCursor);
            Assert.Equal(2, repeated.Historian.Calls);
            Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
                Assert.IsAssignableFrom<IValueHttpResult>(invalidCursor).Value);
        }

        using (var malformed = Fixture.Create())
        {
            malformed.AddCurrentFrequencySignals();
            malformed.Historian.Respond = query => Response(
                query,
                [Row(query.Filters!.Single().Values.Single().AsGuid(), FromUtc.AddMinutes(1),
                    "Good", HistoricalQueryValue.FromDouble(1))]);

            var invalidMetadata = await malformed.ExecuteCurrentFrequencyComparisonV1Async();

            AssertStatus(StatusCodes.Status503ServiceUnavailable, invalidMetadata);
            Assert.Equal(1, malformed.Historian.Calls);
            Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
                Assert.IsAssignableFrom<IValueHttpResult>(invalidMetadata).Value);
        }
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RejectsMissingOrWrongSemanticBindingsBeforeHistorianRead()
    {
        foreach (var wrongRole in new[] { false, true })
        {
            using var fixture = Fixture.Create();
            var (_, _, request) = fixture.AddCurrentFrequencySignals();
            var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            var signals = profile.Signals!.Select(signal => signal.Id == request.FrequencySignalId
                ? signal with
                {
                    SemanticBinding = wrongRole
                        ? signal.SemanticBinding! with
                        {
                            RoleCode = "pump.discharge_pressure",
                            SignalKind = null,
                            FrequencyReference = null,
                            PressureReference = "gauge"
                        }
                        : null
                }
                : signal).ToArray();
            fixture.Workspace.AssetProcessIntelligence.Replace(configuration with
            {
                Profiles = [profile with { Signals = signals }]
            });
            var descriptor = fixture.Workspace.Describe();
            fixture.Workspace.SetCheckout(Fixture.ProjectKey, "invalid semantic binding test",
                descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

            var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

            AssertStatus(StatusCodes.Status400BadRequest, result);
            Assert.Equal(0, fixture.Historian.Calls);
        }
    }

    private static AssetProcessIntelligenceCurrentFrequencyRawObservation CurrentFrequencyRow(
        DateTimeOffset observed,
        string? quality,
        HistoricalQueryValue? value,
        DateTimeOffset? source,
        string? evidence) => new(observed, quality, value, source, evidence);

    private static AssetProcessIntelligenceContextualReferenceRawObservation ContextualReferenceRow(
        DateTimeOffset observed,
        string? quality,
        HistoricalQueryValue? value,
        DateTimeOffset? source,
        string? evidence) => new(observed, quality, value, source, evidence);

    private static AssetProcessIntelligenceContextualDeviationVectorV1Signal VectorSignal(Guid signalId, string key) =>
        new(signalId, key, Guid.NewGuid(), $"Plant.Pump.{key}", null, null, null);

    private static AssetProcessIntelligenceContextualDeviationVectorRawObservation VectorRow(
        DateTimeOffset timestamp,
        double value) => VectorRow(timestamp, "Good", value, timestamp, "Synchronized");

    private static AssetProcessIntelligenceContextualDeviationVectorRawObservation VectorRow(
        DateTimeOffset observedTimestamp,
        string? quality,
        double? value,
        DateTimeOffset? sourceTimestamp,
        string? sourceTimestampEvidence) => new(
        observedTimestamp,
        quality,
        value.HasValue ? HistoricalQueryValue.FromDouble(value.Value) : HistoricalQueryValue.Null(),
        sourceTimestamp,
        sourceTimestampEvidence);

    private static HistoricalQueryRow V2Row(
        Guid tagId,
        DateTimeOffset timestamp,
        string? quality,
        HistoricalQueryValue value,
        DateTimeOffset? sourceTimestamp,
        string? sourceEvidence) =>
        new(new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            ["tag.id"] = HistoricalQueryValue.FromGuid(tagId),
            ["timestamp"] = HistoricalQueryValue.FromDateTime(timestamp),
            ["quality"] = quality is null ? HistoricalQueryValue.Null() : HistoricalQueryValue.FromEnum(quality),
            ["value"] = value,
            ["source.timestamp"] = sourceTimestamp.HasValue
                ? HistoricalQueryValue.FromDateTime(sourceTimestamp.Value)
                : HistoricalQueryValue.Null(),
            ["source.timestamp.status"] = sourceEvidence is null
                ? HistoricalQueryValue.Null()
                : HistoricalQueryValue.FromEnum(sourceEvidence)
        });

    private static HistoricalQueryResponse ResponseV2(
        HistoricalQueryRequest request,
        IReadOnlyList<HistoricalQueryRow> rows,
        string? cursor = null) => new(
        2,
        HistoricalDatasets.HistorianSamplesV2,
        HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamplesV2).Columns,
        rows,
        request.Range.FromUtc!.Value,
        request.Range.ToUtc!.Value,
        cursor,
        request.Page!.Size);

    private static HistoricalQueryResponse PerformanceV1BPage(HistoricalQueryRequest request)
    {
        var tagId = request.Filters!.Single().Values.Single().AsGuid();
        var timestamp = request.Range.FromUtc!.Value.AddMinutes(1);
        return ResponseV2(request,
            [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(1), timestamp, "Synchronized")]);
    }

    private static AssetProcessIntelligenceCurrentFrequencyComparisonV1WindowEvidence EmptyCurrentFrequencyEvidence(
        AssetProcessIntelligenceAnalyticsWindow window)
    {
        var emptySignal = new AssetProcessIntelligenceCurrentFrequencyComparisonV1SignalWindowEvidence(
            0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>(),
            new Dictionary<string, int>());
        return new(window, emptySignal, emptySignal, 0, 0, 0, 0, 0, 0, 0, Array.Empty<AssetProcessIntelligenceCurrentFrequencyComparisonV1PairEvidence>());
    }

    private static AssetProcessIntelligenceRawObservation Observation(
        DateTimeOffset timestamp,
        string? quality,
        HistoricalQueryValue? value) => new(timestamp, quality, value);

    private static AssetProcessIntelligenceAnalyticsWindow Window(DateTimeOffset from, DateTimeOffset to) => new(from, to);

    private static HistoricalQueryRow Row(Guid tagId, DateTimeOffset timestamp, string? quality, HistoricalQueryValue value) =>
        new(new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            ["tag.id"] = HistoricalQueryValue.FromGuid(tagId),
            ["timestamp"] = HistoricalQueryValue.FromDateTime(timestamp),
            ["quality"] = quality is null ? HistoricalQueryValue.Null() : HistoricalQueryValue.FromEnum(quality),
            ["value"] = value
        });

    private static HistoricalQueryResponse Response(
        HistoricalQueryRequest request,
        IReadOnlyList<HistoricalQueryRow> rows,
        string? cursor = null) => new(
        HistoricalQueryContract.Version,
        HistoricalDatasets.HistorianSamples,
        Array.Empty<HistoricalColumn>(),
        rows,
        request.Range.FromUtc!.Value,
        request.Range.ToUtc!.Value,
        cursor,
        request.Page!.Size);

    private static void AssertStatus(int expected, IResult result) =>
        Assert.Equal(expected, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);

    private sealed class Fixture : IDisposable
    {
        public const string ProjectKey = "analytics-test";
        private readonly ServiceProvider _services;

        private Fixture(
            EngineeringWorkspace workspace,
            TagDefinition tag,
            Guid equipmentId,
            Guid scopeId,
            AssetProcessIntelligenceScopeKind scopeKind,
            AssetProcessIntelligenceExecutionGate gate,
            RuntimeHighAvailabilityService highAvailability,
            TestLicenseService licensing,
            TestRuntimeCoordinator runtime,
            InMemoryAuthorityPolicyStore authority,
            RecordingHistoricalQueryService historian,
            AssetProcessIntelligenceAnalyticsService analytics,
            ServiceProvider services)
        {
            Workspace = workspace;
            Tag = tag;
            EquipmentId = equipmentId;
            ScopeId = scopeId;
            ScopeKind = scopeKind;
            Gate = gate;
            HighAvailability = highAvailability;
            Licensing = licensing;
            Runtime = runtime;
            Authority = authority;
            Historian = historian;
            Analytics = analytics;
            _services = services;
            Request = new AssetProcessIntelligenceAnalyticsQueryRequest(
                scopeKind,
                scopeId,
                tag.Id,
                Window(FromUtc, FromUtc.AddHours(1)));
        }

        public EngineeringWorkspace Workspace { get; }
        public TagDefinition Tag { get; }
        public Guid EquipmentId { get; }
        public Guid ScopeId { get; }
        public AssetProcessIntelligenceScopeKind ScopeKind { get; }
        public AssetProcessIntelligenceExecutionGate Gate { get; }
        public RuntimeHighAvailabilityService HighAvailability { get; }
        public TestLicenseService Licensing { get; }
        public TestRuntimeCoordinator Runtime { get; }
        public InMemoryAuthorityPolicyStore Authority { get; }
        public RecordingHistoricalQueryService Historian { get; }
        public AssetProcessIntelligenceAnalyticsService Analytics { get; }
        public AssetProcessIntelligenceAnalyticsQueryRequest Request { get; }
        public AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest? CurrentFrequencyRequest { get; private set; }
        public AssetProcessIntelligencePerformanceV1BQueryRequest? PerformanceV1BRequest { get; private set; }

        public Task<IResult> ExecuteAsync(
            AssetProcessIntelligenceAnalyticsQueryRequest? request = null)
        {
            var context = CreateContext();
            return Analytics.ExecuteAsync(context, request ?? Request, context.RequestAborted);
        }

        public Task<IResult> ExecuteNetObservedRateV1Async(
            AssetProcessIntelligenceNetObservedRateV1QueryRequest? request = null)
        {
            var context = CreateContext();
            return Analytics.ExecuteNetObservedRateV1Async(
                context,
                request ?? new AssetProcessIntelligenceNetObservedRateV1QueryRequest(
                    ScopeKind,
                    ScopeId,
                    Tag.Id,
                    Request.Window),
                context.RequestAborted);
        }

        public Task<IResult> ExecuteProfileCoverageAsync(Guid? scopeId = null)
        {
            var profile = Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single();
            var context = CreateContext();
            return Analytics.ExecuteProfileCoverageAsync(
                context,
                new AssetProcessIntelligenceProfileCoverageQueryRequest(
                    ScopeKind,
                    scopeId ?? ScopeId,
                    profile.Id,
                    Window(FromUtc, FromUtc.AddHours(1))),
                context.RequestAborted);
        }

        public Task<IResult> ExecuteCurrentFrequencyComparisonV1Async(
            AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest? request = null)
        {
            var context = CreateContext();
            return Analytics.ExecuteCurrentFrequencyComparisonV1Async(
                context,
                request ?? CurrentFrequencyRequest ?? throw new InvalidOperationException("Current/frequency signals were not configured."),
                context.RequestAborted);
        }

        public Task<IResult> ExecuteContextualReferenceDeviationV1Async(
            AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var context = CreateContext();
            return Analytics.ExecuteContextualReferenceDeviationV1Async(context, request, cancellationToken);
        }

        public Task<IResult> ExecuteContextualDeviationVectorV1Async(
            AssetProcessIntelligenceContextualDeviationVectorV1QueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var context = CreateContext();
            return Analytics.ExecuteContextualDeviationVectorV1Async(context, request, cancellationToken);
        }

        public Task<IResult> ExecutePerformanceV1BAsync(AssetProcessIntelligencePerformanceV1BQueryRequest? request = null)
        {
            var context = CreateContext();
            return Analytics.ExecutePerformanceV1BAsync(
                context,
                request ?? PerformanceV1BRequest ?? throw new InvalidOperationException("Performance signals were not configured."),
                context.RequestAborted);
        }

        public (AssetProcessIntelligencePerformanceV1BQueryRequest Request, Dictionary<Guid, TagDefinition> Tags)
            AddPerformanceSignals(string ruleId, string productionRole = "process.net_good_production_volume_rate")
        {
            var specifications = ruleId switch
            {
                "specific-active-energy-at-recorded-production-rate.v1" => new[]
                {
                    ("equipment.active_input_power", "ActivePower", 2, "unit.power.kilowatt"),
                    (productionRole, "ProductionRate", 2, productionRole switch
                    {
                        "process.net_good_production_mass_rate" => "unit.mass_flow.kilogram_per_hour",
                        "process.net_good_production_item_rate" => "unit.item_rate.item_per_hour",
                        _ => "unit.volumetric_flow.cubic_metre_per_second"
                    })
                },
                "recorded-differential-pressure-power.v1" => new[]
                {
                    ("pump.discharge_volumetric_flow", "Flow", 1, "unit.volumetric_flow.cubic_metre_per_hour"),
                    ("pump.direct_differential_pressure", "DifferentialPressure", 2, "unit.pressure.bar")
                },
                "recorded-pressure-power-input-ratio.v1" => new[]
                {
                    ("pump.discharge_volumetric_flow", "Flow", 1, "unit.volumetric_flow.cubic_metre_per_hour"),
                    ("pump.direct_differential_pressure", "DifferentialPressure", 2, "unit.pressure.bar"),
                    ("equipment.active_input_power", "ActivePower", 2, "unit.power.watt")
                },
                _ => throw new ArgumentOutOfRangeException(nameof(ruleId))
            };
            var tags = new Dictionary<Guid, TagDefinition>();
            var signals = new List<AssetProcessIntelligenceSignalEngineeringDto>();
            foreach (var (role, suffix, semanticCatalog, unitCode) in specifications)
            {
                var tag = new TagDefinition(
                    Guid.NewGuid(), suffix, $"Plant.Pump.{suffix}", TagDataType.Double, null, null, null, true,
                    ControlledEngineeringUnit: new ControlledEngineeringUnitReference(
                        suffix == "DifferentialPressure" || suffix == "Flow" ? 1 : 2, unitCode));
                Workspace.Tags.Register(tag);
                Runtime.AddTag(tag);
                tags.Add(tag.Id, tag);
                var binding = role switch
                {
                    "equipment.active_input_power" => new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
                        2, role, SignalKind: "measured_total_input"),
                    "process.net_good_production_volume_rate" or "process.net_good_production_mass_rate" or "process.net_good_production_item_rate" =>
                        new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(2, role, SignalKind: "measured_output"),
                    "pump.discharge_volumetric_flow" => new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
                        1, role, FlowDirection: "pump_to_process"),
                    "pump.direct_differential_pressure" => new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
                        2, role, SignalKind: "measured_output", PressureReference: "differential_outlet_minus_inlet"),
                    _ => throw new InvalidOperationException("Unexpected performance role.")
                };
                signals.Add(new AssetProcessIntelligenceSignalEngineeringDto(
                    Guid.NewGuid(), suffix, "legacy role is intentionally opaque", tag.Id, SemanticBinding: binding));
            }
            var configuration = Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            Workspace.AssetProcessIntelligence.Replace(configuration with
            {
                Profiles = [profile with { Signals = profile.Signals!.Concat(signals).ToArray() }]
            });
            var descriptor = Workspace.Describe();
            Workspace.SetCheckout(ProjectKey, "Performance V1B test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
            var assignment = configuration.Assignments!.Single();
            var request = new AssetProcessIntelligencePerformanceV1BQueryRequest(
                ScopeKind, ScopeId, assignment.Id, profile.Id, ruleId,
                Array.AsReadOnly(signals.Select(signal => signal.Id).ToArray()),
                Window(FromUtc, FromUtc.AddHours(1)));
            PerformanceV1BRequest = request;
            return (request, tags);
        }

        public (TagDefinition CurrentTag, TagDefinition FrequencyTag,
            AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest Request) AddCurrentFrequencySignals(
            ControlledEngineeringUnitReference? currentUnit = null,
            TagAccessPolicy? frequencyAccessPolicy = null)
        {
            currentUnit ??= new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere");
            var currentTag = new TagDefinition(
                Guid.NewGuid(), "Current", "Plant.Pump.Current", TagDataType.Double,
                null, "A", null, true,
                AccessPolicy: null,
                ControlledEngineeringUnit: currentUnit);
            var frequencyTag = new TagDefinition(
                Guid.NewGuid(), "Drive frequency", "Plant.Pump.DriveFrequency", TagDataType.Double,
                null, "Hz", null, true,
                AccessPolicy: frequencyAccessPolicy,
                ControlledEngineeringUnit: new ControlledEngineeringUnitReference(1, "unit.frequency.hertz"));
            Workspace.Tags.Register(currentTag);
            Workspace.Tags.Register(frequencyTag);
            Runtime.AddTag(currentTag);
            Runtime.AddTag(frequencyTag);

            var configuration = Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            var currentSignalId = Guid.NewGuid();
            var frequencySignalId = Guid.NewGuid();
            var currentSignal = new AssetProcessIntelligenceSignalEngineeringDto(
                currentSignalId, "electrical-current", "opaque legacy text", currentTag.Id,
                SemanticBinding: new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
                    1, "pump.electrical_current", CurrentBasis: "line_rms"));
            var frequencySignal = new AssetProcessIntelligenceSignalEngineeringDto(
                frequencySignalId, "measured-drive-frequency", "also opaque", frequencyTag.Id,
                SemanticBinding: new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
                    1, "pump.drive_frequency", SignalKind: "measured_output", FrequencyReference: "drive_electrical"));
            Workspace.AssetProcessIntelligence.Replace(configuration with
            {
                Profiles = [profile with
                {
                    Signals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                        .Concat([currentSignal, frequencySignal]).ToArray()
                }]
            });
            var descriptor = Workspace.Describe();
            Workspace.SetCheckout(ProjectKey, "Stage 3B test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
            var assignmentId = configuration.Assignments!.Single().Id;
            var request = new AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest(
                ScopeKind,
                ScopeId,
                assignmentId,
                currentSignalId,
                frequencySignalId,
                Window(FromUtc, FromUtc.AddHours(1)),
                Window(FromUtc.AddHours(2), FromUtc.AddHours(3)),
                49,
                51);
            CurrentFrequencyRequest = request;
            return (currentTag, frequencyTag, request);
        }

        public (AssetProcessIntelligenceContextualDeviationVectorV1QueryRequest Request,
            TagDefinition TargetA, TagDefinition TargetB, TagDefinition Condition) AddContextualDeviationVectorSignals(
            TagAccessPolicy? targetBAccessPolicy = null,
            TagAccessPolicy? conditionAccessPolicy = null)
        {
            var (targetA, condition, currentFrequencyRequest) = AddCurrentFrequencySignals(
                frequencyAccessPolicy: conditionAccessPolicy);
            var targetB = new TagDefinition(
                Guid.NewGuid(), "Flow", "Plant.Pump.Flow", TagDataType.Double,
                null, "m³/h", null, true,
                AccessPolicy: targetBAccessPolicy);
            Workspace.Tags.Register(targetB);
            Runtime.AddTag(targetB);

            var configuration = Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            var targetBSignal = new AssetProcessIntelligenceSignalEngineeringDto(
                Guid.NewGuid(), "pump-flow", "opaque legacy text", targetB.Id);
            Workspace.AssetProcessIntelligence.Replace(configuration with
            {
                Profiles = [profile with
                {
                    Signals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                        .Append(targetBSignal).ToArray()
                }]
            });
            var descriptor = Workspace.Describe();
            Workspace.SetCheckout(ProjectKey, "Contextual deviation vector test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

            var request = new AssetProcessIntelligenceContextualDeviationVectorV1QueryRequest(
                ScopeKind, ScopeId, currentFrequencyRequest.AssignmentId,
                Array.AsReadOnly(new[] { currentFrequencyRequest.CurrentSignalId, targetBSignal.Id }),
                currentFrequencyRequest.FrequencySignalId,
                Window(FromUtc, FromUtc.AddHours(1)), Window(FromUtc.AddHours(2), FromUtc.AddHours(3)), 49d, 51d);
            return (request, targetA, targetB, condition);
        }

        public TagDefinition AddProfileSignal(string key, string role, TagDataType dataType, string? unit)
        {
            var tag = new TagDefinition(
                Guid.NewGuid(),
                key,
                $"Plant.Pump.{key}",
                dataType,
                null,
                unit,
                null,
                true);
            Workspace.Tags.Register(tag);
            Runtime.AddTag(tag);
            var configuration = Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            var signal = new AssetProcessIntelligenceSignalEngineeringDto(
                Guid.NewGuid(),
                key,
                role,
                tag.Id);
            Workspace.AssetProcessIntelligence.Replace(configuration with
            {
                Profiles = [profile with { Signals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>()).Append(signal).ToArray() }]
            });
            var descriptor = Workspace.Describe();
            Workspace.SetCheckout(ProjectKey, "Stage 3B test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
            return tag;
        }

        private static DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "analytics-user"),
                new Claim("role", "operator")
            ],
            "analytics-test"));
            return context;
        }

        public static Fixture Create(
            bool licensed = true,
            bool grantTagRead = true,
            bool grantTrendUse = true,
            bool grantPermit = true,
            bool activeScope = true,
            long workspaceRevision = 41,
            long? runtimeRevision = null,
            AssetProcessIntelligenceScopeKind scopeKind = AssetProcessIntelligenceScopeKind.Equipment,
            bool nestedLocation = false,
            TagDataType tagDataType = TagDataType.Double,
            string? tagUnit = "bar")
        {
            var runtimeVersion = runtimeRevision ?? workspaceRevision;
            var workspace = new EngineeringWorkspace(seedDemo: false);
            var tagId = Guid.NewGuid();
            var tag = new TagDefinition(
                tagId,
                "Pressure",
                "Plant.Pump.Pressure",
                tagDataType,
                null,
                tagUnit,
                null,
                true);
            workspace.Tags.Register(tag);
            var equipmentId = Guid.NewGuid();
            var scopeId = scopeKind == AssetProcessIntelligenceScopeKind.Equipment
                ? equipmentId
                : Guid.NewGuid();
            Guid? locationId = null;
            if (scopeKind == AssetProcessIntelligenceScopeKind.Location)
            {
                var targetLocation = new LocationEngineeringDto(scopeId, "Plant");
                workspace.Assets.UpsertLocation(targetLocation);
                locationId = scopeId;
                if (nestedLocation)
                {
                    var child = Guid.NewGuid();
                    workspace.Assets.UpsertLocation(new LocationEngineeringDto(child, "Area", scopeId));
                    locationId = child;
                }
            }
            workspace.Assets.UpsertEquipment(new EquipmentEngineeringDto(
                equipmentId,
                "Plant.Pump",
                "Pump",
                LocationId: locationId));
            var profileId = Guid.NewGuid();
            var assignmentId = Guid.NewGuid();
            workspace.AssetProcessIntelligence.Replace(new AssetProcessIntelligenceEngineeringDto(
                Version: 2,
                Scopes: [new AssetProcessIntelligenceScopeEngineeringDto(scopeKind, scopeId, activeScope)],
                Profiles: [new AssetProcessIntelligenceProfileEngineeringDto(
                    profileId,
                    "pump.profile",
                    "Pump profile",
                    [new AssetProcessIntelligenceSignalEngineeringDto(Guid.NewGuid(), "pressure", "free-text-only", tagId)])],
                Assignments: [new AssetProcessIntelligenceAssignmentEngineeringDto(assignmentId, equipmentId, profileId)],
                ProcessStages: []));
            workspace.SetCheckout(ProjectKey, "Stage 3A test", workspaceRevision, DateTimeOffset.UtcNow);

            var roleId = Guid.NewGuid();
            var grants = new List<CapabilityGrantEngineeringDto>();
            if (grantTrendUse) grants.Add(new CapabilityGrantEngineeringDto(SecurityCapability.TrendUse));
            if (grantTagRead) grants.Add(new CapabilityGrantEngineeringDto(SecurityCapability.TagRead));
            var authority = new InMemoryAuthorityPolicyStore(
                [new SecurityRoleEngineeringDto(roleId, "operator", "Operator", Grants: grants)],
                []);

            var runtime = new TestRuntimeCoordinator(ProjectKey, runtimeVersion, tag);
            var runtimeFacade = new ScadaRuntimeFacade(runtime);
            var highAvailability = new RuntimeHighAvailabilityService(new ConfigurationBuilder().Build());
            var gate = new AssetProcessIntelligenceExecutionGate();
            if (grantPermit)
                gate.Activate(ProjectKey, runtimeVersion, scopeKind, scopeId);
            var licensing = new TestLicenseService(licensed);
            var historian = new RecordingHistoricalQueryService();
            var persistence = PersistenceProxy.Create(new EngineeringProjectSnapshot(
                runtimeVersion,
                ProjectKey,
                "Stage 3A test",
                "engineering",
                25,
                DateTimeOffset.UtcNow,
                "{}"));
            var services = new ServiceCollection()
                .AddSingleton<IEngineeringProjectPersistenceService>(persistence)
                .AddSingleton(highAvailability)
                .BuildServiceProvider();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Enabled"] = "true" })
                .Build();
            var security = new ApiAuthorizationService(services, authority, configuration);
            var analytics = new AssetProcessIntelligenceAnalyticsService(
                workspace,
                runtimeFacade,
                highAvailability,
                licensing,
                gate,
                security,
                historian);
            return new Fixture(workspace, tag, equipmentId, scopeId, scopeKind,
                gate, highAvailability, licensing, runtime, authority, historian, analytics, services);
        }

        public void Dispose()
        {
            Workspace.Dispose();
            _services.Dispose();
        }
    }

    private sealed class RecordingHistoricalQueryService : IHistoricalQueryService
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public List<HistoricalQueryRequest> Requests { get; } = [];
        public Func<HistoricalQueryRequest, HistoricalQueryResponse>? Respond { get; set; }
        public Func<HistoricalQueryRequest, CancellationToken, Task<HistoricalQueryResponse>>? RespondAsync { get; set; }

        public async Task<HistoricalQueryResponse> QueryAsync(
            HistoricalQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            Requests.Add(request);
            if (RespondAsync is not null)
                return await RespondAsync(request, cancellationToken);
            return Respond?.Invoke(request) ?? Response(request, []);
        }
    }

    private sealed class TestLicenseService(bool licensed) : IProductLicenseService
    {
        public bool Licensed { get; set; } = licensed;
        public string MachineFingerprint => "analytics-test-machine";
        public string MachineRequestCode => "analytics-test-request";
        public LicenseVerificationResult CurrentVerification => new(
            LicenseState.Valid,
            V3Entitlements: new MachineLicenseV3Entitlements(1, 1, false, Licensed));
        public LicenseVerificationResult VerifyCandidate(string licenseCode) => CurrentVerification;
        public void InstallLicense(string licenseCode) => throw new NotSupportedException();
        public void RemoveLicense() => throw new NotSupportedException();
        public RunEntitlementDecision EvaluateRun(int projectTagCount) =>
            ProductEntitlementEvaluator.Evaluate(CurrentVerification, projectTagCount);
    }

    private sealed class TestRuntimeCoordinator(string projectKey, long revision, TagDefinition tag)
        : IEngineeringRuntimeCoordinator
    {
        private readonly List<TagDefinition> _tags = [tag];
        private RuntimeDescriptor _descriptor = new(
            projectKey, revision, DateTimeOffset.Parse("2026-10-14T00:00:00Z"), [], [], 1, 0);

        public RuntimeDescriptor Describe() => _descriptor;
        public void SetRevision(long revision) => _descriptor = _descriptor with { Revision = revision };
        public void AddTag(TagDefinition tag) => _tags.Add(tag);
        public void RemoveTag(Guid tagId) => _tags.RemoveAll(existing => existing.Id == tagId);
        public void SetTag(TagDefinition tag)
        {
            _tags.RemoveAll(existing => existing.Id == tag.Id);
            _tags.Add(tag);
        }
        public IReadOnlyCollection<TagDefinition> Tags() => _tags.ToArray();
        public IReadOnlyCollection<TagValue> CurrentValues() => [];
        public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => [];
        public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => [];
        public IReadOnlyCollection<CommandDefinition> Commands() => [];
        public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => [];
        public bool TryGetTag(Guid tagId, out TagDefinition? result)
        {
            result = _tags.SingleOrDefault(item => item.Id == tagId);
            return result is not null;
        }
        public bool TryGetTagByPath(string path, out TagDefinition? result)
        {
            result = _tags.SingleOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
            return result is not null;
        }
        public bool TryGetCurrent(Guid tagId, out TagValue? value) { value = null; return false; }
        public bool TryGetCommand(Guid commandId, out CommandDefinition? command) { command = null; return false; }
        public bool IsServerMemoryTag(Guid tagId) => false;
        public ValueTask<bool> AcknowledgeAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> ShelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> UnshelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ResetServerMemoryRetainedValueAsync(Guid tagId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ExecuteCommandAsync(Guid commandId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public class PersistenceProxy : DispatchProxy
    {
        public EngineeringProjectSnapshot? ActiveSnapshot { get; set; }

        public static IEngineeringProjectPersistenceService Create(EngineeringProjectSnapshot active)
        {
            var persistence = DispatchProxy.Create<IEngineeringProjectPersistenceService, PersistenceProxy>();
            ((PersistenceProxy)(object)persistence).ActiveSnapshot = active;
            return persistence;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IEngineeringProjectPersistenceService.LoadActiveAsync))
                return Task.FromResult(ActiveSnapshot);
            throw new NotSupportedException($"Persistence method '{targetMethod?.Name}' is not part of this test.");
        }
    }
}
