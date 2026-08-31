using Scada.Core.Tags;

namespace Scada.Drivers.Dnp3;

public enum Dnp3OverRangePolicy
{
    Uncertain,
    BadDevice
}

public sealed record Dnp3PointFlagSet(
    bool HasFlags,
    bool Online,
    bool Restart = false,
    bool CommunicationLost = false,
    bool RemoteForced = false,
    bool LocalForced = false,
    bool ChatterFilter = false,
    bool OverRange = false,
    bool Rollover = false,
    bool Discontinuity = false,
    bool ReferenceError = false)
{
    public static Dnp3PointFlagSet WithoutFlags { get; } = new(HasFlags: false, Online: false);
    public static Dnp3PointFlagSet Nominal { get; } = new(HasFlags: true, Online: true);
}

public static class Dnp3QualityMapper
{
    public static TagQuality Map(
        Dnp3PointFlagSet flags,
        Dnp3OverRangePolicy overRangePolicy = Dnp3OverRangePolicy.Uncertain)
    {
        ArgumentNullException.ThrowIfNull(flags);

        if (!flags.HasFlags)
            return TagQuality.Good;

        if (flags.CommunicationLost)
            return TagQuality.BadCommunication;

        if (!flags.Online || flags.ReferenceError)
            return TagQuality.BadDevice;

        if (flags.OverRange && overRangePolicy == Dnp3OverRangePolicy.BadDevice)
            return TagQuality.BadDevice;

        if (flags.Restart ||
            flags.RemoteForced ||
            flags.LocalForced ||
            flags.ChatterFilter ||
            flags.OverRange ||
            flags.Rollover ||
            flags.Discontinuity)
        {
            return TagQuality.Uncertain;
        }

        return TagQuality.Good;
    }
}

public static class Dnp3MeasurementMapper
{
    public static TagValue CreateTagValue(
        Guid tagId,
        object? value,
        DateTimeOffset observedAt,
        Dnp3PointFlagSet flags,
        DateTimeOffset? sourceTimestamp = null,
        bool sourceTimestampSynchronized = true,
        string? source = null,
        Dnp3OverRangePolicy overRangePolicy = Dnp3OverRangePolicy.Uncertain)
    {
        var quality = Dnp3QualityMapper.Map(flags, overRangePolicy);

        if (sourceTimestamp is not null && !sourceTimestampSynchronized && quality == TagQuality.Good)
            quality = TagQuality.Uncertain;

        return new TagValue(tagId, value, observedAt, quality, source)
        {
            SourceTimestamp = sourceTimestamp
        };
    }
}

public static class Dnp3ValueConversions
{
    public static int Counter16ToCanonical(ushort value) => value;

    public static long Counter32ToCanonical(uint value) => value;
}
