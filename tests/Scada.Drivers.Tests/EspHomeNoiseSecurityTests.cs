using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Noise;
using Scada.Drivers.Abstractions;
using Scada.Drivers.ESPHome.Protocol;

namespace Scada.Drivers.Tests;

public sealed class EspHomeNoiseSecurityTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();

    [Fact]
    public void ProtocolDependency_SupportsExactEspHomeSuite()
    {
        var protocol = Protocol.Parse(EspHomeNoiseContract.ProtocolName.AsSpan());
        using var initiator = protocol.Create(true, EspHomeNoiseContract.Prologue, psks: new[] { (byte[])Key.Clone() });
        Assert.NotNull(initiator);
    }

    [Fact]
    public void NoiseHandshake_AndTransport_RoundTrip_WithCurrentEspHomeFraming()
    {
        using var key = new EspHomeResolvedNoiseKey((byte[])Key.Clone());
        using var client = new EspHomeNoiseSession(key);

        var clientFrames = client.CreateClientHandshakeFrames();
        Assert.Equal(new byte[] { 1, 0, 0 }, clientFrames[..3]);

        var secondLength = BinaryPrimitives.ReadUInt16BigEndian(clientFrames.AsSpan(4, 2));
        var clientHandshakeBody = clientFrames.AsSpan(6, secondLength);
        Assert.Equal((byte)0, clientHandshakeBody[0]);

        var protocol = Protocol.Parse(EspHomeNoiseContract.ProtocolName.AsSpan());
        using var responder = protocol.Create(false, EspHomeNoiseContract.Prologue, psks: new[] { (byte[])Key.Clone() });
        responder.ReadMessage(clientHandshakeBody[1..], Span<byte>.Empty);

        var responderMessage = new byte[Protocol.MaxMessageLength];
        var (_, _, responderTransport) = responder.WriteMessage(ReadOnlySpan<byte>.Empty, responderMessage);
        Assert.NotNull(responderTransport);
        using var serverTransport = responderTransport!;

        var serverHelloBody = new byte[] { 1 }
            .Concat(Encoding.UTF8.GetBytes("fixture\0AA:BB:CC:DD:EE:FF\0"))
            .ToArray();
        var serverHello = EspHomeNoiseSession.BuildOuterFrame(serverHelloBody);
        var hello = client.AcceptServerHello(serverHello);
        Assert.Equal("fixture", hello.NodeName);
        Assert.Equal("AA:BB:CC:DD:EE:FF", hello.MacAddress);

        var serverHandshakeBody = new byte[1 + responderMessage.Length];
        serverHandshakeBody[0] = 0;
        var handshakeLength = responderMessage.AsSpan().IndexOfAnyExcept((byte)0);
        // NNpsk0 responder output length is fixed by the library result; derive it by redoing the write below is not safe.
        // The actual message occupies 48 bytes for NNpsk0/25519/ChaChaPoly/SHA256 with an empty payload.
        const int responderHandshakeLength = 48;
        responderMessage.AsSpan(0, responderHandshakeLength).CopyTo(serverHandshakeBody.AsSpan(1));
        client.CompleteHandshake(EspHomeNoiseSession.BuildOuterFrame(serverHandshakeBody.AsSpan(0, 1 + responderHandshakeLength)));
        Assert.True(client.IsReady);

        var payload = new byte[] { 1, 2, 3, 4 };
        var clientEncrypted = client.EncryptFrame(10, payload);
        var clientCiphertext = EspHomeNoiseSession.ParseOuterFrame(clientEncrypted);
        var serverPlaintext = new byte[clientCiphertext.Length - 16];
        var serverRead = serverTransport.ReadMessage(clientCiphertext, serverPlaintext);
        Assert.Equal(8, serverRead);
        Assert.Equal((ushort)10, BinaryPrimitives.ReadUInt16BigEndian(serverPlaintext.AsSpan(0, 2)));
        Assert.Equal(payload, serverPlaintext.AsSpan(4, 4).ToArray());

        var serverPlain = new byte[7];
        BinaryPrimitives.WriteUInt16BigEndian(serverPlain.AsSpan(0, 2), 2);
        BinaryPrimitives.WriteUInt16BigEndian(serverPlain.AsSpan(2, 2), 3);
        serverPlain.AsSpan(4).Fill(9);
        var serverCipher = new byte[serverPlain.Length + 16];
        var serverWritten = serverTransport.WriteMessage(serverPlain, serverCipher);
        var packet = client.DecryptFrame(EspHomeNoiseSession.BuildOuterFrame(serverCipher.AsSpan(0, serverWritten)));
        Assert.Equal((ushort)2, packet.MessageType);
        Assert.Equal(new byte[] { 9, 9, 9 }, packet.Payload.ToArray());
    }

    [Fact]
    public void HandshakeMacFailure_FailsClosed_WithoutPlaintextFallback()
    {
        using var key = new EspHomeResolvedNoiseKey((byte[])Key.Clone());
        using var client = new EspHomeNoiseSession(key);
        _ = client.CreateClientHandshakeFrames();
        _ = client.AcceptServerHello(EspHomeNoiseSession.BuildOuterFrame(new byte[] { 1, (byte)'x', 0 }));

        var rejection = EspHomeNoiseSession.BuildOuterFrame(
            new byte[] { 1 }.Concat(Encoding.UTF8.GetBytes("Handshake MAC failure")).ToArray());

        var ex = Assert.Throws<CryptographicException>(() => client.CompleteHandshake(rejection));
        Assert.Contains("failed closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(client.IsReady);
        Assert.Throws<InvalidOperationException>(() => client.EncryptFrame(1, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void SecurityPolicy_NoiseFailureNeverChangesMode()
    {
        var failure = EspHomeSecurityPolicy.NoiseFailure(new CryptographicException("bad key"));
        Assert.IsType<CryptographicException>(failure);
        Assert.Contains("plaintext fallback is not permitted", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => EspHomeSecurityPolicy.Validate(EspHomeNativeEncryptionMode.Noise, null));
        EspHomeSecurityPolicy.Validate(EspHomeNativeEncryptionMode.Plaintext, null);
    }

    [Fact]
    public async Task ProtectedKeyResolver_UsesScopedAuthority_AndDoesNotExposeKey()
    {
        var encoded = Encoding.UTF8.GetBytes(Convert.ToBase64String(Key));
        var resolver = new CaptureResolver(encoded);
        using var resolved = await EspHomeNoiseKeyResolver.ResolveAsync(
            resolver, "project-a", "source-a", "builtin.esphome", "secret-ref");

        Assert.Equal("ESPHome Noise key [REDACTED]", resolved.ToString());
        Assert.Equal("esphome.noise-api-key", resolver.Request!.Purpose);
        Assert.Equal("secret-ref", resolver.Request.Reference);
        Assert.True(resolver.LeaseDisposed);
    }

    [Fact]
    public async Task ProtectedKeyResolver_MalformedSecretIsRedacted()
    {
        var secret = Encoding.UTF8.GetBytes("this-is-not-a-valid-api-key");
        var resolver = new CaptureResolver(secret);
        var ex = await Assert.ThrowsAsync<CryptographicException>(async () =>
        {
            using var _ = await EspHomeNoiseKeyResolver.ResolveAsync(
                resolver, "project-a", "source-a", "builtin.esphome", "secret-ref");
        });
        Assert.DoesNotContain("this-is-not", ex.ToString(), StringComparison.Ordinal);
        Assert.True(resolver.LeaseDisposed);
    }

    [Fact]
    public void NoiseOuterFrame_RejectsMalformedAndTruncatedPackets()
    {
        Assert.Throws<InvalidDataException>(() => EspHomeNoiseSession.ParseOuterFrame(new byte[] { 0, 0, 0 }));
        Assert.Throws<InvalidDataException>(() => EspHomeNoiseSession.ParseOuterFrame(new byte[] { 1, 0, 2, 7 }));
    }

    [Fact]
    public void NoiseKey_ToStringIsAlwaysRedacted_AndDisposeIsTerminal()
    {
        var key = new EspHomeResolvedNoiseKey((byte[])Key.Clone());
        Assert.DoesNotContain(Convert.ToBase64String(Key), key.ToString(), StringComparison.Ordinal);
        key.Dispose();
        Assert.Throws<ObjectDisposedException>(() => key.CopyForHandshake());
    }

    private sealed class CaptureResolver(byte[] material) : ICommunicationDriverProtectedMaterialResolver
    {
        public CommunicationDriverProtectedMaterialRequest? Request { get; private set; }
        public bool LeaseDisposed { get; private set; }

        public ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
            CommunicationDriverProtectedMaterialRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            return ValueTask.FromResult<ICommunicationDriverProtectedMaterialLease>(new Lease(this, material));
        }

        private sealed class Lease(CaptureResolver owner, byte[] material) : ICommunicationDriverProtectedMaterialLease
        {
            public ReadOnlyMemory<byte> Material { get; } = material;
            public string? ContentType => "text/plain";

            public ValueTask DisposeAsync()
            {
                owner.LeaseDisposed = true;
                return ValueTask.CompletedTask;
            }
        }
    }
}
