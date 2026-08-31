using Scada.Core.Tags;
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

    /// <summary>
    /// Registers one runtime subscription from the canonical fields already carried by the
    /// runtime entry point. No duration or TAG identity is reconstructed from display text.
    /// </summary>
    public static ScriptEventSubscription RegisterRuntimeSubscription(
        ScriptEventSubscriptionRegistry registry,
        PythonScriptDefinition script,
        PythonScriptEntryPoint entryPoint,
        ScriptExecutionPolicy policy,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(policy);

        ValidateRuntimeEventTarget(entryPoint, policy);

        var timerInterval = entryPoint.EventKind == PythonScriptEventKind.Timer
            ? TimeSpan.FromMilliseconds(entryPoint.TimerIntervalMs!.Value)
            : (TimeSpan?)null;

        return registry.Register(script, entryPoint, policy, timerInterval, createdAt);
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

    private static void ValidateRuntimeEventTarget(
        PythonScriptEntryPoint entryPoint,
        ScriptExecutionPolicy policy)
    {
        if (entryPoint.EventKind == PythonScriptEventKind.Timer)
        {
            if (!entryPoint.TimerIntervalMs.HasValue)
                throw new InvalidOperationException("Timer entry point requires canonical timerIntervalMs.");

            var interval = TimeSpan.FromMilliseconds(entryPoint.TimerIntervalMs.Value);
            if (interval < policy.MinimumTimerInterval)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(entryPoint),
                    entryPoint.TimerIntervalMs,
                    $"Timer interval cannot be shorter than {policy.MinimumTimerInterval}.");
            }

            if (entryPoint.TagReference is not null)
                throw new InvalidOperationException("Timer entry point cannot carry a TAG reference.");

            if (!string.IsNullOrWhiteSpace(entryPoint.TargetReference))
                throw new InvalidOperationException("Timer duration must not be encoded in TargetReference.");

            return;
        }

        if (entryPoint.TimerIntervalMs.HasValue)
            throw new InvalidOperationException("timerIntervalMs is only valid for Timer entry points.");

        if (entryPoint.EventKind == PythonScriptEventKind.TagChanged)
        {
            ValidateRuntimeTagReference(entryPoint.TagReference);
            if (!string.IsNullOrWhiteSpace(entryPoint.TargetReference))
                throw new InvalidOperationException("TAG value-change identity must not be encoded in TargetReference.");
            return;
        }

        if (entryPoint.TagReference is not null)
            throw new InvalidOperationException("Canonical TAG target is only valid for TAG value-change entry points.");

        if (entryPoint.EventKind == PythonScriptEventKind.ClientMemoryChanged &&
            string.IsNullOrWhiteSpace(entryPoint.TargetReference))
        {
            throw new InvalidOperationException(
                "Client Memory change entry point requires a stable definition ID in TargetReference.");
        }
    }

    private static void ValidateRuntimeTagReference(TagValueReference? reference)
    {
        if (reference is null || reference.TagId == Guid.Empty)
            throw new InvalidOperationException("TAG value-change entry point requires a stable TAG reference.");

        if (reference.Selector is not { } selector)
            return;

        if (!Enum.IsDefined(typeof(TagValueSelectorKind), selector.Kind) ||
            selector.Kind != TagValueSelectorKind.Bit ||
            selector.Index < 0)
        {
            throw new InvalidOperationException(
                "TAG value-change selector must be a valid zero-based canonical bit selector.");
        }
    }

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
