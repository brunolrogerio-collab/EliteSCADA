using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HostResources;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record ZWaveJsCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    string HostResourceId,
    IReadOnlyDictionary<string, string> ProtectedKeyReferences,
    IReadOnlyCollection<ZWaveJsPoint> Points,
    Guid DataSourceId) : ICommunicationDriverRuntimePlan
{
    public string DriverType => ZWaveJsDriverDescriptorProvider.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

/// <summary>Compiles canonical, already-commissioned Z-Wave value bindings only.</summary>
public sealed class ZWaveJsCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    private static readonly HashSet<string> Settings = new(StringComparer.OrdinalIgnoreCase) { "hostResourceId" };
    private static readonly Dictionary<string, string> ProtectedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["s0Legacy"] = ZWaveJsDriverDescriptorProvider.S0LegacyPurpose,
        ["s2AccessControl"] = ZWaveJsDriverDescriptorProvider.S2AccessControlPurpose,
        ["s2Authenticated"] = ZWaveJsDriverDescriptorProvider.S2AuthenticatedPurpose,
        ["s2Unauthenticated"] = ZWaveJsDriverDescriptorProvider.S2UnauthenticatedPurpose
    };

    public string DriverType => ZWaveJsDriverDescriptorProvider.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(EngineeringPackage package, DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<EngineeringDriverIssue>();
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("ZWAVE_JS_DRIVER_TYPE_MISMATCH", $"Data Source '{dataSource.Key}' does not declare '{DriverType}'.", dataSource.Key));
            return new(null, issues);
        }
        if (dataSource.Id is not { } sourceId || sourceId == Guid.Empty)
            issues.Add(Error("ZWAVE_JS_DATASOURCE_ID_REQUIRED", "Z-Wave requires a stable Data Source GUID.", dataSource.Key));

        var settings = dataSource.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (settings.Keys.Any(key => !Settings.Contains(key)))
            issues.Add(Error("ZWAVE_JS_SETTING_UNSUPPORTED", "Z-Wave JS v1 accepts only a trusted hostResourceId; serial paths and radio configuration belong to the host-managed Host Resource.", dataSource.Key));
        var hostResourceId = ReadRequired(settings, "hostResourceId", dataSource.Key, issues);

        var secretReferences = dataSource.SecretReferences ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (secretReferences.Keys.Any(key => !ProtectedKeys.ContainsKey(key)))
            issues.Add(Error("ZWAVE_JS_SECRET_FIELD_UNSUPPORTED", "Z-Wave JS accepts only S0/S2 Authority-owned protected-material slots.", dataSource.Key));
        var protectedReferences = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in ProtectedKeys.Keys)
        {
            var reference = ReadOptional(secretReferences, key);
            if (reference is null) continue;
            if (!string.Equals(reference, HostMaterialSlotReference.Marker, StringComparison.Ordinal))
            {
                issues.Add(Error("ZWAVE_JS_PROTECTED_MATERIAL_REFERENCE_INVALID", $"'{key}' must use the portable host-owned Protected Material slot marker.", dataSource.Key));
                continue;
            }
            protectedReferences.Add(key, reference);
        }

        var normalized = EngineeringTagDataSourceAssociation.NormalizeForPlanner(package, dataSource);
        var sourceTags = normalized.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceTags.Length is < 1 or > 512)
            issues.Add(Error("ZWAVE_JS_TAG_COUNT_INVALID", "Z-Wave JS v1 requires 1 through 512 associated persistent-value TAGs.", dataSource.Key));

        var points = new List<ZWaveJsPoint>();
        foreach (var dto in sourceTags)
        {
            if (dto.Id is not { } tagId || tagId == Guid.Empty)
            {
                issues.Add(Error("ZWAVE_JS_TAG_ID_REQUIRED", $"TAG '{dto.Path}' requires a stable non-empty TAG ID.", dataSource.Key, dto.Path));
                continue;
            }
            if (dto.CommunicationBinding is not { } binding)
            {
                issues.Add(Error("ZWAVE_JS_TAG_BINDING_REQUIRED", $"TAG '{dto.Path}' requires a versioned Z-Wave JS CommunicationBinding.", dataSource.Key, dto.Path));
                continue;
            }
            try { binding.Validate(); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                issues.Add(Error("ZWAVE_JS_TAG_BINDING_INVALID", $"TAG '{dto.Path}' has an invalid CommunicationBinding: {exception.Message}", dataSource.Key, dto.Path));
                continue;
            }
            if (!string.Equals(binding.SchemaId, ZWaveJsDriverDescriptorProvider.TagBindingSchemaId, StringComparison.Ordinal) || binding.SchemaVersion != 1)
            {
                issues.Add(Error("ZWAVE_JS_TAG_BINDING_VERSION_UNSUPPORTED", $"TAG '{dto.Path}' must use {ZWaveJsDriverDescriptorProvider.TagBindingSchemaId} version 1.", dataSource.Key, dto.Path));
                continue;
            }
            if (binding.EffectiveSettings.Count != 0 || dto.AddressSelector is not null)
            {
                issues.Add(Error("ZWAVE_JS_TAG_BINDING_SETTINGS_UNSUPPORTED", $"TAG '{dto.Path}' cannot add generic selectors or settings in v1.", dataSource.Key, dto.Path));
                continue;
            }
            if (!string.IsNullOrWhiteSpace(dto.Address) && !string.Equals(dto.Address, binding.PortableAddress, StringComparison.Ordinal))
            {
                issues.Add(Error("ZWAVE_JS_TAG_ADDRESS_MISMATCH", $"TAG '{dto.Path}' Address must equal CommunicationBinding.PortableAddress.", dataSource.Key, dto.Path));
                continue;
            }
            if (!TryParseAddress(binding.PortableAddress, out var nodeId, out var endpoint, out var commandClass, out var property, out var propertyKey))
            {
                issues.Add(Error("ZWAVE_JS_TAG_ADDRESS_INVALID", $"TAG '{dto.Path}' must use a canonical node/endpoint/Command Class/property address.", dataSource.Key, dto.Path));
                continue;
            }
            if (!IsSupportedType(dto.DataType))
            {
                issues.Add(Error("ZWAVE_JS_TAG_TYPE_UNSUPPORTED", $"TAG '{dto.Path}' uses an unsupported Z-Wave process-value type.", dataSource.Key, dto.Path));
                continue;
            }
            if (!dto.ReadOnly && commandClass == 112)
            {
                issues.Add(Error("ZWAVE_JS_CONFIGURATION_WRITE_OUT_OF_SCOPE", $"TAG '{dto.Path}' cannot write Configuration Command Class values in v1.", dataSource.Key, dto.Path));
                continue;
            }
            var tag = BuildCanonicalTag(dto, dataSource.Key);
            points.Add(new ZWaveJsPoint(tag, binding.PortableAddress, nodeId, endpoint, commandClass,
                property, propertyKey, Writable: !dto.ReadOnly));
        }

        if (points.Select(point => point.Tag.Id).Distinct().Count() != points.Count)
            issues.Add(Error("ZWAVE_JS_TAG_ID_DUPLICATE", "Z-Wave JS Data Source contains duplicate stable TAG IDs.", dataSource.Key));
        if (points.Select(point => point.PortableAddress).Distinct(StringComparer.Ordinal).Count() != points.Count)
            issues.Add(Error("ZWAVE_JS_TAG_ADDRESS_DUPLICATE", "Two TAGs cannot bind the same Z-Wave process value.", dataSource.Key));

        if (issues.Any(issue => issue.IsError) || dataSource.Id is not { } stableId || string.IsNullOrWhiteSpace(hostResourceId))
            return new(null, issues);
        return new(new ZWaveJsCommunicationRuntimePlan(dataSource.Key, dataSource.Name, hostResourceId,
            protectedReferences, points, stableId), issues);
    }

    internal static bool TryParseAddress(string address, out int nodeId, out int endpoint, out int commandClass, out object property, out object? propertyKey)
    {
        nodeId = endpoint = commandClass = 0;
        property = string.Empty;
        propertyKey = null;
        if (string.IsNullOrEmpty(address) || address.Length > 1024) return false;
        var parts = address.Split('/');
        if (parts.Length != 12 || parts[0] != "node" || parts[2] != "endpoint" || parts[4] != "cc" ||
            parts[6] != "property" || parts[9] != "key" ||
            !ParseBounded(parts[1], 1, 232, out nodeId) || !ParseBounded(parts[3], 0, 127, out endpoint) ||
            !ParseBounded(parts[5], 1, 255, out commandClass) || IsDisallowedCommandClass(commandClass) ||
            !TryParseTyped(parts[7], parts[8], out property)) return false;
        if (parts[10] == "none" && parts[11] == "-") return true;
        if (!TryParseTyped(parts[10], parts[11], out var parsedKey)) return false;
        propertyKey = parsedKey;
        return true;
    }

    internal static string FormatAddress(int nodeId, int endpoint, int commandClass, object property, object? propertyKey)
    {
        var propertyToken = FormatTyped(property);
        var keyToken = propertyKey is null ? "none/-" : FormatTyped(propertyKey);
        return $"node/{nodeId}/endpoint/{endpoint}/cc/{commandClass}/property/{propertyToken}/key/{keyToken}";
    }

    private static bool IsSupportedType(TagDataType type) => type is TagDataType.Boolean or TagDataType.Int16 or TagDataType.Int32 or
        TagDataType.Int64 or TagDataType.Float or TagDataType.Double or TagDataType.String;

    private static bool IsDisallowedCommandClass(int value) => value is 76 or 91 or 98 or 99 or 102 or 113 or 131;

    private static bool ParseBounded(string value, int minimum, int maximum, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= minimum && result <= maximum &&
        result.ToString(CultureInfo.InvariantCulture) == value;

    private static bool TryParseTyped(string type, string value, out object parsed)
    {
        parsed = string.Empty;
        if (type == "number")
        {
            if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ||
                Math.Abs((double)number) > 9_007_199_254_740_991d || number.ToString(CultureInfo.InvariantCulture) != value) return false;
            parsed = number;
            return true;
        }
        if (type != "string") return false;
        try
        {
            var decoded = Uri.UnescapeDataString(value);
            if (decoded.Length == 0 || Encoding.UTF8.GetByteCount(decoded) > 256 || EncodeComponent(decoded) != value) return false;
            parsed = decoded;
            return true;
        }
        catch (UriFormatException) { return false; }
    }

    private static string FormatTyped(object value) => value switch
    {
        string text when text.Length > 0 && Encoding.UTF8.GetByteCount(text) <= 256 => $"string/{EncodeComponent(text)}",
        int integer => $"number/{integer.ToString(CultureInfo.InvariantCulture)}",
        long integer when Math.Abs((double)integer) <= 9_007_199_254_740_991d => $"number/{integer.ToString(CultureInfo.InvariantCulture)}",
        _ => throw new ArgumentException("Z-Wave property values must be bounded strings or safe integers.", nameof(value))
    };

    private static string EncodeComponent(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var builder = new StringBuilder(bytes.Length);
        foreach (var octet in bytes)
        {
            if (octet is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9' ||
                octet is (byte)'-' or (byte)'_' or (byte)'.' or (byte)'!' or (byte)'~' or (byte)'*' or (byte)'\'' or (byte)'(' or (byte)')')
                builder.Append((char)octet);
            else builder.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }

    private static string? ReadRequired(IReadOnlyDictionary<string, string> values, string key, string sourceKey, List<EngineeringDriverIssue> issues)
    {
        var value = ReadOptional(values, key);
        if (!string.IsNullOrWhiteSpace(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal)) return value;
        issues.Add(Error("ZWAVE_JS_SETTING_REQUIRED", $"Required setting '{key}' is missing or invalid.", sourceKey));
        return null;
    }

    private static string? ReadOptional(IReadOnlyDictionary<string, string> values, string key) =>
        values.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    private static TagDefinition BuildCanonicalTag(TagEngineeringDto dto, string sourceKey)
    {
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
        return new TagDefinition(dto.Id!.Value, dto.Name, dto.Path, dto.DataType, sourceKey, dto.EngineeringUnit, dto.Description,
            dto.ReadOnly, metadata, access, dto.AddressSelector, dto.CommunicationBinding, dto.DataSourceId);
    }

    private static EngineeringDriverIssue Error(string code, string message, string sourceKey, string? path = null) => new(code, message, sourceKey, path);
}

