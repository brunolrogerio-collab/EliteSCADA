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
    public async Task EmptyInitialPolicyBootstrapsItsFirstFenceInsteadOfAbortingIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "elitescada-authority-ha-empty-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var haA = CreateReadyService(CreateTopology("node-a"), now);
            var haB = CreateReadyService(CreateTopology("node-b"), now);
            var referenceOptions = new RuntimeHaProtectionOptions(
                Enabled: true,
                AutomaticFailoverEnabled: true,
                ReferencePath: Path.Combine(directory, "reference.json"),
                LeaseDuration: TimeSpan.FromMinutes(2),
                PollInterval: TimeSpan.FromMilliseconds(500),
                ReadyWitnessMaximumAge: TimeSpan.FromMinutes(1),
                ClockSkewSafetyMargin: TimeSpan.FromSeconds(1));
            var referenceA = new FileRuntimeHaReferenceAuthorityStore(referenceOptions, haA);
            var referenceB = new FileRuntimeHaReferenceAuthorityStore(referenceOptions, haB);
            Assert.True((await referenceA.InitializeAsync("node-a")).Accepted);

            var policyA = new InMemoryAuthorityPolicyStore();
            var policyB = new InMemoryAuthorityPolicyStore();
            var optionsA = PeerOptions("node-a", "node-b");
            var optionsB = PeerOptions("node-b", "node-a");
            var gateA = new AssetProcessIntelligenceExecutionGate();
            using var coordinatorB = new AuthorityPolicyHaCoordinator(
                policyB, new AuthorityPolicyReplicationStoreAdapter(policyB), haB, referenceB,
                optionsB, new RuntimeHaPeerAuthenticator(optionsB, TimeProvider.System),
                new AssetProcessIntelligenceExecutionGate(), TimeProvider.System,
                NullLogger<AuthorityPolicyHaCoordinator>.Instance);
            using var peer = new InProcessPeerHandler(coordinatorB);
            using var coordinatorA = new AuthorityPolicyHaCoordinator(
                policyA, new AuthorityPolicyReplicationStoreAdapter(policyA), haA, referenceA,
                optionsA, new RuntimeHaPeerAuthenticator(optionsA, TimeProvider.System),
                gateA, TimeProvider.System, NullLogger<AuthorityPolicyHaCoordinator>.Instance, peer);

            await coordinatorA.SynchronizeOnceAsync();

            var readiness = await coordinatorA.CheckAsync();
            Assert.True(readiness.Allowed, readiness.ReasonCode);
            Assert.Equal(0, readiness.PolicyVersion);
            var fence = (await referenceA.ReadAsync())!.AuthorityPolicy!;
            Assert.Equal(0, fence.CommittedVersion);
            Assert.NotNull(fence.CommittedDigest);
            Assert.Null(fence.PendingVersion);
            Assert.Equal(0, (await policyB.RefreshAsync()).Version);
            Assert.Equal(
                AuthorityPolicySnapshotDigest.Compute(await policyA.RefreshAsync()),
                AuthorityPolicySnapshotDigest.Compute(await policyB.RefreshAsync()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PolicyFenceCommitPersistenceFailureStaysPendingAndReportsUnconfirmed(bool invalidReferenceData)
    {
        var directory = Path.Combine(Path.GetTempPath(), "elitescada-authority-ha-commit-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var referencePath = Path.Combine(directory, "reference.json");
        try
        {
            var now = DateTimeOffset.UtcNow;
            var haA = CreateReadyService(CreateTopology("node-a"), now);
            var haB = CreateReadyService(CreateTopology("node-b"), now);
            var referenceOptions = new RuntimeHaProtectionOptions(
                Enabled: true,
                AutomaticFailoverEnabled: true,
                ReferencePath: Path.GetFullPath(referencePath),
                LeaseDuration: TimeSpan.FromMinutes(2),
                PollInterval: TimeSpan.FromMilliseconds(500),
                ReadyWitnessMaximumAge: TimeSpan.FromMinutes(1),
                ClockSkewSafetyMargin: TimeSpan.FromSeconds(1));
            var durableReferenceA = new FileRuntimeHaReferenceAuthorityStore(referenceOptions, haA);
            var referenceA = new CommitFailingReferenceStore(durableReferenceA);
            var referenceB = new FileRuntimeHaReferenceAuthorityStore(referenceOptions, haB);
            Assert.True((await durableReferenceA.InitializeAsync("node-a")).Accepted);

            var role = new SecurityRoleEngineeringDto(
                Guid.Parse("b3000000-0000-0000-0000-000000000011"),
                "operator",
                "Operator",
                Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.TrendUse)]);
            var policyA = new InMemoryAuthorityPolicyStore([role], []);
            var policyB = new InMemoryAuthorityPolicyStore([role], []);
            var gateA = new AssetProcessIntelligenceExecutionGate();
            var gateB = new AssetProcessIntelligenceExecutionGate();
            var optionsA = PeerOptions("node-a", "node-b");
            var optionsB = PeerOptions("node-b", "node-a");
            var authenticatorA = new RuntimeHaPeerAuthenticator(optionsA, TimeProvider.System);
            var authenticatorB = new RuntimeHaPeerAuthenticator(optionsB, TimeProvider.System);

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
                NullLogger<AuthorityPolicyHaCoordinator>.Instance,
                peerHandler);

            await coordinatorA.SynchronizeOnceAsync();
            var ready = await coordinatorA.CheckAsync();
            Assert.True(ready.Allowed, ready.ReasonCode);
            var scopeId = Guid.Parse("b3000000-0000-0000-0000-000000000012");
            gateA.Activate("project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId, ready.PolicyVersion, ready.HaEpoch);
            Assert.True(gateA.TryGetToken(
                "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                out var permit, ready.PolicyVersion, ready.HaEpoch));

            referenceA.CommitFailure = invalidReferenceData
                ? new InvalidDataException("Simulated invalid durable HA reference after peer acknowledgement.")
                : new IOException("Simulated durable reference commit failure after peer acknowledgement.");
            var revokedRole = role with { Grants = Array.Empty<CapabilityGrantEngineeringDto>() };
            var mutation = await coordinatorA.ApplyMutationAsync(0, [revokedRole], []);

            Assert.False(mutation.Applied);
            Assert.Equal("AUTHORITY_POLICY_HA_COMMIT_UNCONFIRMED", mutation.Error);
            Assert.True(permit.IsCancellationRequested);
            Assert.Equal(1, (await policyB.RefreshAsync()).Version);
            var pending = await durableReferenceA.ReadAsync();
            Assert.Equal(0, pending!.AuthorityPolicy!.CommittedVersion);
            Assert.Equal(1, pending.AuthorityPolicy.PendingVersion);
            Assert.Equal(1, pending.AuthorityPolicy.AcknowledgedVersion);
            Assert.False((await coordinatorA.CheckAsync()).Allowed);

            referenceA.CommitFailure = null;
            await coordinatorA.SynchronizeOnceAsync();
            var repaired = await coordinatorA.CheckAsync();
            Assert.True(repaired.Allowed, repaired.ReasonCode);
            Assert.Equal(1, repaired.PolicyVersion);
            Assert.False(gateA.TryGetToken(
                "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                out _, repaired.PolicyVersion, repaired.HaEpoch));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("malformed-ack")]
    [InlineData("ack-body-io")]
    [InlineData("ack-lost-after-peer-apply")]
    [InlineData("caller-cancel")]
    public async Task PolicyRevocationFailsClosedDuringPeerFailureAndRequiresExplicitReactivationAfterRepair(string failureMode)
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

            var revokedRole = originalRole with { Grants = Array.Empty<CapabilityGrantEngineeringDto>() };
            AuthorityPolicyWriteResult? mutation = null;
            if (failureMode == "timeout")
            {
                peerHandler.Available = false;
                peerHandler.ThrowOnTimeout = true;
            }
            else if (failureMode == "malformed-ack")
            {
                peerHandler.MalformedAcknowledgement = true;
            }
            else if (failureMode == "ack-body-io")
            {
                peerHandler.FailAcknowledgementBodyRead = true;
            }
            else if (failureMode == "ack-lost-after-peer-apply")
            {
                peerHandler.LoseAcknowledgementAfterPeerApply = true;
            }
            else
            {
                peerHandler.WaitForCallerCancellation = true;
            }

            if (failureMode == "caller-cancel")
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                    await coordinatorA.ApplyMutationAsync(0, [revokedRole], [], cancellation.Token));
            }
            else
            {
                mutation = await coordinatorA.ApplyMutationAsync(0, [revokedRole], []);
                Assert.False(mutation.Applied);
                Assert.Equal("AUTHORITY_POLICY_HA_REPLICATION_PENDING", mutation.Error);
            }

            Assert.True(oldPermit.IsCancellationRequested);
            Assert.False(gateA.TryGetToken(
                "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                out _, ready.PolicyVersion, ready.HaEpoch));
            Assert.True(haA.CanOwnIndustrialEffects());
            Assert.False((await coordinatorA.CheckAsync()).Allowed);
            var pendingReference = await referenceA.ReadAsync();
            Assert.Equal(1, pendingReference!.AuthorityPolicy!.PendingVersion);
            if (failureMode is "malformed-ack" or "ack-body-io" or "ack-lost-after-peer-apply")
            {
                Assert.Equal(1, pendingReference.AuthorityPolicy.AcknowledgedVersion);
                Assert.Equal(1, (await policyB.RefreshAsync()).Version);
            }
            else
            {
                Assert.Null(pendingReference.AuthorityPolicy.AcknowledgedVersion);
                Assert.Equal(0, (await policyB.RefreshAsync()).Version);
            }

            peerHandler.ThrowOnTimeout = false;
            peerHandler.Available = true;
            peerHandler.MalformedAcknowledgement = false;
            peerHandler.FailAcknowledgementBodyRead = false;
            peerHandler.LoseAcknowledgementAfterPeerApply = false;
            peerHandler.WaitForCallerCancellation = false;
            AuthorityPolicyHaReadiness repaired;
            var repairedGate = gateA;
            if (failureMode == "ack-lost-after-peer-apply")
            {
                // The peer's database and shared fence contain the durable ACK,
                // but the sender never received it. Recreate the sender coordinator
                // and its volatile permit gate before it resumes reconciliation.
                var restartedGate = new AssetProcessIntelligenceExecutionGate();
                repairedGate = restartedGate;
                using var restartedCoordinator = new AuthorityPolicyHaCoordinator(
                    policyA,
                    new AuthorityPolicyReplicationStoreAdapter(policyA),
                    haA,
                    referenceA,
                    optionsA,
                    authenticatorA,
                    restartedGate,
                    TimeProvider.System,
                    NullLogger<AuthorityPolicyHaCoordinator>.Instance,
                    peerHandler);
                await restartedCoordinator.SynchronizeOnceAsync();
                repaired = await restartedCoordinator.CheckAsync();
            }
            else
            {
                await coordinatorA.SynchronizeOnceAsync();
                repaired = await coordinatorA.CheckAsync();
            }

            Assert.True(repaired.Allowed, repaired.ReasonCode);
            Assert.Equal(1, repaired.PolicyVersion);
            Assert.Equal(1, (await policyB.RefreshAsync()).Version);
            Assert.Equal(
                AuthorityPolicySnapshotDigest.Compute(await policyA.RefreshAsync()),
                AuthorityPolicySnapshotDigest.Compute(await policyB.RefreshAsync()));

            // Repair commits authority, but no local process permit is restored implicitly.
            Assert.False(repairedGate.TryGetToken(
                "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                out _, repaired.PolicyVersion, repaired.HaEpoch));
            repairedGate.Activate("project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                repaired.PolicyVersion, repaired.HaEpoch);
            Assert.True(repairedGate.TryGetToken(
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

    private sealed class CommitFailingReferenceStore(IRuntimeHaReferenceAuthorityStore inner) : IRuntimeHaReferenceAuthorityStore
    {
        public Exception? CommitFailure { get; set; }

        public Task<RuntimeHaReferenceAuthority?> ReadAsync(CancellationToken cancellationToken = default) =>
            inner.ReadAsync(cancellationToken);

        public Task<RuntimeHaReferenceMutationResult> InitializeAsync(string initialActiveNodeId, CancellationToken cancellationToken = default) =>
            inner.InitializeAsync(initialActiveNodeId, cancellationToken);

        public Task<RuntimeHaReferenceMutationResult> RenewAsync(string nodeId, Guid leaseId, long expectedEpoch, CancellationToken cancellationToken = default) =>
            inner.RenewAsync(nodeId, leaseId, expectedEpoch, cancellationToken);

        public Task<RuntimeHaReferenceMutationResult> ClaimExpiredAsync(string nodeId, long expectedEpoch, CancellationToken cancellationToken = default) =>
            inner.ClaimExpiredAsync(nodeId, expectedEpoch, cancellationToken);

        public Task<RuntimeHaReferenceMutationResult> FenceAsync(string nodeId, Guid leaseId, long expectedEpoch, string reasonCode, CancellationToken cancellationToken = default) =>
            inner.FenceAsync(nodeId, leaseId, expectedEpoch, reasonCode, cancellationToken);

        public Task<RuntimeHaReferenceMutationResult> AssignAfterFenceAsync(string targetNodeId, long expectedBreakEpoch, string reasonCode, CancellationToken cancellationToken = default) =>
            inner.AssignAfterFenceAsync(targetNodeId, expectedBreakEpoch, reasonCode, cancellationToken);

        public Task<RuntimeHaReferenceMutationResult> BeginAuthorityPolicyUpdateAsync(string nodeId, Guid leaseId, long expectedEpoch, long expectedCommittedVersion, string? expectedCommittedDigest, long pendingVersion, string pendingDigest, CancellationToken cancellationToken = default) =>
            inner.BeginAuthorityPolicyUpdateAsync(nodeId, leaseId, expectedEpoch, expectedCommittedVersion, expectedCommittedDigest, pendingVersion, pendingDigest, cancellationToken);

        public Task<RuntimeHaReferenceMutationResult> AcknowledgeAuthorityPolicyUpdateAsync(string acknowledgingNodeId, long expectedEpoch, long pendingVersion, string pendingDigest, CancellationToken cancellationToken = default) =>
            inner.AcknowledgeAuthorityPolicyUpdateAsync(acknowledgingNodeId, expectedEpoch, pendingVersion, pendingDigest, cancellationToken);

        public Task<RuntimeHaReferenceMutationResult> CommitAuthorityPolicyUpdateAsync(string nodeId, Guid leaseId, long expectedEpoch, long pendingVersion, string pendingDigest, CancellationToken cancellationToken = default)
        {
            if (CommitFailure is { } failure) throw failure;
            return inner.CommitAuthorityPolicyUpdateAsync(nodeId, leaseId, expectedEpoch, pendingVersion, pendingDigest, cancellationToken);
        }

        public Task<RuntimeHaReferenceMutationResult> AbortAuthorityPolicyUpdateAsync(string nodeId, Guid leaseId, long expectedEpoch, long pendingVersion, string pendingDigest, CancellationToken cancellationToken = default) =>
            inner.AbortAuthorityPolicyUpdateAsync(nodeId, leaseId, expectedEpoch, pendingVersion, pendingDigest, cancellationToken);
    }

    private sealed class InProcessPeerHandler(AuthorityPolicyHaCoordinator peer) : HttpMessageHandler
    {
        public bool Available { get; set; } = true;
        public bool ThrowOnTimeout { get; set; }
        public bool MalformedAcknowledgement { get; set; }
        public bool FailAcknowledgementBodyRead { get; set; }
        public bool LoseAcknowledgementAfterPeerApply { get; set; }
        public bool WaitForCallerCancellation { get; set; }
        public HttpStatusCode? LastStatus { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (ThrowOnTimeout)
                throw new TaskCanceledException("Simulated peer request timeout.");
            if (WaitForCallerCancellation)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

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

            if (LoseAcknowledgementAfterPeerApply)
            {
                LastStatus = HttpStatusCode.ServiceUnavailable;
                LastBody = "<acknowledgement lost after peer durable apply>";
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            if (MalformedAcknowledgement)
            {
                LastStatus = HttpStatusCode.OK;
                LastBody = "{malformed acknowledgement";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(LastBody)
                };
            }

            if (FailAcknowledgementBodyRead)
            {
                LastStatus = HttpStatusCode.OK;
                LastBody = "<acknowledgement body read failed>";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new FailingReadHttpContent()
                };
            }

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

    private sealed class FailingReadHttpContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            Task.FromException(new IOException("Simulated peer response body read failure."));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
