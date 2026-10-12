using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scada.Core.Tags;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HostResources;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record KnxIpPoint(
    TagDefinition Tag,
    string Dpt,
    string WriteGroupAddress,
    string StateGroupAddress,
    bool Writable);

public sealed record KnxIpCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    string GatewayHost,
    int GatewayPort,
    string IndividualAddress,
    int UserId,
    string UserPasswordReference,
    string DeviceAuthenticationReference,
    string DataSecureKeyringReference,
    IReadOnlyCollection<KnxIpPoint> Points,
    Guid? DataSourceId = null) : ICommunicationDriverRuntimePlan
{
    public string DriverType => KnxIpDriverDescriptorProvider.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

/// <summary>
/// Compiles the frozen bounded KNX/IP v1 contract. It deliberately accepts only
/// explicit Secure TCP tunneling and never resolves Protected Material itself.
/// </summary>
public sealed class KnxIpCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    private static readonly Regex GroupAddressPattern = new("^(?<main>[0-9]{1,2})/(?<middle>[0-9]{1,2})/(?<sub>[0-9]{1,3})$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex IndividualAddressPattern = new("^(?<area>[0-9]{1,2})\\.(?<line>[0-9]{1,2})\\.(?<device>[0-9]{1,3})$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedDpts = new(StringComparer.Ordinal)
    {
        "1.001", "1.002", "5.001", "9.001", "9.004", "9.007", "9.024"
    };

    public string DriverType => KnxIpDriverDescriptorProvider.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(EngineeringPackage package, DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<EngineeringDriverIssue>();
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("KNX_IP_DRIVER_TYPE_MISMATCH", $"Data Source '{dataSource.Key}' does not declare '{DriverType}'.", dataSource.Key));
            return new(null, issues);
        }

        if (!dataSource.Id.HasValue || dataSource.Id.Value == Guid.Empty)
            issues.Add(Error("KNX_IP_DATASOURCE_ID_REQUIRED", "KNX/IP requires a stable Data Source GUID.", dataSource.Key));

        var settings = dataSource.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (settings.Keys.Any(key => !new[] { "gatewayHost", "gatewayPort", "individualAddress", "userId" }
                .Contains(key, StringComparer.OrdinalIgnoreCase)))
            issues.Add(Error("KNX_IP_SETTING_UNSUPPORTED", "KNX/IP v1 accepts only gatewayHost, gatewayPort, individualAddress, and userId; credentials must use Authority-owned Protected Material.", dataSource.Key));
        var gatewayHost = ReadRequired(settings, "gatewayHost", dataSource.Key, issues);
        if (gatewayHost is not null && !IsHostName(gatewayHost))
            issues.Add(Error("KNX_IP_GATEWAY_HOST_INVALID", "gatewayHost must be a hostname or IP address, not a URL or path.", dataSource.Key));
        var portText = ReadOptional(settings, "gatewayPort") ?? "3671";
        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var gatewayPort) || gatewayPort is < 1 or > 65535)
            issues.Add(Error("KNX_IP_GATEWAY_PORT_INVALID", "gatewayPort must be a TCP port from 1 through 65535.", dataSource.Key));
        var individualAddress = ReadRequired(settings, "individualAddress", dataSource.Key, issues);
        if (individualAddress is not null && !IsIndividualAddress(individualAddress))
            issues.Add(Error("KNX_IP_INDIVIDUAL_ADDRESS_INVALID", "individualAddress must be an assigned KNX individual address in area.line.device notation.", dataSource.Key));
        var userIdText = ReadRequired(settings, "userId", dataSource.Key, issues);
        if (!int.TryParse(userIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId is < 1 or > 127)
            issues.Add(Error("KNX_IP_USER_ID_INVALID", "userId must be a Secure tunneling user ID from 1 through 127.", dataSource.Key));

        var secrets = dataSource.SecretReferences ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var userPassword = ReadRequired(secrets, "userPassword", dataSource.Key, issues);
        var deviceAuthentication = ReadRequired(secrets, "deviceAuthentication", dataSource.Key, issues);
        var dataSecureKeyring = ReadRequired(secrets, "dataSecureKeyring", dataSource.Key, issues);
        ValidatePortableSlot(userPassword, "userPassword", dataSource.Key, issues);
        ValidatePortableSlot(deviceAuthentication, "deviceAuthentication", dataSource.Key, issues);
        ValidatePortableSlot(dataSecureKeyring, "dataSecureKeyring", dataSource.Key, issues);
        if (secrets.Keys.Any(key => !new[] { "userPassword", "deviceAuthentication", "dataSecureKeyring" }.Contains(key, StringComparer.OrdinalIgnoreCase)))
            issues.Add(Error("KNX_IP_SECRET_FIELD_UNSUPPORTED", "KNX/IP v1 accepts only its three Authority-owned protected-material references.", dataSource.Key));

        var normalizedPackage = EngineeringTagDataSourceAssociation.NormalizeForPlanner(package, dataSource);
        var sourceTags = normalizedPackage.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceTags.Length is < 1 or > 512)
            issues.Add(Error("KNX_IP_TAG_COUNT_INVALID", "KNX/IP v1 requires 1 through 512 associated TAGs.", dataSource.Key));

        var points = new List<KnxIpPoint>();
        foreach (var dto in sourceTags)
        {
            if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
            {
                issues.Add(Error("KNX_IP_TAG_ID_REQUIRED", $"TAG '{dto.Path}' requires a stable non-empty TAG ID.", dataSource.Key, dto.Path));
                continue;
            }
            if (dto.AddressSelector is not null)
            {
                issues.Add(Error("KNX_IP_TAG_SELECTOR_UNSUPPORTED", $"TAG '{dto.Path}' cannot use a generic AddressSelector.", dataSource.Key, dto.Path));
                continue;
            }
            var binding = dto.CommunicationBinding;
            if (binding is null)
            {
                issues.Add(Error("KNX_IP_TAG_BINDING_REQUIRED", $"TAG '{dto.Path}' requires a versioned KNX/IP CommunicationBinding.", dataSource.Key, dto.Path));
                continue;
            }
            try { binding.Validate(); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                issues.Add(Error("KNX_IP_TAG_BINDING_INVALID", $"TAG '{dto.Path}' has an invalid CommunicationBinding: {exception.Message}", dataSource.Key, dto.Path));
                continue;
            }
            if (!string.Equals(binding.SchemaId, KnxIpDriverDescriptorProvider.TagBindingSchemaId, StringComparison.Ordinal) || binding.SchemaVersion != 1)
            {
                issues.Add(Error("KNX_IP_TAG_BINDING_VERSION_UNSUPPORTED", $"TAG '{dto.Path}' must use {KnxIpDriverDescriptorProvider.TagBindingSchemaId} version 1.", dataSource.Key, dto.Path));
                continue;
            }
            if (!string.IsNullOrWhiteSpace(dto.Address) && !string.Equals(dto.Address, binding.PortableAddress, StringComparison.Ordinal))
            {
                issues.Add(Error("KNX_IP_TAG_ADDRESS_MISMATCH", $"TAG '{dto.Path}' Address must equal CommunicationBinding.PortableAddress.", dataSource.Key, dto.Path));
                continue;
            }

            var bindingSettings = binding.EffectiveSettings;
            if (bindingSettings.Keys.Any(key => key is not "dpt" and not "stateGroupAddress") ||
                !bindingSettings.TryGetValue("dpt", out var dpt) || !SupportedDpts.Contains(dpt) ||
                !bindingSettings.TryGetValue("stateGroupAddress", out var stateGroupAddress))
            {
                issues.Add(Error("KNX_IP_TAG_BINDING_SETTINGS_INVALID", $"TAG '{dto.Path}' must declare only a supported dpt and stateGroupAddress.", dataSource.Key, dto.Path));
                continue;
            }
            if (!IsGroupAddress(binding.PortableAddress) || !IsGroupAddress(stateGroupAddress))
            {
                issues.Add(Error("KNX_IP_GROUP_ADDRESS_INVALID", $"TAG '{dto.Path}' must use three-level group addresses main/middle/sub.", dataSource.Key, dto.Path));
                continue;
            }
            var writeGroupAddress = binding.PortableAddress;
            if (!dto.ReadOnly && string.Equals(writeGroupAddress, stateGroupAddress, StringComparison.Ordinal))
            {
                issues.Add(Error("KNX_IP_WRITE_STATE_ADDRESS_COLLISION", $"Writable TAG '{dto.Path}' requires distinct write and authoritative state group addresses.", dataSource.Key, dto.Path));
                continue;
            }
            var booleanDpt = dpt is "1.001" or "1.002";
            if (booleanDpt ? dto.DataType != TagDataType.Boolean : dto.DataType is not TagDataType.Float and not TagDataType.Double)
            {
                issues.Add(Error("KNX_IP_TAG_TYPE_UNSUPPORTED", $"TAG '{dto.Path}' data type does not match DPT {dpt}.", dataSource.Key, dto.Path));
                continue;
            }
            var tag = BuildCanonicalTag(dto, dataSource.Key);
            points.Add(new KnxIpPoint(tag, dpt, writeGroupAddress, stateGroupAddress, !dto.ReadOnly));
        }

        if (points.Select(point => point.Tag.Id).Distinct().Count() != points.Count)
            issues.Add(Error("KNX_IP_TAG_ID_DUPLICATE", "KNX/IP Data Source contains duplicate stable TAG IDs.", dataSource.Key));
        foreach (var group in points.GroupBy(point => point.StateGroupAddress, StringComparer.Ordinal))
        {
            if (group.Select(point => point.Dpt).Distinct(StringComparer.Ordinal).Count() > 1)
                issues.Add(Error("KNX_IP_STATE_DPT_CONFLICT", $"State group address '{group.Key}' is bound with incompatible DPTs.", dataSource.Key));
        }
        var stateAddresses = points.Select(point => point.StateGroupAddress).ToHashSet(StringComparer.Ordinal);
        foreach (var group in points.Where(point => point.Writable).GroupBy(point => point.WriteGroupAddress, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
                issues.Add(Error("KNX_IP_WRITE_ADDRESS_DUPLICATE", $"Write group address '{group.Key}' is assigned to more than one writable TAG.", dataSource.Key));
            if (stateAddresses.Contains(group.Key))
                issues.Add(Error("KNX_IP_WRITE_STATE_ADDRESS_COLLISION", $"Write group address '{group.Key}' is also an authoritative state address.", dataSource.Key));
        }

        if (issues.Any(issue => issue.IsError)) return new(null, issues);
        return new(new KnxIpCommunicationRuntimePlan(
            dataSource.Key, dataSource.Name, gatewayHost!, gatewayPort, individualAddress!, userId,
            userPassword!, deviceAuthentication!, dataSecureKeyring!, points, dataSource.Id), issues);
    }

    private static bool IsHostName(string value) =>
        value.Length <= 253 && !value.Contains("://", StringComparison.Ordinal) && !value.Contains('/') && !value.Contains('\\') &&
        !value.Any(char.IsWhiteSpace) && Uri.CheckHostName(value) != UriHostNameType.Unknown;

    private static bool IsIndividualAddress(string value)
    {
        var match = IndividualAddressPattern.Match(value);
        return match.Success &&
               int.Parse(match.Groups["area"].Value, CultureInfo.InvariantCulture) is >= 0 and <= 15 &&
               int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture) is >= 0 and <= 15 &&
               int.Parse(match.Groups["device"].Value, CultureInfo.InvariantCulture) is >= 1 and <= 255;
    }

    private static bool IsGroupAddress(string value)
    {
        var match = GroupAddressPattern.Match(value);
        return match.Success &&
               int.Parse(match.Groups["main"].Value, CultureInfo.InvariantCulture) is >= 0 and <= 31 &&
               int.Parse(match.Groups["middle"].Value, CultureInfo.InvariantCulture) is >= 0 and <= 7 &&
               int.Parse(match.Groups["sub"].Value, CultureInfo.InvariantCulture) is >= 0 and <= 255;
    }

    private static string? ReadRequired(IReadOnlyDictionary<string, string> values, string key, string sourceKey, List<EngineeringDriverIssue> issues)
    {
        var value = ReadOptional(values, key);
        if (!string.IsNullOrWhiteSpace(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal)) return value;
        issues.Add(Error("KNX_IP_SETTING_REQUIRED", $"Required setting '{key}' is missing or invalid.", sourceKey));
        return null;
    }

    private static string? ReadOptional(IReadOnlyDictionary<string, string> values, string key) =>
        values.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    private static void ValidatePortableSlot(string? value, string setting, string sourceKey, List<EngineeringDriverIssue> issues)
    {
        if (value is not null && !string.Equals(value, HostMaterialSlotReference.Marker, StringComparison.Ordinal))
            issues.Add(Error("KNX_IP_PROTECTED_MATERIAL_REFERENCE_INVALID", $"{setting} must use the portable host-owned protected-material slot marker.", sourceKey));
    }

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

    private static EngineeringDriverIssue Error(string code, string message, string sourceKey, string? tagPath = null) => new(code, message, sourceKey, tagPath);
}

