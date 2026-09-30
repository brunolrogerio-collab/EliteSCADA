using System.Text;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Branding;
using Scada.Engineering.Views;
using Scada.Engineering.Security;
using Scada.Engineering.Gateways;
using Scada.Engineering.Commands;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.VisualAssets;
using Xunit;

namespace Scada.Core.Tests;

public sealed class ApplicationBrandingEngineeringTests
{
    [Fact]
    public void Text_branding_round_trips_through_canonical_engineering_package()
    {
        var sourceBranding = new InMemoryApplicationBrandingEngineeringRegistry();
        sourceBranding.Replace(new ApplicationBrandingEngineeringDto(
            ApplicationBrandingMode.Text,
            "Plant North",
            "Operations"));

        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var source = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            sourceAlarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry(),
            new InMemoryGatewayEngineeringRegistry(),
            branding: sourceBranding);

        var json = source.ExportJson(indented: false);
        var package = source.ParseJson(json);

        Assert.NotNull(package.Branding);
        Assert.Equal(ApplicationBrandingMode.Text, package.Branding!.Mode);
        Assert.Equal("Plant North", package.Branding.Text);

        var targetBranding = new InMemoryApplicationBrandingEngineeringRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var target = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            targetAlarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry(),
            new InMemoryGatewayEngineeringRegistry(),
            branding: targetBranding);

        var result = target.Apply(package, ImportMode.CreateAndUpdate);

        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        Assert.Equal(package.Branding, targetBranding.Snapshot());
    }

    [Fact]
    public void Image_branding_requires_stable_existing_visual_asset_identity()
    {
        var assets = new InMemoryVisualAssetEngineeringRegistry();
        var missingId = Guid.Parse("4d6b6ab7-7296-4ddd-b426-0aa733b49676");

        var issues = ApplicationBrandingEngineeringValidator.Validate(
            new ApplicationBrandingEngineeringDto(
                ApplicationBrandingMode.Image,
                VisualAssetId: missingId),
            assets);

        Assert.Contains(issues, issue => issue.Code == "BRANDING_IMAGE_ASSET_NOT_FOUND" && issue.IsError);
    }

    [Fact]
    public void Safe_static_svg_is_canonicalized_and_active_content_is_rejected()
    {
        var safe = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><!--remove--><rect height=\"20\" width=\"10\" fill=\"#fff\"/></svg>");

        var first = VisualAssetContentInspector.InspectAndCanonicalize(safe);
        var second = VisualAssetContentInspector.InspectAndCanonicalize(first.CanonicalContent);

        Assert.Equal(VisualAssetContentInspector.SvgMediaType, first.MediaType);
        Assert.Equal(first.CanonicalContent, second.CanonicalContent);
        Assert.DoesNotContain("<!--", Encoding.UTF8.GetString(first.CanonicalContent));

        var active = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");
        Assert.Throws<InvalidDataException>(() => VisualAssetContentInspector.InspectAndCanonicalize(active));

        var external = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><use href=\"https://example.invalid/a.svg#x\"/></svg>");
        Assert.Throws<InvalidDataException>(() => VisualAssetContentInspector.InspectAndCanonicalize(external));
    }
}
