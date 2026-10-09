using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Panasonic;
using Scada.Drivers.Serial;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record PanasonicMewtocolCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    string RuntimeDriverType,
    PanasonicMewtocolConnectionOptions Options,
    IReadOnlyCollection<PanasonicMewtocolPoint> Points,
    IReadOnlyCollection<PanasonicMewtocolPollBatch> PollBatches) : ICommunicationDriverRuntimePlan
{
    public string DriverType => RuntimeDriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

public sealed class PanasonicMewtocolCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public PanasonicMewtocolCommunicationRuntimePlanner(string driverType)
    {
        _ = PanasonicMewtocolDriverDescriptorProvider.For(driverType);
        DriverType = driverType;
    }

    public string DriverType { get; }

    public CommunicationDriverRuntimePlanningResult Plan(EngineeringPackage package, DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<EngineeringDriverIssue>();
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("PANASONIC_DATASOURCE_DRIVER_MISMATCH", $"Data Source '{dataSource.Key}' declares '{dataSource.Driver}', not '{DriverType}'.", dataSource.Key));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }
        if (string.IsNullOrWhiteSpace(dataSource.Key)) issues.Add(Error("PANASONIC_DATASOURCE_KEY_REQUIRED", "Panasonic Data Source key is required.", dataSource.Key));
        if (string.IsNullOrWhiteSpace(dataSource.Name)) issues.Add(Error("PANASONIC_DATASOURCE_NAME_REQUIRED", "Panasonic Data Source name is required.", dataSource.Key));
        if (dataSource.SecretReferences is { Count: > 0 })
            issues.Add(Error("PANASONIC_PROTECTED_MATERIAL_UNSUPPORTED", "MEWTOCOL-COM v1 does not use protected material.", dataSource.Key));

        var settings = dataSource.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _ = PanasonicMewtocolConnectionOptions.TryCreate(DriverType, settings, out var options, out var optionsError);
        if (optionsError is not null) issues.Add(Error("PANASONIC_DATASOURCE_CONFIGURATION_INVALID", optionsError, dataSource.Key));

        var sourceTags = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceTags.Length == 0)
            issues.Add(Error("PANASONIC_DATASOURCE_NO_TAGS", $"Enabled Panasonic Data Source '{dataSource.Key}' has no associated TAGs.", dataSource.Key));

        var points = new List<PanasonicMewtocolPoint>();
        foreach (var dto in sourceTags)
        {
            var point = BuildPoint(dataSource.Key, options, dto, issues);
            if (point is not null) points.Add(point);
        }

        foreach (var duplicate in points.GroupBy(point => point.Tag.Id).Where(group => group.Count() > 1))
            issues.Add(Error("PANASONIC_TAG_ID_DUPLICATE", $"Panasonic Data Source '{dataSource.Key}' contains duplicate TAG ID '{duplicate.Key}'.", dataSource.Key));

        foreach (var group in points.GroupBy(point => point.Address.Area))
        {
            var ordered = group.OrderBy(point => point.Address.Number).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                if (ordered[index].Address.Number == ordered[index - 1].Address.Number)
                    issues.Add(Error("PANASONIC_PHYSICAL_ADDRESS_OVERLAP", $"Panasonic TAG '{ordered[index].Tag.Path}' overlaps another TAG in {group.Key}.", dataSource.Key, ordered[index].Tag.Path));
            }
        }

        if (options is null || points.Count == 0 || issues.Any(issue => issue.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        var batches = BuildPollBatches(points, options.Capabilities);
        return new CommunicationDriverRuntimePlanningResult(
            new PanasonicMewtocolCommunicationRuntimePlan(dataSource.Key, dataSource.Name, DriverType, options, points.ToArray(), batches), issues);
    }

    private static PanasonicMewtocolPoint? BuildPoint(
        string dataSourceKey,
        PanasonicMewtocolConnectionOptions? options,
        TagEngineeringDto dto,
        ICollection<EngineeringDriverIssue> issues)
    {
        if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
        {
            issues.Add(Error("PANASONIC_TAG_STABLE_ID_REQUIRED", $"Panasonic TAG '{dto.Path}' requires a stable non-empty ID.", dataSourceKey, dto.Path));
            return null;
        }
        if (dto.AddressSelector is not null)
        {
            issues.Add(Error("PANASONIC_TAG_ADDRESS_SELECTOR_UNSUPPORTED", $"Panasonic TAG '{dto.Path}' cannot use generic AddressSelector; contact/word identity is part of its portable address.", dataSourceKey, dto.Path));
            return null;
        }
        var binding = dto.CommunicationBinding;
        if (binding is null)
        {
            issues.Add(Error("PANASONIC_TAG_BINDING_REQUIRED", $"Panasonic TAG '{dto.Path}' requires a canonical CommunicationBinding.", dataSourceKey, dto.Path));
            return null;
        }
        try { binding.Validate(); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            issues.Add(Error("PANASONIC_TAG_BINDING_INVALID", $"Panasonic TAG '{dto.Path}' has an invalid CommunicationBinding: {ex.Message}", dataSourceKey, dto.Path));
            return null;
        }
        if (!string.Equals(binding.SchemaId, PanasonicMewtocolDriverDescriptorProvider.BindingSchemaId, StringComparison.Ordinal) ||
            binding.SchemaVersion != PanasonicMewtocolDriverDescriptorProvider.BindingSchemaVersion)
        {
            issues.Add(Error("PANASONIC_TAG_BINDING_SCHEMA_UNSUPPORTED", $"Panasonic TAG '{dto.Path}' requires binding schema '{PanasonicMewtocolDriverDescriptorProvider.BindingSchemaId}' v{PanasonicMewtocolDriverDescriptorProvider.BindingSchemaVersion}.", dataSourceKey, dto.Path));
            return null;
        }
        if (!string.IsNullOrWhiteSpace(dto.Address) && !string.Equals(dto.Address, binding.PortableAddress, StringComparison.Ordinal))
        {
            issues.Add(Error("PANASONIC_TAG_BINDING_ADDRESS_MISMATCH", $"Panasonic TAG '{dto.Path}' Address must match CommunicationBinding.PortableAddress.", dataSourceKey, dto.Path));
            return null;
        }
        if (options is null) return null;
        if (!PanasonicMewtocolAddress.TryParse(binding.PortableAddress, options.FamilyProfile, out var address, out var addressError))
        {
            issues.Add(Error("PANASONIC_TAG_ADDRESS_INVALID", $"Panasonic TAG '{dto.Path}' has an invalid portable address: {addressError}", dataSourceKey, dto.Path));
            return null;
        }
        var rawType = PanasonicMewtocolConnectionOptions.Get(binding.EffectiveSettings, "physicalDataType");
        if (!Enum.TryParse<PanasonicMewtocolPhysicalType>(rawType, true, out var physicalType) || !Enum.IsDefined(physicalType))
        {
            issues.Add(Error("PANASONIC_TAG_PHYSICAL_TYPE_REQUIRED", $"Panasonic TAG '{dto.Path}' requires physicalDataType Boolean, UInt16 or Int16.", dataSourceKey, dto.Path));
            return null;
        }
        var transform = binding.ValueTransform ?? new TagPhysicalValueTransform();
        try { transform.Validate(); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            issues.Add(Error("PANASONIC_TAG_TRANSFORM_INVALID", $"Panasonic TAG '{dto.Path}' has an invalid physical transform: {ex.Message}", dataSourceKey, dto.Path));
            return null;
        }
        if (transform.WordSwap || (physicalType == PanasonicMewtocolPhysicalType.Boolean && !transform.IsIdentity))
        {
            issues.Add(Error("PANASONIC_TAG_TRANSFORM_UNSUPPORTED", $"Panasonic TAG '{dto.Path}' supports byte swap on one-word values only; contacts do not accept a transform.", dataSourceKey, dto.Path));
            return null;
        }
        if (address!.IsContact != (physicalType == PanasonicMewtocolPhysicalType.Boolean))
        {
            issues.Add(Error("PANASONIC_TAG_STORAGE_TYPE_MISMATCH", $"Panasonic TAG '{dto.Path}' physicalDataType does not match contact/word area {address.Area}.", dataSourceKey, dto.Path));
            return null;
        }
        var canonicalType = PanasonicMewtocolValueCodec.CanonicalDataType(physicalType);
        if (dto.DataType != canonicalType)
        {
            issues.Add(Error("PANASONIC_TAG_CANONICAL_TYPE_MISMATCH", $"Panasonic TAG '{dto.Path}' canonical data type must be {canonicalType} for {physicalType} physical storage.", dataSourceKey, dto.Path));
            return null;
        }
        if (!TrySettingBool(binding.EffectiveSettings, "writable", false, out var configuredWritable))
        {
            issues.Add(Error("PANASONIC_TAG_WRITABLE_INVALID", $"Panasonic TAG '{dto.Path}' writable must be true or false.", dataSourceKey, dto.Path));
            return null;
        }
        if (configuredWritable && (dto.ReadOnly || !address.IsWritable))
        {
            issues.Add(Error("PANASONIC_TAG_WRITE_POLICY_INVALID", $"Panasonic TAG '{dto.Path}' is writable while TAG policy or its physical area forbids writes.", dataSourceKey, dto.Path));
            return null;
        }

        var metadata = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
        metadata["address"] = address.PortableAddress;
        var tag = new TagDefinition(
            dto.Id.Value, dto.Name, dto.Path, dto.DataType, dto.Source, dto.EngineeringUnit,
            dto.Description, dto.ReadOnly, metadata,
            dto.AccessPolicy is null ? null : new TagAccessPolicy(dto.AccessPolicy.ReadRoles?.ToArray(), dto.AccessPolicy.WriteRoles?.ToArray(), dto.AccessPolicy.ConfigureRoles?.ToArray()),
            dto.AddressSelector, binding, dto.DataSourceId);
        return new PanasonicMewtocolPoint(tag, address, physicalType, configuredWritable, transform);
    }

    private static IReadOnlyCollection<PanasonicMewtocolPollBatch> BuildPollBatches(
        IReadOnlyCollection<PanasonicMewtocolPoint> points,
        PanasonicMewtocolFamilyCapabilities capabilities)
    {
        var batches = new List<PanasonicMewtocolPollBatch>();
        foreach (var group in points.GroupBy(point => point.Address.Area))
        {
            var ordered = group.OrderBy(point => point.Address.Number).ToArray();
            if (ordered[0].Address.IsContact)
            {
                for (var offset = 0; offset < ordered.Length; offset += capabilities.MaximumSparseContacts)
                {
                    var slice = ordered.Skip(offset).Take(capabilities.MaximumSparseContacts).ToArray();
                    batches.Add(new PanasonicMewtocolPollBatch(group.Key, true, slice[0].Address.Number, slice.Length, slice));
                }
                continue;
            }

            var cursor = 0;
            while (cursor < ordered.Length)
            {
                var start = cursor;
                var expectedNext = ordered[cursor].Address.Number + 1;
                cursor++;
                while (cursor < ordered.Length && cursor - start < capabilities.MaximumReadWords &&
                    ordered[cursor].Address.Number == expectedNext)
                {
                    expectedNext++;
                    cursor++;
                }
                var slice = ordered[start..cursor];
                batches.Add(new PanasonicMewtocolPollBatch(group.Key, false, slice[0].Address.Number, slice.Length, slice));
            }
        }
        return batches;
    }

    private static bool TrySettingBool(IReadOnlyDictionary<string, string> settings, string key, bool fallback, out bool value)
    {
        var raw = PanasonicMewtocolConnectionOptions.Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) { value = fallback; return true; }
        return bool.TryParse(raw, out value);
    }

    private static EngineeringDriverIssue Error(string code, string message, string? sourceKey, string? tagPath = null) =>
        new(code, message, sourceKey ?? string.Empty, tagPath, IsError: true);
}

