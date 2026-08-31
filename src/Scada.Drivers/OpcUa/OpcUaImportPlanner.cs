using System.Security.Cryptography;
using System.Text;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Offline evidence collected from an OPC UA Variable node. This record remains
/// independent of the OPC Foundation SDK so preview/apply logic can be tested
/// without a live server.
/// </summary>
public sealed record OpcUaVariableImportEvidence(
    string NodeId,
    string? NamespaceUri,
    string BrowseName,
    string DisplayName,
    OpcUaBuiltInDataType BuiltInDataType,
    int ValueRank,
    bool IsReadable,
    bool IsWritable,
    string? EngineeringUnit = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record OpcUaImportPlan(
    IReadOnlyCollection<DriverImportCandidate> Candidates,
    int ExaminedCount,
    int DuplicateCount,
    int UnreadableCount,
    int UnsupportedCount,
    bool IsTruncated,
    IReadOnlyCollection<DriverEngineeringIssue> Issues);

/// <summary>
/// Converts bounded OPC UA browse evidence into deterministic Engineering import
/// candidates. It never mutates Engineering and never activates a runtime
/// session. Apply remains the responsibility of the common preview/apply path.
/// </summary>
public static class OpcUaImportPlanner
{
    public const int DefaultMaximumCandidates = 5000;
    public const int HardMaximumCandidates = 5000;

    public static OpcUaImportPlan Build(
        IEnumerable<OpcUaVariableImportEvidence> evidence,
        int maximumCandidates = DefaultMaximumCandidates)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (maximumCandidates <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCandidates), "Maximum candidates must be greater than zero.");
        }

        var examinationLimit = Math.Min(maximumCandidates, HardMaximumCandidates);
        var candidates = new List<DriverImportCandidate>();
        var issues = new List<DriverEngineeringIssue>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var examined = 0;
        var duplicates = 0;
        var unreadable = 0;
        var unsupported = 0;
        var truncated = false;

        using var enumerator = evidence.GetEnumerator();
        while (examined < examinationLimit && enumerator.MoveNext())
        {
            examined++;
            var item = enumerator.Current;

            if (!item.IsReadable)
            {
                unreadable++;
                continue;
            }

            var identity = new OpcUaNodeIdentity(item.NodeId, item.NamespaceUri);
            if (!identities.Add(identity.StableIdentity))
            {
                duplicates++;
                continue;
            }

            var mapping = OpcUaDataTypeMapper.Map(item.BuiltInDataType, item.ValueRank);
            if (!mapping.Supported)
            {
                unsupported++;
                continue;
            }

            var metadata = BuildMetadata(item);
            var candidateIssues = mapping.RequiresAdaptation
                ? new[]
                {
                    new DriverEngineeringIssue(
                        "OPCUA_TYPE_ADAPTATION",
                        DriverEngineeringIssueSeverity.Information,
                        mapping.Reason ?? "OPC UA value requires a canonical TAG adaptation.")
                }
                : Array.Empty<DriverEngineeringIssue>();

            candidates.Add(new DriverImportCandidate(
                CandidateId: CreateCandidateId(identity.StableIdentity),
                StableIdentity: identity.StableIdentity,
                DisplayName: GetDisplayName(item),
                PortableAddress: identity.PortableAddress,
                IsReadable: true,
                IsWritable: item.IsWritable,
                SuggestedDataType: mapping.DataType,
                EngineeringUnit: item.EngineeringUnit,
                Metadata: metadata,
                Issues: candidateIssues));
        }

        if (examined == examinationLimit && enumerator.MoveNext())
        {
            truncated = true;
            issues.Add(new DriverEngineeringIssue(
                "OPCUA_IMPORT_TRUNCATED",
                DriverEngineeringIssueSeverity.Warning,
                $"OPC UA import evidence was truncated after {examinationLimit} nodes. Narrow the browse scope or continue explicitly."));
        }

        return new OpcUaImportPlan(
            candidates,
            examined,
            duplicates,
            unreadable,
            unsupported,
            truncated,
            issues);
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(OpcUaVariableImportEvidence item)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (item.Metadata is not null)
        {
            foreach (var pair in item.Metadata)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

        metadata["opcUa.nodeId"] = item.NodeId;
        metadata["opcUa.browseName"] = item.BrowseName;
        metadata["opcUa.builtInDataType"] = item.BuiltInDataType.ToString();
        metadata["opcUa.valueRank"] = item.ValueRank.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (!string.IsNullOrWhiteSpace(item.NamespaceUri))
        {
            metadata["opcUa.namespaceUri"] = item.NamespaceUri;
        }

        return metadata;
    }

    private static string GetDisplayName(OpcUaVariableImportEvidence item) =>
        !string.IsNullOrWhiteSpace(item.DisplayName)
            ? item.DisplayName
            : !string.IsNullOrWhiteSpace(item.BrowseName)
                ? item.BrowseName
                : item.NodeId;

    private static string CreateCandidateId(string stableIdentity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stableIdentity));
        return $"opcua-{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
