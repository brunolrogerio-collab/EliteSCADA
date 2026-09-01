using System.Globalization;
using System.IO.BACnet;
using Scada.Core.Tags;

namespace Scada.Drivers.Bacnet;

public static class BacnetValueCodec
{
    private const uint PresentValueProperty = 85;

    public static object? Decode(BacnetValue value, TagDataType targetType, BacnetBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var raw = value.Value;
        if (raw is null) return null;

        try
        {
            return targetType switch
            {
                TagDataType.Boolean => DecodeBoolean(raw, binding),
                TagDataType.Int16 => checked(Convert.ToInt16(raw, CultureInfo.InvariantCulture)),
                TagDataType.Int32 => checked(Convert.ToInt32(raw, CultureInfo.InvariantCulture)),
                TagDataType.Int64 => Convert.ToInt64(raw, CultureInfo.InvariantCulture),
                TagDataType.Float => Convert.ToSingle(raw, CultureInfo.InvariantCulture),
                TagDataType.Double => Convert.ToDouble(raw, CultureInfo.InvariantCulture),
                TagDataType.String => raw is string text ? text : throw TypeMismatch(targetType, raw),
                TagDataType.DateTime => raw is DateTime date ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.Zero) : throw TypeMismatch(targetType, raw),
                TagDataType.Enum => checked(Convert.ToInt32(raw, CultureInfo.InvariantCulture)),
                _ => throw new NotSupportedException($"BACnet decoding does not support EliteSCADA TAG type '{targetType}'.")
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            throw TypeMismatch(targetType, raw, ex);
        }
    }

    public static IReadOnlyCollection<BacnetValue> Encode(object? value, TagDataType sourceType, BacnetBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (value is null)
            throw new InvalidOperationException("BACnet null is reserved for explicit priority relinquish. Use EncodeRelinquish/RelinquishAsync instead of a generic null write.");

        BacnetValue encoded = sourceType switch
        {
            TagDataType.Int16 or TagDataType.Int32 or TagDataType.Int64 or TagDataType.Float or TagDataType.Double
                when IsAnalogPresentValue(binding) =>
                new(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, Convert.ToSingle(value, CultureInfo.InvariantCulture)),
            TagDataType.Boolean when IsBinaryPresentValue(binding) =>
                new(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? 1u : 0u),
            TagDataType.Boolean => new(BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, Convert.ToBoolean(value, CultureInfo.InvariantCulture)),
            TagDataType.Int16 or TagDataType.Int32 or TagDataType.Int64 =>
                new(BacnetApplicationTags.BACNET_APPLICATION_TAG_SIGNED_INT, Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            TagDataType.Float => new(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, Convert.ToSingle(value, CultureInfo.InvariantCulture)),
            TagDataType.Double => new(BacnetApplicationTags.BACNET_APPLICATION_TAG_DOUBLE, Convert.ToDouble(value, CultureInfo.InvariantCulture)),
            TagDataType.String => new(BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
            TagDataType.Enum => new(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, checked(Convert.ToUInt32(value, CultureInfo.InvariantCulture))),
            TagDataType.DateTime => new(Convert.ToDateTime(value, CultureInfo.InvariantCulture)),
            _ => throw new NotSupportedException($"BACnet encoding does not support EliteSCADA TAG type '{sourceType}'.")
        };

        return new[] { encoded };
    }

    public static IReadOnlyCollection<BacnetValue> EncodeRelinquish(BacnetBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        if (!binding.WritePriority.HasValue)
            throw new InvalidOperationException("BACnet relinquish requires an explicit write priority from 1 to 16.");
        return new[] { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, null!) };
    }

    private static bool DecodeBoolean(object raw, BacnetBinding binding)
    {
        if (raw is bool boolean) return boolean;
        if (IsBinaryPresentValue(binding))
        {
            var numeric = Convert.ToUInt32(raw, CultureInfo.InvariantCulture);
            return numeric switch
            {
                0 => false,
                1 => true,
                _ => throw new InvalidCastException($"Binary BACnet Present_Value must be 0 or 1, received {numeric}.")
            };
        }
        throw TypeMismatch(TagDataType.Boolean, raw);
    }

    private static bool IsAnalogPresentValue(BacnetBinding binding)
        => binding.PropertyIdentifier == PresentValueProperty && binding.ObjectType is 0 or 1 or 2;

    private static bool IsBinaryPresentValue(BacnetBinding binding)
        => binding.PropertyIdentifier == PresentValueProperty && binding.ObjectType is 3 or 4 or 5;

    private static InvalidOperationException TypeMismatch(TagDataType targetType, object raw, Exception? inner = null)
        => new($"BACnet value of CLR type '{raw.GetType().Name}' is not valid for EliteSCADA TAG type '{targetType}'.", inner);
}
