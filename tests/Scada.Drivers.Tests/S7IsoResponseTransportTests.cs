using System.Buffers.Binary;
using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoResponseTransportTests
{
    [Fact]
    public void Int16_RejectsByteResultTransportEvenWhenPayloadLengthMatches()
    {
        const ushort reference = 51;
        var point = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Int16);
        var response = AckData(
            reference,
            new byte[] { 0x04, 0x01 },
            new byte[] { 0xFF, 0x04, 0x00, 0x10, 0x12, 0x34 });

        var error = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ParseReadResponse(response, reference, new[] { point }));

        Assert.Contains("transport size 0x04", error.Message, StringComparison.Ordinal);
        Assert.Contains("expected 0x05", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UInt16_AcceptsByteResultTransportForWordRequest()
    {
        const ushort reference = 52;
        var point = new S7IsoPoint(
            Tag(TagDataType.Int32),
            S7IsoArea.Merker,
            0,
            S7IsoValueType.UInt16);
        var response = AckData(
            reference,
            new byte[] { 0x04, 0x01 },
            new byte[] { 0xFF, 0x04, 0x00, 0x10, 0xFE, 0xDC });

        var result = Assert.Single(S7IsoProtocol.ParseReadResponse(response, reference, new[] { point }));

        Assert.True(result.Succeeded);
        Assert.Equal(65_244, Assert.IsType<int>(S7IsoValueCodec.Decode(point, result.Data!)));
    }

    [Fact]
    public void Float32_RejectsByteResultTransportEvenWhenFourBytesArePresent()
    {
        const ushort reference = 53;
        var point = new S7IsoPoint(
            Tag(TagDataType.Float),
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Float32);
        var response = AckData(
            reference,
            new byte[] { 0x04, 0x01 },
            new byte[] { 0xFF, 0x04, 0x00, 0x20, 0x41, 0x48, 0x00, 0x00 });

        var error = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ParseReadResponse(response, reference, new[] { point }));

        Assert.Contains("expected 0x07", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DateAndTime_AcceptsByteResultTransportForRawBlockRequest()
    {
        const ushort reference = 54;
        var point = new S7IsoPoint(
            Tag(TagDataType.DateTime),
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.DateTime,
            DbNumber: 1);
        var payload = new byte[] { 0x26, 0x08, 0x29, 0x14, 0x35, 0x42, 0x12, 0x37 };
        var data = new byte[4 + payload.Length];
        data[0] = 0xFF;
        data[1] = 0x04;
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(2, 2), checked((ushort)(payload.Length * 8)));
        payload.CopyTo(data, 4);
        var response = AckData(reference, new byte[] { 0x04, 0x01 }, data);

        var result = Assert.Single(S7IsoProtocol.ParseReadResponse(response, reference, new[] { point }));

        Assert.Equal(new DateTime(2026, 8, 29, 14, 35, 42, 123), Assert.IsType<DateTime>(S7IsoValueCodec.Decode(point, result.Data!)));
    }

    private static byte[] AckData(ushort reference, byte[] parameter, byte[] data)
    {
        var packet = new byte[4 + 3 + 12 + parameter.Length + data.Length];
        packet[0] = 0x03;
        packet[1] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), checked((ushort)packet.Length));
        packet[4] = 0x02;
        packet[5] = 0xF0;
        packet[6] = 0x80;
        packet[7] = 0x32;
        packet[8] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(11, 2), reference);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(13, 2), checked((ushort)parameter.Length));
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(15, 2), checked((ushort)data.Length));
        packet[17] = 0x00;
        packet[18] = 0x00;
        parameter.CopyTo(packet, 19);
        data.CopyTo(packet, 19 + parameter.Length);
        return packet;
    }

    private static TagDefinition Tag(TagDataType type) => new(
        Guid.NewGuid(),
        "T",
        $"PLC.{Guid.NewGuid():N}",
        type,
        "s7",
        null,
        null,
        false);
}
