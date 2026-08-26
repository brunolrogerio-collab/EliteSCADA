using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;

namespace Scada.Core.Tests;

public sealed class EngineeringProjectPersistenceServiceTests
{
    [Fact]
    public async Task SaveCurrentAsync_StoresCanonicalCurrentEngineeringPackage()
    {
        var tags = new InMemoryTagRegistry();
        tags.Register(TagDefinition.Create(
            "Pressure",
            "Plant.P01.Pressure",
            TagDataType.Double,
            engineeringUnit: "bar"));

        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(tags, alarms);
        var store = new FakeEngineeringProjectStore();
        var service = new EngineeringProjectPersistenceService(exchange, store);

        var snapshot = await service.SaveCurrentAsync(
            "plant-a",
            "Plant A",
            "engineering-user");

        Assert.Equal(EngineeringExchangeService.CurrentSchema, snapshot.EngineeringSchema);
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, snapshot.EngineeringSchemaVersion);
        Assert.Equal("engineering-user", snapshot.SavedBy);

        var parsed = exchange.ParseJson(snapshot.EngineeringJson);
        Assert.Contains(parsed.Tags, x => x.Path == "Plant.P01.Pressure");
    }

    [Fact]
    public async Task PreviewAndApplyRevision_RestoresEngineeringIntoEmptyRuntime()
    {
        var sourceTags = new InMemoryTagRegistry();
        sourceTags.Register(TagDefinition.Create(
            "Pressure",
            "Plant.P01.Pressure",
            TagDataType.Double,
            engineeringUnit: "bar"));
        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var sourceExchange = new EngineeringExchangeService(sourceTags, sourceAlarms);

        var package = sourceExchange.ExportPackage();
        var store = new FakeEngineeringProjectStore();
        var saved = await store.SaveAsync(
            "plant-a",
            "Plant A",
            package.Schema,
            package.SchemaVersion,
            sourceExchange.ExportJson(indented: false));

        var targetTags = new InMemoryTagRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var targetExchange = new EngineeringExchangeService(targetTags, targetAlarms);
        var service = new EngineeringProjectPersistenceService(targetExchange, store);

        var preview = await service.PreviewRevisionAsync(
            "plant-a",
            saved.Revision,
            ImportMode.CreateAndUpdate);
        var result = await service.ApplyRevisionAsync(
            "plant-a",
            saved.Revision,
            ImportMode.CreateAndUpdate);

        Assert.NotNull(preview);
        Assert.True(preview!.Preview.CanApply);
        Assert.Equal(1, preview.Preview.CreateCount);
        Assert.NotNull(result);
        Assert.Equal(1, result!.Created);
        Assert.True(targetTags.TryGetByPath("Plant.P01.Pressure", out var restored));
        Assert.Equal("bar", restored!.EngineeringUnit);
    }

    [Fact]
    public async Task Lifecycle_TracksDraftPublishedAndChangesPending()
    {
        var tags = new InMemoryTagRegistry();
        tags.Register(TagDefinition.Create("Pressure", "Plant.Pressure", TagDataType.Double));
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(tags, alarms);
        var store = new FakeEngineeringProjectStore();
        var service = new EngineeringProjectPersistenceService(exchange, store);

        var empty = await service.GetLifecycleAsync("plant-a");
        Assert.Equal(EngineeringProjectLifecycleStatus.Empty, empty.Status);

        var revision1 = await service.SaveCurrentAsync("plant-a", "Plant A", "engineer");
        var draft = await service.GetLifecycleAsync("plant-a");
        Assert.Equal(EngineeringProjectLifecycleStatus.Draft, draft.Status);
        Assert.Equal(revision1.Revision, draft.WorkingRevision);
        Assert.Null(draft.PublishedRevision);

        var published = await service.PublishRevisionAsync("plant-a", revision1.Revision, "supervisor");
        Assert.NotNull(published);
        Assert.True(published!.Published);

        var current = await service.GetLifecycleAsync("plant-a");
        Assert.Equal(EngineeringProjectLifecycleStatus.Published, current.Status);
        Assert.Equal(revision1.Revision, current.PublishedRevision);
        Assert.Equal("supervisor", current.PublishedBy);

        var revision2 = await service.SaveCurrentAsync("plant-a", "Plant A", "engineer");
        var pending = await service.GetLifecycleAsync("plant-a");
        Assert.Equal(EngineeringProjectLifecycleStatus.ChangesPending, pending.Status);
        Assert.Equal(revision2.Revision, pending.WorkingRevision);
        Assert.Equal(revision1.Revision, pending.PublishedRevision);
    }

    [Fact]
    public async Task PublishRevision_DoesNotPublishPackageThatFailsPreview()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(tags, alarms);
        var store = new FakeEngineeringProjectStore();

        var invalid = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(null, "Pressure", "Plant.Pressure", TagDataType.Double, Source: "missing.datasource")
            },
            Array.Empty<AlarmEngineeringDto>());

        var json = System.Text.Json.JsonSerializer.Serialize(invalid, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) }
        });

        var snapshot = await store.SaveAsync(
            "plant-a",
            "Plant A",
            invalid.Schema,
            invalid.SchemaVersion,
            json);

        var service = new EngineeringProjectPersistenceService(exchange, store);
        var result = await service.PublishRevisionAsync("plant-a", snapshot.Revision, "supervisor");

        Assert.NotNull(result);
        Assert.False(result!.Published);
        Assert.False(result.Preview.CanApply);
        Assert.Null(await store.GetPublicationAsync("plant-a"));
    }

    [Fact]
    public async Task PreviewRevision_RejectsStoredMetadataThatDoesNotMatchPayload()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(tags, alarms);
        var store = new FakeEngineeringProjectStore();
        var validJson = exchange.ExportJson(indented: false);

        store.Seed(new EngineeringProjectSnapshot(
            12,
            "plant-a",
            "Plant A",
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion - 1,
            DateTimeOffset.UtcNow,
            validJson));

        var service = new EngineeringProjectPersistenceService(exchange, store);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.PreviewRevisionAsync("plant-a", 12, ImportMode.CreateAndUpdate));
    }

    [Fact]
    public async Task MissingRevision_ReturnsNullInsteadOfInventingState()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(tags, alarms);
        var service = new EngineeringProjectPersistenceService(exchange, new FakeEngineeringProjectStore());

        Assert.Null(await service.PreviewRevisionAsync("missing", 99, ImportMode.CreateAndUpdate));
        Assert.Null(await service.ApplyRevisionAsync("missing", 99, ImportMode.CreateAndUpdate));
        Assert.Null(await service.PublishRevisionAsync("missing", 99, "supervisor"));
    }

    private sealed class FakeEngineeringProjectStore : IEngineeringProjectStore
    {
        private readonly List<EngineeringProjectSnapshot> _items = new();
        private readonly Dictionary<string, EngineeringProjectPublication> _publications =
            new(StringComparer.OrdinalIgnoreCase);
        private long _nextRevision = 1;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default)
        {
            var snapshot = new EngineeringProjectSnapshot(
                _nextRevision++,
                projectKey,
                projectName,
                engineeringSchema,
                engineeringSchemaVersion,
                DateTimeOffset.UtcNow,
                engineeringJson,
                savedBy);
            _items.Add(snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items
                .Where(x => x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Revision)
                .FirstOrDefault());

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(x =>
                x.Revision == revision &&
                x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(_items
                .Where(x => x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Revision)
                .Take(limit)
                .ToArray());

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_publications.GetValueOrDefault(projectKey));

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default)
        {
            var snapshot = _items.FirstOrDefault(x =>
                x.Revision == revision &&
                x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase));
            if (snapshot is null)
                return Task.FromResult<EngineeringProjectPublication?>(null);

            var publication = new EngineeringProjectPublication(
                projectKey,
                revision,
                DateTimeOffset.UtcNow,
                publishedBy);
            _publications[projectKey] = publication;
            return Task.FromResult<EngineeringProjectPublication?>(publication);
        }

        public void Seed(EngineeringProjectSnapshot snapshot)
        {
            _items.Add(snapshot);
            _nextRevision = Math.Max(_nextRevision, snapshot.Revision + 1);
        }
    }
}
