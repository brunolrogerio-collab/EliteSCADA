using Scada.Api.Licensing;
using Scada.Engineering.Contracts;
using Scada.Security.Authentication;

namespace Scada.Drivers.Tests;

public sealed class AssetProcessIntelligenceActivationEvidenceTests
{
    [Fact]
    public void RecoveryRequiresTheExactDurableActivationAndCurrentFence()
    {
        var authority = new AuthorityLifecycleSnapshot(AuthorityLifecycleState.AuthorityPresent, 7);
        var scope = Guid.NewGuid();
        var evidence = new AssetProcessIntelligenceActivationEvidence(authority.Epoch, "plant", 8,
            AssetProcessIntelligenceScopeKind.Equipment, scope, 12, "digest", 3, 5, true);
        bool Matches(AssetProcessIntelligenceActivationEvidence candidate) => candidate.Matches(
            authority, "PLANT", 8, AssetProcessIntelligenceScopeKind.Equipment, scope, 12, "digest", 3, 5);
        Assert.True(Matches(evidence));
        Assert.False(evidence.Matches(authority with { State = AuthorityLifecycleState.DeliberatelyDetached }, "plant", 8,
            AssetProcessIntelligenceScopeKind.Equipment, scope, 12, "digest", 3, 5));
        Assert.False(Matches(evidence with { Active = false }));
        Assert.False(Matches(evidence with { AuthorityEpoch = 8 }));
        Assert.False(Matches(evidence with { ProjectKey = "other" }));
        Assert.False(Matches(evidence with { Revision = 9 }));
        Assert.False(Matches(evidence with { ScopeKind = AssetProcessIntelligenceScopeKind.Location }));
        Assert.False(Matches(evidence with { ScopeId = Guid.NewGuid() }));
        Assert.False(Matches(evidence with { PolicyVersion = 14 }));
        Assert.False(Matches(evidence with { PolicyDigest = "other" }));
        Assert.False(Matches(evidence with { HaEpoch = 4 }));
        Assert.False(Matches(evidence with { Generation = 6 }));
        Assert.False(Matches(evidence with { Generation = 0 }));
        Assert.False(Matches(evidence with { PolicyDigest = "" }));
    }
}

