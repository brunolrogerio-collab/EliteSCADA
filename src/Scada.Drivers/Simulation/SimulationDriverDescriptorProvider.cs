using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Simulation;

public sealed class SimulationDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverTypeId = "builtin.simulation";

    public static CommunicationDriverTypeDescriptor SharedDescriptor { get; } = new(
        DriverType: DriverTypeId,
        DisplayName: "Simulation",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.None,
        AcquisitionModes: new[] { DriverAcquisitionMode.Polling },
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            SchemaId: "builtin.simulation.engineering",
            SchemaVersion: 1,
            DataSourceFields: new[]
            {
                new DriverConfigurationFieldDescriptor(
                    "scanIntervalMilliseconds",
                    DriverConfigurationValueKind.Integer,
                    DisplayName: "Scan interval",
                    Description: "Simulation update interval in milliseconds.",
                    DefaultValue: "500",
                    Minimum: 10,
                    Maximum: 600_000)
            },
            TagBindingFields: Array.Empty<DriverConfigurationFieldDescriptor>()),
        Description: "Built-in deterministic simulation driver for development and testing.");

    public CommunicationDriverTypeDescriptor Descriptor => SharedDescriptor;
}
