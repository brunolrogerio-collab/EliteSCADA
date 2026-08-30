using Scada.Core.Tags;
using Scada.Engineering.VisualScripting;

namespace Scada.Engineering.Scripts;

public sealed record ScriptEngineeringReference(
    ScriptEngineeringDependencyKind Kind,
    string StableReference);

public sealed class ScriptEngineeringReferenceCatalog
{
    private readonly HashSet<string> _references;

    public ScriptEngineeringReferenceCatalog(
        IEnumerable<ScriptEngineeringReference>? references = null)
    {
        _references = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reference in references ?? Array.Empty<ScriptEngineeringReference>())
        {
            if (string.IsNullOrWhiteSpace(reference.StableReference))
                continue;

            _references.Add(ToCatalogKey(reference.Kind, reference.StableReference));
        }
    }

    public bool Contains(
        ScriptEngineeringDependencyKind kind,
        string stableReference)
    {
        if (string.IsNullOrWhiteSpace(stableReference))
            return false;

        return _references.Contains(ToCatalogKey(kind, stableReference));
    }

    public static ScriptEngineeringReferenceCatalog FromVisualRuntimeDefinitions(
        IEnumerable<VisualRuntimeDefinition> definitions,
        IEnumerable<ScriptEngineeringReference>? additionalReferences = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var references = new List<ScriptEngineeringReference>();

        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);

            references.Add(new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.VisualDefinition,
                ScriptEngineeringReferenceKeys.VisualDefinition(definition.Id)));

            foreach (var visualObject in definition.ObjectsById.Values)
            {
                references.Add(new ScriptEngineeringReference(
                    ScriptEngineeringDependencyKind.VisualObject,
                    ScriptEngineeringReferenceKeys.VisualObject(definition.Id, visualObject.Id)));
            }
        }

        if (additionalReferences is not null)
            references.AddRange(additionalReferences);

        return new ScriptEngineeringReferenceCatalog(references);
    }

    private static string ToCatalogKey(
        ScriptEngineeringDependencyKind kind,
        string stableReference) =>
        $"{(int)kind}:{stableReference}";
}

public sealed record ScriptEngineeringValidationIssue(
    string Code,
    string Message,
    bool IsError,
    Guid? ScriptId = null,
    string? EntityKey = null);

