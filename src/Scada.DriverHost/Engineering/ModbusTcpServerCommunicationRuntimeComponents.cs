using System.Globalization;
using Scada.Drivers.Abstractions;
using Scada.Core.Tags;
using Scada.Drivers.Modbus;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record ModbusTcpServerCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    string BindAddress,
    int Port,
    byte UnitId,
    IReadOnlyCollection<ModbusHoldingRegisterRange> Ranges,
    IReadOnlyCollection<ModbusServerPoint> Points,
    int MaxClients,
    TimeSpan ClientIdleTimeout) : ICommunicationDriverRuntimePlan
{
    public const string DriverTypeKey = ModbusTcpServerDriverDescriptorProvider.DriverTypeId;
    public string DriverType => DriverTypeKey;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Point.Tag).ToArray();
}

public sealed class ModbusTcpServerCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public string DriverType => ModbusTcpServerCommunicationRuntimePlan.DriverTypeKey;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);

        var issues = new List<EngineeringDriverIssue>();
        var settings = dataSource.Settings ?? new Dictionary<string, string>();
        var bindAddress = Get(settings, "bindAddress") ?? "0.0.0.0";
        var port = ParseInt(settings, "port", 502, 1, 65535, dataSource.Key, issues);
        var unitId = ParseInt(settings, "unitId", 1, 0, 247, dataSource.Key, issues);
        var maxClients = ParseInt(settings, "maxClients", 16, 1, 128, dataSource.Key, issues);
        var idleMs = ParseInt(settings, "clientIdleTimeoutMilliseconds", 30000, 1000, 600000, dataSource.Key, issues);
        var rawRanges = Get(settings, "holdingRanges") ?? "v1:0-999";

        IReadOnlyCollection<ModbusHoldingRegisterRange> ranges = Array.Empty<ModbusHoldingRegisterRange>();
        if (!ModbusServerRangeCodec.TryParse(rawRanges, out ranges, out var rangeError))
            issues.Add(Error("MODBUS_SERVER_RANGES_INVALID", rangeError ?? "Holding Register ranges are invalid.", dataSource.Key));

        try
        {
            ModbusTcpServerDriver.NormalizeBindAddress(bindAddress);
        }
        catch (ArgumentException ex)
        {
            issues.Add(Error("MODBUS_TCP_SERVER_BIND_ADDRESS_INVALID", ex.Message, dataSource.Key));
        }

        var sourceTags = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceTags.Length == 0)
        {
            issues.Add(new EngineeringDriverIssue(
                "MODBUS_TCP_SERVER_NO_TAGS",
                $"Enabled Modbus TCP Server data source '{dataSource.Key}' has no associated TAGs and will not create a listener.",
                dataSource.Key,
                IsError: false));
        }

        var serverPoints = new List<ModbusServerPoint>();
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

            var access = ParseClientAccess(tag.Metadata, dataSource.Key, tag.Path, issues);
            if (!access.HasValue) continue;
            serverPoints.Add(new ModbusServerPoint(point, access.Value));
        }

        if (!issues.Any(issue => issue.IsError))
        {
            try
            {
                _ = new ModbusServerRegisterMap(ranges, serverPoints);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                issues.Add(Error("MODBUS_SERVER_REGISTER_MAP_INVALID", ex.Message, dataSource.Key));
            }
        }

        if (issues.Any(issue => issue.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        return new CommunicationDriverRuntimePlanningResult(
            new ModbusTcpServerCommunicationRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                bindAddress,
                port,
                checked((byte)unitId),
                ranges,
                serverPoints,
                maxClients,
                TimeSpan.FromMilliseconds(idleMs)),
            issues);
    }

    private static ModbusServerClientAccess? ParseClientAccess(
        IReadOnlyDictionary<string, string>? metadata,
        string dataSourceKey,
        string tagPath,
        List<EngineeringDriverIssue> issues)
    {
        var raw = metadata is not null && metadata.TryGetValue("modbus.server.clientAccess", out var value)
            ? value
            : "ReadOnly";
        if (Enum.TryParse<ModbusServerClientAccess>(raw, true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;
        issues.Add(Error(
            "MODBUS_SERVER_CLIENT_ACCESS_INVALID",
            $"TAG '{tagPath}' has unsupported client access '{raw}'. Use ReadOnly or ReadWrite.",
            dataSourceKey,
            tagPath));
        return null;
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
            "MODBUS_SERVER_SETTING_INVALID",
            $"Setting '{key}' must be an integer from {minimum} to {maximum}; received '{raw}'.",
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

public sealed class ModbusTcpServerCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    public string DriverType => ModbusTcpServerCommunicationRuntimePlan.DriverTypeKey;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not ModbusTcpServerCommunicationRuntimePlan server)
            throw new ArgumentException($"Expected {nameof(ModbusTcpServerCommunicationRuntimePlan)}.", nameof(plan));

        return new ModbusTcpServerDriver(
            $"modbus.tcp.server:{server.DataSourceKey}",
            server.Name,
            server.BindAddress,
            server.Port,
            server.UnitId,
            server.Ranges,
            server.Points,
            services.Cache,
            services.Registry,
            server.MaxClients,
            server.ClientIdleTimeout,
            services.EffectAuthority);
    }
}
