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
    }

    [Fact]
    public void StartupReadinessMissCanRetryButRuntimeReadinessLossDurablyClosesRecovery()
    {
        var authority = new AuthorityLifecycleSnapshot(AuthorityLifecycleState.AuthorityPresent, 7);
        var scope = Guid.NewGuid();
        var receipt = new AssetProcessIntelligenceActivationEvidence(7, "plant", 8,
            AssetProcessIntelligenceScopeKind.Equipment, scope, 12, "digest", 0, 5, true);
        bool Recover(AssetProcessIntelligenceExecutionGate gate) => gate.TryRecoverAtStartup(
            receipt, authority, "plant", 8, receipt.ScopeKind, scope, 12, "digest", 0, 5);

        var starting = new AssetProcessIntelligenceExecutionGate();
        starting.BeginStartupRecovery();
        Assert.True(starting.StartupRecoveryPending);
        starting.InvalidateForAuthorityReadinessFailure();
        Assert.True(starting.StartupRecoveryPending);
        Assert.True(Recover(starting));
        starting.CompleteStartupRecovery();
        Assert.False(starting.StartupRecoveryPending);

        var running = new AssetProcessIntelligenceExecutionGate();
        var persistedInvalidations = 0;
        running.ConfigureDurableInvalidation(() => persistedInvalidations++);
        Assert.True(Recover(running));
        Assert.True(running.TryGetToken("plant", 8, receipt.ScopeKind, scope, out var token, 12, 0));

        running.InvalidateForAuthorityReadinessFailure();

        Assert.True(token.IsCancellationRequested);
        Assert.Equal(1, persistedInvalidations);
        Assert.False(Recover(running));
    }

    [Fact]
    public void DurableInvalidationFailureClosesTokensAndCannotBeReportedAsSuccess()
    {
        var gate = new AssetProcessIntelligenceExecutionGate();
        var scope = Guid.NewGuid();
        gate.Activate("plant", 8, AssetProcessIntelligenceScopeKind.Equipment, scope, 12);
        Assert.True(gate.TryGetToken("plant", 8, AssetProcessIntelligenceScopeKind.Equipment, scope, out var token, 12));
        gate.ConfigureDurableInvalidation(() =>
        {
            Assert.True(token.IsCancellationRequested);
            throw new IOException("Test durable store unavailable");
        });
        Assert.Throws<IOException>(() => gate.InvalidateAll());
        Assert.Equal(0, gate.ActivePermitCount);
        Assert.False(gate.TryGetToken("plant", 8, AssetProcessIntelligenceScopeKind.Equipment, scope, out _, 12));
        Assert.Throws<InvalidOperationException>(() => gate.ConfigureDurableInvalidation(() => { }));
    }

    [Fact]
    public void BaselineNormalizationRemovesOnlyHistorianMetadataThatMatchesTypedConfiguration()
    {
        var tag = new TagEngineeringDto(Guid.NewGuid(), "Pressure", "Plant.Pressure",
            Scada.Core.Tags.TagDataType.Double, Historian: new HistorianSettingsDto(true, "periodic"),
            Metadata: new() { ["historian.enabled"] = "True", ["historian.strategy"] = "periodic" });
        Assert.Null(AssetProcessIntelligenceApi.NormalizeHistorianMetadata(tag).Metadata);
        var conflicting = tag with { Metadata = new() { ["historian.enabled"] = "False", ["historian.strategy"] = "other", ["custom"] = "keep" } };
        var normalized = AssetProcessIntelligenceApi.NormalizeHistorianMetadata(conflicting);
        Assert.Equal(3, normalized.Metadata!.Count);
        Assert.Equal("False", normalized.Metadata["historian.enabled"]);
        Assert.Equal("other", normalized.Metadata["historian.strategy"]);
        Assert.Equal("keep", normalized.Metadata["custom"]);
        Assert.Equal(2, tag.Metadata!.Count);
        var untyped = tag with { Historian = null };
        Assert.Same(untyped, AssetProcessIntelligenceApi.NormalizeHistorianMetadata(untyped));
    }}
