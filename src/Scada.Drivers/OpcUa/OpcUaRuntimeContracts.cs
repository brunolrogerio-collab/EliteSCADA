using System.Globalization;
using Scada.Core.Tags;

namespace Scada.Drivers.OpcUa;

public sealed record OpcUaRuntimeBinding(
    TagDefinition Tag,
    OpcUaNodeIdentity Node,
    TimeSpan SamplingInterval,
    uint QueueSize,
    bool DiscardOldest)
{
    public const string NodeIdMetadataKey = "opcUa.nodeId";
    public const string NamespaceUriMetadataKey = "opcUa.namespaceUri";
    public const string SamplingIntervalMetadataKey = "opcUa.samplingInterval";
    public const string QueueSizeMetadataKey = "opcUa.queueSize";
    public const string DiscardOldestMetadataKey = "opcUa.discardOldest";

    public bool Writable => !Tag.ReadOnly;

    public static OpcUaRuntimeBinding FromTag(TagDefinition tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        var nodeId = GetMetadata(tag, NodeIdMetadataKey);
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new InvalidOperationException(
                $"OPC UA TAG '{tag.Path}' is missing required metadata '{NodeIdMetadataKey}'.");
        }

        var namespaceUri = GetMetadata(tag, NamespaceUriMetadataKey);
        var samplingInterval = ParseDuration(
            GetMetadata(tag, SamplingIntervalMetadataKey),
            TimeSpan.FromSeconds(1),
            SamplingIntervalMetadataKey,
            allowZero: true);
        var queueSize = ParseQueueSize(GetMetadata(tag, QueueSizeMetadataKey));
        var discardOldest = ParseBoolean(GetMetadata(tag, DiscardOldestMetadataKey), defaultValue: true, DiscardOldestMetadataKey);

        return new OpcUaRuntimeBinding(
            tag,
            new OpcUaNodeIdentity(nodeId, namespaceUri),
            samplingInterval,
            queueSize,
            discardOldest);
    }

    private static string? GetMetadata(TagDefinition tag, string key)
    {
        if (tag.Metadata is null) return null;
        if (tag.Metadata.TryGetValue(key, out var exact)) return exact;

        foreach (var pair in tag.Metadata)
        {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        }

        return null;
    }

    private static TimeSpan ParseDuration(string? value, TimeSpan defaultValue, string key, bool allowZero)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < TimeSpan.Zero ||
            (!allowZero && parsed == TimeSpan.Zero))
        {
            throw new InvalidOperationException($"OPC UA metadata '{key}' has invalid duration '{value}'.");
        }

        return parsed;
    }

    private static uint ParseQueueSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 1;
        if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed is < 1 or > 10000)
        {
            throw new InvalidOperationException(
                $"OPC UA metadata '{QueueSizeMetadataKey}' must be an integer between 1 and 10000.");
        }

        return parsed;
    }

    private static bool ParseBoolean(string? value, bool defaultValue, string key)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (bool.TryParse(value, out var parsed)) return parsed;
        throw new InvalidOperationException($"OPC UA metadata '{key}' has invalid Boolean value '{value}'.");
    }
}

public sealed record OpcUaRuntimeDataValue(
    Guid TagId,
    object? Value,
    TagQuality Quality,
    DateTimeOffset? SourceTimestamp = null,
    DateTimeOffset? ServerTimestamp = null);

public interface IOpcUaRuntimeSessionFactory
{
    Task<IOpcUaRuntimeSession> ConnectAsync(
        IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
        CancellationToken cancellationToken);
}

public interface IOpcUaRuntimeSession : IAsyncDisposable
{
    Task<OpcUaRuntimeDataValue> ReadAsync(
        OpcUaRuntimeBinding binding,
        CancellationToken cancellationToken);

    Task WriteAsync(
        OpcUaRuntimeBinding binding,
        object value,
        CancellationToken cancellationToken);

    IAsyncEnumerable<OpcUaRuntimeDataValue> SubscribeAsync(CancellationToken cancellationToken);
}