public sealed class ScriptEngineeringValidationResult
{
    public ScriptEngineeringValidationResult(
        IReadOnlyCollection<ScriptEngineeringValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        Issues = Array.AsReadOnly(issues
            .OrderBy(issue => issue.EntityKey ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(issue => issue.ScriptId)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyCollection<ScriptEngineeringValidationIssue> Issues { get; }

    public bool IsValid => Issues.All(issue => !issue.IsError);
}

public sealed class ScriptEngineeringValidator
{
    private static readonly int MinimumTimerIntervalMs = checked((int)ScriptExecutionPolicy.SafeDefault.MinimumTimerInterval.TotalMilliseconds);

    public ScriptEngineeringValidationResult Validate(
        ScriptEngineeringModel model,
        ScriptEngineeringReferenceCatalog? referenceCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var issues = new List<ScriptEngineeringValidationIssue>();
        var scripts = model.Scripts.ToArray();

        ValidateDuplicateScriptIdentity(scripts, issues);
        ValidateDuplicateScriptPaths(scripts, issues);

        var scriptsById = scripts
            .Where(script => script.Id != Guid.Empty)
            .GroupBy(script => script.Id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var script in scripts
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Id))
        {
            ValidateScript(script, scriptsById, referenceCatalog, issues);
        }

        var nonEmptyIdCount = scripts.Count(script => script.Id != Guid.Empty);
        if (scriptsById.Count == nonEmptyIdCount)
            ValidateScriptDependencyCycles(scriptsById, issues);

        ValidateVisualReferences(model, scriptsById, referenceCatalog, issues);

        return new ScriptEngineeringValidationResult(issues);
    }

    private static void ValidateDuplicateScriptIdentity(
        IReadOnlyCollection<ScriptEngineeringDefinition> scripts,
        ICollection<ScriptEngineeringValidationIssue> issues)
    {
        foreach (var duplicate in scripts
            .Where(script => script.Id != Guid.Empty)
            .GroupBy(script => script.Id)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key))
        {
            issues.Add(new ScriptEngineeringValidationIssue(
                "SCRIPT_ID_DUPLICATE",
                $"Script stable ID '{duplicate.Key:D}' is declared more than once.",
                true,
                duplicate.Key,
                duplicate.First().Path));
        }
    }

    private static void ValidateDuplicateScriptPaths(
        IReadOnlyCollection<ScriptEngineeringDefinition> scripts,
        ICollection<ScriptEngineeringValidationIssue> issues)
    {
        foreach (var duplicate in scripts
            .Where(script => !string.IsNullOrWhiteSpace(script.Path))
            .GroupBy(script => script.Path, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            issues.Add(new ScriptEngineeringValidationIssue(
                "SCRIPT_PATH_DUPLICATE",
                $"Script path '{duplicate.Key}' is declared more than once.",
                true,
                duplicate.First().Id == Guid.Empty ? null : duplicate.First().Id,
                duplicate.Key));
        }
    }

    private static void ValidateScript(
        ScriptEngineeringDefinition script,
        IReadOnlyDictionary<Guid, ScriptEngineeringDefinition> scriptsById,
        ScriptEngineeringReferenceCatalog? referenceCatalog,
        ICollection<ScriptEngineeringValidationIssue> issues)
    {
        var entityKey = string.IsNullOrWhiteSpace(script.Path)
            ? script.Id.ToString("D")
            : script.Path;
        Guid? scriptId = script.Id == Guid.Empty ? null : script.Id;

        if (script.Id == Guid.Empty)
            Add("SCRIPT_ID_REQUIRED", "Script stable ID is required.");

        if (string.IsNullOrWhiteSpace(script.Path))
        {
            Add("SCRIPT_PATH_REQUIRED", "Script path is required.");
        }
        else if (script.Path != script.Path.Trim() || script.Path.Contains('\\'))
        {
            Add("SCRIPT_PATH_INVALID", "Script path must be trimmed and use '/' separators.");
        }

        if (string.IsNullOrWhiteSpace(script.Name))
            Add("SCRIPT_NAME_REQUIRED", "Script name is required.");

        if (!Enum.IsDefined(typeof(ScriptEngineeringScope), script.Scope))
            Add("SCRIPT_SCOPE_INVALID", $"Script scope '{script.Scope}' is not supported.");

        if (!string.Equals(script.Language, "python", StringComparison.OrdinalIgnoreCase))
        {
            Add(
                "SCRIPT_LANGUAGE_INVALID",
                $"Script language '{script.Language}' is not supported. The current contract requires Python.");
        }

        if (string.IsNullOrWhiteSpace(script.LanguageVersion))
            Add("SCRIPT_LANGUAGE_VERSION_REQUIRED", "Python language version marker is required.");

        if (string.IsNullOrWhiteSpace(script.Source))
            Add("SCRIPT_SOURCE_REQUIRED", "Python source is required.");

        ValidateEntryPoints(script, scriptId, entityKey, issues);
        ValidateDependencies(script, scriptsById, referenceCatalog, scriptId, entityKey, issues);

        if (CanMapToRuntime(script))
        {
            var runtimeDefinition = ScriptEngineeringAdapters.ToRuntimeDefinition(script);
            var preflight = new PythonPreflightValidator().Validate(runtimeDefinition);

            foreach (var diagnostic in preflight.Diagnostics
                .Where(diagnostic => diagnostic.Severity == PythonDiagnosticSeverity.Error))
            {
                issues.Add(new ScriptEngineeringValidationIssue(
                    $"SCRIPT_SOURCE_{diagnostic.Code}",
                    $"{diagnostic.Message} (line {diagnostic.Span.Start.Line}, column {diagnostic.Span.Start.Column}).",
                    true,
                    scriptId,
                    entityKey));
            }
        }

        void Add(string code, string message) =>
            issues.Add(new ScriptEngineeringValidationIssue(code, message, true, scriptId, entityKey));
    }

    private static void ValidateEntryPoints(
        ScriptEngineeringDefinition script,
        Guid? scriptId,
        string entityKey,
        ICollection<ScriptEngineeringValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entryPoint in script.EntryPoints
            .OrderBy(item => (int)item.EventKind)
            .ThenBy(item => item.HandlerName, StringComparer.Ordinal)
            .ThenBy(item => EventTargetIdentity(item.TargetReference, item.TagReference, item.TimerIntervalMs), StringComparer.Ordinal))
        {
            if (!Enum.IsDefined(typeof(ScriptEngineeringEventKind), entryPoint.EventKind))
            {
                Add(
                    "SCRIPT_ENTRYPOINT_EVENT_INVALID",
                    $"Entry point event '{entryPoint.EventKind}' is not supported.");
                continue;
            }

            if (!IsPythonIdentifier(entryPoint.HandlerName))
            {
                Add(
                    "SCRIPT_ENTRYPOINT_HANDLER_INVALID",
                    $"Entry point handler '{entryPoint.HandlerName}' is not a valid Python identifier.");
            }

            ValidateEventTarget(
                entryPoint.EventKind,
                entryPoint.TargetReference,
                entryPoint.TagReference,
                entryPoint.TimerIntervalMs,
                (code, message) => Add($"SCRIPT_ENTRYPOINT_{code}", message));

            if (Enum.IsDefined(typeof(ScriptEngineeringScope), script.Scope))
            {
                var runtimeScope = ScriptEngineeringAdapters.ToRuntimeScope(script.Scope);
                var runtimeEvent = ScriptEngineeringAdapters.ToRuntimeEventKind(entryPoint.EventKind);

                if (!ScriptScopeEventRules.IsAllowed(runtimeScope, runtimeEvent))
                {
                    Add(
                        "SCRIPT_ENTRYPOINT_SCOPE_EVENT_INVALID",
                        $"Event '{entryPoint.EventKind}' is not valid for script scope '{script.Scope}'.");
                }
            }

            var identity =
                $"{(int)entryPoint.EventKind}:{entryPoint.HandlerName}:{EventTargetIdentity(entryPoint.TargetReference, entryPoint.TagReference, entryPoint.TimerIntervalMs)}";
            if (!seen.Add(identity))
            {
                Add(
                    "SCRIPT_ENTRYPOINT_DUPLICATE",
                    $"Entry point '{entryPoint.EventKind}:{entryPoint.HandlerName}' with the same canonical target is declared more than once.");
            }
        }

        void Add(string code, string message) =>
            issues.Add(new ScriptEngineeringValidationIssue(code, message, true, scriptId, entityKey));
    }

    private static void ValidateDependencies(
        ScriptEngineeringDefinition script,
        IReadOnlyDictionary<Guid, ScriptEngineeringDefinition> scriptsById,
        ScriptEngineeringReferenceCatalog? referenceCatalog,
        Guid? scriptId,
        string entityKey,
        ICollection<ScriptEngineeringValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dependency in script.Dependencies
            .OrderBy(item => (int)item.Kind)
            .ThenBy(item => item.StableReference, StringComparer.Ordinal))
        {
            if (!Enum.IsDefined(typeof(ScriptEngineeringDependencyKind), dependency.Kind))
            {
                Add("SCRIPT_DEPENDENCY_KIND_INVALID", $"Dependency kind '{dependency.Kind}' is not supported.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(dependency.StableReference))
            {
                Add("SCRIPT_DEPENDENCY_REFERENCE_REQUIRED", $"Dependency '{dependency.Kind}' requires a stable reference.");
                continue;
            }

            var identity = $"{(int)dependency.Kind}:{dependency.StableReference}";
            if (!seen.Add(identity))
            {
                Add(
                    "SCRIPT_DEPENDENCY_DUPLICATE",
                    $"Dependency '{dependency.Kind}:{dependency.StableReference}' is declared more than once.");
            }

            if (Enum.IsDefined(typeof(ScriptEngineeringScope), script.Scope))
                ValidateDependencyScope(script, dependency, scriptId, entityKey, issues);

            if (dependency.Kind == ScriptEngineeringDependencyKind.Script)
            {
                if (!Guid.TryParse(dependency.StableReference, out var targetScriptId))
                {
                    Add(
                        "SCRIPT_DEPENDENCY_SCRIPT_ID_INVALID",
                        $"Script dependency reference '{dependency.StableReference}' is not a valid stable Script ID.");
                    continue;
                }

                if (script.Id != Guid.Empty && targetScriptId == script.Id)
                {
                    Add("SCRIPT_DEPENDENCY_SELF_REFERENCE", "A Script cannot depend on itself.");
                    continue;
                }

                if (!scriptsById.TryGetValue(targetScriptId, out var targetScript))
                {
                    Add(
                        "SCRIPT_DEPENDENCY_REFERENCE_MISSING",
                        $"Required Script dependency '{targetScriptId:D}' does not exist in the Script Engineering model.");
                    continue;
                }

                if (Enum.IsDefined(typeof(ScriptEngineeringScope), script.Scope) && targetScript.Scope != script.Scope)
                {
                    Add(
                        "SCRIPT_DEPENDENCY_SCOPE_MISMATCH",
                        $"Script '{script.Path}' cannot depend on Script '{targetScript.Path}' from scope '{targetScript.Scope}'.");
                }

                continue;
            }

            if (referenceCatalog is null || !referenceCatalog.Contains(dependency.Kind, dependency.StableReference))
            {
                Add(
                    "SCRIPT_DEPENDENCY_REFERENCE_MISSING",
                    $"Required dependency '{dependency.Kind}:{dependency.StableReference}' could not be resolved.");
            }
        }

        void Add(string code, string message) =>
            issues.Add(new ScriptEngineeringValidationIssue(code, message, true, scriptId, entityKey));
    }

    private static void ValidateDependencyScope(
        ScriptEngineeringDefinition script,
        ScriptEngineeringDependency dependency,
        Guid? scriptId,
        string entityKey,
        ICollection<ScriptEngineeringValidationIssue> issues)
    {
        var invalid = script.Scope switch
        {
            ScriptEngineeringScope.ClientVisual => dependency.Kind == ScriptEngineeringDependencyKind.ServerMemoryTag,
            ScriptEngineeringScope.Server => dependency.Kind is
                ScriptEngineeringDependencyKind.ClientMemoryTag or
                ScriptEngineeringDependencyKind.VisualDefinition or
                ScriptEngineeringDependencyKind.VisualObject,
            _ => false
        };

        if (!invalid)
            return;

        issues.Add(new ScriptEngineeringValidationIssue(
            "SCRIPT_DEPENDENCY_SCOPE_INVALID",
            $"Dependency '{dependency.Kind}' is not valid for script scope '{script.Scope}'.",
            true,
            scriptId,
            entityKey));
    }

    private static void ValidateScriptDependencyCycles(
        IReadOnlyDictionary<Guid, ScriptEngineeringDefinition> scriptsById,
        ICollection<ScriptEngineeringValidationIssue> issues)
    {
        var states = new Dictionary<Guid, int>();
        var stack = new List<Guid>();
        var seenCycles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var scriptId in scriptsById.Keys.OrderBy(id => id))
        {
            if (!states.ContainsKey(scriptId))
                Visit(scriptId);
        }

        void Visit(Guid scriptId)
        {
            states[scriptId] = 1;
            stack.Add(scriptId);

            var script = scriptsById[scriptId];
            var dependencies = script.Dependencies
                .Where(dependency => dependency.Kind == ScriptEngineeringDependencyKind.Script)
                .Select(dependency => Guid.TryParse(dependency.StableReference, out var targetId) ? targetId : Guid.Empty)
                .Where(targetId =>
                    targetId != Guid.Empty &&
                    targetId != scriptId &&
                    scriptsById.TryGetValue(targetId, out var target) &&
                    target.Scope == script.Scope)
                .Distinct()
                .OrderBy(targetId => targetId)
                .ToArray();

            foreach (var targetId in dependencies)
            {
                if (!states.TryGetValue(targetId, out var state))
                {
                    Visit(targetId);
                    continue;
                }

                if (state != 1)
                    continue;

                var cycleStart = stack.IndexOf(targetId);
                if (cycleStart < 0)
                    continue;

                var cycle = stack.Skip(cycleStart).ToArray();
                var normalized = NormalizeCycle(cycle);
                var signature = string.Join(">", normalized.Select(id => id.ToString("D")));

                if (!seenCycles.Add(signature))
                    continue;

                var ownerId = normalized[0];
                var path = normalized
                    .Select(id => scriptsById[id].Path)
                    .Append(scriptsById[ownerId].Path);

                issues.Add(new ScriptEngineeringValidationIssue(
                    "SCRIPT_DEPENDENCY_CYCLE",
                    $"Script dependency cycle detected: {string.Join(" -> ", path)}.",
                    true,
                    ownerId,
                    scriptsById[ownerId].Path));
            }

            stack.RemoveAt(stack.Count - 1);
            states[scriptId] = 2;
        }
    }

    private static Guid[] NormalizeCycle(IReadOnlyList<Guid> cycle)
    {
        if (cycle.Count == 0)
            return [];

        var firstIndex = 0;
        for (var index = 1; index < cycle.Count; index++)
        {
            if (cycle[index].CompareTo(cycle[firstIndex]) < 0)
                firstIndex = index;
        }

        var normalized = new Guid[cycle.Count];
        for (var index = 0; index < cycle.Count; index++)
            normalized[index] = cycle[(firstIndex + index) % cycle.Count];

        return normalized;
    }

    private static void ValidateVisualReferences(
        ScriptEngineeringModel model,
        IReadOnlyDictionary<Guid, ScriptEngineeringDefinition> scriptsById,
        ScriptEngineeringReferenceCatalog? referenceCatalog,
        ICollection<ScriptEngineeringValidationIssue> issues)
    {
        var seenRuntimeHandlers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reference in model.VisualEventReferences
            .OrderBy(item => item.VisualDefinitionId)
            .ThenBy(item => item.VisualObjectId)
            .ThenBy(item => item.ScriptId)
            .ThenBy(item => item.EntryPoint, StringComparer.Ordinal)
            .ThenBy(item => EventTargetIdentity(item.TargetReference, item.TagReference, item.TimerIntervalMs), StringComparer.Ordinal))
        {
            var entityKey = reference.VisualObjectId is { } objectId
                ? ScriptEngineeringReferenceKeys.VisualObject(reference.VisualDefinitionId, objectId)
                : ScriptEngineeringReferenceKeys.VisualDefinition(reference.VisualDefinitionId);

            if (reference.VisualDefinitionId == Guid.Empty)
                Add("SCRIPT_VISUAL_DEFINITION_ID_REQUIRED", "Visual Script reference requires a visual definition stable ID.");

            if (reference.VisualObjectId == Guid.Empty)
                Add("SCRIPT_VISUAL_OBJECT_ID_INVALID", "Visual object stable ID cannot be empty when specified.");

            if (!Enum.IsDefined(typeof(ScriptEngineeringEventKind), reference.EventKind))
                Add("SCRIPT_VISUAL_EVENT_INVALID", $"Visual Script event '{reference.EventKind}' is not supported.");
            else
                ValidateEventTarget(
                    reference.EventKind,
                    reference.TargetReference,
                    reference.TagReference,
                    reference.TimerIntervalMs,
                    (code, message) => Add($"SCRIPT_VISUAL_{code}", message));

            if (reference.ScriptId == Guid.Empty)
                Add("SCRIPT_VISUAL_SCRIPT_ID_REQUIRED", "Visual Script reference requires a Script stable ID.");

            if (string.IsNullOrWhiteSpace(reference.EntryPoint))
                Add("SCRIPT_VISUAL_ENTRYPOINT_REQUIRED", "Visual Script reference requires an entry-point handler name.");

            var runtimeIdentity =
                $"{entityKey}:{(int)reference.EventKind}:{reference.ScriptId:D}:{reference.EntryPoint}";
            if (!seenRuntimeHandlers.Add(runtimeIdentity))
            {
                Add(
                    "SCRIPT_VISUAL_REFERENCE_DUPLICATE",
                    $"Visual Script association '{runtimeIdentity}' maps to the same runtime handler more than once.");
            }

            if (referenceCatalog is null ||
                !referenceCatalog.Contains(
                    ScriptEngineeringDependencyKind.VisualDefinition,
                    ScriptEngineeringReferenceKeys.VisualDefinition(reference.VisualDefinitionId)))
            {
                Add(
                    "SCRIPT_VISUAL_DEFINITION_REFERENCE_MISSING",
                    $"Visual definition '{reference.VisualDefinitionId:D}' could not be resolved.");
            }

            if (reference.VisualObjectId is { } visualObjectId &&
                (referenceCatalog is null ||
                 !referenceCatalog.Contains(
                     ScriptEngineeringDependencyKind.VisualObject,
                     ScriptEngineeringReferenceKeys.VisualObject(reference.VisualDefinitionId, visualObjectId))))
            {
                Add(
                    "SCRIPT_VISUAL_OBJECT_REFERENCE_MISSING",
                    $"Visual object '{visualObjectId:D}' could not be resolved in visual definition '{reference.VisualDefinitionId:D}'.");
            }

            if (reference.EventKind == ScriptEngineeringEventKind.TagChanged && reference.TagReference is { TagId: var tagId } && tagId != Guid.Empty &&
                (referenceCatalog is null || !referenceCatalog.Contains(ScriptEngineeringDependencyKind.Tag, ScriptEngineeringReferenceKeys.Tag(tagId))))
            {
                Add("SCRIPT_VISUAL_TAG_REFERENCE_MISSING", $"TAG '{tagId:D}' could not be resolved.");
            }

            if (reference.EventKind == ScriptEngineeringEventKind.ClientMemoryChanged && !string.IsNullOrWhiteSpace(reference.TargetReference) &&
                (referenceCatalog is null || !referenceCatalog.Contains(ScriptEngineeringDependencyKind.ClientMemoryTag, reference.TargetReference)))
            {
                Add("SCRIPT_VISUAL_CLIENT_MEMORY_REFERENCE_MISSING", $"Client Memory definition '{reference.TargetReference}' could not be resolved.");
            }

            if (!scriptsById.TryGetValue(reference.ScriptId, out var script))
            {
                Add("SCRIPT_VISUAL_SCRIPT_REFERENCE_MISSING", $"Referenced Script '{reference.ScriptId:D}' does not exist.");
                continue;
            }

            if (script.Scope != ScriptEngineeringScope.ClientVisual)
            {
                Add(
                    "SCRIPT_VISUAL_SCOPE_INVALID",
                    $"Visual event association cannot reference Server Script '{script.Path}'.");
            }

            if (!Enum.IsDefined(typeof(ScriptEngineeringEventKind), reference.EventKind))
                continue;

            var declared = script.EntryPoints.Any(entryPoint =>
                entryPoint.EventKind == reference.EventKind &&
                string.Equals(entryPoint.HandlerName, reference.EntryPoint, StringComparison.Ordinal) &&
                EventTargetsEqual(entryPoint, reference));

            if (!declared)
            {
                Add(
                    "SCRIPT_VISUAL_ENTRYPOINT_REFERENCE_INVALID",
                    $"Script '{script.Path}' does not declare entry point '{reference.EventKind}:{reference.EntryPoint}' with the same canonical event target.");
            }

            void Add(string code, string message) =>
                issues.Add(new ScriptEngineeringValidationIssue(
                    code,
                    message,
                    true,
                    reference.ScriptId == Guid.Empty ? null : reference.ScriptId,
                    entityKey));
        }
    }

