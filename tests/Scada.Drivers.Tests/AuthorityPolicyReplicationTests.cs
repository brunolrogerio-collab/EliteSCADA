using Scada.Engineering.Contracts;
using Scada.Engineering.Security;

namespace Scada.Drivers.Tests;

public sealed class AuthorityPolicyReplicationTests
{
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
