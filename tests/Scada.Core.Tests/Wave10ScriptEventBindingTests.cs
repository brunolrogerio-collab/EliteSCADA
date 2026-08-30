using Scada.Core.Tags;
using Scada.Engineering.Scripts;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class Wave10ScriptEventBindingTests
{
    [Fact]
    public void Validator_AcceptsCanonicalTimerAndTagBitTargets()
    {
        var scriptId = Guid.Parse("91000000-0000-0000-0000-000000000001");
        var screenId = Guid.Parse("91000000-0000-0000-0000-000000000002");
        var objectId = Guid.Parse("91000000-0000-0000-0000-000000000003");
        var tagId = Guid.Parse("91000000-0000-0000-0000-000000000004");
        var tagReference = new TagValueReference(tagId, new TagValueSelector(TagValueSelectorKind.Bit, 7));

        var script = new ScriptEngineeringDefinition(
            scriptId,
            "scripts/client/wave10",
            "Wave 10",
            ScriptEngineeringScope.ClientVisual,
            "def on_timer():\n    pass\n\ndef on_tag():\n    pass\n",
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.Timer,
                    "on_timer",
                    TimerIntervalMs: 250),
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.TagChanged,
                    "on_tag",
                    TagReference: tagReference)
            ]);

        var model = new ScriptEngineeringModel(
            [script],
            [
                new ScriptVisualEventReference(
                    screenId,
                    null,
                    ScriptEngineeringEventKind.Timer,
                    scriptId,
                    "on_timer",
                    TimerIntervalMs: 250),
                new ScriptVisualEventReference(
                    screenId,
                    objectId,
                    ScriptEngineeringEventKind.TagChanged,
                    scriptId,
                    "on_tag",
                    TagReference: tagReference)
            ]);

        var catalog = new ScriptEngineeringReferenceCatalog(
        [
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.VisualDefinition,
                ScriptEngineeringReferenceKeys.VisualDefinition(screenId)),
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.VisualObject,
                ScriptEngineeringReferenceKeys.VisualObject(screenId, objectId)),
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.Tag,
                ScriptEngineeringReferenceKeys.Tag(tagId))
        ]);

        var result = new ScriptEngineeringValidator().Validate(model, catalog);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
    }

    [Fact]
    public void Validator_FailsClosedForInvalidTimerAndStringEncodedTagIdentity()
    {
        var scriptId = Guid.Parse("92000000-0000-0000-0000-000000000001");
        var screenId = Guid.Parse("92000000-0000-0000-0000-000000000002");
        var objectId = Guid.Parse("92000000-0000-0000-0000-000000000003");

        var script = new ScriptEngineeringDefinition(
            scriptId,
            "scripts/client/invalid-wave10",
            "Invalid Wave 10",
            ScriptEngineeringScope.ClientVisual,
            "def on_timer():\n    pass\n\ndef on_tag():\n    pass\n",
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.Timer,
                    "on_timer",
                    TimerIntervalMs: 49),
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.TagChanged,
                    "on_tag",
                    TargetReference: "Plant.Pump.Word.07")
            ]);

        var model = new ScriptEngineeringModel(
            [script],
            [
                new ScriptVisualEventReference(
                    screenId,
                    null,
                    ScriptEngineeringEventKind.Timer,
                    scriptId,
                    "on_timer",
                    TimerIntervalMs: 49),
                new ScriptVisualEventReference(
                    screenId,
                    objectId,
                    ScriptEngineeringEventKind.TagChanged,
                    scriptId,
                    "on_tag",
                    TargetReference: "Plant.Pump.Word.07")
            ]);

        var catalog = new ScriptEngineeringReferenceCatalog(
        [
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.VisualDefinition,
                ScriptEngineeringReferenceKeys.VisualDefinition(screenId)),
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.VisualObject,
                ScriptEngineeringReferenceKeys.VisualObject(screenId, objectId))
        ]);

        var result = new ScriptEngineeringValidator().Validate(model, catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_ENTRYPOINT_TIMER_INTERVAL_INVALID");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_VISUAL_TIMER_INTERVAL_INVALID");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_ENTRYPOINT_TAG_REFERENCE_REQUIRED");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_ENTRYPOINT_TARGET_REFERENCE_UNEXPECTED");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_VISUAL_TAG_REFERENCE_REQUIRED");
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_VISUAL_TARGET_REFERENCE_UNEXPECTED");
    }

    [Fact]
    public void Adapter_PreservesCanonicalTagSelectorAndTimerInterval()
    {
        var tagId = Guid.Parse("93000000-0000-0000-0000-000000000001");
        var timer = new ScriptEngineeringEntryPoint(
            ScriptEngineeringEventKind.Timer,
            "on_timer",
            TimerIntervalMs: 500);
        var tag = new ScriptEngineeringEntryPoint(
            ScriptEngineeringEventKind.TagChanged,
            "on_tag",
            TagReference: new TagValueReference(tagId, new TagValueSelector(TagValueSelectorKind.Bit, 12)));

        var runtimeTimer = ScriptEngineeringAdapters.ToRuntimeEntryPoint(timer);
        var runtimeTag = ScriptEngineeringAdapters.ToRuntimeEntryPoint(tag);

        Assert.Equal(500, runtimeTimer.TimerIntervalMs);
        Assert.Null(runtimeTimer.TargetReference);
        Assert.Null(runtimeTimer.TagReference);

        Assert.NotNull(runtimeTag.TagReference);
        Assert.Equal(tagId, runtimeTag.TagReference!.TagId);
        Assert.NotNull(runtimeTag.TagReference.Selector);
        Assert.Equal(TagValueSelectorKind.Bit, runtimeTag.TagReference.Selector!.Kind);
        Assert.Equal(12, runtimeTag.TagReference.Selector.Index);
        Assert.Null(runtimeTag.TargetReference);
        Assert.Null(runtimeTag.TimerIntervalMs);
    }

    [Fact]
    public void RuntimeAdapter_RegistersPersistedTimerIntervalAndFailsClosedBelowPolicyMinimum()
    {
        var scriptId = Guid.Parse("93500000-0000-0000-0000-000000000001");
        var validTimer = new PythonScriptEntryPoint(
            PythonScriptEventKind.Timer,
            "on_timer",
            TimerIntervalMs: 250);
        var script = new PythonScriptDefinition(
            scriptId,
            "scripts/client/runtime-timer",
            "Runtime Timer",
            PythonScriptScope.ClientVisual,
            "def on_timer():\n    pass\n",
            entryPoints: [validTimer]);
        using var registry = new ScriptEventSubscriptionRegistry("runtime-1");

        var subscription = ScriptEngineeringAdapters.RegisterRuntimeSubscription(
            registry,
            script,
            validTimer,
            ScriptExecutionPolicy.SafeDefault);

        Assert.Equal(TimeSpan.FromMilliseconds(250), subscription.TimerInterval);
        Assert.Equal(1, registry.Count);

        var invalidTimer = validTimer with { TimerIntervalMs = 49 };
        var invalidScript = new PythonScriptDefinition(
            Guid.Parse("93500000-0000-0000-0000-000000000002"),
            "scripts/client/runtime-timer-invalid",
            "Runtime Timer Invalid",
            PythonScriptScope.ClientVisual,
            "def on_timer():\n    pass\n",
            entryPoints: [invalidTimer]);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScriptEngineeringAdapters.RegisterRuntimeSubscription(
                registry,
                invalidScript,
                invalidTimer,
                ScriptExecutionPolicy.SafeDefault));
    }

    [Fact]
    public void RuntimeAdapter_RejectsStringEncodedTagIdentity()
    {
        var entryPoint = new PythonScriptEntryPoint(
            PythonScriptEventKind.TagChanged,
            "on_tag",
            TargetReference: "Plant.Pump.Word.07");
        var script = new PythonScriptDefinition(
            Guid.Parse("93600000-0000-0000-0000-000000000001"),
            "scripts/client/runtime-tag-invalid",
            "Runtime TAG Invalid",
            PythonScriptScope.ClientVisual,
            "def on_tag():\n    pass\n",
            entryPoints: [entryPoint]);
        using var registry = new ScriptEventSubscriptionRegistry("runtime-2");

        Assert.Throws<InvalidOperationException>(() =>
            ScriptEngineeringAdapters.RegisterRuntimeSubscription(
                registry,
                script,
                entryPoint,
                ScriptExecutionPolicy.SafeDefault));
    }

    [Fact]
    public void Validator_RequiresStableClientMemoryDefinitionIdentity()
    {
        var scriptId = Guid.Parse("94000000-0000-0000-0000-000000000001");
        var screenId = Guid.Parse("94000000-0000-0000-0000-000000000002");
        var objectId = Guid.Parse("94000000-0000-0000-0000-000000000003");
        var memoryId = Guid.Parse("94000000-0000-0000-0000-000000000004");
        var stableMemoryReference = memoryId.ToString("D");

        var script = new ScriptEngineeringDefinition(
            scriptId,
            "scripts/client/memory",
            "Memory",
            ScriptEngineeringScope.ClientVisual,
            "def on_memory():\n    pass\n",
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.ClientMemoryChanged,
                    "on_memory",
                    stableMemoryReference)
            ]);

        var model = new ScriptEngineeringModel(
            [script],
            [
                new ScriptVisualEventReference(
                    screenId,
                    objectId,
                    ScriptEngineeringEventKind.ClientMemoryChanged,
                    scriptId,
                    "on_memory",
                    stableMemoryReference)
            ]);

        var catalog = new ScriptEngineeringReferenceCatalog(
        [
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.VisualDefinition,
                ScriptEngineeringReferenceKeys.VisualDefinition(screenId)),
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.VisualObject,
                ScriptEngineeringReferenceKeys.VisualObject(screenId, objectId)),
            new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.ClientMemoryTag,
                stableMemoryReference)
        ]);

        Assert.True(new ScriptEngineeringValidator().Validate(model, catalog).IsValid);
    }
}
