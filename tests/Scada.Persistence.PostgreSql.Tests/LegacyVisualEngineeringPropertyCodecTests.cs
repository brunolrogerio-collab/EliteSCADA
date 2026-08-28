using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class LegacyVisualEngineeringPropertyCodecTests
{
    [Fact]
    public void Codec_DecodesLegacyStringsOnlyThroughDeclaredPropertySchema()
    {
        var schema = CreateSchema();
        var decoded = LegacyVisualEngineeringPropertyCodec.Decode(
            schema,
            new Dictionary<string, string>
            {
                [VisualPropertyKeys.X] = "12.5",
                [VisualPropertyKeys.ZIndex] = "3",
                [VisualPropertyKeys.Visible] = "false",
                [VisualPropertyKeys.FillColor] = "#11223344",
                [VisualPropertyKeys.AssetRef] = "asset:plant-logo",
                [VisualPropertyKeys.ImageFit] = "native"
            });

        Assert.Equal(12.5d, Assert.IsType<VisualNumberValue>(decoded[VisualPropertyKeys.X]).Value);
        Assert.Equal(3, Assert.IsType<VisualIntegerValue>(decoded[VisualPropertyKeys.ZIndex]).Value);
        Assert.False(Assert.IsType<VisualBooleanValue>(decoded[VisualPropertyKeys.Visible]).Value);
        Assert.Equal("#11223344", Assert.IsType<VisualColorValue>(decoded[VisualPropertyKeys.FillColor]).Value);
        Assert.Equal("asset:plant-logo", Assert.IsType<VisualAssetReferenceValue>(decoded[VisualPropertyKeys.AssetRef]).AssetId);
        Assert.Equal("native", Assert.IsType<VisualStringValue>(decoded[VisualPropertyKeys.ImageFit]).Value);
    }

    [Fact]
    public void Codec_EncodesTypedValuesCanonicallyAndOmitsNullAssetReference()
    {
        var schema = CreateSchema();
        var encoded = LegacyVisualEngineeringPropertyCodec.Encode(
            schema,
            new Dictionary<string, VisualPropertyValue>
            {
                [VisualPropertyKeys.X] = new VisualNumberValue(12.5),
                [VisualPropertyKeys.ZIndex] = new VisualIntegerValue(3),
                [VisualPropertyKeys.Visible] = new VisualBooleanValue(false),
                [VisualPropertyKeys.AssetRef] = new VisualAssetReferenceValue(null)
            });

        Assert.Equal("12.5", encoded[VisualPropertyKeys.X]);
        Assert.Equal("3", encoded[VisualPropertyKeys.ZIndex]);
        Assert.Equal("false", encoded[VisualPropertyKeys.Visible]);
        Assert.DoesNotContain(VisualPropertyKeys.AssetRef, encoded.Keys);
    }

    [Theory]
    [InlineData("TRUE")]
    [InlineData("False")]
    [InlineData("1")]
    public void Codec_RejectsNonCanonicalBooleanText(string serialized)
    {
        var schema = CreateSchema();
        Assert.Throws<InvalidDataException>(() =>
            LegacyVisualEngineeringPropertyCodec.Decode(
                schema,
                new Dictionary<string, string> { [VisualPropertyKeys.Visible] = serialized }));
    }

    [Fact]
    public void Codec_RejectsUnknownPropertyAndInvalidAssetReference()
    {
        var schema = CreateSchema();

        Assert.Throws<KeyNotFoundException>(() =>
            LegacyVisualEngineeringPropertyCodec.Decode(
                schema,
                new Dictionary<string, string> { ["privateEditorValue"] = "1" }));

        Assert.Throws<ArgumentException>(() =>
            LegacyVisualEngineeringPropertyCodec.Decode(
                schema,
                new Dictionary<string, string> { [VisualPropertyKeys.AssetRef] = "https://example.invalid/logo.png" }));
    }

    private static VisualObjectPropertySchema CreateSchema() =>
        new VisualPropertySchemaBuilder("basic.image")
            .Include(CommonVisualPropertyDefinitions.Geometry)
            .Include(CommonVisualPropertyDefinitions.Visibility)
            .Include(CommonVisualPropertyDefinitions.Fill)
            .Include(CommonVisualPropertyDefinitions.Image)
            .Build();
}
