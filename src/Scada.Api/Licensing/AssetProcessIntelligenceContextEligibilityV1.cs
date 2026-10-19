using System.Globalization;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligenceContextEligibilityV1QueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid AssignmentId,
    Guid OperatingContextProfileId,
    int ExpectedProfileVersion,
    AssetProcessIntelligenceAnalyticsWindow? ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow? ComparisonWindow = null);

public sealed record AssetProcessIntelligenceContextEligibilityV1SignalWindow(
    AssetProcessIntelligenceAnalyticsWindow Window,
    int RawObservationCount,
    int GoodFiniteNumericCount,
    int AcceptedSynchronizedUniqueCount,
    int AmbiguousSourceTimestampGroupCount,
    int AmbiguousSourceTimestampRowCount,
    int PredicateMatchCount,
    int ContextEligibleObservationCount,
    IReadOnlyDictionary<string, int> RejectionCounts,
    string Status);

public sealed record AssetProcessIntelligenceContextEligibilityV1Signal(
    Guid SignalId,
    string SignalKey,
    bool Required,
    Guid TagId,
    string TagPath,
    TagDataType TagDataType,
    string? EngineeringUnit,
    AssetProcessIntelligenceContextEligibilityV1SignalWindow Reference,
    AssetProcessIntelligenceContextEligibilityV1SignalWindow? Comparison);

public sealed record AssetProcessIntelligenceContextEligibilityV1Predicate(
    Guid TagId,
    AssetProcessIntelligenceOperatingContextPredicateKind Kind,
    int ReferenceAcceptedCount,
    int ReferenceMatchCount,
    int? ComparisonAcceptedCount,
    int? ComparisonMatchCount,
    string Status);

public sealed record AssetProcessIntelligenceContextEligibilityV1Response(
    int ContractVersion,
    int CatalogVersion,
    string RuleId,
    string RuleVersion,
    string ProjectKey,
    long ActiveRevision,
    Guid AssignmentId,
    Guid EquipmentId,
    Guid MeasurementProfileId,
    string MeasurementProfileKey,
    Guid OperatingContextProfileId,
    string OperatingContextProfileKey,
    string DisplayLabel,
    int ProfileVersion,
    AssetProcessIntelligenceAnalyticsWindow ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow? ComparisonWindow,
    IReadOnlyList<AssetProcessIntelligenceContextEligibilityV1Predicate> Predicates,
    IReadOnlyList<AssetProcessIntelligenceContextEligibilityV1Signal> Signals,
    int TotalRawObservationCount,
    string Interpretation);

public static class AssetProcessIntelligenceContextEligibilityV1Catalog
{
    public const int ContractVersion = 1;
    public const int CatalogVersion = 1;
    public const string RuleId = "context-eligibility.v1";
    public const string RuleVersion = "1.0.0";
    public const int MaximumSignals = 16;
    public const int MaximumPredicates = 16;
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
        maximumSignals = MaximumSignals,
        maximumPredicates = MaximumPredicates,
        maximumWindowDays = (int)MaximumWindow.TotalDays,
        maximumRawRowsAcrossAllSignalsAndWindows = MaximumRawRowsAcrossQuery,
        pageSize = QueryPageSize,
        sourceTimestampRule = "exact unique UTC source timestamps with explicit Synchronized evidence; no nearest join, hold, interpolation or imputation",
        predicateRules = new[] { "inclusive finite numeric interval in the current Engineering unit", "exact Boolean equality", "exact ordinal String/Enum equality" },
        interpretation = "matches only the operator-declared context profile version; does not assert physical regime equivalence, health, sufficiency, score or model validity"
    };
}

internal sealed record AssetProcessIntelligenceContextEligibilityV1WindowCalculation(
    IReadOnlyDictionary<Guid, AssetProcessIntelligenceContextEligibilityV1SignalWindow> Signals,
    IReadOnlyDictionary<Guid, int> PredicateAccepted,
    IReadOnlyDictionary<Guid, int> PredicateMatched);

