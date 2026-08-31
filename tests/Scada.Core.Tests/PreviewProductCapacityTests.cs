using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class PreviewProductCapacityTests
{
    [Fact]
    public void Registry_AllowsEngineeringAboveTwoHundredTags()
    {
        var tags = new InMemoryTagRegistry();

        for (var index = 0; index < 250; index++)
            tags.Register(TagDefinition.Create($"Tag {index}", $"Preview.Tag{index:D3}", TagDataType.Double));

        Assert.Equal(250, tags.Snapshot().Count);
    }

    [Fact]
    public void EngineeringPreviewAndApply_AllowsProjectToGrowBeyondDemoRunLimit()
    {
        var tags = new InMemoryTagRegistry();
        for (var index = 0; index < 199; index++)
            tags.Register(TagDefinition.Create($"Existing {index}", $"Existing.Tag{index:D3}", TagDataType.Double));

        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms);
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(null, "New 1", "Preview.New1", TagDataType.Double),
                new TagEngineeringDto(null, "New 2", "Preview.New2", TagDataType.Double)
            },
            Array.Empty<AlarmEngineeringDto>(),
            Array.Empty<DataSourceEngineeringDto>());

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var apply = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(apply.Issues);
        Assert.Equal(201, tags.Snapshot().Count);
        Assert.True(tags.TryGetByPath("Preview.New1", out _));
        Assert.True(tags.TryGetByPath("Preview.New2", out _));
    }
}
