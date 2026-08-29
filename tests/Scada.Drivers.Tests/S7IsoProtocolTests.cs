using System.Buffers.Binary;
using Scada.Core.Tags;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoProtocolTests
{
    [Fact]
    public void ConnectionRequest_CarriesExplicitSourceAndDerivedDestinationTsap()
    {
        var options = new S7IsoConnectionOptions(
            "plc",
            S7CpuFamily.S71500,
            S7IsoConnectionMode.RackSlot,
            rack: 0,
            slot: 1,
            connectionRole: S7IsoConnectionRole.Basic,
            sourceTsap: 0x0100);

        var packet = S7IsoProtocol.BuildConnectionRequest(options);

        Assert.Equal(22, packet.Length);
        Assert.Equal((byte)0x03, packet[0]);
        Assert.Equal((ushort)22, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(2, 2)));
        Assert.Equal((ushort)0x0100, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(13, 2)));
        Assert.Equal((ushort)0x0301, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(17, 2)));
    }

    [Fact]
    public void SetupCommunication_ParsesNegotiatedPdu()
    {
        const ushort reference = 7;
        var request = S7IsoProtocol.BuildSetupCommunication(reference, 480);

        Assert.Equal(25, request.Length);
        Assert.Equal((byte)0xF0, request[17]);
        Assert.Equal((ushort)480, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(23, 2)));

        var response = AckData(reference, new byte[]
        {
            0xF0, 0x00,
            0x00, 0x01,
            0x00, 0x01,
            0x01, 0xE0
        }, Array.Empty<byte>());

        Assert.Equal((ushort)480, S7IsoProtocol.ParseSetupCommunicationResponse(response, reference));
    }

    [Fact]
    public void ReadRequest_EncodesTypedDbAddress()
    {
        const ushort reference = 11;
        var point = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.DataBlock,
            ByteOffset: 4,
            ValueType: S7IsoValueType.Int16,
            DbNumber: 1);

        var packet = S7IsoProtocol.BuildReadRequest(reference, new[] { point });

        Assert.Equal(31, packet.Length);
        Assert.Equal((byte)0x04, packet[17]);
        Assert.Equal((byte)0x01, packet[18]);
        Assert.Equal((byte)0x05, packet[22]);
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(23, 2)));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(25, 2)));
        Assert.Equal((byte)S7IsoArea.DataBlock, packet[27]);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x20 }, packet[28..31]);
    }

    [Fact]
    public void ReadResponse_PreservesPerItemFailureWithoutPoisoningHealthyItem()
    {
        const ushort reference = 12;
        var healthy = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Int16);
        var missing = new S7IsoPoint(
            Tag(TagDataType.Int16),
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int16,
            DbNumber: 99);

        var data = new byte[]
        {
            0xFF, 0x05, 0x00, 0x10, 0x12, 0x34,
            0x0A, 0x00, 0x00, 0x00
        };
        var response = AckData(reference, new byte[] { 0x04, 0x02 }, data);

        var results = S7IsoProtocol.ParseReadResponse(response, reference, new[] { healthy, missing });

        Assert.True(results[0].Succeeded);
        Assert.Equal(new byte[] { 0x12, 0x34 }, results[0].Data);
        Assert.False(results[1].Succeeded);
        Assert.Equal((byte)0x0A, results[1].ReturnCode);
    }

    [Fact]
    public void ReadResponse_DIntegerTransportUsesByteLength()
    {
        const ushort reference = 21;
        var point = new S7IsoPoint(
            Tag(TagDataType.Int32),
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Int32);
        var response = AckData(
            reference,
            new byte[] { 0x04, 0x01 },
            new byte[] { 0xFF, 0x06, 0x00, 0x04, 0x11, 0x22, 0x33, 0x44 });

        var result = Assert.Single(S7IsoProtocol.ParseReadResponse(response, reference, new[] { point }));

        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, result.Data);
        Assert.Equal(0x11223344, Assert.IsType<int>(S7IsoValueCodec.Decode(point, result.Data!)));
    }

    [Fact]
    public void ReadResponse_RealTransportUsesByteLength()
    {
        const ushort reference = 22;
        var point = new S7IsoPoint(
            Tag(TagDataType.Float),
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Float32);
        var response = AckData(
            reference,
            new byte[] { 0x04, 0x01 },
            new byte[] { 0xFF, 0x07, 0x00, 0x04, 0x41, 0x48, 0x00, 0x00 });

        var result = Assert.Single(S7IsoProtocol.ParseReadResponse(response, reference, new[] { point }));

        Assert.Equal(new byte[] { 0x41, 0x48, 0x00, 0x00 }, result.Data);
        Assert.Equal(12.5f, Assert.IsType<float>(S7IsoValueCodec.Decode(point, result.Data!)));
    }

    [Fact]
    public void WriteRequest_EncodesCanonicalPayloadAndChecksReturnCode()
    {
        const ushort reference = 13;
        var point = new S7IsoPoint(
            Tag(TagDataType.Int32),
            S7IsoArea.DataBlock,
            8,
            S7IsoValueType.Int32,
            DbNumber: 2,
            Writable: true);
        var payload = new byte[] { 0x11, 0x22, 0x33, 0x44 };

        var request = S7IsoProtocol.BuildWriteRequest(reference, point, payload);

        Assert.Equal((byte)0x05, request[17]);
        Assert.Equal((byte)0x01, request[18]);
        Assert.Equal((byte)0x00, request[31]);
        Assert.Equal((byte)0x04, request[32]);
        Assert.Equal((ushort)32, BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(33, 2)));
        Assert.Equal(payload, request[35..39]);

        S7IsoProtocol.ParseWriteResponse(
            AckData(reference, new byte[] { 0x05, 0x01 }, new byte[] { 0xFF }),
            reference);

        var rejected = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ParseWriteResponse(
                AckData(reference, new byte[] { 0x05, 0x01 }, new byte[] { 0x03 }),
                reference));
        Assert.Equal((byte)0x03, rejected.ReturnCode);
    }

    [Fact]
    public void BatchPlanner_UsesNegotiatedPduAndKeepsEveryPoint()
    {
        var points = Enumerable.Range(0, 30)
            .Select(index => new S7IsoPoint(
                Tag(TagDataType.Int32),
                S7IsoArea.Merker,
                index * 4,
                S7IsoValueType.Int32))
            .ToArray();

        var batches = S7IsoBatchPlanner.PlanReads(points, 240);

        Assert.True(batches.Count > 1);
        Assert.Equal(points.Length, batches.Sum(batch => batch.Count));
        Assert.All(batches, batch => Assert.NotEmpty(batch));
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