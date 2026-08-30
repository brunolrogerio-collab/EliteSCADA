using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

/// <summary>
/// Library-independent Siemens S7 runtime plan aligned to the Coordinator-owned
/// communication runtime composition seam. No socket, ISO session or negotiated
/// PDU client object is stored here.
/// </summary>
public sealed record S7IsoRuntimePlan(
    string DataSourceKey,
    string Name,
    S7IsoConnectionOptions Options,
    IReadOnlyCollection<S7IsoPoint> Points)
{
    public string DriverType => S7IsoRuntimePlanner.DriverTypeKey;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

public sealed record S7IsoRuntimePlanningResult(
    S7IsoRuntimePlan? Plan,
    IReadOnlyCollection<EngineeringDriverIssue> Issues)
{
    public bool CanActivate => Plan is not null && Issues.All(issue => !issue.IsError);
}

/// <summary>
/// Branch-local Siemens planner core. Once the common runtime composition seam is
/// reconciled into this branch, the host adapter can implement
/// ICommunicationDriverRuntimePlanner by delegating to this class without moving
/// any Siemens parsing or validation into the central compiler.
/// </summary>
public sealed class S7IsoRuntimePlanner
{
    public const string DriverTypeKey = "siemens.s7.iso";

    public string DriverType => DriverTypeKey;

    public S7IsoRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);

        var issues = new List<EngineeringDriverIssue>();
        if (!string.Equals(dataSource.Driver, DriverTypeKey, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "S7_DATASOURCE_DRIVER_MISMATCH",
                $"Data source '{dataSource.Key}' declares driver '{dataSource.Driver}', not '{DriverTypeKey}'.",
                dataSource.Key));
            return new S7IsoRuntimePlanningResult(null, issues);
        }

        if (string.IsNullOrWhiteSpace(dataSource.Key))
            issues.Add(Error("S7_DATASOURCE_KEY_REQUIRED", "Siemens S7 data source key is required.", dataSource.Key));
        if (string.IsNullOrWhiteSpace(dataSource.Name))
            issues.Add(Error("S7_DATASOURCE_NAME_REQUIRED", "Siemens S7 data source name is required.", dataSource.Key));

        var settings = dataSource.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        S7IsoConnectionOptions? options = null;
        if (!S7IsoRuntimeConfiguration.TryCreateOptions(settings, out options, out var configurationIssues))
        {
            foreach (var issue in configurationIssues)
                issues.Add(MapConfigurationIssue(dataSource.Key, issue));
        }
        else
        {
            foreach (var issue in configurationIssues)
                issues.Add(MapConfigurationIssue(dataSource.Key, issue));
        }

        if ((dataSource.SecretReferences?.Count ?? 0) > 0)
        {
            issues.Add(new EngineeringDriverIssue(
                "S7_SECRET_REFERENCES_UNUSED",
                $"Siemens S7 data source '{dataSource.Key}' currently has no runtime secret material; configured SecretReferences are not resolved or consumed.",
                dataSource.Key,
                IsError: false));
        }

        var sourceTags = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sourceTags.Length == 0)
        {
            issues.Add(Error(
                "S7_DATASOURCE_NO_TAGS",
                $"Enabled Siemens S7 data source '{dataSource.Key}' has no associated TAGs and cannot create an S7 runtime.",
                dataSource.Key));
        }

        var points = new List<S7IsoPoint>();
        foreach (var dto in sourceTags)
        {
            if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
            {
                issues.Add(Error(
                    "S7_TAG_STABLE_ID_REQUIRED",
                    $"Siemens S7 TAG '{dto.Path}' requires a stable non-empty ID before runtime activation.",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            if (dto.AddressSelector is not null)
            {
                issues.Add(Error(
                    "S7_TAG_ADDRESS_SELECTOR_NOT_IMPLEMENTED",
                    $"Siemens S7 TAG '{dto.Path}' uses a generic AddressSelector that is not yet applied by the S7 runtime. Protocol-native BOOL bit addressing must remain in the S7 physical binding.",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            if (!S7IsoTagBinding.TryParsePortableAddress(dto.Address, out var binding, out var bindingError))
            {
                issues.Add(Error(
                    "S7_TAG_BINDING_INVALID",
                    $"Siemens S7 TAG '{dto.Path}' has an invalid binding: {bindingError}",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            try
            {
                var tag = BuildTagDefinition(dto);
                points.Add(binding!.ToPoint(tag));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                issues.Add(Error(
                    "S7_TAG_RUNTIME_INVALID",
                    $"Siemens S7 TAG '{dto.Path}' cannot be compiled for runtime: {ex.Message}",
                    dataSource.Key,
                    dto.Path));
            }
        }

        var duplicateId = points
            .GroupBy(point => point.Tag.Id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            issues.Add(Error(
                "S7_TAG_ID_DUPLICATE",
                $"Siemens S7 data source '{dataSource.Key}' contains duplicate stable TAG ID '{duplicateId.Key}'.",
                dataSource.Key));
        }

        if (options is null || issues.Any(issue => issue.IsError))
            return new S7IsoRuntimePlanningResult(null, issues);

        return new S7IsoRuntimePlanningResult(
            new S7IsoRuntimePlan(dataSource.Key, dataSource.Name, options, points.ToArray()),
            issues);
    }

    private static TagDefinition BuildTagDefinition(TagEngineeringDto dto)
    {
        var metadata = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(dto.Address)) metadata["address"] = dto.Address;
        if (dto.ScaleMinimum.HasValue)
            metadata["scale.minimum"] = dto.ScaleMinimum.Value.ToString(CultureInfo.InvariantCulture);
        if (dto.ScaleMaximum.HasValue)
            metadata["scale.maximum"] = dto.ScaleMaximum.Value.ToString(CultureInfo.InvariantCulture);
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
            dto.Id!.Value,
            dto.Name,
            dto.Path,
            dto.DataType,
            dto.Source,
            dto.EngineeringUnit,
            dto.Description,
            dto.ReadOnly,
            metadata,
            access,
            dto.AddressSelector);
    }

    private static EngineeringDriverIssue MapConfigurationIssue(
        string dataSourceKey,
        DriverEngineeringIssue issue) =>
        new(
            issue.Code,
            issue.Message,
            dataSourceKey,
            IsError: issue.Severity == DriverEngineeringIssueSeverity.Error);

    private static EngineeringDriverIssue Error(
        string code,
        string message,
        string dataSourceKey,
        string? tagPath = null) =>
        new(code, message, dataSourceKey, tagPath);

    private static void Set(Dictionary<string, string> target, string key, double? value)
    {
        if (value.HasValue) target[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static void Set(Dictionary<string, string> target, string key, int? value)
    {
        if (value.HasValue) target[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Branch-local Siemens factory core. It performs no Engineering parsing and no
/// protected-material resolution. The current S7 runtime has no secret material;
/// a future common ICommunicationDriverRuntimeFactory adapter can pass the
/// host-owned cache/registry from CommunicationDriverRuntimeServices directly.
/// </summary>
public sealed class S7IsoRuntimeFactory
{
    public string DriverType => S7IsoRuntimePlanner.DriverTypeKey;

    public ICommunicationDriver Create(
        S7IsoRuntimePlan plan,
        ICurrentTagCache cache,
        ITagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);

        if (!string.Equals(plan.DriverType, DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"S7 runtime plan DriverType '{plan.DriverType}' does not match factory type '{DriverType}'.", nameof(plan));
        if (string.IsNullOrWhiteSpace(plan.DataSourceKey))
            throw new ArgumentException("S7 runtime plan DataSourceKey is required.", nameof(plan));
        if (string.IsNullOrWhiteSpace(plan.Name))
            throw new ArgumentException("S7 runtime plan Name is required.", nameof(plan));
        if (plan.Points.Count == 0)
            throw new ArgumentException("S7 runtime plan requires at least one point.", nameof(plan));

        return new S7IsoDriver(
            plan.DataSourceKey,
            plan.Name,
            plan.Options,
            cache,
            registry,
            plan.Points);
    }
}
