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
using Scada.Engineering.Security;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class GatewayEngineeringPersistenceTests
{
    [Fact]
    public async Task RevisionPersistence_PreservesAndRestoresGatewayRoutes()
    {
        var sourceTags = new InMemoryTagRegistry();
        var source = TagDefinition.Create("Source", "Plant.Source", TagDataType.Double);
        var destination = TagDefinition.Create("Destination", "Plant.Destination", TagDataType.Double);
        sourceTags.Register(source);
        sourceTags.Register(destination);
        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var sourceGateways = new InMemoryGatewayEngineeringRegistry();
        var routeId = Guid.NewGuid();
        sourceGateways.Upsert(new GatewayRouteEngineeringDto(
            routeId,
            "plant.gateway",
            "Plant gateway",
            source.Id,
            source.Path,
            destination.Id,
            destination.Path,
            MinimumIntervalMilliseconds: 500));
        var sourceExchange = CreateExchange(sourceTags, sourceAlarms, sourceGateways);
        var store = new MemoryProjectStore();
        var sourcePersistence = new EngineeringProjectPersistenceService(sourceExchange, store);

        var snapshot = await sourcePersistence.SaveCurrentAsync("plant-a", "Plant A", "engineer");
        var storedPackage = sourceExchange.ParseJson(snapshot.EngineeringJson);

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, snapshot.EngineeringSchemaVersion);
        var storedRoute = Assert.Single(storedPackage.Gateways!);
        Assert.Equal(routeId, storedRoute.Id);

        var targetTags = new InMemoryTagRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var targetGateways = new InMemoryGatewayEngineeringRegistry();
        var targetExchange = CreateExchange(targetTags, targetAlarms, targetGateways);
        var targetPersistence = new EngineeringProjectPersistenceService(targetExchange, store);

        var preview = await targetPersistence.PreviewRevisionAsync("plant-a", snapshot.Revision, ImportMode.CreateAndUpdate);
        var result = await targetPersistence.ApplyRevisionAsync("plant-a", snapshot.Revision, ImportMode.CreateAndUpdate);

        Assert.NotNull(preview);
        Assert.True(preview!.Preview.CanApply);
        Assert.NotNull(result);
        Assert.Empty(result!.Issues);
        var restored = Assert.Single(targetGateways.Snapshot());
        Assert.Equal(routeId, restored.Id);
        Assert.Equal(source.Id, restored.SourceTagId);
        Assert.Equal(destination.Id, restored.DestinationTagId);
    }

    private static EngineeringExchangeService CreateExchange(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IGatewayEngineeringRegistry gateways) =>
        new(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry(),
            gateways);

    private sealed class MemoryProjectStore : IEngineeringProjectStore
    {
        private readonly List<EngineeringProjectSnapshot> _snapshots = new();
        private long _nextRevision = 1;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default) =>
            SaveDerivedAsync(projectKey, projectName, engineeringSchema, engineeringSchemaVersion, engineeringJson, null, savedBy, cancellationToken);

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
            var snapshot = new EngineeringProjectSnapshot(
                _nextRevision++,
                projectKey,
                projectName,
                engineeringSchema,
                engineeringSchemaVersion,
                DateTimeOffset.UtcNow,
                engineeringJson,
                savedBy,
                basedOnRevision);
            _snapshots.Add(snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_snapshots
                .Where(x => x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Revision)
                .FirstOrDefault());

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_snapshots.FirstOrDefault(x =>
                x.Revision == revision && x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(_snapshots
                .Where(x => x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Revision)
                .Take(limit)
                .ToArray());

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectPublication?>(null);

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectPublication?>(null);

        public Task<EngineeringProjectActivation?> GetActivationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectActivation?>(null);

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey,
            long revision,
            string? activatedBy = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectActivation?>(null);
    }
}