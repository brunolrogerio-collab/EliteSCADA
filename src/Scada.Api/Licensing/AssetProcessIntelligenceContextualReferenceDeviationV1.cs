using System.Collections.ObjectModel;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;

namespace Scada.Api.Licensing;

internal sealed record AssetProcessIntelligenceContextualReferenceRawObservation(
    DateTimeOffset ObservedTimestampUtc,
    string? Quality,
    HistoricalQueryValue? Value,
    DateTimeOffset? SourceTimestampUtc,
    string? SourceTimestampEvidence);

public sealed record AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid AssignmentId,
    Guid TargetSignalId,
    Guid ConditionSignalId,
    AssetProcessIntelligenceAnalyticsWindow? ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow? EvaluationWindow,
    double ConditionLowerInclusiveCanonical,
    double ConditionUpperExclusiveCanonical);

public sealed record AssetProcessIntelligenceContextualReferenceDeviationV1Signal(
    Guid SignalId,
    string SignalKey,
    Guid TagId,
    string TagPath,
    TagDataType DataType,
    string? EngineeringUnit,
    string? ControlledUnitCode,
    string? CanonicalUnit);

public sealed record AssetProcessIntelligenceContextualReferenceDeviationV1SignalEvidence(
    int RawObservationCount,
    int GoodFiniteNumericCount,
    int AcceptedSynchronizedSampleCount,
    int RejectedObservationCount,
    int AmbiguousSourceTimestampGroupCount,
    int AmbiguousSourceTimestampRowCount,
    IReadOnlyDictionary<string, int> QualityCounts,
    IReadOnlyDictionary<string, int> RejectionCounts);

public sealed record AssetProcessIntelligenceContextualReferenceDeviationV1WindowEvidence(
    AssetProcessIntelligenceAnalyticsWindow Window,
    AssetProcessIntelligenceContextualReferenceDeviationV1SignalEvidence Target,
    AssetProcessIntelligenceContextualReferenceDeviationV1SignalEvidence Condition,
    int ExactSourceTimestampPairCount,
    int ConditionInRangePairCount,
    int ConditionOutOfRangePairCount,
    int QualifiedPairCount);

public sealed record AssetProcessIntelligenceContextualReferenceDeviationV1EvaluationSample(
    DateTimeOffset SourceTimestampUtc,
    DateTimeOffset TargetObservedTimestampUtc,
    DateTimeOffset ConditionObservedTimestampUtc,
    double TargetValue,
    double ConditionValueCanonical,
    bool ConditionInRange,
    double? RawDifferenceFromReferenceMedian,
    double? RobustScore,
    string? ScoreUnavailableReason);

public sealed record AssetProcessIntelligenceContextualReferenceDeviationV1Response(
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
    AssetProcessIntelligenceContextualReferenceDeviationV1Signal TargetSignal,
    AssetProcessIntelligenceContextualReferenceDeviationV1Signal ConditionSignal,
    AssetProcessIntelligenceAnalyticsWindow ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow EvaluationWindow,
    double ConditionLowerInclusiveCanonical,
    double ConditionUpperExclusiveCanonical,
    string ConditionCanonicalUnit,
    AssetProcessIntelligenceContextualReferenceDeviationV1WindowEvidence ReferenceEvidence,
    AssetProcessIntelligenceContextualReferenceDeviationV1WindowEvidence EvaluationEvidence,
    double? ReferenceMedian,
    double? ReferenceMedianAbsoluteDeviation,
    string Status,
    IReadOnlyList<string> ReasonCodes,
    int TotalRawObservationCount,
    int ReturnedEvaluationSampleCount,
    int OmittedEvaluationSampleCount,
    IReadOnlyList<AssetProcessIntelligenceContextualReferenceDeviationV1EvaluationSample> EvaluationSamples,
    AssetProcessIntelligenceContextualReferenceDeviationV1Provenance Provenance);

public sealed record AssetProcessIntelligenceContextualReferenceDeviationV1Provenance(
    string Dataset,
    int DatasetContractVersion,
    string QueryWindowTimestampBasis,
    string PairTimestampBasis,
    string SourceTimestampEvidenceRequirement,
    string RegimeEquivalence,
    string Completeness,
    string DifferenceUnit,
    string Formula);

