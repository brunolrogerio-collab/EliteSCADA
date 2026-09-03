using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Modbus;

public sealed class ModbusTcpDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverTypeId = "modbus.tcp";

    private static readonly IReadOnlyCollection<DriverConfigurationFieldDescriptor> TagBindingFields =
        new DriverConfigurationFieldDescriptor[]
        {
            new(
                "modbus.unitId",
                DriverConfigurationValueKind.Integer,
                DisplayName: "Unit ID override",
                Description: "Optional TAG-level Modbus Unit ID override. When omitted, the Data Source unitId is used.",
                Minimum: 0,
                Maximum: 255),
            new(
                "modbus.valueType",
                DriverConfigurationValueKind.Enum,
                DisplayName: "Value type",
                Description: "Optional wire value type. When omitted, Runtime infers the compatible Modbus value type from the canonical TAG data type.",
                AllowedValues: new[]
                {
                    "Boolean",
                    "Int16",
                    "UInt16",
                    "Int32",
                    "UInt32",
                    "Float32",
                    "Int64",
                    "UInt64",
                    "Float64"
                }),
            new(
                "modbus.wordOrder",
                DriverConfigurationValueKind.Enum,
                DisplayName: "Word order",
                Description: "Word order for multi-register values.",
                DefaultValue: "HighWordFirst",
                AllowedValues: new[] { "HighWordFirst", "LowWordFirst" },
                Advanced: true),
            new(
                "modbus.scale",
                DriverConfigurationValueKind.Number,
                DisplayName: "Scale",
                Description: "Finite non-zero multiplier applied by the Modbus point adapter.",
                DefaultValue: "1",
                Advanced: true),
            new(
                "modbus.offset",
                DriverConfigurationValueKind.Number,
                DisplayName: "Offset",
                Description: "Finite additive offset applied by the Modbus point adapter.",
                DefaultValue: "0",
                Advanced: true)
        };

    public static CommunicationDriverTypeDescriptor SharedDescriptor { get; } = new(
        DriverType: DriverTypeId,
        DisplayName: "Modbus TCP",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.None,
        AcquisitionModes: new[] { DriverAcquisitionMode.Polling },
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            SchemaId: "modbus.tcp.engineering",
            SchemaVersion: 1,
            DataSourceFields: new DriverConfigurationFieldDescriptor[]
            {
                new("host", DriverConfigurationValueKind.Host, Required: true, DisplayName: "Host", Description: "Controller hostname or IPv4/IPv6 address."),
                new("port", DriverConfigurationValueKind.Port, DisplayName: "Port", Description: "Modbus TCP port.", DefaultValue: "502", Minimum: 1, Maximum: 65535),
                new("scanIntervalMilliseconds", DriverConfigurationValueKind.Integer, DisplayName: "Scan interval (ms)", Description: "Polling interval in milliseconds.", DefaultValue: "1000", Minimum: 10, Maximum: 600000),
                new("requestTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DisplayName: "Request timeout (ms)", Description: "Maximum time to wait for a Modbus request.", DefaultValue: "3000", Minimum: 50, Maximum: 60000),
                new("maxGapElements", DriverConfigurationValueKind.Integer, DisplayName: "Maximum block gap", Description: "Maximum address gap merged into one polling block.", DefaultValue: "8", Minimum: 0, Maximum: 125, Advanced: true),
                new("unitId", DriverConfigurationValueKind.Integer, DisplayName: "Unit ID", Description: "Default Modbus unit identifier.", DefaultValue: "1", Minimum: 0, Maximum: 255)
            },
            TagBindingFields: TagBindingFields),
        Description: "Modbus TCP client driver using cyclic polling.");

    public CommunicationDriverTypeDescriptor Descriptor => SharedDescriptor;
}
