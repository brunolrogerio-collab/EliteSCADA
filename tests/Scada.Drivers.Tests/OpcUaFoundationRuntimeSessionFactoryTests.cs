using System.Security.Cryptography.X509Certificates;
using Scada.Core.Tags;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaFoundationRuntimeSessionFactoryTests
{
    private const string Basic256Sha256 = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256";

    [Fact]
    public async Task SecureConnectionWithoutApprovedPin_FailsBeforeResolvingSecurityMaterialOrNetwork()
    {
        var provider = new RejectingSecurityMaterialProvider();
        var options = new OpcUaRuntimeConnectionOptions(
            EndpointUrl: "opc.tcp://localhost:4840",
            SecurityMode: "SignAndEncrypt",
            SecurityPolicyUri: Basic256Sha256,
            AuthenticationMode: OpcUaRuntimeAuthenticationMode.Anonymous,
            ClientCertificateReference: "cert://client");
        var factory = new OpcUaFoundationRuntimeSessionFactory(options, provider);
        var binding = OpcUaRuntimeBinding.FromTag(CreateTag());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.ConnectAsync([binding], CancellationToken.None));

        Assert.Contains("SHA-256 pin", exception.Message);
        Assert.Equal(0, provider.SecretResolutionCalls);
        Assert.Equal(0, provider.CertificateResolutionCalls);
    }

    [Fact]
    public async Task EmptyBindingSet_FailsBeforeResolvingSecurityMaterialOrNetwork()
    {
        var provider = new RejectingSecurityMaterialProvider();
        var options = new OpcUaRuntimeConnectionOptions(
            EndpointUrl: "opc.tcp://localhost:4840",
            SecurityMode: "None",
            SecurityPolicyUri: "http://opcfoundation.org/UA/SecurityPolicy#None");
        var factory = new OpcUaFoundationRuntimeSessionFactory(options, provider);

        await Assert.ThrowsAsync<ArgumentException>(
            () => factory.ConnectAsync(Array.Empty<OpcUaRuntimeBinding>(), CancellationToken.None));

        Assert.Equal(0, provider.SecretResolutionCalls);
        Assert.Equal(0, provider.CertificateResolutionCalls);
    }

    private static TagDefinition CreateTag() => TagDefinition.Create(
        name: "Value",
        path: $"Area.Foundation.{Guid.NewGuid():N}",
        dataType: TagDataType.Double,
        metadata: new Dictionary<string, string>
        {
            [OpcUaRuntimeBinding.NodeIdMetadataKey] = "ns=2;s=Value",
            [OpcUaRuntimeBinding.NamespaceUriMetadataKey] = "urn:elite:test"
        });

    private sealed class RejectingSecurityMaterialProvider : IOpcUaRuntimeSecurityMaterialProvider
    {
        public int SecretResolutionCalls { get; private set; }
        public int CertificateResolutionCalls { get; private set; }

        public ValueTask<string> ResolveSecretAsync(
            string secretReference,
            CancellationToken cancellationToken = default)
        {
            SecretResolutionCalls++;
            throw new InvalidOperationException("Security material must not be resolved by this test path.");
        }

        public ValueTask<X509Certificate2> ResolveCertificateAsync(
            string certificateReference,
            CancellationToken cancellationToken = default)
        {
            CertificateResolutionCalls++;
            throw new InvalidOperationException("Security material must not be resolved by this test path.");
        }
    }
}
