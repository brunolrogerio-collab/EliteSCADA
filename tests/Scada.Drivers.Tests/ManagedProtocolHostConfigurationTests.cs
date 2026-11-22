using Microsoft.Extensions.Configuration;
using Scada.Api.Engineering;
using Scada.Drivers.HostResources;

namespace Scada.Drivers.Tests;

public sealed class ManagedProtocolHostConfigurationTests
{
    [Theory]
    [InlineData("COM4")]
    [InlineData("/dev/serial/by-id/usb-TI_CC2652")]
    public void ZigbeeCoordinatorResourceAcceptsTrustedWindowsAndLinuxSerialLocators(string portName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommunicationDrivers:HostResources:zigbee-main:Kind"] = "zigbee-coordinator",
                ["CommunicationDrivers:HostResources:zigbee-main:PortName"] = portName,
                ["CommunicationDrivers:HostResources:zigbee-main:PhysicalIdentity"] = "usb:ti:cc2652:001",
                ["CommunicationDrivers:HostResources:zigbee-main:Family"] = "ti-zStack",
                ["CommunicationDrivers:HostResources:zigbee-main:Model"] = "CC2652P2",
                ["CommunicationDrivers:HostResources:zigbee-main:Firmware"] = "2026.1",
                ["CommunicationDrivers:HostResources:zigbee-main:NetworkIdentity"] = "zigbee-network:home-01"
            })
            .Build();

        var resources = ManagedProtocolHostConfiguration.ReadResources(configuration);
        var coordinator = resources.GetRequired(new HostResourceId("zigbee-main"));

        Assert.Equal(HostResourceKinds.ZigbeeCoordinator, coordinator.ResourceKind);
        Assert.Equal(HostResourceLocatorKind.SerialPort, coordinator.Locator.Kind);
        Assert.Equal(portName, coordinator.Locator.Value);
        Assert.Equal("usb:ti:cc2652:001", coordinator.PhysicalIdentity.StableKey);
    }

    [Fact]
    public void UnknownHostResourceKindFailsClosed()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommunicationDrivers:HostResources:radio:Kind"] = "generic-serial"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => ManagedProtocolHostConfiguration.ReadResources(configuration));
    }

    [Theory]
    [InlineData("COM7")]
    [InlineData("/dev/serial/by-id/usb-ZWave_Controller")]
    public void ZWaveControllerResourceRequiresTrustedSerialAndNetworkIdentityMetadata(string portName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommunicationDrivers:HostResources:zwave-main:Kind"] = "zwave-controller",
                ["CommunicationDrivers:HostResources:zwave-main:PortName"] = portName,
                ["CommunicationDrivers:HostResources:zwave-main:PhysicalIdentity"] = "usb:zwave:controller:001",
                ["CommunicationDrivers:HostResources:zwave-main:Family"] = "Silicon Labs",
                ["CommunicationDrivers:HostResources:zwave-main:Model"] = "UZB-7",
                ["CommunicationDrivers:HostResources:zwave-main:Firmware"] = "7.21.0",
                ["CommunicationDrivers:HostResources:zwave-main:RfRegion"] = "US",
                ["CommunicationDrivers:HostResources:zwave-main:NvmIdentity"] = "homeid:0x12345678"
            })
            .Build();

        var resources = ManagedProtocolHostConfiguration.ReadResources(configuration);
        var controller = resources.GetRequired(new HostResourceId("zwave-main"));

        Assert.Equal(HostResourceKinds.ZWaveController, controller.ResourceKind);
        Assert.Equal(HostResourceLocatorKind.SerialPort, controller.Locator.Kind);
        Assert.Equal(portName, controller.Locator.Value);
        Assert.Equal("usb:zwave:controller:001", controller.PhysicalIdentity.StableKey);
        Assert.Equal("US", controller.Metadata![ZWaveControllerHostResource.RfRegionMetadataKey]);
        Assert.Equal("homeid:0x12345678", controller.Metadata[ZWaveControllerHostResource.NvmIdentityMetadataKey]);
    }
}
