using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Scada.Engineering.VisualScripting;

/// <summary>
/// Transitional codec between the schema-v10/v11 string property bag and the
/// typed visual property model. Conversion is always driven by the declared
/// VisualObjectPropertySchema; values are never type-guessed from text.
///
/// This exists to keep the graphical editor and Runtime on one conversion path
/// until a later schema revision can store typed visual values directly.
/// </summary>
public static class LegacyVisualEngineeringPropertyCodec
{
    private static readonly Regex CanonicalInteger = new(
        @"^-?(?:0|[1-9][0-9]*)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex CanonicalNumber = new(
        @"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$",
        RegexOptions.CultureInvariant);

    public static IReadOnlyDictionary<string, VisualPropertyValue> Decode(
        VisualObjectPropertySchema schema,
        IReadOnlyDictionary<string, string>? serializedValues)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var decoded = new Dictionary<string, VisualPropertyValue>(StringComparer.Ordinal);
        foreach (var pair in serializedValues ?? new Dictionary<string, string>())
        {
            var definition = schema.GetRequired(pair.Key);
            var value = DecodeValue(definition, pair.Value);
            definition.ValidateValue(value);
            decoded.Add(pair.Key, value);
        }

        return new ReadOnlyDictionary<string, VisualPropertyValue>(decoded);
    }

    public static IReadOnlyDictionary<string, string> Encode(
        VisualObjectPropertySchema schema,
        IReadOnlyDictionary<string, VisualPropertyValue>? typedValues)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var encoded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in typedValues ?? new Dictionary<string, VisualPropertyValue>())
        {
            var definition = schema.GetRequired(pair.Key);
            definition.ValidateValue(pair.Value);

            var serialized = EncodeValue(pair.Value);
            if (serialized is not null)
                encoded.Add(pair.Key, serialized);
        }

        return new ReadOnlyDictionary<string, string>(encoded);
    }

    private static VisualPropertyValue DecodeValue(VisualPropertyDefinition definition, string serialized)
    {
        ArgumentNullException.ThrowIfNull(serialized);

        return definition.ValueKind switch
        {
            VisualPropertyValueKind.Boolean => DecodeBoolean(definition.Key, serialized),
            VisualPropertyValueKind.Number => DecodeNumber(definition.Key, serialized),
            VisualPropertyValueKind.Integer => DecodeInteger(definition.Key, serialized),
            VisualPropertyValueKind.String => new VisualStringValue(serialized),
            VisualPropertyValueKind.Color => new VisualColorValue(serialized),
            VisualPropertyValueKind.ResourceReference => new VisualResourceReferenceValue(serialized),
            VisualPropertyValueKind.AssetReference => new VisualAssetReferenceValue(serialized),
            _ => throw new InvalidDataException(
                $"Unsupported visual property value kind '{definition.ValueKind}' for '{definition.Key}'.")
        };
    }

    private static VisualPropertyValue DecodeBoolean(string propertyKey, string serialized)
    {
        if (serialized == "true") return new VisualBooleanValue(true);
        if (serialized == "false") return new VisualBooleanValue(false);

        throw new InvalidDataException(
            $"Visual property '{propertyKey}' requires canonical boolean text 'true' or 'false'.");
    }

    private static VisualPropertyValue DecodeNumber(string propertyKey, string serialized)
    {
        if (!CanonicalNumber.IsMatch(serialized) ||
            !double.TryParse(
                serialized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value) ||
            double.IsNaN(value) ||
            double.IsInfinity(value))
        {
            throw new InvalidDataException(
                $"Visual property '{propertyKey}' requires a canonical finite invariant-culture number.");
        }

        return new VisualNumberValue(value);
    }

    private static VisualPropertyValue DecodeInteger(string propertyKey, string serialized)
    {
        if (!CanonicalInteger.IsMatch(serialized) ||
            !int.TryParse(
                serialized,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new InvalidDataException(
                $"Visual property '{propertyKey}' requires a canonical 32-bit invariant-culture integer.");
        }

        return new VisualIntegerValue(value);
    }

    private static string? EncodeValue(VisualPropertyValue value) => value switch
    {
        VisualBooleanValue boolean => boolean.Value ? "true" : "false",
        VisualNumberValue number => number.Value.ToString("R", CultureInfo.InvariantCulture),
        VisualIntegerValue integer => integer.Value.ToString(CultureInfo.InvariantCulture),
        VisualStringValue text => text.Value,
        VisualColorValue color => color.Value,
        VisualResourceReferenceValue resource => resource.ResourceId,
        VisualAssetReferenceValue { AssetId: not null } asset => asset.AssetId,
        VisualAssetReferenceValue => null,
        _ => throw new InvalidDataException(
            $"Unsupported visual property value kind '{value.Kind}'.")
    };
}
