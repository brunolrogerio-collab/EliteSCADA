using System.Buffers.Binary;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoHandshakeHardeningTests
{
    [Fact]
    public void ConnectionConfirm_AcceptsVariableHeaderWithMatchingDestinationReference()
    {
        var packet = new byte[]
        {
            0x03, 0x00, 0x00, 0x0E,
            0x09, 0xD0,
            0x00, 0x01,
            0x12, 0x34,
            0x00,
            0xC0, 0x01, 0x0A
        };

        S7IsoProtocol.ValidateConnectionConfirm(packet);
    }

    [Fact]
    public void ConnectionConfirm_RejectsMismatchedDestinationReference()
    {
        var packet = new byte[]
        {
            0x03, 0x00, 0x00, 0x0B,
            0x06, 0xD0,
            0x00, 0x02,
            0x00, 0x00,
            0x00
        };

        var error = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ValidateConnectionConfirm(packet));

        Assert.Contains("destination reference", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConnectionConfirm_RejectsHeaderLengthBeyondTpktPayload()
    {
        var packet = new byte[]
        {
            0x03, 0x00, 0x00, 0x0B,
            0x11, 0xD0,
            0x00, 0x01,
            0x00, 0x00,
            0x00
        };

        var error = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ValidateConnectionConfirm(packet));

        Assert.Contains("header length", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetupCommunication_RejectsNegotiatedPduAboveRequestedMaximum()
    {
        const ushort reference = 41;
        var response = AckData(
            reference,
            new byte[]
            {
                0xF0, 0x00,
                0x00, 0x01,
                0x00, 0x01,
                0x03, 0xC0
            },
            Array.Empty<byte>());

        var error = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ParseSetupCommunicationResponse(response, reference, requestedPduSize: 480));

        Assert.Contains("exceeding the requested maximum", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transport_PeerPduAboveRequestedMaximum_IsRejectedAsS7SessionFailure()
    {
        await using var server = new TestS7IsoServer(960);
        await using var transport = new S7IsoTransport(S7IsoTransportTests.Options(server.Port));

        var error = await Assert.ThrowsAsync<S7IsoProtocolException>(() => transport.ConnectAsync());

        Assert.Contains("requested maximum", error.Message, StringComparison.Ordinal);
        var diagnostics = transport.GetDiagnostics();
        Assert.False(diagnostics.Connected);
        Assert.Null(diagnostics.NegotiatedPduSize);
        Assert.Equal(0L, diagnostics.ConnectionCount);
        Assert.Equal(S7IsoFailureKind.S7SessionRejected, diagnostics.LastFailureKind);
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
}
