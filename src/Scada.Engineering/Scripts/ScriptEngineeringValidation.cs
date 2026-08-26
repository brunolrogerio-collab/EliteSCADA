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
        {
            Add("SCRIPT_ID_REQUIRED", "Script stable ID is required.");
        }

        if (string.IsNullOrWhiteSpace(script.Path))
        {
            Add("SCRIPT_PATH_REQUIRED", "Script path is required.");
        }
        else if (script.Path != script.Path.Trim() || script.Path.Contains('\\'))
        {
            Add(
                "SCRIPT_PATH_INVALID",
                "Script path must be trimmed and use '/' separators.");
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

            foreach (var diagnostic in preflight.Diagnostics.Where(diagnostic => diagnostic.Severity == PythonDiagnosticSeverity.Error))
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
            .ThenBy(item => item.TargetReference ?? string.Empty, StringComparer.Ordinal))
        {
            if (!Enum.IsDefined(typeof(ScriptEngineeringEventKind), entryPoint.EventKind))
            {
                issues.Add(new ScriptEngineeringValidationIssue(
                    "SCRIPT_ENTRYPOINT_EVENT_INVALID",
                    $"Entry point event '{entryPoint.EventKind}' is not supported.",
                    true,
                    scriptId,
                    entityKey));
                continue;
            }

            if (!IsPythonIdentifier(entryPoint.HandlerName))
            {
                issues.Add(new ScriptEngineeringValidationIssue(
                    "SCRIPT_ENTRYPOINT_HANDLER_INVALID",
                    $"Entry point handler '{entryPoint.HandlerName}' is not a valid Python identifier.",
                    true,
                    scriptId,
                    entityKey));
            }

            if (Enum.IsDefined(typeof(ScriptEngineeringScope), script.Scope))
            {
                var runtimeScope = ScriptEngineeringAdapters.ToRuntimeScope(script.Scope);
                var runtimeEvent = ScriptEngineeringAdapters.ToRuntimeEventKind(entryPoint.EventKind);

                if (!ScriptScopeEventRules.IsAllowed(runtimeScope, runtimeEvent))
                {
                    issues.Add(new ScriptEngineeringValidationIssue(
                        "SCRIPT_ENTRYPOINT_SCOPE_EVENT_INVALID",
                        $"Event '{entryPoint.EventKind}' is not valid for script scope '{script.Scope}'.",
                        true,
                        scriptId,
                        entityKey));
                }
            }

            var identity = $"{(int)entryPoint.EventKind}:{entryPoint.HandlerName}:{entryPoint.TargetReference}";
            if (!seen.Add(identity))
            {
                issues.Add(new ScriptEngineeringValidationIssue(
                    "SCRIPT_ENTRYPOINT_DUPLICATE",
                    $"Entry point '{entryPoint.EventKind}:{entryPoint.HandlerName}:{entryPoint.TargetReference}' is declared more than once.",
                    true,
                    scriptId,
                    entityKey));
            }
        }
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
                issues.Add(new ScriptEngineeringValidationIssue(
                    "SCRIPT_DEPENDENCY_KIND_INVALID",
                    $"Dependency kind '{dependency.Kind}' is not supported.",
                    true,
                    scriptId,
                    entityKey));
                continue;
            }

            if (string.IsNullOrWhiteSpace(dependency.StableReference))
            {
                issues.Add(new ScriptEngineeringValidationIssue(
                    "SCRIPT_DEPENDENCY_REFERENCE_REQUIRED",
                    $"Dependency '{dependency.Kind}' requires a stable reference.",
                    true,
                    scriptId,
                    entityKey));
                continue;
            }

            var identity = $"{(int)dependency.Kind}:{dependency.StableReference}";
            if (!seen.Add(identity))
            {
                issues.Add(new ScriptEngineeringValidationIssue(
                    "SCRIPT_DEPENDENCY_DUPLICATE",
                    $"Dependency '{dependency.Kind}:{dependency.StableReference}' is declared more than once.",
                    true,
                    scriptId,
                    entityKey));
            }

            if (Enum.IsDefined(typeof(ScriptEngineeringScope), script.Scope))
                ValidateDependencyScope(script, dependency, scriptId, entityKey, issues);

            if (dependency.Kind == ScriptEngineeringDependencyKind.Script)
            {
                if (!Guid.TryParse(dependency.StableReference, out var targetScriptId))
                {
                    issues.Add(new ScriptEngineeringValidationIssue(
                        "SCRIPT_DEPENDENCY_SCRIPT_ID_INVALID",
                        $"Script dependency reference '{dependency.StableReference}' is not a valid stable Script ID.",
                        true,
                        scriptId,
                        entityKey));
                    continue;
                }

                if (script.Id != Guid.Empty && targetScriptId == script.Id)
                {
                    issues.Add(new ScriptEngineeringValidationIssue(
                        "SCRIPT_DEPENDENCY_SELF_REFERENCE",
                        "A Script cannot depend on itself.",
                        true,
                        scriptId,
                        entityKey));
                    continue;
                }

                if (!scriptsById.TryGetValue(targetScriptId, out var targetScript))
                {
                    if (dependency.Required)
                    {
                        issues.Add(new ScriptEngineeringValidationIssue(
                            "SCRIPT_DEPENDENCY_REFERENCE_MISSING",
                            $"Required Script dependency '{targetScriptId:D}' does not exist in the Script Engineering model.",
                            true,
                            scriptId,
                            entityKey));
                    }
                    continue;
                }

                if (Enum.IsDefined(typeof(ScriptEngineeringScope), script.Scope) && targetScript.Scope != script.Scope)
                {
                    issues.Add(new ScriptEngineeringValidationIssue(
                        "SCRIPT_DEPENDENCY_SCOPE_MISMATCH",
                        $"Script '{script.Path}' cannot depend on Script '{targetScript.Path}' from scope '{targetScript.Scope}'.",
                        true,
                        scriptId,
                        entityKey));
                }

                continue;
            }

            if (dependency.Required &&
                (referenceCatalog is null || !referenceCatalog.Contains(dependency.Kind, dependency.StableReference)))
            {
                issues.Add(new ScriptEngineeringValidationIssue(
                    "SCRIPT_DEPENDENCY_REFERENCE_MISSING",
                    $"Required dependency '{dependency.Kind}:{dependency.StableReference}' could not be resolved.",
                    true,
                    scriptId,
                    entityKey));
            }
        }
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
            ScriptEngineeringScope.ClientVisual =>
                dependency.Kind == ScriptEngineeringDependencyKind.ServerMemoryTag,

            ScriptEngineeringScope.Server =>
                dependency.Kind is
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

    private static void ValidateVisualReferences(
        ScriptEngineeringModel model,
        IReadOnlyDictionary<Guid, ScriptEngineeringDefinition> scriptsById,
        ScriptEngineeringReferenceCatalog? referenceCatalog,
        ICollection<ScriptEngineeringValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reference in model.VisualEventReferences
            .OrderBy(item => item.VisualDefinitionId)
            .ThenBy(item => item.VisualObjectId)
            .ThenBy(item => item.ScriptId)
            .ThenBy(item => item.EntryPoint, StringComparer.Ordinal))
        {
            var entityKey = reference.VisualObjectId is { } objectId
                ? ScriptEngineeringReferenceKeys.VisualObject(reference.VisualDefinitionId, objectId)
                : ScriptEngineeringReferenceKeys.VisualDefinition(reference.VisualDefinitionId);

            if (reference.VisualDefinitionId == Guid.Empty)
            {
                Add("SCRIPT_VISUAL_DEFINITION_ID_REQUIRED", "Visual Script reference requires a visual definition stable ID.");
            }

            if (reference.VisualObjectId == Guid.Empty)
            {
                Add("SCRIPT_VISUAL_OBJECT_ID_INVALID", "Visual object stable ID cannot be empty when specified.");
            }

            if (!Enum.IsDefined(typeof(ScriptEngineeringEventKind), reference.EventKind))
            {
                Add("SCRIPT_VISUAL_EVENT_INVALID", $"Visual Script event '{reference.EventKind}' is not supported.");
            }

            if (reference.ScriptId == Guid.Empty)
            {
                Add("SCRIPT_VISUAL_SCRIPT_ID_REQUIRED", "Visual Script reference requires a Script stable ID.");
            }

            if (string.IsNullOrWhiteSpace(reference.EntryPoint))
            {
                Add("SCRIPT_VISUAL_ENTRYPOINT_REQUIRED", "Visual Script reference requires an entry-point handler name.");
            }

            var identity = $"{entityKey}:{(int)reference.EventKind}:{reference.ScriptId:D}:{reference.EntryPoint}:{reference.TargetReference}";
            if (!seen.Add(identity))
            {
                Add(
                    "SCRIPT_VISUAL_REFERENCE_DUPLICATE",
                    $"Visual Script association '{identity}' is declared more than once.");
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

            if (!scriptsById.TryGetValue(reference.ScriptId, out var script))
            {
                Add(
                    "SCRIPT_VISUAL_SCRIPT_REFERENCE_MISSING",
                    $"Referenced Script '{reference.ScriptId:D}' does not exist.");
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
                string.Equals(entryPoint.TargetReference, reference.TargetReference, StringComparison.Ordinal));

            if (!declared)
            {
                Add(
                    "SCRIPT_VISUAL_ENTRYPOINT_REFERENCE_INVALID",
                    $"Script '{script.Path}' does not declare entry point '{reference.EventKind}:{reference.EntryPoint}:{reference.TargetReference}'.");
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

    private static bool CanMapToRuntime(ScriptEngineeringDefinition script) =>
        script.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(script.Path) &&
        !string.IsNullOrWhiteSpace(script.Name) &&
        Enum.IsDefined(typeof(ScriptEngineeringScope), script.Scope) &&
        string.Equals(script.Language, "python", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(script.LanguageVersion) &&
        script.EntryPoints.All(entryPoint =>
            Enum.IsDefined(typeof(ScriptEngineeringEventKind), entryPoint.EventKind) &&
            IsPythonIdentifier(entryPoint.HandlerName));

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
