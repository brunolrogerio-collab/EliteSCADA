using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class LogixMultipleServicePacketTests
{
    [Fact]
    public void BuildRequest_MatchesRockwellTwoReadExampleLayout()
    {
        byte[] first =
        [
            0x4C, 0x04, 0x91, 0x05, 0x70, 0x61, 0x72, 0x74, 0x73, 0x00, 0x01, 0x00
        ];
        byte[] second =
        [
            0x4C, 0x07, 0x91, 0x0B, 0x43, 0x6F, 0x6E, 0x74, 0x72, 0x6F, 0x6C, 0x57, 0x6F, 0x72, 0x64, 0x00, 0x01, 0x00
        ];

        var request = LogixMultipleServicePacket.BuildRequest([first, second]);

        byte[] expected =
        [
            0x0A, 0x02, 0x20, 0x02, 0x24, 0x01,
            0x02, 0x00,
            0x06, 0x00,
            0x12, 0x00,
            0x4C, 0x04, 0x91, 0x05, 0x70, 0x61, 0x72, 0x74, 0x73, 0x00, 0x01, 0x00,
            0x4C, 0x07, 0x91, 0x0B, 0x43, 0x6F, 0x6E, 0x74, 0x72, 0x6F, 0x6C, 0x57, 0x6F, 0x72, 0x64, 0x00, 0x01, 0x00
        ];
        Assert.Equal(expected, request);
    }

    [Fact]
    public void ParseResponse_ExtractsIndependentEmbeddedReplies()
    {
        byte[] replyData =
        [
            0x02, 0x00,
            0x06, 0x00,
            0x10, 0x00,
            0xCC, 0x00, 0x00, 0x00, 0xC4, 0x00, 0x2A, 0x00, 0x00, 0x00,
            0xCC, 0x00, 0x00, 0x00, 0xC4, 0x00, 0xDC, 0x01, 0x00, 0x00
        ];
        var outer = new LogixCipResponse(0x8A, 0, Array.Empty<ushort>(), replyData);

        var replies = LogixMultipleServicePacket.ParseResponse(outer);

        Assert.Equal(2, replies.Count);
        var first = new LogixSymbolReference(LogixTagScope.Controller, "parts", LogixNativeType.Dint);
        var second = new LogixSymbolReference(LogixTagScope.Controller, "ControlWord", LogixNativeType.Dint);
        Assert.Equal(42, Assert.IsType<int>(LogixCipCodec.ParseReadTagValue(first, replies[0])));
        Assert.Equal(476, Assert.IsType<int>(LogixCipCodec.ParseReadTagValue(second, replies[1])));
    }

    [Fact]
    public void ParseResponse_PreservesPerServiceFailureInsteadOfFailingWholePacket()
    {
        byte[] replyData =
        [
            0x02, 0x00,
            0x06, 0x00,
            0x0A, 0x00,
            0xCC, 0x00, 0x04, 0x00,
            0xCC, 0x00, 0x00, 0x00, 0xC4, 0x00, 0x07, 0x00, 0x00, 0x00
        ];
        var replies = LogixMultipleServicePacket.ParseResponse(
            new LogixCipResponse(0x8A, 0, Array.Empty<ushort>(), replyData));

        Assert.Equal(2, replies.Count);
        Assert.Equal(0x04, replies[0].GeneralStatus);
        Assert.Equal(0x00, replies[1].GeneralStatus);
    }

    [Fact]
    public void ParseResponse_RejectsMalformedOrOverlappingOffsets()
    {
        byte[] malformed =
        [
            0x02, 0x00,
            0x06, 0x00,
            0x06, 0x00,
            0xCC, 0x00, 0x00, 0x00
        ];

        Assert.Throws<InvalidDataException>(() => LogixMultipleServicePacket.ParseResponse(
            new LogixCipResponse(0x8A, 0, Array.Empty<ushort>(), malformed)));
    }
}
