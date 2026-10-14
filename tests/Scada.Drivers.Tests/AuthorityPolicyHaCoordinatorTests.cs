using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class AuthorityPolicyHaCoordinatorTests
{
    [Fact]
    public async Task PolicyRevocationFailsClosedDuringPeerLossAndRequiresExplicitReactivationAfterRepair()
    {
        var directory = Path.Combine(Path.GetTempPath(), "elitescada-authority-ha-coordinator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var referencePath = Path.Combine(directory, "reference.json");
        try
        {
            var now = DateTimeOffset.UtcNow;
            var topologyA = CreateTopology("node-a");
            var topologyB = CreateTopology("node-b");
            var haA = CreateReadyService(topologyA, now);
            var haB = CreateReadyService(topologyB, now);
            Assert.True(haA.CanOwnIndustrialEffects());
            Assert.False(haB.CanOwnIndustrialEffects());

            var referenceOptions = new RuntimeHaProtectionOptions(
                Enabled: true,
                AutomaticFailoverEnabled: true,
                ReferencePath: Path.GetFullPath(referencePath),
                LeaseDuration: TimeSpan.FromMinutes(2),
                PollInterval: TimeSpan.FromMilliseconds(500),
                ReadyWitnessMaximumAge: TimeSpan.FromMinutes(1),
                ClockSkewSafetyMargin: TimeSpan.FromSeconds(1));
            var referenceA = new FileRuntimeHaReferenceAuthorityStore(referenceOptions, haA);
            var referenceB = new FileRuntimeHaReferenceAuthorityStore(referenceOptions, haB);
            var initialized = await referenceA.InitializeAsync("node-a");
            Assert.True(initialized.Accepted);
            Assert.Equal(1, haA.Snapshot().AuthorityEpoch);

            var roleId = Guid.Parse("b3000000-0000-0000-0000-000000000001");
            var originalRole = new SecurityRoleEngineeringDto(
                roleId,
                "operator",
                "Operator",
                Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.TrendUse)]);
            var policyA = new InMemoryAuthorityPolicyStore([originalRole], []);
            var policyB = new InMemoryAuthorityPolicyStore([originalRole], []);
            var gateA = new AssetProcessIntelligenceExecutionGate();
            var gateB = new AssetProcessIntelligenceExecutionGate();
            var optionsA = PeerOptions("node-a", "node-b");
            var optionsB = PeerOptions("node-b", "node-a");
            Assert.True(optionsA.AuthenticationReady);
            var authenticatorA = new RuntimeHaPeerAuthenticator(optionsA, TimeProvider.System);
            var authenticatorB = new RuntimeHaPeerAuthenticator(optionsB, TimeProvider.System);
            var coordinatorLogger = new CapturingLogger<AuthorityPolicyHaCoordinator>();

            using var coordinatorB = new AuthorityPolicyHaCoordinator(
                policyB,
                new AuthorityPolicyReplicationStoreAdapter(policyB),
                haB,
                referenceB,
                optionsB,
                authenticatorB,
                gateB,
                TimeProvider.System,
                NullLogger<AuthorityPolicyHaCoordinator>.Instance);
            using var peerHandler = new InProcessPeerHandler(coordinatorB);
            using var coordinatorA = new AuthorityPolicyHaCoordinator(
                policyA,
                new AuthorityPolicyReplicationStoreAdapter(policyA),
                haA,
                referenceA,
                optionsA,
                authenticatorA,
                gateA,
                TimeProvider.System,
                coordinatorLogger,
                peerHandler);

            await coordinatorA.SynchronizeOnceAsync();
            var initialFence = await referenceA.ReadAsync();
            Assert.NotNull(initialFence?.AuthorityPolicy);
            var localPolicyAfterSync = await policyA.RefreshAsync();
            Assert.True(initialFence.AuthorityPolicy!.CommittedDigest is not null,
                $"baseline failed; active={initialFence.ActiveNodeId}, epoch={initialFence.Epoch}, local={haA.LocalNodeId}, activeEpoch={haA.Snapshot().AuthorityEpoch}, pending={initialFence.AuthorityPolicy.PendingVersion}, pendingEpoch={initialFence.AuthorityPolicy.PendingEpoch}, pendingNode={initialFence.AuthorityPolicy.PendingNodeId}, digestMatch={AuthorityPolicySnapshotDigest.Compute(localPolicyAfterSync) == initialFence.AuthorityPolicy.PendingDigest}, peerReady={optionsA.AuthenticationReady}, peer status={peerHandler.LastStatus}; response={peerHandler.LastBody}; logs={string.Join(" | ", coordinatorLogger.Messages)}");
            var ready = await coordinatorA.CheckAsync();
            Assert.True(ready.Allowed, ready.ReasonCode);
            Assert.Equal(0, ready.PolicyVersion);

            var scopeId = Guid.Parse("b3000000-0000-0000-0000-000000000002");
            gateA.Activate("project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId, ready.PolicyVersion, ready.HaEpoch);
            Assert.True(gateA.TryGetToken(
                "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                out var oldPermit, ready.PolicyVersion, ready.HaEpoch));

            peerHandler.Available = false;
            var revokedRole = originalRole with { Grants = Array.Empty<CapabilityGrantEngineeringDto>() };
            var mutation = await coordinatorA.ApplyMutationAsync(0, [revokedRole], []);

            Assert.False(mutation.Applied);
            Assert.Equal("AUTHORITY_POLICY_HA_REPLICATION_PENDING", mutation.Error);
            Assert.True(oldPermit.IsCancellationRequested);
            Assert.False(gateA.TryGetToken(
                "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                out _, ready.PolicyVersion, ready.HaEpoch));
            Assert.True(haA.CanOwnIndustrialEffects());
            Assert.False((await coordinatorA.CheckAsync()).Allowed);
            var pendingReference = await referenceA.ReadAsync();
            Assert.Equal(1, pendingReference!.AuthorityPolicy!.PendingVersion);
            Assert.Null(pendingReference.AuthorityPolicy.AcknowledgedVersion);
            Assert.Equal(0, (await policyB.RefreshAsync()).Version);

            peerHandler.Available = true;
            await coordinatorA.SynchronizeOnceAsync();

            var repaired = await coordinatorA.CheckAsync();
            Assert.True(repaired.Allowed, repaired.ReasonCode);
            Assert.Equal(1, repaired.PolicyVersion);
            Assert.Equal(1, (await policyB.RefreshAsync()).Version);
            Assert.Equal(
                AuthorityPolicySnapshotDigest.Compute(await policyA.RefreshAsync()),
                AuthorityPolicySnapshotDigest.Compute(await policyB.RefreshAsync()));

            // Repair commits authority, but no local process permit is restored implicitly.
            Assert.False(gateA.TryGetToken(
                "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                out _, repaired.PolicyVersion, repaired.HaEpoch));
            gateA.Activate("project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                repaired.PolicyVersion, repaired.HaEpoch);
            Assert.True(gateA.TryGetToken(
                "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                out _, repaired.PolicyVersion, repaired.HaEpoch));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static RuntimeHighAvailabilityService CreateReadyService(
        RuntimeHaTopologyDefinition topology,
        DateTimeOffset now)
    {
        var service = new RuntimeHighAvailabilityService(topology, () => now);
        foreach (var node in topology.Nodes)
        {
            service.Authority.UpdateNodeReadiness(
                node.NodeId,
                new RuntimeHaNodeReadinessEvidence(
                    Healthy: true,
                    SynchronizationComplete: true,
                    HaLicenseEntitled: true,
                    Runtime: new RuntimeHaRuntimeIdentity("engineering", "project-a", 7),
                    ObservedAtUtc: now));
        }
        return service;
    }

    private static RuntimeHaTopologyDefinition CreateTopology(string localNodeId) => new(
        Enabled: true,
        ClusterId: "authority-policy-ha-test",
        LocalNodeId: localNodeId,
        InitialActiveNodeId: "node-a",
        TopologyVersion: 5,
        FreshnessWindow: TimeSpan.FromMinutes(1),
        Nodes:
        [
            new RuntimeHaNodeDefinition("node-a", [new RuntimeHaEndpoint(RuntimeHaEndpointKind.Local, "http://node-a.test", 0)]),
            new RuntimeHaNodeDefinition("node-b", [new RuntimeHaEndpoint(RuntimeHaEndpointKind.Local, "http://node-b.test", 0)])
        ]);

    private static RuntimeHaPeerTransportOptions PeerOptions(string localNodeId, string peerNodeId) => new(
        Enabled: true,
        LocalNodeId: localNodeId,
        PeerNodeId: peerNodeId,
        PeerEndpoint: new Uri($"http://{peerNodeId}.test"),
        SharedSecret: "unit-test-authority-policy-peer-secret-32-bytes-minimum",
        MaxPayloadBytes: 64 * 1024,
        RequestTimeout: TimeSpan.FromSeconds(3),
        PollInterval: TimeSpan.FromMilliseconds(500),
        MaximumRetryDelay: TimeSpan.FromSeconds(2),
        AuthenticationFreshness: TimeSpan.FromMinutes(1),
        PeerFreshnessWindow: TimeSpan.FromMinutes(1),
        StatePath: null);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(exception is null ? formatter(state, exception) : formatter(state, exception) + " " + exception);
    }

    private sealed class InProcessPeerHandler(AuthorityPolicyHaCoordinator peer) : HttpMessageHandler
    {
        public bool Available { get; set; } = true;
        public HttpStatusCode? LastStatus { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!Available)
            {
                LastStatus = HttpStatusCode.ServiceUnavailable;
                LastBody = null;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            var context = new DefaultHttpContext();
            using var services = new ServiceCollection()
                .AddLogging()
                .ConfigureHttpJsonOptions(_ => { })
                .BuildServiceProvider();
            context.RequestServices = services;
            context.Request.Method = request.Method.Method;
            context.Request.Path = request.RequestUri!.AbsolutePath;
            context.Request.Body = new MemoryStream(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            foreach (var header in request.Headers)
                context.Request.Headers[header.Key] = header.Value.ToArray();
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            var result = await peer.ReceivePeerSnapshotAsync(context.Request, cancellationToken);
            await result.ExecuteAsync(context);

            var response = new HttpResponseMessage((HttpStatusCode)context.Response.StatusCode)
            {
                Content = new ByteArrayContent(responseBody.ToArray())
            };
            if (!string.IsNullOrWhiteSpace(context.Response.ContentType))
                response.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Response.ContentType);
            LastStatus = response.StatusCode;
            LastBody = System.Text.Encoding.UTF8.GetString(responseBody.ToArray());
            return response;
        }
    }
}
