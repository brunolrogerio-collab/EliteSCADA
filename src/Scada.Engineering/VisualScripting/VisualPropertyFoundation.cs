using System.Collections.ObjectModel;

namespace Scada.Engineering.VisualScripting;

public enum VisualPropertyValueKind
{
    Boolean,
    Number,
    Integer,
    String,
    Color,
    ResourceReference,
    AssetReference
}

public abstract record VisualPropertyValue(VisualPropertyValueKind Kind);

public sealed record VisualBooleanValue(bool Value)
    : VisualPropertyValue(VisualPropertyValueKind.Boolean);

public sealed record VisualNumberValue(double Value)
    : VisualPropertyValue(VisualPropertyValueKind.Number);

public sealed record VisualIntegerValue(int Value)
    : VisualPropertyValue(VisualPropertyValueKind.Integer);

public sealed record VisualStringValue(string Value)
    : VisualPropertyValue(VisualPropertyValueKind.String);

public sealed record VisualColorValue(string Value)
    : VisualPropertyValue(VisualPropertyValueKind.Color);

/// <summary>
/// Legacy resource reference retained for compatibility with the pre-Wave-07
/// visual foundation. New image-capable schemas use VisualAssetReferenceValue.
/// </summary>
public sealed record VisualResourceReferenceValue(string ResourceId)
    : VisualPropertyValue(VisualPropertyValueKind.ResourceReference);

/// <summary>
/// Stable project-asset identity. Null means no asset is selected. Descriptive
/// metadata such as name, media type, dimensions and content hash belongs to the
/// canonical asset entity rather than being duplicated into property values.
/// </summary>
public sealed record VisualAssetReferenceValue(string? AssetId)
    : VisualPropertyValue(VisualPropertyValueKind.AssetReference);

public sealed record VisualPropertyConstraints
{
    public double? Minimum { get; init; }

    public double? Maximum { get; init; }

    public IReadOnlyCollection<string> AllowedValues { get; init; } = Array.Empty<string>();

    public bool AllowEmptyString { get; init; } = true;
}

public sealed class VisualPropertyDefinition
{
    public VisualPropertyDefinition(
        string key,
        VisualPropertyValue defaultValue,
        bool engineeringEditable = true,
        bool runtimeReadable = true,
        bool runtimeWritable = true,
        bool supportsBinding = true,
        bool animatable = false,
        VisualPropertyConstraints? constraints = null,
        string? unit = null,
        string? presentationHint = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Visual property key is required.", nameof(key));

        ArgumentNullException.ThrowIfNull(defaultValue);

        Key = key;
        DefaultValue = defaultValue;
        EngineeringEditable = engineeringEditable;
        RuntimeReadable = runtimeReadable;
        RuntimeWritable = runtimeWritable;
        SupportsBinding = supportsBinding;
        Animatable = animatable;
        Constraints = constraints ?? new VisualPropertyConstraints();
        Unit = unit;
        PresentationHint = presentationHint;

        ValidateValue(defaultValue);
        if (Animatable && defaultValue.Kind is not (VisualPropertyValueKind.Number or VisualPropertyValueKind.Integer or VisualPropertyValueKind.Color))
            throw new ArgumentException($"Property '{key}' is marked animatable but uses unsupported value kind '{defaultValue.Kind}'.", nameof(animatable));
    }

    public string Key { get; }

    public VisualPropertyValueKind ValueKind => DefaultValue.Kind;

    public VisualPropertyValue DefaultValue { get; }

    public bool EngineeringEditable { get; }

    public bool RuntimeReadable { get; }

    public bool RuntimeWritable { get; }

    public bool SupportsBinding { get; }

    public bool Animatable { get; }

    public VisualPropertyConstraints Constraints { get; }

    public string? Unit { get; }

    public string? PresentationHint { get; }

    public void ValidateValue(VisualPropertyValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Kind != ValueKind)
            throw new ArgumentException(
                $"Property '{Key}' expects '{ValueKind}' but received '{value.Kind}'.",
                nameof(value));