public sealed class KnxIpDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverType = "knx.ip";
    public const string SchemaId = "elitescada.driver.knx.ip.configuration";
    public const string TagBindingSchemaId = "elitescada.driver.knx.ip";
    public const string UserPasswordPurpose = "knx.ip-secure.user-password";
    public const string DeviceAuthenticationPurpose = "knx.ip-secure.device-authentication";
    public const string DataSecureKeyringPurpose = "knx.data-secure.keyring";

    public CommunicationDriverTypeDescriptor Descriptor { get; } = new(
        DriverType,
        "KNX/IP Secure",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.None,
        AcquisitionModes: [DriverAcquisitionMode.Hybrid],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            SchemaId, 1,
            DataSourceFields:
            [
                new("gatewayHost", DriverConfigurationValueKind.Host, Required: true),
                new("gatewayPort", DriverConfigurationValueKind.Port, DefaultValue: "3671", Minimum: 1, Maximum: 65535),
                new("individualAddress", DriverConfigurationValueKind.Identifier, Required: true),
                new("userId", DriverConfigurationValueKind.Integer, Required: true, Minimum: 1, Maximum: 127),
                new("userPassword", DriverConfigurationValueKind.SecretReference, Required: true),
                new("deviceAuthentication", DriverConfigurationValueKind.SecretReference, Required: true),
                new("dataSecureKeyring", DriverConfigurationValueKind.SecretReference, Required: true)
            ],
            TagBindingFields:
            [
                new("writeGroupAddress", DriverConfigurationValueKind.Identifier, Required: true),
                new("stateGroupAddress", DriverConfigurationValueKind.Identifier, Required: true),
                new("dpt", DriverConfigurationValueKind.Enum, Required: true, AllowedValues: ["1.001", "1.002", "5.001", "9.001", "9.004", "9.007", "9.024"])
            ]),
        Description: "XKNX 3.20 managed sidecar; explicit KNX IP Secure TCP tunneling and Data Secure only.",
        TagBindingSchemaId: TagBindingSchemaId,
        TagBindingSchemaVersion: 1,
        IntegrationDomains: [IntegrationDomain.Building, IntegrationDomain.Residential],
        ConnectionModel: DriverConnectionModel.DirectNetwork,
        ExternalDependencies: [new DriverExternalDependencyDescriptor(
            DriverExternalDependencyKind.OptionalSidecar,
            "CPython 3.13.x on Windows or Linux x64",
            "XKNX 3.20.0 with a hash-locked permissive runtime dependency closure.")]);
}

