using Microsoft.AspNetCore.Http;
using Scada.Api.Libraries;
using Scada.Api.Runtime;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Libraries;
using Scada.Engineering.VisualAssets;

namespace Scada.Drivers.Tests;

public sealed class ReusableLibraryIncorporationLifecycleTests
{
    [Fact]
    public async Task CanonicalIncorporation_MarksWorkingDirtyAndAdvancesChangeVersion()
    {
        using var workspace = new EngineeringWorkspace();
        var exchange = CreateExchange(workspace);
        var templateId = Guid.NewGuid();
        var libraryBytes = CreateTemplateLibrary(new EquipmentTemplateEngineeringDto(
            templateId,
            "library.lifecycle.template",
            "Lifecycle Template"));
        var incorporation = new ReusableLibraryIncorporationService(
            new ReusableLibraryPackageService(workspace.Assets, workspace.VisualAssets),
            workspace.Assets,
            workspace.VisualAssets,
            exchange);
        var before = workspace.Describe();
        Assert.False(before.IsDirty);

        var plan = incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.EquipmentTemplate,
                templateId));
        Assert.True(plan.RequiresMutation);

        await using (await workspace.AcquireMutationAsync(before.ChangeVersion))
        {
            var finalPreview = exchange.Preview(plan.Engineering, ImportMode.CreateOnly, plan.ImportContext);
            Assert.True(finalPreview.CanApply);
            var result = exchange.Apply(plan.Engineering, ImportMode.CreateOnly, plan.ImportContext);
            Assert.Empty(result.Issues.Where(issue => issue.IsError));
            Assert.Equal(1, result.Created);
        }

        var after = workspace.Describe();
        Assert.True(after.IsDirty);
        Assert.True(after.ChangeVersion > before.ChangeVersion);
        Assert.Equal(templateId, workspace.Assets.FindTemplate(templateId)!.Id);
    }

    [Fact]
    public async Task DeduplicatedIncorporation_ValidatesVersionButDoesNotDirtyWorking()
    {
        using var workspace = new EngineeringWorkspace();
        var exchange = CreateExchange(workspace);
        var templateId = Guid.NewGuid();
        var template = new EquipmentTemplateEngineeringDto(
            templateId,
            "library.lifecycle.same",
            "Same Lifecycle Template");
        workspace.Assets.UpsertTemplate(template);
        workspace.SetCheckout("demo", "Demo Project", 1);
        var before = workspace.Describe();
        Assert.False(before.IsDirty);

        var incorporation = new ReusableLibraryIncorporationService(
            new ReusableLibraryPackageService(workspace.Assets, workspace.VisualAssets),
            workspace.Assets,
            workspace.VisualAssets,
            exchange);
        var plan = incorporation.Plan(
            CreateTemplateLibrary(template),
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.EquipmentTemplate,
                templateId));
        Assert.False(plan.RequiresMutation);
        Assert.Equal(1, plan.DeduplicatedCount);

        await using (await workspace.AcquireMutationAsync(before.ChangeVersion))
        {
            Assert.False(plan.RequiresMutation);
        }

        var after = workspace.Describe();
        Assert.False(after.IsDirty);
        Assert.Equal(before.ChangeVersion, after.ChangeVersion);
        Assert.Equal(before.BaseRevision, after.BaseRevision);
    }

    [Theory]
    [InlineData("0", true, 0L)]
    [InlineData("42", true, 42L)]
    [InlineData("-1", false, 0L)]
    [InlineData("abc", false, 0L)]
    [InlineData("", false, 0L)]
    public void WorkspaceVersionHeader_IsStrictlyParsed(string value, bool expectedSuccess, long expectedVersion)
    {
        var context = new DefaultHttpContext();
        if (value.Length > 0)
            context.Request.Headers["x-elitescada-workspace-version"] = value;

        var success = ReusableLibraryIncorporationEndpoints.TryReadExpectedVersion(
            context.Request,
            out var version);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedVersion, version);
    }

    [Fact]
    public void IncorporationRoute_IsNotEngineeringLockWorkspaceReadExempt()
    {
        var context = new DefaultHttpContext();
        context.Request.Path =
            "/api/engineering/libraries/00000000-0000-0000-0000-000000000001/resources/00000000-0000-0000-0000-000000000002/incorporate";

        Assert.False(Scada.Api.Security.EngineeringLockAccess.IsWorkspaceReadExempt(context.Request));
    }

    private static EngineeringExchangeService CreateExchange(EngineeringWorkspace workspace) =>
        new(
            workspace.Tags,
            workspace.Alarms,
            workspace.DataSources,
            workspace.Assets,
            workspace.Views,
            workspace.SecurityPolicies,
            workspace.Commands,
            new InMemoryGatewayEngineeringRegistry(workspace.MarkDirty),
            workspace.Scripts,
            workspace.VisualAssets);

    private static byte[] CreateTemplateLibrary(EquipmentTemplateEngineeringDto template)
    {
        var sourceAssets = new InMemoryEngineeringAssetRegistry();
        var sourceVisualAssets = new InMemoryVisualAssetEngineeringRegistry();
        sourceAssets.UpsertTemplate(template);
        return new ReusableLibraryPackageService(sourceAssets, sourceVisualAssets).Export(
            new ReusableLibraryExportRequest(
                Guid.NewGuid(),
                "Lifecycle Library",
                "1.0.0",
                [new(ReusableLibraryResourceKinds.EquipmentTemplate, template.Id!.Value)]));
    }
}
