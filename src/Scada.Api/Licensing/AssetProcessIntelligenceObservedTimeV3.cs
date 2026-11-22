using System.Text.Json;
using System.Text.Json.Nodes;

namespace Scada.Api.Licensing;

/// <summary>Separate wire contract: observation time is never labeled as device/source time.</summary>
public static class AssetProcessIntelligenceObservedTimeV3
{
    public const string TimestampBasis = "historian_observed";
    public const string RuleId = "contextual-deviation-observed.v3";
    public const string RuleVersion = "3.0.0";

    public static JsonObject Catalog()
    {
        var catalog = JsonSerializer.SerializeToNode(AssetProcessIntelligenceContextualDeviationVectorV2Catalog.Describe())!.AsObject();
        catalog["contractVersion"] = 3;
        catalog["catalogVersion"] = 3;
        catalog["ruleId"] = RuleId;
        catalog["ruleVersion"] = RuleVersion;
        catalog["timestampBasis"] = TimestampBasis;
        catalog["optionalTemporalAlignment"] = "temporalToleranceSeconds 1..60: nearest unused Historian observation times to first target, reject ties and whole-vector spans above tolerance; no cross-window matching or interpolation";
        catalog["vectorTimestamp"] = "Historian observation timestamp of the first selected target";
        catalog["ordering"] = "targets and contexts follow caller selection order; evaluation vectors use Historian observation time ascending";
        catalog["evidence"] = "accepted observations require Good finite numeric values and valid UTC Historian observation timestamps; rejected raw rows remain visible in evidence counts";
        catalog["alignment"] = "nearest unused Historian observation timestamps within whole-vector tolerance; reject ties; no interpolation, imputation, or device-time synchronization claim";
        return catalog;
    }

    public static JsonObject Project(AssetProcessIntelligenceContextualDeviationVectorV2Response report)
    {
        var result = JsonSerializer.SerializeToNode(report, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        Rename(result);
        result["contractVersion"] = 3;
        result["catalogVersion"] = 3;
        result["ruleId"] = RuleId;
        result["ruleVersion"] = RuleVersion;
        result["timestampBasis"] = TimestampBasis;
        var provenance = result["provenance"]!.AsObject();
        provenance["queryWindowTimestampBasis"] = "Historian observation timestamp";
        provenance["vectorTimestampBasis"] = "Historian observation time; nearest unused readings to first target within whole-vector tolerance; no device acquisition-time or synchronization claim.";
        provenance["timestampEvidenceRequirement"] = "Good finite numeric observations with valid UTC observation times inside the selected window; duplicate observation times rejected.";
        return result;
    }

    private static void Rename(JsonNode? node)
    {
        if (node is JsonArray array) { foreach (var child in array) Rename(child); }
        if (node is not JsonObject obj) return;
        foreach (var item in obj.ToArray())
        {
            Rename(item.Value);
            if (item.Key == "rejectionCounts" && item.Value is JsonObject rejectionCounts)
                RenameRejectionKeys(rejectionCounts);
            var key = item.Key switch
            {
                "sourceTimestampUtc" => "anchorObservedTimestampUtc",
                "matchedSourceTimestampsUtc" => "matchedObservedTimestampsUtc",
                "sourceTimestampEvidenceRequirement" => "timestampEvidenceRequirement",
                "acceptedSynchronizedCount" => "acceptedObservationCount",
                "ambiguousSourceTimestampGroupCount" => "ambiguousObservedTimestampGroupCount",
                "ambiguousSourceTimestampRowCount" => "ambiguousObservedTimestampRowCount",
                _ => item.Key
            };
            if (key == item.Key) continue;
            var value = item.Value?.DeepClone();
            obj.Remove(item.Key);
            obj[key] = value;
        }
    }

    private static void RenameRejectionKeys(JsonObject rejectionCounts)
    {
        foreach (var item in rejectionCounts.ToArray())
        {
            var key = item.Key switch
            {
                "source_timestamp_missing" => "timestamp_missing",
                "source_timestamp_evidence_unrecognized" => "timestamp_evidence_unrecognized",
                "source_timestamp_not_synchronized" => "timestamp_not_synchronized",
                "source_timestamp_outside_requested_window" => "observed_timestamp_outside_requested_window",
                "duplicate_source_timestamp" => "duplicate_observed_timestamp",
                _ => item.Key
            };
            if (key == item.Key) continue;
            var value = item.Value?.DeepClone();
            rejectionCounts.Remove(item.Key);
            rejectionCounts[key] = value;
        }
    }
}
