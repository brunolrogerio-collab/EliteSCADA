using System.Text;
using System.Xml.Linq;
using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class VerticalMotorStateArtworkTests
{
    [Fact]
    public void PaintedHousingRibsAndTerminalBox_AllFollowState_WithoutPaintingShaft()
    {
        var entry = Assert.Single(BuiltinDynamoCatalogV1.CreateArtworkAssets(),
            candidate => candidate.Asset.Key == "builtin.dynamo.v1.motor-vertical");
        var root = XDocument.Parse(Encoding.UTF8.GetString(entry.Payload.Content)).Root!;
        XNamespace svg = "http://www.w3.org/2000/svg";
        var state = root.Descendants(svg + "rect")
            .Where(e => e.Attribute("data-elitescada-slot")?.Value == "state").ToArray();
        Assert.Equal(7, state.Length);
        Assert.All(state, part => Assert.Equal("#93B99A", part.Attribute("fill")?.Value));
        Assert.Contains(state, part => part.Attribute("x")?.Value == "38" && part.Attribute("y")?.Value == "110");
        Assert.Equal(5, state.Count(part => part.Attribute("y")?.Value == "55"));
        var shaft = Assert.Single(root.Descendants(svg + "rect"), part =>
            part.Attribute("x")?.Value == "119" && part.Attribute("y")?.Value == "345");
        Assert.Null(shaft.Attribute("data-elitescada-slot"));
        Assert.Equal("#A9A9A9", shaft.Attribute("fill")?.Value);
        var tones = root.Descendants(svg + "rect").Where(part => part.Attribute("fill-opacity") is not null).ToArray();
        Assert.Equal(6, tones.Length);
        Assert.All(tones, tone => Assert.Null(tone.Attribute("data-elitescada-slot")));
        Assert.Equal(5, tones.Count(tone => tone.Attribute("fill")?.Value == "#000000"));
        Assert.Single(tones, tone => tone.Attribute("fill")?.Value == "#FFFFFF");
        Assert.DoesNotContain(root.Descendants(), part =>
            part.Attribute("fill")?.Value is "#E6D319" or "#D4C214" or "#EBF01D");
    }
}
