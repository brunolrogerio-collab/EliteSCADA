using System.Collections.ObjectModel;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid AssignmentId,
    Guid CurrentSignalId,
    Guid FrequencySignalId,
    AssetProcessIntelligenceAnalyticsWindow? PrimaryWindow,
    AssetProcessIntelligenceAnalyticsWindow? ComparisonWindow,
    double MinimumFrequencyHz,
    double MaximumFrequencyHz);

public sealed record AssetProcessIntelligenceCurrentFrequencyComparisonV1Signal(
    Guid SignalId,
    string SignalKey,
    Guid TagId,
    string TagPath,
    TagDataType TagDataType,
    string RoleCode,
    string? CurrentBasis,
    string? SignalKind,
    string? FrequencyReference,
    string? EngineeringUnitLabel,
    string ControlledUnitCode,
    string CanonicalUnit);

public sealed record AssetProcessIntelligenceCurrentFrequencyComparisonV1SignalWindowEvidence(
    int RawObservationCount,
    int GoodFiniteNumericCount,
    int AcceptedSynchronizedSampleCount,
    int RejectedObservationCount,
    int AmbiguousSourceTimestampGroupCount,
    int AmbiguousSourceTimestampRowCount,
    IReadOnlyDictionary<string, int> QualityCounts,
    IReadOnlyDictionary<string, int> RejectionCounts);

public sealed record AssetProcessIntelligenceCurrentFrequencyComparisonV1PairEvidence(
    DateTimeOffset SourceTimestampUtc,
    DateTimeOffset CurrentObservedTimestampUtc,
    DateTimeOffset FrequencyObservedTimestampUtc,
    double CurrentAmpere,
    double FrequencyHertz,
    bool FrequencyInRequestedRange,
    bool IncludedInMean,
    string? ExclusionReasonCode);

public sealed record AssetProcessIntelligenceCurrentFrequencyComparisonV1WindowEvidence(
    AssetProcessIntelligenceAnalyticsWindow Window,
    AssetProcessIntelligenceCurrentFrequencyComparisonV1SignalWindowEvidence Current,
    AssetProcessIntelligenceCurrentFrequencyComparisonV1SignalWindowEvidence Frequency,
    int ExactSourceTimestampPairCount,
    int FrequencyInRangePairCount,
    int FrequencyOutOfRangePairCount,
    int SelectedPairCount,
    int ReusedSourceTimestampPairCount,
    int UnmatchedCurrentAcceptedSampleCount,
    int UnmatchedFrequencyAcceptedSampleCount,
    IReadOnlyList<AssetProcessIntelligenceCurrentFrequencyComparisonV1PairEvidence> Pairs);

public sealed record AssetProcessIntelligenceCurrentFrequencyComparisonV1Metric(
    string Status,
    IReadOnlyList<string> ReasonCodes,
    double? PrimaryMeanCurrentA,
    double? ComparisonMeanCurrentA,
    double? DifferenceA,
    double? PercentChange,
    string PercentChangeStatus,
    string? PercentChangeReasonCode);

public sealed record AssetProcessIntelligenceCurrentFrequencyComparisonV1Provenance(
    string Dataset,
    int DatasetContractVersion,
    string QueryWindowTimestampBasis,
    string PairTimestampBasis,
    string SourceTimestampEvidenceRequirement,
    string FrequencyMeaning,
    string PhysicalSimultaneity,
    string OperatingRegimeEquivalence,
    string ExpectedCadence,
    string Completeness,
    string Formula);

