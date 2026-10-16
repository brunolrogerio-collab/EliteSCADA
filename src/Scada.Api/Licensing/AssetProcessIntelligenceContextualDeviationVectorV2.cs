using System.Collections.ObjectModel;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligenceContextualDeviationVectorV2QueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid AssignmentId,
    IReadOnlyList<Guid>? TargetSignalIds,
    Guid FirstContextSignalId,
    double FirstContextLowerInclusiveCanonical,
    double FirstContextUpperExclusiveCanonical,
    Guid SecondContextSignalId,
    double SecondContextLowerInclusiveCanonical,
    double SecondContextUpperExclusiveCanonical,
    AssetProcessIntelligenceAnalyticsWindow? ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow? EvaluationWindow);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV2Signal(
    Guid SignalId,
    string SignalKey,
    Guid TagId,
    string TagPath,
    string? EngineeringUnit,
    string? ControlledUnitCode,
    string? CanonicalUnit);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV2SignalWindowEvidence(
    Guid SignalId,
    int RawObservationCount,
    int GoodFiniteNumericCount,
    int AcceptedSynchronizedCount,
    int RejectedObservationCount,
    int AmbiguousSourceTimestampGroupCount,
    int AmbiguousSourceTimestampRowCount,
    IReadOnlyDictionary<string, int> QualityCounts,
    IReadOnlyDictionary<string, int> RejectionCounts);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV2Context(
    AssetProcessIntelligenceContextualDeviationVectorV2Signal Signal,
    double LowerInclusiveCanonical,
    double UpperExclusiveCanonical,
    string CanonicalUnit,
    int CompleteReferenceVectorCount,
    int ReferenceInRangeVectorCount,
    int CompleteEvaluationVectorCount,
    int EvaluationInRangeVectorCount);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV2TargetStatistics(
    AssetProcessIntelligenceContextualDeviationVectorV2Signal Signal,
    double? ReferenceMedian,
    double? ReferenceMad,
    int CompleteReferenceVectorCount,
    int JointContextReferenceVectorCount,
    int CompleteEvaluationVectorCount,
    int ReturnedJointContextEvaluationValueCount,
    string Status,
    string? ScoreUnavailableReason);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV2Value(
    Guid TargetSignalId,
    double Value,
    double? RawDifferenceFromReferenceMedian,
    double? RobustScore,
    string? ScoreUnavailableReason);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV2Sample(
    DateTimeOffset SourceTimestampUtc,
    DateTimeOffset FirstContextObservedTimestampUtc,
    double FirstContextValueCanonical,
    bool FirstContextInRange,
    DateTimeOffset SecondContextObservedTimestampUtc,
    double SecondContextValueCanonical,
    bool SecondContextInRange,
    bool InJointContext,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2Value> Targets);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV2Response(
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
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2Signal> TargetSignals,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2Context> Contexts,
    AssetProcessIntelligenceAnalyticsWindow ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow EvaluationWindow,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2SignalWindowEvidence> ReferenceSignalEvidence,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2SignalWindowEvidence> EvaluationSignalEvidence,
    int CompleteReferenceVectorCount,
    int JointContextReferenceVectorCount,
    int CompleteEvaluationVectorCount,
    int JointContextEvaluationVectorCount,
    int ReturnedEvaluationVectorCount,
    int OmittedEvaluationVectorCount,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2TargetStatistics> TargetStatistics,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2Sample> EvaluationVectors,
    int TotalRawObservationCount,
    AssetProcessIntelligenceContextualDeviationVectorV2Provenance Provenance);

public sealed record AssetProcessIntelligenceContextualDeviationVectorV2Provenance(
    string Dataset,
    int DatasetContractVersion,
    string QueryWindowTimestampBasis,
    string VectorTimestampBasis,
    string SourceTimestampEvidenceRequirement,
    string ContextQualification,
    string RegimeEquivalence,
    string Completeness,
    string Interpretation);

