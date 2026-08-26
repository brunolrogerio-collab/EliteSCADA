using Scada.Engineering.Scripts;
using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class ScriptEngineeringFoundationTests
{
    [Fact]
    public void ValidClientScript_MapsToMergedRuntimeContractsAndVisualHandlers()
    {
        var visualDefinitionId = Guid.NewGuid();
        var visualObjectId = Guid.NewGuid();
        var scriptId = Guid.NewGuid();
        var runtimeVisual = CreateVisualRuntimeDefinition(visualDefinitionId, visualObjectId);
        var catalog = ScriptEngineeringReferenceCatalog.FromVisualRuntimeDefinitions([runtimeVisual]);

        var script = new ScriptEngineeringDefinition(
            scriptId,
            "screens/main/scripts/button",
            "Main button",
            ScriptEngineeringScope.ClientVisual,
            """
            def on_click():
                pass
            """,
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.ObjectInteraction,
                    "on_click",
                    "button")
            ],
            dependencies:
            [
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.VisualObject,
                    ScriptEngineeringReferenceKeys.VisualObject(visualDefinitionId, visualObjectId))
            ],
            description: "Handles the main visual button.");

        var model = new ScriptEngineeringModel(
            [script],
            [
                new ScriptVisualEventReference(
                    visualDefinitionId,
                    visualObjectId,
                    ScriptEngineeringEventKind.ObjectInteraction,
                    scriptId,
                    "on_click",
                    "button")
            ]);

        var result = new ScriptEngineeringValidator().Validate(model, catalog);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);

        var runtimeScript = ScriptEngineeringAdapters.ToRuntimeDefinition(script);
        Assert.Equal(scriptId, runtimeScript.Id);
        Assert.Equal(PythonScriptScope.ClientVisual, runtimeScript.Scope);
        Assert.Equal("python", runtimeScript.Language);
        Assert.Equal("3", runtimeScript.LanguageVersion);

        var entryPoint = Assert.Single(runtimeScript.EntryPoints);
        Assert.Equal(PythonScriptEventKind.ObjectInteraction, entryPoint.EventKind);
        Assert.Equal("on_click", entryPoint.HandlerName);
        Assert.Equal("button", entryPoint.TargetReference);

        var handler = Assert.Single(
            ScriptEngineeringAdapters.GetVisualHandlers(model, visualDefinitionId, visualObjectId));
        Assert.Equal(PythonScriptEventKind.ObjectInteraction, handler.EventKind);
        Assert.Equal(scriptId, handler.ScriptId);
        Assert.Equal("on_click", handler.EntryPoint);
    }

    [Fact]
    public void Validator_RejectsDuplicateStableIdentityAndPathDeterministically()
    {
        var duplicateId = Guid.NewGuid();
        var first = CreateScript(duplicateId, "scripts/duplicate", "First");
        var second = CreateScript(duplicateId, "scripts/duplicate", "Second");
        var validator = new ScriptEngineeringValidator();

        var forward = validator.Validate(new ScriptEngineeringModel([first, second]));
        var reversed = validator.Validate(new ScriptEngineeringModel([second, first]));

        Assert.False(forward.IsValid);
        Assert.Contains(forward.Issues, issue => issue.Code == "SCRIPT_ID_DUPLICATE");
        Assert.Contains(forward.Issues, issue => issue.Code == "SCRIPT_PATH_DUPLICATE");

        Assert.Equal(
            forward.Issues.Select(issue => (issue.Code, issue.EntityKey)),
            reversed.Issues.Select(issue => (issue.Code, issue.EntityKey)));
    }

    [Fact]
    public void Validator_RejectsInvalidScopeLanguageVersionSourceAndEntryPointMetadata()
    {
        var script = new ScriptEngineeringDefinition(
            Guid.Empty,
            "bad\\path",
            string.Empty,
            (ScriptEngineeringScope)999,
            string.Empty,
            language: "javascript",
            languageVersion: string.Empty,
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(
                    (ScriptEngineeringEventKind)999,
                    "1bad")
            ]);

        var result = new ScriptEngineeringValidator().Validate(
            new ScriptEngineeringModel([script]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_ID_REQUIRED");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_PATH_INVALID");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_NAME_REQUIRED");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_SCOPE_INVALID");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_LANGUAGE_INVALID");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_LANGUAGE_VERSION_REQUIRED");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_SOURCE_REQUIRED");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_ENTRYPOINT_EVENT_INVALID");
    }

    [Fact]
    public void Validator_RejectsSelfMissingCrossScopeAndForbiddenScopeDependencies()
    {
        var clientId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var missingId = Guid.NewGuid();

        var client = new ScriptEngineeringDefinition(
            clientId,
            "scripts/client",
            "Client",
            ScriptEngineeringScope.ClientVisual,
            "value = 1",
            dependencies:
            [
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.Script,
                    ScriptEngineeringReferenceKeys.Script(clientId)),
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.Script,
                    ScriptEngineeringReferenceKeys.Script(missingId)),
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.Script,
                    ScriptEngineeringReferenceKeys.Script(serverId)),
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.ServerMemoryTag,
                    ScriptEngineeringReferenceKeys.Tag(Guid.NewGuid()))
            ]);

        var server = CreateScript(
            serverId,
            "scripts/server",
            "Server",
            ScriptEngineeringScope.Server);

        var result = new ScriptEngineeringValidator().Validate(
            new ScriptEngineeringModel([client, server]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_SELF_REFERENCE");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_REFERENCE_MISSING");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_SCOPE_MISMATCH");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_SCOPE_INVALID");
    }

    [Fact]
    public void Validator_RejectsMissingVisualObjectScriptAndEntryPointReferences()
    {
        var visualDefinitionId = Guid.NewGuid();
        var knownObjectId = Guid.NewGuid();
        var missingObjectId = Guid.NewGuid();
        var scriptId = Guid.NewGuid();
        var missingScriptId = Guid.NewGuid();
        var runtimeVisual = CreateVisualRuntimeDefinition(visualDefinitionId, knownObjectId);
        var catalog = ScriptEngineeringReferenceCatalog.FromVisualRuntimeDefinitions([runtimeVisual]);
        var script = new ScriptEngineeringDefinition(
            scriptId,
            "screens/main/scripts/known",
            "Known",
            ScriptEngineeringScope.ClientVisual,
            "def on_click():\n    pass",
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.ObjectInteraction,
                    "on_click",
                    "known")
            ]);

        var model = new ScriptEngineeringModel(
            [script],
            [
                new ScriptVisualEventReference(
                    visualDefinitionId,
                    missingObjectId,
                    ScriptEngineeringEventKind.ObjectInteraction,
                    scriptId,
                    "missing_handler",
                    "missing"),
                new ScriptVisualEventReference(
                    visualDefinitionId,
                    knownObjectId,
                    ScriptEngineeringEventKind.ObjectInteraction,
                    missingScriptId,
                    "on_click",
                    "known")
            ]);

        var result = new ScriptEngineeringValidator().Validate(model, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_VISUAL_OBJECT_REFERENCE_MISSING");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_VISUAL_ENTRYPOINT_REFERENCE_INVALID");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_VISUAL_SCRIPT_REFERENCE_MISSING");
    }

    [Fact]
    public void Validator_ReusesPythonPreflightWithoutStartingConcreteInterpreterWork()
    {
        var script = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            "screens/main/scripts/unsafe",
            "Unsafe",
            ScriptEngineeringScope.ClientVisual,
            """
            value = 1
            import os
            """);

        var result = new ScriptEngineeringValidator().Validate(
            new ScriptEngineeringModel([script]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code == "SCRIPT_SOURCE_PY_SANDBOX_IMPORT_DENIED" &&
                issue.Message.Contains("line 2", StringComparison.Ordinal) &&
                issue.Message.Contains("column", StringComparison.Ordinal));
    }

    [Fact]
    public void Adapter_UsesStableRuntimeDependencyMarkers()
    {
        var dependency = new ScriptEngineeringDependency(
            ScriptEngineeringDependencyKind.ClientMemoryTag,
            ScriptEngineeringReferenceKeys.Tag(Guid.NewGuid()));

        var mapped = ScriptEngineeringAdapters.ToRuntimeDependency(dependency);

        Assert.Equal("client-memory-tag", mapped.Kind);
        Assert.Equal(dependency.StableReference, mapped.StableReference);
    }

    private static ScriptEngineeringDefinition CreateScript(
        Guid id,
        string path,
        string name,
        ScriptEngineeringScope scope = ScriptEngineeringScope.ClientVisual) =>
        new(
            id,
            path,
            name,
            scope,
            "value = 1");

    private static VisualRuntimeDefinition CreateVisualRuntimeDefinition(
        Guid definitionId,
        Guid objectId)
    {
        var schema = new VisualPropertySchemaBuilder("core.button")
            .Include(CommonVisualPropertyDefinitions.Geometry)
            .Include(CommonVisualPropertyDefinitions.Visibility)
            .Build();

        return new VisualRuntimeDefinition(
            definitionId,
            "screen.main",
            VisualRuntimeDefinitionKind.Screen,
            [
                new VisualObjectRuntimeDefinition(
                    objectId,
                    "button",
                    new VisualEngineeringPropertySet(schema))
            ]);
    }
}