public sealed record AssetProcessIntelligenceCurrentFrequencyComparisonV1Response(
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
    AssetProcessIntelligenceCurrentFrequencyComparisonV1Signal CurrentSignal,
    AssetProcessIntelligenceCurrentFrequencyComparisonV1Signal FrequencySignal,
    double MinimumFrequencyHz,
    double MaximumFrequencyHz,
    AssetProcessIntelligenceCurrentFrequencyComparisonV1WindowEvidence Primary,
    AssetProcessIntelligenceCurrentFrequencyComparisonV1WindowEvidence Comparison,
    int TotalRawObservationCount,
    AssetProcessIntelligenceCurrentFrequencyComparisonV1Metric Metric,
    AssetProcessIntelligenceCurrentFrequencyComparisonV1Provenance Provenance);

public static class AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog
{
    public const int ContractVersion = 1;
    public const int CatalogVersion = 1;
    public const string RuleId = "current-at-recorded-frequency.compare.v1";
    public const string RuleVersion = "1.0.0";
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
        currentUnit = new { code = "unit.electric_current.ampere", canonical = "A" },
        frequencyUnit = new { code = "unit.frequency.hertz", canonical = "Hz" },
        frequencyInterpretation = "Use the selected measured signal's explicit FrequencyReference; drive electrical frequency is not shaft rotational speed.",
        maximumWindowDays = (int)MaximumWindow.TotalDays,
        maximumRawRowsAcrossBothTagsAndBothWindows = MaximumRawRowsAcrossQuery,
        pageSize = QueryPageSize,
        frequencyPredicate = "minimumFrequencyHz <= paired measured frequency in Hz <= maximumFrequencyHz",
        formula = "For each window, arithmetic sample mean of paired current values in A where the exact-paired measured frequency is inside the explicit inclusive Hz range; differenceA = primaryMeanA - comparisonMeanA; percentChange = 100 * differenceA / comparisonMeanA when comparisonMeanA is nonzero and the result is finite.",
        timestampPairing = "exact equality of non-null SourceTimestamp values with explicit Synchronized evidence; no interpolation, carry-forward, nearest-time join, or tolerance",
        sourceTimestampEvidence = "Synchronized is per-measurement provenance, not a physical acquisition-batch or clock-accuracy guarantee",
        physicalSimultaneity = "unverified",
        operatingRegimeEquivalence = "unverified",
        expectedCadence = "unknown",
        completeness = "unknown",
        supportedStatuses = new[]
        {
            AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.AvailableStatus,
            AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.InsufficientDataStatus,
            AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.UnsupportedStatus
        },
        reasonCodes = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.ReasonCodes
    };
}

internal sealed record AssetProcessIntelligenceCurrentFrequencyRawObservation(
    DateTimeOffset ObservedTimestampUtc,
    string? Quality,
    HistoricalQueryValue? Value,
    DateTimeOffset? SourceTimestampUtc,
    string? SourceTimestampEvidence);

internal sealed record AssetProcessIntelligenceCurrentFrequencyWindowCalculation(
    AssetProcessIntelligenceCurrentFrequencyComparisonV1WindowEvidence Evidence,
    double? MeanCurrentA);

internal static class AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator
{
    public const string AvailableStatus = "available";
    public const string InsufficientDataStatus = "insufficient_data";
    public const string UnsupportedStatus = "unsupported";
    public const string TooFewPrimaryPairsReason = "fewer_than_two_primary_frequency_conditioned_pairs";
    public const string TooFewComparisonPairsReason = "fewer_than_two_comparison_frequency_conditioned_pairs";
    public const string ReusedSourceTimestampReason = "source_timestamp_reused_across_comparison_windows";
    public const string ZeroComparisonMeanReason = "comparison_mean_zero_percent_change_unavailable";
    public const string NonFiniteDifferenceReason = "non_finite_current_difference";
    public const string NonFiniteWindowMeanReason = "non_finite_window_mean";
    public const string NonFinitePercentChangeReason = "non_finite_percent_change";

