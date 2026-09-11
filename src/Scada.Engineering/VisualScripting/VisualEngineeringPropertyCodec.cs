using System.Collections.ObjectModel;
using System.Text.Json;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.VisualScripting;

/// <summary>
/// Canonical JSON-native codec for visual Engineering property values introduced
/// with Engineering schema v12. Legacy schema-v10/v11 string values are accepted
/// only when explicitly requested by the migration boundary.
/// </summary>
public static class VisualEngineeringPropertyCodec
{
    public const int TypedSchemaVersion = 12;

    public static IReadOnlyDictionary<string, VisualPropertyValue> Decode(
        VisualObjectPropertySchema schema,
        IReadOnlyDictionary<string, JsonElement>? serializedValues,
        bool allowLegacyStringValues = false)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (allowLegacyStringValues)
            return DecodeLegacy(schema, serializedValues);

        var decoded = new Dictionary<string, VisualPropertyValue>(StringComparer.Ordinal);
        foreach (var pair in serializedValues ?? EmptySerialized())
        {
            var definition = schema.GetRequired(pair.Key);
            var value = DecodeCurrentValue(definition, pair.Value);
            definition.ValidateValue(value);
            decoded.Add(pair.Key, value);
        }

        return new ReadOnlyDictionary<string, VisualPropertyValue>(decoded);
    }

    public static IReadOnlyDictionary<string, JsonElement> Encode(
        VisualObjectPropertySchema schema,
        IReadOnlyDictionary<string, VisualPropertyValue>? typedValues)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var encoded = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var pair in typedValues ?? EmptyTyped())
        {
            var definition = schema.GetRequired(pair.Key);
            definition.ValidateValue(pair.Value);
            encoded.Add(pair.Key, EncodeValue(pair.Value));
        }

        return new ReadOnlyDictionary<string, JsonElement>(encoded);
    }

    public static IReadOnlyDictionary<string, JsonElement> Normalize(
        VisualObjectPropertySchema schema,
        IReadOnlyDictionary<string, JsonElement>? serializedValues,
        int sourceSchemaVersion)
    {
        var decoded = Decode(
            schema,
            serializedValues,
            allowLegacyStringValues: sourceSchemaVersion < TypedSchemaVersion);
        return Encode(schema, decoded);
    }

    public static IReadOnlyDictionary<string, JsonElement>? CloneUntyped(
        IReadOnlyDictionary<string, JsonElement>? values)
    {
        if (values is null)
            return null;

        return new ReadOnlyDictionary<string, JsonElement>(
            values.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, VisualPropertyValue> DecodeLegacy(
        VisualObjectPropertySchema schema,
        IReadOnlyDictionary<string, JsonElement>? serializedValues)
    {
        var legacy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in serializedValues ?? EmptySerialized())
        {
            if (pair.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    $"Legacy visual property '{pair.Key}' must be represented as a JSON string.");
            }

            legacy.Add(pair.Key, pair.Value.GetString()
                ?? throw new InvalidDataException($"Legacy visual property '{pair.Key}' cannot be null."));
        }

        return LegacyVisualEngineeringPropertyCodec.Decode(schema, legacy);
    }

    private static VisualPropertyValue DecodeCurrentValue(
        VisualPropertyDefinition definition,
        JsonElement serialized)
    {
        return definition.ValueKind switch
        {
            VisualPropertyValueKind.Boolean => DecodeBoolean(definition.Key, serialized),
            VisualPropertyValueKind.Number => DecodeNumber(definition.Key, serialized),
            VisualPropertyValueKind.Integer => DecodeInteger(definition.Key, serialized),
            VisualPropertyValueKind.String => new VisualStringValue(DecodeString(definition.Key, serialized)),
            VisualPropertyValueKind.Color => new VisualColorValue(DecodeString(definition.Key, serialized)),
            VisualPropertyValueKind.ResourceReference => new VisualResourceReferenceValue(DecodeString(definition.Key, serialized)),
            VisualPropertyValueKind.AssetReference => DecodeAssetReference(definition.Key, serialized),
            _ => throw new InvalidDataException(
                $"Unsupported visual property value kind '{definition.ValueKind}' for '{definition.Key}'.")
        };
    }

    private static VisualPropertyValue DecodeBoolean(string propertyKey, JsonElement serialized)
    {
        if (serialized.ValueKind == JsonValueKind.True) return new VisualBooleanValue(true);
        if (serialized.ValueKind == JsonValueKind.False) return new VisualBooleanValue(false);

        throw WrongKind(propertyKey, "a JSON boolean", serialized);
    }

    private static VisualPropertyValue DecodeNumber(string propertyKey, JsonElement serialized)
    {
        if (serialized.ValueKind != JsonValueKind.Number ||
            !serialized.TryGetDouble(out var value) ||
            double.IsNaN(value) ||
            double.IsInfinity(value))
        {
            throw WrongKind(propertyKey, "a finite JSON number", serialized);
        }

        return new VisualNumberValue(value);
    }

    private static VisualPropertyValue DecodeInteger(string propertyKey, JsonElement serialized)
    {
        if (serialized.ValueKind != JsonValueKind.Number || !serialized.TryGetInt32(out var value))
            throw WrongKind(propertyKey, "a signed 32-bit JSON integer", serialized);

        return new VisualIntegerValue(value);
    }

    private static string DecodeString(string propertyKey, JsonElement serialized)
    {
        if (serialized.ValueKind != JsonValueKind.String)
            throw WrongKind(propertyKey, "a JSON string", serialized);

        return serialized.GetString()
            ?? throw new InvalidDataException($"Visual property '{propertyKey}' cannot be null.");
    }

    private static VisualPropertyValue DecodeAssetReference(string propertyKey, JsonElement serialized)
    {
        if (serialized.ValueKind == JsonValueKind.Null)
            return new VisualAssetReferenceValue(null);

        if (serialized.ValueKind != JsonValueKind.Object)
            throw WrongKind(propertyKey, "null or an asset-reference object", serialized);

        var properties = serialized.EnumerateObject().ToArray();
        if (properties.Length != 1 ||
            !properties[0].NameEquals("assetId") ||
            properties[0].Value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"Visual property '{propertyKey}' requires exactly {{ \"assetId\": \"...\" }} or null.");
        }

        return new VisualAssetReferenceValue(properties[0].Value.GetString());
    }

    private static JsonElement EncodeValue(VisualPropertyValue value) => value switch
    {
        VisualBooleanValue boolean => JsonSerializer.SerializeToElement(boolean.Value),
        VisualNumberValue number => JsonSerializer.SerializeToElement(number.Value),
        VisualIntegerValue integer => JsonSerializer.SerializeToElement(integer.Value),
        VisualStringValue text => JsonSerializer.SerializeToElement(text.Value),
        VisualColorValue color => JsonSerializer.SerializeToElement(color.Value),
        VisualResourceReferenceValue resource => JsonSerializer.SerializeToElement(resource.ResourceId),
        VisualAssetReferenceValue { AssetId: null } => JsonSerializer.SerializeToElement<string?>(null),
        VisualAssetReferenceValue asset => JsonSerializer.SerializeToElement(new { assetId = asset.AssetId! }),
        _ => throw new InvalidDataException($"Unsupported visual property value kind '{value.Kind}'.")
    };

    private static InvalidDataException WrongKind(
        string propertyKey,
        string expected,
        JsonElement actual) =>
        new($"Visual property '{propertyKey}' requires {expected}; received JSON {actual.ValueKind}.");

    private static IReadOnlyDictionary<string, JsonElement> EmptySerialized() =>
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, VisualPropertyValue> EmptyTyped() =>
        new Dictionary<string, VisualPropertyValue>(StringComparer.Ordinal);
}

