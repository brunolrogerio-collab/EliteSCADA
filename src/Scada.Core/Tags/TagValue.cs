namespace Scada.Core.Tags;

public sealed record TagValue(
    Guid TagId,
    object? Value,
    DateTimeOffset Timestamp,
    TagQuality Quality,
    string? Source = null)
{
    public static TagValue Good(Guid tagId, object? value, string? source = null)
        => new(tagId, value, DateTimeOffset.UtcNow, TagQuality.Good, source);
}
