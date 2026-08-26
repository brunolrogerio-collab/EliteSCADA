using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Validation;
using Scada.Engineering.Views;

namespace Scada.Engineering.ImportExport;

public sealed class EngineeringExchangeService : IEngineeringExchangeService
{
    public const string CurrentSchema = "scada.engineering";
    public const int CurrentSchemaVersion = 5;

    private readonly ITagRegistry _tags;
    private readonly IAlarmEngine _alarms;
    private readonly IDataSourceEngineeringRegistry _dataSources;
    private readonly IEngineeringAssetRegistry _assets;
    private readonly IEngineeringViewRegistry _views;
    private readonly JsonSerializerOptions _json;

    public EngineeringExchangeService(ITagRegistry tags, IAlarmEngine alarms)
        : this(tags, alarms, new InMemoryDataSourceEngineeringRegistry(), new InMemoryEngineeringAssetRegistry(), new InMemoryEngineeringViewRegistry())
    {
    }

    public EngineeringExchangeService(ITagRegistry tags, IAlarmEngine alarms, IDataSourceEngineeringRegistry dataSources)
        : this(tags, alarms, dataSources, new InMemoryEngineeringAssetRegistry(), new InMemoryEngineeringViewRegistry())
    {
    }

    public EngineeringExchangeService(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IDataSourceEngineeringRegistry dataSources,
        IEngineeringAssetRegistry assets)
        : this(tags, alarms, dataSources, assets, new InMemoryEngineeringViewRegistry())
    {
    }

