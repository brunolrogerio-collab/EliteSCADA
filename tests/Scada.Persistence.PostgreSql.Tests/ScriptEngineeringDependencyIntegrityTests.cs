using Scada.Engineering.Scripts;
using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class ScriptEngineeringDependencyIntegrityTests
{
    [Fact]
    public void Validator_RejectsIndirectScriptDependencyCycles()
    {
        var firstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var thirdId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var first = CreateScript(
            firstId,
            "scripts/a",
            new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.Script,
                ScriptEngineeringReferenceKeys.Script(secondId)));
        var second = CreateScript(
            secondId,
            "scripts/b",
            new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.Script,
                ScriptEngineeringReferenceKeys.Script(thirdId)));
        var third = CreateScript(
            thirdId,
            "scripts/c",
            new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.Script,
                ScriptEngineeringReferenceKeys.Script(firstId)));

        var result = new ScriptEngineeringValidator().Validate(
            new ScriptEngineeringModel([third, first, second]));

        var cycle = Assert.Single(
            result.Issues,
            issue => issue.Code == "SCRIPT_DEPENDENCY_CYCLE");
        Assert.Equal(firstId, cycle.ScriptId);
        Assert.Equal("scripts/a", cycle.EntityKey);
        Assert.Contains("scripts/a -> scripts/b -> scripts/c -> scripts/a", cycle.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualReferences_CannotCollapseDistinctTargetsIntoSameRuntimeHandler()
    {
        var visualDefinitionId = Guid.NewGuid();
        var visualObjectId = Guid.NewGuid();
        var scriptId = Guid.NewGuid();
        var visual = CreateVisualRuntimeDefinition(visualDefinitionId, visualObjectId);
        var catalog = ScriptEngineeringReferenceCatalog.FromVisualRuntimeDefinitions([visual]);
        var script = new ScriptEngineeringDefinition(
            scriptId,
            "screens/main/scripts/pointer",
            "Pointer",
            ScriptEngineeringScope.ClientVisual,
            "def on_pointer():\n    pass",
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.ObjectInteraction,
                    "on_pointer",
                    "primary"),
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.ObjectInteraction,
                    "on_pointer",
                    "secondary")
            ]);
        var model = new ScriptEngineeringModel(
            [script],
            [
                new ScriptVisualEventReference(
                    visualDefinitionId,
                    visualObjectId,
                    ScriptEngineeringEventKind.ObjectInteraction,
                    scriptId,
                    "on_pointer",
                    "primary"),
                new ScriptVisualEventReference(
                    visualDefinitionId,
                    visualObjectId,
                    ScriptEngineeringEventKind.ObjectInteraction,
                    scriptId,
                    "on_pointer",
                    "secondary")
            ]);

        var result = new ScriptEngineeringValidator().Validate(model, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "SCRIPT_VISUAL_REFERENCE_DUPLICATE");
    }

    [Fact]
    public void DisabledState_IsPreservedByRuntimeAdapter()
    {
        var script = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            "scripts/disabled",
            "Disabled",
            ScriptEngineeringScope.ClientVisual,
            "value = 1",
            enabled: false);

        var runtime = ScriptEngineeringAdapters.ToRuntimeDefinition(script);

        Assert.False(runtime.Enabled);
    }

    [Fact]
    public void ServerScript_RejectsClientVisualEvent()
    {
        var script = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            "scripts/server-invalid-event",
            "Server invalid event",
            ScriptEngineeringScope.Server,
            "def on_click():\n    pass",
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.ObjectInteraction,
                    "on_click")
            ]);

        var result = new ScriptEngineeringValidator().Validate(
            new ScriptEngineeringModel([script]));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "SCRIPT_ENTRYPOINT_SCOPE_EVENT_INVALID");
    }

    private static ScriptEngineeringDefinition CreateScript(
        Guid id,
        string path,
        ScriptEngineeringDependency dependency) =>
        new(
            id,
            path,
            path,
            ScriptEngineeringScope.ClientVisual,
            "value = 1",
            dependencies: [dependency]);

    private static VisualRuntimeDefinition CreateVisualRuntimeDefinition(
        Guid definitionId,
        Guid objectId)
    {
        var schema = new VisualPropertySchemaBuilder("core.button")
            .Include(CommonVisualPropertyDefinitions.Geometry)
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
