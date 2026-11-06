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
            await using var parallelInitializer = new AssetProcessIntelligenceActivationStore(connection);
            await Task.WhenAll(firstProcess.InitializeAsync(), parallelInitializer.InitializeAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => firstProcess.ActivateAsync(evidence with { PolicyDigest = "invalid-digest" }));
            committed = await firstProcess.ActivateAsync(evidence);
        }
        await using var restarted = new AssetProcessIntelligenceActivationStore(connection);
        Assert.Equal(committed, await restarted.ReadAsync(evidence.ProjectKey, evidence.ScopeKind, evidence.ScopeId));
        var localGate = new AssetProcessIntelligenceExecutionGate();
        localGate.Activate(committed.ProjectKey, committed.Revision, committed.ScopeKind, committed.ScopeId,
            committed.PolicyVersion, committed.HaEpoch, committed.Generation);
        Assert.True(localGate.TryGetToken(committed.ProjectKey, committed.Revision, committed.ScopeKind,
            committed.ScopeId, out var oldToken, committed.PolicyVersion, committed.HaEpoch));
        async Task<bool> ValidateLocalAsync() => await AssetProcessIntelligenceAnalyticsService.ValidateDurablePermitAsync(
            restarted, localGate, committed.ProjectKey, committed.Revision, committed.ScopeKind, committed.ScopeId,
            committed.PolicyVersion, committed.HaEpoch, CancellationToken.None);
        Assert.True(await ValidateLocalAsync());
        await using (var remoteNode = new AssetProcessIntelligenceActivationStore(connection))
        {
            await remoteNode.PauseAsync(committed.ProjectKey, committed.ScopeKind, committed.ScopeId);
            Assert.False(await ValidateLocalAsync());
            Assert.True(oldToken.IsCancellationRequested);
            var replacement = await remoteNode.ActivateAsync(evidence);
            localGate.Activate(committed.ProjectKey, committed.Revision, committed.ScopeKind, committed.ScopeId,
                committed.PolicyVersion, committed.HaEpoch, committed.Generation);
            Assert.False(await ValidateLocalAsync());
            Assert.Equal(0, localGate.ActivePermitCount);
            committed = replacement;
        }
        var secondScope = await restarted.ActivateAsync(evidence with { ScopeId = Guid.NewGuid() });
        var pauseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseRecovery = new ManualResetEventSlim();
        var recoveryTask = Task.Run(() => restarted.TryRecoverAsync(committed, _ =>
        {
            callbackEntered.TrySetResult();
            if (!releaseRecovery.Wait(TimeSpan.FromSeconds(15)))
                throw new TimeoutException("Recovery barrier not released.");
            return true;
        }));
        await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await using var pausingNode = new AssetProcessIntelligenceActivationStore(connection);
        var pauseTask = Task.Run(async () =>
        {
            pauseStarted.TrySetResult();
            await pausingNode.PauseAsync(evidence.ProjectKey, evidence.ScopeKind, evidence.ScopeId);
        });
        try { await pauseStarted.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        finally { releaseRecovery.Set(); }
        Assert.True(await recoveryTask.WaitAsync(TimeSpan.FromSeconds(15)));
        await pauseTask.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Null(await restarted.ReadAsync(evidence.ProjectKey, evidence.ScopeKind, evidence.ScopeId));
        Assert.Equal(secondScope, await restarted.ReadAsync(secondScope.ProjectKey, secondScope.ScopeKind, secondScope.ScopeId));
        Assert.False(await restarted.TryRecoverAsync(committed, _ => throw new InvalidOperationException("Paused receipt resurrected")));
        committed = await restarted.ActivateAsync(evidence);
        var previousRevisionReceipt = committed;
        committed = await restarted.ActivateAsync(committed with { Revision = committed.Revision + 1 },
            expectedPrevious: committed);
        Assert.True(committed.Generation > previousRevisionReceipt.Generation);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.ActivateAsync(
            previousRevisionReceipt with { Revision = committed.Revision + 1 }, expectedPrevious: previousRevisionReceipt));
        await restarted.PauseAsync(secondScope.ProjectKey, secondScope.ScopeKind, secondScope.ScopeId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.ActivateAsync(
            secondScope with { Revision = secondScope.Revision + 1 }, expectedPrevious: secondScope));
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
