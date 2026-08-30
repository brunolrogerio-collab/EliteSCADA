using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.SiemensS7Iso;

internal static partial class S7TiaImportValidation
{
    public static DriverImportCandidate ValidateAddressWidth(DriverImportCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        candidate = NormalizeStableIdentity(candidate);

        if (!TryParseCandidateBinding(candidate.PortableAddress, out var binding))
            return candidate;
        if (candidate.Metadata is null ||
            !candidate.Metadata.TryGetValue("logicalAddress", out var logicalAddress) ||
            string.IsNullOrWhiteSpace(logicalAddress))
            return NormalizeSupportedPortableAddress(candidate, binding!);

        if (TryValidateAddressWidth(logicalAddress, binding!.ValueType, out var error))
            return NormalizeSupportedPortableAddress(candidate, binding);

        var issues = (candidate.Issues ?? Array.Empty<DriverEngineeringIssue>())
            .Append(new DriverEngineeringIssue(
                "S7_TIA_ADDRESS_WIDTH_MISMATCH",
                DriverEngineeringIssueSeverity.Error,
                error!))
            .ToArray();
        var metadata = new Dictionary<string, string>(candidate.Metadata, StringComparer.Ordinal)
        {
            ["supportStatus"] = "Unsupported"
        };
        var prefix = metadata.TryGetValue("sourceKind", out var sourceKind) ? sourceKind switch
        {
            "TiaXlsx" => "tia-xlsx",
            "TiaXml" => "tia-xml",
            "TiaSdf" => "tia-sdf",
            _ => "tia-export"
        } : "tia-export";

        return candidate with
        {
            PortableAddress = $"{prefix}:unsupported:{candidate.CandidateId}",
            IsReadable = false,
            IsWritable = false,
            Metadata = metadata,
            Issues = issues
        };
    }

    internal static bool TryValidateAddressWidth(
        string logicalAddress,
        S7IsoValueType valueType,
        out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalAddress);
        error = null;
        var value = logicalAddress.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (value.StartsWith('%')) value = value[1..];

        string width;
        var db = DbRegex().Match(value);
        if (db.Success)
        {
            width = db.Groups[1].Value;
        }
        else
        {
            var absolute = AbsoluteRegex().Match(value);
            if (!absolute.Success) return true;
            width = absolute.Groups[1].Value;
            if (valueType == S7IsoValueType.Boolean && absolute.Groups[2].Success) width = "X";
        }

        var expected = ExpectedWidth(valueType);
        if (expected is null || string.Equals(width, expected, StringComparison.Ordinal))
            return true;

        error = $"TIA logical address '{logicalAddress}' uses {DescribeWidth(width)} notation, " +
                $"but Siemens data type '{valueType}' requires {DescribeWidth(expected)} notation for this classic absolute binding.";
        return false;
    }

    private static bool TryParseCandidateBinding(string portableAddress, out S7IsoTagBinding? binding)
    {
        if (S7IsoCommunicationBindingSchemaV2.TryParsePortableAddress(portableAddress, out binding, out _))
            return true;
        return S7IsoTagBinding.TryParsePortableAddress(portableAddress, out binding, out _);
    }

    private static DriverImportCandidate NormalizeSupportedPortableAddress(
        DriverImportCandidate candidate,
        S7IsoTagBinding binding)
    {
        var portableAddress = S7IsoCommunicationBindingSchemaV2.ToPortableAddress(binding);
        return string.Equals(candidate.PortableAddress, portableAddress, StringComparison.Ordinal)
            ? candidate
            : candidate with { PortableAddress = portableAddress };
    }

    private static DriverImportCandidate NormalizeStableIdentity(DriverImportCandidate candidate)
    {
        if (candidate.Metadata is null ||
            !candidate.Metadata.TryGetValue("sourceKind", out var sourceKind) ||
            !candidate.Metadata.TryGetValue("sourceName", out var sourceName))
            return candidate;

        candidate.Metadata.TryGetValue("tiaPath", out var path);
        candidate.Metadata.TryGetValue("tiaName", out var name);
        if (string.IsNullOrWhiteSpace(name)) name = candidate.DisplayName;

        var stableIdentity = string.Join(
            "|",
            sourceKind.Trim(),
            sourceName.Trim(),
            path?.Trim() ?? string.Empty,
            name.Trim());
        var candidateId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stableIdentity)))
            .ToLowerInvariant()[..24];
        if (candidate.StableIdentity == stableIdentity && candidate.CandidateId == candidateId)
            return candidate;

        var portableAddress = candidate.PortableAddress;
        var unsupportedMarker = ":unsupported:";
        var markerIndex = portableAddress.IndexOf(unsupportedMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
            portableAddress = portableAddress[..(markerIndex + unsupportedMarker.Length)] + candidateId;

        return candidate with
        {
            CandidateId = candidateId,
            StableIdentity = stableIdentity,
            PortableAddress = portableAddress
        };
    }

    private static string? ExpectedWidth(S7IsoValueType valueType) => valueType switch
    {
        S7IsoValueType.Boolean => "X",
        S7IsoValueType.Byte or S7IsoValueType.SInt => "B",
        S7IsoValueType.UInt16 or S7IsoValueType.Int16 or S7IsoValueType.Date => "W",
        S7IsoValueType.UInt32 or S7IsoValueType.Int32 or S7IsoValueType.Float32 => "D",
        _ => null
    };

    private static string DescribeWidth(string width) => width switch
    {
        "X" => "bit/X",
        "B" => "byte/B",
        "W" => "word/W",
        "D" => "double-word/D",
        "" => "unqualified byte",
        _ => width
    };

    [GeneratedRegex(@"^DB\d+\.DB([XBWD])\d+(?:\.\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex DbRegex();

    [GeneratedRegex(@"^[IQEAM]([BWD]?)\d+(?:\.(\d+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex AbsoluteRegex();
}
