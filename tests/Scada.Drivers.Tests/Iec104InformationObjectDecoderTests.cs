using System.Buffers.Binary;
using Scada.Core.Tags;
using Scada.Drivers.Iec60870;
using Xunit;

namespace Scada.Drivers.Tests;

public sealed class Iec104InformationObjectDecoderTests
{
    [Fact]
    public void Decode_NonSequentialSinglePoints_PreservesExplicitAddressesAndQuality()
    {
        var header = new Iec104AsduHeader(
            Iec104TypeId.MSpNa1,
            ObjectCount: 2,
            IsSequence: false,
            new Iec104CauseOfTransmission(3),
            CommonAddress: 7);

        var payload = new byte[]
        {
            100, 0, 0, 0x01,
            105, 0, 0, 0x40
        };

        var points = Iec104InformationObjectDecoder.Decode(
            Iec104AsduEnvelope.Create(header, payload),
            TimeZoneInfo.Utc);

        Assert.Equal(2, points.Count);
        Assert.Equal(100, points[0].InformationObjectAddress.Value);
        Assert.True(Assert.IsType<bool>(points[0].Value));
        Assert.Equal(TagQuality.Good, points[0].Quality);
        Assert.Equal(105, points[1].InformationObjectAddress.Value);
        Assert.False(Assert.IsType<bool>(points[1].Value));
        Assert.Equal(TagQuality.Stale, points[1].Quality);
    }

    [Fact]
    public void Decode_SequentialScaledValues_IncrementsIoaAndPreservesSignedValues()
    {
        var header = new Iec104AsduHeader(
            Iec104TypeId.MMeNb1,
            ObjectCount: 2,
            IsSequence: true,
            new Iec104CauseOfTransmission(20),
            CommonAddress: 1);

        var payload = new byte[9];
        new Iec104InformationObjectAddress(1000).WriteTo(payload.AsSpan(0, 3));
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(3, 2), -5);
        payload[5] = 0x00;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(6, 2), 42);
        payload[8] = 0x20;

        var points = Iec104InformationObjectDecoder.Decode(
            Iec104AsduEnvelope.Create(header, payload),
            TimeZoneInfo.Utc);

        Assert.Equal(1000, points[0].InformationObjectAddress.Value);
        Assert.Equal((short)-5, Assert.IsType<short>(points[0].Value));
        Assert.Equal(TagQuality.Good, points[0].Quality);
        Assert.Equal(1001, points[1].InformationObjectAddress.Value);
        Assert.Equal((short)42, Assert.IsType<short>(points[1].Value));
        Assert.Equal(TagQuality.Uncertain, points[1].Quality);
    }

    [Fact]
    public void Decode_DoublePointIndeterminate_RemainsEnumAndBecomesUncertain()
    {
        var header = new Iec104AsduHeader(
            Iec104TypeId.MDpNa1,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(3),
            CommonAddress: 2);

        var point = Assert.Single(Iec104InformationObjectDecoder.Decode(
            Iec104AsduEnvelope.Create(header, new byte[] { 9, 0, 0, 0x03 }),
            TimeZoneInfo.Utc));

        Assert.Equal(Iec104DoublePointState.Indeterminate3, Assert.IsType<Iec104DoublePointState>(point.Value));
        Assert.Equal(TagQuality.Uncertain, point.Quality);
    }

    [Fact]
    public void Decode_NormalizedValue_UsesSigned32768Scale()
    {
        var header = new Iec104AsduHeader(
            Iec104TypeId.MMeNa1,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(3),
            CommonAddress: 1);

        var payload = new byte[6];
        new Iec104InformationObjectAddress(20).WriteTo(payload.AsSpan(0, 3));
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(3, 2), short.MinValue);
        payload[5] = 0;

        var point = Assert.Single(Iec104InformationObjectDecoder.Decode(
            Iec104AsduEnvelope.Create(header, payload),
            TimeZoneInfo.Utc));

        Assert.Equal(-1f, Assert.IsType<float>(point.Value));
    }

    [Fact]
    public void Decode_TimeTaggedFloat_ProducesSourceTimestamp()
    {
        var header = new Iec104AsduHeader(
            Iec104TypeId.MMeTf1,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(3),
            CommonAddress: 4);

        var payload = new byte[15];
        new Iec104InformationObjectAddress(321).WriteTo(payload.AsSpan(0, 3));
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(3, 4), BitConverter.SingleToInt32Bits(12.5f));
        payload[7] = 0;
        BuildCp56(new DateTime(2026, 8, 29, 14, 45, 12, 345), payload.AsSpan(8, 7));

        var point = Assert.Single(Iec104InformationObjectDecoder.Decode(
            Iec104AsduEnvelope.Create(header, payload),
            TimeZoneInfo.Utc));

        Assert.Equal(12.5f, Assert.IsType<float>(point.Value));
        Assert.Equal(new DateTimeOffset(2026, 8, 29, 14, 45, 12, 345, TimeSpan.Zero), point.SourceTimestamp);
        Assert.True(point.SourceTime!.Success);
    }

    [Fact]
    public void Decode_RejectsPayloadLengthMismatch()
    {
        var header = new Iec104AsduHeader(
            Iec104TypeId.MSpNa1,
            ObjectCount: 2,
            IsSequence: false,
            new Iec104CauseOfTransmission(3),
            CommonAddress: 1);

        Assert.Throws<Iec104ProtocolException>(() =>
            Iec104InformationObjectDecoder.Decode(
                Iec104AsduEnvelope.Create(header, new byte[] { 1, 0, 0, 1 }),
                TimeZoneInfo.Utc));
    }

    private static void BuildCp56(DateTime value, Span<byte> destination)
    {
        var millisecondsWithinMinute = value.Second * 1000 + value.Millisecond;
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(0, 2), checked((ushort)millisecondsWithinMinute));
        destination[2] = checked((byte)value.Minute);
        destination[3] = checked((byte)value.Hour);
        destination[4] = checked((byte)value.Day);
        destination[5] = checked((byte)value.Month);
        destination[6] = checked((byte)(value.Year - 2000));
    }
}
