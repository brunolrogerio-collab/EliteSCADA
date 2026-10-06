using Scada.Api.VisualAssets;
using System.Xml.Linq;
using Xunit;

namespace Scada.Drivers.Tests;

public sealed class StaticArtworkCatalogTests
{
    [Theory]
    [InlineData("builtin.asset.elite-saneamento.bomba-submersivel", "Bomba submersível", "0 0 400 450", 0, 8)]
    [InlineData("builtin.asset.elite-saneamento.motor-vertical", "Motor vertical", "0 0 250 400", 8, 0)]
    [InlineData("builtin.asset.elite-saneamento.entrada-bomba-submersivel", "Entrada de bomba submersível", "0 0 500 220", 6, 2)]
    [InlineData("builtin.asset.elite-saneamento.valvula-manual-volante", "Válvula manual com volante", "0 0 300 260", 4, 0)]
    [InlineData("builtin.asset.elite-saneamento.corpo-esferico-dourado", "Corpo esférico dourado", "0 0 300 260", 0, 1)]
    [InlineData("builtin.asset.elite-saneamento.antena-verde", "Antena verde", "0 0 320 240", 13, 0)]
    [InlineData("builtin.asset.elite-saneamento.calha-com-acionamento", "Calha com acionamento", "0 0 600 280", 17, 3)]
    [InlineData("builtin.asset.elite-saneamento.motor-inclinado-amarelo", "Motor inclinado amarelo", "0 0 350 320", 9, 0)]
    [InlineData("builtin.asset.elite-saneamento.motor-horizontal-vermelho", "Motor horizontal vermelho", "0 0 350 240", 7, 1)]
    [InlineData("builtin.asset.elite-saneamento.valvula-vertical", "Válvula vertical", "0 0 320 200", 6, 0)]
    [InlineData("builtin.asset.elite-saneamento.seta-vermelha", "Seta vermelha", "0 0 200 120", 0, 1)]
    [InlineData("builtin.asset.elite-saneamento.tanque-fluido-marrom", "Tanque com fluido marrom", "0 0 300 320", 2, 11)]
    [InlineData("builtin.asset.elite-saneamento.painel-metalico", "Painel metálico", "0 0 500 220", 3, 0)]
    [InlineData("builtin.asset.elite-saneamento.tanque-limpo", "Tanque limpo", "0 0 300 320", 2, 1)]
    [InlineData("builtin.asset.elite-saneamento.clarificador-marrom", "Clarificador marrom", "0 0 450 280", 5, 6)]
    [InlineData("builtin.asset.elite-saneamento.icone-bomba-motor", "Ícone de bomba e motor", "0 0 320 250", 12, 2)]
    [InlineData("builtin.asset.elite-saneamento.botao-verde-cromado", "Botão verde cromado", "0 0 320 200", 0, 0)]
    [InlineData("builtin.asset.elite-saneamento.botao-vermelho-cromado", "Botão vermelho cromado", "0 0 320 200", 0, 0)]
    [InlineData("builtin.asset.elite-saneamento.botao-cogumelo-vermelho", "Botão cogumelo vermelho", "0 0 300 240", 2, 0)]
    [InlineData("builtin.asset.elite-saneamento.estrutura-cilindrica-detalhada", "Estrutura cilíndrica detalhada", "0 0 1024 680", 15, 10)]
    [InlineData("builtin.asset.elite-saneamento.painel-maquina-controle", "Painel de controle de máquina", "0 0 1024 640", 6, 1)]
    [InlineData("builtin.asset.elite-saneamento.estrutura-motor-horizontal", "Estrutura com motor horizontal", "0 0 1024 512", 9, 2)]
    public void OwnerSuppliedDrawingsPreserveGeometryAndRemainSeparateFromUserAssets(
        string id, string name, string viewBox, int rectangles, int paths)
    {
        var catalog = new StaticArtworkCatalog();
        var entry = Assert.Single(catalog.Entries, item => item.Id == id);
        Assert.Equal("elite-saneamento", entry.Category);
        Assert.Equal(name, entry.Name);
        var svg = XDocument.Parse(System.Text.Encoding.UTF8.GetString(catalog.Content(id)!));
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Equal(ns + "svg", svg.Root!.Name);
        Assert.Equal(viewBox, svg.Root.Attribute("viewBox")!.Value);
        Assert.Equal(rectangles, svg.Descendants(ns + "rect").Count());
        Assert.Equal(paths, svg.Descendants(ns + "path").Count());
        var bounds = viewBox.Split(' ');
        Assert.DoesNotContain(svg.Root.Elements(ns + "rect"), rectangle =>
            (rectangle.Attribute("x")?.Value is null or "0") &&
            (rectangle.Attribute("y")?.Value is null or "0") &&
            rectangle.Attribute("width")?.Value == bounds[2] &&
            rectangle.Attribute("height")?.Value == bounds[3]); // no full-canvas background
        if (id.EndsWith("calha-com-acionamento"))
        {
            var pattern = Assert.Single(svg.Descendants(ns + "pattern"));
            Assert.Equal("rotate(45 0 0)", pattern.Attribute("patternTransform")!.Value);
            Assert.Equal("userSpaceOnUse", pattern.Attribute("patternUnits")!.Value);
        }
        if (id.EndsWith("valvula-manual-volante"))
        {
            Assert.Equal(2, svg.Descendants(ns + "linearGradient").Count());
            Assert.Equal(7, svg.Descendants(ns + "stop").Count());
        }
        if (id.EndsWith("botao-verde-cromado") || id.EndsWith("botao-vermelho-cromado"))
            Assert.Single(svg.Descendants(ns + "radialGradient"));
        if (id.EndsWith("clarificador-marrom") || id.EndsWith("tanque-limpo"))
            Assert.Single(svg.Descendants(ns + "pattern"));
        var asset = catalog.ProjectCopy(id)!.Value.Asset;
        Assert.True(VisualAssetClassification.IsLibraryArtwork(asset));
        Assert.False(VisualAssetClassification.IsDynamoArtwork(asset));
        Assert.False(VisualAssetClassification.IsUserAsset(asset));
        Assert.Equal(0, VisualAssetClassification.CountUserAssets([asset]));
        Assert.Equal(1, VisualAssetClassification.CountUserAssets([
            asset, asset with { Metadata = new Dictionary<string, string>() }
        ]));
    }

