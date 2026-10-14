using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Persistence.PostgreSql;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class AuthorityPolicyPostgreSqlHaTests
{
    private const string Secret = "postgres-ha-authority-policy-test-secret-32-bytes";
    private static string? SeedConnectionString => Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RevocationAndPermitRecovery_AreFencedWithSharedAndSeparateNodeDatabases(bool sharedDatabase)
    {
        var seed = SeedConnectionString;
        if (string.IsNullOrWhiteSpace(seed)) return;

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var databaseA = "w15_authority_a_" + suffix;
        var databaseB = sharedDatabase ? databaseA : "w15_authority_b_" + suffix;
        var admin = new NpgsqlConnectionStringBuilder(seed) { Database = "postgres", Pooling = false };
        var connectionA = new NpgsqlConnectionStringBuilder(seed) { Database = databaseA, Pooling = false };
        var connectionB = new NpgsqlConnectionStringBuilder(seed) { Database = databaseB, Pooling = false };
        var created = new List<string>();
        var directory = Path.Combine(Path.GetTempPath(), "elitescada-authority-pg-ha-" + suffix);
        Directory.CreateDirectory(directory);

        try
        {
            await CreateDatabaseAsync(admin.ConnectionString, databaseA);
            created.Add(databaseA);
            if (!sharedDatabase)
            {
                await CreateDatabaseAsync(admin.ConnectionString, databaseB);
                created.Add(databaseB);
            }

            await ExercisePolicyLifecycleAsync(
                connectionA.ConnectionString,
                connectionB.ConnectionString,
                Path.Combine(directory, "ha-reference.json"),
                sharedDatabase);
        }
        finally
        {
            foreach (var database in created.AsEnumerable().Reverse())
                await DropDatabaseAsync(admin.ConnectionString, database);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task ExercisePolicyLifecycleAsync(
        string connectionA,
        string connectionB,
        string referencePath,
        bool sharedDatabase)
    {
        var now = DateTimeOffset.UtcNow;
        var topologyA = CreateTopology("node-a");
        var topologyB = CreateTopology("node-b");
        var haA = CreateReadyService(topologyA, now);
        var haB = CreateReadyService(topologyB, now);
        var protection = new RuntimeHaProtectionOptions(
            Enabled: true,
            AutomaticFailoverEnabled: true,
            ReferencePath: Path.GetFullPath(referencePath),
            LeaseDuration: TimeSpan.FromMinutes(2),
            PollInterval: TimeSpan.FromMilliseconds(500),
            ReadyWitnessMaximumAge: TimeSpan.FromMinutes(1),
            ClockSkewSafetyMargin: TimeSpan.FromSeconds(1));
        var referenceA = new FileRuntimeHaReferenceAuthorityStore(protection, haA);
        var referenceB = new FileRuntimeHaReferenceAuthorityStore(protection, haB);
        Assert.True((await referenceA.InitializeAsync("node-a")).Accepted);

        var role = new SecurityRoleEngineeringDto(
            Guid.Parse("b3100000-0000-0000-0000-000000000001"),
            "operator",
            "Operator",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.TrendUse)]);
        var initialScopes = Array.Empty<SecurityScopeEngineeringDto>();
        await using var storeA = new PostgreSqlAuthorityPolicyStore(connectionA);
        await using var storeB = new PostgreSqlAuthorityPolicyStore(connectionB);
        await storeA.InitializeAsync();
        await storeB.InitializeAsync();
        var initialized = await storeA.TryReplaceAsync(0, [role], initialScopes);
        Assert.True(initialized.Applied);
        Assert.Equal(1, initialized.Snapshot.Version);

        var gateA = new AssetProcessIntelligenceExecutionGate();
        var optionsA = PeerOptions("node-a", "node-b");
        var optionsB = PeerOptions("node-b", "node-a");
        var authenticatorA = new RuntimeHaPeerAuthenticator(optionsA, TimeProvider.System);
        var authenticatorB = new RuntimeHaPeerAuthenticator(optionsB, TimeProvider.System);
        var gateB = new AssetProcessIntelligenceExecutionGate();
        using var coordinatorB = new AuthorityPolicyHaCoordinator(
            storeB,
            new AuthorityPolicyReplicationStoreAdapter(storeB),
            haB,
            referenceB,
            optionsB,
            authenticatorB,
            gateB,
            TimeProvider.System,
            NullLogger<AuthorityPolicyHaCoordinator>.Instance);
        using var peer = new InProcessPeerHandler(coordinatorB);
        using var coordinatorA = new AuthorityPolicyHaCoordinator(
            storeA,
            new AuthorityPolicyReplicationStoreAdapter(storeA),
            haA,
            referenceA,
            optionsA,
            authenticatorA,
            gateA,
            TimeProvider.System,
            NullLogger<AuthorityPolicyHaCoordinator>.Instance,
            peer);

        await coordinatorA.SynchronizeOnceAsync();
        var baseline = await coordinatorA.CheckAsync();
        Assert.True(baseline.Allowed, baseline.ReasonCode);
        Assert.Equal(1, baseline.PolicyVersion);
        Assert.Equal(1, (await storeB.RefreshAsync()).Version);

        var scopeId = Guid.Parse("b3100000-0000-0000-0000-000000000002");
        gateA.Activate("project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
            baseline.PolicyVersion, baseline.HaEpoch);
        Assert.True(gateA.TryGetToken(
            "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
            out var permit, baseline.PolicyVersion, baseline.HaEpoch));

        peer.Available = false;
        var revoked = role with { Grants = Array.Empty<CapabilityGrantEngineeringDto>() };
        var pending = await coordinatorA.ApplyMutationAsync(1, [revoked], initialScopes);
        Assert.False(pending.Applied);
        Assert.Equal("AUTHORITY_POLICY_HA_REPLICATION_PENDING", pending.Error);
        Assert.True(permit.IsCancellationRequested);
        Assert.False(gateA.TryGetToken(
            "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
            out _, 1, baseline.HaEpoch));
        Assert.True(haA.CanOwnIndustrialEffects());
        Assert.Equal("authority-policy-update-pending", (await coordinatorA.CheckAsync()).ReasonCode);
        Assert.Equal(2, (await storeA.RefreshAsync()).Version);
        Assert.Equal(sharedDatabase ? 2 : 1, (await storeB.RefreshAsync()).Version);

        peer.Available = true;
        await coordinatorA.SynchronizeOnceAsync();
        var repaired = await coordinatorA.CheckAsync();
        Assert.True(repaired.Allowed, repaired.ReasonCode);
        Assert.Equal(2, repaired.PolicyVersion);
        Assert.Equal(2, (await storeB.RefreshAsync()).Version);
        Assert.False(gateA.TryGetToken(
            "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
            out _, repaired.PolicyVersion, repaired.HaEpoch));

        var restored = await coordinatorA.ApplyMutationAsync(2, [role], initialScopes);
        Assert.True(restored.Applied, restored.Error);
        Assert.Equal(3, restored.Snapshot.Version);
        Assert.Equal(3, (await storeB.RefreshAsync()).Version);
        Assert.False(gateA.TryGetToken(
            "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
            out _, 3, repaired.HaEpoch));
        gateA.Activate("project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
            3, repaired.HaEpoch);
        Assert.True(gateA.TryGetToken(
            "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
            out _, 3, repaired.HaEpoch));

        var peerObservation = haA.CreatePeerObservation();
        Assert.True(haB.ApplyPeerObservation(peerObservation).Accepted);
        var standbyWitness = haB.Authority.CaptureStandbyPromotionWitness("node-b");
        Assert.NotNull(standbyWitness);
        var beforeFailover = Assert.IsType<RuntimeHaReferenceAuthority>(await referenceA.ReadAsync());
        var brokenAuthority = await referenceA.FenceAsync(
            "node-a", beforeFailover.LeaseId, beforeFailover.Epoch, "postgres-policy-test-failover");
        Assert.True(brokenAuthority.Accepted, brokenAuthority.ReasonCode);
        Assert.True(haA.Authority.ApplyReferencedAuthority(
            null, brokenAuthority.Authority!.Epoch, previousAuthorityFenced: true,
            allowAmbiguityRecovery: true).Accepted);
        Assert.True(haB.Authority.ApplyReferencedAuthority(
            null, brokenAuthority.Authority.Epoch, previousAuthorityFenced: true,
            allowAmbiguityRecovery: true).Accepted);
        var assignedAuthority = await referenceA.AssignAfterFenceAsync(
            "node-b", brokenAuthority.Authority.Epoch, "postgres-policy-test-failover");
        Assert.True(assignedAuthority.Accepted, assignedAuthority.ReasonCode);
        Assert.True(haB.Authority.ApplyReferencedAuthority(
            "node-b", assignedAuthority.Authority!.Epoch, previousAuthorityFenced: true,
            standbyWitness: standbyWitness).Accepted);
        Assert.True(haA.Authority.ApplyReferencedAuthority(
            "node-b", assignedAuthority.Authority.Epoch, previousAuthorityFenced: true).Accepted);
        Assert.False(haA.CanOwnIndustrialEffects());
        Assert.True(haB.CanOwnIndustrialEffects());

        var newActiveFence = await coordinatorB.CheckAsync();
        Assert.True(newActiveFence.Allowed, newActiveFence.ReasonCode);
        Assert.Equal(3, newActiveFence.PolicyVersion);
        gateB.Activate("project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
            newActiveFence.PolicyVersion, newActiveFence.HaEpoch);
        Assert.True(gateB.TryGetToken(
            "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
            out var activeBPermit, newActiveFence.PolicyVersion, newActiveFence.HaEpoch));

        if (!sharedDatabase)
        {
            await SetDatabasePolicyVersionAsync(connectionB, 2);
            var staleActiveFence = await coordinatorB.CheckAsync();
            Assert.False(staleActiveFence.Allowed);
            Assert.Equal("authority-policy-reconciliation-required", staleActiveFence.ReasonCode);
            Assert.True(activeBPermit.IsCancellationRequested);
            Assert.False(gateB.TryGetToken(
                "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                out _, newActiveFence.PolicyVersion, newActiveFence.HaEpoch));

            // Recreate the standby's durable store after simulated process loss. The peer
            // must repair the exact committed policy before the newly active node is ready.
            await using var restartedStoreB = new PostgreSqlAuthorityPolicyStore(connectionB);
            await restartedStoreB.InitializeAsync();
            var restartedGateB = new AssetProcessIntelligenceExecutionGate();
            using var restartedCoordinatorB = new AuthorityPolicyHaCoordinator(
                restartedStoreB,
                new AuthorityPolicyReplicationStoreAdapter(restartedStoreB),
                haB,
                referenceB,
                optionsB,
                authenticatorB,
                restartedGateB,
                TimeProvider.System,
                NullLogger<AuthorityPolicyHaCoordinator>.Instance);
            peer.PeerCoordinator = restartedCoordinatorB;
            Assert.Equal("authority-policy-reconciliation-required",
                (await restartedCoordinatorB.CheckAsync()).ReasonCode);
            await coordinatorA.SynchronizeOnceAsync();
            var restartedReady = await restartedCoordinatorB.CheckAsync();
            Assert.True(restartedReady.Allowed, restartedReady.ReasonCode);
            Assert.Equal(3, (await restartedStoreB.RefreshAsync()).Version);
            Assert.False(restartedGateB.TryGetToken(
                "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                out _, restartedReady.PolicyVersion, restartedReady.HaEpoch));
            restartedGateB.Activate("project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                restartedReady.PolicyVersion, restartedReady.HaEpoch);
            Assert.True(restartedGateB.TryGetToken(
                "project-a", 7, AssetProcessIntelligenceScopeKind.Equipment, scopeId,
                out _, restartedReady.PolicyVersion, restartedReady.HaEpoch));
        }
        else
        {
            Assert.Equal(3, (await storeB.RefreshAsync()).Version);
            Assert.False(activeBPermit.IsCancellationRequested);
        }

        await using var restartedA = new PostgreSqlAuthorityPolicyStore(connectionA);
        await using var restartedB = new PostgreSqlAuthorityPolicyStore(connectionB);
        await restartedA.InitializeAsync();
        await restartedB.InitializeAsync();
        var persistedA = await restartedA.RefreshAsync();
        var persistedB = await restartedB.RefreshAsync();
        Assert.Equal(3, persistedA.Version);
        Assert.Equal(persistedA.Version, persistedB.Version);
        Assert.Equal(AuthorityPolicySnapshotDigest.Compute(persistedA), AuthorityPolicySnapshotDigest.Compute(persistedB));
    }

    private static RuntimeHighAvailabilityService CreateReadyService(
        RuntimeHaTopologyDefinition topology,
        DateTimeOffset now)
    {
        var service = new RuntimeHighAvailabilityService(topology, () => now);
        foreach (var node in topology.Nodes)
        {
            service.Authority.UpdateNodeReadiness(node.NodeId, new RuntimeHaNodeReadinessEvidence(
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
        ClusterId: "authority-policy-postgres-ha-test",
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
        SharedSecret: Secret,
        MaxPayloadBytes: 64 * 1024,
        RequestTimeout: TimeSpan.FromSeconds(3),
        PollInterval: TimeSpan.FromMilliseconds(500),
        MaximumRetryDelay: TimeSpan.FromSeconds(2),
        AuthenticationFreshness: TimeSpan.FromMinutes(1),
        PeerFreshnessWindow: TimeSpan.FromMinutes(1),
        StatePath: null);

    private static async Task CreateDatabaseAsync(string adminConnectionString, string database)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\";", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string adminConnectionString, string database)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using (var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @database AND pid <> pg_backend_pid();",
            connection))
        {
            terminate.Parameters.AddWithValue("database", database);
            await terminate.ExecuteNonQueryAsync();
        }
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\";", connection);
        await drop.ExecuteNonQueryAsync();
    }

    private static async Task SetDatabasePolicyVersionAsync(string connectionString, long version)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE elitescada.authority_policy_state SET version = @version WHERE state_key = 'canonical-policy-v1';",
            connection);
        command.Parameters.AddWithValue("version", version);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private sealed class InProcessPeerHandler(AuthorityPolicyHaCoordinator peerCoordinator) : HttpMessageHandler
    {
        public bool Available { get; set; } = true;
        public AuthorityPolicyHaCoordinator PeerCoordinator { get; set; } = peerCoordinator;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!Available) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            var context = new DefaultHttpContext();
            using var services = new ServiceCollection().AddLogging().ConfigureHttpJsonOptions(_ => { }).BuildServiceProvider();
            context.RequestServices = services;
            context.Request.Method = request.Method.Method;
            context.Request.Path = request.RequestUri!.AbsolutePath;
            context.Request.Body = new MemoryStream(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            foreach (var header in request.Headers)
                context.Request.Headers[header.Key] = header.Value.ToArray();
            using var body = new MemoryStream();
            context.Response.Body = body;

            var result = await PeerCoordinator.ReceivePeerSnapshotAsync(context.Request, cancellationToken);
            await result.ExecuteAsync(context);
            var response = new HttpResponseMessage((HttpStatusCode)context.Response.StatusCode)
            {
                Content = new ByteArrayContent(body.ToArray())
            };
            if (!string.IsNullOrWhiteSpace(context.Response.ContentType))
                response.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Response.ContentType);
            return response;
        }
    }
}
