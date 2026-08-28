using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class CanonicalScriptEngineeringTests
{
    [Fact]
    public void CurrentSchema_JsonRoundTripPreservesCanonicalScriptsAndVisualReferences()
    {
        using var source = CreateHarnessWithScripts();

        var json = source.Exchange.ExportJson(indented: false);
        var package = source.Exchange.ParseJson(json);

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, package.SchemaVersion);
        Assert.Equal(2, package.Scripts!.Count);
        Assert.Single(package.ScriptVisualEventReferences!);

        var action = package.Scripts.Single(script => script.Path == "scripts/client/action");
        Assert.False(action.Enabled);
        Assert.Equal(ScriptEngineeringScope.ClientVisual, action.Scope);
        Assert.Contains("on_load", action.Source, StringComparison.Ordinal);
        Assert.Contains(action.Dependencies, dependency =>
            dependency.Kind == ScriptEngineeringDependencyKind.Script &&
            dependency.StableReference == ScriptEngineeringReferenceKeys.Script(source.LibraryScriptId));
        Assert.Contains(action.EntryPoints, entryPoint =>
            entryPoint.EventKind == ScriptEngineeringEventKind.Initialize &&
            entryPoint.HandlerName == "on_load");

        using var target = new Harness();
        var preview = target.Exchange.Preview(package, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);
        Assert.Equal(2, preview.Items.Count(item => item.EntityKind == ImportEntityKind.Script));

        var result = target.Exchange.Apply(package, ImportMode.CreateAndUpdate);
        Assert.Empty(result.Issues);
        Assert.Equal(2, target.Scripts.SnapshotScripts().Count);
        Assert.Single(target.Scripts.SnapshotVisualEventReferences());

        var restored = target.Scripts.Find(source.ActionScriptId);
        Assert.NotNull(restored);
        Assert.Equal(action.Source, restored!.Source);
        Assert.Equal(action.Enabled, restored.Enabled);
        Assert.Equal(action.Dependencies, restored.Dependencies);
    }

    [Fact]
    public void SchemaV9_WithoutScriptsRemainsBackwardCompatible()
    {
        using var harness = new Harness();
        const string json = """
            {
              "schema": "scada.engineering",
              "schemaVersion": 9,
              "exportedAt": "2026-08-27T00:00:00Z",
              "tags": [],
              "alarms": []
            }
            """;

        var package = harness.Exchange.ParseJson(json);

        Assert.Equal(9, package.SchemaVersion);
        Assert.NotNull(package.Scripts);
        Assert.Empty(package.Scripts!);
        Assert.NotNull(package.ScriptVisualEventReferences);
        Assert.Empty(package.ScriptVisualEventReferences!);
        Assert.True(harness.Exchange.Preview(package, ImportMode.CreateAndUpdate).CanApply);
    }

    [Fact]
    public void ProjectPackage_PreservesCanonicalScriptsAndAppliesThroughPreview()
    {
        using var source = CreateHarnessWithScripts();
        var sourcePackages = new ProjectPackageService(source.Exchange);
        var bytes = sourcePackages.Export("script-project", "Script Project");

        using var target = new Harness();
        var targetPackages = new ProjectPackageService(target.Exchange);
        var inspection = targetPackages.Inspect(bytes);

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, inspection.Manifest.EngineeringSchemaVersion);
        Assert.Equal(2, inspection.Engineering.Scripts!.Count);
        Assert.Single(inspection.Engineering.ScriptVisualEventReferences!);

        var preview = targetPackages.Preview(bytes, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);

        var result = targetPackages.Apply(bytes, ImportMode.CreateAndUpdate);
        Assert.Empty(result.Issues);
        Assert.Equal(2, target.Scripts.SnapshotScripts().Count);
        Assert.Single(target.Scripts.SnapshotVisualEventReferences());
    }

    [Fact]
    public void Preview_RejectsScriptPathOwnedByDifferentStableId()
    {
        using var harness = new Harness();
        var existing = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            "scripts/client/shared",
            "Existing",
            ScriptEngineeringScope.ClientVisual,
            "value = 1");
        harness.Scripts.Upsert(existing);

        var incoming = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            existing.Path,
            "Incoming",
            ScriptEngineeringScope.ClientVisual,
            "value = 2");
        var package = EmptyPackage() with { Scripts = [incoming] };

        var preview = harness.Exchange.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue =>
            issue.Code is "SCRIPT_PATH_OWNED_BY_DIFFERENT_ID" or "SCRIPT_PATH_DUPLICATE");
    }

    [Fact]
    public void Registry_RemoveReleasesStablePathAndOwnedVisualReferences()
    {
        var changed = 0;
        var registry = new InMemoryScriptEngineeringRegistry(() => changed++);
        var scriptId = Guid.NewGuid();
        var screenId = Guid.NewGuid();
        var script = new ScriptEngineeringDefinition(
            scriptId,
            "scripts/client/removable",
            "Removable",
            ScriptEngineeringScope.ClientVisual,
            "def on_load():\n    pass",
            entryPoints: [new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Initialize, "on_load")]);

        registry.Upsert(script);
        registry.ReplaceVisualEventReferences(
            scriptId,
            [new ScriptVisualEventReference(
                screenId,
                null,
                ScriptEngineeringEventKind.Initialize,
                scriptId,
                "on_load")]);

        Assert.True(registry.Remove(scriptId));
        Assert.Null(registry.Find(scriptId));
        Assert.Null(registry.FindByPath(script.Path));
        Assert.Empty(registry.SnapshotVisualEventReferences());
        Assert.Equal(3, changed);

        registry.Upsert(new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            script.Path,
            "Replacement",
            ScriptEngineeringScope.ClientVisual,
            "value = 2"));
        Assert.NotNull(registry.FindByPath(script.Path));
    }

    private static Harness CreateHarnessWithScripts()
    {
        var harness = new Harness();
        var screenId = Guid.Parse("71000000-0000-0000-0000-000000000001");
        harness.Views.UpsertScreen(new ScreenEngineeringDto(
            screenId,
            "screen.main",
            "Main",
            Route: "/main"));

        harness.LibraryScriptId = Guid.Parse("72000000-0000-0000-0000-000000000001");
        harness.ActionScriptId = Guid.Parse("72000000-0000-0000-0000-000000000002");

        harness.Scripts.Upsert(new ScriptEngineeringDefinition(
            harness.LibraryScriptId,
            "scripts/client/library",
            "Library",
            ScriptEngineeringScope.ClientVisual,
            "value = 1",
            description: "Shared helper"));

        harness.Scripts.Upsert(new ScriptEngineeringDefinition(
            harness.ActionScriptId,
            "scripts/client/action",
            "Action",
            ScriptEngineeringScope.ClientVisual,
            "def on_load():\n    return 1",
            enabled: false,
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.Initialize,
                    "on_load")
            ],
            dependencies:
            [
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.Script,
                    ScriptEngineeringReferenceKeys.Script(harness.LibraryScriptId))
            ],
            metadata: new Dictionary<string, string> { ["owner"] = "wave05" }));

        harness.Scripts.ReplaceVisualEventReferences(
            harness.ActionScriptId,
            [
                new ScriptVisualEventReference(
                    screenId,
                    null,
                    ScriptEngineeringEventKind.Initialize,
                    harness.ActionScriptId,
                    "on_load")
            ]);

        return harness;
    }

    private static EngineeringPackage EmptyPackage() => new(
        EngineeringExchangeService.CurrentSchema,
        EngineeringExchangeService.CurrentSchemaVersion,
        DateTimeOffset.UtcNow,
        Array.Empty<TagEngineeringDto>(),
        Array.Empty<AlarmEngineeringDto>());

    private sealed class Harness : IDisposable
    {
        private readonly InMemoryScadaEventBus _eventBus = new();

        public Harness()
        {
            Tags = new InMemoryTagRegistry();
            Alarms = new InMemoryAlarmEngine(_eventBus);
            DataSources = new InMemoryDataSourceEngineeringRegistry();
            Assets = new InMemoryEngineeringAssetRegistry();
            Views = new InMemoryEngineeringViewRegistry();
            Security = new InMemorySecurityPolicyEngineeringRegistry();
            Commands = new InMemoryCommandEngineeringRegistry();
            Gateways = new InMemoryGatewayEngineeringRegistry();
            Scripts = new InMemoryScriptEngineeringRegistry();
            Exchange = new EngineeringExchangeService(
                Tags,
                Alarms,
                DataSources,
                Assets,
                Views,
                Security,
                Commands,
                Gateways,
                Scripts);
        }

        public Guid LibraryScriptId { get; set; }
        public Guid ActionScriptId { get; set; }
        public InMemoryTagRegistry Tags { get; }
        public InMemoryAlarmEngine Alarms { get; }
        public InMemoryDataSourceEngineeringRegistry DataSources { get; }
        public InMemoryEngineeringAssetRegistry Assets { get; }
        public InMemoryEngineeringViewRegistry Views { get; }
        public InMemorySecurityPolicyEngineeringRegistry Security { get; }
        public InMemoryCommandEngineeringRegistry Commands { get; }
        public InMemoryGatewayEngineeringRegistry Gateways { get; }
        public InMemoryScriptEngineeringRegistry Scripts { get; }
        public EngineeringExchangeService Exchange { get; }

        public void Dispose()
        {
            Alarms.Dispose();
        }
    }
}
