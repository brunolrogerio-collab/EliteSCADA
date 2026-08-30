using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Scada.Drivers.SiemensS7Iso;

public static class S7IsoValueCodec
{
    private static readonly Encoding WStringEncoding = new UnicodeEncoding(
        bigEndian: true,
        byteOrderMark: false,
        throwOnInvalidBytes: true);

    public static object Decode(S7IsoPoint point, ReadOnlySpan<byte> raw)
    {
        ArgumentNullException.ThrowIfNull(point);
        point.Validate();
        if (raw.Length < point.ByteLength)
            throw new ArgumentException(
                $"S7 payload for '{point.Tag.Path}' has {raw.Length} byte(s), expected at least {point.ByteLength}.",
                nameof(raw));

        var ordered = ApplyValueOrder(raw[..point.ByteLength], point.ValueOrder);

        return point.ValueType switch
        {
            S7IsoValueType.Boolean => ordered[0] != 0,
            S7IsoValueType.Byte => (short)ordered[0],
            S7IsoValueType.UInt16 => (int)BinaryPrimitives.ReadUInt16BigEndian(ordered),
            S7IsoValueType.Int16 => BinaryPrimitives.ReadInt16BigEndian(ordered),
            S7IsoValueType.UInt32 => (long)BinaryPrimitives.ReadUInt32BigEndian(ordered),
            S7IsoValueType.Int32 => BinaryPrimitives.ReadInt32BigEndian(ordered),
            S7IsoValueType.Float32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(ordered)),
            S7IsoValueType.Int64 => BinaryPrimitives.ReadInt64BigEndian(ordered),
            S7IsoValueType.Float64 => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(ordered)),
            S7IsoValueType.String => DecodeString(point, ordered),
            S7IsoValueType.WString => DecodeWString(point, ordered),
            S7IsoValueType.DateTime => DecodeDateTime(ordered),
            _ => throw new ArgumentOutOfRangeException(nameof(point.ValueType))
        };
    }

    public static byte[] Encode(S7IsoPoint point, object? value)
    {
        ArgumentNullException.ThrowIfNull(point);
        point.Validate();
        if (value is null)
            throw new ArgumentNullException(nameof(value), $"S7 value for '{point.Tag.Path}' cannot be null.");

        var canonical = new byte[point.ByteLength];

        switch (point.ValueType)
        {
            case S7IsoValueType.Boolean:
                canonical[0] = Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? (byte)1 : (byte)0;
                break;
            case S7IsoValueType.Byte:
                canonical[0] = checked((byte)Convert.ToInt16(value, CultureInfo.InvariantCulture));
                break;
            case S7IsoValueType.UInt16:
                BinaryPrimitives.WriteUInt16BigEndian(
                    canonical,
                    checked((ushort)Convert.ToInt32(value, CultureInfo.InvariantCulture)));
                break;
            case S7IsoValueType.Int16:
                BinaryPrimitives.WriteInt16BigEndian(
                    canonical,
                    Convert.ToInt16(value, CultureInfo.InvariantCulture));
                break;
            case S7IsoValueType.UInt32:
                BinaryPrimitives.WriteUInt32BigEndian(
                    canonical,
                    checked((uint)Convert.ToInt64(value, CultureInfo.InvariantCulture)));
                break;
            case S7IsoValueType.Int32:
                BinaryPrimitives.WriteInt32BigEndian(
                    canonical,
                    Convert.ToInt32(value, CultureInfo.InvariantCulture));
                break;
            case S7IsoValueType.Float32:
                BinaryPrimitives.WriteInt32BigEndian(
                    canonical,
                    BitConverter.SingleToInt32Bits(Convert.ToSingle(value, CultureInfo.InvariantCulture)));
                break;
            case S7IsoValueType.Int64:
                BinaryPrimitives.WriteInt64BigEndian(
                    canonical,
                    Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case S7IsoValueType.Float64:
                BinaryPrimitives.WriteInt64BigEndian(
                    canonical,
                    BitConverter.DoubleToInt64Bits(Convert.ToDouble(value, CultureInfo.InvariantCulture)));
                break;
            case S7IsoValueType.String:
                EncodeString(point, value, canonical);
                break;
            case S7IsoValueType.WString:
                EncodeWString(point, value, canonical);
                break;
            case S7IsoValueType.DateTime:
                EncodeDateTime(value, canonical);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(point.ValueType));
        }

        return ApplyValueOrder(canonical, point.ValueOrder);
    }

    private static byte[] ApplyValueOrder(ReadOnlySpan<byte> source, S7IsoValueOrder order)
    {
        var result = source.ToArray();
        if (order is S7IsoValueOrder.ByteSwap or S7IsoValueOrder.ByteAndWordSwap)
        {
            for (var i = 0; i + 1 < result.Length; i += 2)
                (result[i], result[i + 1]) = (result[i + 1], result[i]);
        }

        if (order is S7IsoValueOrder.WordSwap or S7IsoValueOrder.ByteAndWordSwap)
        {
            var copy = result.ToArray();
            var wordCount = result.Length / 2;
            for (var word = 0; word < wordCount; word++)
            {
                var sourceWord = wordCount - 1 - word;
                result[word * 2] = copy[sourceWord * 2];
                result[word * 2 + 1] = copy[sourceWord * 2 + 1];
            }
        }

        return result;
    }

    private static string DecodeString(S7IsoPoint point, ReadOnlySpan<byte> raw)
    {
        var declaredMaximum = raw[0];
        var length = raw[1];
        if (declaredMaximum != point.StringLength)
            throw new FormatException(
                $"S7 STRING declared maximum length {declaredMaximum} does not match configured length {point.StringLength}.");
        if (length > declaredMaximum)
            throw new FormatException($"S7 STRING current length {length} exceeds its declared maximum {declaredMaximum}.");

        return Encoding.Latin1.GetString(raw.Slice(2, length));
    }

    private static void EncodeString(S7IsoPoint point, object value, Span<byte> destination)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (text.Length > point.StringLength)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"S7 STRING value length {text.Length} exceeds configured maximum {point.StringLength}.");
        if (text.Any(character => character > byte.MaxValue))
            throw new ArgumentException("S7 STRING first-cut codec supports ISO-8859-1 characters only.", nameof(value));

        destination.Clear();
        destination[0] = point.StringLength;
        destination[1] = checked((byte)text.Length);
        Encoding.Latin1.GetBytes(text.AsSpan(), destination[2..]);
    }

    private static string DecodeWString(S7IsoPoint point, ReadOnlySpan<byte> raw)
    {
        var declaredMaximum = BinaryPrimitives.ReadUInt16BigEndian(raw[..2]);
        var length = BinaryPrimitives.ReadUInt16BigEndian(raw.Slice(2, 2));
        if (declaredMaximum != point.StringLength)
            throw new FormatException(
                $"S7 WSTRING declared maximum length {declaredMaximum} does not match configured length {point.StringLength}.");
        if (length > declaredMaximum)
            throw new FormatException($"S7 WSTRING current length {length} exceeds its declared maximum {declaredMaximum}.");

        return WStringEncoding.GetString(raw.Slice(4, checked(length * 2)));
    }

    private static void EncodeWString(S7IsoPoint point, object value, Span<byte> destination)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (text.Length > point.StringLength)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"S7 WSTRING value length {text.Length} UTF-16 code unit(s) exceeds configured maximum {point.StringLength}.");

        destination.Clear();
        BinaryPrimitives.WriteUInt16BigEndian(destination[..2], point.StringLength);
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(2, 2), checked((ushort)text.Length));
        WStringEncoding.GetBytes(text.AsSpan(), destination[4..]);
    }

    private static DateTime DecodeDateTime(ReadOnlySpan<byte> raw)
    {
        var yearTwoDigits = FromBcd(raw[0], "year");
        var year = yearTwoDigits >= 90 ? 1900 + yearTwoDigits : 2000 + yearTwoDigits;
        var month = FromBcd(raw[1], "month");
        var day = FromBcd(raw[2], "day");
        var hour = FromBcd(raw[3], "hour");
        var minute = FromBcd(raw[4], "minute");
        var second = FromBcd(raw[5], "second");
        var millisecondTens = FromBcd(raw[6], "millisecond high digits");
        var millisecondUnits = (raw[7] >> 4) & 0x0F;
        if (millisecondUnits > 9)
            throw new FormatException(
                $"Invalid BCD millisecond units nibble 0x{millisecondUnits:X1} in S7 DATE_AND_TIME.");

        var weekday = raw[7] & 0x0F;
        if (weekday is < 1 or > 7)
            throw new FormatException(
                $"Invalid weekday value {weekday} in S7 DATE_AND_TIME; expected 1 through 7.");

        var millisecond = millisecondTens * 10 + millisecondUnits;
        return new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Unspecified);
    }

    private static void EncodeDateTime(object value, Span<byte> destination)
    {
        var timestamp = value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            _ => throw new ArgumentException("S7 DATE_AND_TIME requires DateTime or DateTimeOffset.", nameof(value))
        };

        if (timestamp.Year is < 1990 or > 2089)
            throw new ArgumentOutOfRangeException(nameof(value), "S7 DATE_AND_TIME supports years 1990 through 2089.");

        var year = timestamp.Year >= 2000 ? timestamp.Year - 2000 : timestamp.Year - 1900;
        destination[0] = ToBcd(year);
        destination[1] = ToBcd(timestamp.Month);
        destination[2] = ToBcd(timestamp.Day);
        destination[3] = ToBcd(timestamp.Hour);
        destination[4] = ToBcd(timestamp.Minute);
        destination[5] = ToBcd(timestamp.Second);
        destination[6] = ToBcd(timestamp.Millisecond / 10);

        var weekday = timestamp.DayOfWeek == DayOfWeek.Sunday ? 1 : (int)timestamp.DayOfWeek + 1;
        destination[7] = (byte)(((timestamp.Millisecond % 10) << 4) | weekday);
    }

    private static byte ToBcd(int value)
    {
        if (value is < 0 or > 99) throw new ArgumentOutOfRangeException(nameof(value));
        return (byte)(((value / 10) << 4) | (value % 10));
    }

    private static int FromBcd(byte value, string field)
    {
        var high = (value >> 4) & 0x0F;
        var low = value & 0x0F;
        if (high > 9 || low > 9)
            throw new FormatException($"Invalid BCD value 0x{value:X2} in S7 DATE_AND_TIME {field}.");
        return high * 10 + low;
    }
}
