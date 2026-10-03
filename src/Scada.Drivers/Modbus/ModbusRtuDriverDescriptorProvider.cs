using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Modbus;

public sealed class ModbusRtuDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverTypeId = "modbus.rtu";

    public static CommunicationDriverTypeDescriptor SharedDescriptor { get; } = new(
        DriverType: DriverTypeId,
        DisplayName: "Modbus RTU Master",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.PointReadTest | DriverEngineeringCapabilities.ConnectionTest,
        AcquisitionModes: new[] { DriverAcquisitionMode.Polling },
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            SchemaId: "modbus.rtu.engineering",
            SchemaVersion: 1,
            DataSourceFields: new DriverConfigurationFieldDescriptor[]
            {
                new("serialPort", DriverConfigurationValueKind.SerialPort, Required: true, DisplayName: "Server serial port", Description: "Serial device visible to the EliteSCADA API/DriverHost server. The device may be configured while offline.", DisplayNameResourceKey: "driver.modbus.rtu.datasource.serialPort.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.serialPort.description"),
                new("baudRate", DriverConfigurationValueKind.Integer, DisplayName: "Baud rate", Description: "Serial line speed.", DefaultValue: "9600", Minimum: 300, Maximum: 4000000, DisplayNameResourceKey: "driver.modbus.rtu.datasource.baudRate.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.baudRate.description"),
                new("dataBits", DriverConfigurationValueKind.Integer, DisplayName: "Data bits", Description: "Serial data bits.", DefaultValue: "8", Minimum: 5, Maximum: 8, DisplayNameResourceKey: "driver.modbus.rtu.datasource.dataBits.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.dataBits.description"),
                new("parity", DriverConfigurationValueKind.Enum, DisplayName: "Parity", Description: "Serial parity.", DefaultValue: "None", AllowedValues: new[] { "None", "Odd", "Even", "Mark", "Space" }, DisplayNameResourceKey: "driver.modbus.rtu.datasource.parity.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.parity.description"),
                new("stopBits", DriverConfigurationValueKind.Enum, DisplayName: "Stop bits", Description: "Serial stop bits.", DefaultValue: "One", AllowedValues: new[] { "One", "OnePointFive", "Two" }, DisplayNameResourceKey: "driver.modbus.rtu.datasource.stopBits.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.stopBits.description"),
                new("scanIntervalMilliseconds", DriverConfigurationValueKind.Integer, DisplayName: "Scan interval (ms)", Description: "Polling interval in milliseconds.", DefaultValue: "1000", Minimum: 10, Maximum: 600000, DisplayNameResourceKey: "driver.modbus.rtu.datasource.scanIntervalMilliseconds.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.scanIntervalMilliseconds.description"),
                new("requestTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DisplayName: "Request timeout (ms)", Description: "Maximum time to wait for one RTU response.", DefaultValue: "3000", Minimum: 50, Maximum: 60000, DisplayNameResourceKey: "driver.modbus.rtu.datasource.requestTimeoutMilliseconds.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.requestTimeoutMilliseconds.description"),
                new("maxGapElements", DriverConfigurationValueKind.Integer, DisplayName: "Maximum block gap", Description: "Maximum address gap merged into one polling block.", DefaultValue: "8", Minimum: 0, Maximum: 125, Advanced: true, DisplayNameResourceKey: "driver.modbus.rtu.datasource.maxGapElements.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.maxGapElements.description"),
                new("unitId", DriverConfigurationValueKind.Integer, DisplayName: "Default Unit ID", Description: "Default Modbus slave address for TAGs without an override.", DefaultValue: "1", Minimum: 0, Maximum: 247, DisplayNameResourceKey: "driver.modbus.rtu.datasource.unitId.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.unitId.description")
            },
            TagBindingFields: ModbusTcpDriverDescriptorProvider.SharedTagBindingFields),
        SupportsSharedTransportInfrastructure: true,
        Description: "Modbus RTU master using a host-owned shared serial bus.",
        DisplayNameResourceKey: "driver.modbus.rtu.displayName",
        DescriptionResourceKey: "driver.modbus.rtu.description",
        TagBindingSchemaId: ModbusTcpDriverDescriptorProvider.SharedDescriptor.TagBindingSchemaId
            ?? ModbusTcpDriverDescriptorProvider.SharedDescriptor.ConfigurationSchema.SchemaId,
        TagBindingSchemaVersion: ModbusTcpDriverDescriptorProvider.SharedDescriptor.TagBindingSchemaVersion
            ?? ModbusTcpDriverDescriptorProvider.SharedDescriptor.ConfigurationSchema.SchemaVersion);

    public CommunicationDriverTypeDescriptor Descriptor => SharedDescriptor;
}
