using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Simulation;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record SimulationCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    IReadOnlyCollection<SimulationPoint> Points,
    TimeSpan ScanRate) : ICommunicationDriverRuntimePlan
{
    public string DriverType => SimulationDriverDescriptorProvider.DriverTypeId;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

public sealed class SimulationCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public string DriverType => SimulationDriverDescriptorProvider.DriverTypeId;

    public CommunicationDriverRuntimePlanningResult Plan(EngineeringPackage package, DataSourceEngineeringDto dataSource)
    {
        var issues = new List<EngineeringDriverIssue>();
        var settings = dataSource.Settings ?? new Dictionary<string, string>();
        var scanMilliseconds = ReadInt(settings, "scanIntervalMilliseconds", 500, 10, 600_000, dataSource.Key, issues);
        var points = new List<SimulationPoint>();
        var tags = package.Tags.Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase));

        foreach (var dto in tags)
        {
            if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
            {
                issues.Add(new("SIMULATION_TAG_ID_REQUIRED", $"Simulation TAG '{dto.Path}' requires a stable ID.", dataSource.Key, dto.Path));
                continue;
            }

            var metadata = dto.Metadata ?? new Dictionary<string, string>();
            var signalRaw = Read(metadata, "simulation.signalType") ?? DefaultSignal(dto.DataType);
            if (!Enum.TryParse<SimulationSignalType>(signalRaw, true, out var signal) || !Enum.IsDefined(signal))
            {
                issues.Add(new("SIMULATION_SIGNAL_INVALID", $"TAG '{dto.Path}' has unsupported simulation signal '{signalRaw}'.", dataSource.Key, dto.Path));
                continue;
            }
            if (signal == SimulationSignalType.CurrentTime && dto.DataType != TagDataType.DateTime)
            {
                issues.Add(new("SIMULATION_CURRENT_TIME_REQUIRES_DATETIME", $"TAG '{dto.Path}' must use DateTime for the Current time simulation.", dataSource.Key, dto.Path));
                continue;
            }
            if (signal != SimulationSignalType.CurrentTime && dto.DataType == TagDataType.DateTime)
            {
                issues.Add(new("SIMULATION_DATETIME_SIGNAL_INVALID", $"DateTime TAG '{dto.Path}' must use CurrentTime simulation.", dataSource.Key, dto.Path));
                continue;
            }

            var minimum = ReadDouble(metadata, "simulation.minimum", 0, dataSource.Key, dto.Path, issues);
            var maximum = ReadDouble(metadata, "simulation.maximum", 100, dataSource.Key, dto.Path, issues);
            var period = ReadDouble(metadata, "simulation.periodSeconds", 10, dataSource.Key, dto.Path, issues);
            var constant = ReadDouble(metadata, "simulation.constantValue", 0, dataSource.Key, dto.Path, issues);
            var step = ReadDouble(metadata, "simulation.step", 1, dataSource.Key, dto.Path, issues);
            if (maximum < minimum)
                issues.Add(new("SIMULATION_RANGE_INVALID", $"TAG '{dto.Path}' maximum must be greater than or equal to minimum.", dataSource.Key, dto.Path));
            if (period <= 0)
                issues.Add(new("SIMULATION_PERIOD_INVALID", $"TAG '{dto.Path}' period must be greater than zero.", dataSource.Key, dto.Path));
            if (issues.Any(issue => issue.IsError && issue.DataSourceKey == dataSource.Key && issue.TagPath == dto.Path)) continue;

            var access = dto.AccessPolicy is null ? null : new TagAccessPolicy(
                dto.AccessPolicy.ReadRoles?.ToArray(), dto.AccessPolicy.WriteRoles?.ToArray(), dto.AccessPolicy.ConfigureRoles?.ToArray());
            var tag = new TagDefinition(dto.Id.Value, dto.Name, dto.Path, dto.DataType, dto.Source, dto.EngineeringUnit,
                dto.Description, dto.ReadOnly, metadata, access, dto.AddressSelector, dto.CommunicationBinding, dto.DataSourceId);
            points.Add(new SimulationPoint(tag, signal, minimum, maximum, period, constant, step));
        }

        if (points.Count == 0 && !issues.Any(issue => issue.IsError))
            issues.Add(new("SIMULATION_DATASOURCE_NO_TAGS", $"Enabled Simulation data source '{dataSource.Key}' has no associated TAGs.", dataSource.Key, IsError: false));

        var plan = issues.Any(issue => issue.IsError) ? null : new SimulationCommunicationRuntimePlan(
            dataSource.Key, dataSource.Name, points, TimeSpan.FromMilliseconds(scanMilliseconds));
        return new CommunicationDriverRuntimePlanningResult(plan, issues);
    }

    private static string DefaultSignal(TagDataType dataType) => dataType switch
    {
        TagDataType.Boolean => nameof(SimulationSignalType.BooleanToggle),
        TagDataType.DateTime => nameof(SimulationSignalType.CurrentTime),
        _ => nameof(SimulationSignalType.Sine)
    };

    private static string? Read(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static int ReadInt(IReadOnlyDictionary<string, string> values, string key, int fallback, int min, int max,
        string source, List<EngineeringDriverIssue> issues)
    {
        if (!values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max) return value;
        issues.Add(new("SIMULATION_SETTING_INVALID", $"Setting '{key}' must be an integer between {min} and {max}.", source));
        return fallback;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, string> values, string key, double fallback,
        string source, string path, List<EngineeringDriverIssue> issues)
    {
        if (!values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)) return value;
        issues.Add(new("SIMULATION_SETTING_INVALID", $"TAG '{path}' setting '{key}' must be a finite number.", source, path));
        return fallback;
    }
}

public sealed class SimulationCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    public string DriverType => SimulationDriverDescriptorProvider.DriverTypeId;

    public ICommunicationDriver Create(ICommunicationDriverRuntimePlan plan, CommunicationDriverRuntimeServices services)
    {
        services.Validate();
        if (plan is not SimulationCommunicationRuntimePlan simulation)
            throw new ArgumentException("The runtime plan is not a Simulation plan.", nameof(plan));
        return new SimulationDriver(services.Cache, services.Registry, simulation.Points, simulation.ScanRate);
    }
}
