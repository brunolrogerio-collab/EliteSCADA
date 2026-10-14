using System.Globalization;
using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;
using Scada.Engineering.Validation;

namespace Scada.Engineering.ImportExport;

internal sealed class EngineeringCsvExchange
{
    private static readonly string[] TagColumns =
    [
        "Id", "Path", "Name", "DataType", "Unit", "Source", "Address", "AddressSelectorKind", "AddressSelectorIndex", "ReadOnly",
        "ScaleMinimum", "ScaleMaximum", "HistorianEnabled", "HistorianStrategy", "Deadband", "PeriodMilliseconds",
        "MaximumPeriodMilliseconds", "Description", "MetadataJson", "ReadRolesJson", "WriteRolesJson", "ConfigureRolesJson",
        "InitialValueDataType", "InitialValueJson", "CommunicationBindingJson", "DataSourceId", "HistorianCaptureProfileId"
    ];
    private static readonly string[] AlarmColumns =
    [
        "Id", "Name", "TagId", "TagPath", "Type", "Priority", "Setpoint", "DigitalActiveValue", "Class", "Area", "Message",
        "ActivationDelayMilliseconds", "RequiresAcknowledgement", "ShelvingAllowed", "Enabled", "SoundProfile", "MetadataJson"
    ];
    private static readonly string[] DataSourceColumns =
        ["Id", "Key", "Name", "Driver", "Enabled", "SettingsJson", "SecretReferencesJson", "MetadataJson"];
    private static readonly string[] GatewayRouteColumns =
    [
        "Id", "Key", "Name", "SourceTagId", "SourceTagPath", "DestinationTagId", "DestinationTagPath", "TransferMode",
        "QualityPolicy", "ConversionPolicy", "InitialTransferPolicy", "Gain", "Offset", "Deadband", "MinimumIntervalMilliseconds",
        "PeriodMilliseconds", "Description", "Enabled", "MetadataJson"
    ];
    private static readonly string[] OperationalEventColumns =
        ["Id", "Key", "Name", "Type", "Category", "Source", "Area", "EquipmentPath", "TagId", "TagPath", "Message", "Enabled", "MetadataJson"];

    private readonly JsonSerializerOptions _json;

    public EngineeringCsvExchange(JsonSerializerOptions json) => _json = json;

