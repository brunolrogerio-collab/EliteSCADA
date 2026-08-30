using System.Buffers.Binary;
using System.Text;

namespace Scada.Drivers.AllenBradley;

public sealed record LogixCipResponse(byte Service, byte GeneralStatus, IReadOnlyList<ushort> AdditionalStatus, byte[] Data)
{
    public bool Succeeded => GeneralStatus == 0;
}

public sealed class LogixCipException : IOException
{
    public LogixCipException(LogixProtocolError error, byte generalStatus, string message)
        : base(message)
    {
        Error = error;
        GeneralStatus = generalStatus;
    }

    public LogixProtocolError Error { get; }
    public byte GeneralStatus { get; }
}

public static class LogixCipCodec
{
    public const ushort RegisterSessionCommand = 0x0065;
    public const ushort UnregisterSessionCommand = 0x0066;
    public const ushort SendRrDataCommand = 0x006F;
    public const byte ReadTagService = 0x4C;
    public const byte WriteTagService = 0x4D;
    public const byte ReadTagFragmentedService = 0x52;
    public const byte WriteTagFragmentedService = 0x53;
    public const byte ReadModifyWriteService = 0x4E;
    public const byte MultipleServicePacketService = 0x0A;
    public const byte GetInstanceAttributeListService = 0x55;
    public const byte UnconnectedSendService = 0x52;

    private const byte AnsiExtendedSymbolSegment = 0x91;

    public static void ValidateSymbolName(string symbolName)
    {
        if (string.IsNullOrWhiteSpace(symbolName)) throw new ArgumentException("Logix symbol name is required.", nameof(symbolName));
        if (symbolName.Length > 255) throw new ArgumentOutOfRangeException(nameof(symbolName), "A single Logix symbolic segment cannot exceed 255 bytes in the first-cut encoder.");
        if (symbolName.Any(static c => c is '.' or '[' or ']' or ':' or ';'))
            throw new ArgumentException($"Logix symbol segment '{symbolName}' contains a reserved path character.", nameof(symbolName));
        if (symbolName.Any(static c => c > 0x7F))
            throw new ArgumentException("The first-cut Logix encoder accepts ASCII symbolic segments only.", nameof(symbolName));
    }

    public static void ValidateSymbolPath(string path) => ParseSymbolPath(path);

    public static byte[] EncodeSymbolicPath(LogixSymbolReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        reference.Validate();
        var encoded = new List<byte>();
        if (reference.Scope == LogixTagScope.Program)
            AppendAnsiSegment(encoded, $"Program:{reference.ProgramName}");
        foreach (var token in ParseSymbolPath(reference.SymbolPath))
        {
            AppendAnsiSegment(encoded, token.Name);
            foreach (var index in token.Indices) AppendArrayIndex(encoded, index);
        }
        if ((encoded.Count & 1) != 0) throw new InvalidDataException("Encoded CIP path must be word aligned.");
        if (encoded.Count / 2 > byte.MaxValue) throw new InvalidDataException("Encoded CIP symbolic path exceeds one-byte path-size limit.");
        return encoded.ToArray();
    }