        switch (value)
        {
            case VisualNumberValue number:
                ValidateNumeric(number.Value);
                break;
            case VisualIntegerValue integer:
                ValidateNumeric(integer.Value);
                break;
            case VisualStringValue text:
                ValidateString(text.Value);
                break;
            case VisualColorValue color:
                ValidateColor(color.Value);
                break;
            case VisualResourceReferenceValue resource:
                if (string.IsNullOrWhiteSpace(resource.ResourceId))
                    throw new ArgumentException($"Property '{Key}' requires a non-empty resource reference.", nameof(value));
                break;
            case VisualAssetReferenceValue asset:
                if (asset.AssetId is not null && !IsStableAssetId(asset.AssetId))
                    throw new ArgumentException(
                        $"Property '{Key}' requires a stable project asset identity rather than a path or URL.",
                        nameof(value));
                break;
        }
    }

    private void ValidateNumeric(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentException($"Property '{Key}' requires a finite numeric value.");

        if (Constraints.Minimum.HasValue && value < Constraints.Minimum.Value)
            throw new ArgumentOutOfRangeException(Key, value, $"Property '{Key}' must be >= {Constraints.Minimum.Value}.");

        if (Constraints.Maximum.HasValue && value > Constraints.Maximum.Value)
            throw new ArgumentOutOfRangeException(Key, value, $"Property '{Key}' must be <= {Constraints.Maximum.Value}.");
    }

    private void ValidateString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!Constraints.AllowEmptyString && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Property '{Key}' cannot be empty.");

        if (Constraints.AllowedValues.Count > 0 &&
            !Constraints.AllowedValues.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Property '{Key}' does not allow value '{value}'.",
                nameof(value));
        }
    }

    private void ValidateColor(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!IsStableHexColor(value))
            throw new ArgumentException(
                $"Property '{Key}' requires a stable #RRGGBB or #RRGGBBAA color value.",
                nameof(value));
    }

    private static bool IsStableHexColor(string value)
    {
        if (value.Length is not (7 or 9) || value[0] != '#')
            return false;

        for (var index = 1; index < value.Length; index++)
        {
            if (!Uri.IsHexDigit(value[index]))
                return false;
        }

        return true;
    }

    private static bool IsStableAssetId(string value)
    {
        if (value.Length is < 1 or > 128 || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            return false;
        if (value.Any(character => character is '/' or '\\' || character < ' ' || character == '\u007f'))
            return false;

        var candidate = value.StartsWith("asset:", StringComparison.Ordinal)
            ? value["asset:".Length..]
            : value;
        if (value.Contains(':') && !value.StartsWith("asset:", StringComparison.Ordinal))
            return false;
        if (candidate.Length == 0 || candidate.Contains(':'))
            return false;

        for (var index = 0; index < candidate.Length; index++)
        {
            var character = candidate[index];
            var allowed = character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-';
            if (!allowed || (index == 0 && character is '.' or '_' or '-'))
                return false;
        }

        return true;
    }
}

public sealed class VisualObjectPropertySchema
{
    private readonly IReadOnlyDictionary<string, VisualPropertyDefinition> _properties;

    internal VisualObjectPropertySchema(
        string objectTypeKey,
        IReadOnlyDictionary<string, VisualPropertyDefinition> properties)
    {
        if (string.IsNullOrWhiteSpace(objectTypeKey))
            throw new ArgumentException("Visual object type key is required.", nameof(objectTypeKey));

        ObjectTypeKey = objectTypeKey;
        _properties = new ReadOnlyDictionary<string, VisualPropertyDefinition>(
            properties.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal));
    }

    public string ObjectTypeKey { get; }

    public IReadOnlyDictionary<string, VisualPropertyDefinition> Properties => _properties;

    public bool Declares(string propertyKey) => _properties.ContainsKey(propertyKey);

    public VisualPropertyDefinition GetRequired(string propertyKey)
    {
        if (!_properties.TryGetValue(propertyKey, out var definition))
            throw new KeyNotFoundException(
                $"Visual object type '{ObjectTypeKey}' does not declare property '{propertyKey}'.");

        return definition;
    }
}

