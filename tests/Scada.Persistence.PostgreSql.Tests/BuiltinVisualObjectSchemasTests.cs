using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class BuiltinVisualObjectSchemasTests
{
    [Fact]
    public void BuiltinTypes_AreStableUniqueAndMatchTheGraphicalEditorFoundationSet()
    {
        Assert.Equal(
        [
            "core.group",
            "core.rectangle",
            "core.ellipse",
            "core.line",
            "core.text",
            "core.image",
            "core.valueDisplay",
            "core.button"
        ], BuiltinVisualObjectSchemas.All.Select(schema => schema.ObjectTypeKey).ToArray());
    }

    [Fact]
    public void BuiltinSchemas_UseTheSharedPropertyContractRatherThanPrivateObjectProperties()
    {
        Assert.True(BuiltinVisualObjectSchemas.Rectangle.Declares(VisualPropertyKeys.FillColor));
        Assert.True(BuiltinVisualObjectSchemas.Rectangle.Declares(VisualPropertyKeys.StrokeStyle));
        Assert.False(BuiltinVisualObjectSchemas.Rectangle.Declares(VisualPropertyKeys.AssetRef));

        Assert.True(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.AssetRef));
        Assert.True(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.ImageFit));
        Assert.True(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.ImagePositionX));
        Assert.False(BuiltinVisualObjectSchemas.Image.Declares(VisualPropertyKeys.Text));

        Assert.True(BuiltinVisualObjectSchemas.Text.Declares(VisualPropertyKeys.FontFamily));
        Assert.True(BuiltinVisualObjectSchemas.Text.Declares(VisualPropertyKeys.HorizontalAlignment));

        Assert.True(BuiltinVisualObjectSchemas.Button.Declares(VisualPropertyKeys.BackgroundColor));
        Assert.True(BuiltinVisualObjectSchemas.Button.Declares(VisualPropertyKeys.CornerRadius));
        Assert.True(BuiltinVisualObjectSchemas.Button.Declares(VisualPropertyKeys.Text));
    }

    [Fact]
    public void UnknownBuiltinType_FailsClosed()
    {
        Assert.Throws<KeyNotFoundException>(() => BuiltinVisualObjectSchemas.GetRequired("core.mystery"));
    }
}
