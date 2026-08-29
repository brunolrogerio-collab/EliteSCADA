using System.Buffers.Binary;

namespace Scada.Drivers.AllenBradley;

public sealed record LogixReadFragment(
    uint ByteOffset,
    ushort TypeCode,
    byte[] Payload,
    bool HasMore);

public static class LogixFragmentedRead
{
    public const int DefaultMaximumValueBytes = 1024 * 1024;
    public const int HardMaximumValueBytes = 16 * 1024 * 1024;

    public static byte[] BuildRequest(
        LogixSymbolReference reference,
        ushort elementCount,
        uint byteOffset)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (elementCount == 0) throw new ArgumentOutOfRangeException(nameof(elementCount));
        EnsureSupportedAtomicArrayType(reference.NativeType);

        var path = LogixCipCodec.EncodeSymbolicPath(reference);
        var request = new byte[2 + path.Length + 6];
        request[0] = LogixCipCodec.ReadTagFragmentedService;
        request[1] = checked((byte)(path.Length / 2));
        path.CopyTo(request, 2);
        var dataOffset = 2 + path.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(dataOffset, 2), elementCount);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(dataOffset + 2, 4), byteOffset);
        return request;
    }

    public static LogixReadFragment ParseResponse(
        LogixSymbolReference reference,
        uint requestedByteOffset,
        LogixCipResponse response)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(response);
        EnsureSupportedAtomicArrayType(reference.NativeType);

        var expectedService = (byte)(LogixCipCodec.ReadTagFragmentedService | 0x80);
        if (response.Service != expectedService)
            throw new InvalidDataException($"CIP response service 0x{response.Service:X2} is not a Read Tag Fragmented reply.");
        if (response.GeneralStatus is not (0x00 or 0x06))
            LogixCipCodec.ThrowIfFailed(response, $"Read Tag Fragmented '{reference.StableIdentity}'");
        if (response.Data.Length < 2)
            throw new InvalidDataException("Read Tag Fragmented response is missing the native type code.");

        var typeCode = BinaryPrimitives.ReadUInt16LittleEndian(response.Data.AsSpan(0, 2));
        var expectedType = LogixValueCodec.GetCipAtomicTypeCode(reference.NativeType);
        if ((typeCode & 0x0FFF) != (expectedType & 0x0FFF))
            throw new LogixCipException(
                LogixProtocolError.TypeMismatch,
                response.GeneralStatus,
                $"Read Tag Fragmented returned CIP type 0x{typeCode:X4}, expected {reference.NativeType}.");

        var payload = response.Data[2..];
        if (payload.Length == 0)
            throw new InvalidDataException(response.GeneralStatus == 0x06
                ? "Read Tag Fragmented returned partial-transfer status without payload progress."
                : "Read Tag Fragmented final response did not contain any value bytes.");

        return new LogixReadFragment(requestedByteOffset, typeCode, payload, response.GeneralStatus == 0x06);
    }

    public static byte[] AssembleCompletePayload(
        LogixSymbolReference reference,
        ushort elementCount,
        IReadOnlyList<LogixReadFragment> fragments,
        int maximumValueBytes = DefaultMaximumValueBytes)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(fragments);
        if (elementCount == 0) throw new ArgumentOutOfRangeException(nameof(elementCount));
        if (maximumValueBytes is <= 0 or > HardMaximumValueBytes)
            throw new ArgumentOutOfRangeException(nameof(maximumValueBytes), $"Fragmented read maximum must be from 1 to {HardMaximumValueBytes} bytes.");

        var nativeWidth = GetSupportedAtomicByteWidth(reference.NativeType);
        var expectedBytes = checked(nativeWidth * elementCount);
        if (expectedBytes > maximumValueBytes)
            throw new LogixCipException(
                LogixProtocolError.FragmentationFailed,
                0,
                $"Fragmented read requires {expectedBytes} bytes, exceeding the configured {maximumValueBytes}-byte limit.");
        if (fragments.Count == 0)
            throw new InvalidDataException("Fragmented read did not return any fragments.");

        var expectedType = LogixValueCodec.GetCipAtomicTypeCode(reference.NativeType);
        var assembled = new byte[expectedBytes];
        var expectedOffset = 0;
        for (var index = 0; index < fragments.Count; index++)
        {
            var fragment = fragments[index] ?? throw new InvalidDataException("Fragmented read contains a null fragment.");
            if (fragment.ByteOffset != (uint)expectedOffset)
                throw new InvalidDataException($"Fragmented read expected byte offset {expectedOffset} but received {fragment.ByteOffset}.");
            if ((fragment.TypeCode & 0x0FFF) != (expectedType & 0x0FFF))
                throw new LogixCipException(LogixProtocolError.TypeMismatch, 0, $"Fragmented read changed CIP type at byte offset {fragment.ByteOffset}.");
            if (fragment.Payload is null || fragment.Payload.Length == 0)
                throw new InvalidDataException($"Fragmented read made no payload progress at byte offset {fragment.ByteOffset}.");
            if (fragment.Payload.Length > expectedBytes - expectedOffset)
                throw new InvalidDataException("Fragmented read returned more value bytes than requested.");

            fragment.Payload.CopyTo(assembled, expectedOffset);
            expectedOffset += fragment.Payload.Length;

            var isLast = index == fragments.Count - 1;
            if (!isLast && !fragment.HasMore)
                throw new InvalidDataException("Fragmented read reported completion before all supplied fragments were consumed.");
            if (isLast && fragment.HasMore)
                throw new LogixCipException(LogixProtocolError.FragmentationFailed, 0, "Fragmented read ended while the controller still reported partial transfer.");
        }

        if (expectedOffset != expectedBytes)
            throw new LogixCipException(
                LogixProtocolError.FragmentationFailed,
                0,
                $"Fragmented read completed with {expectedOffset} value bytes; {expectedBytes} were required.");

        return assembled;
    }

    public static uint NextByteOffset(LogixReadFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        if (fragment.Payload is null || fragment.Payload.Length == 0)
            throw new InvalidDataException("Cannot advance a fragmented read after an empty fragment.");
        return checked(fragment.ByteOffset + (uint)fragment.Payload.Length);
    }

    private static void EnsureSupportedAtomicArrayType(LogixNativeType nativeType) =>
        _ = GetSupportedAtomicByteWidth(nativeType);

    private static int GetSupportedAtomicByteWidth(LogixNativeType nativeType) => nativeType switch
    {
        LogixNativeType.Sint => 1,
        LogixNativeType.Int => 2,
        LogixNativeType.Dint => 4,
        LogixNativeType.Lint => 8,
        LogixNativeType.Real => 4,
        LogixNativeType.Bool => throw new NotSupportedException("Fragmented BOOL arrays remain disabled until packed BOOL layout semantics are proven."),
        LogixNativeType.Lreal => throw new NotSupportedException("LREAL fragmented reads remain disabled while LREAL runtime support is not accepted."),
        LogixNativeType.String => throw new NotSupportedException("STRING fragmented reads require structure metadata and are not enabled by the atomic fragmented-read groundwork."),
        _ => throw new ArgumentOutOfRangeException(nameof(nativeType))
    };
}
