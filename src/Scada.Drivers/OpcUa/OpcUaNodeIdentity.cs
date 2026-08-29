namespace Scada.Drivers.OpcUa;

/// <summary>
/// Library-independent OPC UA node identity. When a NamespaceUri is available,
/// StableIdentity intentionally ignores the server-assigned namespace index so
/// imported TAG bindings survive namespace table reordering.
/// </summary>
public sealed record OpcUaNodeIdentity
{
    public OpcUaNodeIdentity(string nodeId, string? namespaceUri = null)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("OPC UA NodeId cannot be empty.", nameof(nodeId));
        }

        NodeId = nodeId.Trim();
        NamespaceUri = string.IsNullOrWhiteSpace(namespaceUri) ? null : namespaceUri.Trim();
    }

    public string NodeId { get; }

    public string? NamespaceUri { get; }

    public string StableIdentity => NamespaceUri is null
        ? $"node={Escape(NodeId)}"
        : $"nsu={Escape(NamespaceUri)}&id={Escape(GetNamespaceIndependentIdentifier(NodeId))}";

    public string PortableAddress => NamespaceUri is null
        ? $"node={Escape(NodeId)}"
        : $"node={Escape(NodeId)}&nsu={Escape(NamespaceUri)}";

    public static OpcUaNodeIdentity ParsePortableAddress(string portableAddress)
    {
        if (string.IsNullOrWhiteSpace(portableAddress))
        {
            throw new ArgumentException("OPC UA portable address cannot be empty.", nameof(portableAddress));
        }

        string? nodeId = null;
        string? namespaceUri = null;

        foreach (var component in portableAddress.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = component.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = component[..separator];
            var value = Uri.UnescapeDataString(component[(separator + 1)..]);

            if (string.Equals(key, "node", StringComparison.Ordinal))
            {
                nodeId = value;
            }
            else if (string.Equals(key, "nsu", StringComparison.Ordinal))
            {
                namespaceUri = value;
            }
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new FormatException("OPC UA portable address does not contain a node component.");
        }

        return new OpcUaNodeIdentity(nodeId, namespaceUri);
    }

    private static string GetNamespaceIndependentIdentifier(string nodeId)
    {
        if (!nodeId.StartsWith("ns=", StringComparison.Ordinal))
        {
            return nodeId;
        }

        var separator = nodeId.IndexOf(';');
        return separator < 0 || separator == nodeId.Length - 1
            ? nodeId
            : nodeId[(separator + 1)..];
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
}
