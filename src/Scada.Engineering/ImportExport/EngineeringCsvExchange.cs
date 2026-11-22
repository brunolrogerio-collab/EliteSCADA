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
        "InitialValueDataType", "InitialValueJson", "CommunicationBindingJson", "DataSourceId", "DataSourceKey",
        "HistorianCaptureProfileId", "HistorianCaptureProfileKey",
        "ControlledEngineeringUnitJson", "ControlledEngineeringUnitContractVersion"
    ];
    private static readonly string[] AlarmColumns =
    [
        "Id", "Name", "TagId", "TagPath", "Type", "Priority", "Setpoint", "DigitalActiveValue", "Class", "Area", "Message",
        "ActivationDelayMilliseconds", "RequiresAcknowledgement", "ShelvingAllowed", "Enabled", "SoundProfile", "MetadataJson",
        "LocationPath", "EquipmentPath", "LocationId", "EquipmentId"
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
        => ExportTags(tags, [], []);

    public string ExportTags(
        IReadOnlyCollection<TagEngineeringDto> tags,
        IReadOnlyCollection<DataSourceEngineeringDto>? dataSources,
        IReadOnlyCollection<HistorianCaptureProfileEngineeringDto>? historianProfiles)
    {
        if (tags.Any(tag => ContainsPlaintextSecret(tag.CommunicationBinding?.EffectiveSettings)))
            throw new InvalidDataException("Export blocked because a TAG contains plaintext secret material. Replace it with a secret reference first.");

        var rows = new List<IReadOnlyList<string?>>
        {
            new[]
            {
                "Path", "Name", "DataType", "Unit", "DataSourceKey", "Address", "AddressSelectorKind", "AddressSelectorIndex", "ReadOnly",
                "ScaleMinimum", "ScaleMaximum", "HistorianEnabled", "HistorianStrategy", "Deadband",
                "PeriodMilliseconds", "MaximumPeriodMilliseconds", "Description", "MetadataJson",
                "ReadRolesJson", "WriteRolesJson", "ConfigureRolesJson", "InitialValueDataType", "InitialValueJson",
                "CommunicationBindingJson", "HistorianCaptureProfileKey", "ControlledEngineeringUnitJson",
                "ControlledEngineeringUnitContractVersion"
            }
        };

        var dataSourceKeys = (dataSources ?? Array.Empty<DataSourceEngineeringDto>())
            .Where(item => item.Id.HasValue)
            .ToDictionary(item => item.Id!.Value, item => item.Key);
        var profileKeys = (historianProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>())
            .Where(item => item.Id.HasValue)
            .ToDictionary(item => item.Id!.Value, item => item.Key);

        var tagWithUnresolvedDataSource = tags.FirstOrDefault(tag =>
            tag.DataSourceId.HasValue &&
            !dataSourceKeys.ContainsKey(tag.DataSourceId.Value) &&
            string.IsNullOrWhiteSpace(tag.Source));
        if (tagWithUnresolvedDataSource is not null)
            throw new InvalidDataException($"TAG '{tagWithUnresolvedDataSource.Path}' references a Data Source that cannot be resolved to a readable key.");

        var tagWithUnresolvedHistorianProfile = tags.FirstOrDefault(tag =>
            tag.HistorianCaptureProfileId.HasValue &&
            !profileKeys.ContainsKey(tag.HistorianCaptureProfileId.Value));
        if (tagWithUnresolvedHistorianProfile is not null)
            throw new InvalidDataException($"TAG '{tagWithUnresolvedHistorianProfile.Path}' references a Historian profile that cannot be resolved to a readable key.");

        foreach (var tag in tags)
        {
            rows.Add(new[]
            {
                tag.Path, tag.Name, tag.DataType.ToString(), tag.EngineeringUnit,
                tag.DataSourceId.HasValue && dataSourceKeys.TryGetValue(tag.DataSourceId.Value, out var dataSourceKey)
                    ? dataSourceKey
                    : tag.Source,
                tag.Address,
                tag.AddressSelector?.Kind.ToString(), tag.AddressSelector?.Index.ToString(CultureInfo.InvariantCulture),
                tag.ReadOnly.ToString(), CsvCodec.Number(tag.ScaleMinimum), CsvCodec.Number(tag.ScaleMaximum),
                (tag.Historian?.Enabled ?? false).ToString(), tag.Historian?.Strategy, CsvCodec.Number(tag.Historian?.Deadband),
                tag.Historian?.PeriodMilliseconds?.ToString(CultureInfo.InvariantCulture),
                tag.Historian?.MaximumPeriodMilliseconds?.ToString(CultureInfo.InvariantCulture), tag.Description,
                JsonMap(tag.Metadata), JsonList(tag.AccessPolicy?.ReadRoles), JsonList(tag.AccessPolicy?.WriteRoles),
                JsonList(tag.AccessPolicy?.ConfigureRoles), tag.InitialValue?.DataType.ToString(), tag.InitialValue?.Value.GetRawText(),
                JsonCommunicationBinding(tag.CommunicationBinding),
                tag.HistorianCaptureProfileId.HasValue && profileKeys.TryGetValue(tag.HistorianCaptureProfileId.Value, out var profileKey)
                    ? profileKey
                    : null,
                JsonControlledUnit(tag.ControlledEngineeringUnit), "2"
            });
        }

        return CsvCodec.Write(rows);
    }

    public string ExportAlarms(IReadOnlyCollection<AlarmEngineeringDto> alarms)
        => ExportAlarms(alarms, [], [], []);

    public string ExportAlarms(
        IReadOnlyCollection<AlarmEngineeringDto> alarms,
        IReadOnlyCollection<TagEngineeringDto>? tags)
        => ExportAlarms(alarms, tags, [], []);

    public string ExportAlarms(
        IReadOnlyCollection<AlarmEngineeringDto> alarms,
        IReadOnlyCollection<TagEngineeringDto>? tags,
        IReadOnlyCollection<LocationEngineeringDto>? locations,
        IReadOnlyCollection<EquipmentEngineeringDto>? equipment)
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            new[]
            {
                "Name", "TagPath", "Type", "Priority", "Setpoint", "DigitalActiveValue",
                "Class", "Area", "Message", "ActivationDelayMilliseconds", "RequiresAcknowledgement",
                "ShelvingAllowed", "Enabled", "SoundProfile", "MetadataJson", "LocationPath", "EquipmentPath"
            }
        };
        var tagPaths = (tags ?? Array.Empty<TagEngineeringDto>())
            .Where(item => item.Id.HasValue)
            .ToDictionary(item => item.Id!.Value, item => item.Path);
        var locationPaths = BuildLocationPaths(locations ?? Array.Empty<LocationEngineeringDto>());
        var equipmentPaths = BuildEquipmentPaths(equipment ?? Array.Empty<EquipmentEngineeringDto>());
        _ = BuildPathLookup(locationPaths, "Location");
        _ = BuildPathLookup(equipmentPaths, "Equipment");

        var alarmWithUnresolvedTag = alarms.FirstOrDefault(alarm =>
            alarm.TagId.HasValue &&
            string.IsNullOrWhiteSpace(alarm.TagPath) &&
            !tagPaths.ContainsKey(alarm.TagId.Value));
        if (alarmWithUnresolvedTag is not null)
            throw new InvalidDataException($"Alarm '{alarmWithUnresolvedTag.Name}' references a TAG that cannot be resolved to a readable path.");

        var invalidOperationalTarget = alarms.FirstOrDefault(alarm =>
            alarm.LocationId.HasValue && alarm.EquipmentId.HasValue ||
            alarm.LocationId.HasValue && !locationPaths.ContainsKey(alarm.LocationId.Value) ||
            alarm.EquipmentId.HasValue && !equipmentPaths.ContainsKey(alarm.EquipmentId.Value));
        if (invalidOperationalTarget is not null)
            throw new InvalidDataException($"Alarm '{invalidOperationalTarget.Name}' has an operational target that cannot be resolved to one unique readable path.");

        foreach (var alarm in alarms)
        {
            rows.Add(new[]
            {
                alarm.Name,
                !string.IsNullOrWhiteSpace(alarm.TagPath)
                    ? alarm.TagPath
                    : alarm.TagId.HasValue && tagPaths.TryGetValue(alarm.TagId.Value, out var tagPath) ? tagPath : null,
                alarm.Type.ToString(), alarm.Priority.ToString(),
                CsvCodec.Number(alarm.Setpoint), alarm.DigitalActiveValue.ToString(), alarm.AlarmClass, alarm.Area, alarm.Message,
                alarm.ActivationDelayMilliseconds?.ToString(CultureInfo.InvariantCulture), alarm.RequiresAcknowledgement.ToString(),
                alarm.ShelvingAllowed.ToString(), alarm.Enabled.ToString(), alarm.SoundProfile, JsonMap(alarm.Metadata),
                alarm.LocationId.HasValue ? locationPaths[alarm.LocationId.Value] : null,
                alarm.EquipmentId.HasValue ? equipmentPaths[alarm.EquipmentId.Value] : null
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
            new[] { "Key", "Name", "Driver", "Enabled", "SettingsJson", "SecretReferencesJson", "MetadataJson" }
        };

        foreach (var dataSource in dataSources)
        {
            rows.Add(new[]
            {
                dataSource.Key, dataSource.Name, dataSource.Driver, dataSource.Enabled.ToString(),
                JsonMap(dataSource.Settings), JsonMap(dataSource.SecretReferences), JsonMap(dataSource.Metadata)
            });
        }

        return CsvCodec.Write(rows);
    }

    public string ExportGatewayRoutes(IReadOnlyCollection<GatewayRouteEngineeringDto> routes)
        => ExportGatewayRoutes(routes, []);

    public string ExportGatewayRoutes(
        IReadOnlyCollection<GatewayRouteEngineeringDto> routes,
        IReadOnlyCollection<TagEngineeringDto>? tags)
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            new[]
            {
                "Key", "Name", "SourceTagPath", "DestinationTagPath",
                "TransferMode", "QualityPolicy", "ConversionPolicy", "InitialTransferPolicy", "Gain", "Offset",
                "Deadband", "MinimumIntervalMilliseconds", "PeriodMilliseconds", "Description", "Enabled", "MetadataJson"
            }
        };
        var tagPaths = (tags ?? Array.Empty<TagEngineeringDto>())
            .Where(item => item.Id.HasValue)
            .ToDictionary(item => item.Id!.Value, item => item.Path);

        var routeWithUnresolvedTag = routes.FirstOrDefault(route =>
            (route.SourceTagId.HasValue && string.IsNullOrWhiteSpace(route.SourceTagPath) && !tagPaths.ContainsKey(route.SourceTagId.Value)) ||
            (route.DestinationTagId.HasValue && string.IsNullOrWhiteSpace(route.DestinationTagPath) && !tagPaths.ContainsKey(route.DestinationTagId.Value)));
        if (routeWithUnresolvedTag is not null)
            throw new InvalidDataException($"Gateway route '{routeWithUnresolvedTag.Key}' references a TAG that cannot be resolved to a readable path.");

        foreach (var route in routes)
        {
            rows.Add(new[]
            {
                route.Key, route.Name,
                !string.IsNullOrWhiteSpace(route.SourceTagPath)
                    ? route.SourceTagPath
                    : route.SourceTagId.HasValue && tagPaths.TryGetValue(route.SourceTagId.Value, out var sourcePath) ? sourcePath : null,
                !string.IsNullOrWhiteSpace(route.DestinationTagPath)
                    ? route.DestinationTagPath
                    : route.DestinationTagId.HasValue && tagPaths.TryGetValue(route.DestinationTagId.Value, out var destinationPath) ? destinationPath : null,
                route.TransferMode.ToString(),
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
        => ExportOperationalEvents(events, []);

    public string ExportOperationalEvents(
        IReadOnlyCollection<OperationalEventEngineeringDto> events,
        IReadOnlyCollection<TagEngineeringDto>? tags)
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "Key", "Name", "Type", "Category", "Source", "Area", "EquipmentPath", "TagPath", "Message", "Enabled", "MetadataJson" }
        };
        var tagPaths = (tags ?? Array.Empty<TagEngineeringDto>())
            .Where(item => item.Id.HasValue)
            .ToDictionary(item => item.Id!.Value, item => item.Path);
        var eventWithUnresolvedTag = events.FirstOrDefault(item =>
            item.TagId.HasValue && string.IsNullOrWhiteSpace(item.TagPath) && !tagPaths.ContainsKey(item.TagId.Value));
        if (eventWithUnresolvedTag is not null)
            throw new InvalidDataException($"Operational Event '{eventWithUnresolvedTag.Key}' references a TAG that cannot be resolved to a readable path.");
        foreach (var item in events)
        {
            rows.Add(new[]
            {
                item.Key, item.Name, item.Type, item.Category, item.Source, item.Area,
                item.EquipmentPath,
                !string.IsNullOrWhiteSpace(item.TagPath)
                    ? item.TagPath
                    : item.TagId.HasValue && tagPaths.TryGetValue(item.TagId.Value, out var tagPath) ? tagPath : null,
                item.Message, item.Enabled.ToString(), JsonMap(item.Metadata)
            });
        }
        return CsvCodec.Write(rows);
    }

    public IReadOnlyCollection<TagEngineeringDto> ParseTags(string csv)
        => ParseTagsWithSchema(csv).Tags;

    public ParsedEngineeringTagsCsv ParseTagsWithSchema(
        string csv,
        IReadOnlyCollection<HistorianCaptureProfileEngineeringDto>? historianProfiles = null)
    {
        var table = CsvCodec.ReadTable(csv);
        if (table is null) throw new InvalidDataException("TAG CSV import requires a header row.");
        var header = Header(table.Headers, TagColumns, "Name", "Path", "DataType");
        var hasControlledUnitColumn = header.ContainsKey("ControlledEngineeringUnitJson");
        var markerPresent = header.ContainsKey("ControlledEngineeringUnitContractVersion");
        var rowVersions = table.Rows
            .Select(row => ParseControlledUnitContractVersion(Get(row.Cells, header, "ControlledEngineeringUnitContractVersion")))
            .Select(version => version ?? 1)
            .Distinct()
            .ToArray();
        if (rowVersions.Length > 1)
            throw new InvalidDataException("TAG CSV contains conflicting ControlledEngineeringUnitContractVersion row markers.");

        var writerVersion = rowVersions.Length == 0
            ? markerPresent ? 2 : (int?)null
            : rowVersions.Single();
        if (markerPresent && writerVersion == 2 && !hasControlledUnitColumn)
            throw new InvalidDataException("TAG CSV declares controlled-unit contract version 2 but omits ControlledEngineeringUnitJson.");

        var tags = ParseRows(table, row =>
        {
            var rowWriterVersion = ParseControlledUnitContractVersion(Get(row.Cells, header, "ControlledEngineeringUnitContractVersion")) ?? 1;
            var tag = ParseTag(row.Cells, header, historianProfiles);
            if (tag.ControlledEngineeringUnit?.CatalogVersion == 2 && rowWriterVersion < 2)
                throw new InvalidDataException("Version 2 controlled unit references require ControlledEngineeringUnitContractVersion 2.");
            return tag;
        });

        var schemaVersion = writerVersion == 2
            ? 27
            : hasControlledUnitColumn ? 26 : 25;
        return new ParsedEngineeringTagsCsv(tags, schemaVersion);
    }

    private static int? ParseControlledUnitContractVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) && version is 1 or 2)
            return version;
        throw new InvalidDataException($"Unsupported ControlledEngineeringUnitContractVersion '{value}'. Supported versions are 1 and 2.");
    }

    public IReadOnlyCollection<AlarmEngineeringDto> ParseAlarms(string csv) => ParseAlarms(csv, null, null);

    public IReadOnlyCollection<AlarmEngineeringDto> ParseAlarms(
        string csv,
        IReadOnlyCollection<LocationEngineeringDto>? locations,
        IReadOnlyCollection<EquipmentEngineeringDto>? equipment)
    {
        var table = CsvCodec.ReadTable(csv);
        if (table is null) throw new InvalidDataException("Alarm CSV import requires a header row.");
        var header = Header(table.Headers, AlarmColumns, "Name", "Type", "Priority");
        var locationIdsByPath = locations is null ? null : BuildPathLookup(BuildLocationPaths(locations), "Location");
        var equipmentIdsByPath = equipment is null ? null : BuildPathLookup(BuildEquipmentPaths(equipment), "Equipment");
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
            Null(Get(row.Cells, header, "SoundProfile")),
            ResolveOperationalTargetId(
                Get(row.Cells, header, "LocationPath"),
                Get(row.Cells, header, "LocationId"),
                locationIdsByPath,
                "Location"),
            ResolveOperationalTargetId(
                Get(row.Cells, header, "EquipmentPath"),
                Get(row.Cells, header, "EquipmentId"),
                equipmentIdsByPath,
                "Equipment")));
    }

    public IReadOnlyCollection<DataSourceEngineeringDto> ParseDataSources(string csv)
    {
        var table = CsvCodec.ReadTable(csv);
        if (table is null) throw new InvalidDataException("Data Source CSV import requires a header row.");
        var header = Header(table.Headers, DataSourceColumns, "Key", "Name", "Driver");
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
        var header = Header(table.Headers, GatewayRouteColumns, "Key", "Name");
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
        var header = Header(table.Headers, OperationalEventColumns, "Key", "Name", "Type", "Category", "Source");
        return ParseRows(table, row => new OperationalEventEngineeringDto(
            GuidOrNull(Get(row.Cells, header, "Id")), Get(row.Cells, header, "Key"), Get(row.Cells, header, "Name"),
            Get(row.Cells, header, "Type"), Get(row.Cells, header, "Category"), Get(row.Cells, header, "Source"),
            Null(Get(row.Cells, header, "Area")), Null(Get(row.Cells, header, "EquipmentPath")),
            GuidOrNull(Get(row.Cells, header, "TagId")), Null(Get(row.Cells, header, "TagPath")),
            Null(Get(row.Cells, header, "Message")), Bool(Get(row.Cells, header, "Enabled"), true),
            ParseMap(Get(row.Cells, header, "MetadataJson"))));
    }

    private TagEngineeringDto ParseTag(
        string[] row,
        Dictionary<string, int> header,
        IReadOnlyCollection<HistorianCaptureProfileEngineeringDto>? historianProfiles)
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

        var sourceKey = Null(Get(row, header, "DataSourceKey")) ?? Null(Get(row, header, "Source"));
        var historianProfileKey = Null(Get(row, header, "HistorianCaptureProfileKey"));
        Guid? historianProfileId = GuidOrNull(Get(row, header, "HistorianCaptureProfileId"));
        if (historianProfileKey is not null)
        {
            var profile = (historianProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>())
                .FirstOrDefault(item => item.Key.Equals(historianProfileKey, StringComparison.OrdinalIgnoreCase));
            if (profile?.Id is null)
                throw new InvalidDataException($"Historian capture profile key '{historianProfileKey}' was not found. Select an existing profile key.");
            historianProfileId = profile.Id;
        }

        return new TagEngineeringDto(
            GuidOrNull(Get(row, header, "Id")),
            Get(row, header, "Name"),
            Get(row, header, "Path"),
            EnumValue<TagDataType>(Get(row, header, "DataType")),
            sourceKey,
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
            historianProfileId,
            ParseControlledUnit(Null(Get(row, header, "ControlledEngineeringUnitJson"))));
    }

    private string? JsonMap(IReadOnlyDictionary<string, string>? map) =>
        map is null || map.Count == 0 ? null : JsonSerializer.Serialize(map, _json);

    private string? JsonList(IReadOnlyCollection<string>? values) =>
        values is null ? null : JsonSerializer.Serialize(values, _json);

    private string? JsonCommunicationBinding(CommunicationTagBinding? binding) =>
        binding is null ? null : JsonSerializer.Serialize(binding, _json);

    private string? JsonControlledUnit(ControlledEngineeringUnitReference? reference) =>
        reference is null ? null : JsonSerializer.Serialize(reference, _json);

    private ControlledEngineeringUnitReference? ParseControlledUnit(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<ControlledEngineeringUnitReference>(json, _json)
                ?? throw new InvalidDataException("Invalid ControlledEngineeringUnit JSON in TAG engineering CSV.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Invalid ControlledEngineeringUnit JSON in TAG engineering CSV.", ex);
        }
    }

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

    private static Dictionary<Guid, string> BuildLocationPaths(IReadOnlyCollection<LocationEngineeringDto> source)
    {
        var locations = new Dictionary<Guid, LocationEngineeringDto>();
        foreach (var location in source)
        {
            if (!location.Id.HasValue) continue;
            if (location.Id.Value == Guid.Empty || !locations.TryAdd(location.Id.Value, location))
                throw new InvalidDataException("Operational Locations contain an invalid or duplicate identity.");
        }

        var result = new Dictionary<Guid, string>();
        foreach (var (id, location) in locations)
        {
            var names = new List<string> { location.Name };
            var visited = new HashSet<Guid> { id };
            var parentId = location.ParentLocationId;
            while (parentId.HasValue)
            {
                if (!visited.Add(parentId.Value))
                    throw new InvalidDataException("Operational Location hierarchy contains a cycle.");
                if (!locations.TryGetValue(parentId.Value, out var parent))
                    throw new InvalidDataException("Operational Location hierarchy has a missing parent.");
                names.Add(parent.Name);
                parentId = parent.ParentLocationId;
            }

            names.Reverse();
            result.Add(id, string.Join(" / ", names));
        }

        return result;
    }

    private static Dictionary<Guid, string> BuildEquipmentPaths(IReadOnlyCollection<EquipmentEngineeringDto> source)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var equipment in source)
        {
            if (!equipment.Id.HasValue) continue;
            if (equipment.Id.Value == Guid.Empty || string.IsNullOrWhiteSpace(equipment.Path) ||
                !result.TryAdd(equipment.Id.Value, equipment.Path))
                throw new InvalidDataException("Equipment contains an invalid or duplicate stable identity or path.");
        }
        return result;
    }

    private static Dictionary<string, Guid> BuildPathLookup(
        IReadOnlyDictionary<Guid, string> pathsById,
        string targetName)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, path) in pathsById)
        {
            var key = path.Trim();
            if (string.IsNullOrWhiteSpace(key) || !result.TryAdd(key, id))
                throw new InvalidDataException($"{targetName} paths are empty or ambiguous; use a unique readable path.");
        }
        return result;
    }

    private static Guid? ResolveOperationalTargetId(
        string? pathValue,
        string? legacyIdValue,
        IReadOnlyDictionary<string, Guid>? idsByPath,
        string targetName)
    {
        var path = Null(pathValue);
        var legacyId = GuidOrNull(legacyIdValue);
        Guid? resolvedId = null;
        if (path is not null)
        {
            if (idsByPath is null)
                throw new InvalidDataException($"{targetName} path '{path}' requires the active project references to be loaded before import.");
            if (!idsByPath.TryGetValue(path.Trim(), out var id))
                throw new InvalidDataException($"{targetName} path '{path}' was not found uniquely in the current project.");
            resolvedId = id;
        }

        if (legacyId.HasValue && resolvedId.HasValue && legacyId != resolvedId)
            throw new InvalidDataException($"{targetName} path and legacy identity refer to different targets.");
        return resolvedId ?? legacyId;
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

public sealed record ParsedEngineeringTagsCsv(
    IReadOnlyCollection<TagEngineeringDto> Tags,
    int SchemaVersion);
