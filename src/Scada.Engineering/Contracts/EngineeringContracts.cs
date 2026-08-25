using Scada.Core.Alarms;
using Scada.Core.Tags;

namespace Scada.Engineering.Contracts;

public enum ImportMode
{
    CreateOnly,
    UpdateExisting,
    CreateAndUpdate
}

public enum ImportEntityKind
{
    Tag,
    Alarm
}

public enum ImportOperation
{
    Create,
    Update,
    Skip,
    Error
}

public sealed record HistorianSettingsDto(
    bool Enabled = false,
    string Strategy = "none",
    double? Deadband = null,
    int? PeriodMilliseconds = null,
    int? MaximumPeriodMilliseconds = null);

public sealed record TagEngineeringDto(
    Guid? Id,
    string Name,
    string Path,
    TagDataType DataType,
    string? Source = null,
    string? Address = null,
    string? EngineeringUnit = null,
    string? Description = null,
    bool ReadOnly = true,
    double? ScaleMinimum = null,
    double? ScaleMaximum = null,
    HistorianSettingsDto? Historian = null,
    Dictionary<string, string>? Metadata = null);

public sealed record AlarmEngineeringDto(
    Guid? Id,
    string Name,
    Guid? TagId,
    string? TagPath,
    AlarmType Type,
    AlarmPriority Priority,
    double? Setpoint = null,
    bool DigitalActiveValue = true,
    string? AlarmClass = null,
    string? Area = null,
    string? Message = null,
    int? ActivationDelayMilliseconds = null,
    bool RequiresAcknowledgement = true,
    bool ShelvingAllowed = true,
    bool Enabled = true,
    Dictionary<string, string>? Metadata = null);

public sealed record EngineeringPackage(
    string Schema,
    int SchemaVersion,
    DateTimeOffset ExportedAt,
    IReadOnlyCollection<TagEngineeringDto> Tags,
    IReadOnlyCollection<AlarmEngineeringDto> Alarms);

public sealed record ImportIssue(
    string Code,
    string Message,
    ImportEntityKind EntityKind,
    string EntityKey,
    bool IsError);

public sealed record ImportPreviewItem(
    ImportEntityKind EntityKind,
    string EntityKey,
    ImportOperation Operation,
    IReadOnlyCollection<ImportIssue> Issues);

public sealed record ImportPreview(
    ImportMode Mode,
    int CreateCount,
    int UpdateCount,
    int SkipCount,
    int ErrorCount,
    IReadOnlyCollection<ImportPreviewItem> Items)
{
    public bool CanApply => ErrorCount == 0;
}

public sealed record ImportResult(
    ImportMode Mode,
    int Created,
    int Updated,
    int Skipped,
    IReadOnlyCollection<ImportIssue> Issues);