internal static class AssetProcessIntelligenceContextEligibilityV1Calculator
{
    public static AssetProcessIntelligenceContextEligibilityV1WindowCalculation Calculate(
        IReadOnlyList<(AssetProcessIntelligenceSignalEngineeringDto Signal, TagDefinition Tag)> signals,
        IReadOnlyList<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto> predicates,
        AssetProcessIntelligenceAnalyticsWindow window,
        IReadOnlyDictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>> rowsBySignal)
    {
        var acceptedByTag = new Dictionary<Guid, Dictionary<DateTimeOffset, HistoricalQueryValue>>();
        var result = new Dictionary<Guid, AssetProcessIntelligenceContextEligibilityV1SignalWindow>();
        var predicateAccepted = new Dictionary<Guid, int>();
        var predicateMatched = new Dictionary<Guid, int>();

        foreach (var (signal, tag) in signals)
        {
            var rows = rowsBySignal.GetValueOrDefault(signal.Id) ?? Array.Empty<AssetProcessIntelligenceContextualDeviationVectorRawObservation>();
            var rejections = new Dictionary<string, int>(StringComparer.Ordinal);
            static void Count(Dictionary<string, int> values, string key, int amount = 1) =>
                values[key] = values.GetValueOrDefault(key) + amount;

            var timestampGroups = rows
                .Where(row => row.SourceTimestampUtc.HasValue && row.SourceTimestampUtc.Value.Offset == TimeSpan.Zero)
                .GroupBy(row => row.SourceTimestampUtc!.Value)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var ambiguousGroups = timestampGroups.Count(group => group.Value.Length > 1);
            var ambiguousRows = timestampGroups.Where(group => group.Value.Length > 1).Sum(group => group.Value.Length);
            var accepted = new Dictionary<DateTimeOffset, HistoricalQueryValue>();
            var goodFiniteNumeric = 0;
            var predicateMatchCount = 0;
            foreach (var row in rows)
            {
                if (!string.Equals(AssetProcessIntelligenceKpiV1.NormalizeQuality(row.Quality), TagQuality.Good.ToString(), StringComparison.Ordinal))
                {
                    Count(rejections, "poor_or_missing_quality");
                    continue;
                }
                if (IsNumeric(tag.DataType) && TryFiniteNumeric(row.Value, out _)) goodFiniteNumeric++;
                if (!row.SourceTimestampUtc.HasValue)
                {
                    Count(rejections, "source_timestamp_missing");
                    continue;
                }
                if (row.SourceTimestampUtc.Value.Offset != TimeSpan.Zero)
                {
                    Count(rejections, "source_timestamp_not_utc");
                    continue;
                }
                if (!string.Equals(row.SourceTimestampEvidence, TimestampEvidenceState.Synchronized.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    Count(rejections, "source_timestamp_not_synchronized");
                    continue;
                }
                if (row.SourceTimestampUtc.Value < window.FromUtc || row.SourceTimestampUtc.Value > window.ToUtc)
                {
                    Count(rejections, "source_timestamp_outside_window");
                    continue;
                }
                if (!timestampGroups.TryGetValue(row.SourceTimestampUtc.Value, out var sameTimestampRows) || sameTimestampRows.Length != 1)
                {
                    Count(rejections, "duplicate_source_timestamp");
                    continue;
                }
                if (row.Value is null || row.Value.Kind == HistoricalValueKind.Null)
                {
                    Count(rejections, "value_missing");
                    continue;
                }
                if (!SupportsTagValue(tag.DataType, row.Value))
                {
                    Count(rejections, "value_type_mismatch_or_non_finite");
                    continue;
                }
                accepted[row.SourceTimestampUtc.Value] = row.Value;
            }
            acceptedByTag[tag.Id] = accepted;

            var predicate = predicates.FirstOrDefault(item => item.TagId == tag.Id);
            if (predicate is not null)
            {
                var matches = accepted.Count(item => Matches(predicate, tag.DataType, item.Value));
                predicateAccepted[predicate.TagId] = accepted.Count;
                predicateMatched[predicate.TagId] = matches;
                predicateMatchCount = matches;
            }
            result[signal.Id] = new AssetProcessIntelligenceContextEligibilityV1SignalWindow(
                window,
                rows.Count,
                goodFiniteNumeric,
                accepted.Count,
                ambiguousGroups,
                ambiguousRows,
                predicateMatchCount,
                0,
                rejections,
                accepted.Count == 0 ? "insufficient_evidence" : "evidence_available");
        }

        foreach (var (signal, tag) in signals)
        {
            var rows = rowsBySignal.GetValueOrDefault(signal.Id) ?? Array.Empty<AssetProcessIntelligenceContextualDeviationVectorRawObservation>();
            var timestampGroups = rows
                .Where(row => row.SourceTimestampUtc.HasValue && row.SourceTimestampUtc.Value.Offset == TimeSpan.Zero)
                .GroupBy(row => row.SourceTimestampUtc!.Value)
                .Where(group => group.Count() == 1)
                .Select(group => group.Key)
                .ToHashSet();
            var targetValues = acceptedByTag.GetValueOrDefault(tag.Id) ?? new Dictionary<DateTimeOffset, HistoricalQueryValue>();
            var eligible = targetValues.Count(item => timestampGroups.Contains(item.Key) &&
                IsNumeric(tag.DataType) && TryFiniteNumeric(item.Value, out _) &&
                predicates.All(predicate =>
                {
                    var predicateValues = acceptedByTag.GetValueOrDefault(predicate.TagId);
                    return predicateValues is not null && predicateValues.TryGetValue(item.Key, out var predicateValue) &&
                        signals.FirstOrDefault(candidate => candidate.Tag.Id == predicate.TagId) is { Tag: { } predicateTag } &&
                        Matches(predicate, predicateTag.DataType, predicateValue);
                }));
            result[signal.Id] = result[signal.Id] with
            {
                ContextEligibleObservationCount = eligible,
                Status = eligible > 0 ? "context_match_available" :
                    targetValues.Count == 0 ? "insufficient_target_evidence" :
                    predicates.Any(predicate => predicateAccepted.GetValueOrDefault(predicate.TagId) == 0)
                        ? "insufficient_predicate_evidence" :
                    predicates.Any(predicate => predicateMatched.GetValueOrDefault(predicate.TagId) == 0)
                        ? "predicate_mismatch" : "no_exact_context_timestamp_match"
            };
        }

        return new(result, predicateAccepted, predicateMatched);
    }

    private static bool Matches(
        AssetProcessIntelligenceOperatingContextPredicateEngineeringDto predicate,
        TagDataType dataType,
        HistoricalQueryValue value)
    {
        return predicate.Kind switch
        {
            AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval =>
                IsNumeric(dataType) && TryFiniteNumeric(value, out var number) &&
                (!predicate.Minimum.HasValue || number >= predicate.Minimum.Value) &&
                (!predicate.Maximum.HasValue || number <= predicate.Maximum.Value),
            AssetProcessIntelligenceOperatingContextPredicateKind.BooleanEquals =>
                dataType == TagDataType.Boolean && value.Kind == HistoricalValueKind.Boolean &&
                bool.TryParse(value.Value, out var boolean) && boolean == predicate.BooleanValue,
            AssetProcessIntelligenceOperatingContextPredicateKind.StringEquals =>
                dataType == TagDataType.String && value.Kind == HistoricalValueKind.String &&
                string.Equals(value.Value, predicate.ExactValue, StringComparison.Ordinal),
            AssetProcessIntelligenceOperatingContextPredicateKind.EnumEquals =>
                dataType == TagDataType.Enum && value.Kind is HistoricalValueKind.Enum or HistoricalValueKind.String &&
                string.Equals(value.Value, predicate.ExactValue, StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsNumeric(TagDataType type) => type is
        TagDataType.Int16 or TagDataType.Int32 or TagDataType.Int64 or TagDataType.Float or TagDataType.Double;

    private static bool TryFiniteNumeric(HistoricalQueryValue? value, out double number)
    {
        number = 0;
        return value is not null && value.Kind is (HistoricalValueKind.Int16 or HistoricalValueKind.Int32 or
            HistoricalValueKind.Int64 or HistoricalValueKind.Float or HistoricalValueKind.Double or HistoricalValueKind.Number) &&
            double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    }

    private static bool SupportsTagValue(TagDataType type, HistoricalQueryValue value) => type switch
    {
        TagDataType.Boolean => value.Kind == HistoricalValueKind.Boolean,
        TagDataType.String => value.Kind == HistoricalValueKind.String,
        TagDataType.Enum => value.Kind is HistoricalValueKind.Enum or HistoricalValueKind.String,
        TagDataType.Int16 or TagDataType.Int32 or TagDataType.Int64 or TagDataType.Float or TagDataType.Double => TryFiniteNumeric(value, out _),
        _ => false
    };
}
