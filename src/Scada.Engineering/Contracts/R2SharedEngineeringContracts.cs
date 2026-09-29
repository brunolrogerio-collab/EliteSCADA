using System.Text.Json.Serialization;
using Scada.Core.Alarms;
using Scada.Core.HistoricalQueries;

namespace Scada.Engineering.Contracts;

public static class R2SharedEngineeringContractVersions
{
    public const int HistorianCaptureProfile = 1;
    public const int DataQuery = 1;
    public const int AlarmView = 1;
    public const int EngineeringFragment = 1;
    public const int LibraryProvenance = 1;
}

[JsonConverter(typeof(JsonStringEnumConverter<HistorianCaptureStrategy>))]
public enum HistorianCaptureStrategy
{
    [JsonStringEnumMemberName("periodic")] Periodic,
    [JsonStringEnumMemberName("onChange")] OnChange,
    [JsonStringEnumMemberName("onChangeDeadband")] OnChangeDeadband,
    [JsonStringEnumMemberName("onChangeDeadbandMaxInterval")] OnChangeDeadbandMaxInterval
}

public sealed record HistorianCaptureProfileEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    HistorianCaptureStrategy Strategy,
    int? PeriodMilliseconds = null,
    double? Deadband = null,
    int? MaximumIntervalMilliseconds = null,
    string? Description = null,
    Dictionary<string, string>? Metadata = null,
    int Version = R2SharedEngineeringContractVersions.HistorianCaptureProfile);

[JsonConverter(typeof(JsonStringEnumConverter<HistorianRetrievalMode>))]
public enum HistorianRetrievalMode
{
    [JsonStringEnumMemberName("raw")] Raw,
    [JsonStringEnumMemberName("last")] Last,
    [JsonStringEnumMemberName("atOrBefore")] AtOrBefore,
    [JsonStringEnumMemberName("atOrAfter")] AtOrAfter,
    [JsonStringEnumMemberName("exact")] Exact,
    [JsonStringEnumMemberName("interpolated")] Interpolated,
    [JsonStringEnumMemberName("sampledFixedStep")] SampledFixedStep,
    [JsonStringEnumMemberName("aggregate")] Aggregate
}

[JsonConverter(typeof(JsonStringEnumConverter<DataQueryAggregateFunction>))]
public enum DataQueryAggregateFunction
{
    [JsonStringEnumMemberName("count")] Count,
    [JsonStringEnumMemberName("sum")] Sum,
    [JsonStringEnumMemberName("average")] Average,
    [JsonStringEnumMemberName("minimum")] Minimum,
    [JsonStringEnumMemberName("maximum")] Maximum,
    [JsonStringEnumMemberName("first")] First,
    [JsonStringEnumMemberName("last")] Last
}

[JsonConverter(typeof(JsonStringEnumConverter<DataQueryParameterType>))]
public enum DataQueryParameterType
{
    [JsonStringEnumMemberName("string")] String,
    [JsonStringEnumMemberName("boolean")] Boolean,
    [JsonStringEnumMemberName("number")] Number,
    [JsonStringEnumMemberName("int64")] Int64,
    [JsonStringEnumMemberName("dateTime")] DateTime,
    [JsonStringEnumMemberName("durationSeconds")] DurationSeconds,
    [JsonStringEnumMemberName("guid")] Guid,
    [JsonStringEnumMemberName("enum")] Enum
}

[JsonConverter(typeof(JsonStringEnumConverter<DataQueryParameterTarget>))]
public enum DataQueryParameterTarget
{
    [JsonStringEnumMemberName("absoluteFromUtc")] AbsoluteFromUtc,
    [JsonStringEnumMemberName("absoluteToUtc")] AbsoluteToUtc,
    [JsonStringEnumMemberName("relativeDurationSeconds")] RelativeDurationSeconds,
    [JsonStringEnumMemberName("search")] Search,
    [JsonStringEnumMemberName("filterValue")] FilterValue
}

public sealed record DataQueryParameterValue(
    DataQueryParameterType Type,
    string Value);

public sealed record DataQueryParameterEngineeringDto(
    string Key,
    string Name,
    DataQueryParameterType Type,
    DataQueryParameterValue? DefaultValue = null,
    string? Description = null,
    IReadOnlyCollection<DataQueryParameterValue>? AllowedValues = null);

public sealed record DataQueryParameterBindingEngineeringDto(
    string ParameterKey,
    DataQueryParameterTarget Target,
    int? FilterIndex = null,
    int? ValueIndex = null);

public sealed record DataQueryGroupEngineeringDto(string Field);

public sealed record DataQueryAggregateEngineeringDto(
    string Key,
    string Field,
    DataQueryAggregateFunction Function);

public sealed record HistorianRetrievalEngineeringDto(
    HistorianRetrievalMode Mode = HistorianRetrievalMode.Raw,
    int? StepMilliseconds = null,
    int? BucketMilliseconds = null,
    int? MaximumGapMilliseconds = null,
    DataQueryAggregateFunction? AggregateFunction = null);

