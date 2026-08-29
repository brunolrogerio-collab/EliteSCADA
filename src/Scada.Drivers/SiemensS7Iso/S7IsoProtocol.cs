using System.Buffers.Binary;
using System.IO;

namespace Scada.Drivers.SiemensS7Iso;

internal sealed record S7IsoReadItemResult(
    S7IsoPoint Point,
    byte ReturnCode,
    byte[]? Data)
{
    public bool Succeeded => ReturnCode == S7IsoProtocol.ReturnCodeSuccess;
}

internal sealed class S7IsoProtocolException : IOException
{
    public S7IsoProtocolException(string message, byte? returnCode = null)
        : base(message)
    {
        ReturnCode = returnCode;
    }

    public byte? ReturnCode { get; }
}

internal static class S7IsoProtocol
{
    internal const byte ReturnCodeSuccess = 0xFF;

    private const int TpktHeaderLength = 4;
    private const int CotpDataLength = 3;
    private const int S7JobHeaderLength = 10;
    private const int S7AckDataHeaderLength = 12;
    private const int S7Offset = TpktHeaderLength + CotpDataLength;

    public static byte[] BuildConnectionRequest(S7IsoConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var packet = new byte[22];
        WriteTpktHeader(packet, packet.Length);
        packet[4] = 0x11;
        packet[5] = 0xE0;
        packet[6] = 0x00;
        packet[7] = 0x00;
        packet[8] = 0x00;
        packet[9] = 0x01;
        packet[10] = 0x00;
        packet[11] = 0xC1;
        packet[12] = 0x02;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(13, 2), options.EffectiveSourceTsap);
        packet[15] = 0xC2;
        packet[16] = 0x02;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(17, 2), options.EffectiveDestinationTsap);
        packet[19] = 0xC0;
        packet[20] = 0x01;
        packet[21] = 0x0A;
        return packet;
    }

    public static void ValidateConnectionConfirm(ReadOnlySpan<byte> packet)
    {
        ValidateTpkt(packet);
        if (packet.Length < 11 || packet[5] != 0xD0)
            throw new S7IsoProtocolException("S7 ISO peer did not return a valid COTP Connection Confirm.");
    }

    public static byte[] BuildSetupCommunication(ushort pduReference, ushort requestedPduSize)
    {
        var parameter = new byte[8];
        parameter[0] = 0xF0;
        parameter[1] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(parameter.AsSpan(2, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(parameter.AsSpan(4, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(parameter.AsSpan(6, 2), requestedPduSize);

        return BuildJobPacket(pduReference, parameter, ReadOnlySpan<byte>.Empty);
    }

    public static ushort ParseSetupCommunicationResponse(ReadOnlySpan<byte> packet, ushort pduReference)
    {
        ValidateAckData(packet, pduReference, out var parameterOffset, out var parameterLength, out _, out _);
        if (parameterLength < 8 || packet[parameterOffset] != 0xF0)
            throw new S7IsoProtocolException("Invalid S7 Setup Communication response.");

        var negotiated = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(parameterOffset + 6, 2));
        if (negotiated < 240)
            throw new S7IsoProtocolException($"S7 peer negotiated an unsupported PDU size of {negotiated} bytes.");
        return negotiated;
    }

    public static byte[] BuildReadRequest(ushort pduReference, IReadOnlyList<S7IsoPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count is < 1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(points), "S7 Read Var requires from 1 to 255 items.");

        var parameter = new byte[2 + points.Count * 12];
        parameter[0] = 0x04;
        parameter[1] = checked((byte)points.Count);
        var offset = 2;
        foreach (var point in points)
        {
            point.Validate();
            WriteVariableSpecification(parameter.AsSpan(offset, 12), point);
            offset += 12;
        }

        return BuildJobPacket(pduReference, parameter, ReadOnlySpan<byte>.Empty);
    }

    public static IReadOnlyList<S7IsoReadItemResult> ParseReadResponse(
        ReadOnlySpan<byte> packet,
        ushort pduReference,
        IReadOnlyList<S7IsoPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        ValidateAckData(packet, pduReference, out var parameterOffset, out var parameterLength, out var dataOffset, out var dataLength);

        if (parameterLength < 2 || packet[parameterOffset] != 0x04)
            throw new S7IsoProtocolException("Invalid S7 Read Var response parameters.");
        var itemCount = packet[parameterOffset + 1];
        if (itemCount != points.Count)
            throw new S7IsoProtocolException(
                $"S7 Read Var response contains {itemCount} item(s), expected {points.Count}.");

        var results = new List<S7IsoReadItemResult>(points.Count);
        var cursor = dataOffset;
        var dataEnd = checked(dataOffset + dataLength);

        for (var index = 0; index < points.Count; index++)
        {
            if (cursor + 4 > dataEnd)
                throw new S7IsoProtocolException("Truncated S7 Read Var item header.");

            var returnCode = packet[cursor];
            var transportSize = packet[cursor + 1];
            var encodedLength = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(cursor + 2, 2));
            cursor += 4;

            var payloadLength = returnCode == ReturnCodeSuccess
                ? DecodeResponsePayloadLength(transportSize, encodedLength)
                : encodedLength == 0
                    ? 0
                    : DecodeResponsePayloadLength(transportSize, encodedLength);
            if (cursor + payloadLength > dataEnd)
                throw new S7IsoProtocolException("Truncated S7 Read Var item payload.");

            byte[]? payload = null;
            if (returnCode == ReturnCodeSuccess)
                payload = packet.Slice(cursor, payloadLength).ToArray();

            results.Add(new S7IsoReadItemResult(points[index], returnCode, payload));
            cursor += payloadLength;

            if (index < points.Count - 1 && (payloadLength & 1) != 0)
            {
                if (cursor >= dataEnd)
                    throw new S7IsoProtocolException("Missing S7 Read Var item padding byte.");
                cursor++;
            }
        }

        return results;
    }

    public static byte[] BuildWriteRequest(ushort pduReference, S7IsoPoint point, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(point);
        point.Validate();
        if (data.Length != point.ByteLength)
            throw new ArgumentException(
                $"S7 write payload for '{point.Tag.Path}' has {data.Length} byte(s), expected {point.ByteLength}.",
                nameof(data));

        var parameter = new byte[14];
        parameter[0] = 0x05;
        parameter[1] = 0x01;
        WriteVariableSpecification(parameter.AsSpan(2, 12), point);

        var writeData = new byte[4 + data.Length];
        writeData[0] = 0x00;
        var dataTransportSize = WriteDataTransportSize(point);
        writeData[1] = dataTransportSize;
        var encodedLength = dataTransportSize is 0x03 or 0x06 or 0x07 or 0x09
            ? data.Length
            : checked(data.Length * 8);
        BinaryPrimitives.WriteUInt16BigEndian(writeData.AsSpan(2, 2), checked((ushort)encodedLength));
        data.CopyTo(writeData.AsSpan(4));

        return BuildJobPacket(pduReference, parameter, writeData);
    }

    public static void ParseWriteResponse(ReadOnlySpan<byte> packet, ushort pduReference)
    {
        ValidateAckData(packet, pduReference, out var parameterOffset, out var parameterLength, out var dataOffset, out var dataLength);
        if (parameterLength < 2 || packet[parameterOffset] != 0x05 || packet[parameterOffset + 1] != 0x01)
            throw new S7IsoProtocolException("Invalid S7 Write Var response parameters.");
        if (dataLength < 1)
            throw new S7IsoProtocolException("S7 Write Var response omitted the item return code.");

        var returnCode = packet[dataOffset];
        if (returnCode != ReturnCodeSuccess)
            throw new S7IsoProtocolException(
                $"S7 Write Var failed with return code 0x{returnCode:X2} ({DescribeReturnCode(returnCode)}).",
                returnCode);
    }

    public static string DescribeReturnCode(byte returnCode) => returnCode switch
    {
        0x01 => "hardware fault",
        0x03 => "access denied",
        0x05 => "address out of range",
        0x06 => "data type not supported",
        0x07 => "data type inconsistent",
        0x0A => "object does not exist",
        ReturnCodeSuccess => "success",
        _ => "unknown S7 item error"
    };

    private static byte WriteDataTransportSize(S7IsoPoint point) => point.ValueType switch
    {
        S7IsoValueType.Boolean => 0x03,
        S7IsoValueType.Int16 or S7IsoValueType.Int32 => 0x05,
        S7IsoValueType.Float32 => 0x07,
        _ => 0x04
    };

    private static byte[] BuildJobPacket(
        ushort pduReference,
        ReadOnlySpan<byte> parameter,
        ReadOnlySpan<byte> data)
    {
        var totalLength = checked(TpktHeaderLength + CotpDataLength + S7JobHeaderLength + parameter.Length + data.Length);
        var packet = new byte[totalLength];
        WriteTpktHeader(packet, totalLength);
        packet[4] = 0x02;
        packet[5] = 0xF0;
        packet[6] = 0x80;

        var s7 = S7Offset;
        packet[s7] = 0x32;
        packet[s7 + 1] = 0x01;
        packet[s7 + 2] = 0x00;
        packet[s7 + 3] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(s7 + 4, 2), pduReference);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(s7 + 6, 2), checked((ushort)parameter.Length));
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(s7 + 8, 2), checked((ushort)data.Length));

        parameter.CopyTo(packet.AsSpan(s7 + S7JobHeaderLength));
        data.CopyTo(packet.AsSpan(s7 + S7JobHeaderLength + parameter.Length));
        return packet;
    }

    private static void WriteVariableSpecification(Span<byte> destination, S7IsoPoint point)
    {
        destination[0] = 0x12;
        destination[1] = 0x0A;
        destination[2] = 0x10;
        destination[3] = point.S7AnyTransportSize;
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(4, 2), point.S7AnyElementCount);
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(6, 2), point.DbNumber);
        destination[8] = (byte)point.Area;

        var address = point.AddressInBits;
        destination[9] = (byte)((address >> 16) & 0xFF);
        destination[10] = (byte)((address >> 8) & 0xFF);
        destination[11] = (byte)(address & 0xFF);
    }

    private static int DecodeResponsePayloadLength(byte transportSize, ushort encodedLength) => transportSize switch
    {
        0x03 or 0x04 or 0x05 => (encodedLength + 7) / 8,
        0x06 or 0x07 or 0x09 => encodedLength,
        _ => throw new S7IsoProtocolException(
            $"Unsupported S7 response transport size 0x{transportSize:X2}.")
    };

    private static void ValidateAckData(
        ReadOnlySpan<byte> packet,
        ushort pduReference,
        out int parameterOffset,
        out int parameterLength,
        out int dataOffset,
        out int dataLength)
    {
        ValidateTpkt(packet);
        if (packet.Length < S7Offset + S7AckDataHeaderLength)
            throw new S7IsoProtocolException("Truncated S7 Ack Data packet.");
        if (packet[4] != 0x02 || packet[5] != 0xF0)
            throw new S7IsoProtocolException("Invalid COTP Data header in S7 response.");
        if (packet[S7Offset] != 0x32 || packet[S7Offset + 1] != 0x03)
            throw new S7IsoProtocolException("Expected S7 Ack Data response.");

        var responseReference = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(S7Offset + 4, 2));
        if (responseReference != pduReference)
            throw new S7IsoProtocolException(
                $"S7 PDU reference mismatch: received {responseReference}, expected {pduReference}.");

        parameterLength = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(S7Offset + 6, 2));
        dataLength = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(S7Offset + 8, 2));
        var errorClass = packet[S7Offset + 10];
        var errorCode = packet[S7Offset + 11];
        if (errorClass != 0 || errorCode != 0)
            throw new S7IsoProtocolException(
                $"S7 protocol error class 0x{errorClass:X2}, code 0x{errorCode:X2}.");

        parameterOffset = S7Offset + S7AckDataHeaderLength;
        dataOffset = checked(parameterOffset + parameterLength);
        if (dataOffset + dataLength > packet.Length)
            throw new S7IsoProtocolException("S7 Ack Data lengths exceed the TPKT payload.");
    }

    private static void WriteTpktHeader(Span<byte> packet, int totalLength)
    {
        packet[0] = 0x03;
        packet[1] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(packet.Slice(2, 2), checked((ushort)totalLength));
    }

    private static void ValidateTpkt(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < TpktHeaderLength || packet[0] != 0x03 || packet[1] != 0x00)
            throw new S7IsoProtocolException("Invalid RFC1006 TPKT header.");

        var declaredLength = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(2, 2));
        if (declaredLength != packet.Length)
            throw new S7IsoProtocolException(
                $"TPKT declared length {declaredLength} does not match received length {packet.Length}.");
    }
}

