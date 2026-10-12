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
            if (entry["Kind"] != "zigbee-coordinator")
                throw new InvalidOperationException("Unsupported managed protocol Host Resource kind.");
            string Required(string key) => entry[key] is { Length: > 0 } value ? value
                : throw new InvalidOperationException($"Host Resource '{entry.Key}' requires {key}.");
            registry.Observe(ZigbeeCoordinatorHostResource.Create(entry.Key,
                HostResourceLocator.FromSerialPort(new HostSerialLineSettings(Required("PortName"))),
                new HostResourcePhysicalIdentity(Required("PhysicalIdentity")),
                Required("Family"), Required("Model"), Required("Firmware"), Required("NetworkIdentity")));
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
    }
}
