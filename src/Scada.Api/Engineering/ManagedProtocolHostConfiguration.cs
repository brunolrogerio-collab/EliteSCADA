using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.DriverHost.Engineering;
using Scada.Drivers.HostResources;
using Scada.Drivers.Serial;

namespace Scada.Api.Engineering;

/// <summary>Trusted deployment configuration; OS resource locators are never supplied by project packages.</summary>
public static class ManagedProtocolHostConfiguration
{
    public static HostResourceRegistry ReadResources(IConfiguration configuration)
    {
        var registry = new HostResourceRegistry();
        foreach (var entry in configuration.GetSection("CommunicationDrivers:HostResources").GetChildren())
        {
            string Required(string key) => entry[key] is { Length: > 0 } value ? value
                : throw new InvalidOperationException($"Host Resource '{entry.Key}' requires {key}.");
            var port = new HostSerialLineSettings(Required("PortName"));
            var physicalIdentity = new HostResourcePhysicalIdentity(Required("PhysicalIdentity"));
            var family = Required("Family");
            var model = Required("Model");
            var firmware = Required("Firmware");
            var resource = entry["Kind"] switch
            {
                "zigbee-coordinator" => ZigbeeCoordinatorHostResource.Create(entry.Key,
                    HostResourceLocator.FromSerialPort(port), physicalIdentity, family, model, firmware, Required("NetworkIdentity")),
                "zwave-controller" => ZWaveControllerHostResource.Create(entry.Key, port, physicalIdentity,
                    family, model, firmware, Required("RfRegion"), Required("NvmIdentity")),
                _ => throw new InvalidOperationException("Unsupported managed protocol Host Resource kind.")
            };
            registry.Observe(resource);
        }
        return registry;
    }

    public static void AddManagedProtocolHost(this WebApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(_ => ReadResources(builder.Configuration));
        builder.Services.TryAddSingleton<HostResourceLeaseCoordinator>();
        var defaults = ZigbeeNativeSidecarDeploymentOptions.Default;
        builder.Services.TryAddSingleton(new ZigbeeNativeSidecarDeploymentOptions(
            builder.Configuration["CommunicationDrivers:ZigbeeNative:NodeExecutable"] ?? defaults.NodeExecutable,
            Path.GetFullPath(builder.Configuration["CommunicationDrivers:ZigbeeNative:ArtifactDirectory"] ?? defaults.ArtifactDirectory),
            Path.GetFullPath(builder.Configuration["CommunicationDrivers:ZigbeeNative:StateDirectory"] ?? defaults.StateDirectory)));
        var knxDefaults = KnxIpSidecarDeploymentOptions.Default;
        builder.Services.TryAddSingleton(new KnxIpSidecarDeploymentOptions(
            builder.Configuration["CommunicationDrivers:KnxIp:PythonExecutable"] ?? knxDefaults.PythonExecutable,
            Path.GetFullPath(builder.Configuration["CommunicationDrivers:KnxIp:ArtifactDirectory"] ?? knxDefaults.ArtifactDirectory)));
        var zwaveDefaults = ZWaveJsSidecarDeploymentOptions.Default;
        builder.Services.TryAddSingleton(new ZWaveJsSidecarDeploymentOptions(
            builder.Configuration["CommunicationDrivers:ZWaveJs:NodeExecutable"] ?? zwaveDefaults.NodeExecutable,
            Path.GetFullPath(builder.Configuration["CommunicationDrivers:ZWaveJs:ArtifactDirectory"] ?? zwaveDefaults.ArtifactDirectory),
            Path.GetFullPath(builder.Configuration["CommunicationDrivers:ZWaveJs:StateDirectory"] ?? zwaveDefaults.StateDirectory)));
    }
}
