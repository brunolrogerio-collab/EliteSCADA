using System.Collections.ObjectModel;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligenceNetObservedRateV1QueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid TagId,
    AssetProcessIntelligenceAnalyticsWindow? Window);

public sealed record AssetProcessIntelligenceNetObservedRateV1Endpoint(
    DateTimeOffset TimestampUtc,
    double Value);

public sealed record AssetProcessIntelligenceNetObservedRateV1Metric(
    string Status,
    IReadOnlyList<string> ReasonCodes,
    double? Value,
    string? Unit,
    double? ElapsedSeconds,
    int RawObservationCount,
    int AcceptedSampleCount,
    int RejectedSampleCount,
    IReadOnlyDictionary<string, int> QualityCounts,
    AssetProcessIntelligenceNetObservedRateV1Endpoint? FirstAccepted,
    AssetProcessIntelligenceNetObservedRateV1Endpoint? LastAccepted);

public sealed record AssetProcessIntelligenceNetObservedRateV1Provenance(
    string Dataset,
    int DatasetContractVersion,
    string TimestampBasis,
    string QualityAcceptance);

public sealed record AssetProcessIntelligenceNetObservedRateV1Response(
    int ContractVersion,
    int CatalogVersion,
    string RuleId,
    string RuleVersion,
    string ProjectKey,
    long ActiveRevision,
    AssetProcessIntelligenceAnalyticsWindow Window,
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid TagId,
    string TagPath,
    TagDataType TagDataType,
    AssetProcessIntelligenceNetObservedRateV1Metric Metric,
    AssetProcessIntelligenceNetObservedRateV1Provenance Provenance);

public static class AssetProcessIntelligenceNetObservedRateV1Catalog
{
    public const int ContractVersion = 1;
    public const int CatalogVersion = 1;
    public const string RuleId = "net-observed-rate.v1";
    public const string RuleVersion = "1.0.0";
    public const int MaximumRowsPerWindow = AssetProcessIntelligenceKpiCatalogV1.MaximumRowsPerWindow;
    public const int QueryPageSize = AssetProcessIntelligenceKpiCatalogV1.QueryPageSize;
    public static readonly TimeSpan MaximumWindow = AssetProcessIntelligenceKpiCatalogV1.MaximumWindow;

    private static readonly IReadOnlyList<AssetProcessIntelligenceKpiRule> Catalog =
        Array.AsReadOnly<AssetProcessIntelligenceKpiRule>(
        [
            new(
                RuleId,
                RuleVersion,
                "Net observed rate",
                "derived",
                "(last accepted Good finite numeric observation - first accepted Good finite numeric observation) / elapsed seconds between their Historian observation timestamps",
                "Existing TAG Engineering unit per second; preserve the free-text unit without inference, conversion, or normalization.")
        ]);

    public static IReadOnlyList<AssetProcessIntelligenceKpiRule> Rules => Catalog;

    public static object Describe() => new
    {
        contractVersion = ContractVersion,
        catalogVersion = CatalogVersion,
        ruleId = RuleId,
        ruleVersion = RuleVersion,
        acceptedQuality = TagQuality.Good.ToString(),
        timestampBasis = "Historian observation/publication timestamp (`timestamp`); source/server timestamps are not used.",
        maximumWindowDays = (int)MaximumWindow.TotalDays,
        maximumRowsPerWindow = MaximumRowsPerWindow,
        pageSize = QueryPageSize,
        statuses = new[]
        {
            AssetProcessIntelligenceNetObservedRateV1Calculator.AvailableStatus,
            AssetProcessIntelligenceNetObservedRateV1Calculator.InsufficientDataStatus,
            AssetProcessIntelligenceNetObservedRateV1Calculator.UnsupportedStatus
        },
        reasonCodes = new[]
        {
            AssetProcessIntelligenceNetObservedRateV1Calculator.FewerThanTwoSamplesReason,
            AssetProcessIntelligenceNetObservedRateV1Calculator.NonPositiveElapsedTimeReason,
            AssetProcessIntelligenceNetObservedRateV1Calculator.MissingEngineeringUnitReason,
            AssetProcessIntelligenceNetObservedRateV1Calculator.NonFiniteRateReason
        },
        rules = Catalog
    };
}