public sealed class VisualPropertySchemaBuilder
{
    private readonly Dictionary<string, VisualPropertyDefinition> _properties =
        new(StringComparer.Ordinal);

    public VisualPropertySchemaBuilder(string objectTypeKey)
    {
        if (string.IsNullOrWhiteSpace(objectTypeKey))
            throw new ArgumentException("Visual object type key is required.", nameof(objectTypeKey));

        ObjectTypeKey = objectTypeKey;
    }

    public string ObjectTypeKey { get; }

    public VisualPropertySchemaBuilder Include(IEnumerable<VisualPropertyDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (var definition in definitions)
            Add(definition);

        return this;
    }

    public VisualPropertySchemaBuilder Add(VisualPropertyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_properties.TryAdd(definition.Key, definition))
            throw new InvalidOperationException(
                $"Visual object type '{ObjectTypeKey}' already declares property '{definition.Key}'.");

        return this;
    }

    public VisualObjectPropertySchema Build() =>
        new(ObjectTypeKey, _properties);
}

public static class VisualPropertyKeys
{
    public const string X = "x";
    public const string Y = "y";
    public const string Width = "width";
    public const string Height = "height";
    public const string Rotation = "rotation";
    public const string ScaleX = "scaleX";
    public const string ScaleY = "scaleY";
    public const string ZIndex = "zIndex";
    public const string Visible = "visible";
    public const string Opacity = "opacity";
    public const string FillColor = "fillColor";
    public const string BackgroundColor = "backgroundColor";
    public const string StrokeColor = "strokeColor";
    public const string StrokeWidth = "strokeWidth";
    public const string StrokeStyle = "strokeStyle";
    public const string CornerRadius = "cornerRadius";
    public const string Text = "text";
    public const string TextColor = "textColor";
    public const string FontFamily = "fontFamily";
    public const string FontSize = "fontSize";
    public const string FontWeight = "fontWeight";
    public const string FontStyle = "fontStyle";
    public const string HorizontalAlignment = "horizontalAlignment";
    public const string VerticalAlignment = "verticalAlignment";
    public const string AssetRef = "assetRef";
    public const string ImageFit = "imageFit";
    public const string ImagePositionX = "imagePositionX";
    public const string ImagePositionY = "imagePositionY";
    public const string Value = "value";
    public const string Minimum = "minimum";
    public const string Maximum = "maximum";
    public const string Step = "step";
    public const string Orientation = "orientation";
    public const string InteractionEnabled = "interactionEnabled";
    public const string ReverseDirection = "reverseDirection";
    public const string TrackColor = "trackColor";
    public const string ThumbColor = "thumbColor";

    // Pre-Wave-07 compatibility constant. New visual schemas use AssetRef.
    public const string ImageResourceId = "imageResourceId";
}

public static class CommonVisualPropertyDefinitions
{
    public static IReadOnlyList<VisualPropertyDefinition> Geometry { get; } =
    [
        Number(VisualPropertyKeys.X, 0, animatable: true, unit: "px"),
        Number(VisualPropertyKeys.Y, 0, animatable: true, unit: "px"),
        Number(VisualPropertyKeys.Width, 100, minimum: 0, animatable: true, unit: "px"),
        Number(VisualPropertyKeys.Height, 100, minimum: 0, animatable: true, unit: "px"),
        Integer(VisualPropertyKeys.ZIndex, 0)
    ];

    public static IReadOnlyList<VisualPropertyDefinition> Transform { get; } =
    [
        Number(VisualPropertyKeys.Rotation, 0, animatable: true, unit: "deg"),
        Number(VisualPropertyKeys.ScaleX, 1, minimum: 0, animatable: true),
        Number(VisualPropertyKeys.ScaleY, 1, minimum: 0, animatable: true)
    ];

