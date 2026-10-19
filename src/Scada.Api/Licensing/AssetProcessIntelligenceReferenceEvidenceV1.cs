using System.Collections.ObjectModel;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligenceReferenceEvidenceV1QueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid ProfileId,
    AssetProcessIntelligenceAnalyticsWindow? ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow? ComparisonWindow = null);

public sealed record AssetProcessIntelligenceReferenceEvidenceV1Signal(
    Guid SignalId,
    string SignalKey,
    bool Required,
    Guid TagId,
    string TagPath,
    TagDataType TagDataType,
    string? Unit,
    string Status,
    AssetProcessIntelligenceReferenceEvidenceV1SignalWindowEvidence Reference,
    AssetProcessIntelligenceReferenceEvidenceV1SignalWindowEvidence? Comparison);

public sealed record AssetProcessIntelligenceReferenceEvidenceV1SignalWindowEvidence(
    AssetProcessIntelligenceAnalyticsWindow Window,
    int RawObservationCount,
    int GoodFiniteNumericCount,
    int AcceptedSynchronizedCount,
    int RejectedObservationCount,
    int AmbiguousSourceTimestampGroupCount,
    int AmbiguousSourceTimestampRowCount,
    IReadOnlyDictionary<string, int> QualityCounts,
    IReadOnlyDictionary<string, int> RejectionCounts,
    string Status);

public sealed record AssetProcessIntelligenceReferenceEvidenceV1CohortEvidence(
    int ConfiguredRequiredSignalCount,
    int RequiredNumericSignalCount,
    int RequiredNumericSignalsWithAcceptedRows,
    int? CompleteRequiredNumericCohortCount,
    string RequiredCohortStatus,
    IReadOnlyList<string> RequiredReasons,
    int SelectedNumericSignalCount,
    int SelectedNumericSignalsWithAcceptedRows,
    int? CompleteSelectedNumericCohortCount,
    string SelectedCohortStatus,
    IReadOnlyList<string> SelectedReasons);

public sealed record AssetProcessIntelligenceReferenceEvidenceV1Provenance(
    string SubmittedBySubjectId,
    DateTimeOffset RequestReceivedAtUtc,
    string WindowSelection,
    string RequestReceivedTimeMeaning,
    string Dataset,
    int DatasetContractVersion,
    string QueryTimestampBasis,
    string AcceptedObservationRule,
    string SynchronizedCohortRule,
    string ExpectedCadence,
    string Completeness,
    string ReferenceHealth,
    string Representativeness,
    string RegimeEquivalence,
    string Interpretation);

public sealed record AssetProcessIntelligenceReferenceEvidenceV1Response(
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
    string ProfileKey,
    string ProfileName,
    AssetProcessIntelligenceAnalyticsWindow ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow? ComparisonWindow,
    IReadOnlyList<AssetProcessIntelligenceReferenceEvidenceV1Signal> Signals,
    AssetProcessIntelligenceReferenceEvidenceV1CohortEvidence ReferenceCohort,
    AssetProcessIntelligenceReferenceEvidenceV1CohortEvidence? ComparisonCohort,
    int TotalRawObservationCount,
    AssetProcessIntelligenceReferenceEvidenceV1Provenance Provenance);

public static class AssetProcessIntelligenceReferenceEvidenceV1Catalog
{
    public const int ContractVersion = 1;
    public const int CatalogVersion = 1;
    public const string RuleId = "reference-evidence.v1";
    public const string RuleVersion = "1.0.0";
    public const int MaximumSignals = 16;
    public const int MaximumRawRowsAcrossQuery = 50000;
    public const int QueryPageSize = 200;
    public static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(31);

