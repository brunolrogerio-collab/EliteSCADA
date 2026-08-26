using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class EngineeringSchemaV5Tests
{
    [Fact]
    public void SchemaV5_JsonRoundTripsTagAccessPolicyAndHistorianMaximumPeriod()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create(
            "Frequency",
            "Plant.P01.Frequency",
            TagDataType.Double,
            "plant.modbus01",
            "Hz",
            readOnly: false,
            metadata: new Dictionary<string, string>
            {
                ["address"] = "40001",
                ["historian.enabled"] = "True",
                ["historian.strategy"] = "periodic",
                ["historian.periodMs"] = "1000",
                ["historian.maxPeriodMs"] = "10000",
                ["engineering.owner"] = "automation"
            },
            accessPolicy: new TagAccessPolicy(
                new[] { "Operator", "Supervisor" },
                Array.Empty<string>(),
                new[] { "Engineering" }));
        tags.Register(tag);
        var service = new EngineeringExchangeService(tags, alarms);

        var package = service.ParseJson(service.ExportJson());
        var exported = Assert.Single(package.Tags);

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, package.SchemaVersion);
        Assert.Equal(10000, exported.Historian!.MaximumPeriodMilliseconds);
        Assert.Equal("automation", exported.Metadata!["engineering.owner"]);
        Assert.Equal(new[] { "Operator", "Supervisor" }, exported.AccessPolicy!.ReadRoles);
        Assert.Empty(exported.AccessPolicy.WriteRoles!);
        Assert.Equal(new[] { "Engineering" }, exported.AccessPolicy.ConfigureRoles);
    }

    [Fact]
    public void TagCsv_RoundTripsMetadataHistorianAndAccessPolicyWithoutCollapsingEmptyRoles()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        tags.Register(TagDefinition.Create(
            "Setpoint",
            "Plant.P01.Setpoint",
            TagDataType.Double,
            "plant.modbus01",
            "bar",
            readOnly: false,
            metadata: new Dictionary<string, string>
            {
                ["address"] = "40100",
                ["historian.enabled"] = "True",
                ["historian.strategy"] = "deadband",
                ["historian.deadband"] = "0.1",
                ["historian.periodMs"] = "500",
                ["historian.maxPeriodMs"] = "5000",
                ["area"] = "Pumping"
            },
            accessPolicy: new TagAccessPolicy(
                new[] { "Operator", "Supervisor" },
                new[] { "Supervisor" },
                Array.Empty<string>())));
        var service = new EngineeringExchangeService(tags, alarms);

        var parsed = service.ParseTagsCsv(service.ExportTagsCsv());
        var tag = Assert.Single(parsed.Tags);

        Assert.Equal(5000, tag.Historian!.MaximumPeriodMilliseconds);
        Assert.Equal("Pumping", tag.Metadata!["area"]);
        Assert.Equal(new[] { "Operator", "Supervisor" }, tag.AccessPolicy!.ReadRoles);
        Assert.Equal(new[] { "Supervisor" }, tag.AccessPolicy.WriteRoles);
        Assert.Empty(tag.AccessPolicy.ConfigureRoles!);
    }

    [Fact]
    public void AlarmCsv_RoundTripsMetadata()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Plant.Pressure", TagDataType.Double);
        tags.Register(tag);
        alarms.Register(new AlarmDefinition(
            Guid.NewGuid(),
            "High pressure",
            tag.Id,
            AlarmType.High,
            AlarmPriority.High,
            9.5,
            true,
            "Process",
            "Pumping",
            "Pressure high",
            TimeSpan.Zero,
            true,
            true,
            true,
            new Dictionary<string, string> { ["owner"] = "operations" }));
        var service = new EngineeringExchangeService(tags, alarms);

        var parsed = service.ParseAlarmsCsv(service.ExportAlarmsCsv());
        var alarm = Assert.Single(parsed.Alarms);

        Assert.Equal("operations", alarm.Metadata!["owner"]);
    }

    [Fact]
    public void Apply_PreservesTagAccessPolicyInRuntimeRegistry()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms);
        var package = service.ExportPackage() with
        {
            Tags = new[]
            {
                new TagEngineeringDto(
                    null,
                    "Setpoint",
                    "Plant.Setpoint",
                    TagDataType.Double,
                    ReadOnly: false,
                    AccessPolicy: new TagAccessPolicyDto(
                        new[] { "Operator" },
                        new[] { "Supervisor" },
                        new[] { "Engineering" }))
            }
        };

        var result = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.Empty(result.Issues);
        Assert.True(tags.TryGetByPath("Plant.Setpoint", out var restored));
        Assert.Equal(new[] { "Operator" }, restored!.AccessPolicy!.ReadRoles);
        Assert.Equal(new[] { "Supervisor" }, restored.AccessPolicy.WriteRoles);
        Assert.Equal(new[] { "Engineering" }, restored.AccessPolicy.ConfigureRoles);
    }

    [Fact]
    public void Preview_RejectsBlankOrDuplicateRolesInTagAccessPolicy()
    {
        var service = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            new InMemoryAlarmEngine(new InMemoryScadaEventBus()));
        var package = service.ExportPackage() with
        {
            Tags = new[]
            {
                new TagEngineeringDto(
                    null,
                    "Setpoint",
                    "Plant.Setpoint",
                    TagDataType.Double,
                    ReadOnly: false,
                    AccessPolicy: new TagAccessPolicyDto(
                        new[] { "Operator", "operator", " " },
                        new[] { "Supervisor" },
                        null))
            }
        };

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "TAG_ACCESS_ROLE_INVALID");
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "TAG_ACCESS_ROLE_DUPLICATE");
    }
}
