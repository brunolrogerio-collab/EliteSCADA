using System.Collections.ObjectModel;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;

namespace Scada.Api.Licensing;

internal sealed record AssetProcessIntelligenceContextualDeviationVectorRawObservation(
    DateTimeOffset ObservedTimestampUtc,
    string? Quality,
    HistoricalQueryValue? Value,
    DateTimeOffset? SourceTimestampUtc,
    string? SourceTimestampEvidence);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV1QueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid AssignmentId,
    IReadOnlyList<Guid>? TargetSignalIds,
    Guid ConditionSignalId,
    AssetProcessIntelligenceAnalyticsWindow? ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow? EvaluationWindow,
    double ConditionLowerInclusiveCanonical,
    double ConditionUpperExclusiveCanonical);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV1Signal(
    Guid SignalId,
    string SignalKey,
    Guid TagId,
    string TagPath,
    string? EngineeringUnit,
    string? ControlledUnitCode,
    string? CanonicalUnit);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV1SignalWindowEvidence(
    Guid SignalId,
    int RawObservationCount,
    int GoodFiniteNumericCount,
    int AcceptedSynchronizedCount,
    int RejectedObservationCount,
    int AmbiguousSourceTimestampGroupCount,
    int AmbiguousSourceTimestampRowCount,
    IReadOnlyDictionary<string, int> QualityCounts,
    IReadOnlyDictionary<string, int> RejectionCounts);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV1TargetStatistics(
    AssetProcessIntelligenceContextualDeviationVectorV1Signal Signal,
    double? ReferenceMedian,
    double? ReferenceMad,
    int CompleteReferenceVectorCount,
    int InRangeReferenceVectorCount,
    int CompleteEvaluationVectorCount,
    int ReturnedEvaluationValueCount,
    string Status,
    string? ScoreUnavailableReason);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV1Value(
    Guid TargetSignalId,
    double Value,
    double? RawDifferenceFromReferenceMedian,
    double? RobustScore,
    string? ScoreUnavailableReason);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV1Sample(
    DateTimeOffset SourceTimestampUtc,
    DateTimeOffset ConditionObservedTimestampUtc,
    double ConditionValueCanonical,
    bool ConditionInRange,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1Value> Targets);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV1Response(
    int ContractVersion,
    int CatalogVersion,
    string RuleId,
    string RuleVersion,
    string ProjectKey,
    long ActiveRevision,
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid AssignmentId,
    Guid EquipmentId,
    Guid ProfileId,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1Signal> TargetSignals,
    AssetProcessIntelligenceContextualDeviationVectorV1Signal ConditionSignal,
    AssetProcessIntelligenceAnalyticsWindow ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow EvaluationWindow,
    double ConditionLowerInclusiveCanonical,
    double ConditionUpperExclusiveCanonical,
    string ConditionCanonicalUnit,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1SignalWindowEvidence> ReferenceSignalEvidence,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1SignalWindowEvidence> EvaluationSignalEvidence,
    int CompleteReferenceVectorCount,
    int InRangeReferenceVectorCount,
    int CompleteEvaluationVectorCount,
    int ReturnedEvaluationVectorCount,
    int OmittedEvaluationVectorCount,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1TargetStatistics> TargetStatistics,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1Sample> EvaluationVectors,
    int TotalRawObservationCount,
    AssetProcessIntelligenceContextualDeviationVectorV1Provenance Provenance);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV1Provenance(
    string Dataset,
    int DatasetContractVersion,
    string QueryWindowTimestampBasis,
    string VectorTimestampBasis,
    string SourceTimestampEvidenceRequirement,
    string ConditionInterval,
    string RegimeEquivalence,
    string Completeness,
    string Interpretation);

public static class AssetProcessIntelligenceContextualDeviationVectorV1Catalog
{
    public const int ContractVersion = 1;
    public const int CatalogVersion = 1;
    public const string RuleId = "contextual-deviation-vector.v1";
    public const string RuleVersion = "1.0.0";
    public const int MinimumTargetSignals = 2;
    public const int MaximumTargetSignals = 8;
    public const int MinimumReferenceVectorsForRobustScore = 30;
    public const int MaximumRawRowsAcrossQuery = 50000;
    public const int QueryPageSize = 200;
    public const int MaximumReturnedEvaluationVectors = 500;
    public const double RobustScoreConstant = 0.67448975d;
    public static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(31);