public sealed class PanasonicMewtocolCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly HostSerialBusCoordinator _serialCoordinator;

    public PanasonicMewtocolCommunicationRuntimeFactory(string driverType, HostSerialBusCoordinator serialCoordinator)
    {
        _ = PanasonicMewtocolDriverDescriptorProvider.For(driverType);
        DriverType = driverType;
        _serialCoordinator = serialCoordinator ?? throw new ArgumentNullException(nameof(serialCoordinator));
    }

    public string DriverType { get; }

    public ICommunicationDriver Create(ICommunicationDriverRuntimePlan plan, CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not PanasonicMewtocolCommunicationRuntimePlan mewtocolPlan)
            throw new ArgumentException($"Panasonic runtime factory requires {nameof(PanasonicMewtocolCommunicationRuntimePlan)}.", nameof(plan));
        if (!string.Equals(mewtocolPlan.DriverType, DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Panasonic runtime plan driver type does not match its factory.", nameof(plan));

        IPanasonicMewtocolSession session = mewtocolPlan.Options.Transport switch
        {
            PanasonicMewtocolTransportKind.Tcp => new PanasonicMewtocolTcpSession(mewtocolPlan.Options),
            PanasonicMewtocolTransportKind.Serial => new PanasonicMewtocolSerialSession(mewtocolPlan.Options, _serialCoordinator, mewtocolPlan.DataSourceKey),
            _ => throw new ArgumentOutOfRangeException(nameof(plan))
        };
        return new PanasonicMewtocolDriver(
            mewtocolPlan.DataSourceKey, mewtocolPlan.Name, DriverType, mewtocolPlan.Options,
            services.Cache, services.Registry, mewtocolPlan.Points, mewtocolPlan.PollBatches,
            session,
            effectAuthority: () => services.CanOwnExternalEffects,
            inputAcquisitionAuthority: () => services.CanAcquireInputs);
    }
}
