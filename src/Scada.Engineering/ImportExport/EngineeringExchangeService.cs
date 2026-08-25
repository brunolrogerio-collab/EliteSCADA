using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Validation;

namespace Scada.Engineering.ImportExport;

public sealed class EngineeringExchangeService : IEngineeringExchangeService
{
    public const string CurrentSchema = "scada.engineering";
    public const int CurrentSchemaVersion = 2;

    private readonly ITagRegistry _tags;
    private readonly IAlarmEngine _alarms;
    private readonly IDataSourceEngineeringRegistry _dataSources;
    private readonly JsonSerializerOptions _json;

    public EngineeringExchangeService(ITagRegistry tags, IAlarmEngine alarms)
        : this(tags, alarms, new InMemoryDataSourceEngineeringRegistry())
    {
    }

    public EngineeringExchangeService(ITagRegistry tags, IAlarmEngine alarms, IDataSourceEngineeringRegistry dataSources)
    {
        _tags = tags;
        _alarms = alarms;
        _dataSources = dataSources;
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
            _dataSources.Snapshot());
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
            new[] { "Id", "Path", "Name", "DataType", "Unit", "Source", "Address", "ReadOnly", "ScaleMinimum", "ScaleMaximum", "HistorianEnabled", "HistorianStrategy", "Deadband", "PeriodMilliseconds", "Description" }
        };
        foreach (var t in ExportPackage().Tags)
            rows.Add(new[] { t.Id?.ToString(), t.Path, t.Name, t.DataType.ToString(), t.EngineeringUnit, t.Source, t.Address, t.ReadOnly.ToString(), CsvCodec.Number(t.ScaleMinimum), CsvCodec.Number(t.ScaleMaximum), (t.Historian?.Enabled ?? false).ToString(), t.Historian?.Strategy, CsvCodec.Number(t.Historian?.Deadband), t.Historian?.PeriodMilliseconds?.ToString(CultureInfo.InvariantCulture), t.Description });
        return CsvCodec.Write(rows);
    }

    public string ExportAlarmsCsv()
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "Id", "Name", "TagId", "TagPath", "Type", "Priority", "Setpoint", "DigitalActiveValue", "Class", "Area", "Message", "ActivationDelayMilliseconds", "RequiresAcknowledgement", "ShelvingAllowed", "Enabled" }
        };
        foreach (var a in ExportPackage().Alarms)
            rows.Add(new[] { a.Id?.ToString(), a.Name, a.TagId?.ToString(), a.TagPath, a.Type.ToString(), a.Priority.ToString(), CsvCodec.Number(a.Setpoint), a.DigitalActiveValue.ToString(), a.AlarmClass, a.Area, a.Message, a.ActivationDelayMilliseconds?.ToString(CultureInfo.InvariantCulture), a.RequiresAcknowledgement.ToString(), a.ShelvingAllowed.ToString(), a.Enabled.ToString() });
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

        return package with { DataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>() };
    }

    public EngineeringPackage ParseTagsCsv(string csv)
    {
        var rows = CsvCodec.Read(csv);
        if (rows.Count == 0) return Empty();
        var h = Header(rows[0]);
        var tags = rows.Skip(1).Select(r => new TagEngineeringDto(
            GuidOrNull(Get(r,h,"Id")), Get(r,h,"Name"), Get(r,h,"Path"), Enum.Parse<TagDataType>(Get(r,h,"DataType"), true),
            Null(Get(r,h,"Source")), Null(Get(r,h,"Address")), Null(Get(r,h,"Unit")), Null(Get(r,h,"Description")), Bool(Get(r,h,"ReadOnly"), true),
            DoubleOrNull(Get(r,h,"ScaleMinimum")), DoubleOrNull(Get(r,h,"ScaleMaximum")),
            new HistorianSettingsDto(Bool(Get(r,h,"HistorianEnabled")), Null(Get(r,h,"HistorianStrategy")) ?? "none", DoubleOrNull(Get(r,h,"Deadband")), IntOrNull(Get(r,h,"PeriodMilliseconds"))), null)).ToArray();
        return Empty() with { Tags = tags };
    }

    public EngineeringPackage ParseAlarmsCsv(string csv)
    {
        var rows = CsvCodec.Read(csv);
        if (rows.Count == 0) return Empty();
        var h = Header(rows[0]);
        var alarms = rows.Skip(1).Select(r => new AlarmEngineeringDto(
            GuidOrNull(Get(r,h,"Id")), Get(r,h,"Name"), GuidOrNull(Get(r,h,"TagId")), Null(Get(r,h,"TagPath")),
            Enum.Parse<AlarmType>(Get(r,h,"Type"), true), Enum.Parse<AlarmPriority>(Get(r,h,"Priority"), true), DoubleOrNull(Get(r,h,"Setpoint")), Bool(Get(r,h,"DigitalActiveValue"), true),
            Null(Get(r,h,"Class")), Null(Get(r,h,"Area")), Null(Get(r,h,"Message")), IntOrNull(Get(r,h,"ActivationDelayMilliseconds")), Bool(Get(r,h,"RequiresAcknowledgement"), true), Bool(Get(r,h,"ShelvingAllowed"), true), Bool(Get(r,h,"Enabled"), true), null)).ToArray();
        return Empty() with { Alarms = alarms };
    }

    public ImportPreview Preview(EngineeringPackage package, ImportMode mode)
    {
        var items = new List<ImportPreviewItem>();
        var dataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>();
        var duplicateDataSourceKeys = dataSources
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var dto in dataSources)
        {
            var issues = EngineeringValidator.ValidateDataSource(dto).ToList();
            if (duplicateDataSourceKeys.Contains(dto.Key))
                issues.Add(new("DATASOURCE_DUPLICATE_IN_FILE", $"Data source key '{dto.Key}' appears more than once in the import package.", ImportEntityKind.DataSource, dto.Key, true));
            var existing = ResolveExistingDataSource(dto);
            var op = Decide(existing is not null, mode);
            if (issues.Any(x => x.IsError)) op = ImportOperation.Error;
            items.Add(new(ImportEntityKind.DataSource, dto.Key, op, issues));
        }

        var duplicatePaths = package.Tags.GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var dto in package.Tags)
        {
            var issues = EngineeringValidator.ValidateTag(dto).ToList();
            if (duplicatePaths.Contains(dto.Path))
                issues.Add(new("TAG_DUPLICATE_IN_FILE", $"Tag path '{dto.Path}' appears more than once in the import file.", ImportEntityKind.Tag, dto.Path, true));

            if (package.SchemaVersion >= 2 && !string.IsNullOrWhiteSpace(dto.Source) &&
                _dataSources.FindByKey(dto.Source) is null &&
                !dataSources.Any(x => x.Key.Equals(dto.Source, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new("TAG_DATASOURCE_NOT_FOUND", $"Data source '{dto.Source}' referenced by tag '{dto.Path}' was not found.", ImportEntityKind.Tag, dto.Path, true));
            }

            var existing = ResolveExistingTag(dto);
            var op = Decide(existing is not null, mode);
            if (issues.Any(x => x.IsError)) op = ImportOperation.Error;
            items.Add(new(ImportEntityKind.Tag, dto.Path, op, issues));
        }

        foreach (var dto in package.Alarms)
        {
            var issues = EngineeringValidator.ValidateAlarm(dto).ToList();
            if (ResolveAlarmTagForPreview(dto, package) is null)
                issues.Add(new("ALARM_TAG_NOT_FOUND", $"Referenced tag for alarm '{dto.Name}' was not found in current registry or package.", ImportEntityKind.Alarm, dto.Name, true));
            var existing = ResolveExistingAlarm(dto);
            var op = Decide(existing is not null, mode);
            if (issues.Any(x => x.IsError)) op = ImportOperation.Error;
            items.Add(new(ImportEntityKind.Alarm, dto.Name, op, issues));
        }

        return new(mode,
            items.Count(x => x.Operation == ImportOperation.Create),
            items.Count(x => x.Operation == ImportOperation.Update),
            items.Count(x => x.Operation == ImportOperation.Skip),
            items.Count(x => x.Operation == ImportOperation.Error), items);
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
        {
            var existing = ResolveExistingDataSource(dto);
            var op = Decide(existing is not null, mode);
            if (op == ImportOperation.Skip) { skipped++; continue; }
            _dataSources.Upsert(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }

        foreach (var dto in package.Tags)
        {
            var existing = ResolveExistingTag(dto);
            var op = Decide(existing is not null, mode);
            if (op == ImportOperation.Skip) { skipped++; continue; }
            var id = existing?.Id ?? dto.Id ?? Guid.NewGuid();
            var tag = new TagDefinition(id, dto.Name, dto.Path, dto.DataType, dto.Source, dto.EngineeringUnit, dto.Description, dto.ReadOnly, BuildTagMetadata(dto));
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

        return new(mode, created, updated, skipped, Array.Empty<ImportIssue>());
    }

    private DataSourceEngineeringDto? ResolveExistingDataSource(DataSourceEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _dataSources.Find(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _dataSources.FindByKey(dto.Key);
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

        return new TagDefinition(imported.Id ?? Guid.Empty, imported.Name, imported.Path, imported.DataType, imported.Source, imported.EngineeringUnit, imported.Description, imported.ReadOnly, imported.Metadata);
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
        return new(t.Id, t.Name, t.Path, t.DataType, t.Source, address, t.EngineeringUnit, t.Description, t.ReadOnly,
            DoubleOrNull(min), DoubleOrNull(max), new HistorianSettingsDto(Bool(hEnabled), hStrategy ?? "none", DoubleOrNull(deadband), IntOrNull(period)),
            t.Metadata?.ToDictionary(x => x.Key, x => x.Value));
    }

    private static AlarmEngineeringDto ToDto(AlarmDefinition a, string? tagPath) =>
        new(a.Id, a.Name, a.TagId, tagPath, a.Type, a.Priority, a.Setpoint, a.DigitalActiveValue, a.AlarmClass, a.Area, a.Message,
            a.ActivationDelay.HasValue ? (int)a.ActivationDelay.Value.TotalMilliseconds : null, a.RequiresAcknowledgement,
            a.ShelvingAllowed, a.Enabled, a.Metadata?.ToDictionary(x => x.Key, x => x.Value));

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
        return result;
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
        Array.Empty<DataSourceEngineeringDto>());

    private static Dictionary<string,int> Header(string[] row) => row.Select((x,i)=>(x,i)).ToDictionary(x=>x.x,x=>x.i,StringComparer.OrdinalIgnoreCase);
    private static string Get(string[] row, Dictionary<string,int> h, string name) => h.TryGetValue(name, out var i) && i < row.Length ? row[i] : string.Empty;
    private static string? Null(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
    private static Guid? GuidOrNull(string? s) => Guid.TryParse(s, out var x) ? x : null;
    private static double? DoubleOrNull(string? s) => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : null;
    private static int? IntOrNull(string? s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : null;
    private static bool Bool(string? s, bool fallback = false) => bool.TryParse(s, out var x) ? x : fallback;
}
