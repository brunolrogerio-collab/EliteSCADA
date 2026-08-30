using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.AllenBradley;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

/// <summary>
/// Library-independent Allen-Bradley Logix runtime plan shaped for the common
/// communication-driver runtime registry. Protocol sessions/clients are created
/// only by the runtime factory and never become Engineering/runtime-plan state.
/// </summary>
public sealed record AllenBradleyLogixRuntimePlan(
    string DataSourceKey,
    string Name,
    AllenBradleyLogixOptions Options,
    IReadOnlyCollection<LogixTagBinding> Bindings)
{
    public string DriverType => AllenBradleyLogixContractIdentity.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Bindings.Select(static x => x.Tag).ToArray();
}

public sealed record AllenBradleyLogixRuntimePlanningResult(
    AllenBradleyLogixRuntimePlan? Plan,
    IReadOnlyCollection<EngineeringDriverIssue> Issues)
{
    public bool CanActivate => Plan is not null && Issues.All(static x => !x.IsError);
}

/// <summary>
/// Compiles one canonical Logix Data Source without modifying the current
/// monolithic EngineeringDriverCompiler. Coordinator integration can later add a
/// thin ICommunicationDriverRuntimePlanner adapter around this protocol-owned
/// implementation once the common registry contracts land on main.
/// </summary>
public static class AllenBradleyLogixEngineeringRuntimePlanner
{
    public static AllenBradleyLogixRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);

        var issues = new List<EngineeringDriverIssue>();
        if (string.IsNullOrWhiteSpace(dataSource.Key))
        {
            issues.Add(Error(
                "LOGIX_DATASOURCE_KEY_REQUIRED",
                "Allen-Bradley Logix data source key is required.",
                dataSource.Key ?? string.Empty));
            return new(null, issues);
        }

        if (string.IsNullOrWhiteSpace(dataSource.Name))
        {
            issues.Add(Error(
                "LOGIX_DATASOURCE_NAME_REQUIRED",
                $"Allen-Bradley Logix data source '{dataSource.Key}' requires a name.",
                dataSource.Key));
        }

        if (!string.Equals(dataSource.Driver, AllenBradleyLogixContractIdentity.DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "LOGIX_DRIVER_TYPE_INVALID",
                $"Data source '{dataSource.Key}' uses driver '{dataSource.Driver}' instead of '{AllenBradleyLogixContractIdentity.DriverType}'.",
                dataSource.Key));
            return new(null, issues);
        }

        var context = new DriverEngineeringDataSourceContext(
            dataSource.Key,
            dataSource.Name,
            dataSource.Driver,
            dataSource.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            dataSource.SecretReferences ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        if (!AllenBradleyLogixEngineeringAdapter.TryCreateOptions(context, out var options, out var optionIssues))
        {
            issues.AddRange(optionIssues.Select(issue => ToCompilationIssue(issue, dataSource.Key)));
            return new(null, issues);
        }

        issues.AddRange(optionIssues.Select(issue => ToCompilationIssue(issue, dataSource.Key)));
        try
        {
            options!.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            issues.Add(Error("LOGIX_DATASOURCE_CONFIGURATION_INVALID", ex.Message, dataSource.Key));
        }

        var sourceTags = package.Tags
            .Where(x => string.Equals(x.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sourceTags.Length == 0)
        {
            issues.Add(new EngineeringDriverIssue(
                "LOGIX_DATASOURCE_NO_TAGS",
                $"Enabled Allen-Bradley Logix data source '{dataSource.Key}' has no associated TAGs.",
                dataSource.Key,
                IsError: false));
        }

        var bindings = new List<LogixTagBinding>(sourceTags.Length);
        foreach (var dto in sourceTags)
        {
            if (!LogixPortableAddress.TryParse(dto.Address, out var reference, out var access, out var constant, out var parseError) ||
                reference is null)
            {
                issues.Add(Error(
                    "LOGIX_TAG_ADDRESS_INVALID",
                    parseError ?? "Logix portable address is invalid.",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            if (access == LogixExternalAccess.None)
            {
                issues.Add(Error(
                    "LOGIX_TAG_NOT_READABLE",
                    $"TAG '{dto.Path}' declares Logix External Access None and cannot be activated for Runtime acquisition.",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            var tag = BuildTagDefinition(dto);
            var binding = new LogixTagBinding(
                tag,
                reference,
                Writable: !dto.ReadOnly,
                access,
                constant);

            try
            {
                binding.Validate();
                bindings.Add(binding);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                issues.Add(Error(
                    "LOGIX_TAG_CONFIGURATION_INVALID",
                    ex.Message,
                    dataSource.Key,
                    dto.Path));
            }
        }

        var duplicateTag = bindings
            .GroupBy(static x => x.Tag.Id)
            .FirstOrDefault(static x => x.Count() > 1);
        if (duplicateTag is not null)
        {
            issues.Add(Error(
                "LOGIX_TAG_ID_DUPLICATE",
                $"Multiple Logix bindings resolve to canonical TAG ID '{duplicateTag.Key}'.",
                dataSource.Key));
        }

        if (issues.Any(static x => x.IsError))
            return new(null, issues);

        return new(
            new AllenBradleyLogixRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                options!,
                bindings),
            issues);
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
            metadata["historian.enabled"] = dto.Historian.Enabled.ToString(CultureInfo.InvariantCulture);
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
            dto.AddressSelector);
    }

    private static void Set(Dictionary<string, string> metadata, string key, double? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static void Set(Dictionary<string, string> metadata, string key, int? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static EngineeringDriverIssue ToCompilationIssue(DriverEngineeringIssue issue, string dataSourceKey) =>
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
        new(code, message, dataSourceKey, tagPath, IsError: true);
}

/// <summary>
/// Protocol-owned runtime factory core. It receives only host-owned TAG services
/// and creates the concrete CIP client behind ILogixProtocolClientFactory at
/// runtime instantiation time, never while compiling Engineering.
/// </summary>
public sealed class AllenBradleyLogixRuntimeFactory
{
    private readonly ILogixProtocolClientFactory _clientFactory;

    public AllenBradleyLogixRuntimeFactory(ILogixProtocolClientFactory? clientFactory = null)
    {
        _clientFactory = clientFactory ?? new LogixEtherNetIpClientFactory();
    }

    public string DriverType => AllenBradleyLogixContractIdentity.DriverType;

    public ICommunicationDriver Create(
        AllenBradleyLogixRuntimePlan plan,
        ICurrentTagCache cache,
        ITagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);

        if (plan.Bindings.Count == 0)
            throw new InvalidOperationException(
                $"Allen-Bradley Logix data source '{plan.DataSourceKey}' has no runtime TAG bindings.");

        return new AllenBradleyLogixDriver(
            $"{AllenBradleyLogixContractIdentity.DriverType}:{plan.DataSourceKey}",
            plan.Name,
            plan.Options,
            cache,
            registry,
            plan.Bindings,
            _clientFactory);
    }
}
