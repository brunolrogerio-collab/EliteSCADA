using System.Buffers.Binary;
using Scada.Core.Tags;

namespace Scada.Drivers.Mitsubishi;

/// <summary>Maps MELSEC physical words to the canonical TAG value surface.</summary>
public static class MitsubishiMelsecValueCodec
{
    public static TagDataType CanonicalDataType(MitsubishiMelsecPhysicalType type) => type switch
    {
        MitsubishiMelsecPhysicalType.Bit => TagDataType.Boolean,
        MitsubishiMelsecPhysicalType.Int16 or MitsubishiMelsecPhysicalType.UInt16 => TagDataType.Int32,
        MitsubishiMelsecPhysicalType.Int32 or MitsubishiMelsecPhysicalType.UInt32 => TagDataType.Int64,
        MitsubishiMelsecPhysicalType.Float32 => TagDataType.Float,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static int WordSpan(MitsubishiMelsecPhysicalType type) => type switch
    {
        MitsubishiMelsecPhysicalType.Int16 or MitsubishiMelsecPhysicalType.UInt16 => 1,
        MitsubishiMelsecPhysicalType.Int32 or MitsubishiMelsecPhysicalType.UInt32 or MitsubishiMelsecPhysicalType.Float32 => 2,
        MitsubishiMelsecPhysicalType.Bit => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static object Decode(
        MitsubishiMelsecPhysicalType type,
        ReadOnlySpan<byte> rawBytes,
        bool bitValue,
        TagPhysicalValueTransform? transform = null)
    {
        if (type == MitsubishiMelsecPhysicalType.Bit) return bitValue;
        var expectedBytes = WordSpan(type) * 2;
        if (rawBytes.Length != expectedBytes)
            throw new ArgumentException($"Expected {expectedBytes} physical bytes for {type}, received {rawBytes.Length}.", nameof(rawBytes));
        var bytes = rawBytes.ToArray();
        ApplyTransform(bytes, transform);
        var lowWord = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2));
        if (WordSpan(type) == 1)
        {
            return type switch
            {
                MitsubishiMelsecPhysicalType.Int16 => (int)unchecked((short)lowWord),
                MitsubishiMelsecPhysicalType.UInt16 => (int)lowWord,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }

        var highWord = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2));
        var bits = ((uint)highWord << 16) | lowWord;
        return type switch
        {
            MitsubishiMelsecPhysicalType.Int32 => (object)(long)unchecked((int)bits),
            MitsubishiMelsecPhysicalType.UInt32 => (object)(long)bits,
            MitsubishiMelsecPhysicalType.Float32 => (object)BitConverter.Int32BitsToSingle(unchecked((int)bits)),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    public static byte[] Encode(
        MitsubishiMelsecPhysicalType type,
        object? canonicalValue,
        TagPhysicalValueTransform? transform = null)
    {
        if (type == MitsubishiMelsecPhysicalType.Bit)
            throw new ArgumentException("Bit values are encoded through the bit-unit write path.", nameof(type));

        var bytes = new byte[WordSpan(type) * 2];
        switch (type)
        {
            case MitsubishiMelsecPhysicalType.Int16:
            {
                var value = ToInt64(canonicalValue);
                if (value is < short.MinValue or > short.MaxValue) throw new OverflowException("Int16 physical value is outside its range.");
                BinaryPrimitives.WriteInt16LittleEndian(bytes, checked((short)value));
                break;
            }
            case MitsubishiMelsecPhysicalType.UInt16:
            {
                var value = ToUInt64(canonicalValue);
                if (value > ushort.MaxValue) throw new OverflowException("UInt16 physical value is outside its range.");
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)value));
                break;
            }
            case MitsubishiMelsecPhysicalType.Int32:
            {
                var value = ToInt64(canonicalValue);
                if (value is < int.MinValue or > int.MaxValue) throw new OverflowException("Int32 physical value is outside its range.");
                BinaryPrimitives.WriteInt32LittleEndian(bytes, checked((int)value));
                break;
            }
            case MitsubishiMelsecPhysicalType.UInt32:
            {
                var value = ToUInt64(canonicalValue);
                if (value > uint.MaxValue) throw new OverflowException("UInt32 physical value is outside its range.");
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, checked((uint)value));
                break;
            }
            case MitsubishiMelsecPhysicalType.Float32:
            {
                if (!TryDouble(canonicalValue, out var value) || !double.IsFinite(value) || value > float.MaxValue || value < -float.MaxValue)
                    throw new ArgumentException("Float32 requires a finite numeric canonical value.", nameof(canonicalValue));
                BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverter.SingleToInt32Bits((float)value));
                break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
        ApplyTransform(bytes, transform);
        return bytes;
    }

    public static bool ToBit(object? value) => value switch
    {
        bool boolean => boolean,
        byte number => number is 1,
        sbyte number => number is 1,
        short number => number is 1,
        ushort number => number is 1,
        int number => number is 1,
        uint number => number is 1,
        long number => number is 1,
        _ => throw new ArgumentException("A MELSEC bit write requires Boolean or numeric 0/1.", nameof(value))
    };

    private static void ApplyTransform(Span<byte> bytes, TagPhysicalValueTransform? transform)
    {
        transform?.Validate();
        if (transform?.ByteSwap == true)
            for (var i = 0; i < bytes.Length; i += 2)
                (bytes[i], bytes[i + 1]) = (bytes[i + 1], bytes[i]);
        if (transform?.WordSwap == true && bytes.Length == 4)
        {
            (bytes[0], bytes[2]) = (bytes[2], bytes[0]);
            (bytes[1], bytes[3]) = (bytes[3], bytes[1]);
        }
    }

    private static long ToInt64(object? value) => value switch
    {
        sbyte n => n, byte n => n, short n => n, ushort n => n, int n => n, uint n => n,
        long n => n,
        _ => throw new ArgumentException("An integer canonical value is required.", nameof(value))
    };

    private static ulong ToUInt64(object? value) => value switch
    {
        byte n => n, ushort n => n, uint n => n, ulong n => n,
        sbyte n when n >= 0 => (ulong)n,
        short n when n >= 0 => (ulong)n,
        int n when n >= 0 => (ulong)n,
        long n when n >= 0 => (ulong)n,
        _ => throw new ArgumentException("A non-negative integer canonical value is required.", nameof(value))
    };

    private static bool TryDouble(object? value, out double result)
    {
        switch (value)
        {
            case byte n: result = n; return true;
            case sbyte n: result = n; return true;
            case short n: result = n; return true;
            case ushort n: result = n; return true;
            case int n: result = n; return true;
            case uint n: result = n; return true;
            case long n: result = n; return true;
            case float n: result = n; return true;
            case double n: result = n; return true;
            case decimal n: result = (double)n; return true;
            default: result = 0; return false;
        }
    }
}
