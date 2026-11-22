using System.Collections.Frozen;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligenceAnalyticsWindow(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc);

public sealed record AssetProcessIntelligenceAnalyticsQueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid TagId,
    AssetProcessIntelligenceAnalyticsWindow? Window,
    AssetProcessIntelligenceAnalyticsWindow? ComparisonWindow = null,
    int BucketSeconds = AssetProcessIntelligenceKpiCatalogV1.DefaultBucketSeconds);

public sealed record AssetProcessIntelligenceKpiRule(
    string Id,
    string Version,
    string Label,
    string Kind,
    string Formula,
    string UnitSemantics);

public static class AssetProcessIntelligenceKpiCatalogV1
{
    public const int CatalogVersion = 1;
    public const string RuleVersion = "1.0.0";
    public const int DefaultBucketSeconds = 900;
    public const int MaximumBucketCount = 3000;
    public const int MaximumRowsPerWindow = 50000;
    public const int QueryPageSize = 200;
    public static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(31);

    private static readonly IReadOnlyList<AssetProcessIntelligenceKpiRule> Catalog =
        Array.AsReadOnly<AssetProcessIntelligenceKpiRule>(
        [
            new("raw.observation.count.v1", RuleVersion, "Historian observations", "raw", "Rows returned by historian.samples for the exact TAG and window.", "count"),
            new("sample.count.good.v1", RuleVersion, "Valid sample count", "derived", "Count finite numeric observations whose quality is Good.", "count"),
            new("sample.minimum.good.v1", RuleVersion, "Minimum", "derived", "Minimum accepted Good numeric sample.", "active TAG engineering unit"),
            new("sample.maximum.good.v1", RuleVersion, "Maximum", "derived", "Maximum accepted Good numeric sample.", "active TAG engineering unit"),
            new("sample.mean.good.v1", RuleVersion, "Sample mean", "derived", "Arithmetic mean of accepted Good numeric samples; not time-weighted.", "active TAG engineering unit"),
            new("sample.first.good.v1", RuleVersion, "First observed", "derived", "Accepted Good numeric sample with the earliest Historian timestamp; timestamp ties retain the ascending Historian cursor order.", "active TAG engineering unit"),
            new("sample.last.good.v1", RuleVersion, "Last observed", "derived", "Accepted Good numeric sample with the latest Historian timestamp; timestamp ties retain the ascending Historian cursor order.", "active TAG engineering unit"),
            new("sample.delta.good.v1", RuleVersion, "Delta", "derived", "Last observed value minus first observed value in this window.", "active TAG engineering unit"),
            new("sample.bucket-trend.good.v1", RuleVersion, "Time-bucket trend", "derived", "Per-bucket accepted sample count, minimum, maximum and arithmetic sample mean; empty buckets remain visible.", "active TAG engineering unit")
        ]);

    private static readonly FrozenSet<int> BucketSizes = new[] { 900, 1800, 3600, 21600, 86400 }.ToFrozenSet();

    public static IReadOnlyList<AssetProcessIntelligenceKpiRule> Rules => Catalog;
    public static IReadOnlySet<int> AllowedBucketSizesSeconds => BucketSizes;

    public static object Describe() => new
    {
        catalogVersion = CatalogVersion,
        ruleVersion = RuleVersion,
        numericTagTypes = new[]
        {
            TagDataType.Int16.ToString(), TagDataType.Int32.ToString(), TagDataType.Int64.ToString(),
            TagDataType.Float.ToString(), TagDataType.Double.ToString()
        },
        acceptedQuality = TagQuality.Good.ToString(),
        maximumWindowDays = 31,
        maximumRowsPerWindow = MaximumRowsPerWindow,
        pageSize = QueryPageSize,
        bucketSizesSeconds = BucketSizes.Order().ToArray(),
        completeness = "unknown",
        rules = Catalog
    };
}

public sealed record AssetProcessIntelligenceRawObservation(
    DateTimeOffset TimestampUtc,
    string? Quality,
    HistoricalQueryValue? Value);

public sealed record AssetProcessIntelligenceObservedValue(
    DateTimeOffset TimestampUtc,
    double Value,
    string RuleId);

public sealed record AssetProcessIntelligenceKpiBucket(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int RawObservationCount,
    int AcceptedSampleCount,
    int RejectedSampleCount,
    double? Minimum,
    double? Maximum,
    double? SampleMean);

public sealed record AssetProcessIntelligenceKpiSummary(
    int RawObservationCount,
    int AcceptedSampleCount,
    int RejectedSampleCount,
    IReadOnlyDictionary<string, int> QualityCounts,
    int? ValidSampleCount,
    double? Minimum,
    double? Maximum,
    double? SampleMean,
    AssetProcessIntelligenceObservedValue? FirstObserved,
    AssetProcessIntelligenceObservedValue? LastObserved,
    double? Delta,
    IReadOnlyList<AssetProcessIntelligenceKpiBucket> Trend,
    string Completeness,
    int? ExpectedSampleCount);

