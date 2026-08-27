namespace Scada.Core.Tags;

public sealed record TagValue(
    Guid TagId,
    object? Value,
    DateTimeOffset Timestamp,
    TagQuality Quality,
    string? Source = null)
{
    /// <summary>
    /// Optional timestamp supplied by the originating device/application value.
    /// Timestamp remains the local EliteSCADA observation/publication time.
    /// Protocols that do not provide source time leave this null.
    /// </summary>
    public DateTimeOffset? SourceTimestamp { get; init; }

    /// <summary>
    /// Optional intermediary/server timestamp when a protocol exposes one
    /// separately from the source/device timestamp, such as OPC UA DataValue.
    /// Protocols without this distinction leave the value null.
    /// </summary>
    public DateTimeOffset? ServerTimestamp { get; init; }

    public static TagValue Good(Guid tagId, object? value, string? source = null)
        => new(tagId, value, DateTimeOffset.UtcNow, TagQuality.Good, source);
}
