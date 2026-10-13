using System.Globalization;
using Scada.Core.Tags;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record MatterWifiCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    IReadOnlyDictionary<string, string> ProtectedKeyReferences,
    IReadOnlyCollection<MatterWifiPoint> Points,
    Guid DataSourceId) : ICommunicationDriverRuntimePlan
{
    public string DriverType => MatterWifiDriverDescriptorProvider.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

/// <summary>Compiles only explicitly bound, already-commissioned Matter attributes.</summary>
public sealed class MatterWifiCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    private static readonly HashSet<string> AllowedSettings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<bool> _isHighAvailabilityEnabled;

    public MatterWifiCommunicationRuntimePlanner(Func<bool>? isHighAvailabilityEnabled = null) =>
        _isHighAvailabilityEnabled = isHighAvailabilityEnabled ?? (() => false);

    public string DriverType => MatterWifiDriverDescriptorProvider.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(EngineeringPackage package, DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<EngineeringDriverIssue>();
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("MATTER_WIFI_DRIVER_TYPE_MISMATCH", "Data Source type does not match the Matter Wi-Fi driver.", dataSource.Key));
            return new(null, issues);
        }
        if (_isHighAvailabilityEnabled())
        {
            issues.Add(Error("MATTER_WIFI_HA_UNSUPPORTED", "Matter Wi-Fi v1 cannot be activated in a High Availability installation; its fabric and encrypted sidecar state are local to one host.", dataSource.Key));
            return new(null, issues);
        }

        var sourceId = dataSource.Id.GetValueOrDefault();
        if (sourceId == Guid.Empty)
            issues.Add(Error("MATTER_WIFI_DATASOURCE_ID_REQUIRED", "Matter Wi-Fi requires a stable Data Source GUID.", dataSource.Key));

        var settings = dataSource.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (settings.Keys.Any(key => !AllowedSettings.Contains(key)))
            issues.Add(Error("MATTER_WIFI_SETTING_UNSUPPORTED", "Matter Wi-Fi v1 accepts no public Data Source settings.", dataSource.Key));

        var secretReferences = dataSource.SecretReferences ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var keyField = MatterWifiDriverDescriptorProvider.FabricStorageKeyField;
        if (secretReferences.Keys.Any(key => !string.Equals(key, keyField, StringComparison.OrdinalIgnoreCase)))
            issues.Add(Error("MATTER_WIFI_SECRET_FIELD_UNSUPPORTED", "Matter Wi-Fi accepts only the host-owned fabric-storage key slot.", dataSource.Key));
        var keyReference = secretReferences.FirstOrDefault(pair => string.Equals(pair.Key, keyField, StringComparison.OrdinalIgnoreCase)).Value;
        if (!string.Equals(keyReference, HostMaterialSlotReference.Marker, StringComparison.Ordinal))
            issues.Add(Error("MATTER_WIFI_STORAGE_KEY_REQUIRED", "Provision the host-owned protected fabric-storage key before enabling this Data Source.", dataSource.Key));

        var sourceTags = EngineeringTagDataSourceAssociation.NormalizeForPlanner(package, dataSource).Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceTags.Length is < 1 or > 512)
            issues.Add(Error("MATTER_WIFI_TAG_COUNT_INVALID", "Matter Wi-Fi v1 requires 1 through 512 associated persistent-value TAGs.", dataSource.Key));

        var points = new List<MatterWifiPoint>();
        foreach (var dto in sourceTags)
        {
            if (dto.Id is not { } tagId || tagId == Guid.Empty)
            {
                issues.Add(Error("MATTER_WIFI_TAG_ID_REQUIRED", $"TAG '{dto.Path}' requires a stable non-empty TAG ID.", dataSource.Key, dto.Path));
                continue;
            }
            if (dto.CommunicationBinding is not { } binding)
            {
                issues.Add(Error("MATTER_WIFI_TAG_BINDING_REQUIRED", $"TAG '{dto.Path}' requires a versioned Matter Wi-Fi CommunicationBinding.", dataSource.Key, dto.Path));
                continue;
            }
            try { binding.Validate(); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                issues.Add(Error("MATTER_WIFI_TAG_BINDING_INVALID", $"TAG '{dto.Path}' has an invalid CommunicationBinding: {exception.Message}", dataSource.Key, dto.Path));
                continue;
            }
            if (!string.Equals(binding.SchemaId, MatterWifiDriverDescriptorProvider.TagBindingSchemaId, StringComparison.Ordinal) || binding.SchemaVersion != 1)
            {
                issues.Add(Error("MATTER_WIFI_TAG_BINDING_VERSION_UNSUPPORTED", $"TAG '{dto.Path}' must use Matter Wi-Fi binding schema v1.", dataSource.Key, dto.Path));
                continue;
            }
            if (binding.EffectiveSettings.Count != 0 || dto.AddressSelector is not null || binding.ValueTransform is { IsIdentity: false })
            {
                issues.Add(Error("MATTER_WIFI_TAG_BINDING_SETTINGS_UNSUPPORTED", $"TAG '{dto.Path}' cannot add selectors, transforms, or generic settings in v1.", dataSource.Key, dto.Path));
                continue;
            }
            if (!string.IsNullOrWhiteSpace(dto.Address) && !string.Equals(dto.Address, binding.PortableAddress, StringComparison.Ordinal))
            {
                issues.Add(Error("MATTER_WIFI_TAG_ADDRESS_MISMATCH", $"TAG '{dto.Path}' Address must equal CommunicationBinding.PortableAddress.", dataSource.Key, dto.Path));
                continue;
            }
            if (!TryParseAddress(binding.PortableAddress, out var nodeId, out var pointAddress))
            {
                issues.Add(Error("MATTER_WIFI_TAG_ADDRESS_INVALID", $"TAG '{dto.Path}' must use canonical node/<id>/endpoint/<id>/cluster/<id>/attribute/<id> form.", dataSource.Key, dto.Path));
                continue;
            }
            if (!IsSupportedType(dto.DataType))
            {
                issues.Add(Error("MATTER_WIFI_TAG_TYPE_UNSUPPORTED", $"TAG '{dto.Path}' uses a process-value type outside the Matter Wi-Fi v1 subset.", dataSource.Key, dto.Path));
                continue;
            }

            var metadata = dto.Metadata is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(dto.Address)) metadata["address"] = dto.Address;
            if (dto.ScaleMinimum.HasValue) metadata["scale.minimum"] = dto.ScaleMinimum.Value.ToString(CultureInfo.InvariantCulture);
            if (dto.ScaleMaximum.HasValue) metadata["scale.maximum"] = dto.ScaleMaximum.Value.ToString(CultureInfo.InvariantCulture);
            if (dto.Historian is not null)
            {
                metadata["historian.enabled"] = dto.Historian.Enabled.ToString(CultureInfo.InvariantCulture);
                metadata["historian.strategy"] = dto.Historian.Strategy;
                if (dto.Historian.Deadband.HasValue) metadata["historian.deadband"] = dto.Historian.Deadband.Value.ToString(CultureInfo.InvariantCulture);
                if (dto.Historian.PeriodMilliseconds.HasValue) metadata["historian.periodMs"] = dto.Historian.PeriodMilliseconds.Value.ToString(CultureInfo.InvariantCulture);
                if (dto.Historian.MaximumPeriodMilliseconds.HasValue) metadata["historian.maxPeriodMs"] = dto.Historian.MaximumPeriodMilliseconds.Value.ToString(CultureInfo.InvariantCulture);
            }
            var access = dto.AccessPolicy is null ? null : new TagAccessPolicy(dto.AccessPolicy.ReadRoles?.ToArray(), dto.AccessPolicy.WriteRoles?.ToArray(), dto.AccessPolicy.ConfigureRoles?.ToArray());
            var tag = new TagDefinition(tagId, dto.Name, dto.Path, dto.DataType, dataSource.Key, dto.EngineeringUnit, dto.Description,
                dto.ReadOnly, metadata, access, dto.AddressSelector, binding, dto.DataSourceId);
            points.Add(new MatterWifiPoint(tag, nodeId, pointAddress, Writable: !dto.ReadOnly));
        }

        if (points.Select(point => point.Tag.Id).Distinct().Count() != points.Count)
            issues.Add(Error("MATTER_WIFI_TAG_ID_DUPLICATE", "Matter Wi-Fi Data Source contains duplicate stable TAG IDs.", dataSource.Key));
        if (points.Select(point => point.NodeId + "/" + point.PortableAddress).Distinct(StringComparer.Ordinal).Count() != points.Count)
            issues.Add(Error("MATTER_WIFI_TAG_ADDRESS_DUPLICATE", "Two TAGs cannot bind the same Matter attribute.", dataSource.Key));

        if (issues.Any(issue => issue.IsError) || sourceId == Guid.Empty)
            return new(null, issues);
        return new(new MatterWifiCommunicationRuntimePlan(dataSource.Key, dataSource.Name,
            new Dictionary<string, string>(StringComparer.Ordinal) { [keyField] = keyReference! }, points, sourceId), issues);
    }

    internal static bool TryParseAddress(string address, out string nodeId, out string attributeAddress)
    {
        nodeId = string.Empty;
        attributeAddress = string.Empty;
        if (string.IsNullOrEmpty(address) || address.Length > 128) return false;
        var parts = address.Split('/');
        if (parts.Length != 8 || parts[0] != "node" || parts[2] != "endpoint" || parts[4] != "cluster" || parts[6] != "attribute" ||
            !ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedNode) || parsedNode == 0 || parsedNode.ToString(CultureInfo.InvariantCulture) != parts[1] ||
            !ushort.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var endpoint) || endpoint.ToString(CultureInfo.InvariantCulture) != parts[3] ||
            !uint.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out var cluster) || cluster.ToString(CultureInfo.InvariantCulture) != parts[5] ||
            !uint.TryParse(parts[7], NumberStyles.None, CultureInfo.InvariantCulture, out var attribute) || attribute.ToString(CultureInfo.InvariantCulture) != parts[7]) return false;
        nodeId = parts[1];
        attributeAddress = $"{parts[3]}/{parts[5]}/{parts[7]}";
        return true;
    }

    private static bool IsSupportedType(TagDataType type) => type is TagDataType.Boolean or TagDataType.Int16 or TagDataType.Int32 or
        TagDataType.Int64 or TagDataType.Float or TagDataType.Double or TagDataType.String;

    private static EngineeringDriverIssue Error(string code, string message, string sourceKey, string? path = null) =>
        new(code, message, sourceKey, path);
}

