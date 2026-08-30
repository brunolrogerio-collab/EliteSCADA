using System.Buffers.Binary;
using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoFunctionFramingTests
{
    [Fact]
    public void ReadResponse_RejectsExtraParameterBytes()
    {
        const ushort reference = 51;
        var point = Point();
        var response = AckData(
            reference,
            new byte[] { 0x04, 0x01, 0x00 },
            new byte[] { 0xFF, 0x05, 0x00, 0x10, 0x12, 0x34 });

        var error = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ParseReadResponse(response, reference, new[] { point }));

        Assert.Contains("response parameters", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadResponse_RejectsDeclaredTrailingDataAfterItems()
    {
        const ushort reference = 52;
        var point = Point();
        var response = AckData(
            reference,
            new byte[] { 0x04, 0x01 },
            new byte[] { 0xFF, 0x05, 0x00, 0x10, 0x12, 0x34, 0x00 });

        var error = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ParseReadResponse(response, reference, new[] { point }));

        Assert.Contains("unconsumed data", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteResponse_RejectsMultipleReturnCodesForSingleItemRequest()
    {
        const ushort reference = 53;
        var response = AckData(
            reference,
            new byte[] { 0x05, 0x01 },
            new byte[] { 0xFF, 0xFF });

        var error = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ParseWriteResponse(response, reference));

        Assert.Contains("exactly one item return code", error.Message, StringComparison.Ordinal);
    }

    private static S7IsoPoint Point() => new(
        TagDefinition.Create(
            "Framing",
            $"PLC.Framing.{Guid.NewGuid():N}",
            TagDataType.Int16,
            source: "siemens.s7.iso"),
        S7IsoArea.Merker,
        0,
        S7IsoValueType.Int16);

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
}
