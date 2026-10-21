using Scada.Core.HistoricalQueries;
using Scada.Core.Intelligence.Experimental;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligenceExperimentalJointDeviationV1QueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid AssignmentId,
    Guid OperatingContextProfileId,
    int ExpectedProfileVersion,
    IReadOnlyList<Guid>? TargetSignalIds,
    int ExpectedCadenceSeconds,
    AssetProcessIntelligenceAnalyticsWindow? ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow? EvaluationWindow);

public sealed record AssetProcessIntelligenceExperimentalJointDeviationV1Signal(
    Guid SignalId,
    string SignalKey,
    Guid TagId,
    string TagPath,
    TagDataType DataType,
    string? EngineeringUnit);

public sealed record AssetProcessIntelligenceExperimentalJointDeviationV1SignalWindowEvidence(
    int RawObservationCount,
    int GoodQualityCount,
    int CompatibleValueCount,
    int UtcSourceTimestampCount,
    int InWindowSourceTimestampCount,
    int SynchronizedSourceTimestampCount,
    int AmbiguousSourceTimestampGroupCount,
    int AmbiguousSourceTimestampRowCount,
    int AcceptedSynchronizedUniqueCount,
    int PredicateMatchCount);

public sealed record AssetProcessIntelligenceExperimentalJointDeviationV1SignalEvidence(
    Guid SignalId,
    string Role,
    AssetProcessIntelligenceExperimentalJointDeviationV1SignalWindowEvidence Reference,
    AssetProcessIntelligenceExperimentalJointDeviationV1SignalWindowEvidence Evaluation);

public sealed record AssetProcessIntelligenceExperimentalJointDeviationV1TargetContribution(
    Guid SignalId,
    double Level,
    double MarginalVariance);

public sealed record AssetProcessIntelligenceExperimentalJointDeviationV1ScorePoint(
    DateTimeOffset SourceTimestampUtc,
    string Status,
    string? Reason,
    double? LevelComponent,
    double? RelationshipComponent,
    double? Score,
    double? CrossSignalInteraction,
    IReadOnlyList<AssetProcessIntelligenceExperimentalJointDeviationV1TargetContribution>? TargetContributions);

public sealed record AssetProcessIntelligenceExperimentalJointDeviationV1Response(
    int ContractVersion,
    int CatalogVersion,
    string RuleId,
    string RuleVersion,
    string ValidationStatus,
    string ClassificationStatus,
    double? DecisionThreshold,
    string ProjectKey,
    long ActiveRevision,
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid AssignmentId,
    Guid EquipmentId,
    Guid MeasurementProfileId,
    Guid OperatingContextProfileId,
    string OperatingContextProfileKey,
    int OperatingContextProfileVersion,
    AssetProcessIntelligenceAnalyticsWindow ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow EvaluationWindow,
    int ExpectedCadenceSeconds,
    int ReferenceCompleteCohortCount,
    int ReferenceFitSegmentCount,
    string ReferenceFitStatus,
    string? ReferenceFitUnavailableReason,
    int EvaluationCompleteCohortCount,
    int EvaluationScoredCohortCount,
    int EvaluationWarmupCohortCount,
    IReadOnlyList<AssetProcessIntelligenceExperimentalJointDeviationV1Signal> TargetSignals,
    IReadOnlyList<AssetProcessIntelligenceExperimentalJointDeviationV1Signal> ContextSignals,
    IReadOnlyList<AssetProcessIntelligenceExperimentalJointDeviationV1SignalEvidence> SignalEvidence,
    IReadOnlyList<AssetProcessIntelligenceExperimentalJointDeviationV1ScorePoint> Scores,
    int OmittedEvaluationScores,
    int TotalRawObservationCount,
    string Interpretation);

