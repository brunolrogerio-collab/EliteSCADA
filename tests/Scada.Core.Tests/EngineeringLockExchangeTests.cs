using System.IO.Compression;
using System.Text;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Security;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class EngineeringLockExchangeTests
{
    [Fact]
    public void LegacyV16Json_WithoutEngineeringLock_ParsesAsUnconfiguredUnlocked()
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = CreateExchange(new InMemoryTagRegistry(), alarms, new InMemoryEngineeringLockRegistry());
        var json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 16,
          "exportedAt": "2026-09-05T00:00:00Z",
          "tags": [],
          "alarms": []
        }
        """;

        var package = exchange.ParseJson(json);

        Assert.NotNull(package.EngineeringLock);
        Assert.False(package.EngineeringLock!.Locked);
        Assert.Null(package.EngineeringLock.Verifier);
    }

    [Fact]
    public void CanonicalJson_RoundTripsConfiguredLockedState_WithoutPlaintextSecret()
    {
        const string secret = "C25-canonical-roundtrip-secret";
        var registry = new InMemoryEngineeringLockRegistry();
        var secrets = new EngineeringLockSecretService();
        registry.Replace(secrets.Configure(secret, locked: true));
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = CreateExchange(new InMemoryTagRegistry(), alarms, registry);

        var json = exchange.ExportJson(indented: false);
        var parsed = exchange.ParseJson(json);

        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        Assert.NotNull(parsed.EngineeringLock?.Verifier);
        Assert.True(parsed.EngineeringLock!.Locked);
        Assert.True(secrets.Verify(parsed.EngineeringLock, secret));
    }

    [Fact]
    public void PartialCsvPackages_PreserveCurrentEngineeringLockState()
    {
        var registry = new InMemoryEngineeringLockRegistry();
        var configured = new EngineeringLockSecretService().Configure("csv-preserve-secret", locked: true);
        registry.Replace(configured);
        var tags = new InMemoryTagRegistry();
        tags.Register(TagDefinition.Create("Pressure", "Plant.Pressure", TagDataType.Double));
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = CreateExchange(tags, alarms, registry);

        var tagsPackage = exchange.ParseTagsCsv(exchange.ExportTagsCsv());
        var alarmsPackage = exchange.ParseAlarmsCsv(exchange.ExportAlarmsCsv());
        var dataSourcesPackage = exchange.ParseDataSourcesCsv(exchange.ExportDataSourcesCsv());

        Assert.Equal(configured, tagsPackage.EngineeringLock);
        Assert.Equal(configured, alarmsPackage.EngineeringLock);
        Assert.Equal(configured, dataSourcesPackage.EngineeringLock);
    }

    [Fact]
    public void ProjectPackage_InspectAndApply_RoundTripsEngineeringLockWithoutPlaintextSecret()
    {
        const string secret = "C25-package-roundtrip-secret";
        var sourceLock = new InMemoryEngineeringLockRegistry();
        var secrets = new EngineeringLockSecretService();
        sourceLock.Replace(secrets.Configure(secret, locked: true));
        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var source = new ProjectPackageService(
            CreateExchange(new InMemoryTagRegistry(), sourceAlarms, sourceLock));

        var bytes = source.Export("locked-plant", "Locked Plant");
        var inspection = source.Inspect(bytes);

        Assert.True(inspection.Engineering.EngineeringLock!.Locked);
        Assert.True(secrets.Verify(inspection.Engineering.EngineeringLock, secret));
        Assert.DoesNotContain(secret, ReadEngineeringJson(bytes), StringComparison.Ordinal);

        var targetLock = new InMemoryEngineeringLockRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var target = new ProjectPackageService(
            CreateExchange(new InMemoryTagRegistry(), targetAlarms, targetLock));

        var preview = target.Preview(bytes, ImportMode.CreateAndUpdate);
        var result = target.Apply(bytes, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);
        Assert.Equal(inspection.Engineering.EngineeringLock, targetLock.Snapshot());
        Assert.True(secrets.Verify(targetLock.Snapshot(), secret));
    }

    [Fact]
    public void Preview_RejectsMalformedLockBeforeAnyEngineeringMutation()
    {
        var registry = new InMemoryEngineeringLockRegistry();
        var original = new EngineeringLockSecretService().Configure("stable-secret", locked: false);
        registry.Replace(original);
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = CreateExchange(tags, alarms, registry);
        var malformed = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(null, "Pressure", "Plant.Pressure", TagDataType.Double)],
            Array.Empty<AlarmEngineeringDto>(),
            EngineeringLock: new EngineeringLockEngineeringDto(Locked: true));

        Assert.Throws<InvalidDataException>(() =>
            exchange.Preview(malformed, ImportMode.CreateAndUpdate));
        Assert.False(tags.TryGetByPath("Plant.Pressure", out _));
        Assert.Equal(original, registry.Snapshot());
    }

    [Fact]
    public async Task RevisionLifecycle_PreservesImmutableEngineeringLockThroughPublishedAndActive()
    {
        const string secret = "C25-revision-lifecycle-secret";
        var registry = new InMemoryEngineeringLockRegistry();
        var secrets = new EngineeringLockSecretService();
        var locked = secrets.Configure(secret, locked: true);
        registry.Replace(locked);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = CreateExchange(new InMemoryTagRegistry(), alarms, registry);
        var store = new LockLifecycleStore();
        var persistence = new EngineeringProjectPersistenceService(exchange, store);

        var revision = await persistence.SaveCurrentAsync("plant-a", "Plant A", "engineer");
        var publication = await persistence.PublishRevisionAsync("plant-a", revision.Revision, "supervisor");
        var activation = await persistence.RecordActivationAsync("plant-a", revision.Revision, "operator");

        Assert.True(publication!.Published);
        Assert.NotNull(activation);

        registry.Replace(EngineeringLockContract.UnconfiguredUnlocked);
        var active = await persistence.LoadActiveAsync("plant-a");
        Assert.NotNull(active);
        var activePackage = exchange.ParseJson(active!.EngineeringJson);

        Assert.Equal(locked, activePackage.EngineeringLock);
        Assert.True(secrets.Verify(activePackage.EngineeringLock, secret));
        Assert.False(registry.Snapshot().Locked);
        Assert.Null(registry.Snapshot().Verifier);
    }

    private static EngineeringExchangeService CreateExchange(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IEngineeringLockRegistry engineeringLock) =>
        new(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry(),
            new InMemoryGatewayEngineeringRegistry(),
            engineeringLock: engineeringLock);

    private static string ReadEngineeringJson(byte[] packageBytes)
    {
        using var input = new MemoryStream(packageBytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        using var reader = new StreamReader(
            archive.GetEntry(ProjectPackageService.EngineeringPath)!.Open(),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false);
        return reader.ReadToEnd();
    }

    private sealed class LockLifecycleStore : IEngineeringProjectStore
    {
        private readonly List<EngineeringProjectSnapshot> _revisions = [];
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
            _revisions.Add(snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_revisions
                .Where(x => string.Equals(x.ProjectKey, projectKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Revision)
                .FirstOrDefault());

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_revisions.FirstOrDefault(x =>
                x.Revision == revision &&
                string.Equals(x.ProjectKey, projectKey, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(_revisions
                .Where(x => string.Equals(x.ProjectKey, projectKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Revision)
                .Take(limit)
                .ToArray());

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_publications.GetValueOrDefault(projectKey));

        public async Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default)
        {
            if (await LoadRevisionAsync(projectKey, revision, cancellationToken) is null) return null;
            var publication = new EngineeringProjectPublication(
                projectKey,
                revision,
                DateTimeOffset.UtcNow,
                publishedBy);
            _publications[projectKey] = publication;
            return publication;
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
            if (!_publications.TryGetValue(projectKey, out var publication) ||
                publication.PublishedRevision != revision)
                return Task.FromResult<EngineeringProjectActivation?>(null);

            var activation = new EngineeringProjectActivation(
                projectKey,
                revision,
                DateTimeOffset.UtcNow,
                activatedBy);
            _activations[projectKey] = activation;
            return Task.FromResult<EngineeringProjectActivation?>(activation);
        }
    }
}