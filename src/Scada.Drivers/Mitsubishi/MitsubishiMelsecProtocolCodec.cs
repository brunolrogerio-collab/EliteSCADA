using System.Buffers.Binary;

namespace Scada.Drivers.Mitsubishi;

/// <summary>QnA-compatible binary 3E framing and bounded device-access commands.</summary>
public static class MitsubishiMelsecProtocolCodec
{
    public const int HeaderLength = 9;
    public const byte RequestSubheader0 = 0x50;
    public const byte ResponseSubheader0 = 0xD0;
    public const ushort BatchReadCommand = 0x0401;
    public const ushort BatchWriteCommand = 0x1401;
    public const ushort ReadRandomCommand = 0x0403;
    public const ushort ReadBlockCommand = 0x0406;
    public const ushort ReadTypeNameCommand = 0x0101;

    public static byte[] BuildBatchRead(
        MitsubishiMelsecRoute route,
        ushort monitoringTimerUnits,
        MitsubishiMelsecAddress start,
        ushort pointCount,
        bool bitUnits)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(start);
        if (pointCount == 0 || pointCount > (bitUnits ? 7168 : 960))
            throw new ArgumentOutOfRangeException(nameof(pointCount), "Batch Read count exceeds the documented Q/L-compatible binary 3E bound.");
        if (start.IsBitDevice != bitUnits)
            throw new ArgumentException("The selected device area does not match the requested bit/word unit.", nameof(bitUnits));
        Span<byte> data = stackalloc byte[6];
        WriteDeviceNumber(data, start.Number);
        data[3] = start.DeviceCode;
        BinaryPrimitives.WriteUInt16LittleEndian(data[4..], pointCount);
        return BuildRequest(route, monitoringTimerUnits, BatchReadCommand, bitUnits ? (ushort)1 : (ushort)0, data);
    }

    public static byte[] BuildBatchWrite(
        MitsubishiMelsecRoute route,
        ushort monitoringTimerUnits,
        MitsubishiMelsecAddress start,
        ushort pointCount,
        bool bitUnits,
        ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(start);
        if (pointCount == 0 || pointCount > (bitUnits ? 7168 : 960))
            throw new ArgumentOutOfRangeException(nameof(pointCount), "Batch Write count exceeds the documented Q/L-compatible binary 3E bound.");
        if (start.IsBitDevice != bitUnits)
            throw new ArgumentException("The selected device area does not match the requested bit/word unit.", nameof(bitUnits));
        var expectedLength = bitUnits ? (pointCount + 1) / 2 : pointCount * 2;
        if (payload.Length != expectedLength)
            throw new ArgumentException($"Batch Write requires {expectedLength} payload bytes for {pointCount} points.", nameof(payload));
        var data = new byte[6 + payload.Length];
        WriteDeviceNumber(data, start.Number);
        data[3] = start.DeviceCode;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), pointCount);
        payload.CopyTo(data.AsSpan(6));
        return BuildRequest(route, monitoringTimerUnits, BatchWriteCommand, bitUnits ? (ushort)1 : (ushort)0, data);
    }

    public static byte[] BuildReadTypeName(MitsubishiMelsecRoute route, ushort monitoringTimerUnits) =>
        BuildRequest(route, monitoringTimerUnits, ReadTypeNameCommand, 0, ReadOnlySpan<byte>.Empty);

    public static byte[] BuildReadRandom(
        MitsubishiMelsecRoute route,
        ushort monitoringTimerUnits,
        IReadOnlyList<MitsubishiMelsecAddress> wordPoints,
        IReadOnlyList<MitsubishiMelsecAddress> doubleWordPoints)
    {
        ArgumentNullException.ThrowIfNull(wordPoints);
        ArgumentNullException.ThrowIfNull(doubleWordPoints);
        if (wordPoints.Count + doubleWordPoints.Count is < 1 or > 192)
            throw new ArgumentOutOfRangeException(nameof(wordPoints), "Read Random supports 1..192 word and double-word points in the selected common profile.");
        if (wordPoints.Any(static point => point.IsBitDevice) || doubleWordPoints.Any(static point => point.IsBitDevice))
            throw new ArgumentException("Read Random accepts word devices only.");
        var data = new byte[2 + (wordPoints.Count + doubleWordPoints.Count) * 4];
        data[0] = checked((byte)wordPoints.Count);
        data[1] = checked((byte)doubleWordPoints.Count);
        var offset = 2;
        foreach (var point in wordPoints)
        {
            WriteDeviceNumber(data.AsSpan(offset), point.Number);
            data[offset + 3] = point.DeviceCode;
            offset += 4;
        }
        foreach (var point in doubleWordPoints)
        {
            WriteDeviceNumber(data.AsSpan(offset), point.Number);
            data[offset + 3] = point.DeviceCode;
            offset += 4;
        }
        return BuildRequest(route, monitoringTimerUnits, ReadRandomCommand, 0, data);
    }

    public static byte[] BuildReadBlock(
        MitsubishiMelsecRoute route,
        ushort monitoringTimerUnits,
        IReadOnlyList<MitsubishiMelsecReadBlock> wordBlocks)
    {
        ArgumentNullException.ThrowIfNull(wordBlocks);
        if (wordBlocks.Count is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(wordBlocks), "Read Block supports 1..120 word blocks in the selected common profile.");
        var totalPoints = wordBlocks.Sum(block => block.PointCount);
        if (totalPoints > 960)
            throw new ArgumentOutOfRangeException(nameof(wordBlocks), "Read Block total word points exceed the documented Q/L-compatible bound.");
        if (wordBlocks.Any(block => block.StartAddress.IsBitDevice || block.PointCount == 0))
            throw new ArgumentException("The v1 Read Block path accepts non-empty word-device blocks only.", nameof(wordBlocks));
        var data = new byte[2 + wordBlocks.Count * 6];
        data[0] = checked((byte)wordBlocks.Count);
        data[1] = 0; // bit-device block count; bit blocks are intentionally not optimized in v1.
        var offset = 2;
        foreach (var block in wordBlocks)
        {
            WriteDeviceNumber(data.AsSpan(offset), block.StartAddress.Number);
            data[offset + 3] = block.StartAddress.DeviceCode;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 4), block.PointCount);
            offset += 6;
        }
        return BuildRequest(route, monitoringTimerUnits, ReadBlockCommand, 0, data);
    }

    public static byte[] BuildRequest(
        MitsubishiMelsecRoute route,
        ushort monitoringTimerUnits,
        ushort command,
        ushort subcommand,
        ReadOnlySpan<byte> commandData)
    {
        ArgumentNullException.ThrowIfNull(route);
        var length = checked(6 + commandData.Length); // timer + command + subcommand + request data
        if (length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(commandData));
        var frame = new byte[HeaderLength + length];
        frame[0] = RequestSubheader0;
        frame[1] = 0x00;
        frame[2] = route.NetworkNo;
        frame[3] = route.StationNo;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), route.ModuleIoNo);
        frame[6] = route.MultidropStationNo;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(7), checked((ushort)length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(9), monitoringTimerUnits);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(11), command);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(13), subcommand);
        commandData.CopyTo(frame.AsSpan(15));
        return frame;
    }

    public static MitsubishiMelsecDecodedResponse DecodeResponse(
        ReadOnlySpan<byte> frame,
        MitsubishiMelsecRoute expectedRoute,
        ushort? expectedDataLength = null)
    {
        ArgumentNullException.ThrowIfNull(expectedRoute);
        if (frame.Length < HeaderLength + 2)
            throw new MitsubishiMelsecProtocolException("SLMP response is shorter than its fixed header and end code.", "truncated_frame");
        if (frame[0] != ResponseSubheader0 || frame[1] != 0x00)
            throw new MitsubishiMelsecProtocolException("SLMP response subheader is invalid.", "invalid_subheader");
        var route = new MitsubishiMelsecRoute(
            frame[2], frame[3], BinaryPrimitives.ReadUInt16LittleEndian(frame[4..]), frame[6]);
        if (route != expectedRoute)
            throw new MitsubishiMelsecProtocolException("SLMP response routing fields do not match the outstanding request.", "route_mismatch");
        var length = BinaryPrimitives.ReadUInt16LittleEndian(frame[7..]);
        if (length < 2 || frame.Length != HeaderLength + length)
            throw new MitsubishiMelsecProtocolException("SLMP response length is inconsistent with the received frame.", "invalid_length");
        var endCode = BinaryPrimitives.ReadUInt16LittleEndian(frame[9..]);
        if (endCode != 0)
            throw new MitsubishiMelsecProtocolException(
                $"MELSEC returned end code 0x{endCode:X4}.", "plc_end_code", endCode);
        var data = frame[11..].ToArray();
        if (expectedDataLength.HasValue && data.Length != expectedDataLength.Value)
            throw new MitsubishiMelsecProtocolException(
                $"SLMP response data length was {data.Length}; expected {expectedDataLength.Value}.", "unexpected_data_length");
        return new MitsubishiMelsecDecodedResponse(route, endCode, data);
    }

    public static int ExpectedBatchReadDataLength(ushort pointCount, bool bitUnits) =>
        bitUnits ? (pointCount + 1) / 2 : checked(pointCount * 2);

    public static bool DecodePackedBit(ReadOnlySpan<byte> packed, int index)
    {
        if (index < 0 || index / 2 >= packed.Length) throw new ArgumentOutOfRangeException(nameof(index));
        var nibble = (index & 1) == 0 ? (packed[index / 2] & 0x0F) : (packed[index / 2] >> 4);
        if (nibble is not (0x0 or 0x1))
            throw new MitsubishiMelsecProtocolException("SLMP bit-unit response contains a value other than 0 or 1.", "invalid_bit_value");
        return nibble == 1;
    }

    public static byte[] EncodePackedBits(ReadOnlySpan<bool> values)
    {
        var bytes = new byte[(values.Length + 1) / 2];
        for (var i = 0; i < values.Length; i++)
            if (values[i]) bytes[i / 2] |= (byte)((i & 1) == 0 ? 0x01 : 0x10);
        return bytes;
    }

    private static void WriteDeviceNumber(Span<byte> destination, int number)
    {
        if (destination.Length < 3 || number is < 0 or > MitsubishiMelsecAddress.MaximumWireAddress)
            throw new ArgumentOutOfRangeException(nameof(number));
        destination[0] = (byte)number;
        destination[1] = (byte)(number >> 8);
        destination[2] = (byte)(number >> 16);
    }
}

public sealed record MitsubishiMelsecDecodedResponse(
    MitsubishiMelsecRoute Route,
    ushort EndCode,
    byte[] Data);

public sealed record MitsubishiMelsecReadBlock(MitsubishiMelsecAddress StartAddress, ushort PointCount);
