using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Modbus;
using Scada.Drivers.Serial;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record ModbusRtuServerCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    HostSerialLineSettings SerialSettings,
    byte UnitId,
    IReadOnlyCollection<ModbusHoldingRegisterRange> Ranges,
    IReadOnlyCollection<ModbusServerPoint> Points,
    TimeSpan FrameTimeout) : ICommunicationDriverRuntimePlan
{
    public const string DriverTypeKey = ModbusRtuServerDriverDescriptorProvider.DriverTypeId;
    public string DriverType => DriverTypeKey;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Point.Tag).ToArray();
}

public sealed class ModbusRtuServerCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public string DriverType => ModbusRtuServerCommunicationRuntimePlan.DriverTypeKey;

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
            issues.Add(Error("MODBUS_RTU_SERVER_SERIAL_PORT_REQUIRED", "Modbus RTU Server setting 'serialPort' is required.", dataSource.Key));

        var baud = ParseInt(settings, "baudRate", 9600, 300, 4_000_000, dataSource.Key, issues);
        var dataBits = ParseInt(settings, "dataBits", 8, 5, 8, dataSource.Key, issues);
        var parity = ParseEnum(settings, "parity", HostSerialParity.None, dataSource.Key, issues);
        var stopBits = ParseEnum(settings, "stopBits", HostSerialStopBits.One, dataSource.Key, issues);
        var unitId = ParseInt(settings, "unitId", 1, 1, 247, dataSource.Key, issues);
        var frameTimeoutMs = ParseInt(settings, "frameTimeoutMilliseconds", 1000, 50, 10000, dataSource.Key, issues);
        var rawRanges = Get(settings, "holdingRanges") ?? "v1:0-999";

        IReadOnlyCollection<ModbusHoldingRegisterRange> ranges = Array.Empty<ModbusHoldingRegisterRange>();
        if (!ModbusServerRangeCodec.TryParse(rawRanges, out ranges, out var rangeError))
            issues.Add(Error("MODBUS_SERVER_RANGES_INVALID", rangeError ?? "Holding Register ranges are invalid.", dataSource.Key));

        var sourceTags = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceTags.Length == 0)
        {
            issues.Add(new EngineeringDriverIssue(
                "MODBUS_RTU_SERVER_NO_TAGS",
                $"Enabled Modbus RTU Server data source '{dataSource.Key}' has no associated TAGs and will not open the serial server.",
                dataSource.Key,
                IsError: false));
        }

        var points = new List<ModbusServerPoint>();
        foreach (var tag in sourceTags)
        {
            var before = issues.Count;
            var point = EngineeringDriverCompiler.CompileModbusPoint(dataSource.Key, tag, unitId, issues);
            if (point is null || issues.Skip(before).Any(issue => issue.IsError))
                continue;
            if (point.Area != ModbusDataArea.HoldingRegister)
            {
                issues.Add(Error(
                    "MODBUS_SERVER_AREA_UNSUPPORTED",
                    $"TAG '{tag.Path}' uses area '{point.Area}'. Initial Modbus Server scope supports Holding Registers only.",
                    dataSource.Key,
                    tag.Path));
                continue;
            }
            if (point.UnitId != unitId)
            {
                issues.Add(Error(
                    "MODBUS_SERVER_UNIT_ID_MISMATCH",
                    $"TAG '{tag.Path}' Unit ID {point.UnitId} must match server Unit ID {unitId}.",
                    dataSource.Key,
                    tag.Path));
                continue;
            }

            var rawAccess = tag.Metadata is not null && tag.Metadata.TryGetValue("modbus.server.clientAccess", out var accessValue)
                ? accessValue
                : "ReadOnly";
            if (!Enum.TryParse<ModbusServerClientAccess>(rawAccess, true, out var access) || !Enum.IsDefined(access))
            {
                issues.Add(Error(
                    "MODBUS_SERVER_CLIENT_ACCESS_INVALID",
                    $"TAG '{tag.Path}' has unsupported client access '{rawAccess}'. Use ReadOnly or ReadWrite.",
                    dataSource.Key,
                    tag.Path));
                continue;
            }
            points.Add(new ModbusServerPoint(point, access));
        }

        HostSerialLineSettings? line = null;
        if (!issues.Any(issue => issue.IsError))
        {
            try
            {
                line = new HostSerialLineSettings(port!, baud, dataBits, parity, stopBits);
                line.Validate();
                _ = new ModbusServerRegisterMap(ranges, points);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                issues.Add(Error("MODBUS_RTU_SERVER_CONFIGURATION_INVALID", ex.Message, dataSource.Key));
            }
        }

        if (issues.Any(issue => issue.IsError) || line is null)
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        return new CommunicationDriverRuntimePlanningResult(
            new ModbusRtuServerCommunicationRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                line,
                checked((byte)unitId),
                ranges,
                points,
                TimeSpan.FromMilliseconds(frameTimeoutMs)),
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
        issues.Add(Error("MODBUS_RTU_SERVER_SETTING_INVALID", $"Setting '{key}' must be from {minimum} to {maximum}; received '{raw}'.", dataSourceKey));
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
        issues.Add(Error("MODBUS_RTU_SERVER_SETTING_INVALID", $"Setting '{key}' has unsupported value '{raw}'.", dataSourceKey));
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

public sealed class ModbusRtuServerCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly HostSerialBusCoordinator _coordinator;

    public ModbusRtuServerCommunicationRuntimeFactory(HostSerialBusCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public string DriverType => ModbusRtuServerCommunicationRuntimePlan.DriverTypeKey;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not ModbusRtuServerCommunicationRuntimePlan server)
            throw new ArgumentException($"Expected {nameof(ModbusRtuServerCommunicationRuntimePlan)}.", nameof(plan));

        return new ModbusRtuServerDriver(
            $"modbus.rtu.server:{server.DataSourceKey}",
            server.Name,
            _coordinator,
            server.SerialSettings,
            server.UnitId,
            server.Ranges,
            server.Points,
            services.Cache,
            services.Registry,
            server.FrameTimeout);
    }
}
