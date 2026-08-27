using Scada.Core.Abstractions;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Historian.Memory;
using Scada.Historian.Policies;

namespace Scada.Historian.TimescaleDb.Tests;

public sealed class HistorianCapturePolicyTests
{
    [Fact]
    public void ShouldCapture_PreservesLegacyTagsButHonorsExplicitEngineeringFlag()
    {
        Assert.True(HistorianCapturePolicy.ShouldCapture(CreateTag(null)));
        Assert.True(HistorianCapturePolicy.ShouldCapture(CreateTag("true")));
        Assert.False(HistorianCapturePolicy.ShouldCapture(CreateTag("false")));
        Assert.False(HistorianCapturePolicy.ShouldCapture(CreateTag("not-a-bool")));
    }

    [Fact]
    public async Task BufferedHistorian_StoresEnabledTagAndSkipsExplicitlyDisabledTag()
    {
        var bus = new InMemoryScadaEventBus();
        await using var historian = new BufferedInMemoryHistorian(bus);
        var enabled = CreateTag("true");
        var disabled = CreateTag("false");
        var timestamp = DateTimeOffset.UtcNow;

        await bus.PublishAsync(new TagValueChanged(
            enabled,
            null,
            new TagValue(enabled.Id, 10, timestamp, TagQuality.Good, "memory.server"),
            timestamp));
        await bus.PublishAsync(new TagValueChanged(
            disabled,
            null,
            new TagValue(disabled.Id, 20, timestamp, TagQuality.Good, "memory.server"),
            timestamp));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (historian.WrittenSamples < 1 && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Single(historian.Query(enabled.Id, timestamp.AddSeconds(-1), timestamp.AddSeconds(1)));
        Assert.Empty(historian.Query(disabled.Id, timestamp.AddSeconds(-1), timestamp.AddSeconds(1)));
    }

    private static TagDefinition CreateTag(string? historianEnabled)
    {
        var metadata = historianEnabled is null
            ? null
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [HistorianCapturePolicy.EnabledMetadataKey] = historianEnabled
            };

        return new TagDefinition(
            Guid.NewGuid(),
            "Value",
            $"Memory.{Guid.NewGuid():N}",
            TagDataType.Int32,
            Source: "memory.server",
            EngineeringUnit: null,
            Description: null,
            ReadOnly: false,
            Metadata: metadata);
    }
}
