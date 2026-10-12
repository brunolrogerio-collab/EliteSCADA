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
}