public sealed class MatterWifiDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverType = "matter.wifi";
    public const string SchemaId = "elitescada.driver.matter.wifi.configuration";
    public const string TagBindingSchemaId = "elitescada.driver.matter.wifi";
    public const string FabricStorageKeyField = "fabricStorageKey";
    public const string FabricStorageKeyPurpose = "matter.wifi.fabric-storage-key";

    public CommunicationDriverTypeDescriptor Descriptor { get; } = new(
        DriverType,
        "Matter Wi-Fi (isolated preview)",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.None,
        AcquisitionModes: [DriverAcquisitionMode.Hybrid],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            SchemaId, 1,
            DataSourceFields: [new(FabricStorageKeyField, DriverConfigurationValueKind.SecretReference, Required: true)],
            TagBindingFields: []),
        Description: "Isolated preview for explicitly mapped, already-commissioned Matter Wi-Fi attributes. No commissioning, Thread, device administration, HA or production support.",
        TagBindingSchemaId: TagBindingSchemaId,
        TagBindingSchemaVersion: 1,
        IntegrationDomains: [IntegrationDomain.Building, IntegrationDomain.Residential, IntegrationDomain.IoT],
        ConnectionModel: DriverConnectionModel.LocalBridge,
        ExternalDependencies: [new DriverExternalDependencyDescriptor(
            DriverExternalDependencyKind.OptionalSidecar,
            "Linux x64 host, Node.js 22.13+ or 24+, IPv6-capable network for Matter devices",
            "Exact-locked local Matter Server alpha; development/test only, no commissioning or real-device validation.")]);
}
