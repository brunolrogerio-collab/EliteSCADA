using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104ProtocolConformanceTests
{
    [Fact]
    public void CompleteSinglePointApdu_MatchesCanonicalBinaryVector()
    {
        byte[] expected =
        [
            0x68, 0x0E,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x01, 0x03, 0x00, 0x01, 0x00,
            0x4D, 0x00, 0x00, 0x01
        ];

        var payload = new byte[4];
        new Iec104InformationObjectAddress(77).WriteTo(payload.AsSpan(0, 3));
        payload[3] = 0x01;
        var asdu = Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.MSpNa1,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(causeCode: 3),
                CommonAddress: 1),
            payload);
        var frame = Iec104ApciFrame.I(0, 0, Iec104AsduCodec.Serialize(asdu));

        var encoded = Iec104ApciCodec.Serialize(frame);

        Assert.Equal(expected, encoded);

        var parsedFrame = Iec104ApciCodec.Parse(expected);
        var parsedAsdu = Iec104AsduCodec.Parse(parsedFrame.Asdu.Span);
        var point = Assert.Single(Iec104InformationObjectDecoder.Decode(parsedAsdu, TimeZoneInfo.Utc));
        Assert.Equal((ushort)1, point.CommonAddress);
        Assert.Equal(77, point.InformationObjectAddress.Value);
        Assert.Equal(true, point.Value);
    }

    [Fact]
    public void MaximumSequenceNumber_UsesFffeControlEncodingAndRoundTrips()
    {
        var frame = Iec104ApciFrame.I(32767, 32767, new byte[] { 0x01 });

        var encoded = Iec104ApciCodec.Serialize(frame);

        Assert.Equal(new byte[] { 0x68, 0x05, 0xFE, 0xFF, 0xFE, 0xFF, 0x01 }, encoded);
        var parsed = Iec104ApciCodec.Parse(encoded);
        Assert.Equal((ushort)32767, parsed.SendSequence);
        Assert.Equal((ushort)32767, parsed.ReceiveSequence);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(16383)]
    [InlineData(16384)]
    [InlineData(32767)]
    public void RepresentativeSequenceValues_RoundTripWithoutBitLoss(int sequence)
    {
        var value = checked((ushort)sequence);
        var encoded = Iec104ApciCodec.Serialize(Iec104ApciFrame.I(value, value, new byte[] { 0x64 }));

        var parsed = Iec104ApciCodec.Parse(encoded);

        Assert.Equal(value, parsed.SendSequence);
        Assert.Equal(value, parsed.ReceiveSequence);
    }

    [Fact]
    public void Cp56Time2a_KnownUtcVectorDecodesExactCalendarTime()
    {
        byte[] cp56 =
        [
            0x39, 0x30, // 12.345 seconds within minute
            0x29,       // minute 41
            0x16,       // hour 22, SU=0
            0xDD,       // day 29, Saturday (6)
            0x08,       // August
            0x1A        // 2026
        ];

        var decoded = Iec104Cp56Time2a.Decode(cp56, TimeZoneInfo.Utc);

        Assert.True(decoded.Success);
        Assert.False(decoded.Invalid);
        Assert.False(decoded.SummerTime);
        Assert.Null(decoded.Error);
        Assert.Equal(new DateTimeOffset(2026, 8, 29, 22, 41, 12, 345, TimeSpan.Zero), decoded.Timestamp);
    }

    [Fact]
    public void Cp56Time2a_InvalidBitSuppressesSourceTimestampWithoutInventingAnError()
    {
        byte[] cp56 =
        [
            0x39, 0x30,
            0xA9,
            0x16,
            0xDD,
            0x08,
            0x1A
        ];

        var decoded = Iec104Cp56Time2a.Decode(cp56, TimeZoneInfo.Utc);

        Assert.True(decoded.Invalid);
        Assert.False(decoded.Success);
        Assert.Null(decoded.Timestamp);
        Assert.Null(decoded.Error);
    }

    [Fact]
    public void SequentialAsdu_RejectsIoaOverflowInsteadOfWrappingToZero()
    {
        var payload = new byte[5];
        new Iec104InformationObjectAddress(Iec104InformationObjectAddress.MaximumValue)
            .WriteTo(payload.AsSpan(0, 3));
        payload[3] = 0x01;
        payload[4] = 0x00;
        var asdu = Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.MSpNa1,
                ObjectCount: 2,
                IsSequence: true,
                new Iec104CauseOfTransmission(causeCode: 3),
                CommonAddress: 1),
            payload);

        Assert.Throws<Iec104ProtocolException>(() =>
            Iec104InformationObjectDecoder.Decode(asdu, TimeZoneInfo.Utc));
    }

    [Fact]
    public void ReservedCp56MinuteBit_IsRejectedAsProtocolTimestampEvidence()
    {
        byte[] cp56 =
        [
            0x00, 0x00,
            0x40,
            0x00,
            0x21,
            0x01,
            0x1A
        ];

        var decoded = Iec104Cp56Time2a.Decode(cp56, TimeZoneInfo.Utc);

        Assert.False(decoded.Success);
        Assert.Null(decoded.Timestamp);
        Assert.Contains("reserved bits", decoded.Error ?? string.Empty);
    }
}
