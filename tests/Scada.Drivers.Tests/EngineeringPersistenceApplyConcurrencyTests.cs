using Scada.Api.Persistence;
using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class EngineeringPersistenceApplyConcurrencyTests
{
    [Fact]
    public async Task ExecuteAsync_HoldsWorkspaceMutationLeaseUntilApplyCompletes()
    {
        using var workspace = new EngineeringWorkspace();
        var expectedChangeVersion = workspace.CaptureChangeVersion();
        var applyStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowApply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var apply = EngineeringPersistenceApplyGuard.ExecuteAsync(
            workspace,
            expectedChangeVersion,
            async cancellationToken =>
            {
                applyStarted.TrySetResult();
                await allowApply.Task.WaitAsync(cancellationToken);
                return 1;
            });

        await applyStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var competingMutation = workspace.AcquireMutationAsync().AsTask();
        await Task.Yield();
        Assert.False(competingMutation.IsCompleted);

        allowApply.TrySetResult();
        Assert.Equal(1, await apply.WaitAsync(TimeSpan.FromSeconds(1)));
        await using var mutation = await competingMutation.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_RejectsStaleObservedVersionBeforeApplyRuns()
    {
        using var workspace = new EngineeringWorkspace();
        var expectedChangeVersion = workspace.CaptureChangeVersion();
        workspace.MarkDirty();
        var invoked = false;

        var conflict = await Assert.ThrowsAsync<EngineeringWorkspaceVersionConflictException>(() =>
            EngineeringPersistenceApplyGuard.ExecuteAsync(
                workspace,
                expectedChangeVersion,
                _ =>
                {
                    invoked = true;
                    return Task.FromResult(1);
                }));

        Assert.False(invoked);
        Assert.Equal(expectedChangeVersion, conflict.ExpectedChangeVersion);
        Assert.Equal(workspace.CaptureChangeVersion(), conflict.CurrentChangeVersion);
    }
}
