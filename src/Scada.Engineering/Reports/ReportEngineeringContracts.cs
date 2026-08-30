using System.Globalization;
using Scada.Core.HistoricalQueries;

namespace Scada.Engineering.Reports;

public enum ReportPageOrientation
{
    Portrait,
    Landscape
}

public enum ReportSectionKind
{
    ReportHeader,
    ReportFooter,
    PageHeader,
    PageFooter,
    GroupHeader,
    Detail,
    GroupFooter
}

public enum ReportControlKind
{
    Label,
    DataField,
    BooleanState,
    Image,
    Barcode,
    Chart,
    Line,
    Rectangle,
    RoundedRectangle,
    Ellipse,
    PageBreak
}

public enum ReportTextAlignment
{
    Left,
    Center,
    Right
}

public enum ReportParameterType
{
    String,
    Boolean,
    Number,
    Int64,
    DateTime,
    DurationSeconds,
    Guid,
    Enum
}

public enum ReportQueryParameterTarget
{
    AbsoluteFromUtc,
    AbsoluteToUtc,
    RelativeDurationSeconds,
    Search,
    FilterValue
}

public enum ReportAggregateFunction
{
    Count,
    Sum,
    Average,
    Minimum,
    Maximum,
    First,
    Last
}

/// <summary>
/// Canonical typed report parameter value. Values are persisted as invariant text so
/// Int64 never crosses a JSON/JavaScript number boundary and DateTime keeps explicit UTC.
/// </summary>
public sealed record ReportParameterValue(
    ReportParameterType Type,
    string Value)
{
    public static ReportParameterValue FromString(string value) => new(ReportParameterType.String, value);
    public static ReportParameterValue FromBoolean(bool value) =>
        new(ReportParameterType.Boolean, value ? "true" : "false");

    public static ReportParameterValue FromNumber(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Report numeric values must be finite.");
        return new(ReportParameterType.Number, value.ToString("R", CultureInfo.InvariantCulture));
    }

    public static ReportParameterValue FromInt64(long value) =>
        new(ReportParameterType.Int64, value.ToString(CultureInfo.InvariantCulture));

    public static ReportParameterValue FromDateTime(DateTimeOffset value) =>
        new(ReportParameterType.DateTime, value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    public static ReportParameterValue FromDurationSeconds(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Report duration must be positive.");
        return new(ReportParameterType.DurationSeconds, value.ToString(CultureInfo.InvariantCulture));
    }

    public static ReportParameterValue FromGuid(Guid value) =>
        new(ReportParameterType.Guid, value.ToString("D"));

    public static ReportParameterValue FromEnum(string value) =>
        new(ReportParameterType.Enum, value);

    public DateTimeOffset AsDateTimeUtc()
    {
        var parsed = DateTimeOffset.Parse(Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (parsed.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Report DateTime parameter must use UTC offset +00:00.");
        return parsed;
    }

    public int AsDurationSeconds() => int.Parse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    public long AsInt64() => long.Parse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    public double AsNumber() => double.Parse(Value, NumberStyles.Float, CultureInfo.InvariantCulture);
    public bool AsBoolean() => bool.Parse(Value);
    public Guid AsGuid() => Guid.Parse(Value);
}

public sealed record ReportParameterEngineeringDto(
    string Key,
    string Name,
    ReportParameterType Type,
    ReportParameterValue DefaultValue,
    string? Description = null,
    IReadOnlyCollection<ReportParameterValue>? AllowedValues = null);

/// <summary>
/// Binds a typed report parameter into one concrete slot of the canonical Historical
/// Query request. This is deliberately not another query language: the field/operator/
/// dataset semantics remain entirely owned by Historical Query v1.
/// </summary>
public sealed record ReportQueryParameterBindingEngineeringDto(
    string ParameterKey,
    ReportQueryParameterTarget Target,
    int? FilterIndex = null,
    int? ValueIndex = null);

public sealed record ReportQueryEngineeringDto(
    string Key,
    HistoricalQueryRequest Query,
    IReadOnlyCollection<ReportQueryParameterBindingEngineeringDto>? ParameterBindings = null);

public sealed record ReportPageEngineeringDto(
    string PaperSizeKey = "A4",
    ReportPageOrientation Orientation = ReportPageOrientation.Portrait,
    double MarginTopMillimeters = 10,
    double MarginRightMillimeters = 10,
    double MarginBottomMillimeters = 10,
    double MarginLeftMillimeters = 10,
    bool ShowPageNumbers = true);

public sealed record ReportControlStyleEngineeringDto(
    string? FontFamily = null,
    double? FontSizePoints = null,
    bool Bold = false,
    bool Italic = false,
    ReportTextAlignment TextAlignment = ReportTextAlignment.Left,
    string? Foreground = null,
    string? Background = null,
    double? BorderWidth = null);

/// <summary>
/// Renderer-independent report control geometry. Coordinates and dimensions are
/// millimeters in the report layout coordinate system, never browser pixels/CSS.
/// </summary>
public sealed record ReportControlEngineeringDto(
    Guid? Id,
    string Key,
    ReportControlKind Kind,
    double XMillimeters,
    double YMillimeters,
    double WidthMillimeters,
    double HeightMillimeters,
    string? Text = null,
    string? QueryKey = null,
    string? Field = null,
    Guid? AssetId = null,
    ReportControlStyleEngineeringDto? Style = null,
    Dictionary<string, string>? Metadata = null);

public sealed record ReportSectionEngineeringDto(
    Guid? Id,
    string Key,
    ReportSectionKind Kind,
    double HeightMillimeters,
    string? QueryKey = null,
    string? GroupKey = null,
    bool RepeatOnNewPage = false,
    IReadOnlyCollection<ReportControlEngineeringDto>? Controls = null);

public sealed record ReportGroupEngineeringDto(
    string Key,
    string QueryKey,
    string Field,
    HistoricalSortDirection Direction = HistoricalSortDirection.Ascending);

public sealed record ReportAggregateEngineeringDto(
    string Key,
    string QueryKey,
    ReportAggregateFunction Function,
    string? Field = null,
    string? GroupKey = null);

/// <summary>
/// First-class canonical Report Engineering entity. Generated PDF/XLSX/print output
/// is derived runtime output and is intentionally absent from this model.
/// </summary>
public sealed record ReportEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    string? Description = null,
    string? Category = null,
    ReportPageEngineeringDto? Page = null,
    IReadOnlyCollection<ReportParameterEngineeringDto>? Parameters = null,
    IReadOnlyCollection<ReportQueryEngineeringDto>? Queries = null,
    IReadOnlyCollection<ReportSectionEngineeringDto>? Sections = null,
    IReadOnlyCollection<ReportGroupEngineeringDto>? Groups = null,
    IReadOnlyCollection<ReportAggregateEngineeringDto>? Aggregates = null,
    Dictionary<string, string>? Metadata = null);
