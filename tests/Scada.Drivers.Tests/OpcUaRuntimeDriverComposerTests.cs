using System.Security.Cryptography.X509Certificates;
using Scada.Core.Tags;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaRuntimeDriverComposerTests
{
    private const string Basic256Sha256 = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256";

    [Fact]
    public void ParseConnectionOptions_MapsDescriptorSettingsAndKeepsReferencesOpaque()
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["endpointUrl"] = "opc.tcp://server:4840",
            ["securityMode"] = "SignAndEncrypt",
            ["securityPolicyUri"] = Basic256Sha256,
            ["authenticationMode"] = "UserName",
            ["userName"] = "operator",
            ["passwordSecretReference"] = "secret://opcua/operator",
            ["clientCertificateReference"] = "cert://opcua/client",
            ["serverApplicationUri"] = "urn:server:opcua",
            ["serverCertificateSha256"] = new string('A', 64),
            ["sessionTimeout"] = "00:02:00",
            ["publishingInterval"] = "00:00:00.250"
        };

        var options = OpcUaRuntimeDriverComposer.ParseConnectionOptions(settings);

        Assert.Equal("opc.tcp://server:4840", options.EndpointUrl);
        Assert.Equal("SignAndEncrypt", options.SecurityMode);
        Assert.Equal(Basic256Sha256, options.SecurityPolicyUri);
        Assert.Equal(OpcUaRuntimeAuthenticationMode.UserName, options.AuthenticationMode);
        Assert.Equal("operator", options.UserName);
        Assert.Equal("secret://opcua/operator", options.PasswordSecretReference);
        Assert.Equal("cert://opcua/client", options.ClientCertificateReference);
        Assert.Equal("urn:server:opcua", options.ApprovedServerApplicationUri);
        Assert.Equal(new string('A', 64), options.NormalizedApprovedServerCertificateSha256);
        Assert.Equal(TimeSpan.FromMinutes(2), options.EffectiveSessionTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.EffectivePublishingInterval);
    }

    [Fact]
    public void Create_ComposesDriverWithoutResolvingSecurityMaterial()
    {
        var provider = new CountingSecurityMaterialProvider();
        var settings = new Dictionary<string, string>
        {
            ["endpointUrl"] = "opc.tcp://server:4840",
            ["securityMode"] = "SignAndEncrypt",
            ["securityPolicyUri"] = Basic256Sha256,
            ["clientCertificateReference"] = "cert://opcua/client",
            ["serverCertificateSha256"] = new string('B', 64)
        };
        var tag = CreateTag();
        var cache = new FakeCache();
        var registry = new FakeRegistry();

        var driver = OpcUaRuntimeDriverComposer.Create(
            "line-1",
            "Line 1 OPC UA",
            settings,
            [tag],
            cache,
            registry,
            provider,
            [TimeSpan.Zero]);

        Assert.Equal("opc-ua:line-1", driver.DriverId);
        Assert.Equal("Line 1 OPC UA", driver.Name);
        Assert.Equal(tag.Id, Assert.Single(driver.Tags).Id);
        Assert.Equal(0, provider.SecretResolutionCalls);
        Assert.Equal(0, provider.CertificateResolutionCalls);
    }

    [Fact]
    public void ParseConnectionOptions_RejectsMalformedDuration()
    {
        var settings = new Dictionary<string, string>
        {
            ["endpointUrl"] = "opc.tcp://server:4840",
            ["securityMode"] = "None",
            ["securityPolicyUri"] = "http://opcfoundation.org/UA/SecurityPolicy#None",
            ["sessionTimeout"] = "tomorrow-ish"
        };

        Assert.Throws<ArgumentException>(
            () => OpcUaRuntimeDriverComposer.ParseConnectionOptions(settings));
    }

    private static TagDefinition CreateTag() => TagDefinition.Create(
        name: "Value",
        path: "Area.Line1.Value",
        dataType: TagDataType.Double,
        metadata: new Dictionary<string, string>
        {
            [OpcUaRuntimeBinding.NodeIdMetadataKey] = "ns=2;s=Value",
            [OpcUaRuntimeBinding.NamespaceUriMetadataKey] = "urn:line:1"
        });

    private sealed class CountingSecurityMaterialProvider : IOpcUaRuntimeSecurityMaterialProvider
    {
        public int SecretResolutionCalls { get; private set; }
        public int CertificateResolutionCalls { get; private set; }

        public ValueTask<string> ResolveSecretAsync(
            string secretReference,
            CancellationToken cancellationToken = default)
        {
            SecretResolutionCalls++;
            throw new InvalidOperationException("Resolution is not expected during composition.");
        }

        public ValueTask<X509Certificate2> ResolveCertificateAsync(
            string certificateReference,
            CancellationToken cancellationToken = default)
        {
            CertificateResolutionCalls++;
            throw new InvalidOperationException("Resolution is not expected during composition.");
        }
    }

    private sealed class FakeCache : ICurrentTagCache
    {
        public bool TryGet(Guid tagId, out TagValue? value)
        {
            value = null;
            return false;
        }

        public IReadOnlyCollection<TagValue> Snapshot() => Array.Empty<TagValue>();

        public ValueTask<TagValue?> UpdateAsync(
            TagDefinition tag,
            TagValue value,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<TagValue?>(null);
    }

    private sealed class FakeRegistry : ITagRegistry
    {
        public TagDefinition Register(TagDefinition tag) => tag;
        public TagDefinition Upsert(TagDefinition tag) => tag;

        public bool TryGet(Guid tagId, out TagDefinition? tag)
        {
            tag = null;
            return false;
        }

        public bool TryGetByPath(string path, out TagDefinition? tag)
        {
            tag = null;
            return false;
        }

        public IReadOnlyCollection<TagDefinition> Snapshot() => Array.Empty<TagDefinition>();
    }
}