/// <summary>
/// Saved Engineering definition over the existing protected Historical Query v1 request.
/// Query remains the canonical typed filter/sort/time descriptor; this record only adds
/// reusable identity, projection, parameters and retrieval/presentation-independent hints.
/// </summary>
public sealed record DataQueryEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    string ProviderKey,
    HistoricalQueryRequest Query,
    IReadOnlyCollection<string>? SelectedFields = null,
    IReadOnlyCollection<DataQueryGroupEngineeringDto>? Groups = null,
    IReadOnlyCollection<DataQueryAggregateEngineeringDto>? Aggregates = null,
    IReadOnlyCollection<DataQueryParameterEngineeringDto>? Parameters = null,
    IReadOnlyCollection<DataQueryParameterBindingEngineeringDto>? ParameterBindings = null,
    HistorianRetrievalEngineeringDto? HistorianRetrieval = null,
    string? Description = null,
    Dictionary<string, string>? Metadata = null,
    int Version = R2SharedEngineeringContractVersions.DataQuery);

[JsonConverter(typeof(JsonStringEnumConverter<AlarmViewMatchState>))]
public enum AlarmViewMatchState
{
    [JsonStringEnumMemberName("any")] Any,
    [JsonStringEnumMemberName("yes")] Yes,
    [JsonStringEnumMemberName("no")] No
}

public sealed record AlarmViewFilterEngineeringDto(
    IReadOnlyCollection<string>? Areas = null,
    IReadOnlyCollection<AlarmPriority>? Priorities = null,
    IReadOnlyCollection<AlarmType>? Types = null,
    IReadOnlyCollection<string>? AlarmClasses = null,
    IReadOnlyCollection<string>? Categories = null,
    IReadOnlyCollection<string>? Subconditions = null,
    IReadOnlyCollection<string>? Sources = null,
    IReadOnlyCollection<Guid>? AlarmIds = null,
    IReadOnlyCollection<Guid>? TagIds = null,
    IReadOnlyCollection<Guid>? EquipmentIds = null,
    AlarmViewMatchState Active = AlarmViewMatchState.Any,
    AlarmViewMatchState Acknowledged = AlarmViewMatchState.Any,
    AlarmViewMatchState Shelved = AlarmViewMatchState.Any,
    string? Search = null);

public sealed record AlarmViewEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    AlarmViewFilterEngineeringDto Filter,
    string? Description = null,
    Dictionary<string, string>? Metadata = null,
    int Version = R2SharedEngineeringContractVersions.AlarmView);

public static class EngineeringFragmentContract
{
    public const string Schema = "scada.engineering.fragment";
    public const int SchemaVersion = R2SharedEngineeringContractVersions.EngineeringFragment;
    public const string FileExtension = ".escadafrag";
}

[JsonConverter(typeof(JsonStringEnumConverter<EngineeringFragmentPlanOperation>))]
public enum EngineeringFragmentPlanOperation
{
    [JsonStringEnumMemberName("create")] Create,
    [JsonStringEnumMemberName("reuseIdentical")] ReuseIdentical,
    [JsonStringEnumMemberName("update")] Update,
    [JsonStringEnumMemberName("remap")] Remap,
    [JsonStringEnumMemberName("skip")] Skip,
    [JsonStringEnumMemberName("conflict")] Conflict,
    [JsonStringEnumMemberName("unsupported")] Unsupported
}

public sealed record EngineeringFragmentEntityReference(
    ImportEntityKind EntityKind,
    string EntityKey,
    Guid? EntityId = null);

public sealed record EngineeringFragmentManifest(
    IReadOnlyCollection<EngineeringFragmentEntityReference> Roots,
    IReadOnlyCollection<EngineeringFragmentEntityReference>? Dependencies = null,
    int Version = R2SharedEngineeringContractVersions.EngineeringFragment);

public sealed record EngineeringFragmentEnvelope(
    string Schema,
    int SchemaVersion,
    DateTimeOffset ExportedAt,
    EngineeringFragmentManifest Manifest,
    EngineeringPackage Engineering);

public sealed record EngineeringFragmentPreviewItem(
    EngineeringFragmentEntityReference Source,
    EngineeringFragmentPlanOperation Operation,
    EngineeringFragmentEntityReference? Target = null,
    string? Reason = null);

[JsonConverter(typeof(JsonStringEnumConverter<ReusableLibraryUpdateState>))]
public enum ReusableLibraryUpdateState
{
    [JsonStringEnumMemberName("upToDate")] UpToDate,
    [JsonStringEnumMemberName("updateAvailable")] UpdateAvailable,
    [JsonStringEnumMemberName("locallyModified")] LocallyModified,
    [JsonStringEnumMemberName("sourceMissing")] SourceMissing,
    [JsonStringEnumMemberName("incompatible")] Incompatible
}

public sealed record ReusableLibrarySourceProvenanceEngineeringDto(
    Guid SourceLibraryId,
    Guid SourceResourceId,
    string SourceVersion,
    string SourceContentHash,
    int Version = R2SharedEngineeringContractVersions.LibraryProvenance);
