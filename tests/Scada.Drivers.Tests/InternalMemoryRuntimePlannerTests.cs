using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class InternalMemoryRuntimePlannerTests
{
    [Fact]
    public void Compile_SeparatesServerAndClientMemoryFromCommunicationDrivers()
    {
        var serverId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var package = Package(
            new[]
            {
                Tag(serverId, "Plant.ServerCounter", TagDataType.Int32, "memory.server", 12),
                Tag(clientId, "UI.SelectedPump", TagDataType.String, "memory.client", "P01")
            },
            new[]
            {
                new DataSourceEngineeringDto(null, "memory.server", "Server Memory", InternalMemoryRuntimePlanner.ServerMemoryDriverKey),
                new DataSourceEngineeringDto(null, "memory.client", "Client Memory", InternalMemoryRuntimePlanner.ClientMemoryDriverKey),
                new DataSourceEngineeringDto(null, "simulation", "Simulation", EngineeringDriverCompiler.SimulationDriverKey)
            });

        var result = InternalMemoryRuntimePlanner.Compile(package);

        Assert.True(result.CanActivate);
        var server = Assert.Single(result.ServerMemoryPlans);
        Assert.Equal("memory.server", server.DataSourceKey);
        Assert.False(server.IsClientMemory);
        var serverTag = Assert.Single(server.Tags);
        Assert.Equal(12, serverTag.InitialValue.Value);
        Assert.Equal("False", serverTag.Tag.Metadata!["historian.enabled"]);

        var client = Assert.Single(result.ClientMemoryPlans);
        Assert.True(client.IsClientMemory);
        var clientTag = Assert.Single(client.Tags);
        Assert.Equal("P01", clientTag.InitialValue.Value);
        Assert.Equal("False", clientTag.Tag.Metadata!["historian.enabled"]);

        var communicationSource = Assert.Single(result.CommunicationPackage.DataSources!);
        Assert.Equal("simulation", communicationSource.Key);
        Assert.DoesNotContain(result.Issues, x => x.IsError);
    }

    [Fact]
    public void Compile_ServerMemoryHistorian_IsExplicitlyEnabledOnlyWhenEngineered()
    {
        var tagId = Guid.NewGuid();
        var package = Package(
            new[]
            {
                Tag(
                    tagId,
                    "Plant.HistorizedCounter",
                    TagDataType.Int32,
                    "memory.server",
                    7,
                    new HistorianSettingsDto(Enabled: true, Strategy: "change"))
            },
            new[]
            {
                new DataSourceEngineeringDto(null, "memory.server", "Server Memory", InternalMemoryRuntimePlanner.ServerMemoryDriverKey)
            });

        var result = InternalMemoryRuntimePlanner.Compile(package);

        Assert.True(result.CanActivate);
        var tag = Assert.Single(Assert.Single(result.ServerMemoryPlans).Tags).Tag;
        Assert.Equal("True", tag.Metadata!["historian.enabled"]);
        Assert.Equal("change", tag.Metadata["historian.strategy"]);
    }

    [Fact]
    public void Compile_RejectsMemoryTagWithoutStableId()
    {
        var package = Package(
            new[]
            {
                new TagEngineeringDto(
                    null,
                    "Counter",
                    "Plant.Counter",
                    TagDataType.Int32,
                    Source: "memory.server",
                    ReadOnly: false,
                    InitialValue: Initial(TagDataType.Int32, 1))
            },
            new[]
            {
                new DataSourceEngineeringDto(null, "memory.server", "Server Memory", InternalMemoryRuntimePlanner.ServerMemoryDriverKey)
            });

        var result = InternalMemoryRuntimePlanner.Compile(package);

        Assert.False(result.CanActivate);
        Assert.Contains(result.Issues, x => x.Code == "MEMORY_TAG_STABLE_ID_REQUIRED" && x.IsError);
    }

    private static TagEngineeringDto Tag(
        Guid id,
        string path,
        TagDataType type,
        string source,
        object value,
        HistorianSettingsDto? historian = null) => new(
        id,
        path.Split('.').Last(),
        path,
        type,
        Source: source,
        ReadOnly: false,
        Historian: historian,
        InitialValue: Initial(type, value));

    private static MemoryInitialValueDto Initial(TagDataType type, object value) =>
        new(type, JsonSerializer.SerializeToElement(value, value.GetType()));

    private static EngineeringPackage Package(
        IReadOnlyCollection<TagEngineeringDto> tags,
        IReadOnlyCollection<DataSourceEngineeringDto> dataSources) => new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            tags,
            Array.Empty<AlarmEngineeringDto>(),
            dataSources);
}
