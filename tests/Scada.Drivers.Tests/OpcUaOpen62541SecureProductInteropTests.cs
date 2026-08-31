using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
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

    [Fact]
    public async Task CommunicationDriver_PeerRestart_ReconnectsAndResubscribesOverPinnedSecureChannel()
    {
        if (!TryGetEnvironment(out string endpoint, out string clientPfx, out string serverPin))
            return;

        string containerName = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_SECURE_L2_CONTAINER") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(containerName))
            return;

        var binding = Binding("RecoveryCounter", "Lab.RecoveryCounter");
        var tag = binding.Tag;
        var provider = new FileSecurityMaterialProvider(clientPfx);
        var factory = new OpcUaFoundationRuntimeSessionFactory(
            SecureOptions(endpoint, serverPin, OpcUaRuntimeAuthenticationMode.Anonymous),
            provider);
        var cache = new RecordingCache();
        var registry = new FakeRegistry();
        await using var driver = new OpcUaCommunicationDriver(
            "opcua-secure-recovery",
            "OPC UA Secure Recovery L2",
            cache,
            registry,
            [tag],
            factory,
            [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500)],
            endpoint,
            TimeSpan.FromMilliseconds(100));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        bool peerStopped = false;
        try
        {
            await driver.StartAsync(timeout.Token);

            TagValue initial = await cache.WaitForAsync(
                tag.Id,
                value => value.Quality == TagQuality.Good,
                timeout.Token);
            Assert.Equal(37, Assert.IsType<int>(initial.Value));

            await RunDockerAsync(timeout.Token, "stop", "-t", "0", containerName);
            peerStopped = true;

            TagValue failed = await cache.WaitForAsync(
                tag.Id,
                value => value.Quality == TagQuality.BadCommunication,
                timeout.Token);
            Assert.Equal(37, Assert.IsType<int>(failed.Value));
            Assert.Equal(CommunicationDriverOperationalState.Reconnecting, driver.GetCommunicationDiagnostics().State);

            await RunDockerAsync(timeout.Token, "start", containerName);
            peerStopped = false;

            TagValue recovered = await cache.WaitForAsync(
                tag.Id,
                value => value.Quality == TagQuality.Good,
                timeout.Token);
            Assert.Equal(37, Assert.IsType<int>(recovered.Value));

            CommunicationDriverDiagnosticSnapshot diagnostics = driver.GetCommunicationDiagnostics();
            Assert.Equal(CommunicationDriverOperationalState.Healthy, diagnostics.State);
            Assert.True(diagnostics.Counters.Connections >= 2);
            Assert.True(diagnostics.Counters.Disconnections >= 1);
            Assert.True(diagnostics.Counters.Reconnects >= 1);
            Assert.True(diagnostics.Counters.Cycles >= 2);
            Assert.True(provider.CertificateResolveCount >= 2);
            Assert.Equal(0, provider.SecretResolveCount);
        }
        finally
        {
            if (peerStopped)
            {
                using var recoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await RunDockerAsync(recoveryTimeout.Token, "start", containerName);
            }
        }
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

    private static async Task RunDockerAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Failed to start Docker CLI for OPC UA recovery L2.");

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Docker command failed with exit code {process.ExitCode}. stdout='{stdout.Trim()}' stderr='{stderr.Trim()}'.");
        }
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

    private sealed class RecordingCache : ICurrentTagCache
    {
        private readonly ConcurrentDictionary<Guid, TagValue> _values = new();
        private readonly Channel<TagValue> _updates = Channel.CreateUnbounded<TagValue>();

        public bool TryGet(Guid tagId, out TagValue? value)
        {
            bool found = _values.TryGetValue(tagId, out TagValue? current);
            value = current;
            return found;
        }

        public IReadOnlyCollection<TagValue> Snapshot() => _values.Values.ToArray();

        public ValueTask<TagValue?> UpdateAsync(
            TagDefinition tag,
            TagValue value,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values.TryGetValue(tag.Id, out TagValue? previous);
            _values[tag.Id] = value;
            _updates.Writer.TryWrite(value);
            return ValueTask.FromResult(previous);
        }

        public async Task<TagValue> WaitForAsync(
            Guid tagId,
            Func<TagValue, bool> predicate,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                TagValue value = await _updates.Reader.ReadAsync(cancellationToken);
                if (value.TagId == tagId && predicate(value))
                    return value;
            }
        }
    }

    private sealed class FakeRegistry : ITagRegistry
    {
        private readonly Dictionary<Guid, TagDefinition> _tags = [];

        public TagDefinition Register(TagDefinition tag)
        {
            _tags[tag.Id] = tag;
            return tag;
        }

        public TagDefinition Upsert(TagDefinition tag)
        {
            _tags[tag.Id] = tag;
            return tag;
        }

        public bool TryGet(Guid tagId, out TagDefinition? tag) => _tags.TryGetValue(tagId, out tag);

        public bool TryGetByPath(string path, out TagDefinition? tag)
        {
            tag = _tags.Values.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            return tag is not null;
        }

        public IReadOnlyCollection<TagDefinition> Snapshot() => _tags.Values.ToArray();
    }
}
