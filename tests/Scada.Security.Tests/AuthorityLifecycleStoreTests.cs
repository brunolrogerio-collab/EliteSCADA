using Scada.Security.Authentication;

namespace Scada.Security.Tests;

public sealed class AuthorityLifecycleStoreTests
{
    [Fact]
    public async Task FreshUnmarkedInstallation_IsInitial_NotDeliberatelyDetachedOrAttached()
    {
        var store = new InMemoryAuthorityLifecycleStore();

        await store.InitializeAsync();
        var lifecycle = await store.GetAsync();

        Assert.Equal(AuthorityLifecycleState.InitialInstallation, lifecycle.State);
        Assert.Equal(1, lifecycle.Epoch);
        Assert.False(lifecycle.AllowsAuthorization);
    }

    [Fact]
    public async Task DetachIntent_FencesOldSessions_AndRecoveryIsIdempotent()
    {
        var store = new InMemoryAuthorityLifecycleStore();
        var attached = await store.MarkAuthorityPresentAsync();
        var issued = await store.GetAsync();
        Assert.Equal(attached, issued);
        Assert.True(AuthorityLifecycleSessionFence.IsCurrent(issued, issued.Epoch));

        var inProgress = await store.BeginDetachAsync();
        Assert.Equal(AuthorityLifecycleState.DetachInProgress, inProgress.State);
        Assert.Equal(issued.Epoch, inProgress.Epoch);
        Assert.False(AuthorityLifecycleSessionFence.IsCurrent(inProgress, issued.Epoch));
        Assert.Equal(inProgress, await store.BeginDetachAsync());

        var detached = await store.CompleteDetachAsync();
        Assert.Equal(AuthorityLifecycleState.DeliberatelyDetached, detached.State);
        Assert.Equal(issued.Epoch + 1, detached.Epoch);
        Assert.False(AuthorityLifecycleSessionFence.IsCurrent(detached, issued.Epoch));
        Assert.Equal(detached, await store.CompleteDetachAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.MarkAuthorityPresentAsync());
    }

    [Fact]
    public async Task AttachIntent_FencesSessions_AndEitherCompletionOrAbortAdvancesEpoch()
    {
        var store = new InMemoryAuthorityLifecycleStore();
        await store.MarkAuthorityPresentAsync();
        var issued = await store.GetAsync();
        await store.BeginDetachAsync();
        var detached = await store.CompleteDetachAsync();

        var attaching = await store.BeginAttachAsync();
        Assert.Equal(AuthorityLifecycleState.AttachInProgress, attaching.State);
        Assert.Equal(detached.Epoch, attaching.Epoch);
        Assert.False(AuthorityLifecycleSessionFence.IsCurrent(attaching, issued.Epoch));
        Assert.Equal(attaching, await store.BeginAttachAsync());

        var attached = await store.CompleteAttachAsync();
        Assert.Equal(AuthorityLifecycleState.AuthorityPresent, attached.State);
        Assert.Equal(detached.Epoch + 1, attached.Epoch);
        Assert.False(AuthorityLifecycleSessionFence.IsCurrent(attached, issued.Epoch));

        await store.BeginDetachAsync();
        var detachedAgain = await store.CompleteDetachAsync();
        await store.BeginAttachAsync();
        var aborted = await store.AbortAttachAsync();
        Assert.Equal(AuthorityLifecycleState.DeliberatelyDetached, aborted.State);
        Assert.Equal(detachedAgain.Epoch + 1, aborted.Epoch);
        Assert.Equal(aborted, await store.AbortAttachAsync());
    }
}
