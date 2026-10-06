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
    public const string ArcType = "core.arc";
    public const string BezierType = "core.bezier";
    public const string PolygonType = "core.polygon";
    public const string TextType = "core.text";
    public const string ImageType = "core.image";
    public const string Model3dType = "core.model3d";
    public const string SvgSymbolType = "core.svgSymbol";
    public const string VideoPlayerType = "core.videoPlayer";
    public const string PdfViewerType = "core.pdfViewer";
    public const string ReportLauncherType = "core.reportLauncher";
    public const string ValueDisplayType = "core.valueDisplay";
    public const string TrendType = "core.trend";
    public const string AlarmBrowserType = "core.alarmBrowser";
    public const string EventBrowserType = "core.eventBrowser";
    public const string ButtonType = "core.button";
    public const string SliderType = "core.slider";
    public const string NumericInputType = "core.numericInput";
    public const string TrendPensProperty = "pens";
    public const string BrowserConfigProperty = "browserConfig";
    public const string SvgPaintOverridesProperty = "svgPaintOverrides";

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
            .Concat(CommonVisualPropertyDefinitions.Media)
            .Concat(CommonVisualPropertyDefinitions.ReportLauncher)
            .Concat(CommonVisualPropertyDefinitions.Slider)
            .Concat(CommonVisualPropertyDefinitions.NumericInput)
            .Concat(CommonVisualPropertyDefinitions.Arc)
            .Concat(CommonVisualPropertyDefinitions.Bezier)
            .Concat(CommonVisualPropertyDefinitions.Polygon)
            .Concat(TrendDefinitions)
            .ToDictionary(property => property.Key, StringComparer.Ordinal);

    private static readonly HashSet<string> AnalogFillCapableTypes =
        new(StringComparer.Ordinal)
        {
            RectangleType,
            EllipseType,
            BezierType
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

    private static readonly string[] BrowserProperties =
    [
        VisualPropertyKeys.BackgroundColor,
        VisualPropertyKeys.StrokeColor,
        VisualPropertyKeys.StrokeWidth,
        VisualPropertyKeys.CornerRadius
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

    public static VisualObjectPropertySchema Arc { get; } = Create(
        ArcType,
        Base.Concat(Fill).Concat(Stroke).Concat(CommonVisualPropertyDefinitions.Arc.Select(property => property.Key))
            .Concat([VisualPropertyKeys.ArcStartAngle, VisualPropertyKeys.ArcEndAngle]));

    public static VisualObjectPropertySchema Bezier { get; } = Create(
        BezierType,
        Base.Concat(Fill).Concat(Stroke).Concat(CommonVisualPropertyDefinitions.Bezier.Select(property => property.Key)));

    /// <summary>
    /// Polygon points are structural geometry owned by the core.polygon contract,
    /// not a scalar Visual Property Registry value. Only common transform/
    /// appearance properties belong to the shared property schema.
    /// </summary>
    public static VisualObjectPropertySchema Polygon { get; } = Create(
        PolygonType,
        Base
            .Concat(Fill)
            .Concat(CommonVisualPropertyDefinitions.Polygon.Select(property => property.Key))
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
            VisualPropertyKeys.ImagePositionY,
            VisualPropertyKeys.ImageZoom
        ]));

    public static VisualObjectPropertySchema Model3d { get; } = Create(
        Model3dType,
        Base.Concat([VisualPropertyKeys.AssetRef]));

    public static VisualObjectPropertySchema VideoPlayer { get; } = Create(
        VideoPlayerType,
        Base.Concat([
            VisualPropertyKeys.AssetRef, VisualPropertyKeys.ImageFit,
            VisualPropertyKeys.MediaAutoplay, VisualPropertyKeys.MediaMuted,
            VisualPropertyKeys.MediaLoop, VisualPropertyKeys.MediaControls,
            VisualPropertyKeys.MediaSourceId
        ]));

    public static VisualObjectPropertySchema PdfViewer { get; } = Create(
        PdfViewerType,
        Base.Concat([
            VisualPropertyKeys.AssetRef, VisualPropertyKeys.PdfInitialPage,
            VisualPropertyKeys.PdfZoom, VisualPropertyKeys.PdfToolbarVisible
        ]));

    public static VisualObjectPropertySchema ReportLauncher { get; } = Create(
        ReportLauncherType,
        Base
            .Concat([VisualPropertyKeys.BackgroundColor])
            .Concat(CommonVisualPropertyDefinitions.ReportLauncher.Select(property => property.Key))
            .Concat(Stroke)
            .Concat([VisualPropertyKeys.CornerRadius])
            .Concat(TextProperties));

    public static VisualObjectPropertySchema SvgSymbol { get; } = Create(
        SvgSymbolType,
        Base.Concat(
        [
            VisualPropertyKeys.AssetRef,
            VisualPropertyKeys.FillColor,
            VisualPropertyKeys.StrokeColor,
            VisualPropertyKeys.StrokeWidth
        ]));

    public static VisualObjectPropertySchema ValueDisplay { get; } = Create(
        ValueDisplayType,
        Base
            .Concat([VisualPropertyKeys.BackgroundColor])
            .Concat(Stroke)
            .Concat([VisualPropertyKeys.CornerRadius])
            .Concat(TextProperties)
            .Concat([VisualPropertyKeys.ValueFormat, VisualPropertyKeys.TextColorGood, VisualPropertyKeys.TextColorBad])
            .Concat([VisualPropertyKeys.DecimalPlacesEnabled, VisualPropertyKeys.DecimalPlaces]));

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

    /// <summary>
    /// Alarm/Event Browser configuration is structural JSON owned by each Browser
    /// object. Only common geometry/appearance properties participate in the
    /// scalar Visual Property Registry.
    /// </summary>
    public static VisualObjectPropertySchema AlarmBrowser { get; } = Create(
        AlarmBrowserType,
        Base.Concat(BrowserProperties));

    public static VisualObjectPropertySchema EventBrowser { get; } = Create(
        EventBrowserType,
        Base.Concat(BrowserProperties));

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

    public static VisualObjectPropertySchema NumericInput { get; } = Create(
        NumericInputType,
        Base
            .Concat([VisualPropertyKeys.BackgroundColor])
            .Concat(
            [
                VisualPropertyKeys.StrokeColor,
                VisualPropertyKeys.StrokeWidth,
                VisualPropertyKeys.CornerRadius,
                VisualPropertyKeys.TextColor,
                VisualPropertyKeys.TextColorEditing,
                VisualPropertyKeys.TextColorGood,
                VisualPropertyKeys.TextColorBad,
                VisualPropertyKeys.DecimalPlacesEnabled,
                VisualPropertyKeys.DecimalPlaces,
                VisualPropertyKeys.FontFamily,
                VisualPropertyKeys.FontSize,
                VisualPropertyKeys.FontWeight,
                VisualPropertyKeys.HorizontalAlignment,
                VisualPropertyKeys.Value,
                VisualPropertyKeys.Minimum,
                VisualPropertyKeys.Maximum,
                VisualPropertyKeys.Step,
                VisualPropertyKeys.InteractionEnabled,
                VisualPropertyKeys.ShowApplyButton
            ]));

    public static IReadOnlyCollection<VisualObjectPropertySchema> All { get; } =
    [
        Group,
        Rectangle,
        Ellipse,
        Line,
        Arc,
        Bezier,
        Polygon,
        Text,
        Image,
        Model3d,
        VideoPlayer,
        PdfViewer,
        ReportLauncher,
        SvgSymbol,
        ValueDisplay,
        Trend,
        AlarmBrowser,
        EventBrowser,
        Button,
        Slider,
        NumericInput
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
