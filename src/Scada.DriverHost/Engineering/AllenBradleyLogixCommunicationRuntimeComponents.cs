using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.AllenBradley;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record AllenBradleyLogixCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    AllenBradleyLogixOptions Options,
    IReadOnlyCollection<LogixTagBinding> Bindings) : ICommunicationDriverRuntimePlan
{
    public string DriverType => AllenBradleyLogixContractIdentity.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Bindings.Select(static binding => binding.Tag).ToArray();
}

/// <summary>
/// Coordinator-owned Logix convergence adapter. Schema-v15 CommunicationBinding
/// is authoritative; legacy Address is retained only as an explicit migration
/// path. SDK/session objects never enter the shared runtime plan.
/// </summary>
public sealed class AllenBradleyLogixCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    private static readonly HashSet<string> AllowedDataSourceSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        "host",
        "port",
        "profile",
        "route",
        "scanIntervalMs",
        "requestTimeoutMs",
        "reconnectMinimumMs",
        "reconnectMaximumMs",
        "maxBatchSize",
        "securityMode"
    };

    public string DriverType => AllenBradleyLogixContractIdentity.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);

        var issues = new List<EngineeringDriverIssue>();
        if (string.IsNullOrWhiteSpace(dataSource.Key))
        {
            issues.Add(Error("LOGIX_DATASOURCE_KEY_REQUIRED", "Allen-Bradley Logix data source key is required.", dataSource.Key ?? string.Empty));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }
        if (string.IsNullOrWhiteSpace(dataSource.Name))
            issues.Add(Error("LOGIX_DATASOURCE_NAME_REQUIRED", $"Allen-Bradley Logix data source '{dataSource.Key}' requires a name.", dataSource.Key));
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "LOGIX_DRIVER_TYPE_INVALID",
                $"Data source '{dataSource.Key}' uses driver '{dataSource.Driver}' instead of '{DriverType}'.",
                dataSource.Key));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }

        var settings = CaseInsensitive(dataSource.Settings);
        foreach (var key in settings.Keys.Where(key => !AllowedDataSourceSettings.Contains(key)))
        {
            issues.Add(Error(
                "LOGIX_DATASOURCE_SETTING_UNSUPPORTED",
                $"Allen-Bradley Logix data source '{dataSource.Key}' contains unsupported setting '{key}'.",
                dataSource.Key));
        }

        if (dataSource.SecretReferences is { Count: > 0 })
        {
            issues.Add(Error(
                "LOGIX_PROTECTED_MATERIAL_UNSUPPORTED",
                $"Allen-Bradley Logix data source '{dataSource.Key}' contains protected-material references, but the current runtime implements only unsecured EtherNet/IP and will not silently upgrade or downgrade CIP Security.",
                dataSource.Key));
        }

        var context = new DriverEngineeringDataSourceContext(
            dataSource.Key,
            dataSource.Name,
            dataSource.Driver,
            settings,
            dataSource.SecretReferences ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        AllenBradleyLogixOptions? options = null;
        if (!AllenBradleyLogixEngineeringAdapter.TryCreateOptions(context, out options, out var optionIssues))
            issues.AddRange(optionIssues.Select(issue => ToCompilationIssue(issue, dataSource.Key)));
        else
            issues.AddRange(optionIssues.Select(issue => ToCompilationIssue(issue, dataSource.Key)));

        var bindings = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .Select(tag => BuildBinding(package.SchemaVersion, dataSource.Key, tag, issues))
            .Where(binding => binding is not null)
            .Cast<LogixTagBinding>()
            .ToArray();

        if (bindings.Length == 0)
        {
            issues.Add(Error(
                "LOGIX_DATASOURCE_NO_TAGS",
                $"Allen-Bradley Logix data source '{dataSource.Key}' requires at least one configured TAG.",
                dataSource.Key));
        }

        foreach (var duplicate in bindings.GroupBy(static binding => binding.Tag.Id).Where(static group => group.Count() > 1))
        {
            issues.Add(Error(
                "LOGIX_TAG_ID_DUPLICATE",
                $"Allen-Bradley Logix data source '{dataSource.Key}' contains duplicate stable TAG id '{duplicate.Key}'.",
                dataSource.Key));
        }

        if (issues.Any(static issue => issue.IsError) || options is null || bindings.Length == 0)
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        return new CommunicationDriverRuntimePlanningResult(
            new AllenBradleyLogixCommunicationRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                options,
                bindings),
            issues);
    }

    private static LogixTagBinding? BuildBinding(
        int packageSchemaVersion,
        string dataSourceKey,
        TagEngineeringDto dto,
        ICollection<EngineeringDriverIssue> issues)
    {
        if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
        {
            issues.Add(Error(
                "LOGIX_TAG_STABLE_ID_REQUIRED",
                $"Allen-Bradley Logix TAG '{dto.Path}' requires a non-empty stable Id before runtime activation.",
                dataSourceKey,
                dto.Path));
            return null;
        }

        var binding = dto.CommunicationBinding;
        string? portableAddress;
        if (binding is null)
        {
            portableAddress = dto.Address;
            if (packageSchemaVersion >= 15)
            {
                issues.Add(new EngineeringDriverIssue(
                    "LOGIX_TAG_LEGACY_BINDING",
                    $"Allen-Bradley Logix TAG '{dto.Path}' uses legacy Address without CommunicationBinding; it remains activatable only for backward-compatible migration.",
                    dataSourceKey,
                    dto.Path,
                    IsError: false));
            }
        }
        else
        {
            try
            {
                binding.Validate();
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or NotSupportedException)
            {
                issues.Add(Error(
                    "LOGIX_TAG_BINDING_INVALID",
                    $"Allen-Bradley Logix TAG '{dto.Path}' has an invalid CommunicationBinding: {ex.Message}",
                    dataSourceKey,
                    dto.Path));
                return null;
            }

            if (!binding.SchemaId.Equals(AllenBradleyLogixContractIdentity.BindingSchemaId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error(
                    "LOGIX_TAG_BINDING_SCHEMA_MISMATCH",
                    $"Allen-Bradley Logix TAG '{dto.Path}' binding schema must be '{AllenBradleyLogixContractIdentity.BindingSchemaId}', received '{binding.SchemaId}'.",
                    dataSourceKey,
                    dto.Path));
            }
            if (binding.SchemaVersion != AllenBradleyLogixContractIdentity.BindingSchemaVersion)
            {
                issues.Add(Error(
                    "LOGIX_TAG_BINDING_SCHEMA_VERSION_UNSUPPORTED",
                    $"Allen-Bradley Logix TAG '{dto.Path}' binding schema version must be {AllenBradleyLogixContractIdentity.BindingSchemaVersion}, received {binding.SchemaVersion}.",
                    dataSourceKey,
                    dto.Path));
            }
            if (binding.ValueTransform is { IsIdentity: false })
            {
                issues.Add(Error(
                    "LOGIX_TAG_BINDING_TRANSFORM_UNSUPPORTED",
                    $"Allen-Bradley Logix TAG '{dto.Path}' cannot use byte/word transforms because symbolic CIP values are already typed by the protocol.",
                    dataSourceKey,
                    dto.Path));
            }
            foreach (var key in binding.EffectiveSettings.Keys)
            {
                issues.Add(Error(
                    "LOGIX_TAG_BINDING_SETTING_UNSUPPORTED",
                    $"Allen-Bradley Logix TAG '{dto.Path}' contains unsupported binding setting '{key}' in schema v1.",
                    dataSourceKey,
                    dto.Path));
            }
            if (!string.IsNullOrWhiteSpace(dto.Address) &&
                !string.Equals(dto.Address, binding.PortableAddress, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    "LOGIX_TAG_BINDING_ADDRESS_MISMATCH",
                    $"Allen-Bradley Logix TAG '{dto.Path}' Address must exactly match CommunicationBinding.PortableAddress.",
                    dataSourceKey,
                    dto.Path));
            }
            portableAddress = binding.PortableAddress;
        }

        if (!LogixPortableAddress.TryParse(portableAddress, out var reference, out var externalAccess, out var constant, out var parseError) || reference is null)
        {
            issues.Add(Error(
                "LOGIX_TAG_ADDRESS_INVALID",
                parseError ?? $"Allen-Bradley Logix TAG '{dto.Path}' portable address is invalid.",
                dataSourceKey,
                dto.Path));
            return null;
        }

        var canonicalAddress = LogixPortableAddress.Format(reference, externalAccess, constant);
        if (!string.Equals(portableAddress, canonicalAddress, StringComparison.Ordinal))
        {
            issues.Add(Error(
                "LOGIX_TAG_ADDRESS_NONCANONICAL",
                $"Allen-Bradley Logix TAG '{dto.Path}' portable address must be canonical '{canonicalAddress}'.",
                dataSourceKey,
                dto.Path));
        }
        if (externalAccess == LogixExternalAccess.None)
        {
            issues.Add(Error(
                "LOGIX_TAG_NOT_READABLE",
                $"Allen-Bradley Logix TAG '{dto.Path}' declares External Access None and cannot be activated for runtime acquisition.",
                dataSourceKey,
                dto.Path));
        }

        var tag = BuildCanonicalTag(dto, canonicalAddress);
        var logixBinding = new LogixTagBinding(
            tag,
            reference,
            Writable: !dto.ReadOnly,
            ExternalAccess: externalAccess,
            Constant: constant);
        try
        {
            logixBinding.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            issues.Add(Error(
                "LOGIX_TAG_CONFIGURATION_INVALID",
                ex.Message,
                dataSourceKey,
                dto.Path));
            return null;
        }

        return logixBinding;
    }

    private static TagDefinition BuildCanonicalTag(TagEngineeringDto dto, string canonicalAddress)
    {
        var metadata = CaseInsensitive(dto.Metadata);
        if (dto.CommunicationBinding is not null)
        {
            foreach (var key in metadata.Keys
                         .Where(key => key.StartsWith("logix.", StringComparison.OrdinalIgnoreCase) ||
                                      key.StartsWith("cip.", StringComparison.OrdinalIgnoreCase))
                         .ToArray())
                metadata.Remove(key);
        }
        metadata["address"] = canonicalAddress;
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
            dto.AddressSelector,
            dto.CommunicationBinding);
    }

    private static void Set(Dictionary<string, string> metadata, string key, double? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static void Set(Dictionary<string, string> metadata, string key, int? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, string> CaseInsensitive(IReadOnlyDictionary<string, string>? source) =>
        source is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);

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

public sealed class AllenBradleyLogixCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly ILogixProtocolClientFactory _clientFactory;

    public AllenBradleyLogixCommunicationRuntimeFactory(ILogixProtocolClientFactory? clientFactory = null)
    {
        _clientFactory = clientFactory ?? new LogixEtherNetIpClientFactory();
    }

    public string DriverType => AllenBradleyLogixContractIdentity.DriverType;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();

        if (plan is not AllenBradleyLogixCommunicationRuntimePlan logixPlan)
            throw new ArgumentException($"Allen-Bradley Logix runtime factory requires {nameof(AllenBradleyLogixCommunicationRuntimePlan)}.", nameof(plan));
        if (!logixPlan.DriverType.Equals(DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Allen-Bradley Logix runtime plan declares unexpected DriverType '{logixPlan.DriverType}'.", nameof(plan));
        if (logixPlan.Bindings.Count == 0)
            throw new ArgumentException("Allen-Bradley Logix runtime plan requires at least one TAG binding.", nameof(plan));
        if (services.ProtectedMaterialResolver is not null && logixPlan.Options.SecurityMode == LogixSecurityMode.CipSecurityRequired)
            throw new NotSupportedException("CIP Security is not implemented and cannot consume protected material through an unsecured EtherNet/IP runtime.");

        var inner = new AllenBradleyLogixDriver(
            logixPlan.DataSourceKey,
            logixPlan.Name,
            logixPlan.Options,
            services.Cache,
            services.Registry,
            logixPlan.Bindings,
            _clientFactory);
        return new AllenBradleyLogixHostCommunicationDriver(inner);
    }
}

internal sealed class AllenBradleyLogixHostCommunicationDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource,
    ICommunicationDriverReadinessSource
{
    private readonly AllenBradleyLogixDriver _inner;
    private bool _started;

    public AllenBradleyLogixHostCommunicationDriver(AllenBradleyLogixDriver inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string DriverId => _inner.DriverId;
    public string Name => _inner.Name;
    public DriverCapabilities Capabilities => _inner.Capabilities;
    public DriverStatus Status => _inner.Status;
    public IReadOnlyCollection<TagDefinition> Tags => _inner.Tags;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _inner.StartAsync(CancellationToken.None).ConfigureAwait(false);
        _started = true;
    }

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        _inner.StopAsync(cancellationToken);

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(tagId, cancellationToken);

    public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(tagId, value, cancellationToken);

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics() =>
        _inner.GetCommunicationDiagnostics();

    public CommunicationDriverReadinessSnapshot GetCommunicationReadiness()
    {
        var diagnostics = _inner.GetCommunicationDiagnostics();
        var connected = diagnostics.ProtocolDetails is not null &&
                        diagnostics.ProtocolDetails.TryGetValue("connected", out var connectedRaw) &&
                        bool.TryParse(connectedRaw, out var parsedConnected) &&
                        parsedConnected;
        var evidence = AllenBradleyReadinessPolicy.Evaluate(
            connected,
            diagnostics.State,
            diagnostics.Counters.ReadOperations);

        var state = evidence.IsReady
            ? CommunicationDriverReadinessState.Ready
            : diagnostics.State == CommunicationDriverOperationalState.Faulted || _inner.Status.State == DriverState.Faulted
                ? CommunicationDriverReadinessState.Faulted
                : !_started
                    ? CommunicationDriverReadinessState.NotStarted
                    : diagnostics.State == CommunicationDriverOperationalState.Stopped
                        ? CommunicationDriverReadinessState.Stopped
                        : CommunicationDriverReadinessState.Starting;

        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["connected"] = connected ? "true" : "false",
            ["operationalState"] = diagnostics.State.ToString(),
            ["readOperations"] = diagnostics.Counters.ReadOperations.ToString(CultureInfo.InvariantCulture)
        };
        if (diagnostics.ProtocolDetails is not null)
        {
            foreach (var key in new[] { "profile", "route", "messagingMode", "securityMode" })
                if (diagnostics.ProtocolDetails.TryGetValue(key, out var value)) details[key] = value;
        }

        return new CommunicationDriverReadinessSnapshot(
            DriverId,
            AllenBradleyLogixContractIdentity.DriverType,
            state,
            diagnostics.CapturedAt,
            diagnostics.LastError ?? evidence.Reason,
            details);
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
