using System.Globalization;
using Scada.Drivers.Abstractions;
using Scada.Core.Tags;
using Scada.Drivers.Modbus;
using Scada.Drivers.Serial;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record ModbusRtuCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    HostSerialLineSettings SerialSettings,
    TimeSpan ScanRate,
    TimeSpan RequestTimeout,
    int MaxGapElements,
    IReadOnlyCollection<ModbusPoint> Points) : ICommunicationDriverRuntimePlan
{
    public const string DriverTypeKey = ModbusRtuDriverDescriptorProvider.DriverTypeId;
    public string DriverType => DriverTypeKey;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

public sealed class ModbusRtuCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public string DriverType => ModbusRtuCommunicationRuntimePlan.DriverTypeKey;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);

        var issues = new List<EngineeringDriverIssue>();
        var settings = dataSource.Settings ?? new Dictionary<string, string>();
        var port = Get(settings, "serialPort");
        if (string.IsNullOrWhiteSpace(port))
            issues.Add(Error("MODBUS_RTU_SERIAL_PORT_REQUIRED", "Modbus RTU setting 'serialPort' is required.", dataSource.Key));

        var baud = ParseInt(settings, "baudRate", 9600, 300, 4_000_000, dataSource.Key, issues);
        var dataBits = ParseInt(settings, "dataBits", 8, 5, 8, dataSource.Key, issues);
        var parity = ParseEnum(settings, "parity", HostSerialParity.None, dataSource.Key, issues);
        var stopBits = ParseEnum(settings, "stopBits", HostSerialStopBits.One, dataSource.Key, issues);
        var scanMs = ParseInt(settings, "scanIntervalMilliseconds", 1000, 10, 600_000, dataSource.Key, issues);
        var timeoutMs = ParseInt(settings, "requestTimeoutMilliseconds", 3000, 50, 60_000, dataSource.Key, issues);
        var maxGap = ParseInt(settings, "maxGapElements", 8, 0, 125, dataSource.Key, issues);
        var defaultUnitId = ParseInt(settings, "unitId", 1, 1, 247, dataSource.Key, issues);

        var sourceTags = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceTags.Length == 0)
        {
            issues.Add(new EngineeringDriverIssue(
                "MODBUS_RTU_DATASOURCE_NO_TAGS",
                $"Enabled Modbus RTU data source '{dataSource.Key}' has no associated TAGs.",
                dataSource.Key,
                IsError: false));
        }

        var points = new List<ModbusPoint>();
        foreach (var tag in sourceTags)
        {
            var before = issues.Count;
            var point = EngineeringDriverCompiler.CompileModbusPoint(
                dataSource.Key,
                tag,
                defaultUnitId,
                issues);
            if (point is null || issues.Skip(before).Any(issue => issue.IsError))
                continue;
            if (point.UnitId is < 1 or > 247)
            {
                issues.Add(Error(
                    "MODBUS_RTU_UNIT_ID_INVALID",
                    $"TAG '{tag.Path}' Unit ID {point.UnitId} must be from 1 to 247 for Modbus RTU.",
                    dataSource.Key,
                    tag.Path));
                continue;
            }
            points.Add(point);
        }

        if (issues.Any(issue => issue.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        HostSerialLineSettings line;
        try
        {
            line = new HostSerialLineSettings(port!, baud, dataBits, parity, stopBits);
            line.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            issues.Add(Error("MODBUS_RTU_SERIAL_CONFIGURATION_INVALID", ex.Message, dataSource.Key));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }

        return new CommunicationDriverRuntimePlanningResult(
            new ModbusRtuCommunicationRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                line,
                TimeSpan.FromMilliseconds(scanMs),
                TimeSpan.FromMilliseconds(timeoutMs),
                maxGap,
                points),
            issues);
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int fallback,
        int minimum,
        int maximum,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            parsed >= minimum && parsed <= maximum)
            return parsed;
        issues.Add(Error(
            "MODBUS_RTU_SETTING_INVALID",
            $"Setting '{key}' must be an integer from {minimum} to {maximum}; received '{raw}'.",
            dataSourceKey));
        return fallback;
    }

    private static T ParseEnum<T>(
        IReadOnlyDictionary<string, string> settings,
        string key,
        T fallback,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues) where T : struct, Enum
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        var normalized = raw.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<T>(normalized, true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;
        issues.Add(Error(
            "MODBUS_RTU_SETTING_INVALID",
            $"Setting '{key}' has unsupported value '{raw}'.",
            dataSourceKey));
        return fallback;
    }

    private static string? Get(IReadOnlyDictionary<string, string> settings, string key)
    {
        if (settings.TryGetValue(key, out var exact)) return exact?.Trim();
        foreach (var pair in settings)
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                return pair.Value?.Trim();
        return null;
    }

    private static EngineeringDriverIssue Error(string code, string message, string dataSourceKey, string? tagPath = null) =>
        new(code, message, dataSourceKey, tagPath, true);
}

public sealed class ModbusRtuCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly HostSerialBusCoordinator _coordinator;

    public ModbusRtuCommunicationRuntimeFactory(HostSerialBusCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public string DriverType => ModbusRtuCommunicationRuntimePlan.DriverTypeKey;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not ModbusRtuCommunicationRuntimePlan rtu)
            throw new ArgumentException($"Expected {nameof(ModbusRtuCommunicationRuntimePlan)}.", nameof(plan));

        return new ModbusRtuDriver(
            $"modbus.rtu:{rtu.DataSourceKey}",
            rtu.Name,
            _coordinator,
            rtu.SerialSettings,
            services.Cache,
            services.Registry,
            rtu.Points,
            rtu.ScanRate,
            rtu.RequestTimeout,
            rtu.MaxGapElements);
    }
}
