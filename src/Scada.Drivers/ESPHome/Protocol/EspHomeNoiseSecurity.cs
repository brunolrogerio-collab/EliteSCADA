using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Scada.Drivers.Abstractions;
using NoiseHandshakeState = Noise.HandshakeState;
using NoiseProtocol = Noise.Protocol;
using NoiseTransport = Noise.Transport;

namespace Scada.Drivers.ESPHome.Protocol;

public enum EspHomeNativeEncryptionMode
{
    Noise,
    Plaintext
}

public static class EspHomeNoiseContract
{
    public const string ProtocolName = "Noise_NNpsk0_25519_ChaChaPoly_SHA256";
    public const string ProtectedMaterialPurpose = "esphome.noise-api-key";
    public static ReadOnlySpan<byte> Prologue => "NoiseAPIInit\0\0"u8;
    public const int KeySize = 32;
    public const byte FrameMarker = 0x01;
    public const byte HandshakeSuccessPreamble = 0x00;
    public const byte HandshakeErrorPreamble = 0x01;
    public const byte SelectedProtocol = 0x01;
    public const int AuthenticationTagSize = 16;
    public const int TransportHeaderSize = 4;
}

public static class EspHomeSecurityPolicy
{
    public static void Validate(EspHomeNativeEncryptionMode mode, string? protectedKeyReference)
    {
        if (mode == EspHomeNativeEncryptionMode.Noise && string.IsNullOrWhiteSpace(protectedKeyReference))
            throw new InvalidOperationException("ESPHome Noise mode requires a protected encryption-key reference.");
    }

    public static Exception NoiseFailure(Exception cause) =>
        new CryptographicException("ESPHome Noise security negotiation failed closed; plaintext fallback is not permitted.", cause);
}

public sealed class EspHomeResolvedNoiseKey : IDisposable
{
    private byte[]? _key;

    internal EspHomeResolvedNoiseKey(byte[] key)
    {
        if (key.Length != EspHomeNoiseContract.KeySize)
            throw new ArgumentException("ESPHome Noise key must be 32 bytes.", nameof(key));
        _key = key;
    }

    internal byte[] CopyForHandshake()
    {
        ObjectDisposedException.ThrowIf(_key is null, this);
        return (byte[])_key!.Clone();
    }

    public override string ToString() => "ESPHome Noise key [REDACTED]";

    public void Dispose()
    {
        var key = Interlocked.Exchange(ref _key, null);
        if (key is not null)
            CryptographicOperations.ZeroMemory(key);
    }
}