public static class AssetProcessIntelligenceExperimentalJointDeviationV1Catalog
{
    public const int ContractVersion = 1;
    public const int CatalogVersion = 1;
    public const string RuleId = "experimental-joint-deviation.v1";
    public const string RuleVersion = "0.1.0-experimental";
    public const int MinimumTargetSignals = ExperimentalJointDeviationReference.MinimumSignals;
    public const int MaximumTargetSignals = ExperimentalJointDeviationReference.MaximumSignals;
    public const int MaximumRawRowsAcrossQuery = 50_000;
    public const int QueryPageSize = 200;
    public const int MaximumReturnedScores = 500;
    public const int MinimumReferenceInnovations = ExperimentalJointDeviationReference.MinimumReferenceInnovations;
    public const int MinimumReferenceInnovationsPerTargetSignal = ExperimentalJointDeviationReference.MinimumReferenceInnovationsPerSignal;
    public const int WindowLength = ExperimentalJointDeviationDetector.WindowLength;
    public const int DiagonalShrinkagePercent = 5;
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
        minimumReferenceInnovations = MinimumReferenceInnovations,
        minimumReferenceInnovationsPerTargetSignal = MinimumReferenceInnovationsPerTargetSignal,
        maximumWindowDays = (int)MaximumWindow.TotalDays,
        maximumRawRowsAcrossAllSignalsAndWindows = MaximumRawRowsAcrossQuery,
        pageSize = QueryPageSize,
        maximumReturnedScores = MaximumReturnedScores,
        windowLength = WindowLength,
        diagonalCovarianceShrinkagePercent = DiagonalShrinkagePercent,
        evidenceCounterRule = "per-signal quality/type/time/synchronization/duplicate counters may overlap and must not be summed; accepted synchronized unique is the final per-signal eligibility count after all gates; context predicate matches count only the profile's typed predicate",
        duplicateTimestampRule = "ambiguous duplicate groups are counted only for UTC source timestamps inside the requested window; out-of-window rows do not contaminate window diagnostics",
        sourceTimestampRule = "exact unique UTC source timestamps; Good finite values; explicit Synchronized evidence; no imputation or nearest-time join",
        contextRule = "all predicates must match one active Engineering-owned operating-context profile/version",
        cadenceRule = "explicit caller-declared seconds; a gap or change breaks temporal state",
        validationStatus = "experimental_not_field_validated",
        classificationStatus = "none",
        decisionThreshold = (double?)null,
        interpretation = "experimental continuous joint deviation from the selected reference under the declared context; no alarm, normal/fault classification, probability, diagnosis, efficiency or RUL claim"
    };
}

internal sealed record AssetProcessIntelligenceExperimentalJointDeviationV1Calculation(
    int ReferenceCompleteCohortCount,
    int ReferenceFitSegmentCount,
    int EvaluationCompleteCohortCount,
    int EvaluationScoredCohortCount,
    int EvaluationWarmupCohortCount,
    IReadOnlyList<AssetProcessIntelligenceExperimentalJointDeviationV1ScorePoint> Scores,
    IReadOnlyList<AssetProcessIntelligenceExperimentalJointDeviationV1SignalEvidence> SignalEvidence,
    int OmittedEvaluationScores,
    string ReferenceStatus,
    string? ReferenceUnavailableReason);

internal static class AssetProcessIntelligenceExperimentalJointDeviationV1Calculator
{
    public static AssetProcessIntelligenceExperimentalJointDeviationV1Calculation Calculate(
        IReadOnlyList<(AssetProcessIntelligenceSignalEngineeringDto Signal, TagDefinition Tag)> targets,
        IReadOnlyList<(AssetProcessIntelligenceSignalEngineeringDto Signal, TagDefinition Tag)> contextSignals,
        IReadOnlyList<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto> predicates,
        Guid assignmentId,
        Guid contextProfileId,
        int contextProfileVersion,
        AssetProcessIntelligenceAnalyticsWindow referenceWindow,
        AssetProcessIntelligenceAnalyticsWindow evaluationWindow,
        int expectedCadenceSeconds,
        IReadOnlyDictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>> referenceRows,
        IReadOnlyDictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>> evaluationRows)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(contextSignals);
        ArgumentNullException.ThrowIfNull(predicates);
        ArgumentNullException.ThrowIfNull(referenceRows);
        ArgumentNullException.ThrowIfNull(evaluationRows);
        if (targets.Count is < AssetProcessIntelligenceExperimentalJointDeviationV1Catalog.MinimumTargetSignals or
                > AssetProcessIntelligenceExperimentalJointDeviationV1Catalog.MaximumTargetSignals ||
            predicates.Count is < 1 or > AssetProcessIntelligenceContextEligibilityV1Catalog.MaximumPredicates ||
            predicates.Any(predicate => predicate is null || predicate.TagId == Guid.Empty) ||
            predicates.Select(predicate => predicate.TagId).Distinct().Count() != predicates.Count ||
            expectedCadenceSeconds is < 1 or > 86_400)
            throw new ArgumentException("The experimental target, context-predicate and cadence limits are required.");

