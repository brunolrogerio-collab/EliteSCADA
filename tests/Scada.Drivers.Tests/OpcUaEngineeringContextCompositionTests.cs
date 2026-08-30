using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaEngineeringContextCompositionTests
{
    private const string Basic256Sha256 = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256";

    [Fact]
    public void ParseConnectionOptions_ContextMergesOnlyProtectedReferences()
    {
        var context = new DriverEngineeringDataSourceContext(
            "line-1",
            "Line 1",
            OpcUaDriverDescriptorProvider.DriverTypeId,
            new Dictionary<string, string>
            {
                ["endpointUrl"] = "opc.tcp://server:4840",
                ["securityMode"] = "SignAndEncrypt",
                ["securityPolicyUri"] = Basic256Sha256,
                ["authenticationMode"] = "UserName",
                ["userName"] = "operator",
                ["serverCertificateSha256"] = new string('A', 64)
            },
            new Dictionary<string, string>
            {
                ["passwordSecretReference"] = "secret://opcua/operator",
                ["clientCertificateReference"] = "cert://opcua/client",
                ["endpointUrl"] = "opc.tcp://attacker:4840",
                ["securityMode"] = "None"
            });

        var options = OpcUaRuntimeDriverComposer.ParseConnectionOptions(context);

        Assert.Equal("opc.tcp://server:4840", options.EndpointUrl);
        Assert.Equal("SignAndEncrypt", options.SecurityMode);
        Assert.Equal("secret://opcua/operator", options.PasswordSecretReference);
        Assert.Equal("cert://opcua/client", options.ClientCertificateReference);
    }

    [Fact]
    public void ParseConnectionOptions_ContextSettingsWinForProtectedReference()
    {
        var context = new DriverEngineeringDataSourceContext(
            "line-1",
            "Line 1",
            OpcUaDriverDescriptorProvider.DriverTypeId,
            new Dictionary<string, string>
            {
                ["endpointUrl"] = "opc.tcp://server:4840",
                ["securityMode"] = "SignAndEncrypt",
                ["securityPolicyUri"] = Basic256Sha256,
                ["authenticationMode"] = "UserName",
                ["userName"] = "operator",
                ["passwordSecretReference"] = "secret://canonical/operator",
                ["clientCertificateReference"] = "cert://canonical/client",
                ["serverCertificateSha256"] = new string('B', 64)
            },
            new Dictionary<string, string>
            {
                ["passwordSecretReference"] = "secret://shadow/operator",
                ["clientCertificateReference"] = "cert://shadow/client"
            });

        var options = OpcUaRuntimeDriverComposer.ParseConnectionOptions(context);

        Assert.Equal("secret://canonical/operator", options.PasswordSecretReference);
        Assert.Equal("cert://canonical/client", options.ClientCertificateReference);
    }
}
