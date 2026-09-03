using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class OperationalEventContractTests
{
    [Fact]
    public void Occurrence_PreservesDefinitionIdentityAndDynamicContext()
    {
        var definitionId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 9, 3, 20, 0, 0, TimeSpan.Zero);
        var definition = new OperationalEventDefinition(
            definitionId,
            "pump.state.changed",
            "Pump state changed",
            "state-change",
            "process",
            "runtime.transition",
            "LiftStation",
            "LiftStation/Pump01",
            tagId,
            "LiftStation/Pump01/Running",
            "Pump state changed");

        var occurrence = OperationalEventContract.CreateOccurrence(
            definition,
            new OperationalEventEmissionContext(
                Operator: "operator-7",
                Operation: "start",
                CommandId: commandId,
                CommandKey: "pump01.start",
                Context: new Dictionary<string, string> { ["from"] = "stopped", ["to"] = "running" }),
            timestamp,
            Guid.Parse("20000000-0000-0000-0000-000000000001"));

        Assert.Equal(definitionId, occurrence.DefinitionId);
        Assert.Equal(tagId, occurrence.TagId);
        Assert.Equal(commandId, occurrence.CommandId);
        Assert.Equal("operator-7", occurrence.Operator);
        Assert.Equal("start", occurrence.Operation);
        Assert.Equal("running", occurrence.Context["to"]);
        Assert.Equal(timestamp, occurrence.OccurredAt);
        Assert.IsAssignableFrom<IScadaEvent>(occurrence);
        Assert.False(occurrence is AlarmStateChanged);
    }

    [Fact]
    public void HistoricalCatalog_ExposesDedicatedOperationalEventDataset()
    {
        var dataset = HistoricalQueryCatalog.Require(HistoricalDatasets.OperationalEvents);

        Assert.Equal("operational.events", dataset.Id);
        Assert.Contains("type", dataset.Fields.Keys);
        Assert.Contains("category", dataset.Fields.Keys);
        Assert.Contains("source", dataset.Fields.Keys);
        Assert.Contains("area", dataset.Fields.Keys);
        Assert.Contains("equipment.path", dataset.Fields.Keys);
        Assert.Contains("tag.id", dataset.Fields.Keys);
        Assert.Contains("operator", dataset.Fields.Keys);
        Assert.Contains("command.id", dataset.Fields.Keys);
        Assert.Contains("context", dataset.Fields.Keys);
        Assert.NotEqual(HistoricalDatasets.AlarmEvents, dataset.Id);
    }

    [Fact]
    public void EngineeringExchange_RoundTripsOperationalEventAsFirstClassEntity()
    {
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var exchange = new EngineeringExchangeService(new Scada.Core.Tags.InMemoryTagRegistry(), alarms);
        var definition = new OperationalEventEngineeringDto(
            Guid.Parse("21000000-0000-0000-0000-000000000001"),
            "mode.changed",
            "Mode changed",
            "state-change",
            "operation",
            "runtime.logic",
            Area: "Process",
            EquipmentPath: "Process/Unit01",
            Message: "Operating mode changed");
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            OperationalEvents: new[] { definition });

        var preview = exchange.Preview(package, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);
        Assert.Contains(preview.Items, item =>
            item.EntityKind == ImportEntityKind.OperationalEvent && item.Operation == ImportOperation.Create);

        var applied = exchange.Apply(package, ImportMode.CreateAndUpdate);
        Assert.Empty(applied.Issues);
        var exported = exchange.ExportPackage();
        var roundTrip = Assert.Single(exported.OperationalEvents!);
        Assert.Equal(definition.Id, roundTrip.Id);
        Assert.Equal(definition.Key, roundTrip.Key);
        Assert.Equal(16, exported.SchemaVersion);
    }
}