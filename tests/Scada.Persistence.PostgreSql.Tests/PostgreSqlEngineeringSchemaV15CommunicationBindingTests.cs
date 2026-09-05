using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlEngineeringSchemaV15CommunicationBindingTests
{
    [Fact]
    public async Task PostgreSqlRevision_SavePreviewApply_RoundTripsCommunicationBinding()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();

        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            "modbus.tcp.tag",
            1,
            "40001",
            new Dictionary<string, string> { ["function"] = "holding-registers" },
            new TagPhysicalValueTransform(ByteSwap: true, WordSwap: true));
        var projectKey = $"binding-v15-{Guid.NewGuid():N}";

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
        var sourcePersistence = new EngineeringProjectPersistenceService(sourceExchange, store);

        var snapshot = await sourcePersistence.SaveCurrentAsync(projectKey, "Binding v15", "integration-test");
        var loaded = await store.LoadRevisionAsync(projectKey, snapshot.Revision);

        Assert.NotNull(loaded);
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, loaded!.EngineeringSchemaVersion);
        var storedPackage = sourceExchange.ParseJson(loaded.EngineeringJson);
        var storedTag = Assert.Single(storedPackage.Tags);
        Assert.NotNull(storedTag.CommunicationBinding);
        Assert.Equal("modbus.tcp.tag", storedTag.CommunicationBinding!.SchemaId);
        Assert.Equal("40001", storedTag.CommunicationBinding.PortableAddress);
        Assert.Equal("holding-registers", storedTag.CommunicationBinding.EffectiveSettings["function"]);
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
            projectKey,
            snapshot.Revision,
            ImportMode.CreateAndUpdate);
        var result = await targetPersistence.ApplyRevisionAsync(
            projectKey,
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
}