public static class AssetProcessIntelligenceContextualReferenceDeviationV1Catalog
{
    public const int ContractVersion = 1;
    public const int CatalogVersion = 1;
    public const string RuleId = "contextual-reference-deviation.v1";
    public const string RuleVersion = "1.0.0";
    public const int MaximumRawRowsAcrossQuery = 50000;
    public const int QueryPageSize = 200;
    public const int MaximumReturnedEvaluationSamples = 500;
    public const int MinimumReferencePairsForRobustScore = 30;
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
        maximumWindowDays = (int)MaximumWindow.TotalDays,
        maximumRawRowsAcrossBothTagsAndBothWindows = MaximumRawRowsAcrossQuery,
        pageSize = QueryPageSize,
        maximumReturnedEvaluationSamples = MaximumReturnedEvaluationSamples,
        minimumReferencePairsForRobustScore = MinimumReferencePairsForRobustScore,
        conditionInterval = "lowerInclusive <= canonical condition value < upperExclusive",
        timestampPairing = "exact equality of non-null source.timestamp values with explicit Synchronized evidence for both TAGs; both source timestamps must lie in each selected window",
        regimeEquivalence = "not_assessed",
        completeness = "unknown",
        formula = "referenceMedian = median of exact synchronized target/condition pairs inside the declared canonical condition range; rawDifference = evaluation target value - referenceMedian; robustScore = 0.67448975 * rawDifference / reference MAD only with at least 30 qualified reference pairs and MAD > 0",
        supportedStatuses = new[] { "available", "insufficient_data", "unsupported" },
        reasonCodes = AssetProcessIntelligenceContextualReferenceDeviationV1Calculator.ReasonCodes
    };
}

internal sealed record AssetProcessIntelligenceContextualReferenceDeviationV1Calculation(
    AssetProcessIntelligenceContextualReferenceDeviationV1WindowEvidence ReferenceEvidence,
    AssetProcessIntelligenceContextualReferenceDeviationV1WindowEvidence EvaluationEvidence,
    double? ReferenceMedian,
    double? ReferenceMad,
    string Status,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<AssetProcessIntelligenceContextualReferenceDeviationV1EvaluationSample> EvaluationSamples,
    int OmittedEvaluationSamples);

internal static class AssetProcessIntelligenceContextualReferenceDeviationV1Calculator
{
    private const string TooFewReferencePairs = "fewer_than_30_qualified_reference_pairs";
    private const string ZeroMad = "reference_mad_zero";
    private const string NoReferencePairs = "no_qualified_reference_pairs";
    private const string NoEvaluationPairs = "no_qualified_evaluation_pairs";
    private const string NonFiniteReferenceStatistic = "non_finite_reference_statistic";
    private const string NonFiniteDifference = "non_finite_raw_difference";
    private const string NonFiniteScore = "non_finite_robust_score";

    public static IReadOnlyList<string> ReasonCodes { get; } = Array.AsReadOnly(new[]
    {
        NoReferencePairs, TooFewReferencePairs, NoEvaluationPairs, ZeroMad,
        NonFiniteReferenceStatistic, NonFiniteDifference, NonFiniteScore,
        "quality_missing", "quality_unrecognized", "quality_not_good", "value_not_finite_numeric",
        "source_timestamp_missing", "source_timestamp_evidence_unrecognized",
        "source_timestamp_not_synchronized", "source_timestamp_outside_requested_window",
        "condition_unit_conversion_unavailable", "duplicate_target_source_timestamp",
        "duplicate_condition_source_timestamp", "condition_outside_requested_range"
    });