public sealed record KnxIpSidecarDeploymentOptions(string PythonExecutable, string ArtifactDirectory)
{
    public static KnxIpSidecarDeploymentOptions Default => new(
        "python", Path.Combine(AppContext.BaseDirectory, "sidecars", "knx-ip"));
}

public sealed class KnxIpCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly HostResourceRegistry _resources;
    private readonly HostResourceLeaseCoordinator _resourceLeases;
    private readonly KnxIpSidecarDeploymentOptions _deployment;
    private readonly Func<KnxIpCommunicationRuntimePlan, CommunicationDriverRuntimeServices, IKnxIpSidecarSession>? _sessionFactory;

    public KnxIpCommunicationRuntimeFactory(
        HostResourceRegistry resources,
        HostResourceLeaseCoordinator resourceLeases,
        KnxIpSidecarDeploymentOptions deployment,
        Func<KnxIpCommunicationRuntimePlan, CommunicationDriverRuntimeServices, IKnxIpSidecarSession>? sessionFactory = null)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _resourceLeases = resourceLeases ?? throw new ArgumentNullException(nameof(resourceLeases));
        _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
        _sessionFactory = sessionFactory;
        if (string.IsNullOrWhiteSpace(deployment.PythonExecutable) || string.IsNullOrWhiteSpace(deployment.ArtifactDirectory))
            throw new ArgumentException("KNX/IP sidecar deployment configuration is incomplete.", nameof(deployment));
    }

    public string DriverType => KnxIpDriverDescriptorProvider.DriverType;

    public ICommunicationDriver Create(ICommunicationDriverRuntimePlan plan, CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not KnxIpCommunicationRuntimePlan knxPlan)
            throw new ArgumentException("KNX/IP factory requires a KNX/IP runtime plan.", nameof(plan));
        if (_sessionFactory is not null) return new KnxIpCommunicationDriver(knxPlan, services, _sessionFactory(knxPlan, services));

        var stableDataSourceId = knxPlan.DataSourceId is { } id && id != Guid.Empty
            ? id.ToString("D")
            : throw new InvalidOperationException("KNX/IP requires stable Data Source identity.");
        var instanceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{services.ProjectKey}\0{stableDataSourceId}"))).ToLowerInvariant();
        var instanceId = $"knx.ip.{instanceHash[..24]}";
        var pointManifest = JsonSerializer.Serialize(knxPlan.Points.Select(point => new
        {
            tagId = point.Tag.Id,
            dpt = point.Dpt,
            writeGroupAddress = point.WriteGroupAddress,
            stateGroupAddress = point.StateGroupAddress,
            writable = point.Writable
        }));
        var protectedSettings = new[]
        {
            new ManagedSidecarProtectedSettingReference("userPassword", DriverType, stableDataSourceId,
                KnxIpDriverDescriptorProvider.UserPasswordPurpose, knxPlan.UserPasswordReference),
            new ManagedSidecarProtectedSettingReference("deviceAuthentication", DriverType, stableDataSourceId,
                KnxIpDriverDescriptorProvider.DeviceAuthenticationPurpose, knxPlan.DeviceAuthenticationReference),
            new ManagedSidecarProtectedSettingReference("dataSecureKeyring", DriverType, stableDataSourceId,
                KnxIpDriverDescriptorProvider.DataSecureKeyringPurpose, knxPlan.DataSecureKeyringReference)
        };
        var definition = new ManagedSidecarDefinition(
            instanceId,
            new ManagedSidecarArtifactIdentity("elitescada.knx.ip", "1.0.0", "3.13.x", 1),
            new ManagedSidecarCompatibilityMetadata("elitescada.knx.ip", ["1.0.0"], "3.13.x", 1, 1),
            new ManagedSidecarPackagingMetadata([ManagedSidecarPackageTargets.WindowsX64, ManagedSidecarPackageTargets.LinuxX64]),
            new ManagedSidecarConfiguration(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["gatewayHost"] = knxPlan.GatewayHost,
                ["gatewayPort"] = knxPlan.GatewayPort.ToString(CultureInfo.InvariantCulture),
                ["individualAddress"] = knxPlan.IndividualAddress,
                ["userId"] = knxPlan.UserId.ToString(CultureInfo.InvariantCulture),
                ["points"] = pointManifest
            }, protectedSettings));

        var rpc = new KnxIpRpcServer(knxPlan.Points);
        var processFactory = new SystemManagedSidecarProcessFactory((_, materials, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) ||
                !System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.Equals(System.Runtime.InteropServices.Architecture.X64))
                throw new PlatformNotSupportedException("KNX/IP v1 requires Windows or Linux x64.");
            if (!Directory.Exists(_deployment.ArtifactDirectory))
                throw new DirectoryNotFoundException("KNX/IP sidecar artifact directory is missing.");
            if (!materials.TryGetValue("userPassword", out var userPasswordBytes) ||
                !materials.TryGetValue("deviceAuthentication", out var deviceAuthenticationBytes) ||
                !materials.TryGetValue("dataSecureKeyring", out var keyringBundleBytes))
                throw new InvalidOperationException("KNX/IP Secure material was not resolved through Protected Material.");

            var userPassword = DecodeUtf8(userPasswordBytes.Span, "KNX user password");
            var deviceAuthentication = DecodeUtf8(deviceAuthenticationBytes.Span, "KNX device authentication credential");
            var keyringBundle = Convert.ToBase64String(keyringBundleBytes.Span);
            var bootstrapJson = JsonSerializer.SerializeToUtf8Bytes(new
            {
                type = "bootstrap",
                configuration = new
                {
                    gatewayHost = knxPlan.GatewayHost,
                    gatewayPort = knxPlan.GatewayPort.ToString(CultureInfo.InvariantCulture),
                    individualAddress = knxPlan.IndividualAddress,
                    userId = knxPlan.UserId.ToString(CultureInfo.InvariantCulture)
                },
                secrets = new
                {
                    userPassword,
                    deviceAuthentication,
                    dataSecureKeyringBundle = keyringBundle
                },
                points = knxPlan.Points.Select(point => new
                {
                    tagId = point.Tag.Id,
                    dpt = point.Dpt,
                    writeGroupAddress = point.WriteGroupAddress,
                    stateGroupAddress = point.StateGroupAddress,
                    writable = point.Writable
                })
            });
            var startupInput = new byte[bootstrapJson.Length + 1];
            bootstrapJson.CopyTo(startupInput, 0);
            startupInput[^1] = (byte)'\n';
            CryptographicOperations.ZeroMemory(bootstrapJson);
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["KNX_RPC_HOST"] = "127.0.0.1",
                ["KNX_RPC_PORT"] = rpc.Port.ToString(CultureInfo.InvariantCulture),
                ["KNX_RPC_TOKEN"] = rpc.Token,
                ["PYTHONUTF8"] = "1",
                ["PYTHONNOUSERSITE"] = "1"
            };
            var start = new ManagedSidecarProcessStartSpec(_deployment.PythonExecutable,
                ["-I", "-u", "main.py"], _deployment.ArtifactDirectory, environment,
                [userPassword, deviceAuthentication, keyringBundle, rpc.Token],
                StartupInput: startupInput);
            return ValueTask.FromResult(start);
        });
        var protectedFactory = new ManagedSidecarProtectedMaterialProcessFactory(services, processFactory);
        var supervisor = new ManagedSidecarSupervisor(definition, protectedFactory);
        var binding = new ManagedSidecarRuntimeBinding(knxPlan.Name, definition, services, _resources, _resourceLeases, supervisor);
        return new KnxIpCommunicationDriver(knxPlan, services, new KnxIpManagedSidecarSession(binding, rpc, knxPlan.Points));
    }

    private static string DecodeUtf8(ReadOnlySpan<byte> value, string displayName)
    {
        try { return new UTF8Encoding(false, true).GetString(value); }
        catch (DecoderFallbackException) { throw new InvalidOperationException($"Protected {displayName} must be UTF-8."); }
    }
}

