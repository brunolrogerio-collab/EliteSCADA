using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class VisualPropertyC05ContractTests
{
    [Fact]
    public void BuiltinSchemas_ExposeExtendedC05PropertiesWithCanonicalConstraints()
    {
        var rectangle = BuiltinVisualObjectSchemas.Rectangle;
        Assert.False(Assert.IsType<VisualBooleanValue>(rectangle.GetRequired(VisualPropertyKeys.HorizontalFlip).DefaultValue).Value);
        Assert.False(Assert.IsType<VisualBooleanValue>(rectangle.GetRequired(VisualPropertyKeys.VerticalFlip).DefaultValue).Value);
        Assert.Equal(string.Empty, Assert.IsType<VisualStringValue>(rectangle.GetRequired(VisualPropertyKeys.Tooltip).DefaultValue).Value);
        Assert.True(Assert.IsType<VisualBooleanValue>(rectangle.GetRequired(VisualPropertyKeys.Enabled).DefaultValue).Value);

        var fillStyle = rectangle.GetRequired(VisualPropertyKeys.FillStyle);
        Assert.Equal(new[] { "none", "solid", "gradient" }, fillStyle.Constraints.AllowedValues);
        Assert.Equal("solid", Assert.IsType<VisualStringValue>(fillStyle.DefaultValue).Value);
        Assert.Equal(
            new[] { "horizontal", "vertical", "diagonal-down", "diagonal-up" },
            rectangle.GetRequired(VisualPropertyKeys.GradientDirection).Constraints.AllowedValues);
        Assert.Equal(
            "#00000000",
            Assert.IsType<VisualColorValue>(rectangle.GetRequired(VisualPropertyKeys.FillSecondaryColor).DefaultValue).Value);

        Assert.False(Assert.IsType<VisualBooleanValue>(rectangle.GetRequired(VisualPropertyKeys.ShadowEnabled).DefaultValue).Value);
        Assert.Equal(
            "#00000066",
            Assert.IsType<VisualColorValue>(rectangle.GetRequired(VisualPropertyKeys.ShadowColor).DefaultValue).Value);
        Assert.Equal(
            0d,
            Assert.IsType<VisualNumberValue>(rectangle.GetRequired(VisualPropertyKeys.ShadowBlur).DefaultValue).Value);
        Assert.Equal(0d, rectangle.GetRequired(VisualPropertyKeys.ShadowBlur).Constraints.Minimum!.Value);

        var stroke = rectangle.GetRequired(VisualPropertyKeys.StrokeStyle);
        Assert.Equal(
            new[] { "none", "solid", "dashed", "dotted", "dash-dot", "dash-dot-dot" },
            stroke.Constraints.AllowedValues);

        var line = BuiltinVisualObjectSchemas.Line;
        Assert.False(line.Declares(VisualPropertyKeys.FillStyle));
        Assert.True(line.Declares(VisualPropertyKeys.ShadowEnabled));
        Assert.True(line.Declares(VisualPropertyKeys.Enabled));

        var text = BuiltinVisualObjectSchemas.Text;
        Assert.False(Assert.IsType<VisualBooleanValue>(text.GetRequired(VisualPropertyKeys.Underline).DefaultValue).Value);
        Assert.True(Assert.IsType<VisualBooleanValue>(text.GetRequired(VisualPropertyKeys.TextWrap).DefaultValue).Value);

        var lineHeight = text.GetRequired(VisualPropertyKeys.LineHeight);
        Assert.Equal(1.2d, Assert.IsType<VisualNumberValue>(lineHeight.DefaultValue).Value);
        Assert.Equal(0.1d, lineHeight.Constraints.Minimum!.Value);
        Assert.Equal(10d, lineHeight.Constraints.Maximum!.Value);

        var overflow = text.GetRequired(VisualPropertyKeys.TextOverflow);
        Assert.Equal(new[] { "clip", "ellipsis" }, overflow.Constraints.AllowedValues);
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

        var fill = BuiltinVisualObjectSchemas.Rectangle.GetRequired(VisualPropertyKeys.FillStyle);
        fill.ValidateValue(new VisualStringValue("gradient"));
        Assert.Throws<ArgumentException>(() => fill.ValidateValue(new VisualStringValue("pattern")));

        var direction = BuiltinVisualObjectSchemas.Rectangle.GetRequired(VisualPropertyKeys.GradientDirection);
        direction.ValidateValue(new VisualStringValue("diagonal-up"));
        Assert.Throws<ArgumentException>(() => direction.ValidateValue(new VisualStringValue("radial")));

        var shadowBlur = BuiltinVisualObjectSchemas.Rectangle.GetRequired(VisualPropertyKeys.ShadowBlur);
        shadowBlur.ValidateValue(new VisualNumberValue(12));
        Assert.Throws<ArgumentOutOfRangeException>(() => shadowBlur.ValidateValue(new VisualNumberValue(-1)));
    }

    [Fact]
    public void ExtendedC05Properties_PreserveEngineeringAndScriptRuntimePrecedence()
    {
        var engineered = new VisualEngineeringPropertySet(
            BuiltinVisualObjectSchemas.Rectangle,
            new Dictionary<string, VisualPropertyValue>(StringComparer.Ordinal)
            {
                [VisualPropertyKeys.HorizontalFlip] = new VisualBooleanValue(true),
                [VisualPropertyKeys.Tooltip] = new VisualStringValue("Engineering tooltip"),
                [VisualPropertyKeys.FillStyle] = new VisualStringValue("gradient"),
                [VisualPropertyKeys.Enabled] = new VisualBooleanValue(false),
                [VisualPropertyKeys.ShadowEnabled] = new VisualBooleanValue(true)
            });
        var runtime = new VisualRuntimePropertyState("visual:c05", engineered);

        var engineeringFlip = runtime.Resolve(VisualPropertyKeys.HorizontalFlip);
        Assert.Equal(VisualPropertyRuntimeSource.EngineeringBase, engineeringFlip.Source);
        Assert.True(Assert.IsType<VisualBooleanValue>(engineeringFlip.Value).Value);
        Assert.False(Assert.IsType<VisualBooleanValue>(runtime.Resolve(VisualPropertyKeys.Enabled).Value).Value);
        Assert.Equal("gradient", Assert.IsType<VisualStringValue>(runtime.Resolve(VisualPropertyKeys.FillStyle).Value).Value);

        runtime.SetScriptOverride(VisualPropertyKeys.HorizontalFlip, new VisualBooleanValue(false));
        runtime.SetScriptOverride(VisualPropertyKeys.Tooltip, new VisualStringValue("Script tooltip"));
        runtime.SetScriptOverride(VisualPropertyKeys.Enabled, new VisualBooleanValue(true));
        runtime.SetScriptOverride(VisualPropertyKeys.FillStyle, new VisualStringValue("none"));
        runtime.SetScriptOverride(VisualPropertyKeys.ShadowEnabled, new VisualBooleanValue(false));

        var scriptFlip = runtime.Resolve(VisualPropertyKeys.HorizontalFlip);
        Assert.Equal(VisualPropertyRuntimeSource.Script, scriptFlip.Source);
        Assert.False(Assert.IsType<VisualBooleanValue>(scriptFlip.Value).Value);
        Assert.Equal(
            "Script tooltip",
            Assert.IsType<VisualStringValue>(runtime.Resolve(VisualPropertyKeys.Tooltip).Value).Value);
        Assert.True(Assert.IsType<VisualBooleanValue>(runtime.Resolve(VisualPropertyKeys.Enabled).Value).Value);
        Assert.Equal("none", Assert.IsType<VisualStringValue>(runtime.Resolve(VisualPropertyKeys.FillStyle).Value).Value);
        Assert.False(Assert.IsType<VisualBooleanValue>(runtime.Resolve(VisualPropertyKeys.ShadowEnabled).Value).Value);

        runtime.ClearScriptOverride(VisualPropertyKeys.HorizontalFlip);
        runtime.ClearScriptOverride(VisualPropertyKeys.Enabled);
        Assert.True(Assert.IsType<VisualBooleanValue>(runtime.Resolve(VisualPropertyKeys.HorizontalFlip).Value).Value);
        Assert.False(Assert.IsType<VisualBooleanValue>(runtime.Resolve(VisualPropertyKeys.Enabled).Value).Value);
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