    public static IReadOnlyList<VisualPropertyDefinition> Visibility { get; } =
    [
        new VisualPropertyDefinition(VisualPropertyKeys.Visible, new VisualBooleanValue(true), animatable: false),
        Number(VisualPropertyKeys.Opacity, 1, minimum: 0, maximum: 1, animatable: true)
    ];

    public static IReadOnlyList<VisualPropertyDefinition> Fill { get; } =
    [
        Color(VisualPropertyKeys.FillColor, "#00000000", animatable: true),
        Color(VisualPropertyKeys.BackgroundColor, "#00000000", animatable: true)
    ];

    public static IReadOnlyList<VisualPropertyDefinition> Stroke { get; } =
    [
        Color(VisualPropertyKeys.StrokeColor, "#000000", animatable: true),
        Number(VisualPropertyKeys.StrokeWidth, 1, minimum: 0, animatable: true, unit: "px"),
        EnumString(VisualPropertyKeys.StrokeStyle, "solid", ["solid", "dashed", "dotted"]),
        Number(VisualPropertyKeys.CornerRadius, 0, minimum: 0, animatable: true, unit: "px")
    ];

    public static IReadOnlyList<VisualPropertyDefinition> Text { get; } =
    [
        String(VisualPropertyKeys.Text, string.Empty),
        Color(VisualPropertyKeys.TextColor, "#000000", animatable: true),
        String(VisualPropertyKeys.FontFamily, "system"),
        Number(VisualPropertyKeys.FontSize, 14, minimum: 1, animatable: true, unit: "px"),
        Integer(VisualPropertyKeys.FontWeight, 400, minimum: 100, maximum: 900),
        EnumString(VisualPropertyKeys.FontStyle, "normal", ["normal", "italic"]),
        EnumString(VisualPropertyKeys.HorizontalAlignment, "left", ["left", "center", "right"]),
        EnumString(VisualPropertyKeys.VerticalAlignment, "middle", ["top", "middle", "bottom"])
    ];

    public static IReadOnlyList<VisualPropertyDefinition> Image { get; } =
    [
        new VisualPropertyDefinition(
            VisualPropertyKeys.AssetRef,
            new VisualAssetReferenceValue(null),
            engineeringEditable: true,
            runtimeReadable: true,
            runtimeWritable: false,
            supportsBinding: false,
            animatable: false,
            presentationHint: "project-asset"),
        EnumString(VisualPropertyKeys.ImageFit, "contain", ["contain", "cover", "fill", "native"]),
        Number(VisualPropertyKeys.ImagePositionX, 0, minimum: 0, maximum: 1, animatable: true),
        Number(VisualPropertyKeys.ImagePositionY, 0, minimum: 0, maximum: 1, animatable: true)
    ];

    public static IReadOnlyList<VisualPropertyDefinition> Slider { get; } =
    [
        Number(VisualPropertyKeys.Value, 0, animatable: true),
        Number(VisualPropertyKeys.Minimum, 0),
        Number(VisualPropertyKeys.Maximum, 100),
        Number(VisualPropertyKeys.Step, 1, minimum: double.Epsilon),
        EnumString(VisualPropertyKeys.Orientation, "horizontal", ["horizontal", "vertical"]),
        new VisualPropertyDefinition(VisualPropertyKeys.InteractionEnabled, new VisualBooleanValue(false), animatable: false),
        new VisualPropertyDefinition(VisualPropertyKeys.ReverseDirection, new VisualBooleanValue(false), animatable: false),
        Color(VisualPropertyKeys.TrackColor, "#6B7280", animatable: true),
        Color(VisualPropertyKeys.ThumbColor, "#E5E7EB", animatable: true)
    ];

    private static VisualPropertyDefinition Number(
        string key,
        double value,
        double? minimum = null,
        double? maximum = null,
        bool animatable = false,
        string? unit = null) =>
        new(
            key,
            new VisualNumberValue(value),
            animatable: animatable,
            constraints: new VisualPropertyConstraints { Minimum = minimum, Maximum = maximum },
            unit: unit);

