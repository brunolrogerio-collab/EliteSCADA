using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Bacnet;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record BacnetCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    BacnetSessionOptions SessionOptions,
    TimeSpan ScanRate,
    IReadOnlyCollection<BacnetPoint> Points) : ICommunicationDriverRuntimePlan
{
    public string DriverType => BacnetDriverDescriptor.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(static point => point.Tag).ToArray();
}

/// <summary>
/// Coordinator-owned BACnet/IP convergence adapter. BACnet object identity,
/// session settings, COV policy and readiness remain protocol-owned; schema-v15
/// CommunicationBinding is the shared canonical Engineering envelope.
/// </summary>
public sealed class BacnetCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public string DriverType => BacnetDriverDescriptor.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<EngineeringDriverIssue>();

        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("BACNET_DRIVER_TYPE_INVALID", $"Data source '{dataSource.Key}' is not a BACnet/IP data source.", dataSource.Key));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }
        if (string.IsNullOrWhiteSpace(dataSource.Key))
            issues.Add(Error("BACNET_DATASOURCE_KEY_REQUIRED", "BACnet/IP data source key is required.", dataSource.Key));
        if (string.IsNullOrWhiteSpace(dataSource.Name))
            issues.Add(Error("BACNET_DATASOURCE_NAME_REQUIRED", "BACnet/IP data source name is required.", dataSource.Key));
        if (dataSource.SecretReferences is { Count: > 0 })
        {
            issues.Add(Error(
                "BACNET_PROTECTED_MATERIAL_UNSUPPORTED",
                $"BACnet/IP data source '{dataSource.Key}' cannot declare protected material; BACnet Secure Connect is not implemented by this driver type.",
                dataSource.Key));
        }

        var settings = dataSource.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _ = BacnetRuntimeConfigurationParser.TryCreate(settings, out var configuration, out var configurationErrors);
        foreach (var error in configurationErrors)
            issues.Add(Error("BACNET_SETTING_INVALID", error, dataSource.Key));

        var sourceTags = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceTags.Length == 0)
        {
            issues.Add(Error(
                "BACNET_DATASOURCE_NO_TAGS",
                $"Enabled BACnet/IP data source '{dataSource.Key}' has no associated TAGs and cannot create a runtime.",
                dataSource.Key));
        }

        var points = new List<BacnetPoint>();
        foreach (var dto in sourceTags)
        {
            var point = BuildPoint(package.SchemaVersion, dataSource.Key, configuration?.DeviceInstance, dto, issues);
            if (point is not null) points.Add(point);
        }

        foreach (var duplicate in points.GroupBy(static point => point.Tag.Id).Where(static group => group.Count() > 1))
        {
            issues.Add(Error(
                "BACNET_TAG_STABLE_ID_DUPLICATE",
                $"BACnet/IP data source '{dataSource.Key}' contains duplicate stable TAG ID '{duplicate.Key}'.",
                dataSource.Key));
        }
        foreach (var duplicate in points
                     .GroupBy(static point => point.Binding.StableIdentity, StringComparer.OrdinalIgnoreCase)
                     .Where(static group => group.Count() > 1))
        {
            issues.Add(Error(
                "BACNET_PHYSICAL_IDENTITY_DUPLICATE",
                $"BACnet/IP data source '{dataSource.Key}' contains duplicate physical binding '{duplicate.Key}'.",
                dataSource.Key));
        }

        if (configuration is null || points.Count == 0 || issues.Any(static issue => issue.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        return new CommunicationDriverRuntimePlanningResult(
            new BacnetCommunicationRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                configuration.SessionOptions,
                configuration.ScanRate,
                points.ToArray()),
            issues);
    }

    private static BacnetPoint? BuildPoint(
        int packageSchemaVersion,
        string dataSourceKey,
        uint? configuredDeviceInstance,
        TagEngineeringDto dto,
        ICollection<EngineeringDriverIssue> issues)
    {
        if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
        {
            issues.Add(Error(
                "BACNET_TAG_STABLE_ID_REQUIRED",
                $"BACnet TAG '{dto.Path}' requires a stable non-empty TAG ID before runtime activation.",
                dataSourceKey,
                dto.Path));
            return null;
        }
        if (dto.AddressSelector is not null)
        {
            issues.Add(Error(
                "BACNET_TAG_ADDRESS_SELECTOR_UNSUPPORTED",
                $"BACnet TAG '{dto.Path}' cannot use generic AddressSelector; array index belongs to the BACnet binding identity.",
                dataSourceKey,
                dto.Path));
            return null;
        }

        BacnetBinding? binding;
        if (dto.CommunicationBinding is { } communicationBinding)
        {
            try
            {
                communicationBinding.Validate();
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or NotSupportedException)
            {
                issues.Add(Error("BACNET_TAG_BINDING_INVALID", $"BACnet TAG '{dto.Path}' has an invalid CommunicationBinding: {ex.Message}", dataSourceKey, dto.Path));
                return null;
            }

            if (!string.Equals(communicationBinding.SchemaId, BacnetCommunicationBindingProjection.SchemaId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error(
                    "BACNET_TAG_BINDING_SCHEMA_MISMATCH",
                    $"BACnet TAG '{dto.Path}' binding schema must be '{BacnetCommunicationBindingProjection.SchemaId}', received '{communicationBinding.SchemaId}'.",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
            if (communicationBinding.SchemaVersion != BacnetCommunicationBindingProjection.SchemaVersion)
            {
                issues.Add(Error(
                    "BACNET_TAG_BINDING_SCHEMA_VERSION_UNSUPPORTED",
                    $"BACnet TAG '{dto.Path}' binding schema version must be {BacnetCommunicationBindingProjection.SchemaVersion}, received {communicationBinding.SchemaVersion}.",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
            if (!string.IsNullOrWhiteSpace(dto.Address) &&
                !string.Equals(dto.Address, communicationBinding.PortableAddress, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    "BACNET_TAG_BINDING_ADDRESS_MISMATCH",
                    $"BACnet TAG '{dto.Path}' Address must exactly match CommunicationBinding.PortableAddress.",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
            if (communicationBinding.ValueTransform is { IsIdentity: false })
            {
                issues.Add(Error(
                    "BACNET_TAG_PHYSICAL_TRANSFORM_UNSUPPORTED",
                    $"BACnet TAG '{dto.Path}' cannot use byte/word transforms because BACnet exposes typed property values rather than a raw register representation.",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
            if (!BacnetCommunicationBindingProjection.TryMaterializeCanonical(
                    communicationBinding.PortableAddress,
                    communicationBinding.EffectiveSettings,
                    out binding,
                    out var bindingError))
            {
                issues.Add(Error(
                    "BACNET_TAG_BINDING_INVALID",
                    $"BACnet TAG '{dto.Path}' cannot materialize its canonical binding: {bindingError}",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
        }
        else
        {
            if (packageSchemaVersion >= 15)
            {
                issues.Add(new EngineeringDriverIssue(
                    "BACNET_TAG_LEGACY_BINDING",
                    $"BACnet TAG '{dto.Path}' uses legacy Address/Metadata without CommunicationBinding; it remains activatable only for migration compatibility.",
                    dataSourceKey,
                    dto.Path,
                    IsError: false));
            }
            if (!BacnetBinding.TryParse(dto.Address, out var parsed, out var bindingError) || parsed is null)
            {
                issues.Add(Error("BACNET_TAG_ADDRESS_INVALID", bindingError ?? "BACnet TAG address is invalid.", dataSourceKey, dto.Path));
                return null;
            }
            var metadata = dto.Metadata ?? new Dictionary<string, string>();
            var useCov = ParseLegacyBool(metadata, "bacnet.useCov", parsed.UseCov, dataSourceKey, dto.Path, issues);
            var priority = ParseLegacyPriority(metadata, dataSourceKey, dto.Path, issues) ?? parsed.WritePriority;
            binding = parsed with { UseCov = useCov, WritePriority = priority };
        }

        if (configuredDeviceInstance.HasValue && binding!.DeviceInstance != configuredDeviceInstance.Value)
        {
            issues.Add(Error(
                "BACNET_TAG_DEVICE_MISMATCH",
                $"BACnet TAG '{dto.Path}' targets Device Instance {binding.DeviceInstance}, but data source '{dataSourceKey}' targets {configuredDeviceInstance.Value}.",
                dataSourceKey,
                dto.Path));
            return null;
        }

        try
        {
            var point = new BacnetPoint(BuildCanonicalTag(dto), binding!, Writable: !dto.ReadOnly);
            point.Validate();
            return point;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            issues.Add(Error("BACNET_TAG_CONFIGURATION_INVALID", ex.Message, dataSourceKey, dto.Path));
            return null;
        }
    }

    private static TagDefinition BuildCanonicalTag(TagEngineeringDto dto)
    {
        var metadata = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(dto.Address)) metadata["address"] = dto.Address;
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

    private static bool ParseLegacyBool(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        bool defaultValue,
        string dataSourceKey,
        string tagPath,
        ICollection<EngineeringDriverIssue> issues)
    {
        var raw = Get(metadata, key);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (bool.TryParse(raw, out var value)) return value;
        issues.Add(Error("BACNET_TAG_METADATA_INVALID", $"BACnet metadata '{key}' for TAG '{tagPath}' must be true or false.", dataSourceKey, tagPath));
        return defaultValue;
    }

    private static byte? ParseLegacyPriority(
        IReadOnlyDictionary<string, string> metadata,
        string dataSourceKey,
        string tagPath,
        ICollection<EngineeringDriverIssue> issues)
    {
        var raw = Get(metadata, "bacnet.writePriority");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (byte.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= 16)
            return value;
        issues.Add(Error("BACNET_TAG_METADATA_INVALID", $"BACnet metadata 'bacnet.writePriority' for TAG '{tagPath}' must be from 1 to 16.", dataSourceKey, tagPath));
        return null;
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key)
    {
        foreach (var item in values)
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) return item.Value;
        return null;
    }

    private static EngineeringDriverIssue Error(string code, string message, string dataSourceKey, string? tagPath = null)
        => new(code, message, dataSourceKey, tagPath, IsError: true);
}

public sealed class BacnetCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly IBacnetSessionFactory _sessions;

    public BacnetCommunicationRuntimeFactory(IBacnetSessionFactory? sessions = null)
        => _sessions = sessions ?? new SystemIoBacnetSessionFactory();

    public string DriverType => BacnetDriverDescriptor.DriverType;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();

        if (plan is not BacnetCommunicationRuntimePlan bacnetPlan)
            throw new ArgumentException($"BACnet runtime factory requires {nameof(BacnetCommunicationRuntimePlan)}.", nameof(plan));
        if (!string.Equals(bacnetPlan.DriverType, DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"BACnet runtime plan DriverType '{bacnetPlan.DriverType}' does not match '{DriverType}'.", nameof(plan));
        if (bacnetPlan.Points.Count == 0)
            throw new ArgumentException("BACnet runtime plan requires at least one point.", nameof(plan));

        var session = _sessions.Create(bacnetPlan.SessionOptions);
        var driver = new BacnetIpDriver(
            bacnetPlan.DataSourceKey,
            bacnetPlan.Name,
            services.Cache,
            services.Registry,
            bacnetPlan.Points,
            session,
            bacnetPlan.ScanRate);
        return new BacnetCoordinatorRuntimeDriver(driver, bacnetPlan.DataSourceKey, bacnetPlan.Points.Count);
    }

    private sealed class BacnetCoordinatorRuntimeDriver :
        ICommunicationDriver,
        ICommunicationDiagnosticsSource,
        ICommunicationDriverReadinessSource
    {
        private readonly BacnetIpDriver _inner;
        private readonly string _dataSourceKey;
        private readonly int _configuredPointCount;

        public BacnetCoordinatorRuntimeDriver(BacnetIpDriver inner, string dataSourceKey, int configuredPointCount)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _dataSourceKey = dataSourceKey;
            _configuredPointCount = configuredPointCount;
        }

        public string DriverId => _inner.DriverId;
        public string Name => _inner.Name;
        public DriverCapabilities Capabilities => _inner.Capabilities;
        public DriverStatus Status => _inner.Status;
        public IReadOnlyCollection<TagDefinition> Tags => _inner.Tags;

        public Task StartAsync(CancellationToken cancellationToken = default) => _inner.StartAsync(cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken = default) => _inner.StopAsync(cancellationToken);
        public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default) => _inner.ReadAsync(tagId, cancellationToken);
        public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) => _inner.WriteAsync(tagId, value, cancellationToken);
        public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics() => _inner.GetCommunicationDiagnostics();

        public CommunicationDriverReadinessSnapshot GetCommunicationReadiness()
        {
            var diagnostics = _inner.GetCommunicationDiagnostics();
            bool? reachable = null;
            if (diagnostics.ProtocolDetails is not null &&
                diagnostics.ProtocolDetails.TryGetValue("deviceReachable", out var reachableText))
            {
                if (string.Equals(reachableText, "true", StringComparison.OrdinalIgnoreCase)) reachable = true;
                else if (string.Equals(reachableText, "false", StringComparison.OrdinalIgnoreCase)) reachable = false;
            }

            var evaluation = BacnetReadinessPolicy.Evaluate(reachable, diagnostics.State, _configuredPointCount);
            var state = evaluation.Ready
                ? CommunicationDriverReadinessState.Ready
                : diagnostics.State switch
                {
                    CommunicationDriverOperationalState.Faulted => CommunicationDriverReadinessState.Faulted,
                    CommunicationDriverOperationalState.Stopped or CommunicationDriverOperationalState.Stopping => CommunicationDriverReadinessState.Stopped,
                    _ => CommunicationDriverReadinessState.Starting
                };

            return new CommunicationDriverReadinessSnapshot(
                _dataSourceKey,
                BacnetDriverDescriptor.DriverType,
                state,
                diagnostics.CapturedAt,
                evaluation.Reason,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["deviceInstance"] = _inner.DeviceInstance.ToString(CultureInfo.InvariantCulture),
                    ["deviceReachable"] = reachable.HasValue ? (reachable.Value ? "true" : "false") : "unknown",
                    ["operationalState"] = diagnostics.State.ToString(),
                    ["configuredPointCount"] = _configuredPointCount.ToString(CultureInfo.InvariantCulture)
                });
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