    private static void ValidateEventTarget(
        ScriptEngineeringEventKind eventKind,
        string? targetReference,
        TagValueReference? tagReference,
        int? timerIntervalMs,
        Action<string, string> add)
    {
        if (eventKind == ScriptEngineeringEventKind.Timer)
        {
            if (!timerIntervalMs.HasValue)
            {
                add("TIMER_INTERVAL_REQUIRED", "Timer event requires timerIntervalMs.");
            }
            else if (timerIntervalMs.Value < MinimumTimerIntervalMs)
            {
                add(
                    "TIMER_INTERVAL_INVALID",
                    $"Timer event interval cannot be shorter than {MinimumTimerIntervalMs} ms.");
            }

            if (tagReference is not null)
                add("TAG_REFERENCE_UNEXPECTED", "Timer event cannot carry a TAG target.");
            if (!string.IsNullOrWhiteSpace(targetReference))
                add("TARGET_REFERENCE_UNEXPECTED", "Timer event cannot encode its duration in TargetReference.");
            return;
        }

        if (timerIntervalMs.HasValue)
            add("TIMER_INTERVAL_UNEXPECTED", "timerIntervalMs is only valid for Timer events.");

        if (eventKind == ScriptEngineeringEventKind.TagChanged)
        {
            if (tagReference is null || tagReference.TagId == Guid.Empty)
            {
                add("TAG_REFERENCE_REQUIRED", "TAG value-change event requires a stable canonical TAG reference.");
            }
            else if (tagReference.Selector is { } selector)
            {
                if (!Enum.IsDefined(typeof(TagValueSelectorKind), selector.Kind) || selector.Kind != TagValueSelectorKind.Bit || selector.Index < 0)
                {
                    add("TAG_SELECTOR_INVALID", "TAG value-change selector must be a valid zero-based canonical bit selector.");
                }
            }

            if (!string.IsNullOrWhiteSpace(targetReference))
                add("TARGET_REFERENCE_UNEXPECTED", "TAG value-change identity must not be serialized into TargetReference.");
            return;
        }

        if (tagReference is not null)
            add("TAG_REFERENCE_UNEXPECTED", "Canonical TAG target is only valid for TAG value-change events.");

        if (eventKind == ScriptEngineeringEventKind.ClientMemoryChanged && string.IsNullOrWhiteSpace(targetReference))
            add("CLIENT_MEMORY_REFERENCE_REQUIRED", "Client Memory change event requires the stable definition ID in TargetReference.");
    }