public static class AssetProcessIntelligenceNetObservedRateV1Calculator
{
    public const string AvailableStatus = "available";
    public const string InsufficientDataStatus = "insufficient_data";
    public const string UnsupportedStatus = "unsupported";
    public const string FewerThanTwoSamplesReason = "fewer_than_two_accepted_good_finite_samples";
    public const string NonPositiveElapsedTimeReason = "non_positive_endpoint_elapsed_seconds";
    public const string MissingEngineeringUnitReason = "engineering_unit_missing";
    public const string NonFiniteRateReason = "non_finite_calculated_rate";

    public static AssetProcessIntelligenceNetObservedRateV1Metric Calculate(
        IReadOnlyList<AssetProcessIntelligenceRawObservation> observations,
        string? engineeringUnit)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var qualityCounts = Enum.GetValues<TagQuality>()
            .ToDictionary(static quality => quality.ToString(), static _ => 0, StringComparer.Ordinal);
        qualityCounts.Add("Missing", 0);
        qualityCounts.Add("Unrecognized", 0);

        var indexedObservations = observations
            .Select((observation, index) => (observation, index))
            .OrderBy(item => item.observation.TimestampUtc)
            .ThenBy(item => item.index);
        var acceptedCount = 0;
        AssetProcessIntelligenceNetObservedRateV1Endpoint? first = null;
        AssetProcessIntelligenceNetObservedRateV1Endpoint? last = null;

        foreach (var (observation, _) in indexedObservations)
        {
            qualityCounts[AssetProcessIntelligenceKpiV1.NormalizeQuality(observation.Quality)]++;
            if (!AssetProcessIntelligenceKpiV1.TryGetAcceptedGoodNumericValue(observation, out var value))
                continue;

            acceptedCount++;
            var endpoint = new AssetProcessIntelligenceNetObservedRateV1Endpoint(
                observation.TimestampUtc,
                value);
            first ??= endpoint;
            last = endpoint;
        }

        return CalculateFromEndpoints(
            observations.Count,
            acceptedCount,
            qualityCounts,
            first,
            last,
            engineeringUnit);
    }

    internal static AssetProcessIntelligenceNetObservedRateV1Metric CalculateFromEndpoints(
        int rawObservationCount,
        int acceptedSampleCount,
        IReadOnlyDictionary<string, int> qualityCounts,
        AssetProcessIntelligenceNetObservedRateV1Endpoint? first,
        AssetProcessIntelligenceNetObservedRateV1Endpoint? last,
        string? engineeringUnit)
    {
        ArgumentNullException.ThrowIfNull(qualityCounts);
        if (rawObservationCount < 0 || acceptedSampleCount < 0 || acceptedSampleCount > rawObservationCount)
            throw new ArgumentOutOfRangeException(nameof(acceptedSampleCount));

        var reasonCodes = new List<string>();
        var elapsedSeconds = first is not null && last is not null
            ? (last.TimestampUtc - first.TimestampUtc).TotalSeconds
            : (double?)null;
        if (acceptedSampleCount < 2 || first is null || last is null)
            reasonCodes.Add(FewerThanTwoSamplesReason);
        if (elapsedSeconds is <= 0)
            reasonCodes.Add(NonPositiveElapsedTimeReason);

        var unit = string.IsNullOrWhiteSpace(engineeringUnit)
            ? null
            : $"{engineeringUnit}/s";
        if (unit is null)
            reasonCodes.Add(MissingEngineeringUnitReason);

        double? rate = null;
        if (reasonCodes.Count == 0 && first is not null && last is not null && elapsedSeconds is > 0)
        {
            var candidate = (last.Value - first.Value) / elapsedSeconds.Value;
            if (double.IsFinite(candidate))
                rate = candidate;
            else
                reasonCodes.Add(NonFiniteRateReason);
        }

        var status = reasonCodes.Count == 0
            ? AvailableStatus
            : reasonCodes.Contains(MissingEngineeringUnitReason, StringComparer.Ordinal)
                ? UnsupportedStatus
                : InsufficientDataStatus;
        return new AssetProcessIntelligenceNetObservedRateV1Metric(
            status,
            Array.AsReadOnly(reasonCodes.ToArray()),
            rate,
            unit,
            elapsedSeconds,
            rawObservationCount,
            acceptedSampleCount,
            rawObservationCount - acceptedSampleCount,
            new ReadOnlyDictionary<string, int>(qualityCounts.ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.Ordinal)),
            first,
            last);
    }
}
