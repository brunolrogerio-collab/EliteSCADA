using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Bacnet;

public static class BacnetDriverDescriptor
{
    public const string DriverType = "bacnet.ip";
    public const string SchemaId = "scada.driver.bacnet.ip";

    public static readonly CommunicationDriverTypeDescriptor Instance = new(
        DriverType,
        "BACnet/IP",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.ConnectionTest |
                                 DriverEngineeringCapabilities.Discover |
                                 DriverEngineeringCapabilities.Browse |
                                 DriverEngineeringCapabilities.Reconcile,
        AcquisitionModes: new[] { DriverAcquisitionMode.Subscription, DriverAcquisitionMode.Polling, DriverAcquisitionMode.Hybrid },
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            SchemaId,
            SchemaVersion: 2,
            DataSourceFields: new DriverConfigurationFieldDescriptor[]
            {
                new("deviceInstance", DriverConfigurationValueKind.Integer, Required: true, DisplayName: "Device instance", Minimum: 0, Maximum: BacnetBinding.MaximumDeviceInstance),
                new("targetAddress", DriverConfigurationValueKind.String, Required: false, DisplayName: "Manual target IPv4 address", Description: "Optional IPv4 address with UDP port, for example 192.168.1.20:47808."),
                new("localPort", DriverConfigurationValueKind.Port, DefaultValue: "47808", DisplayName: "Local BACnet/IP UDP port", Advanced: true),
                new("scanIntervalMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "1000", DisplayName: "Polling interval (ms)", Minimum: 50, Maximum: 600000),
                new("requestTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "3000", DisplayName: "Request timeout (ms)", Minimum: 100, Maximum: 60000),
                new("discoveryWindowMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "1500", DisplayName: "Discovery window (ms)", Minimum: 100, Maximum: 30000, Advanced: true),
                new("bbmdAddress", DriverConfigurationValueKind.Host, Required: false, DisplayName: "BBMD address", Advanced: true),
                new("foreignDeviceTtlSeconds", DriverConfigurationValueKind.Integer, Required: false, DisplayName: "Foreign device TTL", Minimum: 30, Maximum: 32767, Advanced: true)
            },
            TagBindingFields: new DriverConfigurationFieldDescriptor[]
            {
                new("deviceInstance", DriverConfigurationValueKind.Integer, Required: true, Minimum: 0, Maximum: BacnetBinding.MaximumDeviceInstance),
                new("objectType", DriverConfigurationValueKind.Integer, Required: true, DisplayName: "Object type", Minimum: 0),
                new("objectInstance", DriverConfigurationValueKind.Integer, Required: true, DisplayName: "Object instance", Minimum: 0, Maximum: BacnetBinding.MaximumObjectInstance),
                new("propertyIdentifier", DriverConfigurationValueKind.Integer, Required: true, DisplayName: "Property identifier", Minimum: 0),
                new("arrayIndex", DriverConfigurationValueKind.Integer, Required: false, DisplayName: "Array index", Minimum: 0, Advanced: true),
                new("useCov", DriverConfigurationValueKind.Boolean, Required: false, DefaultValue: "true", DisplayName: "Use COV when supported"),
                new("writePriority", DriverConfigurationValueKind.Integer, Required: false, DisplayName: "BACnet write priority", Minimum: 1, Maximum: 16, Advanced: true)
            }),
        SupportsSharedTransportInfrastructure: true,
        Description: "BACnet/IP over UDP. BACnet Secure Connect is deliberately not advertised by this driver type.");
}
