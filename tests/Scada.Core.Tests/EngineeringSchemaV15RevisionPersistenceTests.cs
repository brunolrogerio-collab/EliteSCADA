using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;

namespace Scada.Core.Tests;

public sealed class EngineeringSchemaV15RevisionPersistenceTests
{
    [Fact]
    public async Task ImmutableRevision_SavePreviewApply_RoundTripsCommunicationBinding()
    {
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            "modbus.tcp.tag",
            1,
            "40001",
            new Dictionary<string, string> { ["function"] = "holding-registers" },
            new TagPhysicalValueTransform(ByteSwap: true, WordSwap: true));

        var sourceTags = new InMemoryTagRegistry();
        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var sourceDataSources = new InMemoryDataSourceEngineeringRegistry();
        sourceDataSources.Upsert(new DataSourceEngineeringDto(
            null,
            "plant.modbus01",
            "PLC principal",
            "modbus.tcp",
            Settings: new Dictionary<string, string> { ["host"] = "10.10.0.10" }));
        sourceTags.Register(TagDefinition.Create(
            "Pressure",
            "Plant.P01.Pressure",
            TagDataType.Double,
            source: "plant.modbus01",
            metadata: new Dictionary<string, string> { ["address"] = binding.PortableAddress },
            communicationBinding: binding));
        var sourceExchange = new EngineeringExchangeService(sourceTags, sourceAlarms, sourceDataSources);
        var store = new SingleRevisionStore();
        var sourcePersistence = new EngineeringProjectPersistenceService(sourceExchange, store);

        var snapshot = await sourcePersistence.SaveCurrentAsync("plant-v15", "Plant v15", "engineer");
        var storedPackage = sourceExchange.ParseJson(snapshot.EngineeringJson);

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, snapshot.EngineeringSchemaVersion);
        var storedTag = Assert.Single(storedPackage.Tags);
        Assert.NotNull(storedTag.CommunicationBinding);
        Assert.Equal("modbus.tcp.tag", storedTag.CommunicationBinding!.SchemaId);
        Assert.Equal("40001", storedTag.CommunicationBinding.PortableAddress);
        Assert.True(storedTag.CommunicationBinding.ValueTransform!.ByteSwap);
        Assert.True(storedTag.CommunicationBinding.ValueTransform.WordSwap);

        var targetTags = new InMemoryTagRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var targetExchange = new EngineeringExchangeService(
            targetTags,
            targetAlarms,
            new InMemoryDataSourceEngineeringRegistry());
        var targetPersistence = new EngineeringProjectPersistenceService(targetExchange, store);

        var preview = await targetPersistence.PreviewRevisionAsync(
            "plant-v15",
            snapshot.Revision,
            ImportMode.CreateAndUpdate);
        var result = await targetPersistence.ApplyRevisionAsync(
            "plant-v15",
            snapshot.Revision,
            ImportMode.CreateAndUpdate);

        Assert.NotNull(preview);
        Assert.True(preview!.Preview.CanApply);
        Assert.NotNull(result);
        Assert.Empty(result!.Issues);
        Assert.True(targetTags.TryGetByPath("Plant.P01.Pressure", out var restored));
        Assert.NotNull(restored!.CommunicationBinding);
        Assert.Equal("modbus.tcp.tag", restored.CommunicationBinding!.SchemaId);
        Assert.Equal("40001", restored.CommunicationBinding.PortableAddress);
        Assert.Equal("holding-registers", restored.CommunicationBinding.EffectiveSettings["function"]);
        Assert.True(restored.CommunicationBinding.ValueTransform!.ByteSwap);
        Assert.True(restored.CommunicationBinding.ValueTransform.WordSwap);
    }

    private sealed class SingleRevisionStore : IEngineeringProjectStore
    {
        private EngineeringProjectSnapshot? _snapshot;

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
            _snapshot = new EngineeringProjectSnapshot(
                1,
                projectKey,
                projectName,
                engineeringSchema,
                engineeringSchemaVersion,
                DateTimeOffset.UtcNow,
                engineeringJson,
                savedBy);
            return Task.FromResult(_snapshot);
        }

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_snapshot is not null &&
                            _snapshot.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase)
                ? _snapshot
                : null);

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_snapshot is not null &&
                            _snapshot.Revision == revision &&
                            _snapshot.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase)
                ? _snapshot
                : null);

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(
                _snapshot is not null && _snapshot.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase)
                    ? new[] { _snapshot }
                    : Array.Empty<EngineeringProjectSnapshot>());

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
