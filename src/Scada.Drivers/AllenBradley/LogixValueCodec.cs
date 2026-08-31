using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Scada.Core.Tags;

namespace Scada.Drivers.AllenBradley;

public static class LogixValueCodec
{
    public const ushort CipTypeBool = 0x00C1;
    public const ushort CipTypeSint = 0x00C2;
    public const ushort CipTypeInt = 0x00C3;
    public const ushort CipTypeDint = 0x00C4;
    public const ushort CipTypeLint = 0x00C5;
    public const ushort CipTypeReal = 0x00CA;
    public const ushort CipTypeLreal = 0x00CB;

    public static bool TryGetCanonicalDataType(LogixNativeType nativeType, out TagDataType dataType)
    {
        switch (nativeType)
        {
            case LogixNativeType.Bool: dataType = TagDataType.Boolean; return true;
            case LogixNativeType.Sint:
            case LogixNativeType.Int: dataType = TagDataType.Int16; return true;
            case LogixNativeType.Dint: dataType = TagDataType.Int32; return true;
            case LogixNativeType.Lint: dataType = TagDataType.Int64; return true;
            case LogixNativeType.Real: dataType = TagDataType.Float; return true;
            case LogixNativeType.Lreal: dataType = TagDataType.Double; return true;
            case LogixNativeType.String: dataType = TagDataType.String; return true;
            default: dataType = default; return false;
        }
    }

    public static int? GetNativeIntegerBitWidth(LogixNativeType nativeType) => nativeType switch
    {
        LogixNativeType.Sint => 8,
        LogixNativeType.Int => 16,
        LogixNativeType.Dint => 32,
        LogixNativeType.Lint => 64,
        _ => null
    };

    public static bool IsFirstCutRuntimeReadable(LogixNativeType nativeType) => nativeType is
        LogixNativeType.Bool or LogixNativeType.Sint or LogixNativeType.Int or LogixNativeType.Dint or LogixNativeType.Lint or LogixNativeType.Real;

    public static bool IsFirstCutRuntimeWritable(LogixNativeType nativeType) => nativeType is
        LogixNativeType.Sint or LogixNativeType.Int or LogixNativeType.Dint or LogixNativeType.Lint or LogixNativeType.Real;

    public static ushort GetCipAtomicTypeCode(LogixNativeType nativeType) => nativeType switch
    {
        LogixNativeType.Bool => CipTypeBool,
        LogixNativeType.Sint => CipTypeSint,
        LogixNativeType.Int => CipTypeInt,
        LogixNativeType.Dint => CipTypeDint,
        LogixNativeType.Lint => CipTypeLint,
        LogixNativeType.Real => CipTypeReal,
        LogixNativeType.Lreal => CipTypeLreal,
        LogixNativeType.String => throw new NotSupportedException("Logix STRING is structure-based and is not encoded as a plain CIP atomic scalar in the first-cut runtime."),
        _ => throw new ArgumentOutOfRangeException(nameof(nativeType))
    };

