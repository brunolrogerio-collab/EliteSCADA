using System.Security.Cryptography.X509Certificates;
using Scada.Core.Tags;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaOpen62541SecureProductInteropTests
{
    private const string Basic256Sha256 = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256";
    private const string NamespaceUri = "urn:elitescada:interop:opcua";

    [Fact]
    public async Task ProductRuntime_SignAndEncrypt_UsesPinnedIndependentPeer()
    {
        string? endpoint = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_SECURE_L2_ENDPOINT");
        string? clientPfx = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_SECURE_L2_CLIENT_PFX");
        string? serverPin = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_SECURE_L2_SERVER_SHA256");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(clientPfx) || string.IsNullOrWhiteSpace(serverPin))
            return;

        var counter = Binding();
        var provider = new FileCertificateProvider(clientPfx);
        var options = new OpcUaRuntimeConnectionOptions(
            EndpointUrl: endpoint,
            SecurityMode: "SignAndEncrypt",
            SecurityPolicyUri: Basic256Sha256,
            AuthenticationMode: OpcUaRuntimeAuthenticationMode.Anonymous,
            ClientCertificateReference: "cert://opcua/l2-client",
            ApprovedServerApplicationUri: "urn:elitescada:interop:opcua:server",
            ApprovedServerCertificateSha256: serverPin,
            SessionTimeout: TimeSpan.FromSeconds(20),
            PublishingInterval: TimeSpan.FromMilliseconds(100));

        var factory = new OpcUaFoundationRuntimeSessionFactory(options, provider);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using IOpcUaRuntimeSession session = await factory.ConnectAsync([counter], timeout.Token);

        OpcUaRuntimeDataValue initial = await session.ReadAsync(counter, timeout.Token);
        Assert.Equal(TagQuality.Good, initial.Quality);
        Assert.Equal(7, Assert.IsType<int>(initial.Value));

        await using IAsyncEnumerator<OpcUaRuntimeDataValue> subscription = session.SubscribeAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.True(await subscription.MoveNextAsync());
        await session.WriteAsync(counter, 11, timeout.Token);

        OpcUaRuntimeDataValue? observed = null;
        while (await subscription.MoveNextAsync())
        {
            if (subscription.Current.TagId == counter.Tag.Id && subscription.Current.Value is int value && value == 11)
            {
                observed = subscription.Current;
                break;
            }
        }

        Assert.NotNull(observed);
        Assert.Equal(TagQuality.Good, observed!.Quality);
        Assert.Equal(1, provider.CertificateResolveCount);
    }

    private static OpcUaRuntimeBinding Binding()
    {
        TagDefinition tag = TagDefinition.Create(
            name: "SecureCounter",
            path: "Interop.OPCUA.SecureCounter",
            dataType: TagDataType.Int32,
            metadata: new Dictionary<string, string>
            {
                [OpcUaRuntimeBinding.NodeIdMetadataKey] = "ns=2;s=Lab.SecureCounter",
                [OpcUaRuntimeBinding.NamespaceUriMetadataKey] = NamespaceUri,
                [OpcUaRuntimeBinding.SamplingIntervalMetadataKey] = "00:00:00.100",
                [OpcUaRuntimeBinding.QueueSizeMetadataKey] = "10",
                [OpcUaRuntimeBinding.DiscardOldestMetadataKey] = "true"
            });
        return OpcUaRuntimeBinding.FromTag(tag);
    }

    private sealed class FileCertificateProvider(string pfxPath) : IOpcUaRuntimeSecurityMaterialProvider
    {
        public int CertificateResolveCount { get; private set; }

        public ValueTask<string> ResolveSecretAsync(string secretReference, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<string>(new InvalidOperationException("Secure anonymous L2 must not resolve a password."));

        public ValueTask<X509Certificate2> ResolveCertificateAsync(string certificateReference, CancellationToken cancellationToken = default)
        {
            CertificateResolveCount++;
            return ValueTask.FromResult(X509CertificateLoader.LoadPkcs12FromFile(pfxPath, string.Empty));
        }
    }
}