        var allSignals = targets.Concat(contextSignals).ToArray();
        if (allSignals.Select(item => item.Signal.Id).Distinct().Count() != allSignals.Length ||
            allSignals.Select(item => item.Tag.Id).Distinct().Count() != allSignals.Length ||
            allSignals.Any(item => !referenceRows.ContainsKey(item.Signal.Id) || !evaluationRows.ContainsKey(item.Signal.Id)) ||
            predicates.Any(predicate => contextSignals.All(item => item.Tag.Id != predicate.TagId)))
            throw new ArgumentException("Every selected TAG and operating-context predicate must be unique and have both windows of evidence.");

        var predicateByTag = predicates.ToDictionary(predicate => predicate.TagId);
        var referenceAccepted = allSignals.ToDictionary(item => item.Signal.Id, item => Accept(
            item.Tag, referenceRows[item.Signal.Id], referenceWindow, predicateByTag.GetValueOrDefault(item.Tag.Id)));
        var evaluationAccepted = allSignals.ToDictionary(item => item.Signal.Id, item => Accept(
            item.Tag, evaluationRows[item.Signal.Id], evaluationWindow, predicateByTag.GetValueOrDefault(item.Tag.Id)));
        var targetSignalIds = targets.Select(target => target.Signal.Id).ToHashSet();
        var signalEvidence = allSignals.Select(item => new AssetProcessIntelligenceExperimentalJointDeviationV1SignalEvidence(
            item.Signal.Id,
            targetSignalIds.Contains(item.Signal.Id) ? "target" : "context",
            referenceAccepted[item.Signal.Id].Evidence,
            evaluationAccepted[item.Signal.Id].Evidence)).ToArray();
        var cadence = TimeSpan.FromSeconds(expectedCadenceSeconds);
        var referenceCohorts = BuildCohorts(targets, contextSignals, predicates, referenceAccepted);
        var evaluationCohorts = BuildCohorts(targets, contextSignals, predicates, evaluationAccepted);
        var fitRuns = SplitRuns(referenceCohorts, cadence).Where(run => run.Count >= 2).ToArray();

        ExperimentalJointDeviationReference model;
        try
        {
            model = ExperimentalJointDeviationReference.Fit(
                ContextBinding(assignmentId, contextProfileId, contextProfileVersion, contextSignals, predicates),
                fitRuns.Select(run => (IReadOnlyList<double[]>)run.Select(cohort => cohort.Values.ToArray()).ToArray()).ToArray());
        }
        catch (ArgumentException exception)
        {
            var reason = exception.Message.Contains("robust scale", StringComparison.OrdinalIgnoreCase)
                ? "reference_signal_robust_scale_zero_or_invalid"
                : exception.Message.Contains("ill-conditioned", StringComparison.OrdinalIgnoreCase)
                    ? "reference_covariance_ill_conditioned"
                    : "reference_fit_insufficient_or_invalid";
            return new(referenceCohorts.Count, fitRuns.Length, evaluationCohorts.Count, 0, evaluationCohorts.Count,
                Array.Empty<AssetProcessIntelligenceExperimentalJointDeviationV1ScorePoint>(),
                Array.AsReadOnly(signalEvidence), 0, "insufficient_data", reason);
        }

