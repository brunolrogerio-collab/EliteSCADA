using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Scada.Core.Tags;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HostResources;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record ZigbeeNativePoint(
    TagDefinition Tag,
    string IeeeAddress,
    int Endpoint,
    bool Writable)
{
    public string AddressKey => $"{IeeeAddress}/{Endpoint}";
}

public sealed record ZigbeeNativeCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    string HostResourceId,
    string NetworkIdentity,
    int PanId,
    string ExtendedPanId,
    int Channel,
    int BaudRate,
    string NetworkKeyReference,
    IReadOnlyCollection<ZigbeeNativePoint> Points) : ICommunicationDriverRuntimePlan
{
    public string DriverType => ZigbeeNativeDriverDescriptorProvider.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

/// <summary>
/// Compiles the bounded Boolean genOnOff/onOff TAG contract. The planner reads
/// only canonical Data Source/TAG configuration and leaves secrets unresolved.
/// </summary>
public sealed class ZigbeeNativeCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    private static readonly Regex AddressPattern = new(
        "^(?:0x)?(?<ieee>[0-9a-fA-F]{16})/(?<endpoint>[0-9]{1,3})/genOnOff/onOff$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string DriverType => ZigbeeNativeDriverDescriptorProvider.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<EngineeringDriverIssue>();

        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("ZIGBEE_NATIVE_DRIVER_TYPE_MISMATCH",
                $"Data Source '{dataSource.Key}' does not declare '{DriverType}'.", dataSource.Key));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }

        var settings = dataSource.Settings ?? new Dictionary<string, string>();
        var hostResourceId = ReadRequired(settings, "hostResourceId", dataSource.Key, issues);
        var networkIdentity = ReadRequired(settings, "networkIdentity", dataSource.Key, issues);
        var extendedPanId = ReadRequired(settings, "extendedPanId", dataSource.Key, issues);
        var panIdText = ReadRequired(settings, "panId", dataSource.Key, issues);
        var channelText = ReadRequired(settings, "channel", dataSource.Key, issues);
        var baudText = ReadOptional(settings, "baudRate") ?? "115200";
        var networkKeyReference = ReadRequired(dataSource.SecretReferences ?? new Dictionary<string, string>(),
            "networkKey", dataSource.Key, issues);

        if (!TryParsePanId(panIdText, out var panId))
            issues.Add(Error("ZIGBEE_NATIVE_PAN_ID_INVALID", "panId must be a decimal or 0x-prefixed value from 1 through 65534.", dataSource.Key));
        if (!TryNormalizeExtendedPanId(extendedPanId, out var normalizedExtendedPanId))
            issues.Add(Error("ZIGBEE_NATIVE_EXTENDED_PAN_ID_INVALID", "extendedPanId must contain exactly 16 hexadecimal digits.", dataSource.Key));
        if (!int.TryParse(channelText, NumberStyles.None, CultureInfo.InvariantCulture, out var channel) || channel is < 11 or > 26)
            issues.Add(Error("ZIGBEE_NATIVE_CHANNEL_INVALID", "channel must be a single Zigbee channel from 11 through 26.", dataSource.Key));
        if (!int.TryParse(baudText, NumberStyles.None, CultureInfo.InvariantCulture, out var baudRate) || baudRate is < 9600 or > 1_000_000)
            issues.Add(Error("ZIGBEE_NATIVE_BAUD_RATE_INVALID", "baudRate must be between 9600 and 1000000.", dataSource.Key));

        var normalizedPackage = EngineeringTagDataSourceAssociation.NormalizeForPlanner(package, dataSource);
        var sourceTags = normalizedPackage.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (sourceTags.Length is < 1 or > 512)
            issues.Add(Error("ZIGBEE_NATIVE_TAG_COUNT_INVALID", "Native Zigbee requires 1 through 512 associated state TAGs.", dataSource.Key));

        var points = new List<ZigbeeNativePoint>();
        foreach (var dto in sourceTags)
        {
            var path = dto.Path;
            var binding = dto.CommunicationBinding;
            if (binding is null)
            {
                issues.Add(Error("ZIGBEE_NATIVE_TAG_BINDING_REQUIRED",
                    $"TAG '{path}' requires a versioned Native Zigbee CommunicationBinding.", dataSource.Key, path));
                continue;
            }

            try
            {
                binding.Validate();
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                issues.Add(Error("ZIGBEE_NATIVE_TAG_BINDING_INVALID",
                    $"TAG '{path}' has an invalid CommunicationBinding: {exception.Message}", dataSource.Key, path));
                continue;
            }

            if (!string.Equals(binding.SchemaId, ZigbeeNativeDriverDescriptorProvider.TagBindingSchemaId, StringComparison.Ordinal) ||
                binding.SchemaVersion != 1)
            {
                issues.Add(Error("ZIGBEE_NATIVE_TAG_BINDING_VERSION_UNSUPPORTED",
                    $"TAG '{path}' must use {ZigbeeNativeDriverDescriptorProvider.TagBindingSchemaId} version 1.", dataSource.Key, path));
                continue;
            }

            if (binding.EffectiveSettings.Count != 0 || dto.AddressSelector is not null)
            {
                issues.Add(Error("ZIGBEE_NATIVE_TAG_BINDING_SETTINGS_UNSUPPORTED",
                    $"TAG '{path}' cannot add binding settings or a generic AddressSelector in v1.", dataSource.Key, path));
                continue;
            }

            var match = AddressPattern.Match(binding.PortableAddress);
            if (!match.Success ||
                !int.TryParse(match.Groups["endpoint"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var endpoint) ||
                endpoint is < 1 or > 240)
            {
                issues.Add(Error("ZIGBEE_NATIVE_TAG_ADDRESS_INVALID",
                    $"TAG '{path}' address must be <16 digit IEEE>/<endpoint>/genOnOff/onOff.", dataSource.Key, path));
                continue;
            }

            if (dto.DataType != TagDataType.Boolean)
            {
                issues.Add(Error("ZIGBEE_NATIVE_TAG_TYPE_UNSUPPORTED",
                    $"TAG '{path}' must use Boolean data type for genOnOff/onOff.", dataSource.Key, path));
                continue;
            }

            var ieeeAddress = match.Groups["ieee"].Value.ToLowerInvariant();
            var tag = BuildCanonicalTag(dto, dataSource.Key);
            points.Add(new ZigbeeNativePoint(tag, ieeeAddress, endpoint, Writable: !dto.ReadOnly));
        }

        if (points.Select(point => point.AddressKey).Distinct(StringComparer.Ordinal).Count() != points.Count)
            issues.Add(Error("ZIGBEE_NATIVE_TAG_ADDRESS_DUPLICATE", "Two TAGs cannot bind the same IEEE address and endpoint in v1.", dataSource.Key));

        if (issues.Any(issue => issue.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        return new CommunicationDriverRuntimePlanningResult(
            new ZigbeeNativeCommunicationRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                hostResourceId!,
                networkIdentity!,
                panId,
                normalizedExtendedPanId!,
                channel,
                baudRate,
                networkKeyReference!,
                points),
            issues);
    }

    private static string? ReadRequired(
        IReadOnlyDictionary<string, string> values,
        string key,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues)
    {
        var value = ReadOptional(values, key);
        if (!string.IsNullOrWhiteSpace(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal))
            return value;
        issues.Add(Error("ZIGBEE_NATIVE_SETTING_REQUIRED", $"Required setting '{key}' is missing or invalid.", dataSourceKey));
        return null;
    }

    private static string? ReadOptional(IReadOnlyDictionary<string, string> values, string key) =>
        values.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    private static bool TryParsePanId(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parsed = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value)
            : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        return parsed && value is >= 1 and <= 65534;
    }

    private static bool TryNormalizeExtendedPanId(string? text, out string? normalized)
    {
        normalized = null;
        if (text is null) return false;
        var value = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        if (value.Length != 16 || !value.All(Uri.IsHexDigit)) return false;
        normalized = value.ToLowerInvariant();
        return true;
    }

    private static TagDefinition BuildCanonicalTag(TagEngineeringDto dto, string dataSourceKey)
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
            if (dto.Historian.Deadband.HasValue) metadata["historian.deadband"] = dto.Historian.Deadband.Value.ToString(CultureInfo.InvariantCulture);
            if (dto.Historian.PeriodMilliseconds.HasValue) metadata["historian.periodMs"] = dto.Historian.PeriodMilliseconds.Value.ToString(CultureInfo.InvariantCulture);
            if (dto.Historian.MaximumPeriodMilliseconds.HasValue) metadata["historian.maxPeriodMs"] = dto.Historian.MaximumPeriodMilliseconds.Value.ToString(CultureInfo.InvariantCulture);
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
            dataSourceKey,
            dto.EngineeringUnit,
            dto.Description,
            dto.ReadOnly,
            metadata,
            access,
            dto.AddressSelector,
            dto.CommunicationBinding,
            dto.DataSourceId);
    }

    private static EngineeringDriverIssue Error(string code, string message, string dataSourceKey, string? tagPath = null) =>
        new(code, message, dataSourceKey, tagPath);
}

