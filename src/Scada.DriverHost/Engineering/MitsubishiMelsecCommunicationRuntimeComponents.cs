using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mitsubishi;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record MitsubishiMelsecCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    MitsubishiMelsecConnectionOptions Options,
    IReadOnlyCollection<MitsubishiMelsecPoint> Points) : ICommunicationDriverRuntimePlan
{
    public string DriverType => MitsubishiMelsecDriverDescriptorProvider.DriverTypeId;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

public sealed class MitsubishiMelsecCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public string DriverType => MitsubishiMelsecDriverDescriptorProvider.DriverTypeId;

    public CommunicationDriverRuntimePlanningResult Plan(EngineeringPackage package, DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<EngineeringDriverIssue>();
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("MELSEC_DATASOURCE_DRIVER_MISMATCH", $"Data Source '{dataSource.Key}' declares '{dataSource.Driver}', not '{DriverType}'.", dataSource.Key));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }
        if (string.IsNullOrWhiteSpace(dataSource.Key)) issues.Add(Error("MELSEC_DATASOURCE_KEY_REQUIRED", "MELSEC Data Source key is required.", dataSource.Key));
        if (string.IsNullOrWhiteSpace(dataSource.Name)) issues.Add(Error("MELSEC_DATASOURCE_NAME_REQUIRED", "MELSEC Data Source name is required.", dataSource.Key));
        if (dataSource.SecretReferences is { Count: > 0 })
            issues.Add(Error("MELSEC_PROTECTED_MATERIAL_UNSUPPORTED", "The v1 MELSEC 3E profile does not consume protected material.", dataSource.Key));

        var settings = dataSource.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _ = MitsubishiMelsecConnectionOptions.TryCreate(settings, out var options, out var optionsError);
        if (optionsError is not null) issues.Add(Error("MELSEC_DATASOURCE_CONFIGURATION_INVALID", optionsError, dataSource.Key));

        var sourceTags = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceTags.Length == 0)
            issues.Add(Error("MELSEC_DATASOURCE_NO_TAGS", $"Enabled MELSEC Data Source '{dataSource.Key}' has no associated TAGs.", dataSource.Key));

        var points = new List<MitsubishiMelsecPoint>();
        foreach (var dto in sourceTags)
        {
            var point = BuildPoint(dataSource.Key, options, dto, issues);
            if (point is not null) points.Add(point);
        }

        foreach (var duplicate in points.GroupBy(point => point.Tag.Id).Where(group => group.Count() > 1))
            issues.Add(Error("MELSEC_TAG_ID_DUPLICATE", $"MELSEC Data Source '{dataSource.Key}' contains duplicate TAG ID '{duplicate.Key}'.", dataSource.Key));

        foreach (var group in points.GroupBy(point => point.Address.Area))
        {
            var ordered = group.OrderBy(point => point.Address.Number).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                var previousEnd = previous.Address.Number + Math.Max(0, MitsubishiMelsecValueCodec.WordSpan(previous.PhysicalType) - 1);
                if (current.Address.Number <= previousEnd)
                    issues.Add(Error(
                        "MELSEC_PHYSICAL_ADDRESS_OVERLAP",
                        $"MELSEC TAG '{current.Tag.Path}' overlaps another canonical TAG in device area {group.Key}.",
                        dataSource.Key,
                        current.Tag.Path));
            }
        }

        if (options is null || points.Count == 0 || issues.Any(issue => issue.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        return new CommunicationDriverRuntimePlanningResult(
            new MitsubishiMelsecCommunicationRuntimePlan(dataSource.Key, dataSource.Name, options, points.ToArray()), issues);
    }

    private static MitsubishiMelsecPoint? BuildPoint(
        string dataSourceKey,
        MitsubishiMelsecConnectionOptions? options,
        TagEngineeringDto dto,
        ICollection<EngineeringDriverIssue> issues)
    {
        if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
        {
            issues.Add(Error("MELSEC_TAG_STABLE_ID_REQUIRED", $"MELSEC TAG '{dto.Path}' requires a stable non-empty ID.", dataSourceKey, dto.Path));
            return null;
        }
        if (dto.AddressSelector is not null)
        {
            issues.Add(Error("MELSEC_TAG_ADDRESS_SELECTOR_UNSUPPORTED", $"MELSEC TAG '{dto.Path}' cannot use generic AddressSelector; device and bit identity are part of the portable address.", dataSourceKey, dto.Path));
            return null;
        }
        var binding = dto.CommunicationBinding;
        if (binding is null)
        {
            issues.Add(Error("MELSEC_TAG_BINDING_REQUIRED", $"MELSEC TAG '{dto.Path}' requires a canonical CommunicationBinding.", dataSourceKey, dto.Path));
            return null;
        }
        try { binding.Validate(); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            issues.Add(Error("MELSEC_TAG_BINDING_INVALID", $"MELSEC TAG '{dto.Path}' has an invalid CommunicationBinding: {ex.Message}", dataSourceKey, dto.Path));
            return null;
        }
        if (!string.Equals(binding.SchemaId, MitsubishiMelsecDriverDescriptorProvider.BindingSchemaId, StringComparison.Ordinal) ||
            binding.SchemaVersion != MitsubishiMelsecDriverDescriptorProvider.BindingSchemaVersion)
        {
            issues.Add(Error("MELSEC_TAG_BINDING_SCHEMA_UNSUPPORTED", $"MELSEC TAG '{dto.Path}' requires binding schema '{MitsubishiMelsecDriverDescriptorProvider.BindingSchemaId}' v{MitsubishiMelsecDriverDescriptorProvider.BindingSchemaVersion}.", dataSourceKey, dto.Path));
            return null;
        }
        if (!string.IsNullOrWhiteSpace(dto.Address) && !string.Equals(dto.Address, binding.PortableAddress, StringComparison.Ordinal))
        {
            issues.Add(Error("MELSEC_TAG_BINDING_ADDRESS_MISMATCH", $"MELSEC TAG '{dto.Path}' Address must exactly match CommunicationBinding.PortableAddress.", dataSourceKey, dto.Path));
            return null;
        }
        if (options is null) return null;
        if (!MitsubishiMelsecAddress.TryParse(binding.PortableAddress, options.FamilyProfile, options.RMaximumAddress, out var address, out var addressError))
        {
            issues.Add(Error("MELSEC_TAG_ADDRESS_INVALID", $"MELSEC TAG '{dto.Path}' has an invalid portable address: {addressError}", dataSourceKey, dto.Path));
            return null;
        }
        if (!TrySettingEnum(binding.EffectiveSettings, "physicalDataType", out MitsubishiMelsecPhysicalType physicalType))
        {
            issues.Add(Error("MELSEC_TAG_PHYSICAL_TYPE_REQUIRED", $"MELSEC TAG '{dto.Path}' requires physicalDataType Bit, Int16, UInt16, Int32, UInt32 or Float32.", dataSourceKey, dto.Path));
            return null;
        }
        var transform = binding.ValueTransform ?? new TagPhysicalValueTransform();
        try { transform.Validate(); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            issues.Add(Error("MELSEC_TAG_TRANSFORM_INVALID", $"MELSEC TAG '{dto.Path}' has an invalid physical transform: {ex.Message}", dataSourceKey, dto.Path));
            return null;
        }
        if (!TrySettingBool(binding.EffectiveSettings, "writable", false, out var configuredWritable))
        {
            issues.Add(Error("MELSEC_TAG_WRITABLE_INVALID", $"MELSEC TAG '{dto.Path}' writable must be true or false.", dataSourceKey, dto.Path));
            return null;
        }
        if (address!.IsBitDevice != (physicalType == MitsubishiMelsecPhysicalType.Bit))
        {
            issues.Add(Error("MELSEC_TAG_STORAGE_TYPE_MISMATCH", $"MELSEC TAG '{dto.Path}' physicalDataType {physicalType} does not match device storage area {address.Area}.", dataSourceKey, dto.Path));
            return null;
        }
        if (physicalType == MitsubishiMelsecPhysicalType.Bit && !transform.IsIdentity)
        {
            issues.Add(Error("MELSEC_TAG_BIT_TRANSFORM_UNSUPPORTED", $"MELSEC bit TAG '{dto.Path}' cannot use Byte Swap or Word Swap.", dataSourceKey, dto.Path));
            return null;
        }
        if (dto.DataType != MitsubishiMelsecValueCodec.CanonicalDataType(physicalType))
        {
            issues.Add(Error("MELSEC_TAG_CANONICAL_TYPE_MISMATCH", $"MELSEC TAG '{dto.Path}' canonical data type must be {MitsubishiMelsecValueCodec.CanonicalDataType(physicalType)} for {physicalType} physical storage.", dataSourceKey, dto.Path));
            return null;
        }
        if (configuredWritable && (dto.ReadOnly || address.Area == MitsubishiMelsecDeviceArea.X))
        {
            issues.Add(Error("MELSEC_TAG_WRITE_POLICY_INVALID", $"MELSEC TAG '{dto.Path}' is marked writable while TAG policy or physical input area forbids writes.", dataSourceKey, dto.Path));
            return null;
        }

        var metadata = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
        metadata["address"] = binding.PortableAddress;
        var tag = new TagDefinition(
            dto.Id.Value,
            dto.Name,
            dto.Path,
            dto.DataType,
            dto.Source,
            dto.EngineeringUnit,
            dto.Description,
            dto.ReadOnly,
            metadata,
            dto.AccessPolicy is null ? null : new TagAccessPolicy(dto.AccessPolicy.ReadRoles?.ToArray(), dto.AccessPolicy.WriteRoles?.ToArray(), dto.AccessPolicy.ConfigureRoles?.ToArray()),
            dto.AddressSelector,
            binding,
            dto.DataSourceId);
        return new MitsubishiMelsecPoint(tag, address, physicalType, configuredWritable, transform);
    }

    private static bool TrySettingEnum<T>(IReadOnlyDictionary<string, string> settings, string key, out T value) where T : struct, Enum
    {
        var raw = ReadSetting(settings, key);
        return Enum.TryParse(raw, ignoreCase: true, out value) && Enum.IsDefined(value);
    }

    private static bool TrySettingBool(IReadOnlyDictionary<string, string> settings, string key, bool fallback, out bool value)
    {
        var raw = ReadSetting(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) { value = fallback; return true; }
        return bool.TryParse(raw, out value);
    }

    private static string? ReadSetting(IReadOnlyDictionary<string, string> settings, string key)
    {
        if (settings.TryGetValue(key, out var exact)) return exact;
        foreach (var item in settings)
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) return item.Value;
        return null;
    }

    private static EngineeringDriverIssue Error(string code, string message, string? sourceKey, string? tagPath = null) =>
        new(code, message, sourceKey ?? string.Empty, tagPath, IsError: true);

}

public sealed class MitsubishiMelsecCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    public string DriverType => MitsubishiMelsecDriverDescriptorProvider.DriverTypeId;

    public ICommunicationDriver Create(ICommunicationDriverRuntimePlan plan, CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not MitsubishiMelsecCommunicationRuntimePlan melsecPlan)
            throw new ArgumentException($"MELSEC runtime factory requires {nameof(MitsubishiMelsecCommunicationRuntimePlan)}.", nameof(plan));
        if (!string.Equals(melsecPlan.DriverType, DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("MELSEC runtime plan driver type does not match its factory.", nameof(plan));
        return new MitsubishiMelsecDriver(
            melsecPlan.DataSourceKey,
            melsecPlan.Name,
            melsecPlan.Options,
            services.Cache,
            services.Registry,
            melsecPlan.Points,
            () => services.CanOwnExternalEffects);
    }
}