public static class AssetProcessIntelligenceContextualDeviationVectorV2Catalog
{
    public const int ContractVersion = 2;
    public const int CatalogVersion = 2;
    public const string RuleId = "contextual-deviation-vector.v2";
    public const string RuleVersion = "2.0.0";
    public const int MinimumTargetSignals = 2;
    public const int MaximumTargetSignals = 8;
    public const int ContextSignalCount = 2;
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
        contextSignalCount = ContextSignalCount,
        maximumWindowDays = (int)MaximumWindow.TotalDays,
        maximumRawRowsAcrossAllSignalsAndWindows = MaximumRawRowsAcrossQuery,
        pageSize = QueryPageSize,
        maximumReturnedEvaluationVectors = MaximumReturnedEvaluationVectors,
        minimumCompleteJointReferenceVectorsForRobustScore = MinimumReferenceVectorsForRobustScore,
        contextInterval = "each lowerInclusive <= canonical context value < upperExclusive; both must match for joint context",
        vectorTimestamp = "exact common non-null UTC source.timestamp across every selected target and both context TAGs",
        ordering = "targets and contexts follow caller selection order; evaluation vectors use source timestamp ascending",
        evidence = "accepted observations require Good finite numeric values and an explicitly named Synchronized source timestamp state; rejected raw rows remain visible in evidence counts",
        alignment = "exact source timestamp equality; no nearest join, hold, interpolation, imputation, or proof of physical simultaneity",
        regimeEquivalence = "not_assessed",
        completeness = "unknown",
        interpretation = "per-target descriptive median, MAD, raw difference and robust score against one joint-context reference cohort; no combined anomaly, diagnosis, fault, or RUL",
        supportedStatuses = new[] { "available", "insufficient_data" }
    };
}

internal sealed record AssetProcessIntelligenceContextualDeviationVectorV2Calculation(
    int CompleteReferenceVectorCount,
    int JointContextReferenceVectorCount,
    int CompleteEvaluationVectorCount,
    int JointContextEvaluationVectorCount,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2SignalWindowEvidence> ReferenceEvidence,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2SignalWindowEvidence> EvaluationEvidence,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2Context> Contexts,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2TargetStatistics> TargetStatistics,
    IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2Sample> EvaluationVectors,
    int OmittedEvaluationVectors);

internal static class AssetProcessIntelligenceContextualDeviationVectorV2Calculator
{
    private const string MissingTimestamp = "source_timestamp_missing";
    private const string Unsynchronized = "source_timestamp_not_synchronized";
    private const string OutsideWindow = "source_timestamp_outside_requested_window";
    private const string DuplicateTimestamp = "duplicate_source_timestamp";

    public static AssetProcessIntelligenceContextualDeviationVectorV2Calculation Calculate(
        IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2Signal> targets,
        IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2Signal> contexts,
        AssetProcessIntelligenceAnalyticsWindow referenceWindow,
        AssetProcessIntelligenceAnalyticsWindow evaluationWindow,
        IReadOnlyDictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>> referenceRows,
        IReadOnlyDictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>> evaluationRows,
        IReadOnlyList<ControlledEngineeringUnitDefinition> contextUnits,
        IReadOnlyList<(double LowerInclusive, double UpperExclusive)> contextRanges)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(referenceWindow);
        ArgumentNullException.ThrowIfNull(evaluationWindow);
        ArgumentNullException.ThrowIfNull(referenceRows);
        ArgumentNullException.ThrowIfNull(evaluationRows);
        if (targets.Count is < AssetProcessIntelligenceContextualDeviationVectorV2Catalog.MinimumTargetSignals or
                > AssetProcessIntelligenceContextualDeviationVectorV2Catalog.MaximumTargetSignals ||
            contexts.Count != AssetProcessIntelligenceContextualDeviationVectorV2Catalog.ContextSignalCount ||
            contextUnits.Count != contexts.Count || contextRanges.Count != contexts.Count ||
            contextUnits.Any(unit => unit is null) ||
            contextRanges.Any(range => !double.IsFinite(range.LowerInclusive) ||
                !double.IsFinite(range.UpperExclusive) || range.LowerInclusive >= range.UpperExclusive))
            throw new ArgumentException("The bounded target, two-context, unit and interval selections are required.");

