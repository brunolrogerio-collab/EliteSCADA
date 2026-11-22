using System.Text;
using Scada.Engineering.VisualAssets;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class VisualSvgWave15Tests
{
    [Fact]
    public void StaticSvgInspector_DerivesDeterministicPaletteSlotsAndViewBox()
    {
        var svg = Encoding.UTF8.GetBytes(
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 50">
              <g data-elitescada-slot="body">
                <rect x="1" y="2" width="98" height="46" fill="#abc" stroke="black" stroke-width="2" />
              </g>
              <path d="M0 0L10 10" style="fill:none;stroke:rgb(255,0,0);stroke-width:3" />
            </svg>
            """);

        var first = VisualAssetContentInspector.InspectAndCanonicalize(svg);
        var second = VisualAssetContentInspector.InspectAndCanonicalize(first.CanonicalContent);

        Assert.Equal(VisualAssetContentInspector.SvgMediaType, first.MediaType);
        Assert.NotNull(first.CanonicalMetadata);
        Assert.Equal("1", first.CanonicalMetadata![StaticSvgInspector.MetadataVersionKey]);
        Assert.Equal("0 0 100 50", first.CanonicalMetadata[StaticSvgInspector.MetadataViewBoxKey]);
        Assert.Contains("\"color\":\"#AABBCC\"", first.CanonicalMetadata[StaticSvgInspector.MetadataPaletteKey]);
        Assert.Contains("\"color\":\"#000000\"", first.CanonicalMetadata[StaticSvgInspector.MetadataPaletteKey]);
        Assert.Contains("\"color\":\"#FF0000\"", first.CanonicalMetadata[StaticSvgInspector.MetadataPaletteKey]);
        Assert.Contains("\"name\":\"body\"", first.CanonicalMetadata[StaticSvgInspector.MetadataSlotsKey]);
        Assert.Contains("\"strokeWidth\":true", first.CanonicalMetadata[StaticSvgInspector.MetadataSlotsKey]);
        Assert.Equal(first.CanonicalMetadata, second.CanonicalMetadata);
    }

    [Fact]
    public void StaticSvgInspector_RejectsActiveContentExternalReferencesAndInvalidSlots()
    {
        var script = Encoding.UTF8.GetBytes(
            """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""");
        var external = Encoding.UTF8.GetBytes(
            """<svg xmlns="http://www.w3.org/2000/svg"><use href="https://example.test/a.svg#x"/></svg>""");
        var badSlot = Encoding.UTF8.GetBytes(
            """<svg xmlns="http://www.w3.org/2000/svg"><path data-elitescada-slot="bad slot" d="M0 0L1 1"/></svg>""");

        Assert.Throws<InvalidDataException>(() => VisualAssetContentInspector.InspectAndCanonicalize(script));
        Assert.Throws<InvalidDataException>(() => VisualAssetContentInspector.InspectAndCanonicalize(external));
        Assert.Throws<InvalidDataException>(() => VisualAssetContentInspector.InspectAndCanonicalize(badSlot));
    }

    [Fact]
    public void SvgSymbolSchema_ExposesRequiredEditableCanvasAndPaintProperties()
    {
        var schema = BuiltinVisualObjectSchemas.GetRequired(BuiltinVisualObjectSchemas.SvgSymbolType);

        foreach (var key in new[]
        {
            VisualPropertyKeys.X,
            VisualPropertyKeys.Y,
            VisualPropertyKeys.Width,
            VisualPropertyKeys.Height,
            VisualPropertyKeys.Rotation,
            VisualPropertyKeys.HorizontalFlip,
            VisualPropertyKeys.VerticalFlip,
            VisualPropertyKeys.Opacity,
            VisualPropertyKeys.AssetRef,
            VisualPropertyKeys.FillColor,
            VisualPropertyKeys.StrokeColor,
            VisualPropertyKeys.StrokeWidth
        })
            Assert.True(schema.Declares(key), $"core.svgSymbol must declare '{key}'.");

        Assert.Contains(BuiltinVisualObjectSchemas.SvgSymbol, BuiltinVisualObjectSchemas.All);
    }
}
