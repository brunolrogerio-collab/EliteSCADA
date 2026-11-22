using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Modbus;

public sealed class ModbusRtuServerDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverTypeId = "modbus.rtu.server";

    public static CommunicationDriverTypeDescriptor SharedDescriptor { get; } = new(
        DriverType: DriverTypeId,
        DisplayName: "Modbus RTU Server",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.ConnectionTest,
        AcquisitionModes: new[] { DriverAcquisitionMode.EventDriven },
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            SchemaId: "modbus.rtu.server.engineering",
            SchemaVersion: 1,
            DataSourceFields: new DriverConfigurationFieldDescriptor[]
            {
                new("serialPort", DriverConfigurationValueKind.SerialPort, Required: true, DisplayName: "Server serial port", Description: "Serial device visible to the EliteSCADA API/DriverHost server. This port is exclusive while the RTU Server is active.", DisplayNameResourceKey: "driver.modbus.rtu.server.datasource.serialPort.label", DescriptionResourceKey: "driver.modbus.rtu.server.datasource.serialPort.description"),
                new("baudRate", DriverConfigurationValueKind.Integer, DisplayName: "Baud rate", Description: "Serial line speed.", DefaultValue: "9600", Minimum: 300, Maximum: 4000000, DisplayNameResourceKey: "driver.modbus.rtu.datasource.baudRate.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.baudRate.description"),
                new("dataBits", DriverConfigurationValueKind.Integer, DisplayName: "Data bits", Description: "Serial data bits.", DefaultValue: "8", Minimum: 5, Maximum: 8, DisplayNameResourceKey: "driver.modbus.rtu.datasource.dataBits.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.dataBits.description"),
                new("parity", DriverConfigurationValueKind.Enum, DisplayName: "Parity", Description: "Serial parity.", DefaultValue: "None", AllowedValues: new[] { "None", "Odd", "Even", "Mark", "Space" }, DisplayNameResourceKey: "driver.modbus.rtu.datasource.parity.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.parity.description"),
                new("stopBits", DriverConfigurationValueKind.Enum, DisplayName: "Stop bits", Description: "Serial stop bits.", DefaultValue: "One", AllowedValues: new[] { "One", "OnePointFive", "Two" }, DisplayNameResourceKey: "driver.modbus.rtu.datasource.stopBits.label", DescriptionResourceKey: "driver.modbus.rtu.datasource.stopBits.description"),
                new("unitId", DriverConfigurationValueKind.Integer, DisplayName: "Server Unit ID", Description: "RTU slave address served on this serial bus.", DefaultValue: "1", Minimum: 1, Maximum: 247, DisplayNameResourceKey: "driver.modbus.rtu.server.datasource.unitId.label", DescriptionResourceKey: "driver.modbus.rtu.server.datasource.unitId.description"),
                new("holdingRanges", DriverConfigurationValueKind.String, Required: true, DisplayName: "Holding Register ranges", Description: "Versioned allowed ranges. Use the structured range editor; canonical format is v1:start-end;start-end.", DefaultValue: "v1:0-999", DisplayNameResourceKey: "driver.modbus.server.datasource.holdingRanges.label", DescriptionResourceKey: "driver.modbus.server.datasource.holdingRanges.description"),
                new("frameTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DisplayName: "Frame timeout (ms)", Description: "Bounded time to receive the remainder of an RTU request frame after traffic starts.", DefaultValue: "1000", Minimum: 50, Maximum: 10000, Advanced: true, DisplayNameResourceKey: "driver.modbus.rtu.server.datasource.frameTimeoutMilliseconds.label", DescriptionResourceKey: "driver.modbus.rtu.server.datasource.frameTimeoutMilliseconds.description")
            },
            TagBindingFields: ModbusTcpServerDriverDescriptorProvider.SharedServerTagBindingFields),
        SupportsSharedTransportInfrastructure: true,
        Description: "Modbus RTU slave/server using the common Holding Register map over an exclusive host serial port.",
        DisplayNameResourceKey: "driver.modbus.rtu.server.displayName",
        DescriptionResourceKey: "driver.modbus.rtu.server.description",
        TagBindingSchemaId: ModbusTcpDriverDescriptorProvider.SharedDescriptor.TagBindingSchemaId
            ?? ModbusTcpDriverDescriptorProvider.SharedDescriptor.ConfigurationSchema.SchemaId,
        TagBindingSchemaVersion: ModbusTcpDriverDescriptorProvider.SharedDescriptor.TagBindingSchemaVersion
            ?? ModbusTcpDriverDescriptorProvider.SharedDescriptor.ConfigurationSchema.SchemaVersion,
        IntegrationDomains: new[] { IntegrationDomain.Industrial },
        ConnectionModel: DriverConnectionModel.HostSerial,
        ExternalDependencies: new[]
        {
            new DriverExternalDependencyDescriptor(DriverExternalDependencyKind.BuiltIn),
            new DriverExternalDependencyDescriptor(DriverExternalDependencyKind.HostResource, "serialPort")
        });

    public CommunicationDriverTypeDescriptor Descriptor => SharedDescriptor;
}
