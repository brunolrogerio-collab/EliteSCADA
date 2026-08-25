using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class EngineeringExchangeTests
{
    [Fact]
    public void ExportAndParseJson_RoundTripsTagAndAlarm()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Plant.P01.Pressure", TagDataType.Double, "modbus.tcp", "bar");
        tags.Register(tag);
        alarms.Register(AlarmDefinition.Create("High pressure", tag.Id, AlarmType.High, AlarmPriority.High, setpoint: 9.5, area: "Plant"));
        var service = new EngineeringExchangeService(tags, alarms);

        var json = service.ExportJson();
        var package = service.ParseJson(json);

        Assert.Single(package.Tags);
        Assert.Single(package.Alarms);
        Assert.Equal("Plant.P01.Pressure", package.Tags.Single().Path);
        Assert.Equal("High pressure", package.Alarms.Single().Name);
    }

    [Fact]
    public void Preview_DetectsDuplicateTagPaths()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms);
        var package = new EngineeringPackage(EngineeringExchangeService.CurrentSchema, 1, DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(null, "A", "Plant.Duplicate", TagDataType.Double),
                new TagEngineeringDto(null, "B", "Plant.Duplicate", TagDataType.Double)
            }, Array.Empty<AlarmEngineeringDto>());

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Equal(2, preview.ErrorCount);
    }

    [Fact]
    public void Apply_CreateAndUpdate_PreservesStableIdWhenPathMatches()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var existing = TagDefinition.Create("Old", "Plant.P01.Current", TagDataType.Double);
        tags.Register(existing);
        var service = new EngineeringExchangeService(tags, alarms);
        var package = new EngineeringPackage(EngineeringExchangeService.CurrentSchema, 1, DateTimeOffset.UtcNow,
            new[] { new TagEngineeringDto(null, "Current", "Plant.P01.Current", TagDataType.Double, EngineeringUnit: "A") },
            Array.Empty<AlarmEngineeringDto>());

        var result = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.Equal(1, result.Updated);
        Assert.True(tags.TryGetByPath("Plant.P01.Current", out var updated));
        Assert.Equal(existing.Id, updated!.Id);
        Assert.Equal("Current", updated.Name);
    }
}
