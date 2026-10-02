using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class RuntimeHighAvailabilityConfigurationTests
{
    [Fact]
    public async Task HostConfiguration_UpdatePersistsSanitizedAndBlocksUntilRestart()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var configuration = CreateConfiguration(root);
            var authority = new RuntimeHaHostConfigurationAuthority(configuration);
            var before = authority.Snapshot();

            Assert.False(before.PendingRestart);
            Assert.True(authority.AllowsIndustrialEffects);
            Assert.Equal(3, before.Running.TopologyVersion);
            Assert.True(before.Running.PeerTransport.AuthenticationConfigured);

            var result = await authority.UpdateAsync(
                CreateUpdate(
                    before.Generation,
                    topologyVersion: 4,
                    nodeBRemoteEndpoint: "https://b2.example.test",
                    referencePath: before.Desired.Protection.ReferencePath!));

            Assert.True(result.Accepted);
            Assert.Equal(
                "host-configuration-persisted-restart-required",
                result.ReasonCode);
            Assert.True(result.Snapshot.PendingRestart);
            Assert.True(result.Snapshot.IndustrialEffectsBlocked);
            Assert.False(authority.AllowsIndustrialEffects);
            Assert.Equal(4, result.Snapshot.Desired.TopologyVersion);
            Assert.Equal(
                RuntimeHaHostConfigurationAuthority.ReferenceStoreModeSharedExternalFile,
                result.Snapshot.ReferenceStoreRequirements.Mode);
            Assert.True(result.Snapshot.ReferenceStoreRequirements.FailClosedWhenUnavailable);

            var serialized = JsonSerializer.Serialize(result.Snapshot);
            Assert.DoesNotContain(SharedSecret, serialized, StringComparison.Ordinal);
            Assert.True(File.Exists(ConfigurationPath(root)));

            var restarted = new RuntimeHaHostConfigurationAuthority(configuration);
            var afterRestart = restarted.Snapshot();
            Assert.False(afterRestart.PendingRestart);
            Assert.False(afterRestart.IndustrialEffectsBlocked);
            Assert.True(restarted.AllowsIndustrialEffects);
            Assert.False(restarted.AllowsReferenceBootstrap);
            Assert.Equal(4, afterRestart.Running.TopologyVersion);
            Assert.Contains(
                afterRestart.Running.Nodes,
                node => node.NodeId == "node-b" &&
                    node.RemoteEndpoint == "https://b2.example.test");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task HostConfiguration_TopologyShapeChangeRequiresNewerVersion()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var authority = new RuntimeHaHostConfigurationAuthority(
                CreateConfiguration(root));
            var before = authority.Snapshot();

            var result = await authority.UpdateAsync(
                CreateUpdate(
                    before.Generation,
                    topologyVersion: before.Desired.TopologyVersion,
                    nodeBRemoteEndpoint: "https://b2.example.test",
                    referencePath: before.Desired.Protection.ReferencePath!));

            Assert.False(result.Accepted);
            Assert.Equal("topology-version-must-increase", result.ReasonCode);
            Assert.False(result.Snapshot.PendingRestart);
            Assert.True(authority.AllowsIndustrialEffects);
            Assert.False(File.Exists(ConfigurationPath(root)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task HostConfiguration_RejectsAmbiguousAdvertisedEndpoints()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var authority = new RuntimeHaHostConfigurationAuthority(
                CreateConfiguration(root));
            var before = authority.Snapshot();
            var update = CreateUpdate(
                before.Generation,
                topologyVersion: 4,
                nodeBRemoteEndpoint: "https://b2.example.test",
                referencePath: before.Desired.Protection.ReferencePath!);
            update = update with
            {
                Nodes = new[]
                {
                    new RuntimeHaHostNodeConfiguration(
                        "node-a",
                        "https://10.0.0.1:5001",
                        "https://a.example.test"),
                    new RuntimeHaHostNodeConfiguration(
                        "node-b",
                        "https://10.0.0.1:5001",
                        "https://b2.example.test")
                }
            };

            var result = await authority.UpdateAsync(update);

            Assert.False(result.Accepted);
            Assert.Equal("host-configuration-invalid", result.ReasonCode);
            Assert.Contains(
                result.Errors,
                error => error.Contains("must be unique", StringComparison.OrdinalIgnoreCase));
            Assert.False(result.Snapshot.PendingRestart);
            Assert.True(authority.AllowsIndustrialEffects);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task HostConfiguration_NeverReturnsOrClearsPeerSecretWhileTransportEnabled()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var authority = new RuntimeHaHostConfigurationAuthority(
                CreateConfiguration(root));
            var before = authority.Snapshot();
            var update = CreateUpdate(
                before.Generation,
                topologyVersion: before.Desired.TopologyVersion,
                nodeBRemoteEndpoint: "https://b.example.test",
                referencePath: before.Desired.Protection.ReferencePath!);
            update = update with
            {
                PeerTransport = update.PeerTransport! with
                {
                    ClearPeerSharedSecret = true
                }
            };

            var result = await authority.UpdateAsync(update);

            Assert.False(result.Accepted);
            Assert.Equal("host-configuration-invalid", result.ReasonCode);
            Assert.True(before.Running.PeerTransport.AuthenticationConfigured);
            Assert.DoesNotContain(
                SharedSecret,
                JsonSerializer.Serialize(before),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static RuntimeHaHostConfigurationUpdateRequest CreateUpdate(
        long generation,
        long topologyVersion,
        string nodeBRemoteEndpoint,
        string referencePath) =>
        new(
            generation,
            Enabled: true,
            ClusterId: "cluster-a",
            LocalNodeId: "node-a",
            InitialActiveNodeId: "node-a",
            TopologyVersion: topologyVersion,
            FreshnessSeconds: 15,
            Nodes: new[]
            {
                new RuntimeHaHostNodeConfiguration(
                    "node-a",
                    "https://10.0.0.1:5001",
                    "https://a.example.test"),
                new RuntimeHaHostNodeConfiguration(
                    "node-b",
                    "https://10.0.0.2:5001",
                    nodeBRemoteEndpoint)
            },
            PeerTransport: new RuntimeHaHostPeerTransportUpdate(
                Enabled: true,
                PeerEndpoint: null),
            Protection: new RuntimeHaHostProtectionConfiguration(
                Enabled: true,
                AutomaticFailoverEnabled: true,
                ReferenceStoreMode:
                    RuntimeHaHostConfigurationAuthority.ReferenceStoreModeSharedExternalFile,
                ReferencePath: referencePath,
                LeaseSeconds: 10,
                PollMilliseconds: 500,
                ReadyWitnessMaximumAgeSeconds: 60,
                ClockSkewSafetyMarginSeconds: 1));

    private static IConfiguration CreateConfiguration(string root)
    {
        var values = new Dictionary<string, string?>
        {
            ["HighAvailability:Enabled"] = "true",
            ["HighAvailability:ClusterId"] = "cluster-a",
            ["HighAvailability:NodeId"] = "node-a",
            ["HighAvailability:InitialActiveNodeId"] = "node-a",
            ["HighAvailability:TopologyVersion"] = "3",
            ["HighAvailability:FreshnessSeconds"] = "15",
            ["HighAvailability:Nodes:0:NodeId"] = "node-a",
            ["HighAvailability:Nodes:0:LocalEndpoint"] = "https://10.0.0.1:5001",
            ["HighAvailability:Nodes:0:RemoteEndpoint"] = "https://a.example.test",
            ["HighAvailability:Nodes:1:NodeId"] = "node-b",
            ["HighAvailability:Nodes:1:LocalEndpoint"] = "https://10.0.0.2:5001",
            ["HighAvailability:Nodes:1:RemoteEndpoint"] = "https://b.example.test",
            ["HighAvailability:PeerTransport:Enabled"] = "true",
            ["HighAvailability:PeerTransport:SharedSecret"] = SharedSecret,
            ["HighAvailability:Protection:Enabled"] = "true",
            ["HighAvailability:Protection:AutomaticFailoverEnabled"] = "true",
            ["HighAvailability:Protection:ReferencePath"] = ReferencePath(root),
            ["HighAvailability:Protection:LeaseSeconds"] = "10",
            ["HighAvailability:Protection:PollMilliseconds"] = "500",
            ["HighAvailability:Protection:ReadyWitnessMaximumAgeSeconds"] = "60",
            ["HighAvailability:Protection:ClockSkewSafetyMarginSeconds"] = "1",
            ["HighAvailability:Administration:ConfigurationPath"] =
                ConfigurationPath(root)
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static string ConfigurationPath(string root) =>
        Path.Combine(root, "host-configuration.json");

    private static string ReferencePath(string root) =>
        Path.Combine(root, "shared-reference.json");

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "elitescada-ha-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private const string SharedSecret =
        "0123456789abcdef0123456789abcdef0123456789abcdef";
}
