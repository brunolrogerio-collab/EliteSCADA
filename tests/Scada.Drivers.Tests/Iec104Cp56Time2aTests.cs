using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104Cp56Time2aTests
{
    [Fact]
    public void DecodeMapsValidTimestampUsingConfiguredTimezone()
    {
        byte[] encoded =
        [
            0x39, 0x30, // 12.345 seconds within the minute
            0x21,       // minute 33
            0x0E,       // hour 14, standard-time bit
            0xDD,       // Saturday (6), day 29
            0x08,       // August
            0x1A        // 2026
        ];

        var result = Iec104Cp56Time2a.Decode(encoded, TimeZoneInfo.Utc);

        Assert.True(result.Success);
        Assert.False(result.Invalid);
        Assert.False(result.SummerTime);
        Assert.Null(result.Error);
        Assert.True(result.Timestamp.HasValue);
        Assert.Equal(new DateTimeOffset(2026, 8, 29, 14, 33, 12, 345, TimeSpan.Zero), result.Timestamp.Value);
    }

    [Fact]
    public void DecodeDoesNotFabricateTimestampWhenInvalidBitIsSet()
    {
        byte[] encoded = [0x00, 0x00, 0x80, 0x00, 0x01, 0x01, 0x1A];

        var result = Iec104Cp56Time2a.Decode(encoded, TimeZoneInfo.Utc);

        Assert.False(result.Success);
        Assert.True(result.Invalid);
        Assert.Null(result.Timestamp);
        Assert.Null(result.Error);
    }

    [Fact]
    public void DecodeRejectsMillisecondsOutsideOneMinute()
    {
        byte[] encoded = [0x60, 0xEA, 0x00, 0x00, 0x01, 0x01, 0x1A]; // 60000 ms

        var result = Iec104Cp56Time2a.Decode(encoded, TimeZoneInfo.Utc);

        Assert.False(result.Success);
        Assert.Null(result.Timestamp);
        Assert.NotNull(result.Error);
        Assert.Contains("59999", result.Error!);
    }

    [Fact]
    public void DecodeRejectsImpossibleCalendarDate()
    {
        byte[] encoded = [0x00, 0x00, 0x00, 0x00, 0x1F, 0x02, 0x1A]; // 31-Feb-2026

        var result = Iec104Cp56Time2a.Decode(encoded, TimeZoneInfo.Utc);

        Assert.False(result.Success);
        Assert.Null(result.Timestamp);
        Assert.NotNull(result.Error);
        Assert.True(result.Error!.Contains("impossible", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DecodeRejectsNonZeroReservedBits()
    {
        byte[] encoded = [0x00, 0x00, 0x40, 0x00, 0x01, 0x01, 0x1A];

        var result = Iec104Cp56Time2a.Decode(encoded, TimeZoneInfo.Utc);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.True(result.Error!.Contains("reserved", StringComparison.OrdinalIgnoreCase));
    }
}
