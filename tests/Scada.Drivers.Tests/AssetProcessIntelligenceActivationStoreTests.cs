using Npgsql;
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
        var database = $"intelligence_recovery_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\";", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var isolated = new NpgsqlConnectionStringBuilder(connection) { Database = database };
            await VerifyReceiptLifecycleAsync(isolated.ConnectionString);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE);", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task VerifyReceiptLifecycleAsync(string connection)
    {
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
            await Assert.ThrowsAsync<InvalidOperationException>(() => firstProcess.ActivateAsync(evidence with { PolicyDigest = "invalid-digest" }));
            committed = await firstProcess.ActivateAsync(evidence);
        }
        await using var restarted = new AssetProcessIntelligenceActivationStore(connection);
        Assert.Equal(committed, await restarted.ReadAsync(evidence.ProjectKey, evidence.ScopeKind, evidence.ScopeId));
        var recoveryCalls = 0;
        Assert.True(await restarted.TryRecoverAsync(committed, _ => { recoveryCalls++; return true; }));
        await restarted.PauseAsync(evidence.ProjectKey, evidence.ScopeKind, evidence.ScopeId);
        Assert.False(await restarted.TryRecoverAsync(committed, _ => { recoveryCalls++; return true; }));
        Assert.Equal(1, recoveryCalls);
        await using var anotherProcess = new AssetProcessIntelligenceActivationStore(connection);
        Assert.Null(await anotherProcess.ReadAsync(evidence.ProjectKey, evidence.ScopeKind, evidence.ScopeId));
        var reactivated = await anotherProcess.ActivateAsync(evidence);
        Assert.True(reactivated.Generation > committed.Generation);
        Assert.False(await restarted.TryRecoverAsync(committed, _ => throw new InvalidOperationException("Stale generation recovered")));
        await restarted.InvalidateAllAsync();
        Assert.Null(await anotherProcess.ReadAsync(evidence.ProjectKey, evidence.ScopeKind, evidence.ScopeId));
        Assert.False(await anotherProcess.TryRecoverAsync(reactivated, _ => throw new InvalidOperationException("Invalidated receipt recovered")));
        await policy.TryReplaceAsync(snapshot.Version, [], []);
        await Assert.ThrowsAsync<InvalidOperationException>(() => anotherProcess.ActivateAsync(evidence));
        await lifecycle.BeginDetachAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => anotherProcess.ActivateAsync(evidence));
    }
}