    private static VisualPropertyDefinition Integer(
        string key,
        int value,
        double? minimum = null,
        double? maximum = null) =>
        new(
            key,
            new VisualIntegerValue(value),
            constraints: new VisualPropertyConstraints { Minimum = minimum, Maximum = maximum });

    private static VisualPropertyDefinition String(string key, string value) =>
        new(key, new VisualStringValue(value));

    private static VisualPropertyDefinition EnumString(
        string key,
        string value,
        IReadOnlyCollection<string> allowedValues) =>
        new(
            key,
            new VisualStringValue(value),
            constraints: new VisualPropertyConstraints
            {
                AllowedValues = allowedValues,
                AllowEmptyString = false
            });

    private static VisualPropertyDefinition Color(
        string key,
        string value,
        bool animatable) =>
        new(key, new VisualColorValue(value), animatable: animatable);
}

public sealed class VisualEngineeringPropertySet
{
    private readonly IReadOnlyDictionary<string, VisualPropertyValue> _engineeredValues;
    private readonly IReadOnlyDictionary<string, VisualPropertyValue> _baseValues;

    public VisualEngineeringPropertySet(
        VisualObjectPropertySchema schema,
        IReadOnlyDictionary<string, VisualPropertyValue>? engineeredValues = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        Schema = schema;

        var explicitValues = new Dictionary<string, VisualPropertyValue>(StringComparer.Ordinal);
        foreach (var pair in engineeredValues ?? new Dictionary<string, VisualPropertyValue>())
        {
            var definition = schema.GetRequired(pair.Key);
            if (!definition.EngineeringEditable)
                throw new InvalidOperationException(
                    $"Property '{pair.Key}' is not editable in Engineering.");

            definition.ValidateValue(pair.Value);
            explicitValues[pair.Key] = pair.Value;
        }

        _engineeredValues = new ReadOnlyDictionary<string, VisualPropertyValue>(explicitValues);

        // Compatibility surface: callers that historically used BaseValues/GetBaseValue
        // still receive an effective design-time value. Runtime source diagnostics must
        // use EngineeredValues/TryGetEngineeredValue so registry defaults are not falsely
        // reported as user-authored Engineering Base values.
        var effectiveValues = schema.Properties.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.DefaultValue,
            StringComparer.Ordinal);
        foreach (var pair in explicitValues)
            effectiveValues[pair.Key] = pair.Value;

        _baseValues = new ReadOnlyDictionary<string, VisualPropertyValue>(effectiveValues);
    }

    public VisualObjectPropertySchema Schema { get; }

    public IReadOnlyDictionary<string, VisualPropertyValue> EngineeredValues => _engineeredValues;

    public IReadOnlyDictionary<string, VisualPropertyValue> BaseValues => _baseValues;

    public bool TryGetEngineeredValue(string propertyKey, out VisualPropertyValue value)
    {
        Schema.GetRequired(propertyKey);
        if (_engineeredValues.TryGetValue(propertyKey, out var engineered))
        {
            value = engineered;
            return true;
        }

        value = null!;
        return false;
    }

    public VisualPropertyValue GetBaseValue(string propertyKey)
    {
        if (!_baseValues.TryGetValue(propertyKey, out var value))
            throw new KeyNotFoundException($"Engineering base/default value for '{propertyKey}' is not declared.");

        return value;
    }
}

public enum VisualPropertyRuntimeSource
{
    EngineeringBase,
    BindingOrExpression,
    Script,
    Animation,
    Default
}

public sealed record VisualResolvedPropertyValue(
    string PropertyKey,
    VisualPropertyValue Value,
    VisualPropertyRuntimeSource Source);

public sealed class VisualRuntimePropertyState
{
    private readonly Dictionary<string, RuntimeLayers> _runtimeLayers =
        new(StringComparer.Ordinal);