public sealed class ZigbeeNativeDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverType = "zigbee.native";
    public const string SchemaId = "elitescada.driver.zigbee.native.configuration";
    public const string TagBindingSchemaId = "elitescada.driver.zigbee.native";

    public CommunicationDriverTypeDescriptor Descriptor { get; } = new(
        DriverType,
        "Native Zigbee",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.None,
        AcquisitionModes: [DriverAcquisitionMode.Hybrid],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            SchemaId,
            SchemaVersion: 1,
            DataSourceFields:
            [
                new("hostResourceId", DriverConfigurationValueKind.Identifier, Required: true),
                new("networkIdentity", DriverConfigurationValueKind.Identifier, Required: true),
                new("panId", DriverConfigurationValueKind.Integer, Required: true, Minimum: 1, Maximum: 65534),
                new("extendedPanId", DriverConfigurationValueKind.Identifier, Required: true),
                new("channel", DriverConfigurationValueKind.Integer, Required: true, Minimum: 11, Maximum: 26),
                new("baudRate", DriverConfigurationValueKind.Integer, DefaultValue: "115200", Minimum: 9600, Maximum: 1000000),
                new("networkKey", DriverConfigurationValueKind.SecretReference, Required: true)
            ],
            TagBindingFields:
            [
                new("ieeeAddress", DriverConfigurationValueKind.Identifier, Required: true),
                new("endpoint", DriverConfigurationValueKind.Integer, Required: true, Minimum: 1, Maximum: 240),
                new("cluster", DriverConfigurationValueKind.Enum, Required: true, AllowedValues: ["genOnOff"]),
                new("attribute", DriverConfigurationValueKind.Enum, Required: true, AllowedValues: ["onOff"])
            ]),
        Description: "Managed TI zStack/CC2652 coordinator sidecar for bounded Boolean on/off state TAGs.",
        TagBindingSchemaId: TagBindingSchemaId,
        TagBindingSchemaVersion: 1,
        IntegrationDomains: [IntegrationDomain.Building, IntegrationDomain.Residential, IntegrationDomain.IoT],
        ConnectionModel: DriverConnectionModel.HostRadio,
        ExternalDependencies: [new DriverExternalDependencyDescriptor(
            DriverExternalDependencyKind.OptionalSidecar,
            "Node.js 24.21.0 on Linux x64",
            "Managed sidecar pinned to zigbee-herdsman 3.3.2 and converters dataset 23.7.0.")]);
}

