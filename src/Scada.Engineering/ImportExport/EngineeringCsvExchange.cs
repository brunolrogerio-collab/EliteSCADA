using System.Globalization;
using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.ImportExport;

internal sealed class EngineeringCsvExchange
{
    private readonly JsonSerializerOptions _json;

    public EngineeringCsvExchange(JsonSerializerOptions json) => _json = json;

    public string ExportTags(IReadOnlyCollection<TagEngineeringDto> tags)
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            new[]
            {
                "Id", "Path", "Name", "DataType", "Unit", "Source", "Address", "ReadOnly",
                "ScaleMinimum", "ScaleMaximum", "HistorianEnabled", "HistorianStrategy", "Deadband",
                "PeriodMilliseconds", "MaximumPeriodMilliseconds", "Description", "MetadataJson",
                "ReadRolesJson", "WriteRolesJson", "ConfigureRolesJson", "InitialValueDataType", "InitialValueJson"
            }
        };

        foreach (var tag in tags)
        {
            rows.Add(new[]
            {
                tag.Id?.ToString(), tag.Path, tag.Name, tag.DataType.ToString(), tag.EngineeringUnit, tag.Source, tag.Address,
                tag.ReadOnly.ToString(), CsvCodec.Number(tag.ScaleMinimum), CsvCodec.Number(tag.ScaleMaximum),
                (tag.Historian?.Enabled ?? false).ToString(), tag.Historian?.Strategy, CsvCodec.Number(tag.Historian?.Deadband),
                tag.Historian?.PeriodMilliseconds?.ToString(CultureInfo.InvariantCulture),
                tag.Historian?.MaximumPeriodMilliseconds?.ToString(CultureInfo.InvariantCulture), tag.Description,
                JsonMap(tag.Metadata), JsonList(tag.AccessPolicy?.ReadRoles), JsonList(tag.AccessPolicy?.WriteRoles),
                JsonList(tag.AccessPolicy?.ConfigureRoles), tag.InitialValue?.DataType.ToString(), tag.InitialValue?.Value.GetRawText()
            });
        }

        return CsvCodec.Write(rows);
    }

    public string ExportAlarms(IReadOnlyCollection<AlarmEngineeringDto> alarms)
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

        foreach (var alarm in alarms)
        {
            rows.Add(new[]
            {
                alarm.Id?.ToString(), alarm.Name, alarm.TagId?.ToString(), alarm.TagPath, alarm.Type.ToString(), alarm.Priority.ToString(),
                CsvCodec.Number(alarm.Setpoint), alarm.DigitalActiveValue.ToString(), alarm.AlarmClass, alarm.Area, alarm.Message,
                alarm.ActivationDelayMilliseconds?.ToString(CultureInfo.InvariantCulture), alarm.RequiresAcknowledgement.ToString(),
                alarm.ShelvingAllowed.ToString(), alarm.Enabled.ToString(), JsonMap(alarm.Metadata)
            });
        }

        return CsvCodec.Write(rows);
    }

    public string ExportDataSources(IReadOnlyCollection<DataSourceEngineeringDto> dataSources)
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "Id", "Key", "Name", "Driver", "Enabled", "SettingsJson", "SecretReferencesJson", "MetadataJson" }
        };

        foreach (var dataSource in dataSources)
        {
            rows.Add(new[]
            {
                dataSource.Id?.ToString(), dataSource.Key, dataSource.Name, dataSource.Driver, dataSource.Enabled.ToString(),
                JsonMap(dataSource.Settings), JsonMap(dataSource.SecretReferences), JsonMap(dataSource.Metadata)
            });
        }

        return CsvCodec.Write(rows);
    }

    public IReadOnlyCollection<TagEngineeringDto> ParseTags(string csv)
    {
        var rows = CsvCodec.Read(csv);
        if (rows.Count == 0) return Array.Empty<TagEngineeringDto>();
        var header = Header(rows[0]);
        return rows.Skip(1).Select(row => ParseTag(row, header)).ToArray();
    }

    public IReadOnlyCollection<AlarmEngineeringDto> ParseAlarms(string csv)
    {
        var rows = CsvCodec.Read(csv);
        if (rows.Count == 0) return Array.Empty<AlarmEngineeringDto>();
        var header = Header(rows[0]);
        return rows.Skip(1).Select(row => new AlarmEngineeringDto(
            GuidOrNull(Get(row, header, "Id")),
            Get(row, header, "Name"),
            GuidOrNull(Get(row, header, "TagId")),
            Null(Get(row, header, "TagPath")),
            Enum.Parse<AlarmType>(Get(row, header, "Type"), true),
            Enum.Parse<AlarmPriority>(Get(row, header, "Priority"), true),
            DoubleOrNull(Get(row, header, "Setpoint")),
            Bool(Get(row, header, "DigitalActiveValue"), true),
            Null(Get(row, header, "Class")),
            Null(Get(row, header, "Area")),
            Null(Get(row, header, "Message")),
            IntOrNull(Get(row, header, "ActivationDelayMilliseconds")),
            Bool(Get(row, header, "RequiresAcknowledgement"), true),
            Bool(Get(row, header, "ShelvingAllowed"), true),
            Bool(Get(row, header, "Enabled"), true),
            ParseMap(Get(row, header, "MetadataJson")))).ToArray();
    }

    public IReadOnlyCollection<DataSourceEngineeringDto> ParseDataSources(string csv)
    {
        var rows = CsvCodec.Read(csv);
        if (rows.Count == 0) return Array.Empty<DataSourceEngineeringDto>();
        var header = Header(rows[0]);
        return rows.Skip(1).Select(row => new DataSourceEngineeringDto(
            GuidOrNull(Get(row, header, "Id")),
            Get(row, header, "Key"),
            Get(row, header, "Name"),
            Get(row, header, "Driver"),
            Bool(Get(row, header, "Enabled"), true),
            ParseMap(Get(row, header, "SettingsJson")),
            ParseMap(Get(row, header, "SecretReferencesJson")),
            ParseMap(Get(row, header, "MetadataJson")))).ToArray();
    }

    private TagEngineeringDto ParseTag(string[] row, Dictionary<string, int> header)
    {
        var readRolesJson = Null(Get(row, header, "ReadRolesJson"));
        var writeRolesJson = Null(Get(row, header, "WriteRolesJson"));
        var configureRolesJson = Null(Get(row, header, "ConfigureRolesJson"));
        var accessPolicy = readRolesJson is null && writeRolesJson is null && configureRolesJson is null
            ? null
            : new TagAccessPolicyDto(
                ParseList(readRolesJson),
                ParseList(writeRolesJson),
                ParseList(configureRolesJson));
        var initialValue = ParseInitialValue(
            Null(Get(row, header, "InitialValueDataType")),
            Null(Get(row, header, "InitialValueJson")));

        return new TagEngineeringDto(
            GuidOrNull(Get(row, header, "Id")),
            Get(row, header, "Name"),
            Get(row, header, "Path"),
            Enum.Parse<TagDataType>(Get(row, header, "DataType"), true),
            Null(Get(row, header, "Source")),
            Null(Get(row, header, "Address")),
            Null(Get(row, header, "Unit")),
            Null(Get(row, header, "Description")),
            Bool(Get(row, header, "ReadOnly"), true),
            DoubleOrNull(Get(row, header, "ScaleMinimum")),
            DoubleOrNull(Get(row, header, "ScaleMaximum")),
            new HistorianSettingsDto(
                Bool(Get(row, header, "HistorianEnabled")),
                Null(Get(row, header, "HistorianStrategy")) ?? "none",
                DoubleOrNull(Get(row, header, "Deadband")),
                IntOrNull(Get(row, header, "PeriodMilliseconds")),
                IntOrNull(Get(row, header, "MaximumPeriodMilliseconds"))),
            ParseMap(Get(row, header, "MetadataJson")),
            accessPolicy,
            initialValue);
    }

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

    private static MemoryInitialValueDto? ParseInitialValue(string? dataTypeText, string? json)
    {
        if (dataTypeText is null && json is null)
            return null;
        if (dataTypeText is null || json is null)
            throw new InvalidDataException("TAG CSV Internal Memory initial value requires both data type and JSON value.");
        if (!Enum.TryParse<TagDataType>(dataTypeText, ignoreCase: true, out var dataType))
            throw new InvalidDataException($"TAG CSV Internal Memory initial value has unsupported data type '{dataTypeText}'.");

        try
        {
            using var document = JsonDocument.Parse(json);
            var initialValue = new MemoryInitialValueDto(dataType, document.RootElement.Clone());
            _ = MemoryEngineeringValueCodec.ToTypedValue(initialValue);
            return initialValue;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException($"TAG CSV Internal Memory initial value is invalid for data type '{dataType}'.", ex);
        }
    }

    private static Dictionary<string, int> Header(string[] row) =>
        row.Select((value, index) => (value, index))
            .ToDictionary(x => x.value, x => x.index, StringComparer.OrdinalIgnoreCase);

    private static string Get(string[] row, Dictionary<string, int> header, string name) =>
        header.TryGetValue(name, out var index) && index < row.Length ? row[index] : string.Empty;

    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static Guid? GuidOrNull(string? value) => Guid.TryParse(value, out var parsed) ? parsed : null;

    private static double? DoubleOrNull(string? value) =>
        double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static int? IntOrNull(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static bool Bool(string? value, bool fallback = false) =>
        bool.TryParse(value, out var parsed) ? parsed : fallback;
}