    public static object Describe() => new
    {
        contractVersion = ContractVersion,
        catalogVersion = CatalogVersion,
        ruleId = RuleId,
        ruleVersion = RuleVersion,
        dataset = HistoricalDatasets.HistorianSamplesV2,
        datasetContractVersion = 2,
        maximumProfileSignals = MaximumSignals,
        maximumWindowDays = (int)MaximumWindow.TotalDays,
        maximumRawRowsAcrossAllSignalsAndWindows = MaximumRawRowsAcrossQuery,
        pageSize = QueryPageSize,
        queryTimestampBasis = "Historian observation timestamp bounds retrieval; source timestamps are independently checked against the requested window.",
        synchronizedCohort = "exact equality of unique non-null UTC source.timestamp values with explicit Synchronized evidence; no nearest join, interpolation, carry-forward, or imputation.",
        requiredAndOptional = "optional signal absence does not suppress the required cohort; required unsupported or unavailable evidence is reported explicitly.",
        expectedCadence = "unknown_without_a_declared_cadence_contract",
        completeness = "unknown_without_a_declared_completeness_contract",
        referenceHealth = "not_assessed",
        representativeness = "not_assessed",
        regimeEquivalence = "not_assessed",
        interpretation = "auditable historical evidence and observed data sufficiency only; not baseline validation or model validation"
    };
}

internal sealed record AssetProcessIntelligenceReferenceEvidenceV1SignalInput(
    Guid SignalId,
    bool Required,
    bool Numeric);

internal sealed record AssetProcessIntelligenceReferenceEvidenceV1WindowCalculation(
    IReadOnlyList<AssetProcessIntelligenceReferenceEvidenceV1SignalWindowEvidence> Signals,
    AssetProcessIntelligenceReferenceEvidenceV1CohortEvidence Cohort);

internal static class AssetProcessIntelligenceReferenceEvidenceV1Calculator
{
    private const string MissingTimestamp = "source_timestamp_missing";
    private const string Unsynchronized = "source_timestamp_not_synchronized";
    private const string OutsideWindow = "source_timestamp_outside_requested_window";
    private const string DuplicateTimestamp = "duplicate_source_timestamp";

    public static AssetProcessIntelligenceReferenceEvidenceV1WindowCalculation CalculateWindow(
        IReadOnlyList<AssetProcessIntelligenceReferenceEvidenceV1SignalInput> signals,
        AssetProcessIntelligenceAnalyticsWindow window,
        IReadOnlyDictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>> rowsBySignal)
    {
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(rowsBySignal);
        if (signals.Count is 0 or > AssetProcessIntelligenceReferenceEvidenceV1Catalog.MaximumSignals ||
            signals.Any(signal => signal.SignalId == Guid.Empty) ||
            signals.Select(signal => signal.SignalId).Distinct().Count() != signals.Count ||
            signals.Any(signal => signal.Numeric && !rowsBySignal.ContainsKey(signal.SignalId)))
            throw new ArgumentException("A unique bounded profile signal set and rows for every numeric signal are required.", nameof(signals));

        var calculations = signals.Select(signal => EvaluateSignal(
            signal,
            window,
            rowsBySignal.GetValueOrDefault(signal.SignalId) ?? Array.Empty<AssetProcessIntelligenceContextualDeviationVectorRawObservation>())).ToArray();
        return new(
            Array.AsReadOnly(calculations.Select(item => item.Evidence).ToArray()),
            BuildCohorts(calculations));
    }

