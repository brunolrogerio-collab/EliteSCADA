using System.Buffers.Binary;
using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoSnap7CompatibilityTests
{
    [Fact]
    public void ReadResponse_Snap7ByteTransport_DecodesInt16()
    {
        const ushort reference = 41;
        var point = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.Int16),
            S7IsoArea.DataBlock,
            ByteOffset: 0,
            ValueType: S7IsoValueType.Int16,
            DbNumber: 1);

        var response = AckData(
            reference,
            new byte[] { 0x04, 0x01 },
            new byte[] { 0xFF, 0x04, 0x00, 0x10, 0x04, 0xD2 });

        var result = Assert.Single(S7IsoProtocol.ParseReadResponse(response, reference, new[] { point }));

        Assert.True(result.Succeeded);
        Assert.Equal(new byte[] { 0x04, 0xD2 }, result.Data);
        Assert.Equal((short)1234, Assert.IsType<short>(S7IsoValueCodec.Decode(point, result.Data!)));
    }

    [Fact]
    public void ReadResponse_Snap7ByteTransport_DecodesInt32()
    {
        const ushort reference = 42;
        var point = new S7IsoPoint(
            S7IsoTransportTests.Tag(TagDataType.Int32),
            S7IsoArea.DataBlock,
            ByteOffset: 4,
            ValueType: S7IsoValueType.Int32,
            DbNumber: 1);

        var response = AckData(
            reference,
            new byte[] { 0x04, 0x01 },
            new byte[] { 0xFF, 0x04, 0x00, 0x20, 0x00, 0x01, 0xE2, 0x40 });

        var result = Assert.Single(S7IsoProtocol.ParseReadResponse(response, reference, new[] { point }));

        Assert.True(result.Succeeded);
        Assert.Equal(123456, Assert.IsType<int>(S7IsoValueCodec.Decode(point, result.Data!)));
    }

    private static byte[] AckData(ushort reference, byte[] parameters, byte[] data)
    {
        const int s7Offset = 7;
        const int ackHeaderLength = 12;
        var packet = new byte[s7Offset + ackHeaderLength + parameters.Length + data.Length];
        packet[0] = 0x03;
        packet[1] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), checked((ushort)packet.Length));
        packet[4] = 0x02;
        packet[5] = 0xF0;
        packet[6] = 0x80;
        packet[s7Offset] = 0x32;
        packet[s7Offset + 1] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(s7Offset + 4, 2), reference);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(s7Offset + 6, 2), checked((ushort)parameters.Length));
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(s7Offset + 8, 2), checked((ushort)data.Length));
        parameters.CopyTo(packet.AsSpan(s7Offset + ackHeaderLength));
        data.CopyTo(packet.AsSpan(s7Offset + ackHeaderLength + parameters.Length));
        return packet;
    }
}
