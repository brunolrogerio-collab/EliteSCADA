using Scada.Engineering.VisualScripting;

namespace Scada.Engineering.Scripts;

/// <summary>
/// Maps isolated, versionable Script Engineering definitions into the merged PR #41 public runtime contracts.
/// This adapter deliberately exposes no renderer, DOM, filesystem, driver, database or arbitrary-network model.
/// </summary>
public static class ScriptEngineeringAdapters
{
    public static PythonScriptDefinition ToRuntimeDefinition(
        ScriptEngineeringDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new PythonScriptDefinition(
            definition.Id,
            definition.Path,
            definition.Name,
            ToRuntimeScope(definition.Scope),
            definition.Source,
            definition.Enabled,
            definition.Language,
            definition.LanguageVersion,
            definition.EntryPoints
                .Select(ToRuntimeEntryPoint)
                .ToArray(),
            definition.Dependencies
                .Select(ToRuntimeDependency)
                .ToArray(),
            definition.Metadata);
    }

    public static PythonScriptEntryPoint ToRuntimeEntryPoint(
        ScriptEngineeringEntryPoint entryPoint)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);

        return new PythonScriptEntryPoint(
            ToRuntimeEventKind(entryPoint.EventKind),
            entryPoint.HandlerName,
            entryPoint.TargetReference,
            entryPoint.TagReference,
            entryPoint.TimerIntervalMs);
    }

    public static PythonScriptDependency ToRuntimeDependency(
        ScriptEngineeringDependency dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);

        return new PythonScriptDependency(
            ToRuntimeDependencyKind(dependency.Kind),
            dependency.StableReference);
    }

    public static VisualScriptHandlerReference ToVisualHandler(
        ScriptVisualEventReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return new VisualScriptHandlerReference(
            ToRuntimeEventKind(reference.EventKind),
            reference.ScriptId,
            reference.EntryPoint);
    }

    public static IReadOnlyCollection<VisualScriptHandlerReference> GetVisualHandlers(
        ScriptEngineeringModel model,
        Guid visualDefinitionId,
        Guid? visualObjectId = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        return Array.AsReadOnly(model.VisualEventReferences
            .Where(reference =>
                reference.VisualDefinitionId == visualDefinitionId &&
                reference.VisualObjectId == visualObjectId)
            .OrderBy(reference => (int)reference.EventKind)
            .ThenBy(reference => reference.ScriptId)
            .ThenBy(reference => reference.EntryPoint, StringComparer.Ordinal)
            .ThenBy(reference => reference.TargetReference ?? string.Empty, StringComparer.Ordinal)
            .Select(ToVisualHandler)
            .ToArray());
    }

    public static PythonScriptScope ToRuntimeScope(
        ScriptEngineeringScope scope) =>
        scope switch
        {
            ScriptEngineeringScope.ClientVisual => PythonScriptScope.ClientVisual,
            ScriptEngineeringScope.Server => PythonScriptScope.Server,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported Script Engineering scope.")
        };

    public static PythonScriptEventKind ToRuntimeEventKind(
        ScriptEngineeringEventKind eventKind) =>
        eventKind switch
        {
            ScriptEngineeringEventKind.Initialize => PythonScriptEventKind.Initialize,
            ScriptEngineeringEventKind.Dispose => PythonScriptEventKind.Dispose,
            ScriptEngineeringEventKind.ObjectInteraction => PythonScriptEventKind.ObjectInteraction,
            ScriptEngineeringEventKind.TagChanged => PythonScriptEventKind.TagChanged,
            ScriptEngineeringEventKind.ClientMemoryChanged => PythonScriptEventKind.ClientMemoryChanged,
            ScriptEngineeringEventKind.Timer => PythonScriptEventKind.Timer,
            ScriptEngineeringEventKind.PropertyChanged => PythonScriptEventKind.PropertyChanged,
            ScriptEngineeringEventKind.FrameTick => PythonScriptEventKind.FrameTick,
            ScriptEngineeringEventKind.ServerRuntimeEvent => PythonScriptEventKind.ServerRuntimeEvent,
            _ => throw new ArgumentOutOfRangeException(nameof(eventKind), eventKind, "Unsupported Script Engineering event kind.")
        };

    private static string ToRuntimeDependencyKind(
        ScriptEngineeringDependencyKind kind) =>
        kind switch
        {
            ScriptEngineeringDependencyKind.Script => "script",
            ScriptEngineeringDependencyKind.VisualDefinition => "visual-definition",
            ScriptEngineeringDependencyKind.VisualObject => "visual-object",
            ScriptEngineeringDependencyKind.Tag => "tag",
            ScriptEngineeringDependencyKind.ClientMemoryTag => "client-memory-tag",
            ScriptEngineeringDependencyKind.ServerMemoryTag => "server-memory-tag",
            ScriptEngineeringDependencyKind.Resource => "resource",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported Script Engineering dependency kind.")
        };
}