    private static bool EventTargetsEqual(
        ScriptEngineeringEntryPoint entryPoint,
        ScriptVisualEventReference reference) =>
        string.Equals(entryPoint.TargetReference, reference.TargetReference, StringComparison.Ordinal) &&
        Equals(entryPoint.TagReference, reference.TagReference) &&
        entryPoint.TimerIntervalMs == reference.TimerIntervalMs;

    private static string EventTargetIdentity(
        string? targetReference,
        TagValueReference? tagReference,
        int? timerIntervalMs)
    {
        var tag = tagReference is null
            ? string.Empty
            : $"{tagReference.TagId:D}:{tagReference.Selector?.Kind}:{tagReference.Selector?.Index}";
        return $"target={targetReference ?? string.Empty}|tag={tag}|timer={timerIntervalMs?.ToString() ?? string.Empty}";
    }

    private static bool CanMapToRuntime(ScriptEngineeringDefinition script) =>
        script.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(script.Path) &&
        !string.IsNullOrWhiteSpace(script.Name) &&
        Enum.IsDefined(typeof(ScriptEngineeringScope), script.Scope) &&
        string.Equals(script.Language, "python", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(script.LanguageVersion) &&
        !string.IsNullOrWhiteSpace(script.Source) &&
        script.EntryPoints.All(entryPoint =>
            Enum.IsDefined(typeof(ScriptEngineeringEventKind), entryPoint.EventKind) &&
            IsPythonIdentifier(entryPoint.HandlerName)) &&
        script.Dependencies.All(dependency => Enum.IsDefined(typeof(ScriptEngineeringDependencyKind), dependency.Kind));

    private static bool IsPythonIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!(value[0] == '_' || char.IsLetter(value[0])))
            return false;

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (character != '_' && !char.IsLetterOrDigit(character))
                return false;
        }

        return true;
    }
}