public sealed class ZWaveJsDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverType = "zwave.js.controller";
    public const string SchemaId = "elitescada.driver.zwave.js.controller.configuration";
    public const string TagBindingSchemaId = "elitescada.driver.zwave.js.controller";
    public const string S0LegacyPurpose = "zwave.network-key.s0-legacy";
    public const string S2AccessControlPurpose = "zwave.network-key.s2-access-control";
    public const string S2AuthenticatedPurpose = "zwave.network-key.s2-authenticated";
    public const string S2UnauthenticatedPurpose = "zwave.network-key.s2-unauthenticated";

    public CommunicationDriverTypeDescriptor Descriptor { get; } = new(
        DriverType,
        "Z-Wave JS Controller",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.None,
        AcquisitionModes: [DriverAcquisitionMode.Hybrid],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            SchemaId, 1,
            DataSourceFields:
            [
                new("hostResourceId", DriverConfigurationValueKind.Identifier, Required: true),
                new("s0Legacy", DriverConfigurationValueKind.SecretReference),
                new("s2AccessControl", DriverConfigurationValueKind.SecretReference),
                new("s2Authenticated", DriverConfigurationValueKind.SecretReference),
                new("s2Unauthenticated", DriverConfigurationValueKind.SecretReference)
            ],
            TagBindingFields: []),
        Description: "Managed loopback-only zwave-js-server bridge for commissioned persistent values; no provisioning or controller administration.",
        TagBindingSchemaId: TagBindingSchemaId,
        TagBindingSchemaVersion: 1,
        IntegrationDomains: [IntegrationDomain.Building, IntegrationDomain.Residential],
        ConnectionModel: DriverConnectionModel.HostSerial,
        ExternalDependencies: [new DriverExternalDependencyDescriptor(
            DriverExternalDependencyKind.OptionalSidecar,
            "Node.js 22.13+ or 24+ on Windows or Linux x64",
            "Exact-locked zwave-js-server 3.11.0 / zwave-js 15.31.1 managed loopback sidecar.")]);
}