    public static object Describe() => new
    {
        contractVersion = ContractVersion,
        catalogVersion = CatalogVersion,
        ruleId = RuleId,
        ruleVersion = RuleVersion,
        dataset = HistoricalDatasets.HistorianSamplesV2,
        datasetContractVersion = 2,
        minimumTargetSignals = MinimumTargetSignals,
        maximumTargetSignals = MaximumTargetSignals,
        maximumWindowDays = (int)MaximumWindow.TotalDays,
        maximumRawRowsAcrossAllSignalsAndWindows = MaximumRawRowsAcrossQuery,
        pageSize = QueryPageSize,
        maximumReturnedEvaluationVectors = MaximumReturnedEvaluationVectors,
        minimumCompleteReferenceVectorsForRobustScore = MinimumReferenceVectorsForRobustScore,
        conditionInterval = "lowerInclusive <= canonical condition value < upperExclusive",
        vectorTimestamp = "exact common non-null UTC source.timestamp across every selected target and the condition TAG",
        ordering = "target signals and per-vector target values follow caller selection order; evaluation vectors are ordered by source timestamp ascending; condition evidence follows target evidence",
        evidence = "every row requires Good finite numeric value and explicit Synchronized source timestamp evidence",
        alignment = "exact source timestamp equality; no nearest join, hold, interpolation, imputation, or proof of physical simultaneity",
        regimeEquivalence = "not_assessed",
        completeness = "unknown",
        interpretation = "per-target descriptive median, MAD, raw difference and robust score; no combined anomaly, diagnosis, fault, or RUL",
        supportedStatuses = new[] { "available", "insufficient_data" }
    };
}

internal sealed record AssetProcessIntelligenceContextualDeviationVectorCalculation(
    int CompleteReferenceVectorCount,
    int InRangeReferenceVectorCount,
    int CompleteEvaluationVectorCount,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1SignalWindowEvidence> ReferenceEvidence,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1SignalWindowEvidence> EvaluationEvidence,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1TargetStatistics> TargetStatistics,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1Sample> EvaluationVectors,
    int OmittedEvaluationVectors);

internal static class AssetProcessIntelligenceContextualDeviationVectorV1Calculator
{
    private const string MissingTimestamp = "source_timestamp_missing";
    private const string Unsynchronized = "source_timestamp_not_synchronized";
    private const string OutsideWindow = "source_timestamp_outside_requested_window";
    private const string DuplicateTimestamp = "duplicate_source_timestamp";

