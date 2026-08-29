using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104ApciCodecTests
{
    [Fact]
    public void IFormatRoundTripPreservesSequencesAndAsdu()
    {
        byte[] asdu = [0x64, 0x01, 0x06];
        var frame = Iec104ApciFrame.I(3, 5, asdu);

        var encoded = Iec104ApciCodec.Serialize(frame);

        Assert.Equal(new byte[] { 0x68, 0x07, 0x06, 0x00, 0x0A, 0x00, 0x64, 0x01, 0x06 }, encoded);

        var decoded = Iec104ApciCodec.Parse(encoded);
        Assert.Equal(Iec104ApciFrameFormat.I, decoded.Format);
        Assert.Equal((ushort)3, decoded.SendSequence);
        Assert.Equal((ushort)5, decoded.ReceiveSequence);
        Assert.Equal(asdu, decoded.Asdu.ToArray());
    }

    [Fact]
    public void SFormatUsesReceiveSequenceOnly()
    {
        var encoded = Iec104ApciCodec.Serialize(Iec104ApciFrame.S(5));

        Assert.Equal(new byte[] { 0x68, 0x04, 0x01, 0x00, 0x0A, 0x00 }, encoded);

        var decoded = Iec104ApciCodec.Parse(encoded);
        Assert.Equal(Iec104ApciFrameFormat.S, decoded.Format);
        Assert.Equal((ushort)5, decoded.ReceiveSequence);
        Assert.True(decoded.Asdu.IsEmpty);
    }

    [Theory]
    [InlineData(Iec104UFunction.StartDataTransferActivation, 0x07)]
    [InlineData(Iec104UFunction.StartDataTransferConfirmation, 0x0B)]
    [InlineData(Iec104UFunction.StopDataTransferActivation, 0x13)]
    [InlineData(Iec104UFunction.StopDataTransferConfirmation, 0x23)]
    [InlineData(Iec104UFunction.TestFrameActivation, 0x43)]
    [InlineData(Iec104UFunction.TestFrameConfirmation, 0x83)]
    public void UFormatRoundTripUsesCanonicalControlByte(Iec104UFunction function, byte control)
    {
        var encoded = Iec104ApciCodec.Serialize(Iec104ApciFrame.U(function));

        Assert.Equal(new byte[] { 0x68, 0x04, control, 0x00, 0x00, 0x00 }, encoded);

        var decoded = Iec104ApciCodec.Parse(encoded);
        Assert.Equal(Iec104ApciFrameFormat.U, decoded.Format);
        Assert.Equal(function, decoded.UFunction);
    }

    [Fact]
    public void MaximumAsduLengthIsBoundedByApduLengthByte()
    {
        var maximum = new byte[Iec104ApciCodec.MaximumAsduLength];
        var encoded = Iec104ApciCodec.Serialize(Iec104ApciFrame.I(0, 0, maximum));

        Assert.Equal(255, encoded.Length);
        Assert.Equal((byte)Iec104ApciCodec.MaximumApduLength, encoded[1]);

        var tooLarge = new byte[Iec104ApciCodec.MaximumAsduLength + 1];
        Assert.Throws<ArgumentOutOfRangeException>(() => Iec104ApciCodec.Serialize(Iec104ApciFrame.I(0, 0, tooLarge)));
    }

    [Theory]
    [MemberData(nameof(InvalidFrames))]
    public void ParseRejectsMalformedFrames(byte[] frame)
    {
        Assert.Throws<Iec104ProtocolException>(() => Iec104ApciCodec.Parse(frame));
        Assert.False(Iec104ApciCodec.TryParse(frame, out var parsed));
        Assert.Null(parsed);
    }

    public static TheoryData<byte[]> InvalidFrames => new()
    {
        new byte[] { 0x67, 0x04, 0x01, 0x00, 0x00, 0x00 },
        new byte[] { 0x68, 0x05, 0x01, 0x00, 0x00, 0x00 },
        new byte[] { 0x68, 0x04, 0x03, 0x00, 0x00, 0x00 },
        new byte[] { 0x68, 0x04, 0x00, 0x00, 0x00, 0x00 }
    };
}
