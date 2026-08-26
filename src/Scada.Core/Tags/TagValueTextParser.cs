using System.Globalization;

namespace Scada.Core.Tags;

public static class TagValueTextParser
{
    public static bool TryParse(TagDataType dataType, string text, out object? value)
    {
        value = null;
        if (text is null) return false;

        switch (dataType)
        {
            case TagDataType.Boolean:
                if (bool.TryParse(text, out var boolean))
                {
                    value = boolean;
                    return true;
                }
                if (text == "1")
                {
                    value = true;
                    return true;
                }
                if (text == "0")
                {
                    value = false;
                    return true;
                }
                return false;

            case TagDataType.Int16:
                if (short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int16))
                {
                    value = int16;
                    return true;
                }
                return false;

            case TagDataType.Int32:
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int32))
                {
                    value = int32;
                    return true;
                }
                return false;

            case TagDataType.Int64:
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int64))
                {
                    value = int64;
                    return true;
                }
                return false;

            case TagDataType.Float:
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var single))
                {
                    value = single;
                    return true;
                }
                return false;

            case TagDataType.Double:
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                {
                    value = doubleValue;
                    return true;
                }
                return false;

            case TagDataType.String:
                value = text;
                return true;

            case TagDataType.DateTime:
                if (DateTimeOffset.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var timestamp))
                {
                    value = timestamp;
                    return true;
                }
                return false;

            case TagDataType.Enum:
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var enumNumeric))
                    value = enumNumeric;
                else
                    value = text;
                return true;

            default:
                return false;
        }
    }
}
