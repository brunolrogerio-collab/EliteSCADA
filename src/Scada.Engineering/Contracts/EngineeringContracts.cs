using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Tags;
using Scada.Engineering.Scripts;
using Scada.Security.Authorization;

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
    Alarm,
    DataSource,
    Template,
    Equipment,
    Dynamo,
    Screen,
    Popup,
    SecurityRole,
    Command,
    Gateway,
    Script,
    VisualAsset
}

public enum ImportOperation
{
    Create,
    Update,
    Skip,
    Error
}

public enum EngineeringBindingKind
{
    Tag,
    ClientMemory,
    Property,
    Expression
}

public enum GatewayTransferMode
{
    OnChange,
    Periodic
}

public enum GatewayQualityPolicy
{
    GoodOnly
}

public enum GatewayConversionPolicy
{
    Exact,
    CheckedNumeric
}

public enum GatewayInitialTransferPolicy
{
    WaitForNextAcceptableValue,
    SynchronizeFirstAcceptableValue
}

public sealed record HistorianSettingsDto(
    bool Enabled = false,
    string Strategy = "none",
    double? Deadband = null,
    int? PeriodMilliseconds = null,
    int? MaximumPeriodMilliseconds = null);

/// <summary>
/// Public, versioned Engineering representation for an Internal Memory TAG's
/// typed startup value. The declared type is repeated deliberately so imports
/// can reject type drift before runtime activation instead of guessing from JSON.
/// </summary>
public sealed record MemoryInitialValueDto(
    TagDataType DataType,
    JsonElement Value);

public sealed record TagAccessPolicyDto(
    IReadOnlyCollection<string>? ReadRoles = null,
    IReadOnlyCollection<string>? WriteRoles = null,
    IReadOnlyCollection<string>? ConfigureRoles = null);

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
    Dictionary<string, string>? Metadata = null,
    TagAccessPolicyDto? AccessPolicy = null,
    MemoryInitialValueDto? InitialValue = null);

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

public sealed record DataSourceEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    string Driver,
    bool Enabled = true,
    Dictionary<string, string>? Settings = null,
    Dictionary<string, string>? SecretReferences = null,
    Dictionary<string, string>? Metadata = null);

/// <summary>
/// Generic Engineering binding. In a visual-element binding, <see cref="Key"/>
/// is the destination visual-property/slot key and <see cref="Target"/> is the
/// TAG/property/expression reference. Keeping that distinction explicit avoids
/// an editor-private binding model later.
/// </summary>
public sealed record EngineeringBindingDto(
    string Key,
    EngineeringBindingKind Kind,
    string Target,
    string? Direction = null,
    Dictionary<string, string>? Metadata = null);

public sealed record EquipmentTemplateEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    IReadOnlyCollection<EngineeringBindingDto>? Bindings = null,
    Dictionary<string, string>? Properties = null,
    Dictionary<string, string>? Context = null,
    Dictionary<string, string>? Metadata = null);

public sealed record EquipmentEngineeringDto(
    Guid? Id,
    string Path,
    string Name,
    string? TemplateKey = null,
    IReadOnlyCollection<EngineeringBindingDto>? Bindings = null,
    Dictionary<string, string>? Properties = null,
    Dictionary<string, string>? Context = null,
    Dictionary<string, string>? Metadata = null);

public sealed record DynamoEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    string? TemplateKey = null,
    IReadOnlyCollection<EngineeringBindingDto>? Bindings = null,
    Dictionary<string, string>? Properties = null,
    Dictionary<string, string>? Context = null,
    Dictionary<string, string>? Metadata = null);

/// <summary>
/// Canonical Engineering node for a visual-object tree. Id is the stable object
/// identity used by Runtime/script references. It remains optional on input so
/// legacy schema-v10/v11 packages can still be parsed; view registries assign and
/// then preserve an identity when such legacy elements are first materialized.
/// Properties are JSON-native from schema v12 onward; legacy string-valued bags
/// remain readable only through the explicit schema migration boundary.
/// Key remains the developer-facing sibling-local name and is not identity.
/// </summary>
public sealed record VisualElementEngineeringDto(
    string Key,
    string Type,
    string? DynamoKey = null,
    string? EquipmentPath = null,
    IReadOnlyCollection<EngineeringBindingDto>? Bindings = null,
    Dictionary<string, JsonElement>? Properties = null,
    Dictionary<string, string>? Context = null,
    IReadOnlyCollection<VisualElementEngineeringDto>? Children = null,
    Dictionary<string, string>? Metadata = null,
    Guid? Id = null);

