using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Scada.Api.Runtime;
using Scada.Core.Tags;

namespace Scada.Drivers.Tests;

public sealed class RuntimeHighAvailabilityPeerTransportTests
{
    [Fact]
    public void HmacAuthentication_BindsNodePathPayloadAndRejectsReplay()
    {
        var now = DateTimeOffset.Parse("2026-09-30T13:00:00Z");
        var options = Options();
        var authenticator = new RuntimeHaPeerAuthenticator(
            options,
            () => now);
        var body = Encoding.UTF8.GetBytes("{\"sequence\":1}");
        var headers = authenticator.Sign(
            "POST",
            RuntimeHaPeerTransportOptions.ReplicationPath,
            body,
            "node-a");

        var accepted = authenticator.Verify(
            headers,
            "POST",
            RuntimeHaPeerTransportOptions.ReplicationPath,
            body,
            "node-a");
        Assert.True(accepted.Accepted);

        var replay = authenticator.Verify(
            headers,
            "POST",
            RuntimeHaPeerTransportOptions.ReplicationPath,
            body,
            "node-a");
        Assert.False(replay.Accepted);
        Assert.Equal("peer-auth-replay", replay.ReasonCode);

        var changedPayload = new RuntimeHaPeerAuthenticator(
            options,
            () => now).Verify(
                headers,
                "POST",
                RuntimeHaPeerTransportOptions.ReplicationPath,
                Encoding.UTF8.GetBytes("{\"sequence\":2}"),
                "node-a");
        Assert.False(changedPayload.Accepted);
        Assert.Equal("peer-auth-signature-invalid", changedPayload.ReasonCode);
    }

    [Fact]
    public void StandbyAuthorityRestart_CanResynchronizeWithoutGrantingAuthority()
    {
        var now = DateTimeOffset.Parse("2026-09-30T13:00:00Z");
        var nodeA = Service(
            "node-a",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            () => now);
        var firstNodeB = Service(
            "node-b",
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            () => now);

        nodeA.ObserveLocalReadiness(Evidence(now, synchronized: true));
        firstNodeB.ObserveLocalReadiness(Evidence(now, synchronized: true));
        var originalB = firstNodeB.CreatePeerObservation();
        Assert.True(nodeA.ApplyPeerObservation(originalB).Accepted);

        var restartedB = Service(
            "node-b",
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            () => now);
        restartedB.ObserveLocalReadiness(Evidence(now, synchronized: false));
        var restartObservation = restartedB.CreatePeerObservation();

        var restarted = nodeA.ApplyPeerObservation(restartObservation);

        Assert.True(restarted.Accepted);
        Assert.False(restarted.Snapshot.AmbiguousAuthority);
        Assert.Equal("node-a", restarted.Snapshot.EffectiveActiveNodeId);
        Assert.False(restartedB.TryAcquireLocalIndustrialAuthority().Allowed);

        var staleOldInstance = nodeA.ApplyPeerObservation(originalB);
        Assert.False(staleOldInstance.Accepted);
        Assert.Equal(
            "peer-authority-instance-stale",
            staleOldInstance.ReasonCode);
        Assert.False(staleOldInstance.Snapshot.AmbiguousAuthority);
    }

    [Fact]
    public void SessionTombstone_RemovesMirrorAndPreventsResurrection()
    {
        var now = DateTimeOffset.Parse("2026-09-30T13:00:00Z");
        var source = new RuntimeSessionLeaseContinuityRegistry(() => now);
        var standby = new RuntimeSessionLeaseContinuityRegistry(() => now);
        var lease = new RuntimeSessionLease(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "operator",
            "runtime-client",
            RuntimeConnectionClass.Interactive,
            now,
            now,
            now.AddMinutes(2),
            "node-a",
            "cluster-a",
            "engineering",
            "project-a",
            7,
            now,
            Generation: 2,
            AuthorityRevision: 11);

        var envelope = source.CaptureOrRetain(
            lease,
            "cluster-a",
            "node-a");
        Assert.True(
            standby.ImportPeerEnvelope(
                envelope,
                "cluster-a",
                "node-a").Accepted);
        Assert.True(source.Terminate(
            "cluster-a",
            "operator",
            "runtime-client",
            lease.SessionId));

        var tombstone = Assert.Single(
            source.TombstoneSnapshot("cluster-a"));
        var applied = standby.ApplyPeerTombstone(
            tombstone,
            "cluster-a",
            "node-a");

        Assert.True(applied.Accepted);
        Assert.Empty(standby.Snapshot());

        var resurrect = standby.ImportPeerEnvelope(
            envelope with
            {
                LastHeartbeatUtc = now.AddSeconds(10),
                ExpiresAtUtc = now.AddMinutes(3)
            },
            "cluster-a",
            "node-a");
        Assert.False(resurrect.Accepted);
        Assert.Equal(
            "session-terminated-or-expired-session",
            resurrect.ReasonCode);
    }