public static class EspHomeNoiseKeyResolver
{
    public static async ValueTask<EspHomeResolvedNoiseKey> ResolveAsync(
        ICommunicationDriverProtectedMaterialResolver resolver,
        string projectKey,
        string dataSourceKey,
        string driverType,
        string protectedKeyReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var request = new CommunicationDriverProtectedMaterialRequest(
            projectKey,
            dataSourceKey,
            driverType,
            EspHomeNoiseContract.ProtectedMaterialPurpose,
            protectedKeyReference);
        request.Validate();

        await using var lease = await resolver.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        var source = lease.Material.ToArray();
        try
        {
            if (source.Length == EspHomeNoiseContract.KeySize &&
                string.Equals(lease.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
                return new EspHomeResolvedNoiseKey((byte[])source.Clone());

            var decoded = new byte[EspHomeNoiseContract.KeySize];
            var status = Base64.DecodeFromUtf8(source, decoded, out var consumed, out var written);
            if (status != System.Buffers.OperationStatus.Done ||
                consumed != source.Length ||
                written != EspHomeNoiseContract.KeySize)
            {
                CryptographicOperations.ZeroMemory(decoded);
                throw new CryptographicException("ESPHome protected encryption key is malformed.");
            }

            return new EspHomeResolvedNoiseKey(decoded);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(source);
        }
    }
}

public sealed record EspHomeNoiseServerHello(string NodeName, string MacAddress);

public readonly record struct EspHomeNoisePacket(ushort MessageType, ReadOnlyMemory<byte> Payload);

public sealed class EspHomeNoiseSession : IDisposable
{
    private NoiseHandshakeState? _handshake;
    private NoiseTransport? _transport;
    private bool _serverHelloAccepted;
    private bool _disposed;

    public EspHomeNoiseSession(EspHomeResolvedNoiseKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var psk = key.CopyForHandshake();
        try
        {
            var protocol = NoiseProtocol.Parse(EspHomeNoiseContract.ProtocolName.AsSpan());
            _handshake = protocol.Create(
                initiator: true,
                prologue: EspHomeNoiseContract.Prologue,
                psks: new[] { psk });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(psk);
        }
    }

    public bool IsReady => _transport is not null && !_disposed;

    public byte[] CreateClientHandshakeFrames()
    {
        ThrowIfDisposed();
        var handshake = _handshake ?? throw new InvalidOperationException("ESPHome Noise handshake is no longer available.");
        var messageBuffer = new byte[NoiseProtocol.MaxMessageLength];
        var (written, _, _) = handshake.WriteMessage(ReadOnlySpan<byte>.Empty, messageBuffer);

        var hello = BuildOuterFrame(ReadOnlySpan<byte>.Empty);
        var handshakeBody = new byte[written + 1];
        handshakeBody[0] = EspHomeNoiseContract.HandshakeSuccessPreamble;
        messageBuffer.AsSpan(0, written).CopyTo(handshakeBody.AsSpan(1));

        var framedHandshake = BuildOuterFrame(handshakeBody);
        var combined = new byte[hello.Length + framedHandshake.Length];
        hello.CopyTo(combined, 0);
        framedHandshake.CopyTo(combined, hello.Length);
        CryptographicOperations.ZeroMemory(messageBuffer);
        return combined;
    }

    public EspHomeNoiseServerHello AcceptServerHello(ReadOnlySpan<byte> frame)
    {
        ThrowIfDisposed();
        var body = ParseOuterFrame(frame);
        if (body.IsEmpty || body[0] != EspHomeNoiseContract.SelectedProtocol)
            throw new CryptographicException("ESPHome Noise server selected an unsupported protocol.");

        var firstNull = body[1..].IndexOf((byte)0);
        if (firstNull < 0)
            throw new InvalidDataException("ESPHome Noise ServerHello is missing the node-name terminator.");

        var nameEnd = firstNull + 1;
        var nodeName = Encoding.UTF8.GetString(body.Slice(1, firstNull));

        var macStart = nameEnd + 1;
        var macAddress = string.Empty;
        if (macStart < body.Length)
        {
            var secondNull = body[macStart..].IndexOf((byte)0);
            if (secondNull >= 0)
                macAddress = Encoding.UTF8.GetString(body.Slice(macStart, secondNull));
        }

        _serverHelloAccepted = true;
        return new EspHomeNoiseServerHello(nodeName, macAddress);
    }

    public void CompleteHandshake(ReadOnlySpan<byte> frame)
    {
        ThrowIfDisposed();
        if (!_serverHelloAccepted)
            throw new InvalidOperationException("ESPHome Noise ServerHello must be accepted before handshake completion.");

        var body = ParseOuterFrame(frame);
        if (body.IsEmpty)
            throw new CryptographicException("ESPHome Noise handshake response is empty.");
        if (body[0] != EspHomeNoiseContract.HandshakeSuccessPreamble)
        {
            var explanation = Encoding.UTF8.GetString(body[1..]);
            var cause = new CryptographicException(
                string.Equals(explanation, "Handshake MAC failure", StringComparison.Ordinal)
                    ? "ESPHome Noise authentication failed."
                    : "ESPHome Noise peer rejected the handshake.");
            throw EspHomeSecurityPolicy.NoiseFailure(cause);
        }

        var handshake = _handshake ?? throw new InvalidOperationException("ESPHome Noise handshake is no longer available.");
        try
        {
            var (_, _, transport) = handshake.ReadMessage(body[1..], Span<byte>.Empty);
            _transport = transport ?? throw new CryptographicException("ESPHome Noise handshake did not produce transport keys.");
            handshake.Dispose();
            _handshake = null;
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            throw EspHomeSecurityPolicy.NoiseFailure(ex);
        }
    }

    public byte[] EncryptFrame(ushort messageType, ReadOnlySpan<byte> payload)
    {
        ThrowIfDisposed();
        var transport = RequireTransport();
        if (payload.Length > ushort.MaxValue)
            throw new InvalidDataException("ESPHome Noise protobuf payload exceeds uint16 length.");

        var plaintextLength = checked(EspHomeNoiseContract.TransportHeaderSize + payload.Length);
        if (plaintextLength + EspHomeNoiseContract.AuthenticationTagSize > NoiseProtocol.MaxMessageLength)
            throw new InvalidDataException("ESPHome Noise transport payload exceeds Noise maximum message length.");

        var plaintext = new byte[plaintextLength];
        BinaryPrimitives.WriteUInt16BigEndian(plaintext.AsSpan(0, 2), messageType);
        BinaryPrimitives.WriteUInt16BigEndian(plaintext.AsSpan(2, 2), checked((ushort)payload.Length));
        payload.CopyTo(plaintext.AsSpan(4));

        var encrypted = new byte[plaintextLength + EspHomeNoiseContract.AuthenticationTagSize];
        try
        {
            var written = transport.WriteMessage(plaintext, encrypted);
            return BuildOuterFrame(encrypted.AsSpan(0, written));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    public EspHomeNoisePacket DecryptFrame(ReadOnlySpan<byte> frame)
    {
        ThrowIfDisposed();
        var transport = RequireTransport();
        var encrypted = ParseOuterFrame(frame);
        if (encrypted.Length < EspHomeNoiseContract.AuthenticationTagSize)
            throw new InvalidDataException("ESPHome Noise encrypted frame is shorter than its authentication tag.");

        var plaintext = new byte[encrypted.Length - EspHomeNoiseContract.AuthenticationTagSize];
        try
        {
            int read;
            try
            {
                read = transport.ReadMessage(encrypted, plaintext);
            }
            catch (CryptographicException ex)
            {
                throw new CryptographicException("ESPHome Noise transport authentication failed.", ex);
            }

            if (read < EspHomeNoiseContract.TransportHeaderSize)
                throw new InvalidDataException("ESPHome Noise decrypted frame is shorter than its message header.");

            var messageType = BinaryPrimitives.ReadUInt16BigEndian(plaintext.AsSpan(0, 2));
            var declaredLength = BinaryPrimitives.ReadUInt16BigEndian(plaintext.AsSpan(2, 2));
            var actualLength = read - EspHomeNoiseContract.TransportHeaderSize;
            if (declaredLength != actualLength)
                throw new InvalidDataException("ESPHome Noise decrypted payload length is inconsistent.");

            return new EspHomeNoisePacket(
                messageType,
                plaintext.AsMemory(EspHomeNoiseContract.TransportHeaderSize, actualLength).ToArray());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    internal static byte[] BuildOuterFrame(ReadOnlySpan<byte> body)
    {
        if (body.Length > ushort.MaxValue)
            throw new InvalidDataException("ESPHome Noise outer frame exceeds uint16 length.");

        var frame = new byte[3 + body.Length];
        frame[0] = EspHomeNoiseContract.FrameMarker;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(1, 2), checked((ushort)body.Length));
        body.CopyTo(frame.AsSpan(3));
        return frame;
    }

    internal static ReadOnlySpan<byte> ParseOuterFrame(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 3 || frame[0] != EspHomeNoiseContract.FrameMarker)
            throw new InvalidDataException("ESPHome Noise frame must start with 0x01 and contain a uint16 length.");

        var length = BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(1, 2));
        if (frame.Length != 3 + length)
            throw new InvalidDataException("ESPHome Noise frame length is inconsistent.");

        return frame[3..];
    }

    private NoiseTransport RequireTransport() =>
        _transport ?? throw new InvalidOperationException("ESPHome Noise session is not ready.");

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _handshake?.Dispose();
        _handshake = null;
        _transport?.Dispose();
        _transport = null;
    }
}
