using System.Globalization;
using System.Text.Json;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.ImportExport;

/// <summary>
/// Strict bridge between the public Engineering representation and the Core
/// Internal Memory typed-value model. JSON number tokens are interpreted only
/// according to their declared TagDataType; no cross-type runtime coercion is used.
/// </summary>
public static class MemoryEngineeringValueCodec
{
    internal const string InitialTypeMetadataKey = "engineering.memory.initial.type";
    internal const string InitialJsonMetadataKey = "engineering.memory.initial.json";
    internal const string ReservedMetadataPrefix = "engineering.memory.";

    public static TypedTagValue ToTypedValue(MemoryInitialValueDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new TypedTagValue(value.DataType, ReadValue(value.DataType, value.Value));
    }

    public static MemoryInitialValueDto FromTypedValue(TypedTagValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var json = JsonSerializer.Serialize(value.Value, value.Value.GetType());
        using var document = JsonDocument.Parse(json);
        return new MemoryInitialValueDto(value.DataType, document.RootElement.Clone());
    }

    internal static MemoryInitialValueDto? ReadFromMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null ||
            !metadata.TryGetValue(InitialTypeMetadataKey, out var typeText) ||
            !metadata.TryGetValue(InitialJsonMetadataKey, out var json) ||
            !Enum.TryParse<TagDataType>(typeText, ignoreCase: true, out var dataType))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var dto = new MemoryInitialValueDto(dataType, document.RootElement.Clone());
            _ = ToTypedValue(dto);
            return dto;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            return null;
        }
    }

    internal static void WriteToMetadata(Dictionary<string, string> metadata, MemoryInitialValueDto? value)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        metadata.Remove(InitialTypeMetadataKey);
        metadata.Remove(InitialJsonMetadataKey);

        if (value is null)
            return;

        _ = ToTypedValue(value);
        metadata[InitialTypeMetadataKey] = value.DataType.ToString();
        metadata[InitialJsonMetadataKey] = value.Value.GetRawText();
    }

    internal static Dictionary<string, string>? PublicMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
            return null;

        var result = metadata
            .Where(pair => !pair.Key.StartsWith(ReservedMetadataPrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        return result.Count == 0 ? null : result;
    }

    private static object ReadValue(TagDataType dataType, JsonElement value) => dataType switch
    {
        TagDataType.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
        TagDataType.Int16 when value.ValueKind == JsonValueKind.Number && value.TryGetInt16(out var int16Value) => int16Value,
        TagDataType.Int32 when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var int32Value) => int32Value,
        TagDataType.Int64 when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var int64Value) => int64Value,
        TagDataType.Float when value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var floatValue) && float.IsFinite(floatValue) => floatValue,
        TagDataType.Double when value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var doubleValue) && double.IsFinite(doubleValue) => doubleValue,
        TagDataType.String when value.ValueKind == JsonValueKind.String => value.GetString()!,
        TagDataType.DateTime when value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(
            value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeValue) => dateTimeValue,
        TagDataType.Enum when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var enumValue) => enumValue,
        _ => throw new ArgumentException(
            $"Engineering initial value is not valid for declared data type {dataType}.",
            nameof(value))
    };
}