public sealed class KnxIpCommunicationDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource,
    ICommunicationDriverReadinessSource
{
    private readonly KnxIpCommunicationRuntimePlan _plan;
    private readonly CommunicationDriverRuntimeServices _services;
    private readonly IKnxIpSidecarSession _session;
    private readonly IReadOnlyDictionary<Guid, KnxIpPoint> _pointsById;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastCommunicationAt;
    private string? _lastError;
    private long _requests, _successes, _failures, _reads, _writes, _updates;
    private int _running, _disposed;

    public KnxIpCommunicationDriver(KnxIpCommunicationRuntimePlan plan, CommunicationDriverRuntimeServices services, IKnxIpSidecarSession session)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _session = session ?? throw new ArgumentNullException(nameof(session));
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
            if (Volatile.Read(ref _running) != 0) throw new InvalidOperationException($"KNX/IP Data Source '{DriverId}' is already active.");
            if (!_services.CanAcquireInputs) throw new InvalidOperationException("KNX/IP Standby Runtime cannot open an external connection.");
            foreach (var point in _plan.Points) _services.Registry.Upsert(point.Tag);
            SetStatus(DriverState.Starting, null);
            try
            {
                await _session.StartAsync(cancellationToken).ConfigureAwait(false);
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
            var value = await _session.ReadAsync(point, cancellationToken).ConfigureAwait(false);
            return await PublishStateAsync(point, value, "knx.ip.secure-readback", cancellationToken).ConfigureAwait(false);
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
        if (!point.Writable || point.Tag.ReadOnly) throw new InvalidOperationException($"KNX/IP TAG '{point.Tag.Path}' is read-only.");
        var requested = value ?? throw new ArgumentNullException(nameof(value));
        ValidateValue(point, requested);
        if (Volatile.Read(ref _running) == 0 || !_services.CanOwnExternalEffects)
            throw new InvalidOperationException("KNX/IP Runtime does not currently own external effects.");
        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _writes);
        try
        {
            var valueReadBack = await _session.WriteAndReadBackAsync(point, requested, cancellationToken).ConfigureAwait(false);
            if (!ValuesEquivalent(point, requested, valueReadBack))
                throw new InvalidOperationException($"KNX/IP write for TAG '{point.Tag.Path}' did not reconcile to its authoritative Secure state group address.");
            await PublishStateAsync(point, valueReadBack, "knx.ip.secure-write-readback", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) { RecordFailure(exception); throw; }
    }

    public CommunicationDriverReadinessSnapshot GetCommunicationReadiness()
    {
        var active = Volatile.Read(ref _running) != 0;
        return new(DriverId, KnxIpDriverDescriptorProvider.DriverType, active ? CommunicationDriverReadinessState.Ready :
            Status.State == DriverState.Faulted ? CommunicationDriverReadinessState.Faulted : CommunicationDriverReadinessState.Stopped,
            DateTimeOffset.UtcNow, _lastError, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["connectionMode"] = "TUNNELING_TCP_SECURE",
                ["stateAuthority"] = "Data Secure status/readback group address",
                ["capabilities"] = string.Join(',', _plan.Points.Select(point => point.Dpt).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            });
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var source = _session.GetCommunicationDiagnostics();
        return source with
        {
            DataSourceKey = DriverId,
            DataSourceName = Name,
            DriverType = KnxIpDriverDescriptorProvider.DriverType,
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
                ["protocol"] = "KNX/IP",
                ["connectionMode"] = "TUNNELING_TCP_SECURE",
                ["dataSecure"] = "required",
                ["stateAuthority"] = "status/readback group addresses",
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

    private void OnObservation(KnxIpObservation observation)
    {
        if (Volatile.Read(ref _running) == 0 || !_services.CanAcquireInputs || !_pointsById.TryGetValue(observation.TagId, out var point)) return;
        _ = PublishObservationAsync(point, observation);
    }

    private async Task PublishObservationAsync(KnxIpPoint point, KnxIpObservation observation)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _running) == 0 || !_services.CanAcquireInputs) return;
            await PublishStateAsync(point, observation.Value, "knx.ip.data-secure-telegram", CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) { RecordFailure(exception); }
        finally { _lifecycleGate.Release(); }
    }

    private async ValueTask<TagValue?> PublishStateAsync(KnxIpPoint point, object value, string source, CancellationToken cancellationToken)
    {
        var normalized = NormalizeValue(point, value);
        var updated = await _services.Cache.UpdateAsync(point.Tag, TagValue.Good(point.Tag.Id, normalized, source), cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _successes);
        if (updated is not null) Interlocked.Increment(ref _updates);
        _lastCommunicationAt = DateTimeOffset.UtcNow;
        _lastError = null;
        return updated;
    }

    private async ValueTask PublishBadCommunicationAsync(KnxIpPoint point, CancellationToken cancellationToken)
    {
        _ = await _services.Cache.UpdateAsync(point.Tag,
            new TagValue(point.Tag.Id, null, DateTimeOffset.UtcNow, TagQuality.BadCommunication, "knx.ip"), cancellationToken).ConfigureAwait(false);
    }

    private KnxIpPoint GetPoint(Guid tagId) => _pointsById.TryGetValue(tagId, out var point)
        ? point : throw new KeyNotFoundException($"KNX/IP TAG '{tagId}' is not part of Data Source '{DriverId}'.");

    private static void ValidateValue(KnxIpPoint point, object? value)
    {
        if (point.Dpt is "1.001" or "1.002")
        {
            if (value is not bool) throw new ArgumentException($"DPT {point.Dpt} requires a Boolean value.", nameof(value));
        }
        else if ((point.Tag.DataType == TagDataType.Float && value is not float) ||
                 (point.Tag.DataType == TagDataType.Double && value is not double) ||
                 value is not float and not double || !double.IsFinite(Convert.ToDouble(value, CultureInfo.InvariantCulture)))
            throw new ArgumentException($"DPT {point.Dpt} requires a finite value matching its canonical TAG type.", nameof(value));
    }

    private static object NormalizeValue(KnxIpPoint point, object value)
    {
        if (point.Dpt is "1.001" or "1.002")
        {
            ValidateValue(point, value);
            return value;
        }
        if (value is not float and not double)
            throw new InvalidOperationException("KNX_IP_NUMERIC_READBACK_INVALID");
        var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (!double.IsFinite(number)) throw new InvalidOperationException("KNX_IP_NUMERIC_READBACK_INVALID");
        if (point.Tag.DataType == TagDataType.Float)
        {
            var normalized = (float)number;
            if (!float.IsFinite(normalized)) throw new InvalidOperationException("KNX_IP_NUMERIC_READBACK_INVALID");
            return normalized;
        }
        return number;
    }

    private static bool ValuesEquivalent(KnxIpPoint point, object requested, object observed)
    {
        if (point.Dpt is "1.001" or "1.002") return requested is bool left && observed is bool right && left == right;
        var requestedDouble = Convert.ToDouble(requested, CultureInfo.InvariantCulture);
        var observedDouble = Convert.ToDouble(observed, CultureInfo.InvariantCulture);
        var wireTolerance = 50d / 255d;
        if (point.Dpt is "9.001" or "9.004" or "9.007" or "9.024")
        {
            var magnitude = Math.Abs(requestedDouble);
            var exponent = 0;
            while (exponent < 15 && magnitude > 20.47d * Math.Pow(2d, exponent)) exponent++;
            wireTolerance = 0.005d * Math.Pow(2d, exponent);
        }
        return Math.Abs(requestedDouble - observedDouble) <= wireTolerance;
    }

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

    private static string Sanitize(string message) => ManagedSidecarLogSanitizer.Sanitize(message, maximumLength: 256) ?? "KNX/IP operation failed.";
}
