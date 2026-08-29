using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Public, library-independent OPC UA driver contract exposed to Engineering.
/// Third-party OPC Foundation types must not escape through this descriptor.
/// </summary>
public sealed class OpcUaDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverTypeId = "opc-ua";
    public const string ConfigurationSchemaId = "elitescada.driver.opc-ua";
    public const int ContractVersion = 1;
    public const int ConfigurationSchemaVersion = 1;

    public static CommunicationDriverTypeDescriptor Definition { get; } = new(
        DriverType: DriverTypeId,
        DisplayName: "OPC UA",
        DriverContractVersion: ContractVersion,
        RuntimeCapabilities: DriverCapabilities.Read |
                             DriverCapabilities.Write |
                             DriverCapabilities.Subscribe |
                             DriverCapabilities.Diagnostics |
                             DriverCapabilities.SourceTimestamp |
                             DriverCapabilities.ServerTimestamp,
        EngineeringCapabilities: DriverEngineeringCapabilities.ConnectionTest |
                                 DriverEngineeringCapabilities.Discover |
                                 DriverEngineeringCapabilities.Browse |
                                 DriverEngineeringCapabilities.Reconcile,
        AcquisitionModes: [DriverAcquisitionMode.Subscription, DriverAcquisitionMode.Polling],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            ConfigurationSchemaId,
            ConfigurationSchemaVersion,
            DataSourceFields:
            [
                new("endpointUrl", DriverConfigurationValueKind.String, Required: true,
                    DisplayName: "Endpoint URL"),
                new("securityMode", DriverConfigurationValueKind.Enum, Required: true,
                    DisplayName: "Security mode", DefaultValue: "SignAndEncrypt",
                    AllowedValues: ["None", "Sign", "SignAndEncrypt"]),
                new("securityPolicyUri", DriverConfigurationValueKind.String, Required: true,
                    DisplayName: "Security policy URI"),
                new("authenticationMode", DriverConfigurationValueKind.Enum, Required: true,
                    DisplayName: "Authentication mode", DefaultValue: "Anonymous",
                    AllowedValues: ["Anonymous", "UserName", "Certificate"]),
                new("userName", DriverConfigurationValueKind.String,
                    DisplayName: "User name", Advanced: true),
                new("passwordSecretReference", DriverConfigurationValueKind.SecretReference,
                    DisplayName: "Password secret reference", Advanced: true),
                new("clientCertificateReference", DriverConfigurationValueKind.CertificateReference,
                    DisplayName: "Client certificate reference"),
                new("userCertificateReference", DriverConfigurationValueKind.CertificateReference,
                    DisplayName: "User certificate reference", Advanced: true),
                new("sessionTimeout", DriverConfigurationValueKind.Duration,
                    DisplayName: "Session timeout", DefaultValue: "00:01:00", Advanced: true),
                new("publishingInterval", DriverConfigurationValueKind.Duration,
                    DisplayName: "Publishing interval", DefaultValue: "00:00:01", Advanced: true),
                new("trustUntrustedServerCertificateForSession", DriverConfigurationValueKind.Boolean,
                    DisplayName: "Trust untrusted server certificate for this session",
                    DefaultValue: "false", Advanced: true)
            ],
            TagBindingFields:
            [
                new("nodeId", DriverConfigurationValueKind.Identifier, Required: true,
                    DisplayName: "NodeId"),
                new("namespaceUri", DriverConfigurationValueKind.String,
                    DisplayName: "Namespace URI"),
                new("samplingInterval", DriverConfigurationValueKind.Duration,
                    DisplayName: "Sampling interval", DefaultValue: "00:00:01", Advanced: true),
                new("queueSize", DriverConfigurationValueKind.Integer,
                    DisplayName: "Queue size", DefaultValue: "1", Minimum: 1, Maximum: 10000, Advanced: true),
                new("discardOldest", DriverConfigurationValueKind.Boolean,
                    DisplayName: "Discard oldest", DefaultValue: "true", Advanced: true)
            ]),
        SupportsSharedTransportInfrastructure: false,
        Description: "OPC UA client with secure sessions, browse/import, subscriptions, read/write and source/server timestamps.");

    public CommunicationDriverTypeDescriptor Descriptor => Definition;
}
