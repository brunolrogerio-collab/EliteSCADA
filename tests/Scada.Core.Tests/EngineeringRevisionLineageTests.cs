using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;

namespace Scada.Core.Tests;

public sealed class EngineeringRevisionLineageTests
{
    [Fact]
    public async Task SaveCurrentDerivedAsync_PreservesBaseRevision()
    {
        var tags = new InMemoryTagRegistry();
        tags.Register(TagDefinition.Create("Pressure", "Plant.Pressure", TagDataType.Double));
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(tags, alarms);
        var store = new LineageStore();
        var service = new EngineeringProjectPersistenceService(exchange, store);

        var saved = await service.SaveCurrentDerivedAsync(
            "plant-a",
            "Plant A",
            42,
            "engineer");

        Assert.Equal(42, saved.BasedOnRevision);
        Assert.Equal(42, store.LastBasedOnRevision);
        Assert.Equal("plant-a", saved.ProjectKey);
        Assert.Equal("engineer", saved.SavedBy);
    }

    private sealed class LineageStore : IEngineeringProjectStore
    {
        public long? LastBasedOnRevision { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default) =>
            SaveDerivedAsync(
                projectKey,
                projectName,
                engineeringSchema,
                engineeringSchemaVersion,
                engineeringJson,
                null,
                savedBy,
                cancellationToken);

        public Task<EngineeringProjectSnapshot> SaveDerivedAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            long? basedOnRevision,
            string? savedBy = null,
            CancellationToken cancellationToken = default)
        {
            LastBasedOnRevision = basedOnRevision;
            return Task.FromResult(new EngineeringProjectSnapshot(
                100,
                projectKey,
                projectName,
                engineeringSchema,
                engineeringSchemaVersion,
                DateTimeOffset.UtcNow,
                engineeringJson,
                savedBy,
                basedOnRevision));
        }

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(string projectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(string projectKey, long revision, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(string projectKey, int limit = 50, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectPublication?> GetPublicationAsync(string projectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(string projectKey, long revision, string? publishedBy = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectActivation?> GetActivationAsync(string projectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectActivation?> RecordActivationAsync(string projectKey, long revision, string? activatedBy = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
