using Scada.Api.Licensing;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Intelligence;
using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Persistence.PostgreSql;

namespace Scada.Drivers.Tests;

public sealed class AssetProcessIntelligenceDurableRestartTests
{
    [Fact]
    public async Task PostgreSqlRevisionReloadPreservesIntelligenceConfigurationButDoesNotRearmPermit()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (string.Equals(
                Environment.GetEnvironmentVariable("ELITESCADA_REQUIRE_INTELLIGENCE_POSTGRES"),
                "true",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "ELITESCADA_TEST_POSTGRES is required for the Intelligence durable-restart evidence job.");
            }

            return;
        }

        var projectKey = $"intelligence-restart-{Guid.NewGuid():N}";
        var equipmentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var signalId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var contextId = Guid.NewGuid();
        var configuration = new AssetProcessIntelligenceEngineeringDto(
            Version: 3,
            Scopes: [new(AssetProcessIntelligenceScopeKind.Equipment, equipmentId, Active: true)],
            Profiles:
            [
                new(profileId, "pump-measurements", "Pump measurements",
                    [new(signalId, "operating-mode", "state", tagId)])
            ],
            Assignments: [new(assignmentId, equipmentId, profileId)],
            ProcessStages: [
                new(stageId, "pumping", "Pumping", 0, [new(assignmentId, 0)])
            ],
            OperatingContextProfiles:
            [
                new(contextId, "normal-running", "Normal running", assignmentId, 1,
                    [new(tagId, AssetProcessIntelligenceOperatingContextPredicateKind.EnumEquals, ExactValue: "RUN")])
            ]);

        var sourceTags = new InMemoryTagRegistry();
        sourceTags.Register(TagDefinition.Create(
            "Operating mode",
            "Plant.P101.Mode",
            TagDataType.Enum,
            readOnly: true) with { Id = tagId });
        var sourceAssets = new InMemoryEngineeringAssetRegistry();
        sourceAssets.UpsertEquipment(new EquipmentEngineeringDto(equipmentId, "Plant.P101", "Pump P101"));
        var sourceIntelligence = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        sourceIntelligence.Replace(configuration);

        EngineeringProjectSnapshot saved;
        await using (var firstStore = new PostgreSqlEngineeringProjectStore(connectionString))
        {
            await firstStore.InitializeAsync();
            using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
            var sourceExchange = CreateExchange(sourceTags, sourceAlarms, sourceAssets, sourceIntelligence);
            var firstProcessPersistence = new EngineeringProjectPersistenceService(sourceExchange, firstStore);
            saved = await firstProcessPersistence.SaveCurrentAsync(projectKey, "Intelligence restart test", "stage6a-test");
        }

        var restoredTags = new InMemoryTagRegistry();
        var restoredAssets = new InMemoryEngineeringAssetRegistry();
        var restoredIntelligence = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        await using var restartedStore = new PostgreSqlEngineeringProjectStore(connectionString);
        await restartedStore.InitializeAsync();
        using var restartedAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var restartedExchange = CreateExchange(restoredTags, restartedAlarms, restoredAssets, restoredIntelligence);
        var restartedPersistence = new EngineeringProjectPersistenceService(restartedExchange, restartedStore);
        var restored = await restartedPersistence.ApplyRevisionAsync(
            projectKey,
            saved.Revision,
            ImportMode.CreateAndUpdate);

        Assert.NotNull(restored);
        Assert.Empty(restored!.Issues);
        Assert.Equal(equipmentId, Assert.Single(restoredAssets.SnapshotEquipment()).Id);
        Assert.Equal(tagId, Assert.Single(restoredTags.Snapshot()).Id);

        var restoredConfiguration = restoredIntelligence.Snapshot();
        Assert.Equal(3, restoredConfiguration.Version);
        Assert.True(Assert.Single(restoredConfiguration.Scopes!).Active);
        Assert.Equal(profileId, Assert.Single(restoredConfiguration.Profiles!).Id);
        Assert.Equal(signalId, Assert.Single(restoredConfiguration.Profiles!.Single().Signals!).Id);
        Assert.Equal(assignmentId, Assert.Single(restoredConfiguration.Assignments!).Id);
        Assert.Equal(stageId, Assert.Single(restoredConfiguration.ProcessStages!).Id);
        Assert.Equal(contextId, Assert.Single(restoredConfiguration.OperatingContextProfiles!).Id);
        Assert.DoesNotContain("sampleValue", saved.EngineeringJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("currentValue", saved.EngineeringJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("historianResult", saved.EngineeringJson, StringComparison.OrdinalIgnoreCase);

        // The canonical configuration may remember that a scope was active. A fresh
        // process-local gate must still require an explicit new permit after restart.
        var restartedGate = new AssetProcessIntelligenceExecutionGate();
        Assert.Equal(0, restartedGate.ActivePermitCount);
        Assert.False(restartedGate.TryGetToken(
            projectKey,
            saved.Revision,
            AssetProcessIntelligenceScopeKind.Equipment,
            equipmentId,
            out _));
    }

    private static EngineeringExchangeService CreateExchange(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IEngineeringAssetRegistry assets,
        IAssetProcessIntelligenceEngineeringRegistry intelligence) =>
        new(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            assets,
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new Scada.Engineering.Commands.InMemoryCommandEngineeringRegistry(),
            new Scada.Engineering.Gateways.InMemoryGatewayEngineeringRegistry(),
            assetProcessIntelligence: intelligence);
}