    public string ExportTags(IReadOnlyCollection<TagEngineeringDto> tags)
    {
        if (tags.Any(tag => ContainsPlaintextSecret(tag.CommunicationBinding?.EffectiveSettings)))
            throw new InvalidDataException("Export blocked because a TAG contains plaintext secret material. Replace it with a secret reference first.");

        var rows = new List<IReadOnlyList<string?>>
        {
            new[]
            {
                "Id", "Path", "Name", "DataType", "Unit", "Source", "Address", "AddressSelectorKind", "AddressSelectorIndex", "ReadOnly",
                "ScaleMinimum", "ScaleMaximum", "HistorianEnabled", "HistorianStrategy", "Deadband",
                "PeriodMilliseconds", "MaximumPeriodMilliseconds", "Description", "MetadataJson",
                "ReadRolesJson", "WriteRolesJson", "ConfigureRolesJson", "InitialValueDataType", "InitialValueJson",
                "CommunicationBindingJson", "DataSourceId", "HistorianCaptureProfileId"
            }
        };

        foreach (var tag in tags)
        {
            rows.Add(new[]
            {
                tag.Id?.ToString(), tag.Path, tag.Name, tag.DataType.ToString(), tag.EngineeringUnit, tag.Source, tag.Address,
                tag.AddressSelector?.Kind.ToString(), tag.AddressSelector?.Index.ToString(CultureInfo.InvariantCulture),
                tag.ReadOnly.ToString(), CsvCodec.Number(tag.ScaleMinimum), CsvCodec.Number(tag.ScaleMaximum),
                (tag.Historian?.Enabled ?? false).ToString(), tag.Historian?.Strategy, CsvCodec.Number(tag.Historian?.Deadband),
                tag.Historian?.PeriodMilliseconds?.ToString(CultureInfo.InvariantCulture),
                tag.Historian?.MaximumPeriodMilliseconds?.ToString(CultureInfo.InvariantCulture), tag.Description,
                JsonMap(tag.Metadata), JsonList(tag.AccessPolicy?.ReadRoles), JsonList(tag.AccessPolicy?.WriteRoles),
                JsonList(tag.AccessPolicy?.ConfigureRoles), tag.InitialValue?.DataType.ToString(), tag.InitialValue?.Value.GetRawText(),
                JsonCommunicationBinding(tag.CommunicationBinding), tag.DataSourceId?.ToString("D"),
                tag.HistorianCaptureProfileId?.ToString("D")
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
                "ShelvingAllowed", "Enabled", "SoundProfile", "MetadataJson"
            }
        };

        foreach (var alarm in alarms)
        {
            rows.Add(new[]
            {
                alarm.Id?.ToString(), alarm.Name, alarm.TagId?.ToString(), alarm.TagPath, alarm.Type.ToString(), alarm.Priority.ToString(),
                CsvCodec.Number(alarm.Setpoint), alarm.DigitalActiveValue.ToString(), alarm.AlarmClass, alarm.Area, alarm.Message,
                alarm.ActivationDelayMilliseconds?.ToString(CultureInfo.InvariantCulture), alarm.RequiresAcknowledgement.ToString(),
                alarm.ShelvingAllowed.ToString(), alarm.Enabled.ToString(), alarm.SoundProfile, JsonMap(alarm.Metadata)
            });
        }

        return CsvCodec.Write(rows);
    }

    public string ExportDataSources(IReadOnlyCollection<DataSourceEngineeringDto> dataSources)
    {
        if (dataSources.Any(dataSource => EngineeringValidator.ValidateDataSource(dataSource)
                .Any(issue => issue.Code == "DATASOURCE_PLAINTEXT_SECRET")))
            throw new InvalidDataException("Export blocked because a Data Source contains plaintext secret material. Replace it with a secret reference first.");

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

    public string ExportGatewayRoutes(IReadOnlyCollection<GatewayRouteEngineeringDto> routes)
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            new[]
            {
                "Id", "Key", "Name", "SourceTagId", "SourceTagPath", "DestinationTagId", "DestinationTagPath",
                "TransferMode", "QualityPolicy", "ConversionPolicy", "InitialTransferPolicy", "Gain", "Offset",
                "Deadband", "MinimumIntervalMilliseconds", "PeriodMilliseconds", "Description", "Enabled", "MetadataJson"
            }
        };

        foreach (var route in routes)
        {
            rows.Add(new[]
            {
                route.Id?.ToString("D"), route.Key, route.Name, route.SourceTagId?.ToString("D"), route.SourceTagPath,
                route.DestinationTagId?.ToString("D"), route.DestinationTagPath, route.TransferMode.ToString(),
                route.QualityPolicy.ToString(), route.ConversionPolicy.ToString(), route.InitialTransferPolicy.ToString(),
                CsvCodec.Number(route.Gain), CsvCodec.Number(route.Offset), CsvCodec.Number(route.Deadband),
                route.MinimumIntervalMilliseconds?.ToString(CultureInfo.InvariantCulture),
                route.PeriodMilliseconds?.ToString(CultureInfo.InvariantCulture), route.Description, route.Enabled.ToString(),
                JsonMap(route.Metadata)
            });
        }

        return CsvCodec.Write(rows);
    }

