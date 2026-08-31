using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaOpen62541SecureRecoveryEnduranceTests
{
    private const string Basic256Sha256 = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256";
    private const string NamespaceUri = "urn:elitescada:interop:opcua";
    private const string ClientCertificateReference = "cert://opcua/l2-client";
    private const int RestartCycles = 3;

    [Fact]
    public async Task CommunicationDriver_RepeatedPeerRestarts_ReconnectsAndResubscribesEveryCycle()
    {
        if (!TryGetEnvironment(out string endpoint, out string clientPfx, out string serverPin, out string containerName))
            return;

        TagDefinition tag = CreateRecoveryTag();
        var provider = new FileCertificateProvider(clientPfx);
        var factory = new OpcUaFoundationRuntimeSessionFactory(
            new OpcUaRuntimeConnectionOptions(
                EndpointUrl: endpoint,
                SecurityMode: "SignAndEncrypt",
                SecurityPolicyUri: Basic256Sha256,
                AuthenticationMode: OpcUaRuntimeAuthenticationMode.Anonymous,
                ClientCertificateReference: ClientCertificateReference,
                ApprovedServerApplicationUri: "urn:elitescada:interop:opcua:server",
                ApprovedServerCertificateSha256: serverPin,
                SessionTimeout: TimeSpan.FromSeconds(20),
                PublishingInterval: TimeSpan.FromMilliseconds(100)),
            provider);
        var cache = new RecordingCache();
        var registry = new FakeRegistry();
        await using var driver = new OpcUaCommunicationDriver(
            "opcua-secure-endurance",
            "OPC UA Secure Recovery Endurance L2",
            cache,
            registry,
            [tag],
            factory,
            [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500)],
            endpoint,
            TimeSpan.FromMilliseconds(100));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        bool peerStopped = false;
        try
        {
            await driver.StartAsync(timeout.Token);
            TagValue initial = await cache.WaitForAsync(
                tag.Id,
                value => value.Quality == TagQuality.Good,
                timeout.Token);
            Assert.Equal(37, Assert.IsType<int>(initial.Value));

            for (int cycle = 1; cycle <= RestartCycles; cycle++)
            {
                await RunDockerAsync(timeout.Token, "stop", "-t", "0", containerName);
                peerStopped = true;

                TagValue failed = await cache.WaitForAsync(
                    tag.Id,
                    value => value.Quality == TagQuality.BadCommunication,
                    timeout.Token);
                Assert.Equal(37, Assert.IsType<int>(failed.Value));
                Assert.Equal(
                    CommunicationDriverOperationalState.Reconnecting,
                    driver.GetCommunicationDiagnostics().State);

                await RunDockerAsync(timeout.Token, "start", containerName);
                peerStopped = false;

                TagValue recovered = await cache.WaitForAsync(
                    tag.Id,
                    value => value.Quality == TagQuality.Good,
                    timeout.Token);
                Assert.Equal(37, Assert.IsType<int>(recovered.Value));
                Assert.Equal(
                    CommunicationDriverOperationalState.Healthy,
                    driver.GetCommunicationDiagnostics().State);
            }

            CommunicationDriverDiagnosticSnapshot diagnostics = driver.GetCommunicationDiagnostics();
            Assert.True(diagnostics.Counters.Connections >= RestartCycles + 1);
            Assert.True(diagnostics.Counters.Disconnections >= RestartCycles);
            Assert.True(diagnostics.Counters.Reconnects >= RestartCycles);
            Assert.True(diagnostics.Counters.Cycles >= RestartCycles + 1);
            Assert.True(provider.CertificateResolveCount >= RestartCycles + 1);
            Assert.Equal(0, provider.SecretResolveCount);
            Assert.Equal(DriverState.Running, driver.Status.State);
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

    private static TagDefinition CreateRecoveryTag() =>
        TagDefinition.Create(
            name: "RecoveryEnduranceCounter",
            path: "Interop.OPCUA.RecoveryEnduranceCounter",
            dataType: TagDataType.Int32,
            metadata: new Dictionary<string, string>
            {
                [OpcUaRuntimeBinding.NodeIdMetadataKey] = "ns=2;s=Lab.RecoveryCounter",
                [OpcUaRuntimeBinding.NamespaceUriMetadataKey] = NamespaceUri,
                [OpcUaRuntimeBinding.SamplingIntervalMetadataKey] = "00:00:00.100",
                [OpcUaRuntimeBinding.QueueSizeMetadataKey] = "10",
                [OpcUaRuntimeBinding.DiscardOldestMetadataKey] = "true"
            });

    private static bool TryGetEnvironment(
        out string endpoint,
        out string clientPfx,
        out string serverPin,
        out string containerName)
    {
        endpoint = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_SECURE_L2_ENDPOINT") ?? string.Empty;
        clientPfx = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_SECURE_L2_CLIENT_PFX") ?? string.Empty;
        serverPin = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_SECURE_L2_SERVER_SHA256") ?? string.Empty;
        containerName = Environment.GetEnvironmentVariable("ELITESCADA_OPCUA_SECURE_L2_CONTAINER") ?? string.Empty;
        return !string.IsNullOrWhiteSpace(endpoint) &&
               !string.IsNullOrWhiteSpace(clientPfx) &&
               !string.IsNullOrWhiteSpace(serverPin) &&
               !string.IsNullOrWhiteSpace(containerName);
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
            throw new InvalidOperationException("Failed to start Docker CLI for OPC UA endurance L2.");

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

    private sealed class FileCertificateProvider(string pfxPath) : IOpcUaRuntimeSecurityMaterialProvider
    {
        public int CertificateResolveCount { get; private set; }
        public int SecretResolveCount { get; private set; }

        public ValueTask<string> ResolveSecretAsync(
            string secretReference,
            CancellationToken cancellationToken = default)
        {
            SecretResolveCount++;
            return ValueTask.FromException<string>(new InvalidOperationException(
                "Secure anonymous endurance L2 must not resolve password material."));
        }

        public ValueTask<X509Certificate2> ResolveCertificateAsync(
            string certificateReference,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(certificateReference, ClientCertificateReference, StringComparison.Ordinal))
            {
                return ValueTask.FromException<X509Certificate2>(new InvalidOperationException(
                    "The secure endurance runtime requested an unexpected certificate reference."));
            }

            CertificateResolveCount++;
            return ValueTask.FromResult(
                X509CertificateLoader.LoadPkcs12FromFile(pfxPath, string.Empty));
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