    public static IReadOnlyList<string> ReasonCodes { get; } = Array.AsReadOnly(new[]
    {
        TooFewPrimaryPairsReason,
        TooFewComparisonPairsReason,
        ReusedSourceTimestampReason,
        ZeroComparisonMeanReason,
        NonFiniteDifferenceReason,
        NonFiniteWindowMeanReason,
        NonFinitePercentChangeReason,
        "source_timestamp_missing",
        "source_timestamp_evidence_unrecognized",
        "source_timestamp_not_synchronized",
        "source_timestamp_outside_requested_window",
        "quality_missing",
        "quality_not_good",
        "quality_unrecognized",
        "value_not_finite_numeric",
        "unit_conversion_non_finite",
        "duplicate_current_source_timestamp",
        "duplicate_frequency_source_timestamp",
        "accepted_current_sample_unmatched",
        "accepted_frequency_sample_unmatched",
        "frequency_outside_requested_range"
    });

    public static AssetProcessIntelligenceCurrentFrequencyWindowCalculation CalculateWindow(
        AssetProcessIntelligenceAnalyticsWindow window,
        IReadOnlyList<AssetProcessIntelligenceCurrentFrequencyRawObservation> currentRows,
        IReadOnlyList<AssetProcessIntelligenceCurrentFrequencyRawObservation> frequencyRows,
        ControlledEngineeringUnitReference currentUnit,
        ControlledEngineeringUnitReference frequencyUnit,
        double minimumFrequencyHz,
        double maximumFrequencyHz,
        IReadOnlySet<DateTimeOffset>? reusedSourceTimestamps = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(currentRows);
        ArgumentNullException.ThrowIfNull(frequencyRows);
        ArgumentNullException.ThrowIfNull(currentUnit);
        ArgumentNullException.ThrowIfNull(frequencyUnit);
        reusedSourceTimestamps ??= new HashSet<DateTimeOffset>();

        var current = EvaluateSignal(currentRows, window, currentUnit);
        var frequency = EvaluateSignal(frequencyRows, window, frequencyUnit);
        var duplicateCurrent = RawSourceTimestampGroups(currentRows, window)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        var duplicateFrequency = RawSourceTimestampGroups(frequencyRows, window)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        var currentBySource = current.Accepted
            .Where(item => !duplicateCurrent.Contains(item.SourceTimestampUtc))
            .ToDictionary(item => item.SourceTimestampUtc);
        var frequencyBySource = frequency.Accepted
            .Where(item => !duplicateFrequency.Contains(item.SourceTimestampUtc))
            .ToDictionary(item => item.SourceTimestampUtc);
        var exactKeys = currentBySource.Keys.Intersect(frequencyBySource.Keys).Order().ToArray();
        var pairs = new List<AssetProcessIntelligenceCurrentFrequencyComparisonV1PairEvidence>(exactKeys.Length);
        foreach (var sourceTimestamp in exactKeys)
        {
            var currentSample = currentBySource[sourceTimestamp];
            var frequencySample = frequencyBySource[sourceTimestamp];
            var inRange = frequencySample.Value >= minimumFrequencyHz && frequencySample.Value <= maximumFrequencyHz;
            var reused = reusedSourceTimestamps.Contains(sourceTimestamp);
            var included = inRange && !reused;
            pairs.Add(new(
                sourceTimestamp,
                currentSample.ObservedTimestampUtc,
                frequencySample.ObservedTimestampUtc,
                currentSample.Value,
                frequencySample.Value,
                inRange,
                included,
                reused ? ReusedSourceTimestampReason : inRange ? null : "frequency_outside_requested_range"));
        }

        var currentAcceptedKeys = currentBySource.Keys.ToHashSet();
        var frequencyAcceptedKeys = frequencyBySource.Keys.ToHashSet();
        var unmatchedCurrent = currentAcceptedKeys.Except(frequencyAcceptedKeys).Count();
        var unmatchedFrequency = frequencyAcceptedKeys.Except(currentAcceptedKeys).Count();
        var stalePairCount = pairs.Count(pair => reusedSourceTimestamps.Contains(pair.SourceTimestampUtc));
        var includedPairs = pairs.Where(pair => pair.IncludedInMean).ToArray();
        var mean = CalculateMean(includedPairs.Select(pair => pair.CurrentAmpere));

        AddPairRejections(current.Rejections, currentRows, window, duplicateCurrent,
            "duplicate_current_source_timestamp", "accepted_current_sample_unmatched", unmatchedCurrent);
        AddPairRejections(frequency.Rejections, frequencyRows, window, duplicateFrequency,
            "duplicate_frequency_source_timestamp", "accepted_frequency_sample_unmatched", unmatchedFrequency);

        Increment(frequency.Rejections, "frequency_outside_requested_range",
            pairs.Count(pair => !pair.FrequencyInRequestedRange));
        var currentEvidence = ToEvidence(current, currentRows, window, duplicateCurrent);
        var frequencyEvidence = ToEvidence(frequency, frequencyRows, window, duplicateFrequency);
        var windowEvidence = new AssetProcessIntelligenceCurrentFrequencyComparisonV1WindowEvidence(
            window,
            currentEvidence,
            frequencyEvidence,
            pairs.Count,
            pairs.Count(pair => pair.FrequencyInRequestedRange),
            pairs.Count(pair => !pair.FrequencyInRequestedRange),
            includedPairs.Length,
            stalePairCount,
            unmatchedCurrent,
            unmatchedFrequency,
            Array.AsReadOnly(pairs.ToArray()));
        return new(windowEvidence, mean);
    }