    public string ExportOperationalEvents(IReadOnlyCollection<OperationalEventEngineeringDto> events)
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "Id", "Key", "Name", "Type", "Category", "Source", "Area", "EquipmentPath", "TagId", "TagPath", "Message", "Enabled", "MetadataJson" }
        };
        foreach (var item in events)
        {
            rows.Add(new[]
            {
                item.Id?.ToString("D"), item.Key, item.Name, item.Type, item.Category, item.Source, item.Area,
                item.EquipmentPath, item.TagId?.ToString("D"), item.TagPath, item.Message, item.Enabled.ToString(), JsonMap(item.Metadata)
            });
        }
        return CsvCodec.Write(rows);
    }

    public IReadOnlyCollection<TagEngineeringDto> ParseTags(string csv)
    {
        var table = CsvCodec.ReadTable(csv);
        if (table is null) throw new InvalidDataException("TAG CSV import requires a header row.");
        var header = Header(table.Headers, TagColumns, "Id", "Name", "Path", "DataType");
        return ParseRows(table, row => ParseTag(row.Cells, header));
    }

    public IReadOnlyCollection<AlarmEngineeringDto> ParseAlarms(string csv)
    {
        var table = CsvCodec.ReadTable(csv);
        if (table is null) throw new InvalidDataException("Alarm CSV import requires a header row.");
        var header = Header(table.Headers, AlarmColumns, "Id", "Name", "TagId", "TagPath", "Type", "Priority");
        return ParseRows(table, row => new AlarmEngineeringDto(
            GuidOrNull(Get(row.Cells, header, "Id")),
            Get(row.Cells, header, "Name"),
            GuidOrNull(Get(row.Cells, header, "TagId")),
            Null(Get(row.Cells, header, "TagPath")),
            EnumValue<AlarmType>(Get(row.Cells, header, "Type")),
            EnumValue<AlarmPriority>(Get(row.Cells, header, "Priority")),
            DoubleOrNull(Get(row.Cells, header, "Setpoint")),
            Bool(Get(row.Cells, header, "DigitalActiveValue"), true),
            Null(Get(row.Cells, header, "Class")),
            Null(Get(row.Cells, header, "Area")),
            Null(Get(row.Cells, header, "Message")),
            IntOrNull(Get(row.Cells, header, "ActivationDelayMilliseconds")),
            Bool(Get(row.Cells, header, "RequiresAcknowledgement"), true),
            Bool(Get(row.Cells, header, "ShelvingAllowed"), true),
            Bool(Get(row.Cells, header, "Enabled"), true),
            ParseMap(Get(row.Cells, header, "MetadataJson")),
            Null(Get(row.Cells, header, "SoundProfile"))));
    }

    public IReadOnlyCollection<DataSourceEngineeringDto> ParseDataSources(string csv)
    {
        var table = CsvCodec.ReadTable(csv);
        if (table is null) throw new InvalidDataException("Data Source CSV import requires a header row.");
        var header = Header(table.Headers, DataSourceColumns, "Id", "Key", "Name", "Driver");
        return ParseRows(table, row => new DataSourceEngineeringDto(
            GuidOrNull(Get(row.Cells, header, "Id")),
            Get(row.Cells, header, "Key"),
            Get(row.Cells, header, "Name"),
            Get(row.Cells, header, "Driver"),
            Bool(Get(row.Cells, header, "Enabled"), true),
            ParseMap(Get(row.Cells, header, "SettingsJson")),
            ParseMap(Get(row.Cells, header, "SecretReferencesJson")),
            ParseMap(Get(row.Cells, header, "MetadataJson"))));
    }

    public IReadOnlyCollection<GatewayRouteEngineeringDto> ParseGatewayRoutes(string csv)
    {
        var table = CsvCodec.ReadTable(csv);
        if (table is null) throw new InvalidDataException("Gateway route CSV import requires a header row.");
        var header = Header(table.Headers, GatewayRouteColumns, "Id", "Key", "Name", "SourceTagId", "SourceTagPath", "DestinationTagId", "DestinationTagPath");
        return ParseRows(table, row => new GatewayRouteEngineeringDto(
            GuidOrNull(Get(row.Cells, header, "Id")), Get(row.Cells, header, "Key"), Get(row.Cells, header, "Name"),
            GuidOrNull(Get(row.Cells, header, "SourceTagId")), Null(Get(row.Cells, header, "SourceTagPath")),
            GuidOrNull(Get(row.Cells, header, "DestinationTagId")), Null(Get(row.Cells, header, "DestinationTagPath")),
            EnumValue<GatewayTransferMode>(Get(row.Cells, header, "TransferMode"), GatewayTransferMode.OnChange),
            EnumValue<GatewayQualityPolicy>(Get(row.Cells, header, "QualityPolicy"), GatewayQualityPolicy.GoodOnly),
            EnumValue<GatewayConversionPolicy>(Get(row.Cells, header, "ConversionPolicy"), GatewayConversionPolicy.Exact),
            EnumValue<GatewayInitialTransferPolicy>(Get(row.Cells, header, "InitialTransferPolicy"), GatewayInitialTransferPolicy.SynchronizeFirstAcceptableValue),
            DoubleOrNull(Get(row.Cells, header, "Gain")), DoubleOrNull(Get(row.Cells, header, "Offset")), DoubleOrNull(Get(row.Cells, header, "Deadband")),
            IntOrNull(Get(row.Cells, header, "MinimumIntervalMilliseconds")), IntOrNull(Get(row.Cells, header, "PeriodMilliseconds")),
            Null(Get(row.Cells, header, "Description")), Bool(Get(row.Cells, header, "Enabled"), true),
            ParseMap(Get(row.Cells, header, "MetadataJson"))));
    }

    public IReadOnlyCollection<OperationalEventEngineeringDto> ParseOperationalEvents(string csv)
    {
        var table = CsvCodec.ReadTable(csv);
        if (table is null) throw new InvalidDataException("Operational Event CSV import requires a header row.");
        var header = Header(table.Headers, OperationalEventColumns, "Id", "Key", "Name", "Type", "Category", "Source", "TagId", "TagPath");
        return ParseRows(table, row => new OperationalEventEngineeringDto(
            GuidOrNull(Get(row.Cells, header, "Id")), Get(row.Cells, header, "Key"), Get(row.Cells, header, "Name"),
            Get(row.Cells, header, "Type"), Get(row.Cells, header, "Category"), Get(row.Cells, header, "Source"),
            Null(Get(row.Cells, header, "Area")), Null(Get(row.Cells, header, "EquipmentPath")),
            GuidOrNull(Get(row.Cells, header, "TagId")), Null(Get(row.Cells, header, "TagPath")),
            Null(Get(row.Cells, header, "Message")), Bool(Get(row.Cells, header, "Enabled"), true),
            ParseMap(Get(row.Cells, header, "MetadataJson"))));
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
        var addressSelector = ParseAddressSelector(
            Null(Get(row, header, "AddressSelectorKind")),
            Null(Get(row, header, "AddressSelectorIndex")));
        var communicationBinding = ParseCommunicationBinding(
            Null(Get(row, header, "CommunicationBindingJson")));

        return new TagEngineeringDto(
            GuidOrNull(Get(row, header, "Id")),
            Get(row, header, "Name"),
            Get(row, header, "Path"),
            EnumValue<TagDataType>(Get(row, header, "DataType")),
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
            initialValue,
            addressSelector,
            communicationBinding,
            GuidOrNull(Get(row, header, "DataSourceId")),
            GuidOrNull(Get(row, header, "HistorianCaptureProfileId")));
    }

    private string? JsonMap(IReadOnlyDictionary<string, string>? map) =>
        map is null || map.Count == 0 ? null : JsonSerializer.Serialize(map, _json);

    private string? JsonList(IReadOnlyCollection<string>? values) =>
        values is null ? null : JsonSerializer.Serialize(values, _json);

    private string? JsonCommunicationBinding(CommunicationTagBinding? binding) =>
        binding is null ? null : JsonSerializer.Serialize(binding, _json);

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

    private CommunicationTagBinding? ParseCommunicationBinding(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<CommunicationTagBinding>(json, _json)
                ?? throw new InvalidDataException("Invalid CommunicationBinding JSON in TAG engineering CSV.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Invalid CommunicationBinding JSON in TAG engineering CSV.", ex);
        }
    }

    private static MemoryInitialValueDto? ParseInitialValue(string? dataTypeText, string? json)
    {
        if (dataTypeText is null && json is null)
            return null;
        if (dataTypeText is null || json is null)
            throw new InvalidDataException("TAG CSV Internal Memory initial value requires both data type and JSON value.");
        var dataType = EnumValue<TagDataType>(dataTypeText);

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

    private static TagValueSelector? ParseAddressSelector(string? kindText, string? indexText)
    {
        if (kindText is null && indexText is null)
            return null;
        if (kindText is null || indexText is null)
            throw new InvalidDataException("TAG CSV address selector requires both kind and index.");
        var kind = EnumValue<TagValueSelectorKind>(kindText);
        if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            throw new InvalidDataException($"TAG CSV address selector index '{indexText}' is invalid.");

        return new TagValueSelector(kind, index);
    }

    private static Dictionary<string, int> Header(string[] row, IReadOnlyCollection<string> supported, params string[] required)
    {
        var header = row.Select((value, index) => (value, index))
            .ToDictionary(x => x.value, x => x.index, StringComparer.OrdinalIgnoreCase);
        var supportedKeys = new HashSet<string>(supported, StringComparer.OrdinalIgnoreCase);
        var unsupported = row.Where(name => !supportedKeys.Contains(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (unsupported.Length > 0)
            throw new InvalidDataException($"Tabular header contains unsupported column(s): {string.Join(", ", unsupported)}.");
        var missing = required.Where(name => !header.ContainsKey(name)).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException($"Tabular header is missing required column(s): {string.Join(", ", missing)}.");
        return header;
    }

    private static string Get(string[] row, Dictionary<string, int> header, string name) =>
        header.TryGetValue(name, out var index) && index < row.Length ? row[index] : string.Empty;

    private static IReadOnlyCollection<T> ParseRows<T>(CsvTable table, Func<CsvTableRow, T> parse)
    {
        var result = new List<T>(table.Rows.Count);
        foreach (var row in table.Rows)
        {
            try
            {
                result.Add(parse(row));
            }
            catch (Exception exception) when (exception is InvalidDataException or FormatException or OverflowException or ArgumentException)
            {
                throw new InvalidDataException($"CSV row {row.LineNumber}: {exception.Message}", exception);
            }
        }
        return result;
    }

    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static Guid? GuidOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Guid.TryParse(value, out var parsed)) return parsed;
        throw new InvalidDataException("Expected a GUID value.");
    }

    private static double? DoubleOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed)) return parsed;
        throw new InvalidDataException("Expected a finite invariant-culture number.");
    }

    private static int? IntOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        throw new InvalidDataException("Expected an invariant-culture integer.");
    }

    private static bool Bool(string? value, bool fallback = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (bool.TryParse(value, out var parsed)) return parsed;
        throw new InvalidDataException("Expected true or false.");
    }

    private static T EnumValue<T>(string? value, T? fallback = null) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value) && fallback.HasValue) return fallback.Value;
        if (Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        throw new InvalidDataException($"Unsupported {typeof(T).Name} value.");
    }

    private static bool ContainsPlaintextSecret(IReadOnlyDictionary<string, string>? settings)
    {
        if (settings is null) return false;
        string[] sensitiveNames = ["password", "passwd", "pwd", "secret", "token", "apikey", "api_key", "privatekey", "private_key", "credential", "clientsecret", "client_secret"];
        string[] sensitiveAssignments = ["password=", "passwd=", "pwd=", "token=", "apikey=", "api_key=", "clientsecret=", "client_secret=", "privatekey=", "private_key="];
        return settings.Any(setting =>
        {
            var normalizedName = setting.Key.Replace("-", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal);
            return sensitiveNames.Any(fragment => normalizedName.Contains(fragment, StringComparison.OrdinalIgnoreCase)) ||
                   sensitiveAssignments.Any(fragment => setting.Value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        });
    }
}