    public static byte[] BuildReadTagRequest(LogixSymbolReference reference, ushort elementCount = 1)
    {
        if (elementCount == 0) throw new ArgumentOutOfRangeException(nameof(elementCount));
        var path = EncodeSymbolicPath(reference);
        var request = new byte[2 + path.Length + 2];
        request[0] = ReadTagService;
        request[1] = checked((byte)(path.Length / 2));
        path.CopyTo(request, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2 + path.Length), elementCount);
        return request;
    }

    public static byte[] BuildWriteTagRequest(LogixSymbolReference reference, object nativeValue)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!LogixValueCodec.IsFirstCutRuntimeWritable(reference.NativeType))
            throw new NotSupportedException($"Direct Logix writes for native type '{reference.NativeType}' are not enabled by the first-cut codec.");
        var path = EncodeSymbolicPath(reference);
        var valueBytes = LogixValueCodec.EncodeAtomic(reference.NativeType, nativeValue);
        var request = new byte[2 + path.Length + 4 + valueBytes.Length];
        request[0] = WriteTagService;
        request[1] = checked((byte)(path.Length / 2));
        path.CopyTo(request, 2);
        var offset = 2 + path.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(offset), LogixValueCodec.GetCipAtomicTypeCode(reference.NativeType));
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(offset + 2), 1);
        valueBytes.CopyTo(request, offset + 4);
        return request;
    }

    public static byte[] BuildControllerSymbolBrowseRequest(uint startInstance)
    {
        if (startInstance > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(startInstance), "The first-cut Symbol Object browse request supports 16-bit starting instance paths documented by the Logix Data Access manual.");
        var request = new byte[14];
        request[0] = GetInstanceAttributeListService;
        request[1] = 0x03;
        request[2] = 0x20;
        request[3] = 0x6B;
        request[4] = 0x25;
        request[5] = 0x00;
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(6, 2), checked((ushort)startInstance));
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(8, 2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(10, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(12, 2), 2);
        return request;
    }

    public static LogixSymbolBrowsePage ParseControllerSymbolBrowseResponse(LogixCipResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.GeneralStatus is not (0x00 or 0x06))
            ThrowIfFailed(response, "Symbol Object Get_Instance_Attribute_List");
        var data = response.Data.AsSpan();
        var symbols = new List<LogixBrowseSymbol>();
        var offset = 0;
        while (offset < data.Length)
        {
            if (data.Length - offset < 8)
                throw new InvalidDataException("Logix Symbol Object browse reply ended in a truncated symbol record.");
            var instance = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
            offset += 4;
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
            offset += 2;
            if (data.Length - offset < nameLength + 2)
                throw new InvalidDataException("Logix Symbol Object browse reply contains a truncated symbol name/type record.");
            var name = Encoding.ASCII.GetString(data.Slice(offset, nameLength));
            offset += nameLength;
            var symbolType = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
            offset += 2;
            symbols.Add(new LogixBrowseSymbol(instance, name, symbolType));
        }
        uint? next = null;
        if (response.GeneralStatus == 0x06 && symbols.Count > 0)
            next = symbols[^1].InstanceId == uint.MaxValue ? null : symbols[^1].InstanceId + 1u;
        return new LogixSymbolBrowsePage(symbols, next, response.GeneralStatus == 0x06);
    }

    public static byte[] BuildIdentityRequest() =>
        new byte[] { 0x01, 0x02, 0x20, 0x01, 0x24, 0x01 };

    public static byte[] BuildUnconnectedSend(byte[] embeddedRequest, IReadOnlyList<CipRouteSegment> route)
    {
        ArgumentNullException.ThrowIfNull(embeddedRequest);
        ArgumentNullException.ThrowIfNull(route);
        if (route.Count == 0) return embeddedRequest;
        var routeBytes = EncodeRoute(route);
        var paddedEmbeddedLength = embeddedRequest.Length + (embeddedRequest.Length & 1);
        var dataLength = 2 + 2 + paddedEmbeddedLength + 1 + 1 + routeBytes.Length;
        var request = new byte[6 + dataLength];
        request[0] = UnconnectedSendService;
        request[1] = 0x02;
        request[2] = 0x20;
        request[3] = 0x06;
        request[4] = 0x24;
        request[5] = 0x01;
        var offset = 6;
        request[offset++] = 0x0A;
        request[offset++] = 0x0E;
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(offset), checked((ushort)embeddedRequest.Length));
        offset += 2;
        embeddedRequest.CopyTo(request, offset);
        offset += paddedEmbeddedLength;
        request[offset++] = checked((byte)(routeBytes.Length / 2));
        request[offset++] = 0;
        routeBytes.CopyTo(request, offset);
        return request;
    }

    public static byte[] BuildRegisterSessionPayload()
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), 0);
        return payload;
    }

    public static byte[] BuildSendRrDataPayload(byte[] cipRequest)
    {
        ArgumentNullException.ThrowIfNull(cipRequest);
        if (cipRequest.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(cipRequest));
        var payload = new byte[16 + cipRequest.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6, 2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8, 2), 0x0000);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(12, 2), 0x00B2);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(14, 2), checked((ushort)cipRequest.Length));
        cipRequest.CopyTo(payload, 16);
        return payload;
    }

    public static byte[] ExtractCipFromSendRrData(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 16)
            throw new InvalidDataException("EtherNet/IP SendRRData response is truncated before the mandatory unconnected CPF items.");

        var interfaceHandle = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(0, 4));
        if (interfaceHandle != 0)
            throw new InvalidDataException($"EtherNet/IP SendRRData response uses unsupported interface handle 0x{interfaceHandle:X8}; CIP interface handle 0 is required.");

        var itemCount = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(6, 2));
        if (itemCount != 2)
            throw new InvalidDataException($"EtherNet/IP SendRRData unconnected response must contain exactly two CPF items, received {itemCount}.");

        var addressType = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(8, 2));
        var addressLength = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(10, 2));
        if (addressType != 0x0000 || addressLength != 0)
            throw new InvalidDataException($"EtherNet/IP SendRRData unconnected response requires a NULL Address Item; received type 0x{addressType:X4} length {addressLength}.");

        var dataType = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(12, 2));
        var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(14, 2));
        if (dataType != 0x00B2)
            throw new InvalidDataException($"EtherNet/IP SendRRData unconnected response requires Unconnected Data Item 0x00B2; received 0x{dataType:X4}.");

        var expectedLength = 16 + dataLength;
        if (payload.Length < expectedLength)
            throw new InvalidDataException("EtherNet/IP SendRRData unconnected data item is truncated.");
        if (payload.Length > expectedLength)
            throw new InvalidDataException("EtherNet/IP SendRRData response contains trailing bytes outside the declared unconnected data item.");

        return payload.Slice(16, dataLength).ToArray();
    }

    public static LogixCipResponse ParseResponse(ReadOnlySpan<byte> response)
    {
        if (response.Length < 4) throw new InvalidDataException("CIP response is truncated.");
        var service = response[0];
        var additionalWords = response[3];
        var statusBytes = additionalWords * 2;
        if (response.Length < 4 + statusBytes) throw new InvalidDataException("CIP additional status is truncated.");
        var additional = new ushort[additionalWords];
        for (var i = 0; i < additionalWords; i++)
            additional[i] = BinaryPrimitives.ReadUInt16LittleEndian(response.Slice(4 + (i * 2), 2));
        return new LogixCipResponse(service, response[2], additional, response[(4 + statusBytes)..].ToArray());
    }

    public static LogixCipResponse ParsePossiblyRoutedResponse(ReadOnlySpan<byte> response, bool routed, bool allowPartialTransfer = false)
    {
        var outer = ParseResponse(response);
        if (outer.GeneralStatus != 0 && !(allowPartialTransfer && outer.GeneralStatus == 0x06 && !routed))
            ThrowIfFailed(outer, routed ? "Unconnected Send" : "CIP request");
        if (!routed) return outer;
        if (outer.Data.Length < 4) throw new InvalidDataException("Routed CIP response did not contain an embedded message response.");
        var inner = ParseResponse(outer.Data);
        if (inner.GeneralStatus != 0 && !(allowPartialTransfer && inner.GeneralStatus == 0x06))
            ThrowIfFailed(inner, "Routed CIP request");
        return inner;
    }

    public static object ParseReadTagValue(LogixSymbolReference reference, LogixCipResponse response)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(response);
        ThrowIfFailed(response, $"Read Tag '{reference.StableIdentity}'");
        if (response.Data.Length < 2) throw new InvalidDataException("Read Tag response is missing the native type code.");
        var typeCode = BinaryPrimitives.ReadUInt16LittleEndian(response.Data.AsSpan(0, 2));
        var expected = LogixValueCodec.GetCipAtomicTypeCode(reference.NativeType);
        var typeMatches = reference.NativeType == LogixNativeType.Bool
            ? (typeCode & 0x00FF) == (expected & 0x00FF)
            : (typeCode & 0x0FFF) == (expected & 0x0FFF);
        if (!typeMatches)
            throw new LogixCipException(LogixProtocolError.TypeMismatch, response.GeneralStatus, $"Read Tag returned CIP type 0x{typeCode:X4}, expected the first-cut {reference.NativeType} type family.");
        return LogixValueCodec.DecodeAtomic(reference.NativeType, response.Data.AsSpan(2));
    }

    public static LogixControllerIdentity ParseIdentity(LogixCipResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        ThrowIfFailed(response, "Identity Object Get_Attributes_All");
        var data = response.Data.AsSpan();
        if (data.Length < 15) throw new InvalidDataException("CIP Identity Object response is truncated.");
        var vendor = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0, 2));
        var device = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2, 2));
        var product = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2));
        var major = data[6];
        var minor = data[7];
        var serial = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(10, 4));
        var nameLength = data[14];
        if (data.Length < 15 + nameLength) throw new InvalidDataException("CIP Identity product name is truncated.");
        var name = Encoding.ASCII.GetString(data.Slice(15, nameLength)).Trim();
        return new LogixControllerIdentity(vendor, device, product, major, minor, serial, string.IsNullOrWhiteSpace(name) ? "CIP target" : name);
    }

    public static void ThrowIfFailed(LogixCipResponse response, string operation)
    {
        if (response.Succeeded) return;
        var error = MapGeneralStatus(response.GeneralStatus);
        var additional = response.AdditionalStatus.Count == 0
            ? string.Empty
            : $" Additional: {string.Join(", ", response.AdditionalStatus.Select(static x => $"0x{x:X4}"))}.";
        throw new LogixCipException(error, response.GeneralStatus, $"{operation} failed with CIP general status 0x{response.GeneralStatus:X2}.{additional}");
    }

    public static LogixProtocolError MapGeneralStatus(byte status) => status switch
    {
        0x00 => LogixProtocolError.None,
        0x01 => LogixProtocolError.ProtocolFault,
        0x02 => LogixProtocolError.ControllerResourceUnavailable,
        0x04 => LogixProtocolError.SymbolNotFound,
        0x05 => LogixProtocolError.RouteRejected,
        0x08 => LogixProtocolError.ProtocolFault,
        0x09 => LogixProtocolError.ProtocolFault,
        0x0F => LogixProtocolError.AccessDenied,
        0x13 => LogixProtocolError.PacketTooLarge,
        0x14 => LogixProtocolError.SymbolNotFound,
        0x15 => LogixProtocolError.PacketTooLarge,
        0x1A => LogixProtocolError.PacketTooLarge,
        0x1B => LogixProtocolError.PacketTooLarge,
        0x1E => LogixProtocolError.ProtocolFault,
        0x20 => LogixProtocolError.TypeMismatch,
        0x26 => LogixProtocolError.RouteRejected,
        _ => LogixProtocolError.ProtocolFault
    };

    private static IReadOnlyList<SymbolToken> ParseSymbolPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Logix symbolic path is required.", nameof(path));
        var tokens = new List<SymbolToken>();
        var position = 0;
        while (position < path.Length)
        {
            var nameStart = position;
            while (position < path.Length && path[position] is not '.' and not '[') position++;
            var name = path[nameStart..position];
            ValidateSymbolName(name);
            var indices = new List<uint>();
            while (position < path.Length && path[position] == '[')
            {
                position++;
                var indexStart = position;
                while (position < path.Length && char.IsDigit(path[position])) position++;
                if (indexStart == position || position >= path.Length || path[position] != ']')
                    throw new ArgumentException($"Invalid Logix array index syntax in '{path}'.", nameof(path));
                if (!uint.TryParse(path[indexStart..position], out var index))
                    throw new ArgumentException($"Invalid Logix array index in '{path}'.", nameof(path));
                indices.Add(index);
                position++;
            }
            tokens.Add(new SymbolToken(name, indices));
            if (position == path.Length) break;
            if (path[position] != '.') throw new ArgumentException($"Invalid Logix symbolic path '{path}'.", nameof(path));
            position++;
            if (position == path.Length) throw new ArgumentException($"Logix symbolic path '{path}' cannot end with '.'.", nameof(path));
        }
        return tokens;
    }

    private static void AppendAnsiSegment(List<byte> destination, string name)
    {
        if (name.StartsWith("Program:", StringComparison.Ordinal))
        {
            var program = name["Program:".Length..];
            ValidateSymbolName(program);
        }
        else
        {
            ValidateSymbolName(name);
        }
        var bytes = Encoding.ASCII.GetBytes(name);
        if (bytes.Length > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(name));
        destination.Add(AnsiExtendedSymbolSegment);
        destination.Add((byte)bytes.Length);
        destination.AddRange(bytes);
        if ((bytes.Length & 1) != 0) destination.Add(0);
    }

    private static void AppendArrayIndex(List<byte> destination, uint index)
    {
        if (index <= byte.MaxValue)
        {
            destination.Add(0x28);
            destination.Add((byte)index);
        }
        else if (index <= ushort.MaxValue)
        {
            destination.Add(0x29);
            destination.Add(0);
            destination.Add((byte)index);
            destination.Add((byte)(index >> 8));
        }
        else
        {
            destination.Add(0x2A);
            destination.Add(0);
            var bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, index);
            destination.AddRange(bytes);
        }
    }

    private static byte[] EncodeRoute(IReadOnlyList<CipRouteSegment> route)
    {
        var bytes = new List<byte>(route.Count * 2);
        foreach (var segment in route)
        {
            if (segment.Port is 0 or > 14)
                throw new ArgumentOutOfRangeException(nameof(route), "The first-cut route encoder supports numeric one-byte port and link segments only.");
            bytes.Add(segment.Port);
            bytes.Add(segment.LinkAddress);
        }
        if ((bytes.Count & 1) != 0) bytes.Add(0);
        return bytes.ToArray();
    }

    private sealed record SymbolToken(string Name, IReadOnlyList<uint> Indices);
}
