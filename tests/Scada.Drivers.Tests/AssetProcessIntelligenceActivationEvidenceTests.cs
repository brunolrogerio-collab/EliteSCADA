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
    [Fact]
    public void StartupRecoveryCannotResurrectAfterPauseInvalidationOrStartupCompletion()
    {
        var authority = new AuthorityLifecycleSnapshot(AuthorityLifecycleState.AuthorityPresent, 7);
        var scope = Guid.NewGuid();
        var receipt = new AssetProcessIntelligenceActivationEvidence(7, "plant", 8,
            AssetProcessIntelligenceScopeKind.Equipment, scope, 12, "digest", 0, 5, true);
        bool Recover(AssetProcessIntelligenceExecutionGate gate) => gate.TryRecoverAtStartup(
            receipt, authority, "plant", 8, receipt.ScopeKind, scope, 12, "digest", 0, 5);
        var fresh = new AssetProcessIntelligenceExecutionGate();
        Assert.True(Recover(fresh));
        Assert.True(fresh.TryGetToken("plant", 8, receipt.ScopeKind, scope, out var token, 12, 0));
        fresh.Pause("plant", receipt.ScopeKind, scope);
        Assert.True(token.IsCancellationRequested);
        Assert.False(Recover(fresh));
        var invalidated = new AssetProcessIntelligenceExecutionGate();
        invalidated.InvalidateAll();
        Assert.False(Recover(invalidated));
        var completed = new AssetProcessIntelligenceExecutionGate();
        completed.CompleteStartupRecovery();
        Assert.False(Recover(completed));
        Assert.True(Recover(new AssetProcessIntelligenceExecutionGate()));
    }}
