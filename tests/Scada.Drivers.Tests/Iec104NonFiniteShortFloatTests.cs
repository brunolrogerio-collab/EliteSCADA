using System.Buffers.Binary;
using Scada.Core.Tags;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104NonFiniteShortFloatTests
{
    [Fact]
    public void UntimedNaN_IsPreservedButMarkedUncertain()
    {
        var point = DecodeUntimed(float.NaN);

        Assert.True(float.IsNaN(Assert.IsType<float>(point.Value)));
        Assert.Equal(TagQuality.Uncertain, point.Quality);
        Assert.Null(point.SourceTimestamp);
    }

    [Fact]
    public void UntimedPositiveInfinity_IsPreservedButMarkedUncertain()
    {
        var point = DecodeUntimed(float.PositiveInfinity);

        Assert.Equal(float.PositiveInfinity, Assert.IsType<float>(point.Value));
        Assert.Equal(TagQuality.Uncertain, point.Quality);
    }

    [Fact]
    public void UntimedNegativeInfinity_IsPreservedButMarkedUncertain()
    {
        var point = DecodeUntimed(float.NegativeInfinity);

        Assert.Equal(float.NegativeInfinity, Assert.IsType<float>(point.Value));
        Assert.Equal(TagQuality.Uncertain, point.Quality);
    }

    [Fact]
    public void TimedNaN_PreservesValidCp56SourceTimestampAndMarksValueUncertain()
    {
        Span<byte> objectData = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(
            objectData[..4],
            BitConverter.SingleToInt32Bits(float.NaN));
        objectData[4] = 0x00;
        byte[] cp56 =
        [
            0x39, 0x30,
            0x29,
            0x16,
            0xDD,
            0x08,
            0x1A
        ];
        cp56.AsSpan().CopyTo(objectData[5..]);

        var point = Decode(Iec104TypeId.MMeTf1, objectData);

        Assert.True(float.IsNaN(Assert.IsType<float>(point.Value)));
        Assert.Equal(TagQuality.Uncertain, point.Quality);
        Assert.Equal<DateTimeOffset?>(
            new DateTimeOffset(2026, 8, 29, 22, 41, 12, 345, TimeSpan.Zero),
            point.SourceTimestamp);
    }

    [Fact]
    public void InvalidQdsStillOutranksNonFiniteSemanticUncertainty()
    {
        Span<byte> objectData = stackalloc byte[5];
        BinaryPrimitives.WriteInt32LittleEndian(
            objectData[..4],
            BitConverter.SingleToInt32Bits(float.NaN));
        objectData[4] = 0x80;

        var point = Decode(Iec104TypeId.MMeNc1, objectData);

        Assert.True(float.IsNaN(Assert.IsType<float>(point.Value)));
        Assert.Equal(TagQuality.BadDevice, point.Quality);
    }

    private static Iec104DecodedPoint DecodeUntimed(float value)
    {
        Span<byte> objectData = stackalloc byte[5];
        BinaryPrimitives.WriteInt32LittleEndian(
            objectData[..4],
            BitConverter.SingleToInt32Bits(value));
        objectData[4] = 0x00;
        return Decode(Iec104TypeId.MMeNc1, objectData);
    }

    private static Iec104DecodedPoint Decode(
        Iec104TypeId typeId,
        ReadOnlySpan<byte> objectData)
    {
        var payload = new byte[3 + objectData.Length];
        new Iec104InformationObjectAddress(77).WriteTo(payload.AsSpan(0, 3));
        objectData.CopyTo(payload.AsSpan(3));
        var asdu = Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                typeId,
                ObjectCount: 1,
                IsSequence: false,
                new Iec104CauseOfTransmission(causeCode: 3),
                CommonAddress: 1),
            payload);

        return Assert.Single(Iec104InformationObjectDecoder.Decode(asdu, TimeZoneInfo.Utc));
    }
}