    public static AssetProcessIntelligenceContextualReferenceDeviationV1Calculation Calculate(
        AssetProcessIntelligenceAnalyticsWindow referenceWindow,
        AssetProcessIntelligenceAnalyticsWindow evaluationWindow,
        IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation> referenceTargetRows,
        IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation> referenceConditionRows,
        IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation> evaluationTargetRows,
        IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation> evaluationConditionRows,
        ControlledEngineeringUnitReference conditionUnit,
        double conditionLowerInclusiveCanonical,
        double conditionUpperExclusiveCanonical)
    {
        ArgumentNullException.ThrowIfNull(referenceWindow);
        ArgumentNullException.ThrowIfNull(evaluationWindow);
        ArgumentNullException.ThrowIfNull(referenceTargetRows);
        ArgumentNullException.ThrowIfNull(referenceConditionRows);
        ArgumentNullException.ThrowIfNull(evaluationTargetRows);
        ArgumentNullException.ThrowIfNull(evaluationConditionRows);
        ArgumentNullException.ThrowIfNull(conditionUnit);

        var reference = PairWindow(referenceWindow, referenceTargetRows, referenceConditionRows,
            conditionUnit, conditionLowerInclusiveCanonical, conditionUpperExclusiveCanonical);
        var evaluation = PairWindow(evaluationWindow, evaluationTargetRows, evaluationConditionRows,
            conditionUnit, conditionLowerInclusiveCanonical, conditionUpperExclusiveCanonical);
        var reasons = new SortedSet<string>(StringComparer.Ordinal);
        var selectedReference = reference.Pairs.Where(pair => pair.InRange).Select(pair => pair.Target.Value).ToArray();
        var selectedEvaluation = evaluation.Pairs.Where(pair => pair.InRange).ToArray();
        if (selectedReference.Length == 0)
            reasons.Add(NoReferencePairs);
        if (selectedEvaluation.Length == 0)
            reasons.Add(NoEvaluationPairs);

        double? median = null;
        double? mad = null;
        if (selectedReference.Length > 0)
        {
            median = Median(selectedReference);
            if (!median.HasValue)
                reasons.Add(NonFiniteReferenceStatistic);
            else
            {
                var deviations = selectedReference.Select(value => Math.Abs(value - median.Value)).ToArray();
                mad = Median(deviations);
                if (!mad.HasValue)
                    reasons.Add(NonFiniteReferenceStatistic);
                if (selectedReference.Length < AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.MinimumReferencePairsForRobustScore)
                    reasons.Add(TooFewReferencePairs);
                else if (mad == 0d)
                    reasons.Add(ZeroMad);
            }
        }

        var samples = new List<AssetProcessIntelligenceContextualReferenceDeviationV1EvaluationSample>(
            Math.Min(selectedEvaluation.Length, AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.MaximumReturnedEvaluationSamples));
        foreach (var pair in evaluation.Pairs)
        {
            double? difference = null;
            double? score = null;
            string? scoreReason = null;
            if (pair.InRange)
            {
                if (!median.HasValue || !mad.HasValue)
                    scoreReason = reasons.Contains(NonFiniteReferenceStatistic) ? NonFiniteReferenceStatistic : NoReferencePairs;
                else
                {
                    var calculatedDifference = pair.Target.Value - median.Value;
                    if (!double.IsFinite(calculatedDifference))
                    {
                        scoreReason = NonFiniteDifference;
                        reasons.Add(NonFiniteDifference);
                    }
                    else
                    {
                        difference = calculatedDifference;
                        if (selectedReference.Length < AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.MinimumReferencePairsForRobustScore)
                            scoreReason = TooFewReferencePairs;
                        else if (mad.Value == 0d)
                            scoreReason = ZeroMad;
                        else
                        {
                            var calculatedScore = AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.RobustScoreConstant *
                                calculatedDifference / mad.Value;
                            if (double.IsFinite(calculatedScore)) score = calculatedScore;
                            else
                            {
                                scoreReason = NonFiniteScore;
                                reasons.Add(NonFiniteScore);
                            }
                        }
                    }
                }
            }
            else
                scoreReason = "condition_outside_requested_range";

            samples.Add(new(
                pair.Target.SourceTimestampUtc,
                pair.Target.ObservedTimestampUtc,
                pair.Condition.ObservedTimestampUtc,
                pair.Target.Value,
                pair.Condition.Value,
                pair.InRange,
                difference,
                score,
                scoreReason));
        }

        var status = reasons.Contains(NonFiniteReferenceStatistic) || reasons.Contains(NonFiniteDifference) || reasons.Contains(NonFiniteScore)
            ? "unsupported"
            : selectedReference.Length == 0 || selectedEvaluation.Length == 0 || !median.HasValue || !mad.HasValue
                ? "insufficient_data"
                : "available";
        var bounded = samples.Take(AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.MaximumReturnedEvaluationSamples).ToArray();
        return new(
            reference.Evidence,
            evaluation.Evidence,
            median,
            mad,
            status,
            Array.AsReadOnly(reasons.ToArray()),
            Array.AsReadOnly(bounded),
            Math.Max(0, samples.Count - bounded.Length));
    }