        var orderedTargets = targets.ToArray();
        var orderedContexts = contexts.ToArray();
        var allSignals = orderedTargets.Concat(orderedContexts).ToArray();
        if (allSignals.Any(signal => signal.SignalId == Guid.Empty || signal.TagId == Guid.Empty) ||
            allSignals.Select(signal => signal.SignalId).Distinct().Count() != allSignals.Length ||
            allSignals.Select(signal => signal.TagId).Distinct().Count() != allSignals.Length ||
            allSignals.Any(signal => !referenceRows.ContainsKey(signal.SignalId) || !evaluationRows.ContainsKey(signal.SignalId)))
            throw new ArgumentException("Every selected signal and TAG must be unique and have both window evidence.");
        ControlledEngineeringUnitDefinition? UnitFor(Guid signalId)
        {
            var index = ContextIndex(signalId, orderedContexts);
            return index >= 0 ? contextUnits[index] : null;
        }

        var reference = allSignals.ToDictionary(signal => signal.SignalId, signal => Evaluate(
            referenceRows[signal.SignalId], referenceWindow, UnitFor(signal.SignalId)));
        var evaluation = allSignals.ToDictionary(signal => signal.SignalId, signal => Evaluate(
            evaluationRows[signal.SignalId], evaluationWindow, UnitFor(signal.SignalId)));

        var referenceEvidence = allSignals.Select(signal => ToEvidence(signal.SignalId, reference[signal.SignalId])).ToArray();
        var evaluationEvidence = allSignals.Select(signal => ToEvidence(signal.SignalId, evaluation[signal.SignalId])).ToArray();
        var completeReference = BuildVectors(orderedTargets, orderedContexts, reference);
        var completeEvaluation = BuildVectors(orderedTargets, orderedContexts, evaluation);
        var jointReference = completeReference.Where(vector => InRange(vector.Contexts[0].Value, contextRanges[0]) &&
            InRange(vector.Contexts[1].Value, contextRanges[1])).ToArray();
        var inContextEvaluation = completeEvaluation.Where(vector => InRange(vector.Contexts[0].Value, contextRanges[0]) &&
            InRange(vector.Contexts[1].Value, contextRanges[1])).ToArray();
        var returnedEvaluation = completeEvaluation.Take(
            AssetProcessIntelligenceContextualDeviationVectorV2Catalog.MaximumReturnedEvaluationVectors).ToArray();

        var contextEvidence = orderedContexts.Select((signal, index) => new AssetProcessIntelligenceContextualDeviationVectorV2Context(
            signal,
            contextRanges[index].LowerInclusive,
            contextRanges[index].UpperExclusive,
            contextUnits[index].CanonicalUnit,
            completeReference.Length,
            completeReference.Count(vector => InRange(vector.Contexts[index].Value, contextRanges[index])),
            completeEvaluation.Length,
            completeEvaluation.Count(vector => InRange(vector.Contexts[index].Value, contextRanges[index])))).ToArray();

        var perTarget = new List<AssetProcessIntelligenceContextualDeviationVectorV2TargetStatistics>(orderedTargets.Length);
        var medians = new Dictionary<Guid, double?>();
        var mads = new Dictionary<Guid, double?>();
        foreach (var target in orderedTargets)
        {
            var values = jointReference.Select(vector => vector.Targets[target.SignalId].Value).ToArray();
            var median = Median(values);
            var mad = median.HasValue ? Median(values.Select(value => Math.Abs(value - median.Value)).ToArray()) : null;
            var reason = values.Length == 0 ? "no_complete_joint_context_reference_vectors"
                : values.Length < AssetProcessIntelligenceContextualDeviationVectorV2Catalog.MinimumReferenceVectorsForRobustScore
                    ? "fewer_than_30_complete_joint_context_reference_vectors"
                    : !median.HasValue || !mad.HasValue ? "non_finite_reference_statistic"
                    : mad.Value == 0d ? "reference_mad_zero" : null;
            var status = !median.HasValue || !mad.HasValue || reason is not null ? "insufficient_data" : "available";
            medians[target.SignalId] = median;
            mads[target.SignalId] = mad;
            perTarget.Add(new(target, median, mad, completeReference.Length, jointReference.Length,
                completeEvaluation.Length, returnedEvaluation.Count(vector => InJointContext(vector, contextRanges)),
                status, reason));
        }

