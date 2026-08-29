using System.Globalization;
using System.Text;
using System.Text.Json;
using Scada.Core.Tags;

namespace Scada.Drivers.Mqtt;

public sealed record MqttDecodedPayload(
    object? Value,
    TagQuality Quality,
    DateTimeOffset? SourceTimestamp,
    bool Retained);

public sealed class MqttPayloadException : FormatException
{
    public MqttPayloadException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public static class MqttPayloadCodec
{
    public static MqttDecodedPayload Decode(
        MqttPoint point,
        ReadOnlySpan<byte> payload,
        bool retained,
        DateTimeOffset receivedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(point);
        point.Validate();

        if (receivedAtUtc == default)
            throw new ArgumentOutOfRangeException(nameof(receivedAtUtc));

        try
        {
            object? value;
            DateTimeOffset? sourceTimestamp = null;

            if (point.PayloadFormat == MqttPayloadFormat.Utf8Scalar)
            {
                value = DecodeUtf8Scalar(point.Tag.DataType, payload);
            }
            else
            {
                using var document = JsonDocument.Parse(payload.ToArray());
                var valueElement = ResolveJsonPointer(document.RootElement, point.JsonPointer, "value");
                value = DecodeJsonScalar(point.Tag.DataType, valueElement);

                if (point.SourceTimestampJsonPointer is not null)
                {
                    var timestampElement = ResolveJsonPointer(
                        document.RootElement,
                        point.SourceTimestampJsonPointer,
                        "source timestamp");
                    sourceTimestamp = DecodeSourceTimestamp(timestampElement);
                }
            }

            if (point.SourceTimestampRequired && sourceTimestamp is null)
                throw new MqttPayloadException("MQTT payload did not provide the required source timestamp.");

            var quality = retained && sourceTimestamp is null &&
                          point.RetainedValuePolicy == MqttRetainedValuePolicy.MarkStaleWithoutSourceTimestamp
                ? TagQuality.Stale
                : TagQuality.Good;

            return new MqttDecodedPayload(value, quality, sourceTimestamp, retained);
        }
        catch (MqttPayloadException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new MqttPayloadException("MQTT JSON payload is malformed.", ex);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidOperationException)
        {
            throw new MqttPayloadException("MQTT payload cannot be converted to the configured TAG type.", ex);
        }
    }

    public static byte[] Encode(MqttPoint point, object? value)
    {
        ArgumentNullException.ThrowIfNull(point);
        point.Validate();
        if (!point.Writable)
            throw new InvalidOperationException($"MQTT TAG '{point.Tag.Path}' is not configured for writes.");

        var normalized = NormalizeWriteValue(point.Tag.DataType, value);
        if (point.PayloadFormat == MqttPayloadFormat.Utf8Scalar)
            return Encoding.UTF8.GetBytes(FormatUtf8Scalar(point.Tag.DataType, normalized));

        if (!string.IsNullOrEmpty(point.JsonPointer))
        {
            throw new InvalidOperationException(
                "Publishing a JSON field requires an explicit envelope/template contract; non-root JSON extraction is read-only in this slice.");
        }

        return JsonSerializer.SerializeToUtf8Bytes(normalized, normalized?.GetType() ?? typeof(object));
    }

    private static object? DecodeUtf8Scalar(TagDataType dataType, ReadOnlySpan<byte> payload)
    {
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(payload);
        }
        catch (DecoderFallbackException ex)
        {
            throw new MqttPayloadException("MQTT scalar payload is not valid UTF-8.", ex);
        }

        if (dataType is TagDataType.String or TagDataType.Enum)
            return text;

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            throw new MqttPayloadException("MQTT scalar payload is empty.");

        return dataType switch
        {
            TagDataType.Boolean => ParseBoolean(trimmed),
            TagDataType.Int16 => ParseInt16(trimmed),
            TagDataType.Int32 => ParseInt32(trimmed),
            TagDataType.Int64 => ParseInt64(trimmed),
            TagDataType.Float => ParseFloat(trimmed),
            TagDataType.Double => ParseDouble(trimmed),
            TagDataType.DateTime => ParseDateTime(trimmed),
            _ => throw new MqttPayloadException($"MQTT scalar payload is invalid for TAG type '{dataType}'.")
        };
    }