    private static WindowResult PairWindow(
        AssetProcessIntelligenceAnalyticsWindow window,
        IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation> targetRows,
        IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation> conditionRows,
        ControlledEngineeringUnitReference conditionUnit,
        double lower,
        double upper)
    {
        var target = Evaluate(targetRows, window, null);
        var condition = Evaluate(conditionRows, window, conditionUnit);
        var duplicateTargets = DuplicateSourceTimestamps(targetRows, window);
        var duplicateConditions = DuplicateSourceTimestamps(conditionRows, window);
        var targetByTime = target.Accepted.Where(sample => !duplicateTargets.Contains(sample.SourceTimestampUtc))
            .ToDictionary(sample => sample.SourceTimestampUtc);
        var conditionByTime = condition.Accepted.Where(sample => !duplicateConditions.Contains(sample.SourceTimestampUtc))
            .ToDictionary(sample => sample.SourceTimestampUtc);
        var pairs = targetByTime.Keys.Intersect(conditionByTime.Keys).Order()
            .Select(timestamp => new PairedSample(
                targetByTime[timestamp], conditionByTime[timestamp],
                conditionByTime[timestamp].Value >= lower && conditionByTime[timestamp].Value < upper))
            .ToArray();

        Increment(target.Rejections, "duplicate_target_source_timestamp",
            duplicateTargets.Sum(timestamp => DuplicateCount(targetRows, window, timestamp)));
        Increment(condition.Rejections, "duplicate_condition_source_timestamp",
            duplicateConditions.Sum(timestamp => DuplicateCount(conditionRows, window, timestamp)));
        Increment(condition.Rejections, "condition_outside_requested_range", pairs.Count(pair => !pair.InRange));
        var targetEvidence = Evidence(targetRows, window, target, duplicateTargets);
        var conditionEvidence = Evidence(conditionRows, window, condition, duplicateConditions);
        return new(new(
            window,
            targetEvidence,
            conditionEvidence,
            pairs.Length,
            pairs.Count(pair => pair.InRange),
            pairs.Count(pair => !pair.InRange),
            pairs.Count(pair => pair.InRange)), pairs);
    }

    private static SignalEvaluation Evaluate(
        IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation> rows,
        AssetProcessIntelligenceAnalyticsWindow window,
        ControlledEngineeringUnitReference? canonicalUnit)
    {
        var qualityCounts = Enum.GetValues<TagQuality>().ToDictionary(value => value.ToString(), _ => 0, StringComparer.Ordinal);
        qualityCounts.Add("Missing", 0);
        qualityCounts.Add("Unrecognized", 0);
        var rejections = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var accepted = new List<AcceptedSample>();
        var goodNumeric = 0;
        foreach (var row in rows)
        {
            var quality = AssetProcessIntelligenceKpiV1.NormalizeQuality(row.Quality);
            qualityCounts[quality]++;
            var isGoodNumeric = AssetProcessIntelligenceKpiV1.TryGetAcceptedGoodNumericValue(
                new AssetProcessIntelligenceRawObservation(row.ObservedTimestampUtc, row.Quality, row.Value), out var value);
            if (isGoodNumeric) goodNumeric++;
            if (quality == "Missing") { Increment(rejections, "quality_missing"); continue; }
            if (quality == "Unrecognized") { Increment(rejections, "quality_unrecognized"); continue; }
            if (quality != TagQuality.Good.ToString()) { Increment(rejections, "quality_not_good"); continue; }
            if (!isGoodNumeric) { Increment(rejections, "value_not_finite_numeric"); continue; }
            if (!row.SourceTimestampUtc.HasValue) { Increment(rejections, "source_timestamp_missing"); continue; }
            if (!Enum.TryParse<TimestampEvidenceState>(row.SourceTimestampEvidence, true, out var evidence) || !Enum.IsDefined(evidence))
            { Increment(rejections, "source_timestamp_evidence_unrecognized"); continue; }
            if (evidence != TimestampEvidenceState.Synchronized)
            { Increment(rejections, "source_timestamp_not_synchronized"); continue; }
            var sourceTimestamp = row.SourceTimestampUtc.Value;
            if (sourceTimestamp.Offset != TimeSpan.Zero || sourceTimestamp < window.FromUtc || sourceTimestamp > window.ToUtc)
            { Increment(rejections, "source_timestamp_outside_requested_window"); continue; }
            if (canonicalUnit is not null &&
                !ControlledEngineeringSemanticsResolver.TryConvertToCanonical(value, canonicalUnit, out value))
            { Increment(rejections, "condition_unit_conversion_unavailable"); continue; }
            accepted.Add(new(row.ObservedTimestampUtc, sourceTimestamp, value));
        }
        return new(rows.Count, goodNumeric, accepted, qualityCounts, rejections);
    }

