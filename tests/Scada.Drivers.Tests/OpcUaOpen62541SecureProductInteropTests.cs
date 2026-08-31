using System.Security.Cryptography.X509Certificates;
using Scada.Core.Tags;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaOpen62541SecureProductInteropTests
{
    private const string Basic256Sha256 = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256";
    private const string NamespaceUri = "urn:elitescada:interop:opcua";
    private const string ClientCertificateReference = "cert://opcua/l2-client";
    private const string UserPasswordReference = "secret://opcua/l2-user-password";

    [Fact]
    public async Task ProductRuntime_SignAndEncrypt_UsesPinnedIndependentPeer()
    {
        if (!TryGetEnvironment(out string endpoint, out string clientPfx, out string serverPin))
            return;

        var counter = Binding("SecureCounter", "Lab.SecureCounter");
        var provider = new FileSecurityMaterialProvider(clientPfx);
        var options = SecureOptions(endpoint, serverPin, OpcUaRuntimeAuthenticationMode.Anonymous);

        var factory = new OpcUaFoundationRuntimeSessionFactory(options, provider);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using IOpcUaRuntimeSession session = await factory.ConnectAsync([counter], timeout.Token);

        OpcUaRuntimeDataValue initial = await session.ReadAsync(counter, timeout.Token);
        Assert.Equal(TagQuality.Good, initial.Quality);
        Assert.Equal(7, Assert.IsType<int>(initial.Value));

        await using IAsyncEnumerator<OpcUaRuntimeDataValue> subscription =
            session.SubscribeAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.True(await subscription.MoveNextAsync());
        await session.WriteAsync(counter, 11, timeout.Token);

        OpcUaRuntimeDataValue? observed = null;
        while (await subscription.MoveNextAsync())
        {
            if (subscription.Current.TagId == counter.Tag.Id &&
                subscription.Current.Value is int value && value == 11)
            {
                observed = subscription.Current;
                break;
            }
        }

        Assert.NotNull(observed);
        Assert.Equal(TagQuality.Good, observed!.Quality);
        Assert.Equal(1, provider.CertificateResolveCount);
        Assert.Equal(0, provider.SecretResolveCount);
    }

    [Fact]
    public async Task ProductRuntime_UserNameOverSignAndEncrypt_ResolvesSecretAndAuthenticates()
    {
        if (!TryGetEnvironment(out string endpoint, out string clientPfx, out string serverPin))
            return;

        var counter = Binding("UserCounter", "Lab.UserCounter");
        var provider = new FileSecurityMaterialProvider(clientPfx, UserPasswordReference, "elite-pass");
        var options = SecureOptions(
            endpoint,
            serverPin,
            OpcUaRuntimeAuthenticationMode.UserName,
            userName: "elite-user",
            passwordSecretReference: UserPasswordReference);

        var factory = new OpcUaFoundationRuntimeSessionFactory(options, provider);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using IOpcUaRuntimeSession session = await factory.ConnectAsync([counter], timeout.Token);

        OpcUaRuntimeDataValue initial = await session.ReadAsync(counter, timeout.Token);
        Assert.Equal(TagQuality.Good, initial.Quality);
        Assert.Equal(17, Assert.IsType<int>(initial.Value));

        await session.WriteAsync(counter, 23, timeout.Token);
        OpcUaRuntimeDataValue afterWrite = await session.ReadAsync(counter, timeout.Token);
        Assert.Equal(TagQuality.Good, afterWrite.Quality);
        Assert.Equal(23, Assert.IsType<int>(afterWrite.Value));

        Assert.Equal(1, provider.CertificateResolveCount);
        Assert.Equal(1, provider.SecretResolveCount);
    }

    [Fact]
    public async Task ProductRuntime_X509UserIdentity_ReusesPinnedApplicationCertificate()
    {
        if (!TryGetEnvironment(out string endpoint, out string clientPfx, out string serverPin))
            return;

        var counter = Binding("CertificateCounter", "Lab.CertificateCounter");
        var provider = new FileSecurityMaterialProvider(clientPfx);
        var options = SecureOptions(
            endpoint,
            serverPin,
            OpcUaRuntimeAuthenticationMode.Certificate,
            userCertificateReference: ClientCertificateReference);

        var factory = new OpcUaFoundationRuntimeSessionFactory(options, provider);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using IOpcUaRuntimeSession session = await factory.ConnectAsync([counter], timeout.Token);

        OpcUaRuntimeDataValue initial = await session.ReadAsync(counter, timeout.Token);
        Assert.Equal(TagQuality.Good, initial.Quality);
        Assert.Equal(27, Assert.IsType<int>(initial.Value));

        await session.WriteAsync(counter, 31, timeout.Token);
        OpcUaRuntimeDataValue afterWrite = await session.ReadAsync(counter, timeout.Token);
        Assert.Equal(TagQuality.Good, afterWrite.Quality);
        Assert.Equal(31, Assert.IsType<int>(afterWrite.Value));

        Assert.Equal(1, provider.CertificateResolveCount);
        Assert.Equal(0, provider.SecretResolveCount);
    }

    private static OpcUaRuntimeConnectionOptions SecureOptions(
        string endpoint,
        string serverPin,
        OpcUaRuntimeAuthenticationMode authenticationMode,
        string? userName = null,
        string? passwordSecretReference = null,
        string? userCertificateReference = null) =>
        new(
            EndpointUrl: endpoint,
            SecurityMode: "SignAndEncrypt",
            SecurityPolicyUri: Basic256Sha256,
            AuthenticationMode: authenticationMode,
            UserName: userName,
            PasswordSecretReference: passwordSecretReference,
            ClientCertificateReference: ClientCertificateReference,
            UserCertificateReference: userCertificateReference,
            ApprovedServerApplicationUri: "urn:elitescada:interop:opcua:server",
            ApprovedServerCertificateSha256: serverPin,
            SessionTimeout: TimeSpan.FromSeconds(20),
            PublishingInterval: TimeSpan.FromMilliseconds(100));

    private static OpcUaRuntimeBinding Binding(string name, string nodeId)
    {
        TagDefinition tag = TagDefinition.Create(
            name: name,
            path: $"Interop.OPCUA.{name}",
            dataType: TagDataType.Int32,
            metadata: new Dictionary<string, string>
            {
                [OpcUaRuntimeBinding.NodeIdMetadataKey] = $"ns=2;s={nodeId}",
                [OpcUaRuntimeBinding.NamespaceUriMetadataKey] = NamespaceUri,
                [OpcUaRuntimeBinding.SamplingIntervalMetadataKey] = "00:00:00.100",
                [OpcUaRuntimeBinding.QueueSizeMetadataKey] = "10",
                [OpcUaRuntimeBinding.DiscardOldestMetadataKey] = "true"
            });
        return OpcUaRuntimeBinding.FromTag(tag);
    }

    private static bool TryGetEnvironment(out string endpoint, out string clientPfx, out string serverPin)
    {
        endpoint = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_SECURE_L2_ENDPOINT") ?? string.Empty;
        clientPfx = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_SECURE_L2_CLIENT_PFX") ?? string.Empty;
        serverPin = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_SECURE_L2_SERVER_SHA256") ?? string.Empty;
        return !string.IsNullOrWhiteSpace(endpoint) &&
               !string.IsNullOrWhiteSpace(clientPfx) &&
               !string.IsNullOrWhiteSpace(serverPin);
    }

    private sealed class FileSecurityMaterialProvider : IOpcUaRuntimeSecurityMaterialProvider
    {
        private readonly string _pfxPath;
        private readonly string? _secretReference;
        private readonly string? _secretValue;

        public FileSecurityMaterialProvider(
            string pfxPath,
            string? secretReference = null,
            string? secretValue = null)
        {
            _pfxPath = pfxPath;
            _secretReference = secretReference;
            _secretValue = secretValue;
        }

        public int CertificateResolveCount { get; private set; }
        public int SecretResolveCount { get; private set; }

        public ValueTask<string> ResolveSecretAsync(
            string secretReference,
            CancellationToken cancellationToken = default)
        {
            SecretResolveCount++;
            if (_secretValue is null ||
                !string.Equals(secretReference, _secretReference, StringComparison.Ordinal))
            {
                return ValueTask.FromException<string>(new InvalidOperationException(
                    "The secure L2 runtime requested an unexpected secret reference."));
            }

            return ValueTask.FromResult(_secretValue);
        }

        public ValueTask<X509Certificate2> ResolveCertificateAsync(
            string certificateReference,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(certificateReference, ClientCertificateReference, StringComparison.Ordinal))
            {
                return ValueTask.FromException<X509Certificate2>(new InvalidOperationException(
                    "The secure L2 runtime requested an unexpected certificate reference."));
            }

            CertificateResolveCount++;
            return ValueTask.FromResult(
                X509CertificateLoader.LoadPkcs12FromFile(_pfxPath, string.Empty));
        }
    }
}