    public static object DecodeAtomic(LogixNativeType nativeType, ReadOnlySpan<byte> payload)
    {
        return nativeType switch
        {
            LogixNativeType.Bool when payload.Length >= 1 => payload[0] != 0,
            LogixNativeType.Sint when payload.Length >= 1 => unchecked((sbyte)payload[0]),
            LogixNativeType.Int when payload.Length >= 2 => BinaryPrimitives.ReadInt16LittleEndian(payload),
            LogixNativeType.Dint when payload.Length >= 4 => BinaryPrimitives.ReadInt32LittleEndian(payload),
            LogixNativeType.Lint when payload.Length >= 8 => BinaryPrimitives.ReadInt64LittleEndian(payload),
            LogixNativeType.Real when payload.Length >= 4 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(payload)),
            LogixNativeType.Lreal when payload.Length >= 8 => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(payload)),
            LogixNativeType.String => throw new NotSupportedException("Logix STRING runtime codec requires structure metadata and is not enabled by the first-cut atomic codec."),
            _ => throw new InvalidDataException($"Insufficient payload for Logix native type '{nativeType}'.")
        };
    }

    public static byte[] EncodeAtomic(LogixNativeType nativeType, object? nativeValue)
    {
        if (nativeValue is null) throw new ArgumentNullException(nameof(nativeValue));
        var buffer = nativeType switch
        {
            LogixNativeType.Bool => new byte[1],
            LogixNativeType.Sint => new byte[1],
            LogixNativeType.Int => new byte[2],
            LogixNativeType.Dint or LogixNativeType.Real => new byte[4],
            LogixNativeType.Lint or LogixNativeType.Lreal => new byte[8],
            LogixNativeType.String => throw new NotSupportedException("Logix STRING writes require structure metadata and are not enabled in the first-cut runtime."),
            _ => throw new ArgumentOutOfRangeException(nameof(nativeType))
        };

        switch (nativeType)
        {
            case LogixNativeType.Bool:
                if (nativeValue is not bool boolean) throw InvalidValue(nativeType, nativeValue);
                buffer[0] = boolean ? (byte)1 : (byte)0;
                break;
            case LogixNativeType.Sint:
                if (nativeValue is not sbyte sint) throw InvalidValue(nativeType, nativeValue);
                buffer[0] = unchecked((byte)sint);
                break;
            case LogixNativeType.Int:
                if (nativeValue is not short int16) throw InvalidValue(nativeType, nativeValue);
                BinaryPrimitives.WriteInt16LittleEndian(buffer, int16);
                break;
            case LogixNativeType.Dint:
                if (nativeValue is not int int32) throw InvalidValue(nativeType, nativeValue);
                BinaryPrimitives.WriteInt32LittleEndian(buffer, int32);
                break;
            case LogixNativeType.Lint:
                if (nativeValue is not long int64) throw InvalidValue(nativeType, nativeValue);
                BinaryPrimitives.WriteInt64LittleEndian(buffer, int64);
                break;
            case LogixNativeType.Real:
                if (nativeValue is not float single || !float.IsFinite(single)) throw InvalidValue(nativeType, nativeValue);
                BinaryPrimitives.WriteInt32LittleEndian(buffer, BitConverter.SingleToInt32Bits(single));
                break;
            case LogixNativeType.Lreal:
                if (nativeValue is not double dbl || !double.IsFinite(dbl)) throw InvalidValue(nativeType, nativeValue);
                BinaryPrimitives.WriteInt64LittleEndian(buffer, BitConverter.DoubleToInt64Bits(dbl));
                break;
        }
        return buffer;
    }

    public static object ToCanonicalValue(LogixTagBinding binding, object nativeValue)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(nativeValue);
        if (binding.AddressSelector is not null)
            return ReadPhysicalBit(binding.Reference.NativeType, nativeValue, binding.AddressSelector.Index);

        return binding.Reference.NativeType switch
        {
            LogixNativeType.Bool when nativeValue is bool v => v,
            LogixNativeType.Sint when nativeValue is sbyte v => (short)v,
            LogixNativeType.Int when nativeValue is short v => v,
            LogixNativeType.Dint when nativeValue is int v => v,
            LogixNativeType.Lint when nativeValue is long v => v,
            LogixNativeType.Real when nativeValue is float v => v,
            LogixNativeType.Lreal when nativeValue is double v => v,
            LogixNativeType.String when nativeValue is string v => v,
            _ => throw new InvalidDataException($"Native value type does not match Logix type '{binding.Reference.NativeType}'.")
        };
    }

    public static object ToNativeWriteValue(LogixTagBinding binding, object? canonicalValue)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.AddressSelector is not null)
            throw new InvalidOperationException("Physical bit writes require a current native value and ApplyPhysicalBit().");
        if (canonicalValue is null) throw new ArgumentNullException(nameof(canonicalValue));

        return binding.Reference.NativeType switch
        {
            LogixNativeType.Bool when canonicalValue is bool v => v,
            LogixNativeType.Sint when canonicalValue is short v && v is >= sbyte.MinValue and <= sbyte.MaxValue => (sbyte)v,
            LogixNativeType.Int when canonicalValue is short v => v,
            LogixNativeType.Dint when canonicalValue is int v => v,
            LogixNativeType.Lint when canonicalValue is long v => v,
            LogixNativeType.Real when canonicalValue is float v && float.IsFinite(v) => v,
            LogixNativeType.Lreal when canonicalValue is double v && double.IsFinite(v) => v,
            LogixNativeType.String when canonicalValue is string => throw new NotSupportedException("Logix STRING writes are deferred until structure capacity metadata is available."),
            _ => throw InvalidValue(binding.Reference.NativeType, canonicalValue)
        };
    }

    public static object ApplyPhysicalBit(LogixNativeType nativeType, object currentNativeValue, int index, bool bitValue)
    {
        var width = GetNativeIntegerBitWidth(nativeType) ?? throw new ArgumentException($"Logix type '{nativeType}' does not support physical bit selection.", nameof(nativeType));
        if (index < 0 || index >= width) throw new ArgumentOutOfRangeException(nameof(index));
        return nativeType switch
        {
            LogixNativeType.Sint when currentNativeValue is sbyte v => (object)unchecked((sbyte)ApplyMask((byte)v, index, bitValue)),
            LogixNativeType.Int when currentNativeValue is short v => (object)unchecked((short)ApplyMask((ushort)v, index, bitValue)),
            LogixNativeType.Dint when currentNativeValue is int v => (object)unchecked((int)ApplyMask((uint)v, index, bitValue)),
            LogixNativeType.Lint when currentNativeValue is long v => (object)unchecked((long)ApplyMask((ulong)v, index, bitValue)),
            _ => throw InvalidValue(nativeType, currentNativeValue)
        };
    }

    private static bool ReadPhysicalBit(LogixNativeType nativeType, object value, int index)
    {
        var width = GetNativeIntegerBitWidth(nativeType) ?? throw new ArgumentException($"Logix type '{nativeType}' does not support physical bit selection.");
        if (index < 0 || index >= width) throw new ArgumentOutOfRangeException(nameof(index));
        return nativeType switch
        {
            LogixNativeType.Sint when value is sbyte v => ((unchecked((byte)v) >> index) & 1) != 0,
            LogixNativeType.Int when value is short v => ((unchecked((ushort)v) >> index) & 1) != 0,
            LogixNativeType.Dint when value is int v => ((unchecked((uint)v) >> index) & 1u) != 0,
            LogixNativeType.Lint when value is long v => ((unchecked((ulong)v) >> index) & 1UL) != 0,
            _ => throw InvalidValue(nativeType, value)
        };
    }

    private static byte ApplyMask(byte value, int index, bool set)
    {
        var mask = (byte)(1u << index);
        return set ? (byte)(value | mask) : (byte)(value & ~mask);
    }

    private static ushort ApplyMask(ushort value, int index, bool set)
    {
        var mask = (ushort)(1u << index);
        return set ? (ushort)(value | mask) : (ushort)(value & ~mask);
    }

    private static uint ApplyMask(uint value, int index, bool set)
    {
        var mask = 1u << index;
        return set ? value | mask : value & ~mask;
    }

    private static ulong ApplyMask(ulong value, int index, bool set)
    {
        var mask = 1UL << index;
        return set ? value | mask : value & ~mask;
    }

    private static ArgumentException InvalidValue(LogixNativeType nativeType, object value) =>
        new($"Value '{Convert.ToString(value, CultureInfo.InvariantCulture)}' ({value.GetType().Name}) is not valid for Logix native type '{nativeType}'.");
}