    private static AssetProcessIntelligenceContextualReferenceDeviationV1SignalEvidence Evidence(
        IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation> rows,
        AssetProcessIntelligenceAnalyticsWindow window,
        SignalEvaluation signal,
        IReadOnlySet<DateTimeOffset> duplicates)
    {
        var duplicateRows = duplicates.Sum(timestamp => DuplicateCount(rows, window, timestamp));
        var acceptedCount = signal.Accepted.Count(sample => !duplicates.Contains(sample.SourceTimestampUtc));
        return new(
            signal.RawCount,
            signal.GoodNumericCount,
            acceptedCount,
            signal.RawCount - acceptedCount,
            duplicates.Count,
            duplicateRows,
            new ReadOnlyDictionary<string, int>(signal.QualityCounts),
            new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(signal.Rejections, StringComparer.Ordinal)));
    }

    private static HashSet<DateTimeOffset> DuplicateSourceTimestamps(
        IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation> rows,
        AssetProcessIntelligenceAnalyticsWindow window) => rows
        .Where(row => row.SourceTimestampUtc is { } timestamp && timestamp.Offset == TimeSpan.Zero &&
                      timestamp >= window.FromUtc && timestamp <= window.ToUtc)
        .GroupBy(row => row.SourceTimestampUtc!.Value)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToHashSet();

    private static int DuplicateCount(
        IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation> rows,
        AssetProcessIntelligenceAnalyticsWindow window,
        DateTimeOffset timestamp) => rows.Count(row => row.SourceTimestampUtc == timestamp &&
            row.SourceTimestampUtc.Value.Offset == TimeSpan.Zero &&
            row.SourceTimestampUtc.Value >= window.FromUtc && row.SourceTimestampUtc.Value <= window.ToUtc);

    private static double? Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0 || values.Any(value => !double.IsFinite(value))) return null;
        var ordered = values.Order().ToArray();
        var middle = ordered.Length / 2;
        var result = ordered.Length % 2 == 1 ? ordered[middle] : ordered[middle - 1] / 2d + ordered[middle] / 2d;
        return double.IsFinite(result) ? result : null;
    }

    private static void Increment(IDictionary<string, int> counts, string key, int amount = 1)
    {
        if (amount <= 0) return;
        counts[key] = counts.TryGetValue(key, out var current) ? current + amount : amount;
    }

    private sealed record AcceptedSample(DateTimeOffset ObservedTimestampUtc, DateTimeOffset SourceTimestampUtc, double Value);
    private sealed record PairedSample(AcceptedSample Target, AcceptedSample Condition, bool InRange);
    private sealed record SignalEvaluation(
        int RawCount,
        int GoodNumericCount,
        IReadOnlyList<AcceptedSample> Accepted,
        Dictionary<string, int> QualityCounts,
        SortedDictionary<string, int> Rejections);
    private sealed record WindowResult(
        AssetProcessIntelligenceContextualReferenceDeviationV1WindowEvidence Evidence,
        IReadOnlyList<PairedSample> Pairs);
}
