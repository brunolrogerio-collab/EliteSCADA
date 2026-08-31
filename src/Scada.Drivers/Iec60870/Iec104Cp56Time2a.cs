using System.Buffers.Binary;

namespace Scada.Drivers.Iec60870;

public sealed record Iec104Cp56DecodeResult(
    DateTimeOffset? Timestamp,
    bool Invalid,
    bool SummerTime,
    string? Error)
{
    public bool Success => Timestamp.HasValue && !Invalid && Error is null;
}

public static class Iec104Cp56Time2a
{
    public const int EncodedLength = 7;

    public static Iec104Cp56DecodeResult Decode(ReadOnlySpan<byte> data, TimeZoneInfo stationTimeZone)
    {
        ArgumentNullException.ThrowIfNull(stationTimeZone);

        if (data.Length != EncodedLength)
            return new Iec104Cp56DecodeResult(null, false, false, $"CP56Time2a requires exactly {EncodedLength} octets.");

        var millisecondsWithinMinute = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0, 2));
        var minuteByte = data[2];
        var hourByte = data[3];
        var dayByte = data[4];
        var monthByte = data[5];
        var yearByte = data[6];

        var invalid = (minuteByte & 0x80) != 0;
        var summerTime = (hourByte & 0x80) != 0;

        if ((minuteByte & 0x40) != 0 || (hourByte & 0x60) != 0 || (monthByte & 0xF0) != 0 || (yearByte & 0x80) != 0)
            return new Iec104Cp56DecodeResult(null, invalid, summerTime, "CP56Time2a contains non-zero reserved bits.");

        if (invalid)
            return new Iec104Cp56DecodeResult(null, true, summerTime, null);

        if (millisecondsWithinMinute > 59_999)
            return new Iec104Cp56DecodeResult(null, false, summerTime, "CP56Time2a milliseconds-within-minute is outside 0..59999.");

        var minute = minuteByte & 0x3F;
        var hour = hourByte & 0x1F;
        var dayOfMonth = dayByte & 0x1F;
        var dayOfWeek = (dayByte >> 5) & 0x07;
        var month = monthByte & 0x0F;
        var year = 2000 + (yearByte & 0x7F);
        var second = millisecondsWithinMinute / 1000;
        var millisecond = millisecondsWithinMinute % 1000;

        if (minute > 59 || hour > 23 || dayOfMonth is < 1 or > 31 || month is < 1 or > 12)
            return new Iec104Cp56DecodeResult(null, false, summerTime, "CP56Time2a contains an out-of-range calendar/time field.");

        DateTime local;
        try
        {
            local = new DateTime(year, month, dayOfMonth, hour, minute, second, millisecond, DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new Iec104Cp56DecodeResult(null, false, summerTime, "CP56Time2a contains an impossible calendar date.");
        }

        if (dayOfWeek != 0)
        {
            var expectedDayOfWeek = local.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;
            if (dayOfWeek != expectedDayOfWeek)
                return new Iec104Cp56DecodeResult(null, false, summerTime, "CP56Time2a day-of-week does not match the encoded calendar date.");
        }

        if (stationTimeZone.IsInvalidTime(local))
            return new Iec104Cp56DecodeResult(null, false, summerTime, "CP56Time2a maps to a non-existent local time in the configured station timezone.");

        DateTimeOffset timestamp;
        if (stationTimeZone.IsAmbiguousTime(local))
        {
            var offsets = stationTimeZone.GetAmbiguousTimeOffsets(local);
            DateTimeOffset? selected = null;

            foreach (var offset in offsets)
            {
                var candidate = new DateTimeOffset(local, offset);
                if (stationTimeZone.IsDaylightSavingTime(candidate) != summerTime)
                    continue;

                if (selected.HasValue)
                    return new Iec104Cp56DecodeResult(null, false, summerTime, "CP56Time2a remains ambiguous in the configured station timezone.");

                selected = candidate;
            }

            if (!selected.HasValue)
                return new Iec104Cp56DecodeResult(null, false, summerTime, "CP56Time2a summer-time bit does not resolve the ambiguous local time.");

            timestamp = selected.Value;
        }
        else
        {
            timestamp = new DateTimeOffset(local, stationTimeZone.GetUtcOffset(local));
        }

        return new Iec104Cp56DecodeResult(timestamp, false, summerTime, null);
    }
}
