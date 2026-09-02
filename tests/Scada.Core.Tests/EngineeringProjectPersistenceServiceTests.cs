using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.VisualAssets;

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
    public async Task SaveCurrentAsync_DerivesSnapshotMetadataFromPersistedJson()
    {
        var tags = new InMemoryTagRegistry();
        tags.Register(TagDefinition.Create("Pressure", "Plant.Pressure", TagDataType.Double));
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new TrackingEngineeringExchangeService(
            new EngineeringExchangeService(tags, alarms));
        var service = new EngineeringProjectPersistenceService(
            exchange,
            new FakeEngineeringProjectStore());

        var snapshot = await service.SaveCurrentAsync("plant-a", "Plant A");

        Assert.Equal(1, exchange.ExportJsonCalls);
        Assert.Equal(1, exchange.ParseJsonCalls);
        Assert.Equal(0, exchange.ExportPackageCalls);
        Assert.Single(exchange.ParseJson(snapshot.EngineeringJson).Tags);
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
    public async Task Lifecycle_TracksWorkingPublishedAndActiveRevisionsIndependently()
    {
        var tags = new InMemoryTagRegistry();
        tags.Register(TagDefinition.Create("Pressure", "Plant.Pressure", TagDataType.Double));
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(tags, alarms);
        var store = new FakeEngineeringProjectStore();
        var service = new EngineeringProjectPersistenceService(exchange, store);

        var empty = await service.GetLifecycleAsync("plant-a");
        Assert.Equal(EngineeringProjectLifecycleStatus.Empty, empty.Status);
        Assert.Equal(EngineeringRuntimeStatus.Inactive, empty.RuntimeStatus);

        var revision1 = await service.SaveCurrentAsync("plant-a", "Plant A", "engineer");
        var draft = await service.GetLifecycleAsync("plant-a");
        Assert.Equal(EngineeringProjectLifecycleStatus.Draft, draft.Status);
        Assert.Equal(EngineeringRuntimeStatus.Inactive, draft.RuntimeStatus);
        Assert.Equal(revision1.Revision, draft.WorkingRevision);
        Assert.Null(draft.PublishedRevision);
        Assert.Null(draft.ActiveRevision);

        var published1 = await service.PublishRevisionAsync("plant-a", revision1.Revision, "supervisor");
        Assert.NotNull(published1);
        Assert.True(published1!.Published);

        var pendingActivation1 = await service.GetLifecycleAsync("plant-a");
        Assert.Equal(EngineeringProjectLifecycleStatus.Published, pendingActivation1.Status);
        Assert.Equal(EngineeringRuntimeStatus.ActivationPending, pendingActivation1.RuntimeStatus);
        Assert.Equal(revision1.Revision, pendingActivation1.PublishedRevision);
        Assert.Null(pendingActivation1.ActiveRevision);

        var activation1 = await service.RecordActivationAsync("plant-a", revision1.Revision, "operator-a");
        Assert.NotNull(activation1);
        var active1 = await service.GetLifecycleAsync("plant-a");
        Assert.Equal(EngineeringRuntimeStatus.Active, active1.RuntimeStatus);
        Assert.Equal(revision1.Revision, active1.ActiveRevision);
        Assert.Equal("operator-a", active1.ActivatedBy);

        var revision2 = await service.SaveCurrentAsync("plant-a", "Plant A", "engineer");
        var workingChanges = await service.GetLifecycleAsync("plant-a");
        Assert.Equal(EngineeringProjectLifecycleStatus.ChangesPending, workingChanges.Status);
        Assert.Equal(EngineeringRuntimeStatus.Active, workingChanges.RuntimeStatus);
        Assert.Equal(revision2.Revision, workingChanges.WorkingRevision);
        Assert.Equal(revision1.Revision, workingChanges.PublishedRevision);
        Assert.Equal(revision1.Revision, workingChanges.ActiveRevision);

        var published2 = await service.PublishRevisionAsync("plant-a", revision2.Revision, "supervisor");
        Assert.NotNull(published2);
        Assert.True(published2!.Published);

        var pendingActivation2 = await service.GetLifecycleAsync("plant-a");
        Assert.Equal(EngineeringProjectLifecycleStatus.Published, pendingActivation2.Status);
        Assert.Equal(EngineeringRuntimeStatus.ActivationPending, pendingActivation2.RuntimeStatus);
        Assert.Equal(revision2.Revision, pendingActivation2.PublishedRevision);
        Assert.Equal(revision1.Revision, pendingActivation2.ActiveRevision);

        var activation2 = await service.RecordActivationAsync("plant-a", revision2.Revision, "operator-b");
        Assert.NotNull(activation2);
        var active2 = await service.GetLifecycleAsync("plant-a");
        Assert.Equal(EngineeringRuntimeStatus.Active, active2.RuntimeStatus);
        Assert.Equal(revision2.Revision, active2.ActiveRevision);
        Assert.Equal("operator-b", active2.ActivatedBy);
    }

    [Fact]
    public async Task RecordActivation_RejectsRevisionThatIsNotCurrentlyPublished()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(tags, alarms);
        var store = new FakeEngineeringProjectStore();
        var service = new EngineeringProjectPersistenceService(exchange, store);

        var revision1 = await service.SaveCurrentAsync("plant-a", "Plant A");
        var revision2 = await service.SaveCurrentAsync("plant-a", "Plant A");
        await service.PublishRevisionAsync("plant-a", revision1.Revision, "supervisor");

        Assert.Null(await service.RecordActivationAsync("plant-a", revision2.Revision, "operator"));
        Assert.Null(await service.GetActivationAsync("plant-a"));
    }

    [Fact]
    public async Task LoadPublishedAsync_ReturnsPublishedSnapshotNotLatestDraft()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(tags, alarms);
        var store = new FakeEngineeringProjectStore();
        var service = new EngineeringProjectPersistenceService(exchange, store);

        var first = await service.SaveCurrentAsync("plant-a", "Plant A");
        await service.PublishRevisionAsync("plant-a", first.Revision, "supervisor");
        var second = await service.SaveCurrentAsync("plant-a", "Plant A");

        var published = await service.LoadPublishedAsync("plant-a");
        var latest = await service.LoadLatestAsync("plant-a");

        Assert.NotNull(published);
        Assert.NotNull(latest);
        Assert.Equal(first.Revision, published!.Revision);
        Assert.Equal(second.Revision, latest!.Revision);
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
        Assert.Null(await service.RecordActivationAsync("missing", 99, "operator"));
        Assert.Null(await service.LoadPublishedAsync("missing"));
    }

    private sealed class TrackingEngineeringExchangeService(IEngineeringExchangeService inner)
        : IEngineeringExchangeService
    {
        public int ExportPackageCalls { get; private set; }
        public int ExportJsonCalls { get; private set; }
        public int ParseJsonCalls { get; private set; }

        public EngineeringPackage ExportPackage()
        {
            ExportPackageCalls++;
            return inner.ExportPackage();
        }

        public string ExportJson(bool indented = true)
        {
            ExportJsonCalls++;
            return inner.ExportJson(indented);
        }

        public string ExportTagsCsv() => inner.ExportTagsCsv();
        public string ExportAlarmsCsv() => inner.ExportAlarmsCsv();
        public string ExportDataSourcesCsv() => inner.ExportDataSourcesCsv();

        public EngineeringPackage ParseJson(string json)
        {
            ParseJsonCalls++;
            return inner.ParseJson(json);
        }

        public EngineeringPackage ParseTagsCsv(string csv) => inner.ParseTagsCsv(csv);
        public EngineeringPackage ParseAlarmsCsv(string csv) => inner.ParseAlarmsCsv(csv);
        public EngineeringPackage ParseDataSourcesCsv(string csv) => inner.ParseDataSourcesCsv(csv);
        public ImportPreview Preview(EngineeringPackage package, ImportMode mode) => inner.Preview(package, mode);

        public ImportPreview Preview(
            EngineeringPackage package,
            ImportMode mode,
            EngineeringImportContext? context) =>
            inner.Preview(package, mode, context);

        public ImportResult Apply(EngineeringPackage package, ImportMode mode) => inner.Apply(package, mode);

        public ImportResult Apply(
            EngineeringPackage package,
            ImportMode mode,
            EngineeringImportContext? context) =>
            inner.Apply(package, mode, context);
    }

    private sealed class FakeEngineeringProjectStore : IEngineeringProjectStore
    {
        private readonly List<EngineeringProjectSnapshot> _items = new();
        private readonly Dictionary<string, EngineeringProjectPublication> _publications =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EngineeringProjectActivation> _activations =
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

        public Task<EngineeringProjectActivation?> GetActivationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_activations.GetValueOrDefault(projectKey));

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey,
            long revision,
            string? activatedBy = null,
            CancellationToken cancellationToken = default)
        {
            if (!_publications.TryGetValue(projectKey, out var publication) || publication.PublishedRevision != revision)
                return Task.FromResult<EngineeringProjectActivation?>(null);

            var activation = new EngineeringProjectActivation(
                projectKey,
                revision,
                DateTimeOffset.UtcNow,
                activatedBy);
            _activations[projectKey] = activation;
            return Task.FromResult<EngineeringProjectActivation?>(activation);
        }

        public void Seed(EngineeringProjectSnapshot snapshot)
        {
            _items.Add(snapshot);
            _nextRevision = Math.Max(_nextRevision, snapshot.Revision + 1);
        }
    }
}
