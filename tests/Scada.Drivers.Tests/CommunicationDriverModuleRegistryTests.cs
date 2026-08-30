using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Tests;

public sealed class CommunicationDriverModuleRegistryTests
{
    [Fact]
    public void Registry_ResolvesDriverTypeCaseInsensitively()
    {
        var registry = new CommunicationDriverModuleRegistry();
        registry.Register(new CommunicationDriverModuleRegistration(new DescriptorProvider(CreateDescriptor("mqtt.industrial"))));

        Assert.True(registry.TryGet(" MQTT.INDUSTRIAL ", out var registration));
        Assert.NotNull(registration);
        Assert.Equal("mqtt.industrial", registration!.Descriptor.DriverType);
    }

    [Fact]
    public void Registry_RejectsDuplicateDriverTypeInsteadOfLastWins()
    {
        var registry = new CommunicationDriverModuleRegistry();
        registry.Register(new CommunicationDriverModuleRegistration(new DescriptorProvider(CreateDescriptor("opcua.client"))));

        var error = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new CommunicationDriverModuleRegistration(new DescriptorProvider(CreateDescriptor("OPCUA.CLIENT")))));

        Assert.True(error.Message.Contains("already registered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Registration_RejectsAdvertisedEngineeringCapabilityWithoutProvider()
    {
        var descriptor = CreateDescriptor(
            "bacnet.ip",
            DriverEngineeringCapabilities.Browse);

        var registration = new CommunicationDriverModuleRegistration(new DescriptorProvider(descriptor));

        var error = Assert.Throws<InvalidOperationException>(registration.Validate);
        Assert.True(error.Message.Contains("Browse", StringComparison.Ordinal));
    }

    private static CommunicationDriverTypeDescriptor CreateDescriptor(
        string driverType,
        DriverEngineeringCapabilities engineeringCapabilities = DriverEngineeringCapabilities.None)
        => new(
            DriverType: driverType,
            DisplayName: driverType,
            DriverContractVersion: 1,
            RuntimeCapabilities: DriverCapabilities.Read,
            EngineeringCapabilities: engineeringCapabilities,
            AcquisitionModes: new[] { DriverAcquisitionMode.Polling },
            ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
                SchemaId: $"{driverType}.schema",
                SchemaVersion: 1,
                DataSourceFields: Array.Empty<DriverConfigurationFieldDescriptor>(),
                TagBindingFields: Array.Empty<DriverConfigurationFieldDescriptor>()));

    private sealed class DescriptorProvider : ICommunicationDriverDescriptorProvider
    {
        public DescriptorProvider(CommunicationDriverTypeDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public CommunicationDriverTypeDescriptor Descriptor { get; }
    }
}
