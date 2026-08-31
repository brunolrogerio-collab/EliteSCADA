using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Product;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class PreviewProductCapacityTests
{
    [Fact]
    public void Registry_AllowsTwoHundredTags_RejectsNewTagBeyondLimit_AndStillAllowsUpdates()
    {
        var tags = new InMemoryTagRegistry();

        for (var index = 0; index < ProductCapacityPolicy.MaxTagsPerProject; index++)
            tags.Register(TagDefinition.Create($"Tag {index}", $"Preview.Tag{index:D3}", TagDataType.Double));

        Assert.Equal(ProductCapacityPolicy.MaxTagsPerProject, tags.Snapshot().Count);

        var first = tags.Snapshot().First();
        var updated = first with { Description = "updated at capacity" };
        tags.Upsert(updated);
        Assert.True(tags.TryGet(first.Id, out var roundTripped));
        Assert.Equal("updated at capacity", roundTripped!.Description);

        var registerException = Assert.Throws<ProductCapacityExceededException>(() =>
            tags.Register(TagDefinition.Create("Overflow", "Preview.Overflow", TagDataType.Double)));
        Assert.Equal(ProductCapacityPolicy.MaxTagsPerProject + 1, registerException.RequestedCount);
        Assert.Equal(ProductCapacityPolicy.MaxTagsPerProject, registerException.MaximumCount);

        var upsertException = Assert.Throws<ProductCapacityExceededException>(() =>
            tags.Upsert(TagDefinition.Create("Overflow Upsert", "Preview.OverflowUpsert", TagDataType.Double)));
        Assert.Equal(ProductCapacityPolicy.MaxTagsPerProject + 1, upsertException.RequestedCount);
        Assert.Equal(ProductCapacityPolicy.MaxTagsPerProject, tags.Snapshot().Count);
    }

    [Fact]
    public void EngineeringPreviewAndApply_RejectProjectedTwoHundredFirstTagWithoutPartialMutation()
    {
        var tags = new InMemoryTagRegistry();
        for (var index = 0; index < ProductCapacityPolicy.MaxTagsPerProject - 1; index++)
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

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(x => x.Issues),
            issue => issue.Code == ProductCapacityPolicy.TagLimitIssueCode);
        Assert.Contains(apply.Issues, issue => issue.Code == ProductCapacityPolicy.TagLimitIssueCode);
        Assert.Equal(ProductCapacityPolicy.MaxTagsPerProject - 1, tags.Snapshot().Count);
        Assert.False(tags.TryGetByPath("Preview.New1", out _));
        Assert.False(tags.TryGetByPath("Preview.New2", out _));
    }

    [Fact]
    public void EngineeringPreviewAndApply_AllowsExactTwoHundredthTag()
    {
        var tags = new InMemoryTagRegistry();
        for (var index = 0; index < ProductCapacityPolicy.MaxTagsPerProject - 1; index++)
            tags.Register(TagDefinition.Create($"Existing {index}", $"Existing.Tag{index:D3}", TagDataType.Double));

        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms);
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { new TagEngineeringDto(null, "Tag 200", "Preview.Tag200", TagDataType.Double) },
            Array.Empty<AlarmEngineeringDto>(),
            Array.Empty<DataSourceEngineeringDto>());

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var apply = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(apply.Issues);
        Assert.Equal(ProductCapacityPolicy.MaxTagsPerProject, tags.Snapshot().Count);
        Assert.True(tags.TryGetByPath("Preview.Tag200", out _));
    }
}
