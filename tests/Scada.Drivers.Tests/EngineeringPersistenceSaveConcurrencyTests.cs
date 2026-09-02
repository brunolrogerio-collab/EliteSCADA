using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;

namespace Scada.Drivers.Tests;

public sealed class EngineeringPersistenceSaveConcurrencyTests
{
    [Fact]
    public async Task SaveCurrentAsync_HoldsWorkspaceMutationLeaseUntilSaveIsAccepted()
    {
        using var workspace = new EngineeringWorkspace();
        var exchange = new EngineeringExchangeService(workspace.Tags, workspace.Alarms);
        var store = new BlockingEngineeringProjectStore();
        var persistence = new EngineeringProjectPersistenceService(exchange, store);
        var save = EngineeringPersistenceApi.SaveCurrentAsync(
            "plant-a",
            new EngineeringSaveRequest("Plant A", "engineer"),
            persistence,
            workspace);

        await store.SaveStarted.WaitAsync(TimeSpan.FromSeconds(1));
        var competingMutation = workspace.AcquireMutationAsync().AsTask();
        await Task.Yield();
        Assert.False(competingMutation.IsCompleted);

        store.AllowSaveToComplete();
        var snapshot = await save.WaitAsync(TimeSpan.FromSeconds(1));
        await using var mutation = await competingMutation.WaitAsync(TimeSpan.FromSeconds(1));

        var descriptor = workspace.Describe();
        Assert.Equal(snapshot.Revision, descriptor.BaseRevision);
        Assert.Equal("plant-a", descriptor.ProjectKey);
        Assert.False(descriptor.IsDirty);
    }

    private sealed class BlockingEngineeringProjectStore : IEngineeringProjectStore
    {
        private readonly TaskCompletionSource _allowSave =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _saveStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SaveStarted => _saveStarted.Task;

        public void AllowSaveToComplete() => _allowSave.TrySetResult();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default)
        {
            _saveStarted.TrySetResult();
            await _allowSave.Task.WaitAsync(cancellationToken);
            return new EngineeringProjectSnapshot(
                1,
                projectKey,
                projectName,
                engineeringSchema,
                engineeringSchemaVersion,
                DateTimeOffset.UtcNow,
                engineeringJson,
                savedBy);
        }

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectActivation?> GetActivationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey,
            long revision,
            string? activatedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