    private static object? DecodeJsonScalar(TagDataType dataType, JsonElement element)
    {
        return dataType switch
        {
            TagDataType.Boolean => DecodeJsonBoolean(element),
            TagDataType.Int16 => CheckedInteger(element, short.MinValue, short.MaxValue, value => (short)value),
            TagDataType.Int32 => CheckedInteger(element, int.MinValue, int.MaxValue, value => (int)value),
            TagDataType.Int64 => CheckedInteger(element, long.MinValue, long.MaxValue, value => value),
            TagDataType.Float => DecodeJsonFloat(element),
            TagDataType.Double => DecodeJsonDouble(element),
            TagDataType.String => DecodeJsonString(element, TagDataType.String),
            TagDataType.Enum => DecodeJsonString(element, TagDataType.Enum),
            TagDataType.DateTime => DecodeJsonDateTime(element),
            _ => throw new MqttPayloadException($"MQTT JSON value is invalid for TAG type '{dataType}'.")
        };
    }

    private static bool ParseBoolean(string text)
    {
        if (!bool.TryParse(text, out var value))
            throw new MqttPayloadException("MQTT scalar payload must be 'true' or 'false' for a Boolean TAG.");
        return value;
    }

    private static short ParseInt16(string text)
    {
        if (!short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new MqttPayloadException("MQTT scalar payload is invalid for an Int16 TAG.");
        return value;
    }

    private static int ParseInt32(string text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new MqttPayloadException("MQTT scalar payload is invalid for an Int32 TAG.");
        return value;
    }

    private static long ParseInt64(string text)
    {
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new MqttPayloadException("MQTT scalar payload is invalid for an Int64 TAG.");
        return value;
    }

    private static float ParseFloat(string text)
    {
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value))
            throw new MqttPayloadException("MQTT scalar payload is invalid for a finite Float TAG.");
        return value;
    }

    private static double ParseDouble(string text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new MqttPayloadException("MQTT scalar payload is invalid for a finite Double TAG.");
        return value;
    }

    private static DateTimeOffset ParseDateTime(string text)
    {
        if (!DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var value))
        {
            throw new MqttPayloadException("MQTT scalar payload is invalid for a DateTime TAG.");
        }

        return value;
    }

    private static bool DecodeJsonBoolean(JsonElement element)
    {
        if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new MqttPayloadException("MQTT JSON value is invalid for a Boolean TAG.");
        return element.GetBoolean();
    }

    private static float DecodeJsonFloat(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetSingle(out var value) || !float.IsFinite(value))
            throw new MqttPayloadException("MQTT JSON value is invalid for a finite Float TAG.");
        return value;
    }

    private static double DecodeJsonDouble(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out var value) || !double.IsFinite(value))
            throw new MqttPayloadException("MQTT JSON value is invalid for a finite Double TAG.");
        return value;
    }

    private static string DecodeJsonString(JsonElement element, TagDataType dataType)
    {
        if (element.ValueKind != JsonValueKind.String)
            throw new MqttPayloadException($"MQTT JSON value is invalid for a {dataType} TAG.");
        return element.GetString() ?? string.Empty;
    }

    private static DateTimeOffset DecodeJsonDateTime(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
            throw new MqttPayloadException("MQTT JSON value is invalid for a DateTime TAG.");
        return DecodeDateTimeString(element.GetString());
    }

    private static object CheckedInteger(
        JsonElement element,
        long minimum,
        long maximum,
        Func<long, object> projector)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var value) || value < minimum || value > maximum)
            throw new MqttPayloadException("MQTT JSON integer is outside the configured TAG range or is not integral.");
        return projector(value);
    }

    private static DateTimeOffset DecodeDateTimeString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw new MqttPayloadException("MQTT timestamp must be an unambiguous ISO-8601 date/time string.");
        }

        return parsed;
    }

    private static DateTimeOffset DecodeSourceTimestamp(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
            throw new MqttPayloadException("MQTT source timestamp must be an ISO-8601 JSON string.");
        return DecodeDateTimeString(element.GetString());
    }

    private static JsonElement ResolveJsonPointer(JsonElement root, string? pointer, string purpose)
    {
        if (string.IsNullOrEmpty(pointer)) return root;

        var current = root;
        foreach (var encodedToken in pointer.Split('/').Skip(1))
        {
            var token = DecodePointerToken(encodedToken);
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(token, out current))
                    throw new MqttPayloadException($"Configured MQTT JSON {purpose} pointer '{pointer}' was not found.");
                continue;
            }

            if (current.ValueKind == JsonValueKind.Array &&
                int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
                index >= 0 && index < current.GetArrayLength())
            {
                current = current[index];
                continue;
            }

            throw new MqttPayloadException($"Configured MQTT JSON {purpose} pointer '{pointer}' was not found.");
        }

        return current;
    }

    private static string DecodePointerToken(string token)
    {
        var builder = new StringBuilder(token.Length);
        for (var index = 0; index < token.Length; index++)
        {
            if (token[index] != '~')
            {
                builder.Append(token[index]);
                continue;
            }

            if (index + 1 >= token.Length)
                throw new MqttPayloadException("MQTT JSON Pointer contains an invalid escape sequence.");

            builder.Append(token[index + 1] switch
            {
                '0' => '~',
                '1' => '/',
                _ => throw new MqttPayloadException("MQTT JSON Pointer contains an invalid escape sequence.")
            });
            index++;
        }
        return builder.ToString();
    }

    private static object? NormalizeWriteValue(TagDataType dataType, object? value)
    {
        return dataType switch
        {
            TagDataType.Boolean when value is bool typed => typed,
            TagDataType.Int16 when value is short typed => typed,
            TagDataType.Int32 when value is int typed => typed,
            TagDataType.Int64 when value is long typed => typed,
            TagDataType.Float when value is float typed && float.IsFinite(typed) => typed,
            TagDataType.Double when value is double typed && double.IsFinite(typed) => typed,
            TagDataType.String when value is string typed => typed,
            TagDataType.Enum when value is string typed => typed,
            TagDataType.DateTime when value is DateTimeOffset typed => typed,
            TagDataType.DateTime when value is DateTime typed => new DateTimeOffset(typed.ToUniversalTime()),
            _ => throw new MqttPayloadException(
                $"MQTT write value type '{value?.GetType().Name ?? "null"}' is invalid for TAG type '{dataType}'.")
        };
    }

    private static string FormatUtf8Scalar(TagDataType dataType, object? value)
    {
        return dataType switch
        {
            TagDataType.Boolean => ((bool)value!).ToString().ToLowerInvariant(),
            TagDataType.Int16 => ((short)value!).ToString(CultureInfo.InvariantCulture),
            TagDataType.Int32 => ((int)value!).ToString(CultureInfo.InvariantCulture),
            TagDataType.Int64 => ((long)value!).ToString(CultureInfo.InvariantCulture),
            TagDataType.Float => ((float)value!).ToString("R", CultureInfo.InvariantCulture),
            TagDataType.Double => ((double)value!).ToString("R", CultureInfo.InvariantCulture),
            TagDataType.String or TagDataType.Enum => (string)value!,
            TagDataType.DateTime => ((DateTimeOffset)value!).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            _ => throw new MqttPayloadException($"MQTT TAG type '{dataType}' is not supported for scalar publishing.")
        };
    }
}