        var detector = new ExperimentalJointDeviationDetector(model, cadence);
        var points = new List<AssetProcessIntelligenceExperimentalJointDeviationV1ScorePoint>();
        var scoredCount = 0;
        var warmupCount = 0;
        foreach (var cohort in evaluationCohorts)
        {
            var result = detector.Push(new ExperimentalJointDeviationObservation(
                cohort.TimestampUtc, cohort.Values, true, true, model.ContextBinding));
            if (result.Status != "available")
            {
                warmupCount++;
                continue;
            }

            scoredCount++;
            if (points.Count < AssetProcessIntelligenceExperimentalJointDeviationV1Catalog.MaximumReturnedScores)
            {
                var contributions = targets.Select((target, index) => new AssetProcessIntelligenceExperimentalJointDeviationV1TargetContribution(
                    target.Signal.Id,
                    result.TargetLevelContributions![index],
                    result.TargetVarianceContributions![index])).ToArray();
                points.Add(new(cohort.TimestampUtc, result.Status, null,
                    result.LevelComponent, result.RelationshipComponent, result.Score,
                    result.CrossSignalInteraction, Array.AsReadOnly(contributions)));
            }
        }

        return new(referenceCohorts.Count, fitRuns.Length, evaluationCohorts.Count, scoredCount, warmupCount,
            Array.AsReadOnly(points.ToArray()), Array.AsReadOnly(signalEvidence),
            Math.Max(0, scoredCount - points.Count), "available", null);
    }

    private static AcceptedSignalRows Accept(
        TagDefinition tag,
        IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation> rows,
        AssetProcessIntelligenceAnalyticsWindow window,
        AssetProcessIntelligenceOperatingContextPredicateEngineeringDto? predicate)
    {
        var timestampGroups = rows.Where(row => row.SourceTimestampUtc.HasValue && row.SourceTimestampUtc.Value.Offset == TimeSpan.Zero &&
                row.SourceTimestampUtc.Value >= window.FromUtc && row.SourceTimestampUtc.Value <= window.ToUtc)
            .GroupBy(row => row.SourceTimestampUtc!.Value).ToDictionary(group => group.Key, group => group.Count());
        var accepted = new Dictionary<DateTimeOffset, HistoricalQueryValue>();
        var goodQualityCount = 0;
        var compatibleValueCount = 0;
        var utcSourceTimestampCount = 0;
        var inWindowSourceTimestampCount = 0;
        var synchronizedSourceTimestampCount = 0;
        foreach (var row in rows)
        {
            var goodQuality = string.Equals(
                AssetProcessIntelligenceKpiV1.NormalizeQuality(row.Quality), TagQuality.Good.ToString(), StringComparison.Ordinal);
            if (goodQuality) goodQualityCount++;

            var compatibleValue = row.Value is not null &&
                AssetProcessIntelligenceContextEligibilityV1Calculator.SupportsTagValue(tag.DataType, row.Value) &&
                (!AssetProcessIntelligenceContextEligibilityV1Calculator.IsNumeric(tag.DataType) ||
                 AssetProcessIntelligenceContextEligibilityV1Calculator.TryFiniteNumeric(row.Value, out _));
            if (compatibleValue) compatibleValueCount++;

            var utcSourceTimestamp = row.SourceTimestampUtc.HasValue && row.SourceTimestampUtc.Value.Offset == TimeSpan.Zero;
            if (utcSourceTimestamp) utcSourceTimestampCount++;
            var inWindowSourceTimestamp = utcSourceTimestamp && row.SourceTimestampUtc!.Value >= window.FromUtc &&
                row.SourceTimestampUtc.Value <= window.ToUtc;
            if (inWindowSourceTimestamp) inWindowSourceTimestampCount++;
            var synchronized = inWindowSourceTimestamp && string.Equals(
                row.SourceTimestampEvidence, TimestampEvidenceState.Synchronized.ToString(), StringComparison.OrdinalIgnoreCase);
            if (synchronized) synchronizedSourceTimestampCount++;

            if (!goodQuality || !compatibleValue || !inWindowSourceTimestamp || !synchronized ||
                timestampGroups.GetValueOrDefault(row.SourceTimestampUtc!.Value) != 1)
                continue;
            accepted[row.SourceTimestampUtc.Value] = row.Value!;
        }
        var duplicateGroups = timestampGroups.Where(group => group.Value > 1).ToArray();
        var predicateMatchCount = predicate is null ? 0 : accepted.Count(pair =>
            AssetProcessIntelligenceContextEligibilityV1Calculator.Matches(predicate, tag.DataType, pair.Value));
        return new(accepted, new AssetProcessIntelligenceExperimentalJointDeviationV1SignalWindowEvidence(
            rows.Count,
            goodQualityCount,
            compatibleValueCount,
            utcSourceTimestampCount,
            inWindowSourceTimestampCount,
            synchronizedSourceTimestampCount,
            duplicateGroups.Length,
            duplicateGroups.Sum(group => group.Value),
            accepted.Count,
            predicateMatchCount));
    }

    private static IReadOnlyList<Cohort> BuildCohorts(
        IReadOnlyList<(AssetProcessIntelligenceSignalEngineeringDto Signal, TagDefinition Tag)> targets,
        IReadOnlyList<(AssetProcessIntelligenceSignalEngineeringDto Signal, TagDefinition Tag)> contextSignals,
        IReadOnlyList<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto> predicates,
        IReadOnlyDictionary<Guid, AcceptedSignalRows> accepted)
    {
        var targetMaps = targets.ToDictionary(item => item.Signal.Id, item => accepted[item.Signal.Id].Values);
        var contextMaps = contextSignals.ToDictionary(item => item.Tag.Id, item => accepted[item.Signal.Id].Values);
        var candidates = targetMaps.Values.Select(map => map.Keys.ToHashSet()).ToArray();
        if (candidates.Length == 0) return Array.Empty<Cohort>();
        var common = candidates[0];
        foreach (var candidate in candidates.Skip(1)) common.IntersectWith(candidate);
        foreach (var predicate in predicates)
        {
            var contextSignal = contextSignals.SingleOrDefault(item => item.Tag.Id == predicate.TagId);
            if (contextSignal.Signal is null) return Array.Empty<Cohort>();
            var map = contextMaps[predicate.TagId];
            common.RemoveWhere(timestamp => !map.TryGetValue(timestamp, out var value) ||
                !AssetProcessIntelligenceContextEligibilityV1Calculator.Matches(predicate, contextSignal.Tag.DataType, value));
        }

        return common.OrderBy(timestamp => timestamp).Select(timestamp =>
        {
            var values = new double[targets.Count];
            for (var index = 0; index < targets.Count; index++)
            {
                if (!AssetProcessIntelligenceContextEligibilityV1Calculator.TryFiniteNumeric(
                        targetMaps[targets[index].Signal.Id][timestamp], out values[index]))
                    return null;
            }
            return new Cohort(timestamp, values);
        }).Where(cohort => cohort is not null).Select(cohort => cohort!).ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<Cohort>> SplitRuns(IReadOnlyList<Cohort> cohorts, TimeSpan cadence)
    {
        var runs = new List<IReadOnlyList<Cohort>>();
        var current = new List<Cohort>();
        foreach (var cohort in cohorts)
        {
            if (current.Count > 0 && cohort.TimestampUtc - current[^1].TimestampUtc != cadence)
            {
                runs.Add(current.AsReadOnly());
                current = [];
            }
            current.Add(cohort);
        }
        if (current.Count > 0) runs.Add(current.AsReadOnly());
        return runs.AsReadOnly();
    }

    private static string ContextBinding(
        Guid assignmentId,
        Guid contextProfileId,
        int contextProfileVersion,
        IReadOnlyList<(AssetProcessIntelligenceSignalEngineeringDto Signal, TagDefinition Tag)> contextSignals,
        IReadOnlyList<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto> predicates) =>
        $"{assignmentId:N}:{contextProfileId:N}:v{contextProfileVersion}:" +
        string.Join("|", predicates.OrderBy(predicate => predicate.TagId).ThenBy(predicate => predicate.Kind)
            .Select(predicate => $"{predicate.TagId:N}:{predicate.Kind}:{predicate.Minimum:R}:{predicate.Maximum:R}:{predicate.BooleanValue}:{predicate.ExactValue}")) +
        ":" + string.Join("|", contextSignals.Select(item => $"{item.Tag.Id:N}:{item.Tag.DataType}"));

    private sealed record Cohort(DateTimeOffset TimestampUtc, double[] Values);
    private sealed record AcceptedSignalRows(
        Dictionary<DateTimeOffset, HistoricalQueryValue> Values,
        AssetProcessIntelligenceExperimentalJointDeviationV1SignalWindowEvidence Evidence);
}
