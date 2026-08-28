using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class VisualAssetPropertyConvergenceTests
{
    [Fact]
    public void CommonImageSchema_UsesAssetRefWithNullDefaultAndNativeFit()
    {
        var schema = new VisualPropertySchemaBuilder("basic.image")
            .Include(CommonVisualPropertyDefinitions.Image)
            .Build();

        var asset = schema.GetRequired(VisualPropertyKeys.AssetRef);
        var fit = schema.GetRequired(VisualPropertyKeys.ImageFit);

        Assert.Equal(VisualPropertyValueKind.AssetReference, asset.ValueKind);
        Assert.Null(Assert.IsType<VisualAssetReferenceValue>(asset.DefaultValue).AssetId);
        Assert.False(asset.RuntimeWritable);
        Assert.False(asset.SupportsBinding);
        Assert.Equal("project-asset", asset.PresentationHint);

        Assert.Equal("contain", Assert.IsType<VisualStringValue>(fit.DefaultValue).Value);
        fit.ValidateValue(new VisualStringValue("native"));
        Assert.Throws<ArgumentException>(() => fit.ValidateValue(new VisualStringValue("none")));
        Assert.False(schema.Declares(VisualPropertyKeys.ImageResourceId));
    }

    [Theory]
    [InlineData("asset:plant-logo")]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("logo_01.png")]
    public void AssetRef_AcceptsStableProjectOwnedIdentity(string assetId)
    {
        var property = new VisualPropertySchemaBuilder("basic.image")
            .Include(CommonVisualPropertyDefinitions.Image)
            .Build()
            .GetRequired(VisualPropertyKeys.AssetRef);

        property.ValidateValue(new VisualAssetReferenceValue(assetId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" https://example.invalid/logo.png")]
    [InlineData("https://example.invalid/logo.png")]
    [InlineData("file:logo.png")]
    [InlineData("C:\\plant\\logo.png")]
    [InlineData("asset:folder/logo.png")]
    [InlineData("asset:")]
    public void AssetRef_RejectsPathsUrlsAndMalformedIdentity(string assetId)
    {
        var property = new VisualPropertySchemaBuilder("basic.image")
            .Include(CommonVisualPropertyDefinitions.Image)
            .Build()
            .GetRequired(VisualPropertyKeys.AssetRef);

        Assert.Throws<ArgumentException>(() =>
            property.ValidateValue(new VisualAssetReferenceValue(assetId)));
    }
}
