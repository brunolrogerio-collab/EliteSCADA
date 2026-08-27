using System.Globalization;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.DriverHost.Engineering;

public sealed record InternalMemoryRuntimePlan(
    string DataSourceKey,
    string Name,
    bool IsClientMemory,
    IReadOnlyCollection<MemoryTagDefinition> Tags);

public sealed record InternalMemoryRuntimeCompilation(
    EngineeringPackage CommunicationPackage,
    IReadOnlyCollection<InternalMemoryRuntimePlan> ServerMemoryPlans,
    IReadOnlyCollection<InternalMemoryRuntimePlan> ClientMemoryPlans,
    IReadOnlyCollection<EngineeringDriverIssue> Issues)
{
    public bool CanActivate => Issues.All(x => !x.IsError);
}

public static class InternalMemoryRuntimePlanner
{
    public const string ClientMemoryDriverKey = "builtin.memory.client";
    public const string ServerMemoryDriverKey = "builtin.memory.server";

    public static InternalMemoryRuntimeCompilation Compile(EngineeringPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var issues = new List<EngineeringDriverIssue>();
        var serverPlans = new List<InternalMemoryRuntimePlan>();
        var clientPlans = new List<InternalMemoryRuntimePlan>();
        var dataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>();

        foreach (var dataSource in dataSources.Where(x => x.Enabled && IsMemoryDriver(x.Driver)))
        {
            var definitions = new List<MemoryTagDefinition>();
            var sourceTags = package.Tags
                .Where(x => string.Equals(x.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (sourceTags.Length == 0)
            {
                issues.Add(new EngineeringDriverIssue(
                    "MEMORY_DATASOURCE_NO_TAGS",
                    $"Enabled Internal Memory data source '{dataSource.Key}' has no associated TAGs.",
                    dataSource.Key,
                    IsError: false));
            }

            foreach (var dto in sourceTags)
            {
                if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
                {
                    issues.Add(new EngineeringDriverIssue(
                        "MEMORY_TAG_STABLE_ID_REQUIRED",
                        $"Internal Memory TAG '{dto.Path}' requires a stable non-empty ID before runtime activation.",
                        dataSource.Key,
                        dto.Path));
                    continue;
                }

                try
                {
                    var tag = BuildTagDefinition(dto);
                    var initialValue = dto.InitialValue is null
                        ? TypedTagValue.CreateDefault(dto.DataType)
                        : MemoryEngineeringValueCodec.ToTypedValue(dto.InitialValue);
                    definitions.Add(new MemoryTagDefinition(tag, initialValue));
                }
                catch (Exception ex) when (ex is ArgumentException or ArgumentNullException or InvalidOperationException or FormatException)
                {
                    issues.Add(new EngineeringDriverIssue(
                        "MEMORY_TAG_RUNTIME_INVALID",
                        $"Internal Memory TAG '{dto.Path}' cannot be compiled for runtime: {ex.Message}",
                        dataSource.Key,
                        dto.Path));
                }
            }

            var duplicateId = definitions
                .GroupBy(x => x.Tag.Id)
                .FirstOrDefault(x => x.Count() > 1);
            if (duplicateId is not null)
            {
                issues.Add(new EngineeringDriverIssue(
                    "MEMORY_TAG_ID_DUPLICATE",
                    $"Internal Memory data source '{dataSource.Key}' contains duplicate stable TAG ID '{duplicateId.Key}'.",
                    dataSource.Key));
                continue;
            }

            var plan = new InternalMemoryRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                IsClientMemoryDriver(dataSource.Driver),
                definitions);

            if (plan.IsClientMemory) clientPlans.Add(plan);
            else serverPlans.Add(plan);
        }

        var memoryKeys = dataSources
            .Where(x => IsMemoryDriver(x.Driver))
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var communicationPackage = package with
        {
            DataSources = dataSources.Where(x => !memoryKeys.Contains(x.Key)).ToArray()
        };

        return new InternalMemoryRuntimeCompilation(
            communicationPackage,
            serverPlans,
            clientPlans,
            issues);
    }

    public static bool IsMemoryDriver(string? driver) =>
        IsClientMemoryDriver(driver) || IsServerMemoryDriver(driver);

    public static bool IsClientMemoryDriver(string? driver) =>
        string.Equals(driver, ClientMemoryDriverKey, StringComparison.OrdinalIgnoreCase);

    public static bool IsServerMemoryDriver(string? driver) =>
        string.Equals(driver, ServerMemoryDriverKey, StringComparison.OrdinalIgnoreCase);

    private static TagDefinition BuildTagDefinition(TagEngineeringDto dto)
    {
        var metadata = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
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
            access);
    }

    private static void Set(Dictionary<string, string> target, string key, double? value)
    {
        if (value.HasValue) target[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static void Set(Dictionary<string, string> target, string key, int? value)
    {
        if (value.HasValue) target[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }
}