    public static AssetProcessIntelligenceContextualDeviationVectorCalculation Calculate(
        IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1Signal> targets,
        AssetProcessIntelligenceContextualDeviationVectorV1Signal condition,
        AssetProcessIntelligenceAnalyticsWindow referenceWindow,
        AssetProcessIntelligenceAnalyticsWindow evaluationWindow,
        IReadOnlyDictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>> referenceRows,
        IReadOnlyDictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>> evaluationRows,
        ControlledEngineeringUnitDefinition conditionUnit,
        double lowerInclusive,
        double upperExclusive)
    {
        // Preserve the caller's selected target order; timestamps remain independently sorted below.
        var orderedTargets = targets.ToArray();
        var signals = orderedTargets.Append(condition).ToArray();
        var reference = signals.ToDictionary(signal => signal.SignalId,
            signal => Evaluate(referenceRows[signal.SignalId], referenceWindow,
                signal.SignalId == condition.SignalId ? conditionUnit : null));
        var evaluation = signals.ToDictionary(signal => signal.SignalId,
            signal => Evaluate(evaluationRows[signal.SignalId], evaluationWindow,
                signal.SignalId == condition.SignalId ? conditionUnit : null));
        var referenceEvidence = signals.Select(signal => ToEvidence(signal.SignalId, reference[signal.SignalId])).ToArray();
        var evaluationEvidence = signals.Select(signal => ToEvidence(signal.SignalId, evaluation[signal.SignalId])).ToArray();

        var referenceTimes = IntersectCompleteTimestamps(signals, reference);
        var conditionReference = reference[condition.SignalId].Accepted;
        var completeReference = referenceTimes.OrderBy(timestamp => timestamp).Select(timestamp => new Vector(timestamp,
            conditionReference[timestamp], orderedTargets.ToDictionary(target => target.SignalId,
                target => reference[target.SignalId].Accepted[timestamp]))).ToArray();
        var inRangeReference = completeReference.Where(vector => InRange(vector.Condition.Value, lowerInclusive, upperExclusive)).ToArray();

        var evaluationTimes = IntersectCompleteTimestamps(signals, evaluation);
        var conditionEvaluation = evaluation[condition.SignalId].Accepted;
        var completeEvaluation = evaluationTimes.OrderBy(timestamp => timestamp).Select(timestamp => new Vector(timestamp,
            conditionEvaluation[timestamp], orderedTargets.ToDictionary(target => target.SignalId,
                target => evaluation[target.SignalId].Accepted[timestamp]))).ToArray();
        var returnedEvaluation = completeEvaluation
            .Take(AssetProcessIntelligenceContextualDeviationVectorV1Catalog.MaximumReturnedEvaluationVectors)
            .ToArray();

        var perTarget = new List<AssetProcessIntelligenceContextualDeviationVectorV1TargetStatistics>(orderedTargets.Length);
        var medians = new Dictionary<Guid, double?>();
        var mads = new Dictionary<Guid, double?>();
        foreach (var target in orderedTargets)
        {
            var values = inRangeReference.Select(vector => vector.Targets[target.SignalId].Value).ToArray();
            var median = Median(values);
            var mad = median.HasValue ? Median(values.Select(value => Math.Abs(value - median.Value)).ToArray()) : null;
            var reason = values.Length == 0 ? "no_complete_in_range_reference_vectors"
                : values.Length < AssetProcessIntelligenceContextualDeviationVectorV1Catalog.MinimumReferenceVectorsForRobustScore
                    ? "fewer_than_30_complete_reference_vectors"
                    : !median.HasValue || !mad.HasValue ? "non_finite_reference_statistic"
                    : mad.Value == 0d ? "reference_mad_zero" : null;
            var status = !median.HasValue || !mad.HasValue ? "insufficient_data"
                : reason is null ? "available" : "insufficient_data";
            medians[target.SignalId] = median;
            mads[target.SignalId] = mad;
            perTarget.Add(new(target, median, mad, completeReference.Length, inRangeReference.Length,
                completeEvaluation.Length,
                returnedEvaluation.Count(vector => InRange(vector.Condition.Value, lowerInclusive, upperExclusive)),
                status, reason));
        }

        var returned = returnedEvaluation
            .Select(vector =>
            {
                var inRange = InRange(vector.Condition.Value, lowerInclusive, upperExclusive);
                var values = orderedTargets.Select(target =>
                {
                    double? difference = null;
                    double? score = null;
                    string? scoreReason = null;
                    if (!inRange) scoreReason = "condition_outside_requested_range";
                    else if (!medians[target.SignalId].HasValue) scoreReason = "no_complete_in_range_reference_vectors";
                    else
                    {
                        var candidate = vector.Targets[target.SignalId].Value - medians[target.SignalId]!.Value;
                        if (!double.IsFinite(candidate)) scoreReason = "non_finite_raw_difference";
                        else
                        {
                            difference = candidate;
                            var count = inRangeReference.Length;
                            var mad = mads[target.SignalId];
                            if (count < AssetProcessIntelligenceContextualDeviationVectorV1Catalog.MinimumReferenceVectorsForRobustScore)
                                scoreReason = "fewer_than_30_complete_reference_vectors";
                            else if (!mad.HasValue) scoreReason = "non_finite_reference_statistic";
                            else if (mad.Value == 0d) scoreReason = "reference_mad_zero";
                            else
                            {
                                var candidateScore = AssetProcessIntelligenceContextualDeviationVectorV1Catalog.RobustScoreConstant * candidate / mad.Value;
                                if (double.IsFinite(candidateScore)) score = candidateScore;
                                else scoreReason = "non_finite_robust_score";
                            }
                        }
                    }
                    return new AssetProcessIntelligenceContextualDeviationVectorV1Value(
                        target.SignalId, vector.Targets[target.SignalId].Value, difference, score, scoreReason);
                }).ToArray();
                return new AssetProcessIntelligenceContextualDeviationVectorV1Sample(
                    vector.Timestamp, vector.Condition.ObservedTimestampUtc, vector.Condition.Value,
                    inRange, Array.AsReadOnly(values));
            }).ToArray();

        return new(completeReference.Length, inRangeReference.Length, completeEvaluation.Length,
            Array.AsReadOnly(referenceEvidence), Array.AsReadOnly(evaluationEvidence),
            Array.AsReadOnly(perTarget.ToArray()), Array.AsReadOnly(returned),
            Math.Max(0, completeEvaluation.Length - returned.Length));
    }

    private static HashSet<DateTimeOffset> IntersectCompleteTimestamps(
        IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV1Signal> signals,
        IReadOnlyDictionary<Guid, SignalEvaluation> evaluations)
    {
        HashSet<DateTimeOffset>? common = null;
        foreach (var signal in signals)
        {
            var timestamps = evaluations[signal.SignalId].Accepted.Keys.ToHashSet();
            if (common is null) common = timestamps;
            else common.IntersectWith(timestamps);
        }
        return common ?? [];
    }

