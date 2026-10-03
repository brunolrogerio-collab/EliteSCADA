using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Modbus;

public sealed class ModbusTcpServerDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverTypeId = "modbus.tcp.server";

    private static readonly IReadOnlyCollection<DriverConfigurationFieldDescriptor> ServerTagBindingFields =
        ModbusTcpDriverDescriptorProvider.SharedTagBindingFields
            .Concat(new[]
            {
                new DriverConfigurationFieldDescriptor(
                    "modbus.server.clientAccess",
                    DriverConfigurationValueKind.Enum,
                    DisplayName: "External client access",
                    Description: "Controls whether external Modbus clients may write this Server TAG. Internal EliteSCADA writes remain governed separately.",
                    DefaultValue: "ReadOnly",
                    AllowedValues: new[] { "ReadOnly", "ReadWrite" },
                    DisplayNameResourceKey: "driver.modbus.server.tag.clientAccess.label",
                    DescriptionResourceKey: "driver.modbus.server.tag.clientAccess.description")
            })
            .ToArray();

    internal static IReadOnlyCollection<DriverConfigurationFieldDescriptor> SharedServerTagBindingFields => ServerTagBindingFields;

    public static CommunicationDriverTypeDescriptor SharedDescriptor { get; } = new(
        DriverType: DriverTypeId,
        DisplayName: "Modbus TCP Server",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.ConnectionTest,
        AcquisitionModes: new[] { DriverAcquisitionMode.EventDriven },
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            SchemaId: "modbus.tcp.server.engineering",
            SchemaVersion: 1,
            DataSourceFields: new DriverConfigurationFieldDescriptor[]
            {
                new("bindAddress", DriverConfigurationValueKind.Host, DisplayName: "Bind address", Description: "Local server address to listen on. Use 0.0.0.0 for all IPv4 interfaces.", DefaultValue: "0.0.0.0", DisplayNameResourceKey: "driver.modbus.tcp.server.datasource.bindAddress.label", DescriptionResourceKey: "driver.modbus.tcp.server.datasource.bindAddress.description"),
                new("port", DriverConfigurationValueKind.Port, DisplayName: "Port", Description: "TCP listen port owned by this Data Source.", DefaultValue: "502", Minimum: 1, Maximum: 65535, DisplayNameResourceKey: "driver.modbus.tcp.server.datasource.port.label", DescriptionResourceKey: "driver.modbus.tcp.server.datasource.port.description"),
                new("unitId", DriverConfigurationValueKind.Integer, DisplayName: "Server Unit ID", Description: "Unit identifier accepted by this server endpoint.", DefaultValue: "1", Minimum: 0, Maximum: 247, DisplayNameResourceKey: "driver.modbus.tcp.server.datasource.unitId.label", DescriptionResourceKey: "driver.modbus.tcp.server.datasource.unitId.description"),
                new("holdingRanges", DriverConfigurationValueKind.String, Required: true, DisplayName: "Holding Register ranges", Description: "Versioned allowed ranges. Use the structured range editor; canonical format is v1:start-end;start-end.", DefaultValue: "v1:0-999", DisplayNameResourceKey: "driver.modbus.server.datasource.holdingRanges.label", DescriptionResourceKey: "driver.modbus.server.datasource.holdingRanges.description"),
                new("maxClients", DriverConfigurationValueKind.Integer, DisplayName: "Maximum clients", Description: "Maximum simultaneous TCP client connections.", DefaultValue: "16", Minimum: 1, Maximum: 128, Advanced: true, DisplayNameResourceKey: "driver.modbus.tcp.server.datasource.maxClients.label", DescriptionResourceKey: "driver.modbus.tcp.server.datasource.maxClients.description"),
                new("clientIdleTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DisplayName: "Client idle timeout (ms)", Description: "Maximum bounded wait for the next client request.", DefaultValue: "30000", Minimum: 1000, Maximum: 600000, Advanced: true, DisplayNameResourceKey: "driver.modbus.tcp.server.datasource.clientIdleTimeoutMilliseconds.label", DescriptionResourceKey: "driver.modbus.tcp.server.datasource.clientIdleTimeoutMilliseconds.description")
            },
            TagBindingFields: ServerTagBindingFields),
        Description: "Modbus TCP server exposing canonical EliteSCADA TAGs as bounded Holding Registers.",
        DisplayNameResourceKey: "driver.modbus.tcp.server.displayName",
        DescriptionResourceKey: "driver.modbus.tcp.server.description",
        TagBindingSchemaId: ModbusTcpDriverDescriptorProvider.SharedDescriptor.TagBindingSchemaId
            ?? ModbusTcpDriverDescriptorProvider.SharedDescriptor.ConfigurationSchema.SchemaId,
        TagBindingSchemaVersion: ModbusTcpDriverDescriptorProvider.SharedDescriptor.TagBindingSchemaVersion
            ?? ModbusTcpDriverDescriptorProvider.SharedDescriptor.ConfigurationSchema.SchemaVersion);

    public CommunicationDriverTypeDescriptor Descriptor => SharedDescriptor;
}
