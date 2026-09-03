using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Modbus;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record EngineeringDriverIssue(
    string Code,
    string Message,
    string DataSourceKey,
    string? TagPath = null,
    bool IsError = true);

public sealed record ModbusTcpRuntimePlan(
    string DataSourceKey,
    string Name,
    string Host,
    int Port,
    TimeSpan ScanRate,
    TimeSpan RequestTimeout,
    int MaxGapElements,
    IReadOnlyCollection<ModbusPoint> Points);

public sealed record EngineeringDriverCompilation(
    IReadOnlyCollection<ModbusTcpRuntimePlan> ModbusTcpPlans,
    IReadOnlyCollection<EngineeringDriverIssue> Issues)
{
    public IReadOnlyCollection<ICommunicationDriverRuntimePlan> CommunicationPlans { get; init; } =
        Array.Empty<ICommunicationDriverRuntimePlan>();

    public bool CanActivate => Issues.All(x => !x.IsError);
}

public interface IEngineeringDriverCompiler
{
    EngineeringDriverCompilation Compile(EngineeringPackage package);
}

public sealed class EngineeringDriverCompiler : IEngineeringDriverCompiler
{
    public const string SimulationDriverKey = "builtin.simulation";
    public const string ModbusTcpDriverKey = "modbus.tcp";

    private readonly CommunicationDriverRuntimeComponentRegistry _communicationComponents;

    public EngineeringDriverCompiler(
        CommunicationDriverRuntimeComponentRegistry? communicationComponents = null)
    {
        _communicationComponents = communicationComponents
            ?? CommunicationDriverRuntimeComposition.BuildForCurrentSchema();
    }

    public EngineeringDriverCompilation Compile(EngineeringPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var plans = new List<ModbusTcpRuntimePlan>();
        var communicationPlans = new List<ICommunicationDriverRuntimePlan>();
        var issues = new List<EngineeringDriverIssue>();
        var dataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>();

        foreach (var dataSource in dataSources.Where(x => x.Enabled))
        {
            if (dataSource.Driver.Equals(SimulationDriverKey, StringComparison.OrdinalIgnoreCase))
                continue;

            if (dataSource.Driver.Equals(ModbusTcpDriverKey, StringComparison.OrdinalIgnoreCase))
            {
                CompileModbusTcp(package, dataSource, plans, issues);
                continue;
            }

            if (_communicationComponents.TryGet(dataSource.Driver, out var registration) && registration is not null)
            {
                var result = registration.Planner.Plan(package, dataSource);
                issues.AddRange(result.Issues);
                if (result.CanActivate && result.Plan is not null)
                    communicationPlans.Add(result.Plan);
                continue;
            }

            issues.Add(new(
                "DRIVER_UNSUPPORTED",
                $"Enabled data source '{dataSource.Key}' uses unsupported runtime driver '{dataSource.Driver}'.",
                dataSource.Key));
        }

        return new EngineeringDriverCompilation(plans, issues)
        {
            CommunicationPlans = communicationPlans
        };
    }

    private static void CompileModbusTcp(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource,
        List<ModbusTcpRuntimePlan> plans,
        List<EngineeringDriverIssue> issues)
    {
        var settings = dataSource.Settings ?? new Dictionary<string, string>();
        var host = Get(settings, "host");
        if (string.IsNullOrWhiteSpace(host))
            issues.Add(Error("MODBUS_HOST_REQUIRED", "Modbus TCP setting 'host' is required.", dataSource.Key));

        var port = ParseInt(settings, "port", 502, 1, 65535, dataSource.Key, issues);
        var scanMs = ParseInt(settings, "scanIntervalMilliseconds", 1000, 10, 600_000, dataSource.Key, issues);
        var timeoutMs = ParseInt(settings, "requestTimeoutMilliseconds", 3000, 50, 60_000, dataSource.Key, issues);
        var maxGap = ParseInt(settings, "maxGapElements", 8, 0, 125, dataSource.Key, issues);
        var defaultUnitId = ParseInt(settings, "unitId", 1, 0, 255, dataSource.Key, issues);

        var sourceTags = package.Tags
            .Where(tag => IsAssociatedWithDataSource(tag, dataSource))
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sourceTags.Length == 0)
        {
            issues.Add(new(
                "MODBUS_DATASOURCE_NO_TAGS",
                $"Enabled Modbus TCP data source '{dataSource.Key}' has no associated TAGs.",
                dataSource.Key,
                IsError: false));
        }