    public VisualRuntimePropertyState(
        string runtimeInstanceId,
        VisualEngineeringPropertySet engineering)
    {
        if (string.IsNullOrWhiteSpace(runtimeInstanceId))
            throw new ArgumentException("Runtime visual instance ID is required.", nameof(runtimeInstanceId));

        ArgumentNullException.ThrowIfNull(engineering);

        RuntimeInstanceId = runtimeInstanceId;
        Engineering = engineering;
    }

    public string RuntimeInstanceId { get; }

    public VisualEngineeringPropertySet Engineering { get; }

    public VisualResolvedPropertyValue Resolve(string propertyKey)
    {
        var definition = Engineering.Schema.GetRequired(propertyKey);
        if (!definition.RuntimeReadable)
            throw new InvalidOperationException(
                $"Property '{propertyKey}' is not readable at runtime.");

        _runtimeLayers.TryGetValue(propertyKey, out var layers);

        if (layers?.Animation is not null)
            return new(propertyKey, layers.Animation, VisualPropertyRuntimeSource.Animation);

        if (layers?.Script is not null)
            return new(propertyKey, layers.Script, VisualPropertyRuntimeSource.Script);

        if (layers?.Binding is not null)
            return new(propertyKey, layers.Binding, VisualPropertyRuntimeSource.BindingOrExpression);

        if (Engineering.TryGetEngineeredValue(propertyKey, out var engineered))
            return new(propertyKey, engineered, VisualPropertyRuntimeSource.EngineeringBase);

        return new(propertyKey, definition.DefaultValue, VisualPropertyRuntimeSource.Default);
    }

    public void SetBindingOverride(string propertyKey, VisualPropertyValue value)
    {
        var definition = Engineering.Schema.GetRequired(propertyKey);
        if (!definition.SupportsBinding)
            throw new InvalidOperationException(
                $"Property '{propertyKey}' does not support bindings/expressions.");

        definition.ValidateValue(value);
        GetOrCreateLayers(propertyKey).Binding = value;
    }

    public void SetScriptOverride(string propertyKey, VisualPropertyValue value)
    {
        var definition = Engineering.Schema.GetRequired(propertyKey);
        if (!definition.RuntimeWritable)
            throw new InvalidOperationException(
                $"Property '{propertyKey}' is not writable by a client visual script.");

        definition.ValidateValue(value);
        GetOrCreateLayers(propertyKey).Script = value;
    }

    public void SetAnimationOverride(string propertyKey, VisualPropertyValue value)
    {
        var definition = Engineering.Schema.GetRequired(propertyKey);
        if (!definition.Animatable)
            throw new InvalidOperationException(
                $"Property '{propertyKey}' is not animatable.");

        definition.ValidateValue(value);
        GetOrCreateLayers(propertyKey).Animation = value;
    }

    public void ClearBindingOverride(string propertyKey) =>
        ClearLayer(propertyKey, static layers => layers.Binding = null);

    public void ClearScriptOverride(string propertyKey) =>
        ClearLayer(propertyKey, static layers => layers.Script = null);

    public void ClearAnimationOverride(string propertyKey) =>
        ClearLayer(propertyKey, static layers => layers.Animation = null);

    public void ClearAllRuntimeOverrides() => _runtimeLayers.Clear();

    private RuntimeLayers GetOrCreateLayers(string propertyKey)
    {
        if (_runtimeLayers.TryGetValue(propertyKey, out var existing))
            return existing;

        var created = new RuntimeLayers();
        _runtimeLayers.Add(propertyKey, created);
        return created;
    }

    private void ClearLayer(string propertyKey, Action<RuntimeLayers> clear)
    {
        Engineering.Schema.GetRequired(propertyKey);

        if (!_runtimeLayers.TryGetValue(propertyKey, out var layers))
            return;

        clear(layers);

        if (layers.Binding is null && layers.Script is null && layers.Animation is null)
            _runtimeLayers.Remove(propertyKey);
    }

    private sealed class RuntimeLayers
    {
        public VisualPropertyValue? Binding { get; set; }

        public VisualPropertyValue? Script { get; set; }

        public VisualPropertyValue? Animation { get; set; }
    }
}