    private static SignalEvaluation Evaluate(
        IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation> rows,
        AssetProcessIntelligenceAnalyticsWindow window,
        ControlledEngineeringUnitDefinition? canonicalUnit)
    {
        var qualities = Enum.GetValues<TagQuality>().ToDictionary(value => value.ToString(), _ => 0, StringComparer.Ordinal);
        qualities.Add("Missing", 0);
        qualities.Add("Unrecognized", 0);
        var rejected = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var candidates = new List<Accepted>();
        var goodNumericCount = 0;
        foreach (var row in rows)
        {
            var quality = AssetProcessIntelligenceKpiV1.NormalizeQuality(row.Quality);
            qualities[quality]++;
            var numeric = AssetProcessIntelligenceKpiV1.TryGetAcceptedGoodNumericValue(
                new AssetProcessIntelligenceRawObservation(row.ObservedTimestampUtc, row.Quality, row.Value), out var value);
            if (numeric) goodNumericCount++;
            if (quality == "Missing") { Increment(rejected, "quality_missing"); continue; }
            if (quality == "Unrecognized") { Increment(rejected, "quality_unrecognized"); continue; }
            if (quality != TagQuality.Good.ToString()) { Increment(rejected, "quality_not_good"); continue; }
            if (!numeric) { Increment(rejected, "value_not_finite_numeric"); continue; }
            if (!row.SourceTimestampUtc.HasValue) { Increment(rejected, MissingTimestamp); continue; }
            if (!Enum.TryParse<TimestampEvidenceState>(row.SourceTimestampEvidence, true, out var evidence) || !Enum.IsDefined(evidence))
            { Increment(rejected, "source_timestamp_evidence_unrecognized"); continue; }
            if (evidence != TimestampEvidenceState.Synchronized) { Increment(rejected, Unsynchronized); continue; }
            var timestamp = row.SourceTimestampUtc.Value;
            if (timestamp.Offset != TimeSpan.Zero || timestamp < window.FromUtc || timestamp > window.ToUtc)
            { Increment(rejected, OutsideWindow); continue; }
            if (canonicalUnit is not null)
            {
                value *= canonicalUnit.ScaleToCanonical;
                if (!double.IsFinite(value))
                { Increment(rejected, "condition_unit_conversion_unavailable"); continue; }
            }
            candidates.Add(new(row.ObservedTimestampUtc, timestamp, value));
        }

        var duplicateGroups = rows.Where(row => row.SourceTimestampUtc is { } timestamp && timestamp.Offset == TimeSpan.Zero &&
                timestamp >= window.FromUtc && timestamp <= window.ToUtc)
            .GroupBy(row => row.SourceTimestampUtc!.Value)
            .Where(group => group.Count() > 1).ToArray();
        foreach (var duplicate in duplicateGroups)
            Increment(rejected, DuplicateTimestamp, duplicate.Count());
        var duplicateTimes = duplicateGroups.Select(group => group.Key).ToHashSet();
        var accepted = candidates.Where(sample => !duplicateTimes.Contains(sample.SourceTimestampUtc))
            .ToDictionary(sample => sample.SourceTimestampUtc);
        return new(rows.Count, goodNumericCount, accepted, qualities, rejected, duplicateGroups.Length,
            duplicateGroups.Sum(group => group.Count()));
    }

    private static AssetProcessIntelligenceContextualDeviationVectorV1SignalWindowEvidence ToEvidence(
        Guid signalId,
        SignalEvaluation signal) => new(signalId, signal.RawCount, signal.GoodNumericCount,
        signal.Accepted.Count, Math.Max(0, signal.RawCount - signal.Accepted.Count),
        signal.AmbiguousGroups, signal.AmbiguousRows,
        new ReadOnlyDictionary<string, int>(signal.QualityCounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)),
        new ReadOnlyDictionary<string, int>(signal.RejectionCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)));

    private static bool InRange(double value, double lowerInclusive, double upperExclusive) =>
        value >= lowerInclusive && value < upperExclusive;

    private static double? Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0 || values.Any(value => !double.IsFinite(value))) return null;
        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;
        var median = sorted.Length % 2 == 1 ? sorted[middle] : sorted[middle - 1] / 2d + sorted[middle] / 2d;
        return double.IsFinite(median) ? median : null;
    }

    private static void Increment(IDictionary<string, int> values, string key, int amount = 1)
    {
        if (amount > 0) values[key] = values.TryGetValue(key, out var current) ? current + amount : amount;
    }

    private sealed record Accepted(DateTimeOffset ObservedTimestampUtc, DateTimeOffset SourceTimestampUtc, double Value);
    private sealed record Vector(DateTimeOffset Timestamp, Accepted Condition, IReadOnlyDictionary<Guid, Accepted> Targets);
    private sealed record SignalEvaluation(int RawCount, int GoodNumericCount, IReadOnlyDictionary<DateTimeOffset, Accepted> Accepted,
        IReadOnlyDictionary<string, int> QualityCounts, IReadOnlyDictionary<string, int> RejectionCounts,
        int AmbiguousGroups, int AmbiguousRows);
}