    public static AssetProcessIntelligenceCurrentFrequencyComparisonV1Metric CalculateMetric(
        AssetProcessIntelligenceCurrentFrequencyWindowCalculation primary,
        AssetProcessIntelligenceCurrentFrequencyWindowCalculation comparison,
        bool sourceTimestampReusedAcrossWindows)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(comparison);
        var reasons = new List<string>();
        if (sourceTimestampReusedAcrossWindows)
            reasons.Add(ReusedSourceTimestampReason);
        if (primary.Evidence.SelectedPairCount < 2)
            reasons.Add(TooFewPrimaryPairsReason);
        if (comparison.Evidence.SelectedPairCount < 2)
            reasons.Add(TooFewComparisonPairsReason);
        if (reasons.Count > 0)
            return new(
                InsufficientDataStatus,
                Array.AsReadOnly(reasons.ToArray()),
                null,
                null,
                null,
                null,
                "unavailable",
                sourceTimestampReusedAcrossWindows ? ReusedSourceTimestampReason : string.Join(";", reasons));

        var primaryMean = primary.MeanCurrentA;
        var comparisonMean = comparison.MeanCurrentA;
        if (!primaryMean.HasValue || !comparisonMean.HasValue)
            return new(
                UnsupportedStatus,
                Array.AsReadOnly(new[] { NonFiniteWindowMeanReason }),
                primaryMean,
                comparisonMean,
                null,
                null,
                "unavailable",
                NonFiniteWindowMeanReason);

        var difference = primaryMean.Value - comparisonMean.Value;
        if (!double.IsFinite(difference))
            return new(
                UnsupportedStatus,
                Array.AsReadOnly(new[] { NonFiniteDifferenceReason }),
                primaryMean,
                comparisonMean,
                null,
                null,
                "unavailable",
                NonFiniteDifferenceReason);

        if (comparisonMean.Value == 0d)
            return new(
                AvailableStatus,
                Array.AsReadOnly(new[] { ZeroComparisonMeanReason }),
                primaryMean,
                comparisonMean,
                difference,
                null,
                "unsupported",
                ZeroComparisonMeanReason);

        var percentChange = difference / comparisonMean.Value * 100d;
        if (!double.IsFinite(percentChange))
            return new(
                AvailableStatus,
                Array.AsReadOnly(new[] { NonFinitePercentChangeReason }),
                primaryMean,
                comparisonMean,
                difference,
                null,
                "unsupported",
                NonFinitePercentChangeReason);

