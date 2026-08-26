using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class EngineeringWorkspaceDirtyStateTests
{
    [Fact]
    public void Workspace_TracksChangesAndDoesNotLoseConcurrentDirtyStateOnSave()
    {
        using var workspace = new EngineeringWorkspace();
        var initial = workspace.Describe();

        Assert.False(initial.IsDirty);
        Assert.Equal(0, initial.ChangeVersion);
        Assert.Null(initial.BaseRevision);

        var tag = workspace.Tags.Snapshot().First();
        workspace.Tags.Upsert(tag with { Description = "first edit" });

        var edited = workspace.Describe();
        Assert.True(edited.IsDirty);
        Assert.True(edited.ChangeVersion > initial.ChangeVersion);

        var saveVersion = workspace.CaptureChangeVersion();
        workspace.Tags.Upsert(tag with { Description = "edit during save" });

        workspace.AcceptSave(
            "demo",
            "Demo Project",
            10,
            DateTimeOffset.UtcNow,
            saveVersion);

        var afterConcurrentEdit = workspace.Describe();
        Assert.Equal(10, afterConcurrentEdit.BaseRevision);
        Assert.True(afterConcurrentEdit.IsDirty);
        Assert.True(afterConcurrentEdit.ChangeVersion > saveVersion);

        var stableVersion = workspace.CaptureChangeVersion();
        workspace.AcceptSave(
            "demo",
            "Demo Project",
            11,
            DateTimeOffset.UtcNow,
            stableVersion);

        var saved = workspace.Describe();
        Assert.Equal(11, saved.BaseRevision);
        Assert.False(saved.IsDirty);
        Assert.NotNull(saved.LastSavedAtUtc);
    }
}