    [Fact]
    public async Task DurableMirror_ReloadsFailClosedUntilLiveResynchronization()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "elitescada-ha-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var statePath = Path.Combine(root, "node-b.json");
            var options = Options() with
            {
                LocalNodeId = "node-b",
                PeerNodeId = "node-a",
                StatePath = statePath
            };
            var first = new RuntimeHaPeerMirrorStore(
                options,
                TimeProvider.System,
                NullLogger<RuntimeHaPeerMirrorStore>.Instance);
            var envelope = ReplicationEnvelope();

            var applied = await first.ApplyAsync(
                envelope,
                CancellationToken.None);
            Assert.True(applied.Accepted);
            Assert.True(applied.Snapshot.LiveSynchronized);
            Assert.True(File.Exists(statePath));

            var restarted = new RuntimeHaPeerMirrorStore(
                options,
                TimeProvider.System,
                NullLogger<RuntimeHaPeerMirrorStore>.Instance);
            var loaded = restarted.Snapshot();

            Assert.True(loaded.HasState);
            Assert.False(loaded.LiveSynchronized);
            Assert.Equal(
                "durable-state-loaded-awaiting-live-resync",
                loaded.ReasonCode);
            Assert.Equal(7, loaded.AuthoritativeState!.Runtime.Revision);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static RuntimeHaPeerReplicationEnvelope ReplicationEnvelope()
    {
        var now = DateTimeOffset.UtcNow;
        var runtime = new RuntimeHaRuntimeIdentity(
            "engineering",
            "project-a",
            7);
        var readiness = new RuntimeHaNodeReadinessEvidence(
            true,
            true,
            true,
            runtime,
            now);
        var observation = new RuntimeHaPeerObservationEnvelope(
            RuntimeHaPeerObservationEnvelope.SchemaName,
            RuntimeHaPeerObservationEnvelope.CurrentSchemaVersion,
            "cluster-a",
            3,
            "node-a",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            1,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            1,
            "node-a",
            false,
            readiness,
            now);
        var state = new RuntimeHaAuthoritativeStateSnapshot(
            runtime,
            1,
            new RuntimeHaPeerLicenseEvidence(true, true, 500, 2, 2),
            new[]
            {
                new RuntimeHaMirroredTagValue(
                    Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    System.Text.Json.JsonSerializer.SerializeToElement(42),
                    now,
                    TagQuality.Good,
                    "test",
                    null,
                    null)
            },
            Array.Empty<RuntimeSessionLeaseContinuityEnvelope>(),
            Array.Empty<RuntimeSessionLeaseContinuityTombstoneEnvelope>(),
            now);
        return new RuntimeHaPeerReplicationEnvelope(
            RuntimeHaPeerReplicationEnvelope.SchemaName,
            RuntimeHaPeerReplicationEnvelope.CurrentSchemaVersion,
            "cluster-a",
            3,
            "node-a",
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            1,
            observation,
            state,
            now);
    }

    private static RuntimeHighAvailabilityService Service(
        string localNodeId,
        Guid authorityInstanceId,
        Func<DateTimeOffset> utcNow) =>
        new(
            new RuntimeHaTopologyDefinition(
                true,
                "cluster-a",
                localNodeId,
                "node-a",
                3,
                TimeSpan.FromSeconds(15),
                new[]
                {
                    new RuntimeHaNodeDefinition(
                        "node-a",
                        new[]
                        {
                            new RuntimeHaEndpoint(
                                RuntimeHaEndpointKind.Local,
                                "https://node-a.test",
                                0)
                        }),
                    new RuntimeHaNodeDefinition(
                        "node-b",
                        new[]
                        {
                            new RuntimeHaEndpoint(
                                RuntimeHaEndpointKind.Local,
                                "https://node-b.test",
                                0)
                        })
                }),
            utcNow,
            authorityInstanceId);

    private static RuntimeHaNodeReadinessEvidence Evidence(
        DateTimeOffset now,
        bool synchronized) =>
        new(
            true,
            synchronized,
            true,
            new RuntimeHaRuntimeIdentity(
                "engineering",
                "project-a",
                7),
            now);

    private static RuntimeHaPeerTransportOptions Options() =>
        new(
            true,
            "node-b",
            "node-a",
            new Uri(
                "https://node-a.test" +
                RuntimeHaPeerTransportOptions.ReplicationPath),
            "0123456789abcdef0123456789abcdef",
            1024 * 1024,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(15),
            null);
}
