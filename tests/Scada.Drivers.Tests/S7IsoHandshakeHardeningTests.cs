using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
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
    public void SetupCommunication_RejectsZeroParallelJobNegotiation()
    {
        const ushort reference = 42;
        var response = AckData(
            reference,
            new byte[]
            {
                0xF0, 0x00,
                0x00, 0x00,
                0x00, 0x01,
                0x01, 0xE0
            },
            Array.Empty<byte>());

        var error = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ParseSetupCommunicationResponse(response, reference, requestedPduSize: 480));

        Assert.Contains("zero parallel jobs", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SetupCommunication_RejectsFragmentedCotpData()
    {
        const ushort reference = 43;
        var response = AckData(
            reference,
            new byte[]
            {
                0xF0, 0x00,
                0x00, 0x01,
                0x00, 0x01,
                0x01, 0xE0
            },
            Array.Empty<byte>());
        response[6] = 0x00;

        var error = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ParseSetupCommunicationResponse(response, reference, requestedPduSize: 480));

        Assert.Contains("fragmented COTP", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SetupCommunication_RejectsTrailingBytesOutsideDeclaredS7Lengths()
    {
        const ushort reference = 44;
        var response = AckData(
            reference,
            new byte[]
            {
                0xF0, 0x00,
                0x00, 0x01,
                0x00, 0x01,
                0x01, 0xE0
            },
            Array.Empty<byte>());
        Array.Resize(ref response, response.Length + 1);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2, 2), checked((ushort)response.Length));

        var error = Assert.Throws<S7IsoProtocolException>(() =>
            S7IsoProtocol.ParseSetupCommunicationResponse(response, reference, requestedPduSize: 480));

        Assert.Contains("do not exactly match", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((ushort)239)]
    [InlineData((ushort)961)]
    public void SetupCommunication_RequestRejectsPduOutsideSupportedRange(ushort requestedPduSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            S7IsoProtocol.BuildSetupCommunication(1, requestedPduSize));
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

    [Fact]
    public async Task Transport_MismatchedCotpDestinationReference_IsIsoConnectionRejected()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var peer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var request = new byte[22];
            await ReadExactAsync(stream, request);
            await stream.WriteAsync(new byte[]
            {
                0x03, 0x00, 0x00, 0x0B,
                0x06, 0xD0,
                0x00, 0x02,
                0x00, 0x00,
                0x00
            });
            await stream.FlushAsync();
        });

        try
        {
            await using var transport = new S7IsoTransport(S7IsoTransportTests.Options(port));

            var error = await Assert.ThrowsAsync<S7IsoProtocolException>(() => transport.ConnectAsync());

            Assert.Contains("destination reference", error.Message, StringComparison.OrdinalIgnoreCase);
            var diagnostics = transport.GetDiagnostics();
            Assert.False(diagnostics.Connected);
            Assert.Equal(0L, diagnostics.ConnectionCount);
            Assert.Equal(S7IsoFailureKind.IsoConnectionRejected, diagnostics.LastFailureKind);
        }
        finally
        {
            listener.Stop();
            await peer;
        }
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

    private static async Task ReadExactAsync(NetworkStream stream, Memory<byte> destination)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await stream.ReadAsync(destination[offset..]);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}