public sealed record ZWaveJsSidecarDeploymentOptions(string NodeExecutable, string ArtifactDirectory, string StateDirectory)
{
    public static ZWaveJsSidecarDeploymentOptions Default => new(
        "node", Path.Combine(AppContext.BaseDirectory, "sidecars", "zwave-js"),
        Path.Combine(AppContext.BaseDirectory, "data", "sidecars", "zwave-js"));
}

public sealed class ZWaveJsCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly HostResourceRegistry _resources;
    private readonly HostResourceLeaseCoordinator _resourceLeases;
    private readonly ZWaveJsSidecarDeploymentOptions _deployment;
    private readonly Func<ZWaveJsCommunicationRuntimePlan, CommunicationDriverRuntimeServices, IZWaveJsSidecarSession>? _sessionFactory;

    public ZWaveJsCommunicationRuntimeFactory(HostResourceRegistry resources, HostResourceLeaseCoordinator resourceLeases,
        ZWaveJsSidecarDeploymentOptions deployment,
        Func<ZWaveJsCommunicationRuntimePlan, CommunicationDriverRuntimeServices, IZWaveJsSidecarSession>? sessionFactory = null)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _resourceLeases = resourceLeases ?? throw new ArgumentNullException(nameof(resourceLeases));
        _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
        _sessionFactory = sessionFactory;
        if (string.IsNullOrWhiteSpace(deployment.NodeExecutable) || string.IsNullOrWhiteSpace(deployment.ArtifactDirectory) ||
            string.IsNullOrWhiteSpace(deployment.StateDirectory)) throw new ArgumentException("Z-Wave JS sidecar deployment configuration is incomplete.", nameof(deployment));
    }

    public string DriverType => ZWaveJsDriverDescriptorProvider.DriverType;

    public ICommunicationDriver Create(ICommunicationDriverRuntimePlan plan, CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not ZWaveJsCommunicationRuntimePlan zwavePlan) throw new ArgumentException("Z-Wave JS factory requires a Z-Wave JS runtime plan.", nameof(plan));
        var resourceId = new HostResourceId(zwavePlan.HostResourceId);
        var resource = _resources.GetRequired(resourceId);
        var (rfRegion, nvmIdentity) = ValidateController(resource);
        if (_sessionFactory is not null) return new ZWaveJsCommunicationDriver(zwavePlan, services, _sessionFactory(zwavePlan, services), rfRegion);

        var dataSourceId = zwavePlan.DataSourceId.ToString("D");
        var instanceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{services.ProjectKey}\0{dataSourceId}"))).ToLowerInvariant();
        var instanceId = $"zwave.js.{instanceHash[..24]}";
        var stateDirectory = Path.Combine(_deployment.StateDirectory, instanceHash);
        var pointManifest = JsonSerializer.Serialize(zwavePlan.Points.Select(point => new
        {
            tagId = point.Tag.Id,
            address = point.PortableAddress,
            dataType = point.Tag.DataType.ToString(),
            writable = point.Writable && !point.Tag.ReadOnly
        }));
        var protectedSettings = zwavePlan.ProtectedKeyReferences.Select(reference =>
            new ManagedSidecarProtectedSettingReference(reference.Key, DriverType, dataSourceId,
                PurposeFor(reference.Key), reference.Value)).ToArray();
        var definition = new ManagedSidecarDefinition(
            instanceId,
            new ManagedSidecarArtifactIdentity("elitescada.zwave.js", "1.0.0", "node-22.13.0-or-24-plus", 1),
            new ManagedSidecarCompatibilityMetadata("elitescada.zwave.js", ["1.0.0"], "node-22.13.0-or-24-plus", 1, 1),
            new ManagedSidecarPackagingMetadata([ManagedSidecarPackageTargets.WindowsX64, ManagedSidecarPackageTargets.LinuxX64]),
            new ManagedSidecarConfiguration(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["hostResourceId"] = resource.ResourceId.Value,
                ["rfRegion"] = rfRegion,
                ["nvmIdentity"] = nvmIdentity,
                ["points"] = pointManifest
            }, protectedSettings),
            new ManagedSidecarPersistentStateOwnership(instanceId, 1, $"zwave-js-{instanceHash[..24]}", resourceId, RequiresBackupBeforeUpgrade: false));

        var rpc = new ZWaveJsRpcServer();
        var processFactory = new SystemManagedSidecarProcessFactory((_, materials, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) ||
                !System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.Equals(System.Runtime.InteropServices.Architecture.X64))
                throw new PlatformNotSupportedException("Z-Wave JS v1 requires Windows or Linux x64.");
            if (!Directory.Exists(_deployment.ArtifactDirectory)) throw new DirectoryNotFoundException("Z-Wave JS sidecar artifact directory is missing.");
            Directory.CreateDirectory(stateDirectory);
            var keyBytes = new Dictionary<string, int[]>(StringComparer.Ordinal);
            byte[]? startupJson = null;
            try
            {
                foreach (var pair in zwavePlan.ProtectedKeyReferences)
                {
                    if (!materials.TryGetValue(pair.Key, out var material) || material.Length != 16)
                        throw new InvalidOperationException($"Z-Wave protected {pair.Key} key was not resolved through Protected Material or has an invalid length.");
                    keyBytes.Add(pair.Key, material.ToArray().Select(value => (int)value).ToArray());
                }
                startupJson = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    type = "bootstrap",
                    configuration = new
                    {
                        serialPath = resource.Locator.Value,
                        cacheDirectory = stateDirectory,
                        nvmIdentity
                    },
                    points = zwavePlan.Points.Select(point => new
                    {
                        tagId = point.Tag.Id,
                        address = point.PortableAddress,
                        dataType = point.Tag.DataType.ToString(),
                        writable = point.Writable && !point.Tag.ReadOnly
                    }),
                    keys = keyBytes
                });
                var startupInput = new byte[startupJson.Length + 1];
                startupJson.CopyTo(startupInput, 0);
                startupInput[^1] = (byte)'\n';
                var environment = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ZWAVE_RPC_HOST"] = "127.0.0.1",
                    ["ZWAVE_RPC_PORT"] = rpc.Port.ToString(CultureInfo.InvariantCulture),
                    ["ZWAVE_RPC_TOKEN"] = rpc.Token
                };
                return ValueTask.FromResult(new ManagedSidecarProcessStartSpec(_deployment.NodeExecutable,
                    ["src/sidecar.mjs"], _deployment.ArtifactDirectory, environment, [rpc.Token], "stop", startupInput));
            }
            finally
            {
                if (startupJson is not null) CryptographicOperations.ZeroMemory(startupJson);
                foreach (var values in keyBytes.Values) Array.Clear(values);
            }
        });
        var protectedFactory = new ManagedSidecarProtectedMaterialProcessFactory(services, processFactory);
        var supervisor = new ManagedSidecarSupervisor(definition, protectedFactory);
        var binding = new ManagedSidecarRuntimeBinding(zwavePlan.Name, definition, services, _resources, _resourceLeases, supervisor);
        return new ZWaveJsCommunicationDriver(zwavePlan, services, new ZWaveJsManagedSidecarSession(binding, rpc), rfRegion);
    }

    private static string PurposeFor(string key) => key switch
    {
        "s0Legacy" => ZWaveJsDriverDescriptorProvider.S0LegacyPurpose,
        "s2AccessControl" => ZWaveJsDriverDescriptorProvider.S2AccessControlPurpose,
        "s2Authenticated" => ZWaveJsDriverDescriptorProvider.S2AuthenticatedPurpose,
        "s2Unauthenticated" => ZWaveJsDriverDescriptorProvider.S2UnauthenticatedPurpose,
        _ => throw new ArgumentException("Unknown Z-Wave Protected Material slot.", nameof(key))
    };

    private static (string RfRegion, string NvmIdentity) ValidateController(HostResourceDescriptor resource)
    {
        if (resource.ResourceKind != HostResourceKinds.ZWaveController) throw new InvalidOperationException($"Host Resource '{resource.ResourceId}' is not a ZWaveController.");
        if (resource.Locator.Kind != HostResourceLocatorKind.SerialPort) throw new NotSupportedException("Z-Wave JS v1 requires a serial ZWaveController Host Resource.");
        if (resource.Availability != HostResourceAvailability.Available) throw new InvalidOperationException($"ZWaveController Host Resource '{resource.ResourceId}' is not available.");
        var metadata = resource.Metadata ?? throw new InvalidOperationException("ZWaveController Host Resource metadata is missing.");
        if (!metadata.TryGetValue(ZWaveControllerHostResource.RfRegionMetadataKey, out var rfRegion) || string.IsNullOrWhiteSpace(rfRegion) ||
            !metadata.TryGetValue(ZWaveControllerHostResource.NvmIdentityMetadataKey, out var nvmIdentity) ||
            !System.Text.RegularExpressions.Regex.IsMatch(nvmIdentity, "^homeid:0x[0-9a-fA-F]{8}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new InvalidOperationException("ZWaveController Host Resource must carry RF-region and canonical NVM identity metadata.");
        return (rfRegion, nvmIdentity);
    }
}

public sealed class ZWaveJsCommunicationDriver : ICommunicationDriver, ICommunicationDiagnosticsSource, ICommunicationDriverReadinessSource
{
    private readonly ZWaveJsCommunicationRuntimePlan _plan;
    private readonly CommunicationDriverRuntimeServices _services;
    private readonly IZWaveJsSidecarSession _session;
    private readonly string _rfRegion;
    private readonly IReadOnlyDictionary<Guid, ZWaveJsPoint> _pointsById;
    private readonly ConcurrentDictionary<Guid, ZWaveJsObservation> _startupObservations = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastCommunicationAt;
    private string? _lastError;
    private long _requests, _successes, _failures, _reads, _writes, _updates;
    private int _running, _starting, _disposed;

    public ZWaveJsCommunicationDriver(ZWaveJsCommunicationRuntimePlan plan, CommunicationDriverRuntimeServices services,
        IZWaveJsSidecarSession session, string rfRegion)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _rfRegion = rfRegion;
        _services.Validate();
        _pointsById = plan.Points.ToDictionary(point => point.Tag.Id);
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, _stateChangedAt);
        _session.ObservationReceived += OnObservation;
    }

    public string DriverId => _plan.DataSourceKey;
    public string Name => _plan.Name;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _plan.Tags;
    public DriverCapabilities Capabilities => DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _running) != 0) throw new InvalidOperationException($"Z-Wave Data Source '{DriverId}' is already active.");
            if (!_services.CanAcquireInputs) throw new InvalidOperationException("Z-Wave Standby Runtime cannot open the controller or sidecar.");
            foreach (var point in _plan.Points) _services.Registry.Upsert(point.Tag);
            SetStatus(DriverState.Starting, null);
            Volatile.Write(ref _starting, 1);
            var sessionStarted = false;
            try
            {
                await _session.StartAsync(cancellationToken).ConfigureAwait(false);
                sessionStarted = true;
                Volatile.Write(ref _running, 1);
                Volatile.Write(ref _starting, 0);
                foreach (var observation in _startupObservations.Values.OrderBy(item => item.ObservedAt))
                {
                    if (_pointsById.TryGetValue(observation.TagId, out var point))
                        await PublishStateAsync(point, ConvertValue(observation.Value, point.Tag.DataType), "zwave.js.device-report", cancellationToken).ConfigureAwait(false);
                }
                _startupObservations.Clear();
                SetStatus(DriverState.Running, null);
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _running, 0);
                Volatile.Write(ref _starting, 0);
                _startupObservations.Clear();
                if (sessionStarted)
                {
                    try { await _session.StopAsync(CancellationToken.None).ConfigureAwait(false); }
                    catch { /* Preserve the startup error; host lifecycle will retain sidecar diagnostics. */ }
                }
                Interlocked.Increment(ref _failures);
                _lastError = Sanitize(exception.Message);
                SetStatus(DriverState.Faulted, _lastError);
                throw;
            }
        }
        finally { _lifecycleGate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _running) == 0) return;
            SetStatus(DriverState.Stopping, null);
            try
            {
                await _session.StopAsync(cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _running, 0);
                SetStatus(DriverState.Stopped, null);
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _running, 0);
                Interlocked.Increment(ref _failures);
                _lastError = Sanitize(exception.Message);
                SetStatus(DriverState.Faulted, _lastError);
                throw;
            }
        }
        finally { _lifecycleGate.Release(); }
    }

    public async ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        var point = GetPoint(tagId);
        if (Volatile.Read(ref _running) == 0 || !_services.CanAcquireInputs)
            return _services.Cache.TryGet(tagId, out var cached) ? cached : null;
        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _reads);
        try
        {
            var json = await _session.ReadAsync(point, cancellationToken).ConfigureAwait(false);
            return await PublishStateAsync(point, ConvertValue(json, point.Tag.DataType), "zwave.js.device-readback", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RecordFailure(exception);
            await PublishBadCommunicationAsync(point, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        var point = GetPoint(tagId);
        if (point.Tag.ReadOnly || !point.Writable) throw new InvalidOperationException($"Z-Wave TAG '{point.Tag.Path}' is read-only.");
        var requested = ValidateValue(point.Tag.DataType, value);
        if (Volatile.Read(ref _running) == 0 || !_services.CanOwnExternalEffects)
            throw new InvalidOperationException("Z-Wave Runtime does not currently own external effects.");
        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _writes);
        try
        {
            var json = await _session.WriteAndReadBackAsync(point, requested, cancellationToken).ConfigureAwait(false);
            var observed = ConvertValue(json, point.Tag.DataType);
            if (!Equals(requested, observed))
                throw new ZWaveJsWriteUnconfirmedException(point.Tag.Id, "READBACK_MISMATCH");
            await PublishStateAsync(point, observed, "zwave.js.confirmed-write", cancellationToken).ConfigureAwait(false);
        }
        catch (ZWaveJsWriteUnconfirmedException exception)
        {
            RecordFailure(exception);
            await PublishUncertainAsync(point).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) { RecordFailure(exception); throw; }
    }

    public CommunicationDriverReadinessSnapshot GetCommunicationReadiness() => new(DriverId, ZWaveJsDriverDescriptorProvider.DriverType,
        Volatile.Read(ref _running) != 0 ? CommunicationDriverReadinessState.Ready :
            Status.State == DriverState.Faulted ? CommunicationDriverReadinessState.Faulted : CommunicationDriverReadinessState.Stopped,
        DateTimeOffset.UtcNow, _lastError,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["controllerResourceId"] = _plan.HostResourceId,
            ["rfRegion"] = _rfRegion,
            ["stateAuthority"] = "device-report-or-fresh-poll",
            ["scope"] = "commissioned-persistent-values-only"
        });

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var source = _session.GetCommunicationDiagnostics();
        return source with
        {
            DataSourceKey = DriverId,
            DataSourceName = Name,
            DriverType = ZWaveJsDriverDescriptorProvider.DriverType,
            RuntimeInstanceId = _runtimeInstanceId,
            State = Status.State switch
            {
                DriverState.Starting => CommunicationDriverOperationalState.Starting,
                DriverState.Running => CommunicationDriverOperationalState.Healthy,
                DriverState.Stopping => CommunicationDriverOperationalState.Stopping,
                DriverState.Faulted => CommunicationDriverOperationalState.Faulted,
                _ => CommunicationDriverOperationalState.Stopped
            },
            StateChangedAt = _stateChangedAt,
            LastSuccessfulCommunicationAt = _lastCommunicationAt ?? source.LastSuccessfulCommunicationAt,
            LastError = _lastError ?? source.LastError,
            AssociatedTagCount = _plan.Points.Count,
            TagQuality = BuildQualitySummary(),
            Counters = source.Counters with
            {
                Requests = Interlocked.Read(ref _requests), SuccessfulOperations = Interlocked.Read(ref _successes),
                FailedOperations = Interlocked.Read(ref _failures), ReadOperations = Interlocked.Read(ref _reads),
                WriteOperations = Interlocked.Read(ref _writes), UpdatesPublished = Interlocked.Read(ref _updates)
            },
            ProtocolDetails = new Dictionary<string, string>(source.ProtocolDetails ?? new Dictionary<string, string>(), StringComparer.Ordinal)
            {
                ["protocol"] = "Z-Wave JS",
                ["controllerResourceId"] = _plan.HostResourceId,
                ["rfRegion"] = _rfRegion,
                ["stateAuthority"] = "device-report-or-fresh-poll",
                ["physicalCompatibilityClaim"] = "deferred-L4"
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _session.ObservationReceived -= OnObservation;
        try { await StopAsync().ConfigureAwait(false); }
        finally
        {
            try { await _session.DisposeAsync().ConfigureAwait(false); }
            finally { _lifecycleGate.Dispose(); }
        }
    }

    private ZWaveJsPoint GetPoint(Guid tagId) => _pointsById.TryGetValue(tagId, out var point)
        ? point : throw new KeyNotFoundException($"Z-Wave TAG '{tagId}' is not part of Data Source '{DriverId}'.");

    private void OnObservation(ZWaveJsObservation observation)
    {
        if (!_services.CanAcquireInputs || !_pointsById.ContainsKey(observation.TagId)) return;
        if (Volatile.Read(ref _running) == 0)
        {
            if (Volatile.Read(ref _starting) != 0) _startupObservations[observation.TagId] = observation;
            return;
        }
        if (!_pointsById.TryGetValue(observation.TagId, out var point)) return;
        _ = PublishObservationAsync(point, observation);
    }

    private async Task PublishObservationAsync(ZWaveJsPoint point, ZWaveJsObservation observation)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _running) == 0 || !_services.CanAcquireInputs) return;
            await PublishStateAsync(point, ConvertValue(observation.Value, point.Tag.DataType), "zwave.js.device-report", CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) { RecordFailure(exception); }
        finally { _lifecycleGate.Release(); }
    }

    private async ValueTask<TagValue?> PublishStateAsync(ZWaveJsPoint point, object value, string source, CancellationToken cancellationToken)
    {
        var updated = await _services.Cache.UpdateAsync(point.Tag, TagValue.Good(point.Tag.Id, value, source), cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _successes);
        if (updated is not null) Interlocked.Increment(ref _updates);
        _lastCommunicationAt = DateTimeOffset.UtcNow;
        _lastError = null;
        return updated;
    }

    private async ValueTask PublishBadCommunicationAsync(ZWaveJsPoint point, CancellationToken cancellationToken) =>
        _ = await _services.Cache.UpdateAsync(point.Tag, new TagValue(point.Tag.Id, null, DateTimeOffset.UtcNow, TagQuality.BadCommunication, "zwave.js"), cancellationToken).ConfigureAwait(false);

    private async ValueTask PublishUncertainAsync(ZWaveJsPoint point)
    {
        _services.Cache.TryGet(point.Tag.Id, out var current);
        await _services.Cache.UpdateAsync(point.Tag,
            new TagValue(point.Tag.Id, current?.Value, DateTimeOffset.UtcNow, TagQuality.Uncertain, "zwave.js.write-outcome-unknown"),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static object ValidateValue(TagDataType type, object? value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        var valid = type switch
        {
            TagDataType.Boolean => value is bool,
            TagDataType.Int16 => value is short,
            TagDataType.Int32 => value is int,
            TagDataType.Int64 => value is long,
            TagDataType.Float => value is float number && float.IsFinite(number),
            TagDataType.Double => value is double number && double.IsFinite(number),
            TagDataType.String => value is string text && text.Length <= 4096,
            _ => false
        };
        return valid ? value : throw new ArgumentException($"Z-Wave write value does not match the canonical {type} TAG type.", nameof(value));
    }

    private static object ConvertValue(JsonElement value, TagDataType type) => type switch
    {
        TagDataType.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
        TagDataType.Int16 when value.TryGetInt16(out var number) => number,
        TagDataType.Int32 when value.TryGetInt32(out var number) => number,
        TagDataType.Int64 when value.TryGetInt64(out var number) => number,
        TagDataType.Float when value.TryGetSingle(out var number) && float.IsFinite(number) => number,
        TagDataType.Double when value.TryGetDouble(out var number) && double.IsFinite(number) => number,
        TagDataType.String when value.ValueKind == JsonValueKind.String && value.GetString() is { Length: <= 4096 } text => text,
        _ => throw new InvalidDataException("Z-Wave returned a process value with a type or range mismatch.")
    };

    private void RecordFailure(Exception exception)
    {
        Interlocked.Increment(ref _failures);
        _lastError = Sanitize(exception.Message);
    }

    private void SetStatus(DriverState state, string? message)
    {
        _stateChangedAt = DateTimeOffset.UtcNow;
        Status = new DriverStatus(DriverId, Name, state, _stateChangedAt, message, Interlocked.Read(ref _updates));
    }

    private CommunicationTagQualitySummary BuildQualitySummary()
    {
        var counts = new int[9];
        foreach (var point in _plan.Points)
        {
            if (!_services.Cache.TryGet(point.Tag.Id, out var current) || current is null) { counts[8]++; continue; }
            counts[current.Quality switch
            {
                TagQuality.Good => 0, TagQuality.BadCommunication => 1, TagQuality.Uncertain => 2,
                TagQuality.Bad => 3, TagQuality.BadConfiguration => 4, TagQuality.BadDevice => 5,
                TagQuality.Stale => 6, TagQuality.Disabled => 7, _ => 1
            }]++;
        }
        return new(counts[0], counts[1], counts[2], counts[3], counts[4], counts[5], counts[6], counts[7], counts[8]);
    }

    private static string Sanitize(string message) => ManagedSidecarLogSanitizer.Sanitize(message, maximumLength: 256) ?? "Z-Wave operation failed.";
}
