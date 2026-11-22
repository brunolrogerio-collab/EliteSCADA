using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HostResources;

namespace Scada.DriverHost.Engineering;

public sealed record MatterWifiSidecarDeploymentOptions(string NodeExecutable, string ArtifactDirectory, string StateDirectory)
{
    public static MatterWifiSidecarDeploymentOptions Default => new(
        "node",
        Path.Combine(AppContext.BaseDirectory, "sidecars", "matter-wifi"),
        Path.Combine(AppContext.BaseDirectory, "data", "sidecars", "matter-wifi"));
}

public sealed class MatterWifiCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly HostResourceRegistry _resources;
    private readonly HostResourceLeaseCoordinator _resourceLeases;
    private readonly MatterWifiSidecarDeploymentOptions _deployment;
    private readonly Func<bool> _isHighAvailabilityEnabled;
    private readonly bool _matterWifiPreviewEnabled;
    private readonly Func<MatterWifiCommunicationRuntimePlan, CommunicationDriverRuntimeServices, IMatterWifiSidecarSession>? _sessionFactory;

    public MatterWifiCommunicationRuntimeFactory(
        HostResourceRegistry resources,
        HostResourceLeaseCoordinator resourceLeases,
        MatterWifiSidecarDeploymentOptions deployment,
        Func<MatterWifiCommunicationRuntimePlan, CommunicationDriverRuntimeServices, IMatterWifiSidecarSession>? sessionFactory = null,
        Func<bool>? isHighAvailabilityEnabled = null,
        bool matterWifiPreviewEnabled = false)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _resourceLeases = resourceLeases ?? throw new ArgumentNullException(nameof(resourceLeases));
        _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
        _sessionFactory = sessionFactory;
        _isHighAvailabilityEnabled = isHighAvailabilityEnabled ?? (() => false);
        _matterWifiPreviewEnabled = matterWifiPreviewEnabled;
        if (string.IsNullOrWhiteSpace(deployment.NodeExecutable) || string.IsNullOrWhiteSpace(deployment.ArtifactDirectory) ||
            string.IsNullOrWhiteSpace(deployment.StateDirectory))
            throw new ArgumentException("Matter Wi-Fi sidecar deployment configuration is incomplete.", nameof(deployment));
    }

    public string DriverType => MatterWifiDriverDescriptorProvider.DriverType;

    public ICommunicationDriver Create(ICommunicationDriverRuntimePlan plan, CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not MatterWifiCommunicationRuntimePlan matterPlan)
            throw new ArgumentException("Matter Wi-Fi factory requires a Matter Wi-Fi runtime plan.", nameof(plan));
        if (!_matterWifiPreviewEnabled)
            throw new InvalidOperationException("Matter Wi-Fi v1 is a developer preview and requires an explicit isolated development/test opt-in.");
        if (_isHighAvailabilityEnabled())
            throw new InvalidOperationException("Matter Wi-Fi v1 cannot run in a High Availability installation; Matter fabric state is local-only.");
        if (_sessionFactory is not null)
            return new MatterWifiCommunicationDriver(matterPlan, services, _sessionFactory(matterPlan, services));

        var sourceId = matterPlan.DataSourceId.ToString("D");
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{services.ProjectKey}\0{sourceId}"))).ToLowerInvariant();
        var instanceId = $"matter.wifi.{identity[..24]}";
        var stateDirectory = Path.Combine(_deployment.StateDirectory, identity);
        var pointManifest = JsonSerializer.Serialize(matterPlan.Points.Select(point => new
        {
            tagId = point.Tag.Id,
            nodeId = point.NodeId,
            address = point.PortableAddress,
            dataType = point.Tag.DataType.ToString(),
            writable = point.Writable && !point.Tag.ReadOnly
        }));
        var protectedSettings = matterPlan.ProtectedKeyReferences.Select(pair =>
            new ManagedSidecarProtectedSettingReference(pair.Key, DriverType, sourceId,
                MatterWifiDriverDescriptorProvider.FabricStorageKeyPurpose, pair.Value)).ToArray();
        var definition = new ManagedSidecarDefinition(
            instanceId,
            new ManagedSidecarArtifactIdentity("elitescada.matter.wifi", "1.0.0", "node-22.13.0-or-24-plus", 1),
            new ManagedSidecarCompatibilityMetadata("elitescada.matter.wifi", ["1.0.0"], "node-22.13.0-or-24-plus", 1, 1),
            new ManagedSidecarPackagingMetadata([ManagedSidecarPackageTargets.LinuxX64]),
            new ManagedSidecarConfiguration(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dataSourceId"] = sourceId,
                ["pointManifest"] = pointManifest
            }, protectedSettings),
            new ManagedSidecarPersistentStateOwnership(instanceId, 1, $"matter-wifi-{identity[..24]}", RequiresBackupBeforeUpgrade: false));

        var rpc = new MatterWifiRpcServer();
        var processFactory = new SystemManagedSidecarProcessFactory((_, materials, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsLinux() || !System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.Equals(System.Runtime.InteropServices.Architecture.X64))
                throw new PlatformNotSupportedException("Matter Wi-Fi v1 preview is limited to Linux x64.");
            if (!Directory.Exists(_deployment.ArtifactDirectory))
                throw new DirectoryNotFoundException("Matter Wi-Fi sidecar artifact directory is missing.");
            if (!materials.TryGetValue(MatterWifiDriverDescriptorProvider.FabricStorageKeyField, out var material) || material.Length != 32)
                throw new InvalidOperationException("Matter protected fabric-storage key was not resolved through Protected Material or has an invalid length.");

            var fullStateDirectory = Path.GetFullPath(stateDirectory);
            var socketPath = Path.Combine(fullStateDirectory, "matter.sock");
            if (Encoding.UTF8.GetByteCount(socketPath) > 95)
                throw new InvalidOperationException("Matter Unix socket path exceeds the supported Linux limit.");
            var keyBytes = material.ToArray().Select(value => (int)value).ToArray();
            byte[]? startupJson = null;
            byte[]? startupInput = null;
            try
            {
                startupJson = JsonSerializer.SerializeToUtf8Bytes(new { type = "bootstrap", schemaVersion = 1, keyBytes });
                startupInput = new byte[startupJson.Length + 1];
                startupJson.CopyTo(startupInput, 0);
                startupInput[^1] = (byte)'\n';
                var environment = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MATTER_RPC_HOST"] = "127.0.0.1",
                    ["MATTER_RPC_PORT"] = rpc.Port.ToString(CultureInfo.InvariantCulture),
                    ["MATTER_RPC_TOKEN"] = rpc.Token,
                    ["MATTER_POINT_MANIFEST"] = pointManifest
                };
                var args = new[]
                {
                    "--import", "src/bootstrap.mjs",
                    "node_modules/matter-server/dist/esm/MatterServer.js",
                    "--storage-path", fullStateDirectory,
                    "--listen-address", socketPath,
                    "--disable-dashboard", "--disable-ota"
                };
                var transferredInput = startupInput;
                startupInput = null;
                return ValueTask.FromResult(new ManagedSidecarProcessStartSpec(
                    _deployment.NodeExecutable, args, _deployment.ArtifactDirectory, environment,
                    [rpc.Token], "stop", transferredInput));
            }
            finally
            {
                Array.Clear(keyBytes);
                if (startupJson is not null) CryptographicOperations.ZeroMemory(startupJson);
                if (startupInput is not null) CryptographicOperations.ZeroMemory(startupInput);
            }
        });
        var protectedFactory = new ManagedSidecarProtectedMaterialProcessFactory(services, processFactory);
        var supervisor = new ManagedSidecarSupervisor(definition, protectedFactory);
        var binding = new ManagedSidecarRuntimeBinding(matterPlan.Name, definition, services, _resources, _resourceLeases, supervisor);
        return new MatterWifiCommunicationDriver(matterPlan, services, new MatterWifiManagedSidecarSession(binding, rpc));
    }
}
