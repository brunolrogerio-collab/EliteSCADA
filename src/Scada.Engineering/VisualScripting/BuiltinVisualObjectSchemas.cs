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
    public const string TrendType = "core.trend";
    public const string ButtonType = "core.button";
    public const string SliderType = "core.slider";
    public const string TrendPensProperty = "pens";

    private const string TrendModeProperty = "trendMode";
    private const string TrendWindowSecondsProperty = "trendWindowSeconds";
    private const string TrendRefreshSecondsProperty = "trendRefreshSeconds";
    private const string TrendLegendVisibleProperty = "trendLegendVisible";
    private const string TrendGridVisibleProperty = "trendGridVisible";
    private const string TrendAxesVisibleProperty = "trendAxesVisible";
    private const string TrendQualityVisibleProperty = "trendQualityVisible";

    private static readonly IReadOnlyList<VisualPropertyDefinition> TrendDefinitions =
    [
        new(
            TrendModeProperty,
            new VisualStringValue("history"),
            constraints: new VisualPropertyConstraints
            {
                AllowedValues = ["history", "live"],
                AllowEmptyString = false
            }),
        new(
            TrendWindowSecondsProperty,
            new VisualIntegerValue(3600),
            constraints: new VisualPropertyConstraints { Minimum = 60, Maximum = 604800 },
            unit: "s"),
        new(
            TrendRefreshSecondsProperty,
            new VisualIntegerValue(5),
            constraints: new VisualPropertyConstraints { Minimum = 1, Maximum = 3600 },
            unit: "s"),
        new(TrendLegendVisibleProperty, new VisualBooleanValue(true)),
        new(TrendGridVisibleProperty, new VisualBooleanValue(true)),
        new(TrendAxesVisibleProperty, new VisualBooleanValue(true)),
        new(TrendQualityVisibleProperty, new VisualBooleanValue(true))
    ];

    private static readonly IReadOnlyDictionary<string, VisualPropertyDefinition> CommonByKey =
        CommonVisualPropertyDefinitions.Geometry
            .Concat(CommonVisualPropertyDefinitions.Transform)
            .Concat(CommonVisualPropertyDefinitions.Visibility)
            .Concat(CommonVisualPropertyDefinitions.Fill)
            .Concat(CommonVisualPropertyDefinitions.Stroke)
            .Concat(CommonVisualPropertyDefinitions.Effects)
            .Concat(CommonVisualPropertyDefinitions.Text)
            .Concat(CommonVisualPropertyDefinitions.Image)
            .Concat(CommonVisualPropertyDefinitions.Slider)
            .Concat(TrendDefinitions)
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
        VisualPropertyKeys.Tooltip,
        VisualPropertyKeys.Enabled,
        VisualPropertyKeys.ShadowEnabled,
        VisualPropertyKeys.ShadowColor,
        VisualPropertyKeys.ShadowOffsetX,
        VisualPropertyKeys.ShadowOffsetY,
        VisualPropertyKeys.ShadowBlur
    ];

    private static readonly string[] Fill =
    [
        VisualPropertyKeys.FillStyle,
        VisualPropertyKeys.FillColor,
        VisualPropertyKeys.FillSecondaryColor,
        VisualPropertyKeys.GradientDirection
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
            .Concat(Fill)
            .Concat(Stroke)
            .Concat([VisualPropertyKeys.CornerRadius]));

    public static VisualObjectPropertySchema Ellipse { get; } = Create(
        EllipseType,
        Base
            .Concat(Fill)
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
            .Concat(Fill)
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

    /// <summary>
    /// Trend pens are structural payload owned by core.trend and are deliberately
    /// excluded from the scalar Visual Property Registry. The scalar contract is
    /// kept in lockstep with the browser canonical registry.
    /// </summary>
    public static VisualObjectPropertySchema Trend { get; } = Create(
        TrendType,
        Base.Concat(
        [
            VisualPropertyKeys.BackgroundColor,
            VisualPropertyKeys.StrokeColor,
            VisualPropertyKeys.StrokeWidth,
            VisualPropertyKeys.CornerRadius,
            TrendModeProperty,
            TrendWindowSecondsProperty,
            TrendRefreshSecondsProperty,
            TrendLegendVisibleProperty,
            TrendGridVisibleProperty,
            TrendAxesVisibleProperty,
            TrendQualityVisibleProperty
        ]));

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
        Trend,
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