internal static class S7IsoBatchPlanner
{
    public static IReadOnlyList<IReadOnlyList<S7IsoPoint>> PlanReads(
        IReadOnlyList<S7IsoPoint> points,
        int negotiatedPduSize)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (negotiatedPduSize < 64)
            throw new ArgumentOutOfRangeException(nameof(negotiatedPduSize));

        var result = new List<IReadOnlyList<S7IsoPoint>>();
        var current = new List<S7IsoPoint>();

        foreach (var point in points)
        {
            point.Validate();
            if (current.Count > 0 && !Fits(current.Append(point), negotiatedPduSize))
            {
                result.Add(current.ToArray());
                current = new List<S7IsoPoint>();
            }

            current.Add(point);
            if (!Fits(current, negotiatedPduSize))
                throw new ArgumentException(
                    $"S7 point '{point.Tag.Path}' cannot fit inside negotiated PDU size {negotiatedPduSize}.",
                    nameof(points));

            if (current.Count == byte.MaxValue)
            {
                result.Add(current.ToArray());
                current = new List<S7IsoPoint>();
            }
        }

        if (current.Count > 0)
            result.Add(current.ToArray());

        return result;
    }

    private static bool Fits(IEnumerable<S7IsoPoint> points, int pduSize)
    {
        var array = points as S7IsoPoint[] ?? points.ToArray();
        if (array.Length == 0) return true;

        var requestPduLength = 10 + 2 + array.Length * 12;
        var responsePduLength = 12 + 2 + array.Sum(point => 4 + AlignEven(point.ByteLength));
        return requestPduLength <= pduSize && responsePduLength <= pduSize;
    }

    private static int AlignEven(int value) => (value & 1) == 0 ? value : value + 1;
}