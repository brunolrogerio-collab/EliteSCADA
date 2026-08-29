using System.Security.Cryptography;
using Scada.Core.Tags;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// SDK-independent protocol rules shared by the OPC Foundation runtime adapter.
/// Keeping these rules free of third-party types makes namespace stability,
/// certificate pinning and quality/timestamp conversion independently testable.
/// </summary>
public static class OpcUaRuntimeProtocolSupport
{
    private const uint SeverityMask = 0xC0000000u;
    private const uint UncertainSeverity = 0x40000000u;

    public static string ResolveSessionNodeId(
        OpcUaNodeIdentity identity,
        Func<string, int> namespaceIndexResolver)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(namespaceIndexResolver);

        if (identity.NamespaceUri is null)
        {
            return identity.NodeId;
        }

        var namespaceIndex = namespaceIndexResolver(identity.NamespaceUri);
        if (namespaceIndex < 0)
        {
            throw new InvalidOperationException(
                $"OPC UA namespace URI '{identity.NamespaceUri}' is not present in the active server namespace table.");
        }
        if (namespaceIndex > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"OPC UA namespace index '{namespaceIndex}' exceeds the protocol UInt16 range.");
        }

        var identifier = RemoveNamespaceIndex(identity.NodeId);
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new FormatException($"OPC UA NodeId '{identity.NodeId}' does not contain a usable identifier.");
        }

        return namespaceIndex == 0
            ? identifier
            : $"ns={namespaceIndex};{identifier}";
    }

    public static string ComputeCertificateSha256(ReadOnlySpan<byte> certificateBytes) =>
        Convert.ToHexString(SHA256.HashData(certificateBytes));

    public static bool CertificateMatchesSha256Pin(
        ReadOnlySpan<byte> certificateBytes,
        string expectedNormalizedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedNormalizedSha256)) return false;
        var expected = expectedNormalizedSha256.Trim().ToUpperInvariant();
        if (expected.Length != 64 || expected.Any(ch => !Uri.IsHexDigit(ch))) return false;
        return string.Equals(
            ComputeCertificateSha256(certificateBytes),
            expected,
            StringComparison.Ordinal);
    }

    public static TagQuality MapStatusCode(uint statusCode)
    {
        var severity = statusCode & SeverityMask;
        return severity switch
        {
            0u => TagQuality.Good,
            UncertainSeverity => TagQuality.Uncertain,
            _ => TagQuality.Bad
        };
    }

    public static DateTimeOffset? NormalizeProtocolTimestamp(DateTime timestamp)
    {
        if (timestamp == default || timestamp == DateTime.MinValue) return null;
        var utc = timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };
        return new DateTimeOffset(utc);
    }

    private static string RemoveNamespaceIndex(string nodeId)
    {
        if (!nodeId.StartsWith("ns=", StringComparison.Ordinal)) return nodeId;
        var separator = nodeId.IndexOf(';');
        return separator < 0 || separator == nodeId.Length - 1
            ? string.Empty
            : nodeId[(separator + 1)..];
    }
}