        var points = new List<ModbusPoint>();
        foreach (var tag in sourceTags)
        {
            var before = issues.Count;
            var point = CompilePoint(dataSource.Key, tag, defaultUnitId, issues);
            if (point is not null && issues.Skip(before).All(x => !x.IsError))
                points.Add(point);
        }

        if (issues.Any(x => x.IsError && x.DataSourceKey.Equals(dataSource.Key, StringComparison.OrdinalIgnoreCase)))
            return;

        plans.Add(new ModbusTcpRuntimePlan(
            dataSource.Key,
            dataSource.Name,
            host!,
            port,
            TimeSpan.FromMilliseconds(scanMs),
            TimeSpan.FromMilliseconds(timeoutMs),
            maxGap,
            points));
    }

    private static bool IsAssociatedWithDataSource(TagEngineeringDto tag, DataSourceEngineeringDto dataSource)
    {
        if (tag.DataSourceId.HasValue)
            return dataSource.Id.HasValue && tag.DataSourceId.Value == dataSource.Id.Value;
        return string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase);
    }

    private static ModbusPoint? CompilePoint(
        string dataSourceKey,
        TagEngineeringDto dto,
        int defaultUnitId,
        List<EngineeringDriverIssue> issues)
    {
        if (!ModbusTagAddressCodec.TryParse(dto.Address, dto.Metadata, out var area, out var address, out var addressError))
        {
            issues.Add(Error("MODBUS_TAG_ADDRESS_INVALID", addressError!, dataSourceKey, dto.Path));
            return null;
        }

        var metadata = dto.Metadata ?? new Dictionary<string, string>();
        var unitId = ParseInt(metadata, "modbus.unitId", defaultUnitId, 0, 255, dataSourceKey, issues, dto.Path);
        var valueType = ParseValueType(metadata, dto, dataSourceKey, issues);
        var wordOrder = ParseWordOrder(metadata, dataSourceKey, dto.Path, issues);
        var scale = ParseDouble(metadata, "modbus.scale", 1d, dataSourceKey, issues, dto.Path, nonZero: true);
        var offset = ParseDouble(metadata, "modbus.offset", 0d, dataSourceKey, issues, dto.Path);

        if (valueType is null || wordOrder is null) return null;

        var tag = BuildTagDefinition(dto);
        try
        {
            var point = new ModbusPoint(
                tag,
                checked((byte)unitId),
                area,
                address,
                valueType.Value,
                Writable: !dto.ReadOnly,
                WordOrder: wordOrder.Value,
                Scale: scale,
                Offset: offset);
            point.Validate();
            return point;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            issues.Add(Error("MODBUS_TAG_CONFIGURATION_INVALID", ex.Message, dataSourceKey, dto.Path));
            return null;
        }
    }

    private static TagDefinition BuildTagDefinition(TagEngineeringDto dto)
    {
        var metadata = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(dto.Address)) metadata["address"] = dto.Address;
        if (dto.ScaleMinimum.HasValue) metadata["scale.minimum"] = dto.ScaleMinimum.Value.ToString(CultureInfo.InvariantCulture);
        if (dto.ScaleMaximum.HasValue) metadata["scale.maximum"] = dto.ScaleMaximum.Value.ToString(CultureInfo.InvariantCulture);
        if (dto.Historian is not null)
        {
            metadata["historian.enabled"] = dto.Historian.Enabled.ToString();
            metadata["historian.strategy"] = dto.Historian.Strategy;
            Set(metadata, "historian.deadband", dto.Historian.Deadband);
            Set(metadata, "historian.periodMs", dto.Historian.PeriodMilliseconds);
            Set(metadata, "historian.maxPeriodMs", dto.Historian.MaximumPeriodMilliseconds);
        }

        var access = dto.AccessPolicy is null
            ? null
            : new TagAccessPolicy(
                dto.AccessPolicy.ReadRoles?.ToArray(),
                dto.AccessPolicy.WriteRoles?.ToArray(),
                dto.AccessPolicy.ConfigureRoles?.ToArray());

        return new TagDefinition(
            dto.Id ?? Guid.NewGuid(),
            dto.Name,
            dto.Path,
            dto.DataType,
            dto.Source,
            dto.EngineeringUnit,
            dto.Description,
            dto.ReadOnly,
            metadata,
            access,
            dto.AddressSelector,
            dto.CommunicationBinding,
            dto.DataSourceId);
    }

    private static ModbusValueType? ParseValueType(
        IReadOnlyDictionary<string, string> metadata,
        TagEngineeringDto tag,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues)
    {
        var raw = Meta(metadata, "modbus.valueType");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return tag.DataType switch
            {
                TagDataType.Boolean => ModbusValueType.Boolean,
                TagDataType.Int16 => ModbusValueType.Int16,
                TagDataType.Int32 => ModbusValueType.Int32,
                TagDataType.Int64 => ModbusValueType.Int64,
                TagDataType.Float => ModbusValueType.Float32,
                TagDataType.Double => ModbusValueType.Float64,
                _ => AddValueTypeError(tag, dataSourceKey, issues)
            };
        }

        var normalized = raw.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<ModbusValueType>(normalized, ignoreCase: true, out var parsed)) return parsed;
        issues.Add(Error(
            "MODBUS_VALUE_TYPE_INVALID",
            $"TAG '{tag.Path}' has unsupported modbus.valueType '{raw}'.",
            dataSourceKey,
            tag.Path));
        return null;
    }

    private static ModbusValueType? AddValueTypeError(
        TagEngineeringDto tag,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues)
    {
        issues.Add(Error(
            "MODBUS_VALUE_TYPE_REQUIRED",
            $"TAG '{tag.Path}' data type '{tag.DataType}' cannot infer a Modbus value type. Set metadata 'modbus.valueType'.",
            dataSourceKey,
            tag.Path));
        return null;
    }

    private static ModbusWordOrder? ParseWordOrder(
        IReadOnlyDictionary<string, string> metadata,
        string dataSourceKey,
        string tagPath,
        List<EngineeringDriverIssue> issues)
    {
        var raw = Meta(metadata, "modbus.wordOrder");
        if (string.IsNullOrWhiteSpace(raw)) return ModbusWordOrder.HighWordFirst;
        var normalized = raw.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<ModbusWordOrder>(normalized, ignoreCase: true, out var parsed)) return parsed;
        issues.Add(Error(
            "MODBUS_WORD_ORDER_INVALID",
            $"TAG '{tagPath}' has unsupported modbus.wordOrder '{raw}'. Use HighWordFirst or LowWordFirst.",
            dataSourceKey,
            tagPath));
        return null;
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> map,
        string key,
        int fallback,
        int minimum,
        int maximum,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues,
        string? tagPath = null)
    {
        var raw = Meta(map, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum && parsed <= maximum)
            return parsed;
        issues.Add(Error(
            "MODBUS_SETTING_INVALID",
            $"Setting '{key}' must be an integer from {minimum} to {maximum}; received '{raw}'.",
            dataSourceKey,
            tagPath));
        return fallback;
    }

    private static double ParseDouble(
        IReadOnlyDictionary<string, string> map,
        string key,
        double fallback,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues,
        string tagPath,
        bool nonZero = false)
    {
        var raw = Meta(map, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            double.IsFinite(parsed) && (!nonZero || parsed != 0d))
            return parsed;
        issues.Add(Error(
            "MODBUS_SETTING_INVALID",
            $"TAG '{tagPath}' metadata '{key}' must be a finite{(nonZero ? " non-zero" : string.Empty)} number; received '{raw}'.",
            dataSourceKey,
            tagPath));
        return fallback;
    }

    private static string? Get(IReadOnlyDictionary<string, string> map, string key) => Meta(map, key)?.Trim();

    private static string? Meta(IReadOnlyDictionary<string, string>? map, string key) =>
        map is not null && map.TryGetValue(key, out var value) ? value : null;

    private static void Set(Dictionary<string, string> map, string key, object? value)
    {
        if (value is not null) map[key] = Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }

    private static EngineeringDriverIssue Error(string code, string message, string dataSourceKey, string? tagPath = null) =>
        new(code, message, dataSourceKey, tagPath, true);
}