    private static SignalCalculation EvaluateSignal(
        AssetProcessIntelligenceReferenceEvidenceV1SignalInput signal,
        AssetProcessIntelligenceAnalyticsWindow window,
        IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation> rows)
    {
        var qualityCounts = Enum.GetValues<TagQuality>()
            .ToDictionary(value => value.ToString(), _ => 0, StringComparer.Ordinal);
        qualityCounts.Add("Missing", 0);
        qualityCounts.Add("Unrecognized", 0);
        var rejectionCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var goodFiniteNumericCount = 0;
        var acceptedTimestamps = new HashSet<DateTimeOffset>();
        var duplicateGroups = rows
            .Where(row => IsSourceTimestampInWindow(row.SourceTimestampUtc, window))
            .GroupBy(row => row.SourceTimestampUtc!.Value)
            .Where(group => group.Count() > 1)
            .ToArray();
        var duplicateTimestamps = duplicateGroups.Select(group => group.Key).ToHashSet();

        foreach (var row in rows)
        {
            var quality = AssetProcessIntelligenceKpiV1.NormalizeQuality(row.Quality);
            qualityCounts[quality]++;
            var goodFiniteNumeric = AssetProcessIntelligenceKpiV1.TryGetAcceptedGoodNumericValue(
                new AssetProcessIntelligenceRawObservation(row.ObservedTimestampUtc, row.Quality, row.Value), out _);
            if (goodFiniteNumeric)
                goodFiniteNumericCount++;

            if (!signal.Numeric)
                continue;

            if (IsSourceTimestampInWindow(row.SourceTimestampUtc, window) &&
                duplicateTimestamps.Contains(row.SourceTimestampUtc!.Value))
            {
                Increment(rejectionCounts, DuplicateTimestamp);
                continue;
            }

            if (quality == "Missing") { Increment(rejectionCounts, "quality_missing"); continue; }
            if (quality == "Unrecognized") { Increment(rejectionCounts, "quality_unrecognized"); continue; }
            if (quality != TagQuality.Good.ToString()) { Increment(rejectionCounts, "quality_not_good"); continue; }
            if (!goodFiniteNumeric) { Increment(rejectionCounts, "value_not_finite_numeric"); continue; }
            if (!row.SourceTimestampUtc.HasValue) { Increment(rejectionCounts, MissingTimestamp); continue; }
            if (!string.Equals(row.SourceTimestampEvidence, TimestampEvidenceState.Synchronized.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (!Enum.TryParse<TimestampEvidenceState>(row.SourceTimestampEvidence, true, out var parsedEvidence) || !Enum.IsDefined(parsedEvidence))
                    Increment(rejectionCounts, "source_timestamp_evidence_unrecognized");
                else
                    Increment(rejectionCounts, Unsynchronized);
                continue;
            }
            var timestamp = row.SourceTimestampUtc.Value;
            if (timestamp.Offset != TimeSpan.Zero || timestamp < window.FromUtc || timestamp > window.ToUtc)
            {
                Increment(rejectionCounts, OutsideWindow);
                continue;
            }

            acceptedTimestamps.Add(timestamp);
        }

        var rejectedCount = rows.Count - acceptedTimestamps.Count;
        var status = !signal.Numeric
            ? "unsupported_non_numeric_tag_type"
            : rows.Count == 0
                ? "missing_historical_observations"
                : goodFiniteNumericCount == 0
                    ? "insufficient_no_good_finite_numeric_samples"
                    : acceptedTimestamps.Count == 0
                        ? "insufficient_no_synchronized_accepted_samples"
                        : "evidence_available";
        var orderedQuality = qualityCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var orderedRejections = rejectionCounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var evidence = new AssetProcessIntelligenceReferenceEvidenceV1SignalWindowEvidence(
            window,
            rows.Count,
            goodFiniteNumericCount,
            acceptedTimestamps.Count,
            rejectedCount,
            duplicateGroups.Length,
            duplicateGroups.Sum(group => group.Count()),
            new ReadOnlyDictionary<string, int>(orderedQuality),
            new ReadOnlyDictionary<string, int>(orderedRejections),
            status);
        return new(signal, evidence, acceptedTimestamps);
    }

    private static AssetProcessIntelligenceReferenceEvidenceV1CohortEvidence BuildCohorts(
        IReadOnlyList<SignalCalculation> signals)
    {
        var required = signals.Where(item => item.Signal.Required).ToArray();
        var requiredNumeric = required.Where(item => item.Signal.Numeric).ToArray();
        var requiredWithRows = requiredNumeric.Count(item => item.AcceptedTimestamps.Count > 0);
        var requiredReasons = new List<string>();
        int? requiredCount = null;
        string requiredStatus;
        if (required.Length == 0)
        {
            requiredStatus = "insufficient_no_required_signals";
            requiredReasons.Add("no_required_signals_configured");
        }
        else if (requiredNumeric.Length == 0)
        {
            requiredStatus = "insufficient_no_required_numeric_signals";
            requiredReasons.Add("no_required_numeric_signals");
        }
        else if (requiredNumeric.Length != required.Length)
        {
            requiredStatus = "insufficient_required_signal_unsupported";
            requiredReasons.Add("required_signal_unsupported_non_numeric_tag_type");
        }
        else
        {
            requiredCount = IntersectionCount(requiredNumeric);
            if (requiredWithRows != requiredNumeric.Length)
                requiredReasons.Add("required_signal_has_no_synchronized_accepted_rows");
            if (requiredCount == 0)
                requiredReasons.Add("no_complete_required_synchronized_cohort");

            if (requiredNumeric.Length == 1)
            {
                requiredStatus = "single_required_numeric_signal_no_alignment_comparison";
                requiredReasons.Add("single_required_numeric_signal_cannot_demonstrate_cross_signal_alignment");
            }
            else if (requiredCount == 0)
            {
                requiredStatus = requiredWithRows != requiredNumeric.Length
                    ? "insufficient_required_signal_data"
                    : "insufficient_no_complete_required_numeric_cohort";
            }
            else
            {
                requiredStatus = "required_source_synchronized_cohort_observed";
            }
        }

        var selectedNumeric = signals.Where(item => item.Signal.Numeric).ToArray();
        var selectedWithRows = selectedNumeric.Count(item => item.AcceptedTimestamps.Count > 0);
        var selectedReasons = new List<string>();
        int? selectedCount = null;
        string selectedStatus;
        if (selectedNumeric.Length == 0)
        {
            selectedStatus = "insufficient_no_selected_numeric_signals";
            selectedReasons.Add("no_selected_numeric_signals");
        }
        else
        {
            selectedCount = IntersectionCount(selectedNumeric);
            if (selectedWithRows != selectedNumeric.Length)
                selectedReasons.Add("selected_signal_has_no_synchronized_accepted_rows");
            if (selectedCount == 0)
                selectedReasons.Add("no_complete_selected_synchronized_cohort");

            if (selectedNumeric.Length == 1)
            {
                selectedStatus = "single_selected_numeric_signal_no_alignment_comparison";
                selectedReasons.Add("single_selected_numeric_signal_cannot_demonstrate_cross_signal_alignment");
            }
            else if (selectedCount == 0)
            {
                selectedStatus = selectedWithRows != selectedNumeric.Length
                    ? "insufficient_selected_signal_data"
                    : "insufficient_no_complete_selected_numeric_cohort";
            }
            else
            {
                selectedStatus = "selected_source_synchronized_cohort_observed";
            }
        }

        return new(
            required.Length,
            requiredNumeric.Length,
            requiredWithRows,
            requiredCount,
            requiredStatus,
            Array.AsReadOnly(requiredReasons.Distinct(StringComparer.Ordinal).ToArray()),
            selectedNumeric.Length,
            selectedWithRows,
            selectedCount,
            selectedStatus,
            Array.AsReadOnly(selectedReasons.Distinct(StringComparer.Ordinal).ToArray()));
    }

    private static int IntersectionCount(IReadOnlyList<SignalCalculation> signals)
    {
        var intersection = new HashSet<DateTimeOffset>(signals[0].AcceptedTimestamps);
        for (var index = 1; index < signals.Count; index++)
            intersection.IntersectWith(signals[index].AcceptedTimestamps);
        return intersection.Count;
    }

    private static bool IsSourceTimestampInWindow(
        DateTimeOffset? timestamp,
        AssetProcessIntelligenceAnalyticsWindow window) =>
        timestamp.HasValue &&
        timestamp.Value.Offset == TimeSpan.Zero &&
        timestamp.Value >= window.FromUtc &&
        timestamp.Value <= window.ToUtc;

    private static void Increment(IDictionary<string, int> counts, string reason)
    {
        counts.TryGetValue(reason, out var current);
        counts[reason] = current + 1;
    }

    private sealed record SignalCalculation(
        AssetProcessIntelligenceReferenceEvidenceV1SignalInput Signal,
        AssetProcessIntelligenceReferenceEvidenceV1SignalWindowEvidence Evidence,
        HashSet<DateTimeOffset> AcceptedTimestamps);
}