        var returned = returnedEvaluation.Select(vector =>
        {
            var firstInRange = InRange(vector.Contexts[0].Value, contextRanges[0]);
            var secondInRange = InRange(vector.Contexts[1].Value, contextRanges[1]);
            var inJointContext = firstInRange && secondInRange;
            var values = orderedTargets.Select(target =>
            {
                double? difference = null;
                double? score = null;
                string? reason = null;
                if (!inJointContext) reason = "outside_joint_declared_context";
                else if (!medians[target.SignalId].HasValue) reason = "no_complete_joint_context_reference_vectors";
                else
                {
                    var candidate = vector.Targets[target.SignalId].Value - medians[target.SignalId]!.Value;
                    if (!double.IsFinite(candidate)) reason = "non_finite_raw_difference";
                    else
                    {
                        difference = candidate;
                        var mad = mads[target.SignalId];
                        if (jointReference.Length < AssetProcessIntelligenceContextualDeviationVectorV2Catalog.MinimumReferenceVectorsForRobustScore)
                            reason = "fewer_than_30_complete_joint_context_reference_vectors";
                        else if (!mad.HasValue) reason = "non_finite_reference_statistic";
                        else if (mad.Value == 0d) reason = "reference_mad_zero";
                        else
                        {
                            var candidateScore = AssetProcessIntelligenceContextualDeviationVectorV2Catalog.RobustScoreConstant * candidate / mad.Value;
                            if (double.IsFinite(candidateScore)) score = candidateScore;
                            else reason = "non_finite_robust_score";
                        }
                    }
                }
                return new AssetProcessIntelligenceContextualDeviationVectorV2Value(
                    target.SignalId, vector.Targets[target.SignalId].Value, difference, score, reason);
            }).ToArray();

            return new AssetProcessIntelligenceContextualDeviationVectorV2Sample(
                vector.Timestamp,
                vector.Contexts[0].ObservedTimestampUtc,
                vector.Contexts[0].Value,
                firstInRange,
                vector.Contexts[1].ObservedTimestampUtc,
                vector.Contexts[1].Value,
                secondInRange,
                inJointContext,
                Array.AsReadOnly(values));
        }).ToArray();

