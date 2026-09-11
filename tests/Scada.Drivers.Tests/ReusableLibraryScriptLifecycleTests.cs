using Scada.Api.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Libraries;
using Scada.Engineering.Scripts;

namespace Scada.Drivers.Tests;

public sealed class ReusableLibraryScriptLifecycleTests
{
    [Fact]
    public async Task ScriptClosureIncorporation_MarksWorkingDirtyAndAdvancesChangeVersion()
    {
        using var workspace = new EngineeringWorkspace();
        workspace.SetCheckout("script-lifecycle", "Script Lifecycle", 1);
        var exchange = CreateExchange(workspace);
        var helperId = Guid.NewGuid();
        var rootId = Guid.NewGuid();

        var sourceScripts = new InMemoryScriptEngineeringRegistry();
        sourceScripts.Upsert(Script(helperId, "scripts/library/helper", "Helper"));
        sourceScripts.Upsert(Script(
            rootId,
            "scripts/library/root",
            "Root",
            [new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.Script,
                helperId.ToString("D"))]));
        var sourcePackages = new ReusableLibraryPackageService(
            new Scada.Engineering.Assets.InMemoryEngineeringAssetRegistry(),
            new Scada.Engineering.VisualAssets.InMemoryVisualAssetEngineeringRegistry(),
            sourceScripts);
        var libraryBytes = sourcePackages.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Script Lifecycle Library",
            "1.0.0",
            [new(ReusableLibraryResourceKinds.Script, rootId)]));

        var packages = new ReusableLibraryPackageService(
            workspace.Assets,
            workspace.VisualAssets,
            workspace.Scripts);
        var incorporation = new ReusableLibraryIncorporationService(
            packages,
            workspace.Assets,
            workspace.VisualAssets,
            exchange,
            workspace.Scripts);
        var before = workspace.Describe();
        Assert.False(before.IsDirty);

        var plan = incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(ReusableLibraryResourceKinds.Script, rootId));
        Assert.True(plan.RequiresMutation);
        Assert.Equal(2, plan.DependencyClosure.Count);

        await using (await workspace.AcquireMutationAsync(before.ChangeVersion))
        {
            var finalPreview = exchange.Preview(plan.Engineering, ImportMode.CreateOnly, plan.ImportContext);
            Assert.True(finalPreview.CanApply);
            var result = exchange.Apply(plan.Engineering, ImportMode.CreateOnly, plan.ImportContext);
            Assert.DoesNotContain(result.Issues, issue => issue.IsError);
            Assert.Equal(2, result.Created);
        }

        var after = workspace.Describe();
        Assert.True(after.IsDirty);
        Assert.True(after.ChangeVersion > before.ChangeVersion);
        Assert.NotNull(workspace.Scripts.Find(helperId));
        Assert.NotNull(workspace.Scripts.Find(rootId));

        var secondPlan = incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(ReusableLibraryResourceKinds.Script, rootId));
        Assert.False(secondPlan.RequiresMutation);
        Assert.Equal(2, secondPlan.DeduplicatedCount);
    }

    private static ScriptEngineeringDefinition Script(
        Guid id,
        string path,
        string name,
        IReadOnlyCollection<ScriptEngineeringDependency>? dependencies = null) =>
        new(
            id,
            path,
            name,
            ScriptEngineeringScope.Server,
            "def run():\n    return 1",
            dependencies: dependencies);

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
