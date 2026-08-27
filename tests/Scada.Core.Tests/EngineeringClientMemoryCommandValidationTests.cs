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

public sealed class EngineeringClientMemoryCommandValidationTests
{
    [Fact]
    public void Preview_RejectsServerCommandTargetingClientMemoryTag()
    {
        var service = CreateService(
            new InMemoryTagRegistry(),
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry());
        var tagId = Guid.NewGuid();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    tagId,
                    "SelectedPump",
                    "UI.SelectedPump",
                    TagDataType.String,
                    Source: "memory.client",
                    ReadOnly: false)
            },
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                new DataSourceEngineeringDto(
                    null,
                    "memory.client",
                    "Client Memory",
                    "builtin.memory.client")
            },
            Commands: new[]
            {
                new CommandEngineeringDto(
                    null,
                    "ui.select-pump",
                    "Select pump",
                    CommandKind.WriteTagValue,
                    "P02",
                    tagId,
                    "UI.SelectedPump")
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "CLIENT_MEMORY_COMMAND_TARGET_NOT_ALLOWED" && issue.EntityKind == ImportEntityKind.Command);
    }

    [Fact]
    public void Preview_RejectsDataSourceTransitionToClientMemoryWhileExistingCommandStillTargetsItsTag()
    {
        var tags = new InMemoryTagRegistry();
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var commands = new InMemoryCommandEngineeringRegistry();
        var tag = TagDefinition.Create(
            "Selection",
            "UI.Selection",
            TagDataType.Int32,
            "mutable.source",
            readOnly: false);
        tags.Register(tag);
        dataSources.Upsert(new DataSourceEngineeringDto(
            Guid.NewGuid(),
            "mutable.source",
            "Mutable Source",
            "builtin.simulation"));
        commands.Upsert(new CommandEngineeringDto(
            Guid.NewGuid(),
            "ui.selection.set",
            "Set selection",
            CommandKind.WriteTagValue,
            "2",
            tag.Id,
            tag.Path));
        var service = CreateService(tags, dataSources, commands);
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                new DataSourceEngineeringDto(
                    null,
                    "mutable.source",
                    "Mutable Source",
                    "builtin.memory.client")
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "CLIENT_MEMORY_EXISTING_COMMAND_NOT_ALLOWED" && issue.EntityKind == ImportEntityKind.DataSource);
    }

    private static EngineeringExchangeService CreateService(
        InMemoryTagRegistry tags,
        InMemoryDataSourceEngineeringRegistry dataSources,
        InMemoryCommandEngineeringRegistry commands)
    {
        var bus = new InMemoryScadaEventBus();
        var alarms = new InMemoryAlarmEngine(bus);
        return new EngineeringExchangeService(
            tags,
            alarms,
            dataSources,
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            commands);
    }
}
