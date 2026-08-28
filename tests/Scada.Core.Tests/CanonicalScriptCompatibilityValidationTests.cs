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

public sealed class CanonicalScriptCompatibilityValidationTests
{
    [Fact]
    public void Validation_IsDeterministicAcrossScriptEntryPointAndDependencyOrdering()
    {
        var firstId = Guid.Parse("81000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("81000000-0000-0000-0000-000000000002");
        var missingId = Guid.Parse("81000000-0000-0000-0000-000000000099");
        var serverMemoryId = Guid.Parse("82000000-0000-0000-0000-000000000001");
        var tagId = Guid.Parse("82000000-0000-0000-0000-000000000002");

        var entryPoints = new[]
        {
            new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Timer, "on_timer", "timer.fast"),
            new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Timer, "on_timer", "timer.fast"),
            new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Initialize, "1invalid")
        };
        var dependencies = new[]
        {
            new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.Script,
                ScriptEngineeringReferenceKeys.Script(secondId)),
            new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.Script,
                ScriptEngineeringReferenceKeys.Script(missingId)),
            new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.ServerMemoryTag,
                ScriptEngineeringReferenceKeys.Tag(serverMemoryId)),
            new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.Tag,
                ScriptEngineeringReferenceKeys.Tag(tagId)),
            new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.Tag,
                ScriptEngineeringReferenceKeys.Tag(tagId))
        };

        var firstForward = CreateScript(
            firstId,
            "scripts/client/a",
            ScriptEngineeringScope.ClientVisual,
            entryPoints,
            dependencies);
        var firstReordered = CreateScript(
            firstId,
            "scripts/client/a",
            ScriptEngineeringScope.ClientVisual,
            entryPoints.Reverse().ToArray(),
            dependencies.Reverse().ToArray());
        var second = CreateScript(
            secondId,
            "scripts/client/b",
            ScriptEngineeringScope.ClientVisual,
            dependencies:
            [
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.Script,
                    ScriptEngineeringReferenceKeys.Script(firstId))
            ]);

        var catalog = new ScriptEngineeringReferenceCatalog(
        [
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.ServerMemoryTag,
                ScriptEngineeringReferenceKeys.Tag(serverMemoryId)),
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.Tag,
                ScriptEngineeringReferenceKeys.Tag(tagId))
        ]);
        var validator = new ScriptEngineeringValidator();

        var forward = validator.Validate(
            new ScriptEngineeringModel([firstForward, second]),
            catalog);
        var reordered = validator.Validate(
            new ScriptEngineeringModel([second, firstReordered]),
            catalog);

        Assert.False(forward.IsValid);
        Assert.Equal(forward.Issues.ToArray(), reordered.Issues.ToArray());
        Assert.Contains(forward.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_CYCLE");
        Assert.Contains(forward.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_REFERENCE_MISSING");
        Assert.Contains(forward.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_SCOPE_INVALID");
        Assert.Contains(forward.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_DUPLICATE");
        Assert.Contains(forward.Issues, issue => issue.Code == "SCRIPT_ENTRYPOINT_DUPLICATE");
        Assert.Contains(forward.Issues, issue => issue.Code == "SCRIPT_ENTRYPOINT_HANDLER_INVALID");
    }

    [Fact]
    public void DependencyScopeMatrix_AcceptsPermittedReferencesAndRejectsCrossBoundaryReferences()
    {
        var tagId = Guid.Parse("83000000-0000-0000-0000-000000000001");
        var clientMemoryId = Guid.Parse("83000000-0000-0000-0000-000000000002");
        var serverMemoryId = Guid.Parse("83000000-0000-0000-0000-000000000003");
        var visualDefinitionId = Guid.Parse("83000000-0000-0000-0000-000000000004");
        var visualObjectId = Guid.Parse("83000000-0000-0000-0000-000000000005");
        var resourceId = Guid.Parse("83000000-0000-0000-0000-000000000006");

        var references = new[]
        {
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.Tag,
                ScriptEngineeringReferenceKeys.Tag(tagId)),
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.ClientMemoryTag,
                ScriptEngineeringReferenceKeys.Tag(clientMemoryId)),
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.ServerMemoryTag,
                ScriptEngineeringReferenceKeys.Tag(serverMemoryId)),
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.VisualDefinition,
                ScriptEngineeringReferenceKeys.VisualDefinition(visualDefinitionId)),
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.VisualObject,
                ScriptEngineeringReferenceKeys.VisualObject(visualDefinitionId, visualObjectId)),
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.Resource,
                ScriptEngineeringReferenceKeys.Resource(resourceId))
        };
        var catalog = new ScriptEngineeringReferenceCatalog(references);
        var validator = new ScriptEngineeringValidator();

        var validClient = CreateScript(
            Guid.Parse("84000000-0000-0000-0000-000000000001"),
            "scripts/client/valid",
            ScriptEngineeringScope.ClientVisual,
            dependencies:
            [
                Dependency(ScriptEngineeringDependencyKind.Tag, ScriptEngineeringReferenceKeys.Tag(tagId)),
                Dependency(ScriptEngineeringDependencyKind.ClientMemoryTag, ScriptEngineeringReferenceKeys.Tag(clientMemoryId)),
                Dependency(ScriptEngineeringDependencyKind.VisualDefinition, ScriptEngineeringReferenceKeys.VisualDefinition(visualDefinitionId)),
                Dependency(ScriptEngineeringDependencyKind.VisualObject, ScriptEngineeringReferenceKeys.VisualObject(visualDefinitionId, visualObjectId)),
                Dependency(ScriptEngineeringDependencyKind.Resource, ScriptEngineeringReferenceKeys.Resource(resourceId))
            ]);
        var validServer = CreateScript(
            Guid.Parse("84000000-0000-0000-0000-000000000002"),
            "scripts/server/valid",
            ScriptEngineeringScope.Server,
            dependencies:
            [
                Dependency(ScriptEngineeringDependencyKind.Tag, ScriptEngineeringReferenceKeys.Tag(tagId)),
                Dependency(ScriptEngineeringDependencyKind.ServerMemoryTag, ScriptEngineeringReferenceKeys.Tag(serverMemoryId)),
                Dependency(ScriptEngineeringDependencyKind.Resource, ScriptEngineeringReferenceKeys.Resource(resourceId))
            ]);

        Assert.True(validator.Validate(new ScriptEngineeringModel([validClient]), catalog).IsValid);
        Assert.True(validator.Validate(new ScriptEngineeringModel([validServer]), catalog).IsValid);

        var invalidClient = CreateScript(
            Guid.Parse("84000000-0000-0000-0000-000000000003"),
            "scripts/client/invalid",
            ScriptEngineeringScope.ClientVisual,
            dependencies:
            [
                Dependency(ScriptEngineeringDependencyKind.ServerMemoryTag, ScriptEngineeringReferenceKeys.Tag(serverMemoryId))
            ]);
        var invalidServer = CreateScript(
            Guid.Parse("84000000-0000-0000-0000-000000000004"),
            "scripts/server/invalid",
            ScriptEngineeringScope.Server,
            dependencies:
            [
                Dependency(ScriptEngineeringDependencyKind.ClientMemoryTag, ScriptEngineeringReferenceKeys.Tag(clientMemoryId)),
                Dependency(ScriptEngineeringDependencyKind.VisualDefinition, ScriptEngineeringReferenceKeys.VisualDefinition(visualDefinitionId)),
                Dependency(ScriptEngineeringDependencyKind.VisualObject, ScriptEngineeringReferenceKeys.VisualObject(visualDefinitionId, visualObjectId))
            ]);

        var invalidClientResult = validator.Validate(new ScriptEngineeringModel([invalidClient]), catalog);
        var invalidServerResult = validator.Validate(new ScriptEngineeringModel([invalidServer]), catalog);

        Assert.Single(invalidClientResult.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_SCOPE_INVALID");
        Assert.Equal(3, invalidServerResult.Issues.Count(issue => issue.Code == "SCRIPT_DEPENDENCY_SCOPE_INVALID"));
        Assert.DoesNotContain(invalidClientResult.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_REFERENCE_MISSING");
        Assert.DoesNotContain(invalidServerResult.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_REFERENCE_MISSING");
    }

    [Fact]
    public void SchemaV9_AppliesAndReExportsAsV10WithoutPhantomScripts()
    {
        using var harness = new Harness();
        const string legacyJson = """
            {
              "schema": "scada.engineering",
              "schemaVersion": 9,
              "exportedAt": "2026-08-27T00:00:00Z",
              "tags": [],
              "alarms": [],
              "dataSources": [],
              "templates": [],
              "equipment": [],
              "dynamos": [],
              "screens": [],
              "popups": [],
              "securityRoles": [],
              "commands": [],
              "gateways": []
            }
            """;

        var legacy = harness.Exchange.ParseJson(legacyJson);
        var preview = harness.Exchange.Preview(legacy, ImportMode.CreateAndUpdate);
        var apply = harness.Exchange.Apply(legacy, ImportMode.CreateAndUpdate);
        var migrated = harness.Exchange.ParseJson(harness.Exchange.ExportJson(indented: false));

        Assert.True(preview.CanApply);
        Assert.Empty(apply.Issues);
        Assert.Equal(10, migrated.SchemaVersion);
        Assert.NotNull(migrated.Scripts);
        Assert.Empty(migrated.Scripts!);
        Assert.NotNull(migrated.ScriptVisualEventReferences);
        Assert.Empty(migrated.ScriptVisualEventReferences!);
    }

    [Fact]
    public void ProjectPackage_PreservesFullScriptPayloadAcrossInspectApplyAndReExport()
    {
        using var source = new Harness();
        var libraryId = Guid.Parse("85000000-0000-0000-0000-000000000001");
        var actionId = Guid.Parse("85000000-0000-0000-0000-000000000002");
        var actionSource = "def on_load():\n    return \"ação ⚙\"\n";

        source.Scripts.Upsert(new ScriptEngineeringDefinition(
            libraryId,
            "scripts/client/library",
            "Library",
            ScriptEngineeringScope.ClientVisual,
            "value = 1\n",
            description: "Biblioteca compartilhada"));
        source.Scripts.Upsert(new ScriptEngineeringDefinition(
            actionId,
            "scripts/client/action",
            "Ação",
            ScriptEngineeringScope.ClientVisual,
            actionSource,
            enabled: false,
            language: "python",
            languageVersion: "3",
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Initialize, "on_load")
            ],
            dependencies:
            [
                Dependency(
                    ScriptEngineeringDependencyKind.Script,
                    ScriptEngineeringReferenceKeys.Script(libraryId))
            ],
            description: "Descrição Unicode Ω",
            metadata: new Dictionary<string, string>
            {
                ["owner"] = "DEV 3",
                ["purpose"] = "compatibilidade"
            }));

        var packageService = new ProjectPackageService(source.Exchange);
        var bytes = packageService.Export("script-compatibility", "Script Compatibility");
        var inspection = packageService.Inspect(bytes);
        var inspected = inspection.Engineering.Scripts!.Single(script => script.Id == actionId);

        Assert.Equal(actionSource, inspected.Source);
        Assert.False(inspected.Enabled);
        Assert.Equal("python", inspected.Language);
        Assert.Equal("3", inspected.LanguageVersion);
        Assert.Equal("Descrição Unicode Ω", inspected.Description);
        Assert.Equal("DEV 3", inspected.Metadata["owner"]);
        Assert.Equal("compatibilidade", inspected.Metadata["purpose"]);
        Assert.Equal(
            new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Initialize, "on_load"),
            Assert.Single(inspected.EntryPoints));
        Assert.Equal(
            Dependency(ScriptEngineeringDependencyKind.Script, ScriptEngineeringReferenceKeys.Script(libraryId)),
            Assert.Single(inspected.Dependencies));

        using var target = new Harness();
        var targetPackages = new ProjectPackageService(target.Exchange);
        Assert.True(targetPackages.Preview(bytes, ImportMode.CreateAndUpdate).CanApply);
        Assert.Empty(targetPackages.Apply(bytes, ImportMode.CreateAndUpdate).Issues);

        var reExported = target.Exchange.ParseJson(target.Exchange.ExportJson(indented: false));
        var restored = reExported.Scripts!.Single(script => script.Id == actionId);

        Assert.Equal(inspected.Id, restored.Id);
        Assert.Equal(inspected.Path, restored.Path);
        Assert.Equal(inspected.Name, restored.Name);
        Assert.Equal(inspected.Scope, restored.Scope);
        Assert.Equal(inspected.Source, restored.Source);
        Assert.Equal(inspected.Enabled, restored.Enabled);
        Assert.Equal(inspected.Language, restored.Language);
        Assert.Equal(inspected.LanguageVersion, restored.LanguageVersion);
        Assert.Equal(inspected.EntryPoints, restored.EntryPoints);
        Assert.Equal(inspected.Dependencies, restored.Dependencies);
        Assert.Equal(inspected.Description, restored.Description);
        Assert.Equal(inspected.Metadata.OrderBy(pair => pair.Key), restored.Metadata.OrderBy(pair => pair.Key));
    }

    private static ScriptEngineeringDefinition CreateScript(
        Guid id,
        string path,
        ScriptEngineeringScope scope,
        IReadOnlyCollection<ScriptEngineeringEntryPoint>? entryPoints = null,
        IReadOnlyCollection<ScriptEngineeringDependency>? dependencies = null) =>
        new(
            id,
            path,
            path,
            scope,
            "value = 1",
            entryPoints: entryPoints,
            dependencies: dependencies);

    private static ScriptEngineeringDependency Dependency(
        ScriptEngineeringDependencyKind kind,
        string stableReference) =>
        new(kind, stableReference);

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

        public void Dispose() => Alarms.Dispose();
    }
}