    [Fact]
    public void OriginalFactoryCatalogIsEmbeddedCategorizedAndNotFalselyApproved()
    {
        var catalog = new StaticArtworkCatalog();
        Assert.True(catalog.Entries.Count > 1200);
        Assert.True(catalog.Entries.Select(entry => entry.Category).Distinct().Count() > 20);
        Assert.All(catalog.Entries, entry => Assert.Equal("draft", entry.Status));
        Assert.Null(catalog.Content("../../secret"));
    }

    [Fact]
    public void EveryShippedFactoryPreviewPassesCanonicalStaticSvgSanitization()
    {
        var catalog = new StaticArtworkCatalog();
        foreach (var entry in catalog.Entries) {
            var content = catalog.Content(entry.Id);
            Assert.NotNull(content);
            Assert.NotEmpty(content);
        }
    }

    [Fact]
    public void ProjectCopyPreservesCategoryProvenanceAndCanonicalPaintMetadata()
    {
        var catalog = new StaticArtworkCatalog();
        var entry = catalog.Entries.First(item => item.Category == "industrial/rotating/motors");
        var copy = catalog.ProjectCopy(entry.Id)!.Value;
        Assert.Equal($"factory.{entry.Id}", copy.Asset.Key);
        Assert.Equal(entry.Category, copy.Asset.Metadata!["categoryPath"]);
        Assert.Equal(entry.Status, copy.Asset.Metadata["artworkReviewStatus"]);
        Assert.Equal(entry.Id, copy.Asset.Metadata["factoryArtworkId"]);
        Assert.False(copy.Asset.Metadata.ContainsKey("builtinLibrary"));
        var registry = new Scada.Engineering.VisualAssets.InMemoryVisualAssetEngineeringRegistry();
        var context = new Scada.Engineering.VisualAssets.EngineeringImportContext(
            new Dictionary<string, Scada.Engineering.VisualAssets.VisualAssetPayload> { [copy.Payload.Sha256] = copy.Payload });
        Assert.DoesNotContain(Scada.Engineering.VisualAssets.VisualAssetEngineeringValidator.Validate(copy.Asset, registry, context), issue => issue.IsError);
        Assert.Null(catalog.ProjectCopy("../../secret"));
    }
}
