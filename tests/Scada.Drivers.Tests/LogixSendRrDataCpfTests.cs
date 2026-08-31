using System.Buffers.Binary;
using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class LogixSendRrDataCpfTests
{
    [Fact]
    public void BuildAndExtract_RoundTripsCanonicalUnconnectedCpf()
    {
        var cip = new byte[] { 0xCC, 0x00, 0x00, 0x00, 0xC4, 0x00, 0x2A, 0x00, 0x00, 0x00 };

        var payload = LogixCipCodec.BuildSendRrDataPayload(cip);

        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4)));
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(6, 2)));
        Assert.Equal((ushort)0x0000, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(8, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(10, 2)));
        Assert.Equal((ushort)0x00B2, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(12, 2)));
        Assert.Equal(cip, LogixCipCodec.ExtractCipFromSendRrData(payload));
    }

    [Fact]
    public void Extract_RejectsConnectedDataItem()
    {
        var payload = LogixCipCodec.BuildSendRrDataPayload([0xCC, 0x00, 0x00, 0x00]);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(12, 2), 0x00B1);

        var error = Assert.Throws<InvalidDataException>(() => LogixCipCodec.ExtractCipFromSendRrData(payload));

        Assert.Contains("0x00B2", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extract_RejectsNonNullAddressItem()
    {
        var payload = LogixCipCodec.BuildSendRrDataPayload([0xCC, 0x00, 0x00, 0x00]);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8, 2), 0x00A1);

        var error = Assert.Throws<InvalidDataException>(() => LogixCipCodec.ExtractCipFromSendRrData(payload));

        Assert.Contains("NULL Address Item", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extract_RejectsUnexpectedItemCountAndInterfaceHandle()
    {
        var wrongCount = LogixCipCodec.BuildSendRrDataPayload([0xCC, 0x00, 0x00, 0x00]);
        BinaryPrimitives.WriteUInt16LittleEndian(wrongCount.AsSpan(6, 2), 1);
        Assert.Throws<InvalidDataException>(() => LogixCipCodec.ExtractCipFromSendRrData(wrongCount));

        var wrongInterface = LogixCipCodec.BuildSendRrDataPayload([0xCC, 0x00, 0x00, 0x00]);
        BinaryPrimitives.WriteUInt32LittleEndian(wrongInterface.AsSpan(0, 4), 1);
        Assert.Throws<InvalidDataException>(() => LogixCipCodec.ExtractCipFromSendRrData(wrongInterface));
    }

    [Fact]
    public void Extract_RejectsTruncatedOrTrailingUnconnectedData()
    {
        var truncated = LogixCipCodec.BuildSendRrDataPayload([0xCC, 0x00, 0x00, 0x00]);
        BinaryPrimitives.WriteUInt16LittleEndian(truncated.AsSpan(14, 2), 5);
        Assert.Throws<InvalidDataException>(() => LogixCipCodec.ExtractCipFromSendRrData(truncated));

        var canonical = LogixCipCodec.BuildSendRrDataPayload([0xCC, 0x00, 0x00, 0x00]);
        var trailing = canonical.Concat(new byte[] { 0x00 }).ToArray();
        Assert.Throws<InvalidDataException>(() => LogixCipCodec.ExtractCipFromSendRrData(trailing));
    }
}
