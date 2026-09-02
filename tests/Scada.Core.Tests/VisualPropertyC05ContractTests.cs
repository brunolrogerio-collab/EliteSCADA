using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class VisualPropertyC05ContractTests
{
    [Fact]
    public void BuiltinSchemas_ExposeExtendedC05PropertiesWithCanonicalConstraints()
    {
        var rectangle = BuiltinVisualObjectSchemas.Rectangle;
        Assert.Equal(false, Assert.IsType<VisualBooleanValue>(rectangle.GetRequired(VisualPropertyKeys.HorizontalFlip).DefaultValue).Value);
        Assert.Equal(false, Assert.IsType<VisualBooleanValue>(rectangle.GetRequired(VisualPropertyKeys.VerticalFlip).DefaultValue).Value);
        Assert.Equal(string.Empty, Assert.IsType<VisualStringValue>(rectangle.GetRequired(VisualPropertyKeys.Tooltip).DefaultValue).Value);

        var stroke = rectangle.GetRequired(VisualPropertyKeys.StrokeStyle);
        Assert.Equal(
            ["none", "solid", "dashed", "dotted", "dash-dot", "dash-dot-dot"],
            stroke.Constraints.AllowedValues);

        var text = BuiltinVisualObjectSchemas.Text;
        Assert.Equal(false, Assert.IsType<VisualBooleanValue>(text.GetRequired(VisualPropertyKeys.Underline).DefaultValue).Value);
        Assert.Equal(true, Assert.IsType<VisualBooleanValue>(text.GetRequired(VisualPropertyKeys.TextWrap).DefaultValue).Value);

        var lineHeight = text.GetRequired(VisualPropertyKeys.LineHeight);
        Assert.Equal(1.2, Assert.IsType<VisualNumberValue>(lineHeight.DefaultValue).Value);
        Assert.Equal(0.1, lineHeight.Constraints.Minimum);
        Assert.Equal(10, lineHeight.Constraints.Maximum);

        var overflow = text.GetRequired(VisualPropertyKeys.TextOverflow);
        Assert.Equal(["clip", "ellipsis"], overflow.Constraints.AllowedValues);
        Assert.Equal("font-family", text.GetRequired(VisualPropertyKeys.FontFamily).PresentationHint);

        var asset = BuiltinVisualObjectSchemas.Image.GetRequired(VisualPropertyKeys.AssetRef);
        Assert.False(asset.RuntimeWritable);
        Assert.Equal("project-asset", asset.PresentationHint);
    }

    [Fact]
    public void ExtendedC05Properties_ValidateFailClosed()
    {
        var lineHeight = BuiltinVisualObjectSchemas.Text.GetRequired(VisualPropertyKeys.LineHeight);
        lineHeight.ValidateValue(new VisualNumberValue(1.6));
        Assert.Throws<ArgumentOutOfRangeException>(() => lineHeight.ValidateValue(new VisualNumberValue(0)));

        var overflow = BuiltinVisualObjectSchemas.Text.GetRequired(VisualPropertyKeys.TextOverflow);
        overflow.ValidateValue(new VisualStringValue("ellipsis"));
        Assert.Throws<ArgumentException>(() => overflow.ValidateValue(new VisualStringValue("scroll")));

        var stroke = BuiltinVisualObjectSchemas.Rectangle.GetRequired(VisualPropertyKeys.StrokeStyle);
        stroke.ValidateValue(new VisualStringValue("none"));
        stroke.ValidateValue(new VisualStringValue("dash-dot-dot"));
        Assert.Throws<ArgumentException>(() => stroke.ValidateValue(new VisualStringValue("future-style")));
    }

    [Fact]
    public void ExtendedC05Properties_PreserveEngineeringAndScriptRuntimePrecedence()
    {
        var engineered = new VisualEngineeringPropertySet(
            BuiltinVisualObjectSchemas.Rectangle,
            new Dictionary<string, VisualPropertyValue>(StringComparer.Ordinal)
            {
                [VisualPropertyKeys.HorizontalFlip] = new VisualBooleanValue(true),
                [VisualPropertyKeys.Tooltip] = new VisualStringValue("Engineering tooltip")
            });
        var runtime = new VisualRuntimePropertyState("visual:c05", engineered);

        var engineeringFlip = runtime.Resolve(VisualPropertyKeys.HorizontalFlip);
        Assert.Equal(VisualPropertyRuntimeSource.EngineeringBase, engineeringFlip.Source);
        Assert.True(Assert.IsType<VisualBooleanValue>(engineeringFlip.Value).Value);

        runtime.SetScriptOverride(VisualPropertyKeys.HorizontalFlip, new VisualBooleanValue(false));
        runtime.SetScriptOverride(VisualPropertyKeys.Tooltip, new VisualStringValue("Script tooltip"));

        var scriptFlip = runtime.Resolve(VisualPropertyKeys.HorizontalFlip);
        Assert.Equal(VisualPropertyRuntimeSource.Script, scriptFlip.Source);
        Assert.False(Assert.IsType<VisualBooleanValue>(scriptFlip.Value).Value);
        Assert.Equal(
            "Script tooltip",
            Assert.IsType<VisualStringValue>(runtime.Resolve(VisualPropertyKeys.Tooltip).Value).Value);

        runtime.ClearScriptOverride(VisualPropertyKeys.HorizontalFlip);
        Assert.True(Assert.IsType<VisualBooleanValue>(runtime.Resolve(VisualPropertyKeys.HorizontalFlip).Value).Value);
    }

    [Fact]
    public void AssetReference_RemainsExplicitRuntimeWriteException()
    {
        var engineered = new VisualEngineeringPropertySet(
            BuiltinVisualObjectSchemas.Image,
            new Dictionary<string, VisualPropertyValue>(StringComparer.Ordinal)
            {
                [VisualPropertyKeys.AssetRef] = new VisualAssetReferenceValue("asset:c05-image")
            });
        var runtime = new VisualRuntimePropertyState("visual:c05-image", engineered);

        Assert.Equal(
            "asset:c05-image",
            Assert.IsType<VisualAssetReferenceValue>(runtime.Resolve(VisualPropertyKeys.AssetRef).Value).AssetId);
        Assert.Throws<InvalidOperationException>(() =>
            runtime.SetScriptOverride(
                VisualPropertyKeys.AssetRef,
                new VisualAssetReferenceValue("asset:c05-other")));
    }
}
