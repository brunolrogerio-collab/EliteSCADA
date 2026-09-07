using Scada.Api.Runtime;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Libraries;
using Scada.Engineering.Scripts;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Drivers.Tests;

public sealed class ReusableLibraryViewLifecycleTests
{
    [Fact]
    public async Task ScreenClosureIncorporation_MarksWorkingDirtyAdvancesVersion_AndRededuplicates()
    {
        using var workspace = new EngineeringWorkspace();
        workspace.SetCheckout("view-lifecycle", "View Lifecycle", 1);
        var exchange = CreateExchange(workspace);
        var dynamoId = Guid.NewGuid();
        var screenId = Guid.NewGuid();

        var sourceAssets = new InMemoryEngineeringAssetRegistry();
        var sourceViews = new InMemoryEngineeringViewRegistry();
        var sourceScripts = new InMemoryScriptEngineeringRegistry();
        var sourceVisualAssets = new InMemoryVisualAssetEngineeringRegistry();
        sourceAssets.UpsertDynamo(new DynamoEngineeringDto(
            dynamoId,
            "dynamo.library.lifecycle",
            "Lifecycle Dynamo"));
        sourceViews.UpsertScreen(new ScreenEngineeringDto(
            screenId,
            "screen.library.lifecycle",
            "Lifecycle Screen",
            Route: "/library-lifecycle",
            Elements:
            [new VisualElementEngineeringDto(
                "dynamo",
                "dynamo",
                DynamoKey: "dynamo.library.lifecycle")]));

        var sourcePackages = new ReusableLibraryPackageService(
            sourceAssets,
            sourceVisualAssets,
            sourceScripts,
            sourceViews);
        var libraryBytes = sourcePackages.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "View Lifecycle Library",
            "1.0.0",
            [new(ReusableLibraryResourceKinds.Screen, screenId)]));

        var packages = new ReusableLibraryPackageService(
            workspace.Assets,
            workspace.VisualAssets,
            workspace.Scripts,
            workspace.Views);
        var incorporation = new ReusableLibraryIncorporationService(
            packages,
            workspace.Assets,
            workspace.VisualAssets,
            exchange,
            workspace.Scripts,
            workspace.Views);

        var before = workspace.Describe();
        Assert.False(before.IsDirty);

        var plan = incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.Screen,
                screenId));
        Assert.True(plan.RequiresMutation);
        Assert.Equal(2, plan.DependencyClosure.Count);

        await using (await workspace.AcquireMutationAsync(before.ChangeVersion))
        {
            var finalPreview = exchange.Preview(
                plan.Engineering,
                ImportMode.CreateOnly,
                plan.ImportContext);
            Assert.True(finalPreview.CanApply);

            var result = exchange.Apply(
                plan.Engineering,
                ImportMode.CreateOnly,
                plan.ImportContext);
            Assert.DoesNotContain(result.Issues, issue => issue.IsError);
            Assert.Equal(2, result.Created);
        }

        var after = workspace.Describe();
        Assert.True(after.IsDirty);
        Assert.True(after.ChangeVersion > before.ChangeVersion);
        Assert.NotNull(workspace.Assets.FindDynamo(dynamoId));
        Assert.NotNull(workspace.Views.FindScreen(screenId));

        var secondPlan = incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(
                ReusableLibraryResourceKinds.Screen,
                screenId));
        Assert.False(secondPlan.RequiresMutation);
        Assert.Equal(2, secondPlan.DeduplicatedCount);
        Assert.Equal(0, secondPlan.Preview.CreateCount);

        await using (await workspace.AcquireMutationAsync(after.ChangeVersion))
        {
            Assert.False(secondPlan.RequiresMutation);
        }

        var afterDeduplication = workspace.Describe();
        Assert.Equal(after.ChangeVersion, afterDeduplication.ChangeVersion);
        Assert.True(afterDeduplication.IsDirty);
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
}
