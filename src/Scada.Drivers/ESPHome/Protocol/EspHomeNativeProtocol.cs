using Google.Protobuf;
using Scada.Drivers.ESPHome.Protocol.Generated;

namespace Scada.Drivers.ESPHome.Protocol;

public static class EspHomeNativeMessageId
{
    public const ushort HelloRequest = 1;
    public const ushort HelloResponse = 2;
    public const ushort AuthenticationRequestReserved = 3;
    public const ushort AuthenticationResponseReserved = 4;
    public const ushort DeviceInfoRequest = 9;
    public const ushort DeviceInfoResponse = 10;
    public const ushort DeviceCapabilitiesRequest = 149;
    public const ushort DeviceCapabilitiesResponse = 150;
}

public readonly record struct EspHomeApiVersion(uint Major, uint Minor) : IComparable<EspHomeApiVersion>
{
    public int CompareTo(EspHomeApiVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);

    public override string ToString() => $"{Major}.{Minor}";
}

public readonly record struct EspHomePlaintextFrame(ushort MessageType, ReadOnlyMemory<byte> Payload);

public static class EspHomePlaintextFrameCodec
{
    public const int DefaultMaximumPayloadBytes = 4 * 1024 * 1024;

    public static byte[] Encode(ushort messageType, IMessage message, int maximumPayloadBytes = DefaultMaximumPayloadBytes)
    {
        ArgumentNullException.ThrowIfNull(message);
        var payload = message.ToByteArray();
        if (payload.Length > maximumPayloadBytes)
            throw new InvalidDataException($"ESPHome payload length {payload.Length} exceeds configured maximum {maximumPayloadBytes}.");

        using var output = new MemoryStream(payload.Length + 12);
        output.WriteByte(0);
        WriteVarUInt32(output, checked((uint)payload.Length));
        WriteVarUInt32(output, messageType);
        output.Write(payload);
        return output.ToArray();
    }

    public static EspHomePlaintextFrame Decode(ReadOnlySpan<byte> buffer, out int consumed, int maximumPayloadBytes = DefaultMaximumPayloadBytes)
    {
        consumed = 0;
        if (buffer.IsEmpty || buffer[0] != 0)
            throw new InvalidDataException("ESPHome plaintext frame must start with 0x00.");

        var offset = 1;
        var payloadLength = ReadVarUInt32(buffer, ref offset);
        if (payloadLength > maximumPayloadBytes)
            throw new InvalidDataException($"ESPHome payload length {payloadLength} exceeds configured maximum {maximumPayloadBytes}.");

        var messageType = ReadVarUInt32(buffer, ref offset);
        if (messageType > ushort.MaxValue)
            throw new InvalidDataException($"ESPHome message type {messageType} exceeds uint16 range.");

        var required = checked(offset + (int)payloadLength);
        if (buffer.Length < required)
            throw new EndOfStreamException("ESPHome plaintext frame is truncated.");

        var payload = buffer.Slice(offset, (int)payloadLength).ToArray();
        consumed = required;
        return new EspHomePlaintextFrame((ushort)messageType, payload);
    }

    private static uint ReadVarUInt32(ReadOnlySpan<byte> buffer, ref int offset)
    {
        uint value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            if (offset >= buffer.Length)
                throw new EndOfStreamException("ESPHome varint is truncated.");
            var current = buffer[offset++];
            if (shift == 28 && (current & 0xF0) != 0)
                throw new InvalidDataException("ESPHome varint exceeds uint32 range.");
            value |= (uint)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return value;
        }
        throw new InvalidDataException("ESPHome varint is malformed.");
    }

    private static void WriteVarUInt32(Stream output, uint value)
    {
        while (value >= 0x80)
        {
            output.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        output.WriteByte((byte)value);
    }
}

public static class EspHomeNativeHandshake
{
    public static readonly EspHomeApiVersion ClientVersion = new(1, 15);

    public static HelloRequest CreateHello(string clientInfo = "EliteSCADA") => new()
    {
        ClientInfo = clientInfo,
        ApiVersionMajor = ClientVersion.Major,
        ApiVersionMinor = ClientVersion.Minor
    };

    public static EspHomeApiVersion Negotiate(HelloResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.ApiVersionMajor != ClientVersion.Major)
            throw new NotSupportedException($"ESPHome API major {response.ApiVersionMajor} is incompatible with client major {ClientVersion.Major}.");
        return new EspHomeApiVersion(response.ApiVersionMajor, Math.Min(response.ApiVersionMinor, ClientVersion.Minor));
    }

    public static bool SupportsDeviceCapabilities(EspHomeApiVersion negotiated) =>
        negotiated.Major == 1 && negotiated.Minor >= 15;
}

public sealed record EspHomeDeviceIdentity(
    string StableDeviceIdentity,
    string OriginalMacAddress,
    string Name,
    string FriendlyName,
    string ESPHomeVersion,
    string CompilationTime,
    string Manufacturer,
    string Model,
    string ProjectName,
    string ProjectVersion,
    bool HasDeepSleep);

public static class EspHomeDeviceInfoMapper
{
    public static EspHomeDeviceIdentity Map(DeviceInfoResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var normalizedMac = NormalizeMac(response.MacAddress);
        return new EspHomeDeviceIdentity(
            normalizedMac,
            response.MacAddress,
            response.Name,
            response.FriendlyName,
            response.EsphomeVersion,
            response.CompilationTime,
            response.Manufacturer,
            response.Model,
            response.ProjectName,
            response.ProjectVersion,
            response.HasDeepSleep);
    }

    public static string NormalizeMac(string? macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            throw new InvalidDataException("DEVICE_IDENTITY_UNAVAILABLE: ESPHome DeviceInfoResponse.mac_address is empty.");

        Span<char> hex = stackalloc char[12];
        var count = 0;
        foreach (var ch in macAddress)
        {
            if (ch is ':' or '-' or '.' or ' ') continue;
            if (!Uri.IsHexDigit(ch) || count >= hex.Length)
                throw new InvalidDataException($"DEVICE_IDENTITY_UNAVAILABLE: invalid ESPHome MAC address '{macAddress}'.");
            hex[count++] = char.ToUpperInvariant(ch);
        }
        if (count != 12)
            throw new InvalidDataException($"DEVICE_IDENTITY_UNAVAILABLE: invalid ESPHome MAC address '{macAddress}'.");

        return string.Create(17, hex.ToArray(), static (span, chars) =>
        {
            var source = 0;
            for (var i = 0; i < span.Length; i++)
                span[i] = i % 3 == 2 ? ':' : chars[source++];
        });
    }
}
