using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Security;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class EngineeringCommandExchangeTests
{
    [Fact]
    public void SchemaV7_RoundTripsOperationalCommands()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var commands = new InMemoryCommandEngineeringRegistry();
        var tag = TagDefinition.Create("Run", "Plant.P01.Run", TagDataType.Boolean, "modbus.tcp", readOnly: false);
        tags.Register(tag);
        commands.Upsert(new CommandEngineeringDto(
            Guid.NewGuid(),
            "plant.p01.start",
            "Start P01",
            CommandKind.WriteTagValue,
            "true",
            tag.Id,
            tag.Path,
            Area: "Plant",
            EquipmentPath: "Plant.P01"));
        var service = CreateService(tags, alarms, commands);

        var package = service.ParseJson(service.ExportJson());

        Assert.Equal(7, package.SchemaVersion);
        var command = Assert.Single(package.Commands!);
        Assert.Equal("plant.p01.start", command.Key);
        Assert.Equal(tag.Id, command.TargetTagId);
        Assert.Equal("Plant.P01", command.EquipmentPath);
    }

    [Fact]
    public void Preview_RejectsCommandReferencingUnknownTag()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = CreateService(tags, alarms, new InMemoryCommandEngineeringRegistry());
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Commands: new[]
            {
                new CommandEngineeringDto(null, "plant.p01.start", "Start P01", CommandKind.WriteTagValue, "true", TargetTagPath: "Plant.P01.Run")
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "COMMAND_TARGET_TAG_NOT_FOUND");
    }

    [Fact]
    public void Preview_RejectsCommandTargetingReadOnlyTag()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Running", "Plant.P01.Running", TagDataType.Boolean, readOnly: true);
        tags.Register(tag);
        var service = CreateService(tags, alarms, new InMemoryCommandEngineeringRegistry());
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Commands: new[]
            {
                new CommandEngineeringDto(null, "plant.p01.start", "Start P01", CommandKind.WriteTagValue, "true", tag.Id, tag.Path)
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "COMMAND_TARGET_TAG_READ_ONLY");
    }

    [Fact]
    public void Preview_RejectsCommandValueThatDoesNotMatchTagType()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Run", "Plant.P01.Run", TagDataType.Boolean, readOnly: false);
        tags.Register(tag);
        var service = CreateService(tags, alarms, new InMemoryCommandEngineeringRegistry());
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Commands: new[]
            {
                new CommandEngineeringDto(null, "plant.p01.start", "Start P01", CommandKind.WriteTagValue, "maybe", tag.Id, tag.Path)
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "COMMAND_VALUE_INVALID");
    }

    [Fact]
    public void SchemaV6WithoutCommands_RemainsReadable()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = CreateService(tags, alarms, new InMemoryCommandEngineeringRegistry());
        const string json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 6,
          "exportedAt": "2026-08-26T00:00:00Z",
          "tags": [],
          "alarms": []
        }
        """;

        var package = service.ParseJson(json);

        Assert.Empty(package.Commands!);
    }

    private static EngineeringExchangeService CreateService(
        ITagRegistry tags,
        IAlarmEngine alarms,
        ICommandEngineeringRegistry commands) =>
        new(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            commands);
}
