namespace Scada.Drivers.AllenBradley;

/// <summary>
/// Builds and validates Logix Read Modify Write Tag (0x4E) operations.
/// The service is the controller-native atomic primitive used to change integer
/// bits without a client-side read/write race.
/// </summary>
public static class LogixReadModifyWrite
{
    public static byte[] BuildBitRequest(
        LogixSymbolReference reference,
        int bitIndex,
        bool bitValue)
    {
        ArgumentNullException.ThrowIfNull(reference);
        reference.Validate();

        var bitWidth = LogixValueCodec.GetNativeIntegerBitWidth(reference.NativeType)
            ?? throw new NotSupportedException($"Logix native type '{reference.NativeType}' does not support atomic integer-bit modification.");
        if (bitIndex < 0 || bitIndex >= bitWidth)
            throw new ArgumentOutOfRangeException(nameof(bitIndex), $"Bit index must be from 0 to {bitWidth - 1} for {reference.NativeType}.");

        var maskSize = bitWidth / 8;
        var orMask = new byte[maskSize];
        var andMask = Enumerable.Repeat((byte)0xFF, maskSize).ToArray();
        var byteIndex = bitIndex / 8;
        var mask = (byte)(1 << (bitIndex % 8));

        if (bitValue)
            orMask[byteIndex] |= mask;
        else
            andMask[byteIndex] &= unchecked((byte)~mask);

        return BuildRequest(reference, orMask, andMask);
    }

    public static byte[] BuildRequest(
        LogixSymbolReference reference,
        ReadOnlySpan<byte> orMask,
        ReadOnlySpan<byte> andMask)
    {
        ArgumentNullException.ThrowIfNull(reference);
        reference.Validate();

        var bitWidth = LogixValueCodec.GetNativeIntegerBitWidth(reference.NativeType)
            ?? throw new NotSupportedException($"Logix native type '{reference.NativeType}' does not support Read Modify Write Tag masks.");
        var requiredMaskSize = bitWidth / 8;
        if (orMask.Length != requiredMaskSize || andMask.Length != requiredMaskSize)
            throw new ArgumentException($"Read Modify Write masks for {reference.NativeType} must both be exactly {requiredMaskSize} bytes to preserve complete data integrity.");

        var path = LogixCipCodec.EncodeSymbolicPath(reference);
        var request = new byte[2 + path.Length + 2 + requiredMaskSize + requiredMaskSize];
        request[0] = LogixCipCodec.ReadModifyWriteService;
        request[1] = checked((byte)(path.Length / 2));
        path.CopyTo(request, 2);

        var offset = 2 + path.Length;
        request[offset] = checked((byte)requiredMaskSize);
        request[offset + 1] = 0;
        offset += 2;
        orMask.CopyTo(request.AsSpan(offset, requiredMaskSize));
        offset += requiredMaskSize;
        andMask.CopyTo(request.AsSpan(offset, requiredMaskSize));
        return request;
    }

    public static void ValidateResponse(LogixSymbolReference reference, LogixCipResponse response)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(response);
        var expectedService = (byte)(LogixCipCodec.ReadModifyWriteService | 0x80);
        if (response.Service != expectedService)
            throw new InvalidDataException($"CIP response service 0x{response.Service:X2} is not a Read Modify Write Tag reply for '{reference.StableIdentity}'.");
        LogixCipCodec.ThrowIfFailed(response, $"Read Modify Write Tag '{reference.StableIdentity}'");
    }
}
