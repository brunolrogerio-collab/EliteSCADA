namespace Scada.Engineering.VisualScripting;

/// <summary>
/// Renderer-independent built-in object schemas shared by Engineering, Runtime
/// and the future graphical editor. This is a type/property contract only; it
/// does not implement a canvas, palette UI or renderer.
/// </summary>
public static class BuiltinVisualObjectSchemas
{
    public const string GroupType = "core.group";
    public const string RectangleType = "core.rectangle";
    public const string EllipseType = "core.ellipse";
    public const string LineType = "core.line";
    public const string PolygonType = "core.polygon";
    public const string TextType = "core.text";
    public const string ImageType = "core.image";
    public const string ValueDisplayType = "core.valueDisplay";
    public const string ButtonType = "core.button";
    public const string SliderType = "core.slider";

    private static readonly IReadOnlyDictionary<string, VisualPropertyDefinition> CommonByKey =
        CommonVisualPropertyDefinitions.Geometry
            .Concat(CommonVisualPropertyDefinitions.Transform)
            .Concat(CommonVisualPropertyDefinitions.Visibility)
            .Concat(CommonVisualPropertyDefinitions.Fill)
            .Concat(CommonVisualPropertyDefinitions.Stroke)
            .Concat(CommonVisualPropertyDefinitions.Text)
            .Concat(CommonVisualPropertyDefinitions.Image)
            .Concat(CommonVisualPropertyDefinitions.Slider)
            .ToDictionary(property => property.Key, StringComparer.Ordinal);

    private static readonly HashSet<string> AnalogFillCapableTypes =
        new(StringComparer.Ordinal)
        {
            RectangleType,
            EllipseType
        };

    private static readonly string[] Base =
    [
        VisualPropertyKeys.X,
        VisualPropertyKeys.Y,
        VisualPropertyKeys.Width,
        VisualPropertyKeys.Height,
        VisualPropertyKeys.ZIndex,
        VisualPropertyKeys.Rotation,
        VisualPropertyKeys.ScaleX,
        VisualPropertyKeys.ScaleY,
        VisualPropertyKeys.HorizontalFlip,
        VisualPropertyKeys.VerticalFlip,
        VisualPropertyKeys.Visible,
        VisualPropertyKeys.Opacity,
        VisualPropertyKeys.Tooltip
    ];

    private static readonly string[] Stroke =
    [
        VisualPropertyKeys.StrokeColor,
        VisualPropertyKeys.StrokeWidth,
        VisualPropertyKeys.StrokeStyle
    ];

    private static readonly string[] TextProperties =
    [
        VisualPropertyKeys.Text,
        VisualPropertyKeys.TextColor,
        VisualPropertyKeys.FontFamily,
        VisualPropertyKeys.FontSize,
        VisualPropertyKeys.FontWeight,
        VisualPropertyKeys.FontStyle,
        VisualPropertyKeys.Underline,
        VisualPropertyKeys.TextWrap,
        VisualPropertyKeys.LineHeight,
        VisualPropertyKeys.TextOverflow,
        VisualPropertyKeys.HorizontalAlignment,
        VisualPropertyKeys.VerticalAlignment
    ];

    public static VisualObjectPropertySchema Group { get; } = Create(GroupType, Base);

    public static VisualObjectPropertySchema Rectangle { get; } = Create(
        RectangleType,
        Base
            .Concat([VisualPropertyKeys.FillColor])
            .Concat(Stroke)
            .Concat([VisualPropertyKeys.CornerRadius]));

    public static VisualObjectPropertySchema Ellipse { get; } = Create(
        EllipseType,
        Base
            .Concat([VisualPropertyKeys.FillColor])
            .Concat(Stroke));

    public static VisualObjectPropertySchema Line { get; } = Create(
        LineType,
        Base.Concat(Stroke));

    /// <summary>
    /// Polygon points are structural geometry owned by the core.polygon contract,
    /// not a scalar Visual Property Registry value. Only common transform/
    /// appearance properties belong to the shared property schema.
    /// </summary>
    public static VisualObjectPropertySchema Polygon { get; } = Create(
        PolygonType,
        Base
            .Concat([VisualPropertyKeys.FillColor])
            .Concat(Stroke));

    public static VisualObjectPropertySchema Text { get; } = Create(
        TextType,
        Base.Concat(TextProperties));

    public static VisualObjectPropertySchema Image { get; } = Create(
        ImageType,
        Base.Concat(
        [
            VisualPropertyKeys.AssetRef,
            VisualPropertyKeys.ImageFit,
            VisualPropertyKeys.ImagePositionX,
            VisualPropertyKeys.ImagePositionY
        ]));

    public static VisualObjectPropertySchema ValueDisplay { get; } = Create(
        ValueDisplayType,
        Base
            .Concat([VisualPropertyKeys.BackgroundColor])
            .Concat(Stroke)
            .Concat([VisualPropertyKeys.CornerRadius])
            .Concat(TextProperties));

    public static VisualObjectPropertySchema Button { get; } = Create(
        ButtonType,
        Base
            .Concat([VisualPropertyKeys.BackgroundColor])
            .Concat(Stroke)
            .Concat([VisualPropertyKeys.CornerRadius])
            .Concat(TextProperties));

    public static VisualObjectPropertySchema Slider { get; } = Create(
        SliderType,
        Base.Concat(
        [
            VisualPropertyKeys.Value,
            VisualPropertyKeys.Minimum,
            VisualPropertyKeys.Maximum,
            VisualPropertyKeys.Step,
            VisualPropertyKeys.Orientation,
            VisualPropertyKeys.InteractionEnabled,
            VisualPropertyKeys.ReverseDirection,
            VisualPropertyKeys.TrackColor,
            VisualPropertyKeys.ThumbColor,
            VisualPropertyKeys.StrokeColor,
            VisualPropertyKeys.StrokeWidth,
            VisualPropertyKeys.CornerRadius
        ]));

    public static IReadOnlyCollection<VisualObjectPropertySchema> All { get; } =
    [
        Group,
        Rectangle,
        Ellipse,
        Line,
        Polygon,
        Text,
        Image,
        ValueDisplay,
        Button,
        Slider
    ];

    /// <summary>
    /// Public object-capability declaration for FOLLOW-B Analog Fill. Eligibility
    /// is explicit and renderer-independent; renderers must not infer it from CSS,
    /// geometry implementation or the mere presence of a color property.
    /// </summary>
    public static bool SupportsAnalogFill(string objectType) =>
        !string.IsNullOrWhiteSpace(objectType) && AnalogFillCapableTypes.Contains(objectType);

    public static VisualObjectPropertySchema GetRequired(string objectType)
    {
        var schema = All.SingleOrDefault(
            candidate => candidate.ObjectTypeKey.Equals(objectType, StringComparison.Ordinal));
        return schema ?? throw new KeyNotFoundException(
            $"Built-in visual object type '{objectType}' is not registered.");
    }

    private static VisualObjectPropertySchema Create(
        string objectType,
        IEnumerable<string> propertyKeys)
    {
        var builder = new VisualPropertySchemaBuilder(objectType);
        foreach (var propertyKey in propertyKeys)
        {
            if (!CommonByKey.TryGetValue(propertyKey, out var definition))
                throw new InvalidOperationException(
                    $"Built-in object type '{objectType}' references unknown visual property '{propertyKey}'.");
            builder.Add(definition);
        }
        return builder.Build();
    }
}
