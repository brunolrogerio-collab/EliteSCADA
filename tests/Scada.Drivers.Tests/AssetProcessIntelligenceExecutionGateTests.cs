using Scada.Api.Licensing;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;

namespace Scada.Drivers.Tests;

public sealed class AssetProcessIntelligenceExecutionGateTests
{
    [Fact]
    public void GateStartsClosedAndScopePauseCancelsItsExistingToken()
    {
        var gate = new AssetProcessIntelligenceExecutionGate();
        var scopeId = Guid.NewGuid();
        Assert.False(gate.TryGetToken("project-a", 4, AssetProcessIntelligenceScopeKind.Equipment, scopeId, out _));

        gate.Activate("project-a", 4, AssetProcessIntelligenceScopeKind.Equipment, scopeId);
        Assert.True(gate.TryGetToken("PROJECT-A", 4, AssetProcessIntelligenceScopeKind.Equipment, scopeId, out var token));
        Assert.False(token.IsCancellationRequested);

        gate.Pause("project-a", AssetProcessIntelligenceScopeKind.Equipment, scopeId);

        Assert.True(token.IsCancellationRequested);
        Assert.False(gate.TryGetToken("project-a", 4, AssetProcessIntelligenceScopeKind.Equipment, scopeId, out _));
        Assert.Equal(0, gate.ActivePermitCount);
    }

    [Fact]
    public void ActiveRevisionChangeAndLicenseChangeInvalidatePendingWork()
    {
        var gate = new AssetProcessIntelligenceExecutionGate();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var otherProjectScope = Guid.NewGuid();
        gate.Activate("project-a", 4, AssetProcessIntelligenceScopeKind.Location, first);
        gate.Activate("project-a", 4, AssetProcessIntelligenceScopeKind.Equipment, second);
        gate.Activate("project-b", 9, AssetProcessIntelligenceScopeKind.Location, otherProjectScope);
        Assert.True(gate.TryGetToken("project-a", 4, AssetProcessIntelligenceScopeKind.Location, first, out var firstToken));
        Assert.True(gate.TryGetToken("project-a", 4, AssetProcessIntelligenceScopeKind.Equipment, second, out var secondToken));
        Assert.True(gate.TryGetToken("project-b", 9, AssetProcessIntelligenceScopeKind.Location, otherProjectScope, out var otherProjectToken));

        gate.InvalidateProject("PROJECT-A");

        Assert.True(firstToken.IsCancellationRequested);
        Assert.True(secondToken.IsCancellationRequested);
        Assert.False(otherProjectToken.IsCancellationRequested);
        Assert.True(gate.TryGetToken("project-b", 9, AssetProcessIntelligenceScopeKind.Location, otherProjectScope, out _));
        Assert.False(gate.TryGetToken("project-a", 5, AssetProcessIntelligenceScopeKind.Location, first, out _));

        gate.Activate("project-a", 5, AssetProcessIntelligenceScopeKind.Location, first);
        Assert.True(gate.TryGetToken("project-a", 5, AssetProcessIntelligenceScopeKind.Location, first, out var newToken));
        gate.InvalidateAll();
        Assert.True(newToken.IsCancellationRequested);
        Assert.True(otherProjectToken.IsCancellationRequested);
        Assert.Equal(0, gate.ActivePermitCount);
    }

    [Fact]
    public void PersistedActiveScopeDoesNotRearmAfterProcessRestart()
    {
        var scopeId = Guid.NewGuid();
        var persistedConfiguration = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        persistedConfiguration.Replace(new AssetProcessIntelligenceEngineeringDto(Scopes:
        [new(AssetProcessIntelligenceScopeKind.Equipment, scopeId, Active: true)]));

        // A new gate models a fresh process after service restart. Persisted configuration
        // is intentionally not an input to gate initialization.
        var restartedProcessGate = new AssetProcessIntelligenceExecutionGate();

        Assert.True(Assert.Single(persistedConfiguration.Snapshot().Scopes!).Active);
        Assert.Equal(0, restartedProcessGate.ActivePermitCount);
        Assert.False(restartedProcessGate.TryGetToken(
            "project-a", 4, AssetProcessIntelligenceScopeKind.Equipment, scopeId, out _));
    }
}
