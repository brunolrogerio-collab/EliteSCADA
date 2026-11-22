using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class AuthorityPolicyReplicationTests
{
    [Fact]
    public async Task PublicRuntimePolicyIsReplicatedAndIncludedInDigestFence()
    {
        var screenId = Guid.Parse("95000000-0000-0000-0000-000000000011");
        var tagId = Guid.Parse("95000000-0000-0000-0000-000000000012");
        var initial = new InMemoryAuthorityPolicyStore();
        var policy = new RuntimePublicAccessPolicy(true, screenId, [screenId], [], [tagId], [], "project-a", 3);

        var changed = await initial.TryReplacePublicRuntimeAsync(0, policy);
        Assert.True(changed.Applied);
        Assert.Equal(1, changed.Snapshot.Version);
        AssertPublicPolicy(policy, changed.Snapshot.PublicRuntime!);

        var replica = new InMemoryAuthorityPolicyStore();
        var replicated = await replica.ApplyReplicatedSnapshotAsync(changed.Snapshot);
        Assert.True(replicated.Applied);
        AssertPublicPolicy(policy, replica.Snapshot().PublicRuntime!);

        var changedSameVersion = changed.Snapshot with
        {
            PublicRuntime = policy with { TagIds = [Guid.Parse("95000000-0000-0000-0000-000000000013")] }
        };
        var mismatch = await replica.ApplyReplicatedSnapshotAsync(changedSameVersion);
        Assert.False(mismatch.Applied);
        Assert.Equal("AUTHORITY_POLICY_REPLICA_DIGEST_MISMATCH", mismatch.Error);
    }

    private static void AssertPublicPolicy(RuntimePublicAccessPolicy expected, RuntimePublicAccessPolicy actual)
    {
        Assert.Equal(expected.Enabled, actual.Enabled);
        Assert.Equal(expected.StartScreenId, actual.StartScreenId);
        Assert.Equal(expected.ScreenIds.Order(), actual.ScreenIds.Order());
        Assert.Equal(expected.PopupIds.Order(), actual.PopupIds.Order());
        Assert.Equal(expected.TagIds.Order(), actual.TagIds.Order());
        Assert.Equal(expected.VisualAssetIds.Order(), actual.VisualAssetIds.Order());
        Assert.Equal(expected.ProjectKey, actual.ProjectKey);
        Assert.Equal(expected.Revision, actual.Revision);
    }

    [Fact]
    public async Task ReplicaApply_IsMonotonicIdempotentAndDigestFenced()
    {
        var scopeId = Guid.Parse("95000000-0000-0000-0000-000000000001");
        var first = Snapshot(1, new SecurityScopeEngineeringDto(
            scopeId,
            "plant",
            "Plant",
            SecurityScopeNodeKind.Plant,
            Metadata: new Dictionary<string, string>
            {
                ["zeta"] = "last",
                ["alpha"] = "first"
            }));
        var equivalent = Snapshot(1, new SecurityScopeEngineeringDto(
            scopeId,
            "plant",
            "Plant",
            SecurityScopeNodeKind.Plant,
            Metadata: new Dictionary<string, string>
            {
                ["alpha"] = "first",
                ["zeta"] = "last"
            }));
        var sameVersionDifferentContent = Snapshot(1, new SecurityScopeEngineeringDto(
            scopeId,
            "plant",
            "Plant",
            SecurityScopeNodeKind.Plant,
            Metadata: new Dictionary<string, string> { ["alpha"] = "tampered" }));
        var replica = new InMemoryAuthorityPolicyStore();

        Assert.Equal(
            AuthorityPolicySnapshotDigest.Compute(first),
            AuthorityPolicySnapshotDigest.Compute(equivalent));

        var applied = await replica.ApplyReplicatedSnapshotAsync(first);
        Assert.True(applied.Applied);
        Assert.Null(applied.Error);
        Assert.Equal(1, applied.Snapshot.Version);

        var idempotent = await replica.ApplyReplicatedSnapshotAsync(equivalent);
        Assert.True(idempotent.Applied);
        Assert.Equal(1, idempotent.Snapshot.Version);

        var digestMismatch = await replica.ApplyReplicatedSnapshotAsync(sameVersionDifferentContent);
        Assert.False(digestMismatch.Applied);
        Assert.Equal("AUTHORITY_POLICY_REPLICA_DIGEST_MISMATCH", digestMismatch.Error);
        Assert.Equal("first", digestMismatch.Snapshot.Scopes.Single().Metadata!["alpha"]);

        var stale = await replica.ApplyReplicatedSnapshotAsync(
            new AuthorityPolicySnapshot(0, Array.Empty<SecurityRoleEngineeringDto>(), Array.Empty<SecurityScopeEngineeringDto>()));
        Assert.False(stale.Applied);
        Assert.Equal("AUTHORITY_POLICY_REPLICA_STALE", stale.Error);
        Assert.Equal(1, (await replica.RefreshAsync()).Version);
    }

    private static AuthorityPolicySnapshot Snapshot(long version, SecurityScopeEngineeringDto scope) =>
        new(version, Array.Empty<SecurityRoleEngineeringDto>(), [scope]);
}
