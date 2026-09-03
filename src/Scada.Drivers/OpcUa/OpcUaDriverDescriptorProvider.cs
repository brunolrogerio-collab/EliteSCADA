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
    public const int ConfigurationSchemaVersion = 2;

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
                    DisplayName: "Endpoint URL",
                    DisplayNameResourceKey: "driver.opcua.datasource.endpointUrl.label"),
                new("securityMode", DriverConfigurationValueKind.Enum, Required: true,
                    DisplayName: "Security mode", DefaultValue: "SignAndEncrypt",
                    AllowedValues: ["None", "Sign", "SignAndEncrypt"],
                    DisplayNameResourceKey: "driver.opcua.datasource.securityMode.label"),
                new("securityPolicyUri", DriverConfigurationValueKind.String, Required: true,
                    DisplayName: "Security policy URI",
                    DisplayNameResourceKey: "driver.opcua.datasource.securityPolicyUri.label"),
                new("serverApplicationUri", DriverConfigurationValueKind.String,
                    DisplayName: "Approved server ApplicationUri", Advanced: true,
                    DisplayNameResourceKey: "driver.opcua.datasource.serverApplicationUri.label"),
                new("serverCertificateSha256", DriverConfigurationValueKind.String,
                    DisplayName: "Approved server certificate SHA-256", Advanced: true,
                    DisplayNameResourceKey: "driver.opcua.datasource.serverCertificateSha256.label"),
                new("authenticationMode", DriverConfigurationValueKind.Enum, Required: true,
                    DisplayName: "Authentication mode", DefaultValue: "Anonymous",
                    AllowedValues: ["Anonymous", "UserName", "Certificate"],
                    DisplayNameResourceKey: "driver.opcua.datasource.authenticationMode.label"),
                new("userName", DriverConfigurationValueKind.String,
                    DisplayName: "User name", Advanced: true,
                    DisplayNameResourceKey: "driver.opcua.datasource.userName.label"),
                new("passwordSecretReference", DriverConfigurationValueKind.SecretReference,
                    DisplayName: "Password secret reference", Advanced: true,
                    DisplayNameResourceKey: "driver.opcua.datasource.passwordSecretReference.label"),
                new("clientCertificateReference", DriverConfigurationValueKind.CertificateReference,
                    DisplayName: "Client certificate reference",
                    DisplayNameResourceKey: "driver.opcua.datasource.clientCertificateReference.label"),
                new("userCertificateReference", DriverConfigurationValueKind.CertificateReference,
                    DisplayName: "User certificate reference", Advanced: true,
                    DisplayNameResourceKey: "driver.opcua.datasource.userCertificateReference.label"),
                new("sessionTimeout", DriverConfigurationValueKind.Duration,
                    DisplayName: "Session timeout", DefaultValue: "00:01:00", Advanced: true,
                    DisplayNameResourceKey: "driver.opcua.datasource.sessionTimeout.label"),
                new("publishingInterval", DriverConfigurationValueKind.Duration,
                    DisplayName: "Publishing interval", DefaultValue: "00:00:01", Advanced: true,
                    DisplayNameResourceKey: "driver.opcua.datasource.publishingInterval.label"),
                new("trustUntrustedServerCertificateForSession", DriverConfigurationValueKind.Boolean,
                    DisplayName: "Allow untrusted certificate for temporary Engineering session",
                    DefaultValue: "false", Advanced: true,
                    DisplayNameResourceKey: "driver.opcua.datasource.trustUntrustedServerCertificateForSession.label")
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
