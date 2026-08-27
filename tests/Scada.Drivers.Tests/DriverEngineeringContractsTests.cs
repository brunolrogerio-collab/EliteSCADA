using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Tests;

public sealed class DriverEngineeringContractsTests
{
    [Fact]
    public void Descriptor_SeparatesRuntimeEngineeringAndAcquisitionCapabilities()
    {
        var descriptor = new CommunicationDriverTypeDescriptor(
            "mqtt.raw",
            "MQTT",
            DriverContractVersion: 1,
            RuntimeCapabilities: DriverCapabilities.Read |
                                 DriverCapabilities.Write |
                                 DriverCapabilities.Subscribe |
                                 DriverCapabilities.Diagnostics |
                                 DriverCapabilities.SourceTimestamp,
            EngineeringCapabilities: DriverEngineeringCapabilities.ConnectionTest |
                                     DriverEngineeringCapabilities.Discover,
            AcquisitionModes: [DriverAcquisitionMode.EventDriven],
            ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
                "elitescada.driver.mqtt.raw",
                1,
                [new DriverConfigurationFieldDescriptor("host", DriverConfigurationValueKind.Host, Required: true)],
                [new DriverConfigurationFieldDescriptor("topic", DriverConfigurationValueKind.String, Required: true)]));

        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Subscribe));
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Discover));
        Assert.False(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Browse));
        Assert.Equal(DriverAcquisitionMode.EventDriven, Assert.Single(descriptor.AcquisitionModes));
    }

    [Fact]
    public void EngineeringInterfaces_AreCapabilitySpecific()
    {
        Assert.True(typeof(ICommunicationDriverDiscoverySource)
            .IsAssignableTo(typeof(ICommunicationDriverDescriptorProvider)));
        Assert.True(typeof(ICommunicationDriverBrowser)
            .IsAssignableTo(typeof(ICommunicationDriverDescriptorProvider)));
        Assert.False(typeof(ICommunicationDriverDiscoverySource)
            .IsAssignableTo(typeof(ICommunicationDriverBrowser)));
        Assert.False(typeof(ICommunicationDriverFileImporter)
            .IsAssignableTo(typeof(ICommunicationDriverBrowser)));
    }
}