/// <summary>
/// Converts imported visual trees to the current JSON-native property format.
/// Registered built-ins are migrated through their schema. Unknown/custom legacy
/// types preserve their historical string values as JSON strings rather than
/// guessing a type that the legacy package never declared.
/// </summary>
public static class VisualEngineeringPropertyMigration
{
    public static ScreenEngineeringDto NormalizeScreen(ScreenEngineeringDto screen, int sourceSchemaVersion) =>
        screen with { Elements = NormalizeElements(screen.Elements, sourceSchemaVersion) };

    public static PopupEngineeringDto NormalizePopup(PopupEngineeringDto popup, int sourceSchemaVersion) =>
        popup with { Elements = NormalizeElements(popup.Elements, sourceSchemaVersion) };

    public static IReadOnlyCollection<VisualElementEngineeringDto>? NormalizeElements(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        int sourceSchemaVersion)
    {
        if (elements is null)
            return null;

        return elements
            .Select(element => NormalizeElement(element, sourceSchemaVersion))
            .ToArray();
    }

    public static IReadOnlyCollection<VisualElementEngineeringDto>? NormalizeCurrentElements(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements) =>
        NormalizeElements(elements, VisualEngineeringPropertyCodec.TypedSchemaVersion);

    private static VisualElementEngineeringDto NormalizeElement(
        VisualElementEngineeringDto element,
        int sourceSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(element);

        var schema = BuiltinVisualObjectSchemas.All.SingleOrDefault(
            candidate => candidate.ObjectTypeKey.Equals(element.Type, StringComparison.Ordinal));

        IReadOnlyDictionary<string, JsonElement>? properties;
        if (schema is not null)
        {
            var scalarProperties = element.Properties;
            var structuralProperty = StructuralPropertyFor(element.Type);
            JsonElement? structuralValue = null;

            if (structuralProperty is not null &&
                element.Properties is not null &&
                element.Properties.TryGetValue(structuralProperty, out var structural))
            {
                structuralValue = structural.Clone();
                scalarProperties = element.Properties
                    .Where(property => !property.Key.Equals(structuralProperty, StringComparison.Ordinal))
                    .ToDictionary(
                        property => property.Key,
                        property => property.Value.Clone(),
                        StringComparer.Ordinal);
            }

            var normalizedScalars = VisualEngineeringPropertyCodec.Normalize(
                schema,
                scalarProperties,
                sourceSchemaVersion);

            if (structuralProperty is not null && structuralValue.HasValue)
            {
                var withStructuralPayload = normalizedScalars.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Clone(),
                    StringComparer.Ordinal);
                withStructuralPayload[structuralProperty] = structuralValue.Value.Clone();
                properties = new ReadOnlyDictionary<string, JsonElement>(withStructuralPayload);
            }
            else
            {
                properties = normalizedScalars;
            }
        }
        else
        {
            properties = VisualEngineeringPropertyCodec.CloneUntyped(element.Properties);
        }

        return element with
        {
            Properties = properties?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal),
            Children = NormalizeElements(element.Children, sourceSchemaVersion)
        };
    }

    private static string? StructuralPropertyFor(string objectType) => objectType switch
    {
        BuiltinVisualObjectSchemas.PolygonType => "points",
        BuiltinVisualObjectSchemas.TrendType => BuiltinVisualObjectSchemas.TrendPensProperty,
        BuiltinVisualObjectSchemas.AlarmBrowserType => BuiltinVisualObjectSchemas.BrowserConfigProperty,
        BuiltinVisualObjectSchemas.EventBrowserType => BuiltinVisualObjectSchemas.BrowserConfigProperty,
        _ => null
    };
}
