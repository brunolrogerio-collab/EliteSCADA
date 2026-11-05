using Scada.Api.Licensing;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Persistence.PostgreSql;

namespace Scada.Drivers.Tests;

public sealed class AssetProcessIntelligenceActivationStoreTests
{
    [Fact]
    public async Task PostgreSqlReceiptSurvivesStoreRestartAndPauseCannotRecoverOldGeneration()
    {
        var connection = Environment.GetEnvironmentVariable("SCADIVO_RECOVERY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection))
        {
            if (Environment.GetEnvironmentVariable("SCADIVO_REQUIRE_RECOVERY_POSTGRES") == "true")
                throw new InvalidOperationException("Recovery PostgreSQL test connection required.");
            return;
        }
        await using var lifecycle = new PostgreSqlAuthorityLifecycleStore(connection);
        await lifecycle.InitializeAsync();
        var authority = await lifecycle.MarkAuthorityPresentAsync();
        await using var policy = new PostgreSqlAuthorityPolicyStore(connection);
        await policy.InitializeAsync();
        var policyWrite = await policy.TryReplaceAsync(policy.Snapshot().Version, [], []);
        Assert.True(policyWrite.Applied);
        var snapshot = policyWrite.Snapshot;
        var evidence = new AssetProcessIntelligenceActivationEvidence(authority.Epoch,
            $"recovery-{Guid.NewGuid():N}", 4, AssetProcessIntelligenceScopeKind.Equipment, Guid.NewGuid(),
            snapshot.Version, AuthorityPolicySnapshotDigest.Compute(snapshot), 0, 1, true);
        AssetProcessIntelligenceActivationEvidence committed;
        await using (var firstProcess = new AssetProcessIntelligenceActivationStore(connection))
        {
            await firstProcess.InitializeAsync();
            committed = await firstProcess.ActivateAsync(evidence);
        }
        await using var restarted = new AssetProcessIntelligenceActivationStore(connection);
        Assert.Equal(committed, await restarted.ReadAsync(evidence.ProjectKey, evidence.ScopeKind, evidence.ScopeId));
        await restarted.PauseAsync(evidence.ProjectKey, evidence.ScopeKind, evidence.ScopeId);
        await using var anotherProcess = new AssetProcessIntelligenceActivationStore(connection);
        Assert.Null(await anotherProcess.ReadAsync(evidence.ProjectKey, evidence.ScopeKind, evidence.ScopeId));
        var reactivated = await anotherProcess.ActivateAsync(evidence);
        Assert.True(reactivated.Generation > committed.Generation);
        await policy.TryReplaceAsync(snapshot.Version, [], []);
        await Assert.ThrowsAsync<InvalidOperationException>(() => anotherProcess.ActivateAsync(evidence));
        await lifecycle.BeginDetachAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => anotherProcess.ActivateAsync(evidence));
    }
}