        return new(
            AvailableStatus,
            Array.Empty<string>(),
            primaryMean,
            comparisonMean,
            difference,
            percentChange,
            "available",
            null);
    }

    public static IReadOnlySet<DateTimeOffset> AcceptedSynchronizedSourceTimestamps(
        IReadOnlyList<AssetProcessIntelligenceCurrentFrequencyRawObservation> rows,
        AssetProcessIntelligenceAnalyticsWindow window,
        ControlledEngineeringUnitReference unit)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(unit);
        var evaluation = EvaluateSignal(rows, window, unit);
        var duplicateKeys = RawSourceTimestampGroups(rows, window)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        return evaluation.Accepted
            .Where(sample => !duplicateKeys.Contains(sample.SourceTimestampUtc))
            .Select(sample => sample.SourceTimestampUtc)
            .ToHashSet();
    }

    private static SignalEvaluation EvaluateSignal(
        IReadOnlyList<AssetProcessIntelligenceCurrentFrequencyRawObservation> rows,
        AssetProcessIntelligenceAnalyticsWindow window,
        ControlledEngineeringUnitReference unit)
    {
        var qualityCounts = Enum.GetValues<TagQuality>()
            .ToDictionary(static quality => quality.ToString(), static _ => 0, StringComparer.Ordinal);
        qualityCounts.Add("Missing", 0);
        qualityCounts.Add("Unrecognized", 0);
        var rejectionCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var accepted = new List<AcceptedSample>();
        var goodFiniteNumericCount = 0;
        foreach (var row in rows)
        {
            var quality = AssetProcessIntelligenceKpiV1.NormalizeQuality(row.Quality);
            qualityCounts[quality]++;
            var rawObservation = new AssetProcessIntelligenceRawObservation(row.ObservedTimestampUtc, row.Quality, row.Value);
            var isGoodFiniteNumeric = AssetProcessIntelligenceKpiV1.TryGetAcceptedGoodNumericValue(rawObservation, out var value);
            if (isGoodFiniteNumeric)
                goodFiniteNumericCount++;

            if (quality == "Missing")
            {
                Increment(rejectionCounts, "quality_missing");
                continue;
            }
            if (quality == "Unrecognized")
            {
                Increment(rejectionCounts, "quality_unrecognized");
                continue;
            }
            if (quality != TagQuality.Good.ToString())
            {
                Increment(rejectionCounts, "quality_not_good");
                continue;
            }
            if (!isGoodFiniteNumeric)
            {
                Increment(rejectionCounts, "value_not_finite_numeric");
                continue;
            }
            if (!row.SourceTimestampUtc.HasValue)
            {
                Increment(rejectionCounts, "source_timestamp_missing");
                continue;
            }
            if (!TryParseEvidence(row.SourceTimestampEvidence, out var evidence))
            {
                Increment(rejectionCounts, "source_timestamp_evidence_unrecognized");
                continue;
            }
            if (evidence != TimestampEvidenceState.Synchronized)
            {
                Increment(rejectionCounts, "source_timestamp_not_synchronized");
                continue;
            }
            var sourceTimestamp = row.SourceTimestampUtc.Value;
            if (sourceTimestamp.Offset != TimeSpan.Zero ||
                sourceTimestamp < window.FromUtc || sourceTimestamp > window.ToUtc)
            {
                Increment(rejectionCounts, "source_timestamp_outside_requested_window");
                continue;
            }
            if (!ControlledEngineeringUnitCatalog.TryConvertToCanonical(value, unit, out var canonical))
            {
                Increment(rejectionCounts, "unit_conversion_non_finite");
                continue;
            }
            accepted.Add(new(row.ObservedTimestampUtc, sourceTimestamp, canonical));
        }

        return new(rows.Count, goodFiniteNumericCount, accepted, qualityCounts, rejectionCounts);
    }

    private static IEnumerable<IGrouping<DateTimeOffset, AssetProcessIntelligenceCurrentFrequencyRawObservation>>
        RawSourceTimestampGroups(
            IReadOnlyList<AssetProcessIntelligenceCurrentFrequencyRawObservation> rows,
            AssetProcessIntelligenceAnalyticsWindow window) =>
        rows.Where(row => row.SourceTimestampUtc is { } timestamp &&
                          timestamp.Offset == TimeSpan.Zero &&
                          timestamp >= window.FromUtc && timestamp <= window.ToUtc)
            .GroupBy(row => row.SourceTimestampUtc!.Value);

    private static AssetProcessIntelligenceCurrentFrequencyComparisonV1SignalWindowEvidence ToEvidence(
        SignalEvaluation evaluation,
        IReadOnlyList<AssetProcessIntelligenceCurrentFrequencyRawObservation> rows,
        AssetProcessIntelligenceAnalyticsWindow window,
        IReadOnlySet<DateTimeOffset> duplicateKeys)
    {
        var duplicateRows = RawSourceTimestampGroups(rows, window)
            .Where(group => duplicateKeys.Contains(group.Key))
            .Sum(group => group.Count());
        var accepted = evaluation.Accepted.Count(sample => !duplicateKeys.Contains(sample.SourceTimestampUtc));
        var rejected = evaluation.RawObservationCount - accepted;
        return new(
            evaluation.RawObservationCount,
            evaluation.GoodFiniteNumericCount,
            accepted,
            rejected,
            duplicateKeys.Count,
            duplicateRows,
            new ReadOnlyDictionary<string, int>(evaluation.QualityCounts.ToDictionary(
                static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal)),
            new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(evaluation.Rejections, StringComparer.Ordinal)));
    }

    private static void AddPairRejections(
        SortedDictionary<string, int> rejections,
        IReadOnlyList<AssetProcessIntelligenceCurrentFrequencyRawObservation> rows,
        AssetProcessIntelligenceAnalyticsWindow window,
        IReadOnlySet<DateTimeOffset> duplicateKeys,
        string duplicateReason,
        string unmatchedReason,
        int unmatchedCount)
    {
        var duplicateRows = RawSourceTimestampGroups(rows, window)
            .Where(group => duplicateKeys.Contains(group.Key))
            .Sum(group => group.Count());
        if (duplicateRows > 0) Increment(rejections, duplicateReason, duplicateRows);
        if (unmatchedCount > 0) Increment(rejections, unmatchedReason, unmatchedCount);
    }

    private static double? CalculateMean(IEnumerable<double> values)
    {
        var count = 0;
        var mean = 0d;
        foreach (var value in values)
        {
            count++;
            mean = mean * ((count - 1d) / count) + value / count;
            if (!double.IsFinite(mean)) return null;
        }
        return count == 0 ? null : mean;
    }

    private static bool TryParseEvidence(string? text, out TimestampEvidenceState evidence)
    {
        evidence = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Trim();
        return Enum.GetNames<TimestampEvidenceState>()
                   .Contains(normalized, StringComparer.OrdinalIgnoreCase) &&
               Enum.TryParse(normalized, ignoreCase: true, out evidence) &&
               Enum.IsDefined(evidence);
    }

    private static void Increment(IDictionary<string, int> counts, string key, int amount = 1)
    {
        if (amount <= 0) return;
        counts.TryGetValue(key, out var current);
        counts[key] = current + amount;
    }

    private sealed record AcceptedSample(
        DateTimeOffset ObservedTimestampUtc,
        DateTimeOffset SourceTimestampUtc,
        double Value);

    private sealed record SignalEvaluation(
        int RawObservationCount,
        int GoodFiniteNumericCount,
        IReadOnlyList<AcceptedSample> Accepted,
        IReadOnlyDictionary<string, int> QualityCounts,
        SortedDictionary<string, int> Rejections);
}
