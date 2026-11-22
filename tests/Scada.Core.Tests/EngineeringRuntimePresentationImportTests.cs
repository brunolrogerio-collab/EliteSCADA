using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class EngineeringRuntimePresentationImportTests
{
    [Fact]
    public void PartialImportWithoutRuntimePresentation_PreservesExistingPresentation()
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        IEngineeringExchangeService exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var expected = new RuntimePresentationEngineeringDto(
            HistoricalPlaybackEnabled: true,
            MobileOrientation: "portrait");
        var seed = Package() with { RuntimePresentation = expected };

        Assert.Empty(exchange.Apply(seed, ImportMode.CreateAndUpdate).Issues);

        var partial = Package() with
        {
            Tags = [new TagEngineeringDto(Guid.NewGuid(), "Flow", "Plant.Flow", TagDataType.Double)]
        };
        var partialJson = JsonSerializer.Serialize(partial, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });
        var parsedPartial = exchange.ParseJsonForImport(partialJson);
        Assert.Null(parsedPartial.RuntimePresentation);
        var preview = exchange.Preview(parsedPartial, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Contains(preview.Items, item =>
            item.EntityKind == ImportEntityKind.RuntimePresentation && item.Operation == ImportOperation.Skip);
        Assert.Empty(exchange.Apply(parsedPartial, ImportMode.CreateAndUpdate).Issues);
        Assert.Equal(expected, exchange.ExportPackage().RuntimePresentation);
    }

    private static EngineeringPackage Package() => new(
        EngineeringExchangeService.CurrentSchema,
        EngineeringExchangeService.CurrentSchemaVersion,
        DateTimeOffset.UtcNow,
        Array.Empty<TagEngineeringDto>(),
        Array.Empty<AlarmEngineeringDto>());
}
