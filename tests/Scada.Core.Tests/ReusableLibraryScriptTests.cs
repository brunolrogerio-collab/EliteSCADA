using System.IO.Compression;
using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Libraries;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class ReusableLibraryScriptTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Export_RootScriptIncludesTransitiveScriptClosure()
    {
        var scripts = new InMemoryScriptEngineeringRegistry();
        var helperId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        scripts.Upsert(Script(helperId, "scripts/lib/helper", "Helper"));
        scripts.Upsert(Script(
            rootId,
            "scripts/lib/root",
            "Root",
            dependencies:
            [new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.Script,
                helperId.ToString("D"))]));

        var service = new ReusableLibraryPackageService(
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryVisualAssetEngineeringRegistry(),
            scripts);
        var bytes = service.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "Script Library",
            "1.0.0",
            [new(ReusableLibraryResourceKinds.Script, rootId)]));

        var inspection = service.Inspect(bytes);
        Assert.Equal(2, inspection.Manifest.Resources.Count);
        var root = Assert.Single(inspection.Manifest.Resources, resource => resource.ResourceId == rootId);
        var helper = Assert.Single(inspection.Manifest.Resources, resource => resource.ResourceId == helperId);
        Assert.Equal(ReusableLibraryResourceKinds.Script, root.Kind);
        Assert.Equal(ReusableLibraryResourceKinds.Script, helper.Kind);
        var dependency = Assert.Single(root.Dependencies);
        Assert.Equal(ReusableLibraryResourceKinds.Script, dependency.Kind);
        Assert.Equal(helperId, dependency.ResourceId);
        Assert.Empty(helper.Dependencies);
    }

    [Fact]
    public void Export_RejectsProjectOwnedDependencyAndVisualAssociation()
    {
        var scripts = new InMemoryScriptEngineeringRegistry();
        var dependencyBound = Script(
            Guid.NewGuid(),
            "scripts/lib/tag-bound",
            "Tag Bound",
            dependencies:
            [new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.Tag,
                Guid.NewGuid().ToString("D"))]);
        scripts.Upsert(dependencyBound);

        var service = new ReusableLibraryPackageService(
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryVisualAssetEngineeringRegistry(),
            scripts);
        var dependencyException = Assert.Throws<InvalidDataException>(() =>
            service.Export(new ReusableLibraryExportRequest(
                Guid.NewGuid(),
                "Script Library",
                "1",
                [new(ReusableLibraryResourceKinds.Script, dependencyBound.Id)])));
        Assert.Contains("project-owned", dependencyException.Message, StringComparison.OrdinalIgnoreCase);

        var visualBound = Script(
            Guid.NewGuid(),
            "scripts/lib/visual-bound",
            "Visual Bound",
            entryPoints: [new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Initialize, "run")]);
        scripts.Upsert(visualBound);
        scripts.ReplaceVisualEventReferences(
            visualBound.Id,
            [new ScriptVisualEventReference(
                Guid.NewGuid(),
                null,
                ScriptEngineeringEventKind.Initialize,
                visualBound.Id,
                "run")]);

        var visualException = Assert.Throws<InvalidDataException>(() =>
            service.Export(new ReusableLibraryExportRequest(
                Guid.NewGuid(),
                "Script Library",
                "1",
                [new(ReusableLibraryResourceKinds.Script, visualBound.Id)])));
        Assert.Contains("project HMI event associations", visualException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_RejectsConcreteEventTargetsAndDependencyCycles()
    {
        var scripts = new InMemoryScriptEngineeringRegistry();
        var clientMemory = Script(
            Guid.NewGuid(),
            "scripts/lib/client-memory",
            "Client Memory",
            scope: ScriptEngineeringScope.ClientVisual,
            entryPoints:
            [new ScriptEngineeringEntryPoint(
                ScriptEngineeringEventKind.ClientMemoryChanged,
                "run",
                TargetReference: Guid.NewGuid().ToString("D"))]);
        scripts.Upsert(clientMemory);

        var service = new ReusableLibraryPackageService(
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryVisualAssetEngineeringRegistry(),
            scripts);
        var targetException = Assert.Throws<InvalidDataException>(() =>
            service.Export(new ReusableLibraryExportRequest(
                Guid.NewGuid(),
                "Script Library",
                "1",
                [new(ReusableLibraryResourceKinds.Script, clientMemory.Id)])));
        Assert.Contains("not portable", targetException.Message, StringComparison.OrdinalIgnoreCase);

        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        scripts.Upsert(Script(
            aId,
            "scripts/lib/a",
            "A",
            dependencies:
            [new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.Script, bId.ToString("D"))]));
        scripts.Upsert(Script(
            bId,
            "scripts/lib/b",
            "B",
            dependencies:
            [new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.Script, aId.ToString("D"))]));

        var cycleException = Assert.Throws<InvalidDataException>(() =>
            service.Export(new ReusableLibraryExportRequest(
                Guid.NewGuid(),
                "Script Library",
                "1",
                [new(ReusableLibraryResourceKinds.Script, aId)])));
        Assert.Contains("cycle", cycleException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Incorporation_IsIdempotent_AndProjectPackageRestoresWithoutLibrary()
    {
        var sourceScripts = new InMemoryScriptEngineeringRegistry();
        var helperId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        sourceScripts.Upsert(Script(
            helperId,
            "scripts/lib/helper",
            "Helper",
            metadata: new Dictionary<string, string> { ["role"] = "helper" }));
        sourceScripts.Upsert(Script(
            rootId,
            "scripts/lib/root",
            "Root",
            dependencies:
            [new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.Script, helperId.ToString("D"))],
            metadata: new Dictionary<string, string> { ["role"] = "root" }));

        var libraryId = Guid.NewGuid();
        var sourcePackages = new ReusableLibraryPackageService(
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryVisualAssetEngineeringRegistry(),
            sourceScripts);
        var libraryBytes = sourcePackages.Export(new ReusableLibraryExportRequest(
            libraryId,
            "Portable Scripts",
            "3.2.1",
            [new(ReusableLibraryResourceKinds.Script, rootId)]));
        var libraryInspection = sourcePackages.Inspect(libraryBytes);

        using var target = CreateTarget();
        var firstPlan = target.Incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(ReusableLibraryResourceKinds.Script, rootId));
        Assert.True(firstPlan.RequiresMutation);
        Assert.Equal(2, firstPlan.DependencyClosure.Count);
        Assert.Equal(2, firstPlan.Engineering.Scripts!.Count);
        var firstResult = target.Exchange.Apply(firstPlan.Engineering, ImportMode.CreateOnly, firstPlan.ImportContext);
        Assert.DoesNotContain(firstResult.Issues, issue => issue.IsError);
        Assert.Equal(2, firstResult.Created);

        AssertOrigin(target.Scripts.Find(helperId)!, libraryInspection.Manifest, helperId);
        AssertOrigin(target.Scripts.Find(rootId)!, libraryInspection.Manifest, rootId);

        var secondPlan = target.Incorporation.Plan(
            libraryBytes,
            new ReusableLibraryIncorporationSelection(ReusableLibraryResourceKinds.Script, rootId));
        Assert.False(secondPlan.RequiresMutation);
        Assert.Equal(2, secondPlan.DeduplicatedCount);
        Assert.Equal(0, secondPlan.Preview.CreateCount);

        var projectPackages = new ProjectPackageService(target.Exchange, target.VisualAssets);
        var projectBytes = projectPackages.Export("script-library-roundtrip", "Script Library Roundtrip");

        using var restored = CreateTarget();
        var restoredPackages = new ProjectPackageService(restored.Exchange, restored.VisualAssets);
        var preview = restoredPackages.Preview(projectBytes, ImportMode.CreateOnly);
        Assert.True(preview.CanApply);
        var result = restoredPackages.Apply(projectBytes, ImportMode.CreateOnly);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        Assert.NotNull(restored.Scripts.Find(helperId));
        Assert.NotNull(restored.Scripts.Find(rootId));
        AssertOrigin(restored.Scripts.Find(rootId)!, libraryInspection.Manifest, rootId);
    }

    [Fact]
    public void Reexport_StripsPreviousLibraryOriginButKeepsOrdinaryMetadata()
    {
        var scripts = new InMemoryScriptEngineeringRegistry();
        var id = Guid.NewGuid();
        scripts.Upsert(Script(
            id,
            "scripts/lib/reexport",
            "Re-export",
            metadata: new Dictionary<string, string>
            {
                ["owner"] = "engineering",
                [ReusableLibraryProvenance.LibraryIdKey] = Guid.NewGuid().ToString("D"),
                [ReusableLibraryProvenance.LibraryVersionKey] = "old",
                [ReusableLibraryProvenance.ResourceIdKey] = id.ToString("D"),
                [ReusableLibraryProvenance.ResourceKindKey] = ReusableLibraryResourceKinds.Script,
                [ReusableLibraryProvenance.PayloadSha256Key] = new string('a', 64)
            }));

        var service = new ReusableLibraryPackageService(
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryVisualAssetEngineeringRegistry(),
            scripts);
        var bytes = service.Export(new ReusableLibraryExportRequest(
            Guid.NewGuid(),
            "New Origin",
            "1",
            [new(ReusableLibraryResourceKinds.Script, id)]));
        var inspection = service.Inspect(bytes);
        var resource = Assert.Single(inspection.Manifest.Resources);

        using var input = new MemoryStream(bytes);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        var entry = Assert.NotNull(archive.GetEntry(resource.PayloadPath));
        using var stream = entry.Open();
        var exported = JsonSerializer.Deserialize<ScriptEngineeringDefinition>(stream, Json);
        Assert.NotNull(exported);
        Assert.Equal("engineering", exported!.Metadata["owner"]);
        Assert.DoesNotContain(exported.Metadata.Keys, ReusableLibraryProvenance.IsOriginKey);
    }

    private static ScriptEngineeringDefinition Script(
        Guid id,
        string path,
        string name,
        ScriptEngineeringScope scope = ScriptEngineeringScope.Server,
        IReadOnlyCollection<ScriptEngineeringEntryPoint>? entryPoints = null,
        IReadOnlyCollection<ScriptEngineeringDependency>? dependencies = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new(
            id,
            path,
            name,
            scope,
            "def run():\n    return 1",
            entryPoints: entryPoints,
            dependencies: dependencies,
            metadata: metadata);

    private static void AssertOrigin(
        ScriptEngineeringDefinition script,
        ReusableLibraryManifest manifest,
        Guid resourceId)
    {
        var resource = Assert.Single(
            manifest.Resources,
            entry => entry.Kind == ReusableLibraryResourceKinds.Script && entry.ResourceId == resourceId);
        var payload = Assert.Single(manifest.Files, file => file.Path == resource.PayloadPath);
        Assert.Equal(manifest.LibraryId.ToString("D"), script.Metadata[ReusableLibraryProvenance.LibraryIdKey]);
        Assert.Equal(manifest.Version, script.Metadata[ReusableLibraryProvenance.LibraryVersionKey]);
        Assert.Equal(resourceId.ToString("D"), script.Metadata[ReusableLibraryProvenance.ResourceIdKey]);
        Assert.Equal(ReusableLibraryResourceKinds.Script, script.Metadata[ReusableLibraryProvenance.ResourceKindKey]);
        Assert.Equal(payload.Sha256.ToLowerInvariant(), script.Metadata[ReusableLibraryProvenance.PayloadSha256Key]);
    }

    private static TargetHarness CreateTarget()
    {
        var eventBus = new InMemoryScadaEventBus();
        var tags = new InMemoryTagRegistry();
        var alarms = new InMemoryAlarmEngine(eventBus);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var assets = new InMemoryEngineeringAssetRegistry();
        var views = new InMemoryEngineeringViewRegistry();
        var security = new InMemorySecurityPolicyEngineeringRegistry();
        var commands = new InMemoryCommandEngineeringRegistry();
        var gateways = new InMemoryGatewayEngineeringRegistry();
        var scripts = new InMemoryScriptEngineeringRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var exchange = new EngineeringExchangeService(
            tags,
            alarms,
            dataSources,
            assets,
            views,
            security,
            commands,
            gateways,
            scripts,
            visualAssets);
        var packages = new ReusableLibraryPackageService(assets, visualAssets, scripts);
        var incorporation = new ReusableLibraryIncorporationService(
            packages,
            assets,
            visualAssets,
            exchange,
            scripts);
        return new TargetHarness(alarms, scripts, visualAssets, exchange, incorporation);
    }

    private sealed class TargetHarness(
        InMemoryAlarmEngine alarms,
        InMemoryScriptEngineeringRegistry scripts,
        InMemoryVisualAssetEngineeringRegistry visualAssets,
        EngineeringExchangeService exchange,
        ReusableLibraryIncorporationService incorporation) : IDisposable
    {
        public InMemoryScriptEngineeringRegistry Scripts { get; } = scripts;
        public InMemoryVisualAssetEngineeringRegistry VisualAssets { get; } = visualAssets;
        public EngineeringExchangeService Exchange { get; } = exchange;
        public ReusableLibraryIncorporationService Incorporation { get; } = incorporation;

        public void Dispose() => alarms.Dispose();
    }
}