        return new(completeReference.Length, jointReference.Length, completeEvaluation.Length, inContextEvaluation.Length,
            Array.AsReadOnly(referenceEvidence), Array.AsReadOnly(evaluationEvidence), Array.AsReadOnly(contextEvidence),
            Array.AsReadOnly(perTarget.ToArray()), Array.AsReadOnly(returned),
            Math.Max(0, completeEvaluation.Length - returned.Length));
    }

    private static int ContextIndex(
        Guid signalId,
        IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2Signal> contexts)
    {
        for (var index = 0; index < contexts.Count; index++)
            if (contexts[index].SignalId == signalId) return index;
        return -1;
    }

    private static Vector[] BuildVectors(
        IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2Signal> targets,
        IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorV2Signal> contexts,
        IReadOnlyDictionary<Guid, SignalEvaluation> evaluations)
    {
        var signals = targets.Concat(contexts).ToArray();
        HashSet<DateTimeOffset>? common = null;
        foreach (var signal in signals)
        {
            var timestamps = evaluations[signal.SignalId].Accepted.Keys.ToHashSet();
            if (common is null) common = timestamps;
            else common.IntersectWith(timestamps);
        }

        return (common ?? []).OrderBy(timestamp => timestamp).Select(timestamp => new Vector(
            timestamp,
            contexts.Select(context => evaluations[context.SignalId].Accepted[timestamp]).ToArray(),
            targets.ToDictionary(target => target.SignalId, target => evaluations[target.SignalId].Accepted[timestamp]))).ToArray();
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
            if (!Enum.TryParse<TimestampEvidenceState>(row.SourceTimestampEvidence, true, out var evidence) ||
                !Enum.IsDefined(evidence) ||
                !string.Equals(row.SourceTimestampEvidence, evidence.ToString(), StringComparison.OrdinalIgnoreCase))
            { Increment(rejected, "source_timestamp_evidence_unrecognized"); continue; }
            if (evidence != TimestampEvidenceState.Synchronized) { Increment(rejected, Unsynchronized); continue; }
            var timestamp = row.SourceTimestampUtc.Value;
            if (timestamp.Offset != TimeSpan.Zero || timestamp < window.FromUtc || timestamp > window.ToUtc)
            { Increment(rejected, OutsideWindow); continue; }
            if (canonicalUnit is not null)
            {
                value *= canonicalUnit.ScaleToCanonical;
                if (!double.IsFinite(value))
                { Increment(rejected, "context_unit_conversion_unavailable"); continue; }
            }
            candidates.Add(new(row.ObservedTimestampUtc, timestamp, value));
        }

        var duplicateGroups = rows.Where(row => row.SourceTimestampUtc is { } timestamp && timestamp.Offset == TimeSpan.Zero &&
                timestamp >= window.FromUtc && timestamp <= window.ToUtc)
            .GroupBy(row => row.SourceTimestampUtc!.Value)
            .Where(group => group.Count() > 1).ToArray();
        foreach (var duplicate in duplicateGroups) Increment(rejected, DuplicateTimestamp, duplicate.Count());
        var duplicateTimes = duplicateGroups.Select(group => group.Key).ToHashSet();
        var accepted = candidates.Where(sample => !duplicateTimes.Contains(sample.SourceTimestampUtc))
            .ToDictionary(sample => sample.SourceTimestampUtc);
        return new(rows.Count, goodNumericCount, accepted, qualities, rejected, duplicateGroups.Length,
            duplicateGroups.Sum(group => group.Count()));
    }

    private static AssetProcessIntelligenceContextualDeviationVectorV2SignalWindowEvidence ToEvidence(
        Guid signalId,
        SignalEvaluation signal) => new(signalId, signal.RawCount, signal.GoodNumericCount,
        signal.Accepted.Count, Math.Max(0, signal.RawCount - signal.Accepted.Count),
        signal.AmbiguousGroups, signal.AmbiguousRows,
        new ReadOnlyDictionary<string, int>(signal.QualityCounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)),
        new ReadOnlyDictionary<string, int>(signal.RejectionCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)));

    private static bool InRange(double value, (double LowerInclusive, double UpperExclusive) range) =>
        value >= range.LowerInclusive && value < range.UpperExclusive;

    private static bool InJointContext(Vector vector, IReadOnlyList<(double LowerInclusive, double UpperExclusive)> ranges) =>
        InRange(vector.Contexts[0].Value, ranges[0]) && InRange(vector.Contexts[1].Value, ranges[1]);

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
    private sealed record Vector(DateTimeOffset Timestamp, IReadOnlyList<Accepted> Contexts,
        IReadOnlyDictionary<Guid, Accepted> Targets);
    private sealed record SignalEvaluation(int RawCount, int GoodNumericCount, IReadOnlyDictionary<DateTimeOffset, Accepted> Accepted,
        IReadOnlyDictionary<string, int> QualityCounts, IReadOnlyDictionary<string, int> RejectionCounts,
        int AmbiguousGroups, int AmbiguousRows);
}