public sealed record ZigbeeNativeSidecarDeploymentOptions(
    string NodeExecutable,
    string ArtifactDirectory,
    string StateDirectory);

public sealed class ZigbeeNativeCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly HostResourceRegistry _resourceRegistry;
    private readonly HostResourceLeaseCoordinator _resourceLeases;
    private readonly ZigbeeNativeSidecarDeploymentOptions _deployment;

    public ZigbeeNativeCommunicationRuntimeFactory(
        HostResourceRegistry resourceRegistry,
        HostResourceLeaseCoordinator resourceLeases,
        ZigbeeNativeSidecarDeploymentOptions deployment)
    {
        _resourceRegistry = resourceRegistry ?? throw new ArgumentNullException(nameof(resourceRegistry));
        _resourceLeases = resourceLeases ?? throw new ArgumentNullException(nameof(resourceLeases));
        _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
        if (string.IsNullOrWhiteSpace(_deployment.NodeExecutable) ||
            string.IsNullOrWhiteSpace(_deployment.ArtifactDirectory) ||
            string.IsNullOrWhiteSpace(_deployment.StateDirectory))
            throw new ArgumentException("Native Zigbee sidecar deployment paths are required.", nameof(deployment));
    }

    public string DriverType => ZigbeeNativeDriverDescriptorProvider.DriverType;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not ZigbeeNativeCommunicationRuntimePlan zigbeePlan)
            throw new ArgumentException("Native Zigbee factory requires a Native Zigbee runtime plan.", nameof(plan));

        var resourceId = new HostResourceId(zigbeePlan.HostResourceId);
        var resource = _resourceRegistry.GetRequired(resourceId);
        ValidateCoordinator(resource, zigbeePlan);

        var instanceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{services.ProjectKey}\0{zigbeePlan.DataSourceKey}"))).ToLowerInvariant();
        var instanceId = $"zigbee.native.{instanceHash[..24]}";
        var stateDirectory = Path.Combine(_deployment.StateDirectory, instanceHash);
        var databasePath = Path.Combine(stateDirectory, "herdsman.db");
        var protectedReference = new ManagedSidecarProtectedSettingReference(
            "networkKey",
            "data-source",
            zigbeePlan.DataSourceKey,
            "zigbee.native.network-key",
            zigbeePlan.NetworkKeyReference);
        var definition = new ManagedSidecarDefinition(
            instanceId,
            new ManagedSidecarArtifactIdentity("elitescada.zigbee.native", "1.0.0", "24.21.0", 1),
            new ManagedSidecarCompatibilityMetadata("elitescada.zigbee.native", ["1.0.0"], "24.21.0", 1, 1),
            new ManagedSidecarPackagingMetadata([ManagedSidecarPackageTargets.LinuxX64]),
            new ManagedSidecarConfiguration(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["hostResourceId"] = resource.ResourceId.Value,
                    ["networkIdentity"] = zigbeePlan.NetworkIdentity,
                    ["panId"] = zigbeePlan.PanId.ToString(CultureInfo.InvariantCulture),
                    ["extendedPanId"] = zigbeePlan.ExtendedPanId,
                    ["channel"] = zigbeePlan.Channel.ToString(CultureInfo.InvariantCulture),
                    ["baudRate"] = zigbeePlan.BaudRate.ToString(CultureInfo.InvariantCulture)
                },
                [protectedReference]),
            new ManagedSidecarPersistentStateOwnership(
                instanceId,
                1,
                $"zigbee-native-{instanceHash[..24]}",
                resourceId,
                RequiresBackupBeforeUpgrade: false));

        var rpc = new ZigbeeNativeRpcServer();
        var processFactory = new SystemManagedSidecarProcessFactory((_, protectedSettings, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsLinux() || !System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.Equals(System.Runtime.InteropServices.Architecture.X64))
                throw new PlatformNotSupportedException("Native Zigbee v1 sidecar is qualified for Linux x64 only.");
            if (!Directory.Exists(_deployment.ArtifactDirectory))
                throw new DirectoryNotFoundException("Native Zigbee sidecar artifact directory is missing.");
            if (!protectedSettings.TryGetValue("networkKey", out var protectedNetworkKey))
                throw new InvalidOperationException("Native Zigbee network key was not resolved through Protected Material.");

            var networkKey = NormalizeNetworkKey(protectedNetworkKey);
            Directory.CreateDirectory(stateDirectory);
            var allowedPoints = System.Text.Json.JsonSerializer.Serialize(zigbeePlan.Points.Select(point => new
            {
                ieee = point.IeeeAddress,
                endpoint = point.Endpoint
            }));
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ZIGBEE_RPC_HOST"] = "127.0.0.1",
                ["ZIGBEE_RPC_PORT"] = rpc.Port.ToString(CultureInfo.InvariantCulture),
                ["ZIGBEE_RPC_TOKEN"] = rpc.Token,
                ["ZIGBEE_SERIAL_PATH"] = resource.Locator.Value,
                ["ZIGBEE_PAN_ID"] = zigbeePlan.PanId.ToString(CultureInfo.InvariantCulture),
                ["ZIGBEE_EXTENDED_PAN_ID"] = zigbeePlan.ExtendedPanId,
                ["ZIGBEE_CHANNEL"] = zigbeePlan.Channel.ToString(CultureInfo.InvariantCulture),
                ["ZIGBEE_BAUD_RATE"] = zigbeePlan.BaudRate.ToString(CultureInfo.InvariantCulture),
                ["ZIGBEE_DATABASE_PATH"] = databasePath,
                ["ZIGBEE_ALLOWED_POINTS"] = allowedPoints,
                ["ZIGBEE_NETWORK_KEY"] = networkKey
            };
            var spec = new ManagedSidecarProcessStartSpec(
                _deployment.NodeExecutable,
                ["src/sidecar.mjs"],
                _deployment.ArtifactDirectory,
                environment,
                [networkKey, rpc.Token],
                "stop");
            return ValueTask.FromResult(spec);
        });

        var protectedFactory = new ManagedSidecarProtectedMaterialProcessFactory(services, processFactory);
        var supervisor = new ManagedSidecarSupervisor(definition, protectedFactory);
        var binding = new ManagedSidecarRuntimeBinding(
            zigbeePlan.Name,
            definition,
            services,
            _resourceRegistry,
            _resourceLeases,
            supervisor);
        var runtime = new ZigbeeNativeManagedSidecarSession(binding, rpc, zigbeePlan.Points);
        return new ZigbeeNativeCommunicationDriver(zigbeePlan, services, runtime);
    }

    private static void ValidateCoordinator(HostResourceDescriptor resource, ZigbeeNativeCommunicationRuntimePlan plan)
    {
        if (resource.ResourceKind != HostResourceKinds.ZigbeeCoordinator)
            throw new InvalidOperationException($"Host Resource '{resource.ResourceId}' is not a ZigbeeCoordinator.");
        if (resource.Locator.Kind != HostResourceLocatorKind.SerialPort)
            throw new NotSupportedException("Native Zigbee v1 requires a serial Host Resource locator.");
        if (resource.Availability != HostResourceAvailability.Available)
            throw new InvalidOperationException($"Zigbee coordinator Host Resource '{resource.ResourceId}' is not available.");

        var family = NormalizeToken(resource.Family);
        var model = NormalizeToken(resource.Model);
        if (!family.Contains("zstack", StringComparison.Ordinal) || !model.Contains("cc2652", StringComparison.Ordinal))
            throw new NotSupportedException("Native Zigbee v1 accepts only TI zStack/CC2652 Host Resources.");

        var hostNetworkIdentity = resource.Metadata?.FirstOrDefault(pair =>
            string.Equals(pair.Key, ZigbeeCoordinatorHostResource.NetworkIdentityMetadataKey, StringComparison.OrdinalIgnoreCase)).Value;
        if (string.IsNullOrWhiteSpace(hostNetworkIdentity) ||
            !string.Equals(hostNetworkIdentity, plan.NetworkIdentity, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Data Source networkIdentity does not match the coordinator Host Resource evidence.");
        }
    }

    private static string NormalizeToken(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string NormalizeNetworkKey(ReadOnlyMemory<byte> protectedMaterial)
    {
        if (protectedMaterial.Length == 16)
            return Convert.ToHexString(protectedMaterial.Span).ToLowerInvariant();

        var text = Encoding.ASCII.GetString(protectedMaterial.Span);
        if (text.Length != 32 || !text.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Protected Zigbee network key must be 16 bytes or 32 hexadecimal characters.");
        return text.ToLowerInvariant();
    }
}

public sealed class ZigbeeNativeCommunicationDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource,
    ICommunicationDriverReadinessSource
{
    private readonly ZigbeeNativeCommunicationRuntimePlan _plan;
    private readonly CommunicationDriverRuntimeServices _services;
    private readonly INativeZigbeeSidecarSession _runtime;
    private readonly IReadOnlyDictionary<Guid, ZigbeeNativePoint> _pointsByTagId;
    private readonly IReadOnlyDictionary<string, ZigbeeNativePoint> _pointsByAddress;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private DateTimeOffset _stateChangedAt;
    private DateTimeOffset? _lastCommunicationAt;
    private string? _lastError;
    private long _requests;
    private long _successes;
    private long _failures;
    private long _reads;
    private long _writes;
    private long _updates;
    private int _running;
    private int _disposed;

    public ZigbeeNativeCommunicationDriver(
        ZigbeeNativeCommunicationRuntimePlan plan,
        CommunicationDriverRuntimeServices services,
        INativeZigbeeSidecarSession runtime)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _services.Validate();
        _pointsByTagId = plan.Points.ToDictionary(point => point.Tag.Id);
        _pointsByAddress = plan.Points.ToDictionary(point => point.AddressKey, StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;
        _stateChangedAt = now;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, now);
        _runtime.ObservationReceived += OnObservation;
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
            if (Volatile.Read(ref _running) != 0)
                throw new InvalidOperationException($"Native Zigbee Data Source '{DriverId}' is already active.");
            cancellationToken.ThrowIfCancellationRequested();
            if (!_services.CanOwnExternalEffects)
                throw new InvalidOperationException("Native Zigbee Standby Runtime cannot open a physical coordinator.");

            foreach (var point in _plan.Points) _services.Registry.Upsert(point.Tag);
            SetStatus(DriverState.Starting, null);
            try
            {
                await _runtime.StartAsync(cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _running, 1);
                SetStatus(DriverState.Running, null);
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref _failures);
                _lastError = Sanitize(exception.Message);
                SetStatus(DriverState.Faulted, _lastError);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
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
                await _runtime.StopAsync(cancellationToken).ConfigureAwait(false);
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
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        if (!_pointsByTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"Native Zigbee TAG '{tagId}' is not part of Data Source '{DriverId}'.");
        if (Volatile.Read(ref _running) == 0 || !_services.CanOwnExternalEffects)
            return _services.Cache.TryGet(tagId, out var cached) ? cached : null;

        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _reads);
        try
        {
            var state = await _runtime.ReadStateAsync(point, cancellationToken).ConfigureAwait(false);
            return await PublishStateAsync(point, state, "zigbee.native.readback", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RecordOperationFailure(exception);
            await PublishBadCommunicationAsync(point, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        if (!_pointsByTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"Native Zigbee TAG '{tagId}' is not part of Data Source '{DriverId}'.");
        if (point.Tag.ReadOnly || !point.Writable)
            throw new InvalidOperationException($"Native Zigbee TAG '{point.Tag.Path}' is read-only.");
        if (value is not bool requested)
            throw new ArgumentException("Native Zigbee onOff writes require a Boolean value.", nameof(value));
        if (Volatile.Read(ref _running) == 0 || !_services.CanOwnExternalEffects)
            throw new InvalidOperationException("Native Zigbee Runtime does not currently own physical effects.");

        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _writes);
        try
        {
            var observed = await _runtime.WriteAndReadBackAsync(point, requested, cancellationToken).ConfigureAwait(false);
            await PublishStateAsync(point, observed, "zigbee.native.write-readback", cancellationToken).ConfigureAwait(false);
            if (observed != requested)
                throw new InvalidOperationException($"Native Zigbee write for TAG '{point.Tag.Path}' did not reconcile to the requested state.");
        }
        catch (Exception exception)
        {
            RecordOperationFailure(exception);
            throw;
        }
    }

    public CommunicationDriverReadinessSnapshot GetCommunicationReadiness()
    {
        var active = Volatile.Read(ref _running) != 0;
        var now = DateTimeOffset.UtcNow;
        return new CommunicationDriverReadinessSnapshot(
            DriverId,
            ZigbeeNativeDriverDescriptorProvider.DriverType,
            active ? CommunicationDriverReadinessState.Ready :
                Status.State == DriverState.Faulted ? CommunicationDriverReadinessState.Faulted : CommunicationDriverReadinessState.Stopped,
            now,
            _lastError,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["coordinatorFamily"] = "TI zStack/CC2652",
                ["capability"] = "genOnOff/onOff:Boolean",
                ["stateAuthority"] = "report/readback",
                ["sidecarArtifact"] = "elitescada.zigbee.native@1.0.0"
            });
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var snapshot = _runtime.GetCommunicationDiagnostics();
        return snapshot with
        {
            DataSourceKey = DriverId,
            DataSourceName = Name,
            DriverType = ZigbeeNativeDriverDescriptorProvider.DriverType,
            RuntimeInstanceId = _runtimeInstanceId,
            LastSuccessfulCommunicationAt = _lastCommunicationAt ?? snapshot.LastSuccessfulCommunicationAt,
            LastError = _lastError ?? snapshot.LastError,
            AssociatedTagCount = _plan.Points.Count,
            TagQuality = BuildQualitySummary(),
            Counters = snapshot.Counters with
            {
                Requests = Interlocked.Read(ref _requests),
                SuccessfulOperations = Interlocked.Read(ref _successes),
                FailedOperations = Interlocked.Read(ref _failures),
                ReadOperations = Interlocked.Read(ref _reads),
                WriteOperations = Interlocked.Read(ref _writes),
                UpdatesPublished = Interlocked.Read(ref _updates)
            },
            ProtocolDetails = new Dictionary<string, string>(snapshot.ProtocolDetails ?? new Dictionary<string, string>(), StringComparer.Ordinal)
            {
                ["protocol"] = "Zigbee",
                ["coordinatorFamily"] = "TI zStack/CC2652",
                ["capability"] = "genOnOff/onOff:Boolean",
                ["stateAuthority"] = "device report/readback",
                ["physicalCompatibilityClaim"] = "deferred-L4"
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _runtime.ObservationReceived -= OnObservation;
        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            try { await _runtime.DisposeAsync().ConfigureAwait(false); }
            finally { _lifecycleGate.Dispose(); }
        }
    }

    private void OnObservation(NativeZigbeeObservation observation)
    {
        if (!_pointsByAddress.TryGetValue(observation.AddressKey, out var point)) return;
        _ = PublishObservationAsync(point, observation);
    }

    private async Task PublishObservationAsync(ZigbeeNativePoint point, NativeZigbeeObservation observation)
    {
        try
        {
            await PublishStateAsync(point, observation.Value, $"zigbee.native.{observation.Source}", CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RecordOperationFailure(exception);
        }
    }

    private async ValueTask<TagValue?> PublishStateAsync(
        ZigbeeNativePoint point,
        bool value,
        string source,
        CancellationToken cancellationToken)
    {
        var sample = TagValue.Good(point.Tag.Id, value, source);
        var updated = await _services.Cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _successes);
        if (updated is not null) Interlocked.Increment(ref _updates);
        _lastCommunicationAt = DateTimeOffset.UtcNow;
        _lastError = null;
        return updated;
    }

    private async ValueTask PublishBadCommunicationAsync(ZigbeeNativePoint point, CancellationToken cancellationToken)
    {
        var failed = new TagValue(point.Tag.Id, null, DateTimeOffset.UtcNow, TagQuality.BadCommunication, "zigbee.native");
        await _services.Cache.UpdateAsync(point.Tag, failed, cancellationToken).ConfigureAwait(false);
    }

    private void RecordOperationFailure(Exception exception)
    {
        Interlocked.Increment(ref _failures);
        _lastError = Sanitize(exception.Message);
    }

    private void SetStatus(DriverState state, string? message)
    {
        var now = DateTimeOffset.UtcNow;
        _stateChangedAt = now;
        Status = new DriverStatus(DriverId, Name, state, now, message, Interlocked.Read(ref _updates));
    }

    private CommunicationTagQualitySummary BuildQualitySummary()
    {
        var good = 0;
        var badCommunication = 0;
        var uncertain = 0;
        var bad = 0;
        var badConfiguration = 0;
        var badDevice = 0;
        var stale = 0;
        var disabled = 0;
        var noCurrent = 0;
        foreach (var point in _plan.Points)
        {
            if (!_services.Cache.TryGet(point.Tag.Id, out var value) || value is null)
            {
                noCurrent++;
                continue;
            }
            switch (value.Quality)
            {
                case TagQuality.Good: good++; break;
                case TagQuality.BadCommunication: badCommunication++; break;
                case TagQuality.Uncertain: uncertain++; break;
                case TagQuality.Bad: bad++; break;
                case TagQuality.BadConfiguration: badConfiguration++; break;
                case TagQuality.BadDevice: badDevice++; break;
                case TagQuality.Stale: stale++; break;
                case TagQuality.Disabled: disabled++; break;
                default: badCommunication++; break;
            }
        }
        return new CommunicationTagQualitySummary(good, badCommunication, uncertain, bad, badConfiguration, badDevice, stale, disabled, noCurrent);
    }

    private static string Sanitize(string message) =>
        ManagedSidecarLogSanitizer.Sanitize(message, maximumLength: 256) ?? "Native Zigbee operation failed.";
}