    public EngineeringExchangeService(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IDataSourceEngineeringRegistry dataSources,
        IEngineeringAssetRegistry assets,
        IEngineeringViewRegistry views)
    {
        _tags = tags;
        _alarms = alarms;
        _dataSources = dataSources;
        _assets = assets;
        _views = views;
        _json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    public EngineeringPackage ExportPackage()
    {
        var tagDtos = _tags.Snapshot().Select(ToDto).ToArray();
        var paths = _tags.Snapshot().ToDictionary(x => x.Id, x => x.Path);
        var alarmDtos = _alarms.Definitions().Select(x => ToDto(x, paths.GetValueOrDefault(x.TagId))).ToArray();
        return new EngineeringPackage(
            CurrentSchema,
            CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            tagDtos,
            alarmDtos,
            _dataSources.Snapshot(),
            _assets.SnapshotTemplates(),
            _assets.SnapshotEquipment(),
            _assets.SnapshotDynamos(),
            _views.SnapshotScreens(),
            _views.SnapshotPopups());
    }

    public string ExportJson(bool indented = true)
    {
        var options = new JsonSerializerOptions(_json) { WriteIndented = indented };
        return JsonSerializer.Serialize(ExportPackage(), options);
    }

    public string ExportTagsCsv()
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            new[]
            {
                "Id", "Path", "Name", "DataType", "Unit", "Source", "Address", "ReadOnly",
                "ScaleMinimum", "ScaleMaximum", "HistorianEnabled", "HistorianStrategy", "Deadband",
                "PeriodMilliseconds", "MaximumPeriodMilliseconds", "Description", "MetadataJson",
                "ReadRolesJson", "WriteRolesJson", "ConfigureRolesJson"
            }
        };
        foreach (var t in ExportPackage().Tags)
        {
            rows.Add(new[]
            {
                t.Id?.ToString(), t.Path, t.Name, t.DataType.ToString(), t.EngineeringUnit, t.Source, t.Address,
                t.ReadOnly.ToString(), CsvCodec.Number(t.ScaleMinimum), CsvCodec.Number(t.ScaleMaximum),
                (t.Historian?.Enabled ?? false).ToString(), t.Historian?.Strategy, CsvCodec.Number(t.Historian?.Deadband),
                t.Historian?.PeriodMilliseconds?.ToString(CultureInfo.InvariantCulture),
                t.Historian?.MaximumPeriodMilliseconds?.ToString(CultureInfo.InvariantCulture), t.Description,
                JsonMap(t.Metadata), JsonList(t.AccessPolicy?.ReadRoles), JsonList(t.AccessPolicy?.WriteRoles),
                JsonList(t.AccessPolicy?.ConfigureRoles)
            });
        }
        return CsvCodec.Write(rows);
    }

    public string ExportAlarmsCsv()
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            new[]
            {
                "Id", "Name", "TagId", "TagPath", "Type", "Priority", "Setpoint", "DigitalActiveValue",
                "Class", "Area", "Message", "ActivationDelayMilliseconds", "RequiresAcknowledgement",
                "ShelvingAllowed", "Enabled", "MetadataJson"
            }
        };
        foreach (var a in ExportPackage().Alarms)
        {
            rows.Add(new[]
            {
                a.Id?.ToString(), a.Name, a.TagId?.ToString(), a.TagPath, a.Type.ToString(), a.Priority.ToString(),
                CsvCodec.Number(a.Setpoint), a.DigitalActiveValue.ToString(), a.AlarmClass, a.Area, a.Message,
                a.ActivationDelayMilliseconds?.ToString(CultureInfo.InvariantCulture), a.RequiresAcknowledgement.ToString(),
                a.ShelvingAllowed.ToString(), a.Enabled.ToString(), JsonMap(a.Metadata)
            });
        }
        return CsvCodec.Write(rows);
    }

    public string ExportDataSourcesCsv()
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "Id", "Key", "Name", "Driver", "Enabled", "SettingsJson", "SecretReferencesJson", "MetadataJson" }
        };
        foreach (var dataSource in ExportPackage().DataSources ?? Array.Empty<DataSourceEngineeringDto>())
        {
            rows.Add(new[]
            {
                dataSource.Id?.ToString(), dataSource.Key, dataSource.Name, dataSource.Driver, dataSource.Enabled.ToString(),
                JsonMap(dataSource.Settings), JsonMap(dataSource.SecretReferences), JsonMap(dataSource.Metadata)
            });
        }
        return CsvCodec.Write(rows);
    }

    public EngineeringPackage ParseJson(string json)
    {
        var package = JsonSerializer.Deserialize<EngineeringPackage>(json, _json) ?? throw new InvalidDataException("Invalid engineering package.");
        if (!string.Equals(package.Schema, CurrentSchema, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsupported schema '{package.Schema}'.");
        if (package.SchemaVersion < 1)
            throw new InvalidDataException($"Schema version {package.SchemaVersion} is invalid.");
        if (package.SchemaVersion > CurrentSchemaVersion)
            throw new InvalidDataException($"Schema version {package.SchemaVersion} is newer than supported version {CurrentSchemaVersion}.");

        return package with
        {
            DataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>(),
            Templates = package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>(),
            Equipment = package.Equipment ?? Array.Empty<EquipmentEngineeringDto>(),
            Dynamos = package.Dynamos ?? Array.Empty<DynamoEngineeringDto>(),
            Screens = package.Screens ?? Array.Empty<ScreenEngineeringDto>(),
            Popups = package.Popups ?? Array.Empty<PopupEngineeringDto>()
        };
    }

    public EngineeringPackage ParseTagsCsv(string csv)
    {
        var rows = CsvCodec.Read(csv);
        if (rows.Count == 0) return Empty();
        var h = Header(rows[0]);
        var tags = rows.Skip(1).Select(r => ParseTagCsvRow(r, h)).ToArray();
        return Empty() with { Tags = tags };
    }

    public EngineeringPackage ParseAlarmsCsv(string csv)
    {
        var rows = CsvCodec.Read(csv);
        if (rows.Count == 0) return Empty();
        var h = Header(rows[0]);
        var alarms = rows.Skip(1).Select(r => new AlarmEngineeringDto(
            GuidOrNull(Get(r,h,"Id")), Get(r,h,"Name"), GuidOrNull(Get(r,h,"TagId")), Null(Get(r,h,"TagPath")),
            Enum.Parse<AlarmType>(Get(r,h,"Type"), true), Enum.Parse<AlarmPriority>(Get(r,h,"Priority"), true),
            DoubleOrNull(Get(r,h,"Setpoint")), Bool(Get(r,h,"DigitalActiveValue"), true), Null(Get(r,h,"Class")),
            Null(Get(r,h,"Area")), Null(Get(r,h,"Message")), IntOrNull(Get(r,h,"ActivationDelayMilliseconds")),
            Bool(Get(r,h,"RequiresAcknowledgement"), true), Bool(Get(r,h,"ShelvingAllowed"), true),
            Bool(Get(r,h,"Enabled"), true), ParseMap(Get(r,h,"MetadataJson")))).ToArray();
        return Empty() with { Alarms = alarms };
    }

    public EngineeringPackage ParseDataSourcesCsv(string csv)
    {
        var rows = CsvCodec.Read(csv);
        if (rows.Count == 0) return Empty();
        var h = Header(rows[0]);
        var dataSources = rows.Skip(1).Select(r => new DataSourceEngineeringDto(
            GuidOrNull(Get(r, h, "Id")), Get(r, h, "Key"), Get(r, h, "Name"), Get(r, h, "Driver"),
            Bool(Get(r, h, "Enabled"), true), ParseMap(Get(r, h, "SettingsJson")),
            ParseMap(Get(r, h, "SecretReferencesJson")), ParseMap(Get(r, h, "MetadataJson")))).ToArray();
        return Empty() with { DataSources = dataSources };
    }

    public ImportPreview Preview(EngineeringPackage package, ImportMode mode)
    {
        var items = new List<ImportPreviewItem>();
        var dataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>();
        var templates = package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>();
        var equipment = package.Equipment ?? Array.Empty<EquipmentEngineeringDto>();
        var dynamos = package.Dynamos ?? Array.Empty<DynamoEngineeringDto>();
        var screens = package.Screens ?? Array.Empty<ScreenEngineeringDto>();
        var popups = package.Popups ?? Array.Empty<PopupEngineeringDto>();

        var duplicateDataSourceKeys = Duplicates(dataSources.Select(x => x.Key));
        foreach (var dto in dataSources)
        {
            var issues = EngineeringValidator.ValidateDataSource(dto).ToList();
            if (duplicateDataSourceKeys.Contains(dto.Key))
                issues.Add(new("DATASOURCE_DUPLICATE_IN_FILE", $"Data source key '{dto.Key}' appears more than once in the import package.", ImportEntityKind.DataSource, dto.Key, true));
            AddPreview(items, ImportEntityKind.DataSource, dto.Key, ResolveExistingDataSource(dto) is not null, mode, issues);
        }

        var duplicatePaths = Duplicates(package.Tags.Select(x => x.Path));
        foreach (var dto in package.Tags)
        {
            var issues = EngineeringValidator.ValidateTag(dto).ToList();
            ValidateTagAccessPolicy(dto, issues);
            if (duplicatePaths.Contains(dto.Path))
                issues.Add(new("TAG_DUPLICATE_IN_FILE", $"Tag path '{dto.Path}' appears more than once in the import file.", ImportEntityKind.Tag, dto.Path, true));
            if (package.SchemaVersion >= 2 && !string.IsNullOrWhiteSpace(dto.Source) &&
                _dataSources.FindByKey(dto.Source) is null &&
                !dataSources.Any(x => x.Key.Equals(dto.Source, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new("TAG_DATASOURCE_NOT_FOUND", $"Data source '{dto.Source}' referenced by tag '{dto.Path}' was not found.", ImportEntityKind.Tag, dto.Path, true));
            }
            AddPreview(items, ImportEntityKind.Tag, dto.Path, ResolveExistingTag(dto) is not null, mode, issues);
        }

        foreach (var dto in package.Alarms)
        {
            var issues = EngineeringValidator.ValidateAlarm(dto).ToList();
            if (ResolveAlarmTagForPreview(dto, package) is null)
                issues.Add(new("ALARM_TAG_NOT_FOUND", $"Referenced tag for alarm '{dto.Name}' was not found in current registry or package.", ImportEntityKind.Alarm, dto.Name, true));
            AddPreview(items, ImportEntityKind.Alarm, dto.Name, ResolveExistingAlarm(dto) is not null, mode, issues);
        }

        var duplicateTemplateKeys = Duplicates(templates.Select(x => x.Key));
        foreach (var dto in templates)
        {
            var issues = EngineeringValidator.ValidateTemplate(dto).ToList();
            if (duplicateTemplateKeys.Contains(dto.Key))
                issues.Add(new("TEMPLATE_DUPLICATE_IN_FILE", $"Template key '{dto.Key}' appears more than once in the import package.", ImportEntityKind.Template, dto.Key, true));
            ValidateConcreteTagBindings(dto.Bindings, ImportEntityKind.Template, dto.Key, package, issues);
            AddPreview(items, ImportEntityKind.Template, dto.Key, ResolveExistingTemplate(dto) is not null, mode, issues);
        }

        var duplicateEquipmentPaths = Duplicates(equipment.Select(x => x.Path));
        foreach (var dto in equipment)
        {
            var issues = EngineeringValidator.ValidateEquipment(dto).ToList();
            if (duplicateEquipmentPaths.Contains(dto.Path))
                issues.Add(new("EQUIPMENT_DUPLICATE_IN_FILE", $"Equipment path '{dto.Path}' appears more than once in the import package.", ImportEntityKind.Equipment, dto.Path, true));
            if (!string.IsNullOrWhiteSpace(dto.TemplateKey) && !TemplateExists(dto.TemplateKey, package))
                issues.Add(new("EQUIPMENT_TEMPLATE_NOT_FOUND", $"Template '{dto.TemplateKey}' referenced by equipment '{dto.Path}' was not found.", ImportEntityKind.Equipment, dto.Path, true));
            ValidateConcreteTagBindings(dto.Bindings, ImportEntityKind.Equipment, dto.Path, package, issues);
            AddPreview(items, ImportEntityKind.Equipment, dto.Path, ResolveExistingEquipment(dto) is not null, mode, issues);
        }

        var duplicateDynamoKeys = Duplicates(dynamos.Select(x => x.Key));
        foreach (var dto in dynamos)
        {
            var issues = EngineeringValidator.ValidateDynamo(dto).ToList();
            if (duplicateDynamoKeys.Contains(dto.Key))
                issues.Add(new("DYNAMO_DUPLICATE_IN_FILE", $"Dynamo key '{dto.Key}' appears more than once in the import package.", ImportEntityKind.Dynamo, dto.Key, true));
            if (!string.IsNullOrWhiteSpace(dto.TemplateKey) && !TemplateExists(dto.TemplateKey, package))
                issues.Add(new("DYNAMO_TEMPLATE_NOT_FOUND", $"Template '{dto.TemplateKey}' referenced by dynamo '{dto.Key}' was not found.", ImportEntityKind.Dynamo, dto.Key, true));
            ValidateConcreteTagBindings(dto.Bindings, ImportEntityKind.Dynamo, dto.Key, package, issues);
            AddPreview(items, ImportEntityKind.Dynamo, dto.Key, ResolveExistingDynamo(dto) is not null, mode, issues);
        }

        var duplicateScreenKeys = Duplicates(screens.Select(x => x.Key));
        var duplicateRoutes = Duplicates(screens.Select(x => x.Route ?? string.Empty));
        foreach (var dto in screens)
        {
            var issues = EngineeringValidator.ValidateScreen(dto).ToList();
            if (duplicateScreenKeys.Contains(dto.Key))
                issues.Add(new("SCREEN_DUPLICATE_IN_FILE", $"Screen key '{dto.Key}' appears more than once in the import package.", ImportEntityKind.Screen, dto.Key, true));
            if (!string.IsNullOrWhiteSpace(dto.Route) && duplicateRoutes.Contains(dto.Route))
                issues.Add(new("SCREEN_ROUTE_DUPLICATE", $"Screen route '{dto.Route}' appears more than once in the import package.", ImportEntityKind.Screen, dto.Key, true));
            ValidateVisualReferences(dto.Elements, ImportEntityKind.Screen, dto.Key, package, issues);
            AddPreview(items, ImportEntityKind.Screen, dto.Key, ResolveExistingScreen(dto) is not null, mode, issues);
        }

        var duplicatePopupKeys = Duplicates(popups.Select(x => x.Key));
        foreach (var dto in popups)
        {
            var issues = EngineeringValidator.ValidatePopup(dto).ToList();
            if (duplicatePopupKeys.Contains(dto.Key))
                issues.Add(new("POPUP_DUPLICATE_IN_FILE", $"Popup key '{dto.Key}' appears more than once in the import package.", ImportEntityKind.Popup, dto.Key, true));
            if (!string.IsNullOrWhiteSpace(dto.TemplateKey) && !TemplateExists(dto.TemplateKey, package))
                issues.Add(new("POPUP_TEMPLATE_NOT_FOUND", $"Template '{dto.TemplateKey}' referenced by popup '{dto.Key}' was not found.", ImportEntityKind.Popup, dto.Key, true));
            ValidateVisualReferences(dto.Elements, ImportEntityKind.Popup, dto.Key, package, issues);
            AddPreview(items, ImportEntityKind.Popup, dto.Key, ResolveExistingPopup(dto) is not null, mode, issues);
        }

        return new(mode,
            items.Count(x => x.Operation == ImportOperation.Create),
            items.Count(x => x.Operation == ImportOperation.Update),
            items.Count(x => x.Operation == ImportOperation.Skip),
            items.Count(x => x.Operation == ImportOperation.Error),
            items);
    }

    public ImportResult Apply(EngineeringPackage package, ImportMode mode)
    {
        var preview = Preview(package, mode);
        if (!preview.CanApply)
            return new(mode, 0, 0, preview.SkipCount, preview.Items.SelectMany(x => x.Issues).ToArray());

        var created = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var dto in package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
            ApplyDataSource(dto, ResolveExistingDataSource(dto), mode, ref created, ref updated, ref skipped);

        foreach (var dto in package.Tags)
        {
            var existing = ResolveExistingTag(dto);
            var op = Decide(existing is not null, mode);
            if (op == ImportOperation.Skip) { skipped++; continue; }
            var id = existing?.Id ?? dto.Id ?? Guid.NewGuid();
            var tag = new TagDefinition(
                id, dto.Name, dto.Path, dto.DataType, dto.Source, dto.EngineeringUnit, dto.Description, dto.ReadOnly,
                BuildTagMetadata(dto), BuildTagAccessPolicy(dto.AccessPolicy));
            if (existing is null) { _tags.Register(tag); created++; } else { _tags.Upsert(tag); updated++; }
        }

        foreach (var dto in package.Alarms)
        {
            var existing = ResolveExistingAlarm(dto);
            var op = Decide(existing is not null, mode);
            if (op == ImportOperation.Skip) { skipped++; continue; }
            var tag = ResolveAlarmTag(dto)!;
            var definition = new AlarmDefinition(existing?.Id ?? dto.Id ?? Guid.NewGuid(), dto.Name, tag.Id, dto.Type, dto.Priority,
                dto.Setpoint, dto.DigitalActiveValue, dto.Area, dto.Message, dto.Enabled, dto.AlarmClass,
                dto.ActivationDelayMilliseconds.HasValue ? TimeSpan.FromMilliseconds(dto.ActivationDelayMilliseconds.Value) : null,
                dto.RequiresAcknowledgement, dto.ShelvingAllowed, dto.Metadata);
            _alarms.Register(definition);
            if (existing is null) created++; else updated++;
        }

        foreach (var dto in package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>())
        {
            var existing = ResolveExistingTemplate(dto);
            var op = Decide(existing is not null, mode);
            if (op == ImportOperation.Skip) { skipped++; continue; }
            _assets.UpsertTemplate(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }

        foreach (var dto in package.Equipment ?? Array.Empty<EquipmentEngineeringDto>())
        {
            var existing = ResolveExistingEquipment(dto);
            var op = Decide(existing is not null, mode);
            if (op == ImportOperation.Skip) { skipped++; continue; }
            _assets.UpsertEquipment(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }

        foreach (var dto in package.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
        {
            var existing = ResolveExistingDynamo(dto);
            var op = Decide(existing is not null, mode);
            if (op == ImportOperation.Skip) { skipped++; continue; }
            _assets.UpsertDynamo(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }

        foreach (var dto in package.Screens ?? Array.Empty<ScreenEngineeringDto>())
        {
            var existing = ResolveExistingScreen(dto);
            var op = Decide(existing is not null, mode);
            if (op == ImportOperation.Skip) { skipped++; continue; }
            _views.UpsertScreen(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }

        foreach (var dto in package.Popups ?? Array.Empty<PopupEngineeringDto>())
        {
            var existing = ResolveExistingPopup(dto);
            var op = Decide(existing is not null, mode);
            if (op == ImportOperation.Skip) { skipped++; continue; }
            _views.UpsertPopup(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }

        return new(mode, created, updated, skipped, Array.Empty<ImportIssue>());
    }

    private void ApplyDataSource(
        DataSourceEngineeringDto dto,
        DataSourceEngineeringDto? existing,
        ImportMode mode,
        ref int created,
        ref int updated,
        ref int skipped)
    {
        var op = Decide(existing is not null, mode);
        if (op == ImportOperation.Skip) { skipped++; return; }
        _dataSources.Upsert(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
        if (existing is null) created++; else updated++;
    }

    private static void AddPreview(
        List<ImportPreviewItem> items,
        ImportEntityKind kind,
        string key,
        bool exists,
        ImportMode mode,
        IReadOnlyCollection<ImportIssue> issues)
    {
        var operation = Decide(exists, mode);
        if (issues.Any(x => x.IsError)) operation = ImportOperation.Error;
        items.Add(new(kind, key, operation, issues));
    }

    private static HashSet<string> Duplicates(IEnumerable<string> keys) =>
        keys.Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void ValidateTagAccessPolicy(TagEngineeringDto dto, List<ImportIssue> issues)
    {
        if (dto.AccessPolicy is null) return;
        ValidateRoleList(dto.AccessPolicy.ReadRoles, "read", dto.Path, issues);
        ValidateRoleList(dto.AccessPolicy.WriteRoles, "write", dto.Path, issues);
        ValidateRoleList(dto.AccessPolicy.ConfigureRoles, "configure", dto.Path, issues);
    }

    private static void ValidateRoleList(
        IReadOnlyCollection<string>? roles,
        string operation,
        string tagPath,
        List<ImportIssue> issues)
    {
        if (roles is null) return;
        if (roles.Any(string.IsNullOrWhiteSpace))
            issues.Add(new("TAG_ACCESS_ROLE_INVALID", $"TAG '{tagPath}' has a blank role in its {operation} access policy.", ImportEntityKind.Tag, tagPath, true));
        if (roles.Where(x => !string.IsNullOrWhiteSpace(x)).GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            issues.Add(new("TAG_ACCESS_ROLE_DUPLICATE", $"TAG '{tagPath}' repeats a role in its {operation} access policy.", ImportEntityKind.Tag, tagPath, true));
    }

    private void ValidateVisualReferences(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            ValidateConcreteTagBindings(element.Bindings, kind, entityKey, package, issues);

            if (!string.IsNullOrWhiteSpace(element.DynamoKey) && !DynamoExists(element.DynamoKey, package))
                issues.Add(new("VISUAL_DYNAMO_NOT_FOUND", $"Dynamo '{element.DynamoKey}' referenced by visual element '{element.Key}' was not found.", kind, entityKey, true));

            if (!string.IsNullOrWhiteSpace(element.EquipmentPath) && !ContainsPlaceholder(element.EquipmentPath) && !EquipmentExists(element.EquipmentPath, package))
                issues.Add(new("VISUAL_EQUIPMENT_NOT_FOUND", $"Equipment '{element.EquipmentPath}' referenced by visual element '{element.Key}' was not found.", kind, entityKey, true));

            ValidateVisualReferences(element.Children, kind, entityKey, package, issues);
        }
    }

    private void ValidateConcreteTagBindings(
        IReadOnlyCollection<EngineeringBindingDto>? bindings,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        foreach (var binding in bindings ?? Array.Empty<EngineeringBindingDto>())
        {
            if (binding.Kind != EngineeringBindingKind.Tag || string.IsNullOrWhiteSpace(binding.Target) || ContainsPlaceholder(binding.Target))
                continue;
            if (!TagPathExists(binding.Target, package))
                issues.Add(new("BINDING_TAG_NOT_FOUND", $"TAG '{binding.Target}' referenced by binding '{binding.Key}' was not found.", kind, entityKey, true));
        }
    }

    private bool TagPathExists(string path, EngineeringPackage package) =>
        _tags.TryGetByPath(path, out _) || package.Tags.Any(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

    private bool TemplateExists(string key, EngineeringPackage package) =>
        _assets.FindTemplateByKey(key) is not null ||
        (package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>()).Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    private bool EquipmentExists(string path, EngineeringPackage package) =>
        _assets.FindEquipmentByPath(path) is not null ||
        (package.Equipment ?? Array.Empty<EquipmentEngineeringDto>()).Any(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

    private bool DynamoExists(string key, EngineeringPackage package) =>
        _assets.FindDynamoByKey(key) is not null ||
        (package.Dynamos ?? Array.Empty<DynamoEngineeringDto>()).Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsPlaceholder(string value) => value.Contains('{', StringComparison.Ordinal) || value.Contains('}', StringComparison.Ordinal);

    private DataSourceEngineeringDto? ResolveExistingDataSource(DataSourceEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _dataSources.Find(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _dataSources.FindByKey(dto.Key);
    }

    private EquipmentTemplateEngineeringDto? ResolveExistingTemplate(EquipmentTemplateEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _assets.FindTemplate(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _assets.FindTemplateByKey(dto.Key);
    }

    private EquipmentEngineeringDto? ResolveExistingEquipment(EquipmentEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _assets.FindEquipment(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _assets.FindEquipmentByPath(dto.Path);
    }

    private DynamoEngineeringDto? ResolveExistingDynamo(DynamoEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _assets.FindDynamo(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _assets.FindDynamoByKey(dto.Key);
    }

    private ScreenEngineeringDto? ResolveExistingScreen(ScreenEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _views.FindScreen(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _views.FindScreenByKey(dto.Key);
    }

    private PopupEngineeringDto? ResolveExistingPopup(PopupEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _views.FindPopup(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _views.FindPopupByKey(dto.Key);
    }

    private TagDefinition? ResolveExistingTag(TagEngineeringDto dto)
    {
        if (dto.Id.HasValue && _tags.TryGet(dto.Id.Value, out var byId)) return byId;
        return _tags.TryGetByPath(dto.Path, out var byPath) ? byPath : null;
    }

    private AlarmDefinition? ResolveExistingAlarm(AlarmEngineeringDto dto)
    {
        if (dto.Id.HasValue) return _alarms.Definitions().FirstOrDefault(x => x.Id == dto.Id.Value);
        var tag = ResolveAlarmTag(dto);
        return tag is null ? null : _alarms.Definitions().FirstOrDefault(x => x.TagId == tag.Id && x.Name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase));
    }

    private TagDefinition? ResolveAlarmTagForPreview(AlarmEngineeringDto dto, EngineeringPackage package)
    {
        var existing = ResolveAlarmTag(dto);
        if (existing is not null) return existing;

        TagEngineeringDto? imported = null;
        if (dto.TagId.HasValue) imported = package.Tags.FirstOrDefault(x => x.Id == dto.TagId);
        if (imported is null && !string.IsNullOrWhiteSpace(dto.TagPath))
            imported = package.Tags.FirstOrDefault(x => x.Path.Equals(dto.TagPath, StringComparison.OrdinalIgnoreCase));
        if (imported is null) return null;

        return new TagDefinition(
            imported.Id ?? Guid.Empty, imported.Name, imported.Path, imported.DataType, imported.Source,
            imported.EngineeringUnit, imported.Description, imported.ReadOnly, imported.Metadata,
            BuildTagAccessPolicy(imported.AccessPolicy));
    }

    private TagDefinition? ResolveAlarmTag(AlarmEngineeringDto dto)
    {
        if (dto.TagId.HasValue && _tags.TryGet(dto.TagId.Value, out var byId)) return byId;
        if (!string.IsNullOrWhiteSpace(dto.TagPath) && _tags.TryGetByPath(dto.TagPath, out var byPath)) return byPath;
        return null;
    }

    private static ImportOperation Decide(bool exists, ImportMode mode) => mode switch
    {
        ImportMode.CreateOnly => exists ? ImportOperation.Skip : ImportOperation.Create,
        ImportMode.UpdateExisting => exists ? ImportOperation.Update : ImportOperation.Skip,
        ImportMode.CreateAndUpdate => exists ? ImportOperation.Update : ImportOperation.Create,
        _ => ImportOperation.Error
    };

    private static TagEngineeringDto ToDto(TagDefinition t)
    {
        var address = Meta(t.Metadata, "address");
        var min = Meta(t.Metadata, "scale.minimum");
        var max = Meta(t.Metadata, "scale.maximum");
        var hEnabled = Meta(t.Metadata, "historian.enabled");
        var hStrategy = Meta(t.Metadata, "historian.strategy");
        var deadband = Meta(t.Metadata, "historian.deadband");
        var period = Meta(t.Metadata, "historian.periodMs");
        var maximumPeriod = Meta(t.Metadata, "historian.maxPeriodMs");
        var access = t.AccessPolicy is null
            ? null
            : new TagAccessPolicyDto(
                t.AccessPolicy.ReadRoles?.ToArray(),
                t.AccessPolicy.WriteRoles?.ToArray(),
                t.AccessPolicy.ConfigureRoles?.ToArray());

        return new TagEngineeringDto(
            t.Id, t.Name, t.Path, t.DataType, t.Source, address, t.EngineeringUnit, t.Description, t.ReadOnly,
            DoubleOrNull(min), DoubleOrNull(max),
            new HistorianSettingsDto(Bool(hEnabled), hStrategy ?? "none", DoubleOrNull(deadband), IntOrNull(period), IntOrNull(maximumPeriod)),
            t.Metadata?.ToDictionary(x => x.Key, x => x.Value), access);
    }

    private static AlarmEngineeringDto ToDto(AlarmDefinition a, string? tagPath) =>
        new(a.Id, a.Name, a.TagId, tagPath, a.Type, a.Priority, a.Setpoint, a.DigitalActiveValue, a.AlarmClass, a.Area, a.Message,
            a.ActivationDelay.HasValue ? (int)a.ActivationDelay.Value.TotalMilliseconds : null, a.RequiresAcknowledgement,
            a.ShelvingAllowed, a.Enabled, a.Metadata?.ToDictionary(x => x.Key, x => x.Value));

    private TagEngineeringDto ParseTagCsvRow(string[] row, Dictionary<string, int> header)
    {
        var readRolesJson = Null(Get(row, header, "ReadRolesJson"));
        var writeRolesJson = Null(Get(row, header, "WriteRolesJson"));
        var configureRolesJson = Null(Get(row, header, "ConfigureRolesJson"));
        var accessPolicy = readRolesJson is null && writeRolesJson is null && configureRolesJson is null
            ? null
            : new TagAccessPolicyDto(
                ParseList(readRolesJson), ParseList(writeRolesJson), ParseList(configureRolesJson));

        return new TagEngineeringDto(
            GuidOrNull(Get(row, header, "Id")), Get(row, header, "Name"), Get(row, header, "Path"),
            Enum.Parse<TagDataType>(Get(row, header, "DataType"), true), Null(Get(row, header, "Source")),
            Null(Get(row, header, "Address")), Null(Get(row, header, "Unit")), Null(Get(row, header, "Description")),
            Bool(Get(row, header, "ReadOnly"), true), DoubleOrNull(Get(row, header, "ScaleMinimum")),
            DoubleOrNull(Get(row, header, "ScaleMaximum")),
            new HistorianSettingsDto(
                Bool(Get(row, header, "HistorianEnabled")),
                Null(Get(row, header, "HistorianStrategy")) ?? "none",
                DoubleOrNull(Get(row, header, "Deadband")),
                IntOrNull(Get(row, header, "PeriodMilliseconds")),
                IntOrNull(Get(row, header, "MaximumPeriodMilliseconds"))),
            ParseMap(Get(row, header, "MetadataJson")), accessPolicy);
    }

    private static IReadOnlyDictionary<string,string> BuildTagMetadata(TagEngineeringDto dto)
    {
        var result = dto.Metadata is null
            ? new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string,string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
        Set(result, "address", dto.Address);
        Set(result, "scale.minimum", dto.ScaleMinimum);
        Set(result, "scale.maximum", dto.ScaleMaximum);
        Set(result, "historian.enabled", dto.Historian?.Enabled);
        Set(result, "historian.strategy", dto.Historian?.Strategy);
        Set(result, "historian.deadband", dto.Historian?.Deadband);
        Set(result, "historian.periodMs", dto.Historian?.PeriodMilliseconds);
        Set(result, "historian.maxPeriodMs", dto.Historian?.MaximumPeriodMilliseconds);
        return result;
    }

    private static TagAccessPolicy? BuildTagAccessPolicy(TagAccessPolicyDto? dto) =>
        dto is null
            ? null
            : new TagAccessPolicy(dto.ReadRoles?.ToArray(), dto.WriteRoles?.ToArray(), dto.ConfigureRoles?.ToArray());

    private string? JsonMap(IReadOnlyDictionary<string, string>? map) =>
        map is null || map.Count == 0 ? null : JsonSerializer.Serialize(map, _json);

    private string? JsonList(IReadOnlyCollection<string>? values) =>
        values is null ? null : JsonSerializer.Serialize(values, _json);

    private Dictionary<string, string>? ParseMap(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, _json)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Invalid JSON map in engineering CSV.", ex);
        }
    }

    private IReadOnlyCollection<string>? ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, _json) ?? Array.Empty<string>();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Invalid JSON role list in TAG engineering CSV.", ex);
        }
    }

    private static void Set(Dictionary<string,string> map, string key, object? value)
    {
        if (value is not null) map[key] = Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }

    private static string? Meta(IReadOnlyDictionary<string, string>? metadata, string key) =>
        metadata is not null && metadata.TryGetValue(key, out var value) ? value : null;

    private static EngineeringPackage Empty() => new(
        CurrentSchema,
        CurrentSchemaVersion,
        DateTimeOffset.UtcNow,
        Array.Empty<TagEngineeringDto>(),
        Array.Empty<AlarmEngineeringDto>(),
        Array.Empty<DataSourceEngineeringDto>(),
        Array.Empty<EquipmentTemplateEngineeringDto>(),
        Array.Empty<EquipmentEngineeringDto>(),
        Array.Empty<DynamoEngineeringDto>(),
        Array.Empty<ScreenEngineeringDto>(),
        Array.Empty<PopupEngineeringDto>());

    private static Dictionary<string,int> Header(string[] row) => row.Select((x,i)=>(x,i)).ToDictionary(x=>x.x,x=>x.i,StringComparer.OrdinalIgnoreCase);
    private static string Get(string[] row, Dictionary<string,int> h, string name) => h.TryGetValue(name, out var i) && i < row.Length ? row[i] : string.Empty;
    private static string? Null(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
    private static Guid? GuidOrNull(string? s) => Guid.TryParse(s, out var x) ? x : null;
    private static double? DoubleOrNull(string? s) => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : null;
    private static int? IntOrNull(string? s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : null;
    private static bool Bool(string? s, bool fallback = false) => bool.TryParse(s, out var x) ? x : fallback;
}