public sealed record ScreenEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    string? Route = null,
    IReadOnlyCollection<VisualElementEngineeringDto>? Elements = null,
    Dictionary<string, string>? Properties = null,
    Dictionary<string, string>? Context = null,
    Dictionary<string, string>? Metadata = null);

public sealed record PopupEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    string? TemplateKey = null,
    IReadOnlyCollection<VisualElementEngineeringDto>? Elements = null,
    Dictionary<string, string>? Properties = null,
    Dictionary<string, string>? Context = null,
    Dictionary<string, string>? Metadata = null);

/// <summary>
/// First-class Wave 08 project image asset metadata. Raw raster bytes are not
/// embedded in canonical Engineering JSON; Sha256 identifies the exact immutable
/// content stored in the project/revision asset blob boundary. Id is the stable
/// project reference used by visual assetRef values.
/// </summary>
public sealed record VisualAssetEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    string OriginalFileName,
    string MediaType,
    long ByteLength,
    string Sha256,
    int? PixelWidth = null,
    int? PixelHeight = null,
    string? Description = null,
    Dictionary<string, string>? Metadata = null);

public sealed record AuthorizationScopeEngineeringDto(
    string? Area = null,
    string? EquipmentPath = null,
    string? ScreenKey = null,
    string? TagPath = null,
    string? CommandKey = null);

public sealed record CapabilityGrantEngineeringDto(
    SecurityCapability Capability,
    AuthorizationScopeEngineeringDto? Scope = null,
    Dictionary<string, string>? Metadata = null);

public sealed record SecurityRoleEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    string? Description = null,
    IReadOnlyCollection<CapabilityGrantEngineeringDto>? Grants = null,
    Dictionary<string, string>? Metadata = null);

public sealed record CommandEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    CommandKind Kind,
    string Value,
    Guid? TargetTagId = null,
    string? TargetTagPath = null,
    string? Description = null,
    string? Area = null,
    string? EquipmentPath = null,
    bool Enabled = true,
    Dictionary<string, string>? Metadata = null);

/// <summary>
/// Public, versioned TAG-to-TAG gateway route. Stable TAG IDs are the runtime
/// identity while paths are retained as portable reconciliation context.
/// Mutable runtime counters/state do not belong in this Engineering contract.
/// </summary>
public sealed record GatewayRouteEngineeringDto(
    Guid? Id,
    string Key,
    string Name,
    Guid? SourceTagId = null,
    string? SourceTagPath = null,
    Guid? DestinationTagId = null,
    string? DestinationTagPath = null,
    GatewayTransferMode TransferMode = GatewayTransferMode.OnChange,
    GatewayQualityPolicy QualityPolicy = GatewayQualityPolicy.GoodOnly,
    GatewayConversionPolicy ConversionPolicy = GatewayConversionPolicy.Exact,
    GatewayInitialTransferPolicy InitialTransferPolicy = GatewayInitialTransferPolicy.SynchronizeFirstAcceptableValue,
    double? Gain = null,
    double? Offset = null,
    double? Deadband = null,
    int? MinimumIntervalMilliseconds = null,
    int? PeriodMilliseconds = null,
    string? Description = null,
    bool Enabled = true,
    Dictionary<string, string>? Metadata = null);

public sealed record EngineeringPackage(
    string Schema,
    int SchemaVersion,
    DateTimeOffset ExportedAt,
    IReadOnlyCollection<TagEngineeringDto> Tags,
    IReadOnlyCollection<AlarmEngineeringDto> Alarms,
    IReadOnlyCollection<DataSourceEngineeringDto>? DataSources = null,
    IReadOnlyCollection<EquipmentTemplateEngineeringDto>? Templates = null,
    IReadOnlyCollection<EquipmentEngineeringDto>? Equipment = null,
    IReadOnlyCollection<DynamoEngineeringDto>? Dynamos = null,
    IReadOnlyCollection<ScreenEngineeringDto>? Screens = null,
    IReadOnlyCollection<PopupEngineeringDto>? Popups = null,
    IReadOnlyCollection<SecurityRoleEngineeringDto>? SecurityRoles = null,
    IReadOnlyCollection<CommandEngineeringDto>? Commands = null,
    IReadOnlyCollection<GatewayRouteEngineeringDto>? Gateways = null,
    IReadOnlyCollection<ScriptEngineeringDefinition>? Scripts = null,
    IReadOnlyCollection<ScriptVisualEventReference>? ScriptVisualEventReferences = null,
    IReadOnlyCollection<VisualAssetEngineeringDto>? VisualAssets = null);

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