public sealed record AssetProcessIntelligenceAssignmentProvenance(
    Guid AssignmentId,
    Guid EquipmentId,
    Guid ProfileId,
    Guid SignalId,
    string SignalKey);

public sealed record AssetProcessIntelligenceAnalyticsProvenance(
    string Dataset,
    int DatasetContractVersion,
    string ObservationDescription,
    string RuleVersion,
    IReadOnlyList<AssetProcessIntelligenceAssignmentProvenance> Assignments,
    string RegimeEquivalence);

public sealed record AssetProcessIntelligenceAnalyticsPeriodResult(
    AssetProcessIntelligenceAnalyticsWindow Window,
    AssetProcessIntelligenceKpiSummary Summary);

public sealed record AssetProcessIntelligenceAnalyticsComparison(
    AssetProcessIntelligenceAnalyticsPeriodResult Period,
    int? ValidSampleCountDelta,
    double? MinimumDelta,
    double? MaximumDelta,
    double? SampleMeanDelta,
    double? FirstObservedDelta,
    double? LastObservedDelta,
    double? DeltaDelta,
    string RegimeEquivalence);

public sealed record AssetProcessIntelligenceAnalyticsResponse(
    int ContractVersion,
    int CatalogVersion,
    string RuleVersion,
    string ProjectKey,
    long ActiveRevision,
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid TagId,
    string TagPath,
    TagDataType TagDataType,
    string? Unit,
    AssetProcessIntelligenceAnalyticsPeriodResult Period,
    AssetProcessIntelligenceAnalyticsComparison? Comparison,
    AssetProcessIntelligenceAnalyticsProvenance Provenance);

public sealed record AssetProcessIntelligenceAnalyticsApiError(string Code, string Error);

public sealed record AssetProcessIntelligenceProfileCoverageQueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid ProfileId,
    AssetProcessIntelligenceAnalyticsWindow? Window,
    int BucketSeconds = AssetProcessIntelligenceKpiCatalogV1.DefaultBucketSeconds);

public sealed record AssetProcessIntelligenceProfileSignalCoverage(
    Guid SignalId,
    string SignalKey,
    string SignalRoleMetadata,
    bool Required,
    Guid TagId,
    string TagPath,
    TagDataType TagDataType,
    string? Unit,
    string Status,
    AssetProcessIntelligenceKpiSummary? Summary,
    IReadOnlyList<AssetProcessIntelligenceAssignmentProvenance> Assignments);

public sealed record AssetProcessIntelligenceProfileTemporalAlignment(
    int RequiredSignalCount,
    int RequiredSignalsWithGoodSamples,
    int ExactHistorianTimestampIntersectionCount,
    string Status,
    string TimestampBasis,
    string SourceAcquisitionAlignment,
    string ExpectedCadence,
    string Completeness);

public sealed record AssetProcessIntelligenceProfileCoverageProvenance(
    string Dataset,
    int DatasetContractVersion,
    string RuleVersion,
    IReadOnlyList<AssetProcessIntelligenceAssignmentProvenance> Assignments,
    string SignalRoleInterpretation,
    string UnitConversion,
    string ProcessKpiDisposition);

public sealed record AssetProcessIntelligenceProfileCoverageResponse(
    int ContractVersion,
    int RuleCatalogVersion,
    string RuleVersion,
    string ProjectKey,
    long ActiveRevision,
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid ProfileId,
    string ProfileKey,
    string ProfileName,
    AssetProcessIntelligenceAnalyticsWindow Window,
    IReadOnlyList<AssetProcessIntelligenceProfileSignalCoverage> Signals,
    AssetProcessIntelligenceProfileTemporalAlignment TemporalAlignment,
    AssetProcessIntelligenceProfileCoverageProvenance Provenance);

public static class AssetProcessIntelligenceProfileCoverageCatalogV1
{
    public const int CatalogVersion = 1;
    public const string RuleVersion = "1.0.0";
    public const int MaximumSignals = 16;
    public const int MaximumRowsPerWindow = 50000;

    public static object Describe() => new
    {
        catalogVersion = CatalogVersion,
        ruleVersion = RuleVersion,
        maximumSignals = MaximumSignals,
        maximumRowsPerWindow = MaximumRowsPerWindow,
        maximumWindowDays = 31,
        rules = new[]
        {
            new
            {
                id = "profile.required-signals.exact-historian-timestamp-overlap.v1",
                version = RuleVersion,
                kind = "coverage",
                formula = "Count distinct timestamps present as finite-numeric Good samples for every required numeric signal; exact Historian event-time equality only.",
                unitSemantics = "distinct Historian timestamps; not a physical process unit",
                completeness = "unknown_without_expected_cadence",
                sourceAcquisitionAlignment = "unverified"
            }
        },
        signalRoleInterpretation = "metadata_only",
        unitConversion = "none",
        processKpiDisposition = "unsupported_without_a_canonical_semantic_and_unit-dimension-contract"
    };
}
