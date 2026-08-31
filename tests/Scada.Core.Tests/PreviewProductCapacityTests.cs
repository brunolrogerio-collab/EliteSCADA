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
    public void EngineeringRegistry_AllowsProjectsAboveDemoRuntimeLimit()
    {
        var tags = new InMemoryTagRegistry();
        var expected = ProductLicensePolicy.DemoMaxTags + 25;

        for (var index = 0; index < expected; index++)
            tags.Register(TagDefinition.Create($"Tag {index}", $"Project.Tag{index:D3}", TagDataType.Double));

        Assert.Equal(expected, tags.Snapshot().Count);
    }

    [Fact]
    public void EngineeringPreviewAndApply_AllowsImportBeyondDemoRuntimeLimit()
    {
        var tags = new InMemoryTagRegistry();
        for (var index = 0; index < ProductLicensePolicy.DemoMaxTags; index++)
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
                new TagEngineeringDto(null, "New 201", "Project.Tag201", TagDataType.Double),
                new TagEngineeringDto(null, "New 202", "Project.Tag202", TagDataType.Double)
            },
            Array.Empty<AlarmEngineeringDto>(),
            Array.Empty<DataSourceEngineeringDto>());

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var apply = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(apply.Issues);
        Assert.Equal(ProductLicensePolicy.DemoMaxTags + 2, tags.Snapshot().Count);
        Assert.True(tags.TryGetByPath("Project.Tag201", out _));
        Assert.True(tags.TryGetByPath("Project.Tag202", out _));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(1500)]
    [InlineData(3000)]
    [InlineData(5000)]
    public void LicensedTagTiers_AcceptSupportedCommercialCapacities(int tags)
    {
        Assert.True(ProductLicensePolicy.IsSupportedLicensedTagTier(tags));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(999)]
    [InlineData(6000)]
    public void LicensedTagTiers_RejectArbitraryCapacities(int tags)
    {
        Assert.False(ProductLicensePolicy.IsSupportedLicensedTagTier(tags));
    }
}
