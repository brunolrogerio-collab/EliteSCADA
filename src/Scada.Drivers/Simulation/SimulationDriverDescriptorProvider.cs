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
                    DefaultValue: "500")
            },
            TagBindingFields: new[]
            {
                new DriverConfigurationFieldDescriptor("simulation.signalType", DriverConfigurationValueKind.Enum, DisplayName: "Simulation behavior", DefaultValue: "Sine", AllowedValues: new[] { "Constant", "Random", "Sine", "Square", "RampUp", "RampDown", "RampUpDown", "Counter", "BooleanToggle", "Manual", "CurrentTime" }),
                new DriverConfigurationFieldDescriptor("simulation.minimum", DriverConfigurationValueKind.Number, DisplayName: "Minimum", DefaultValue: "0"),
                new DriverConfigurationFieldDescriptor("simulation.maximum", DriverConfigurationValueKind.Number, DisplayName: "Maximum", DefaultValue: "100"),
                new DriverConfigurationFieldDescriptor("simulation.periodSeconds", DriverConfigurationValueKind.Number, DisplayName: "Period (s)", DefaultValue: "10", Minimum: 0.001),
                new DriverConfigurationFieldDescriptor("simulation.constantValue", DriverConfigurationValueKind.Number, DisplayName: "Constant value", DefaultValue: "0"),
                new DriverConfigurationFieldDescriptor("simulation.step", DriverConfigurationValueKind.Number, DisplayName: "Step", DefaultValue: "1")
            }),
        Description: "Built-in deterministic simulation driver for development and testing.");

    public CommunicationDriverTypeDescriptor Descriptor => SharedDescriptor;
}
