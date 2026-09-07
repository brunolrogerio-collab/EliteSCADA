using Scada.Engineering.Scripts;

namespace Scada.Engineering.Libraries;

/// <summary>
/// Single authority for the C25.6 reusable Script v1 boundary.
/// A portable Script may depend transitively only on other Scripts. Concrete TAG,
/// memory, HMI definition/object and generic resource dependencies remain project-owned
/// and therefore fail before .escadalib export/incorporation. Existing visual-event
/// associations are likewise project composition, not part of the reusable Script payload.
/// </summary>
public static class ReusableScriptDependencyAnalyzer
{
    public static IReadOnlyCollection<ReusableLibraryDependency> AnalyzeWorking(
        ScriptEngineeringDefinition root,
        IScriptEngineeringRegistry scripts)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(scripts);

        var visualReferences = scripts.SnapshotVisualEventReferences()
            .GroupBy(reference => reference.ScriptId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var states = new Dictionary<Guid, int>();
        var closure = new Dictionary<Guid, ScriptEngineeringDefinition>();

        void Visit(ScriptEngineeringDefinition script)
        {
            if (script.Id == Guid.Empty)
                throw new InvalidDataException("Reusable Script requires a stable non-empty Script ID.");

            if (states.TryGetValue(script.Id, out var state))
            {
                if (state == 1)
                    throw new InvalidDataException(
                        $"Reusable Script dependency cycle detected at '{script.Id:D}'.");
                if (state == 2)
                    return;
            }

            states[script.Id] = 1;
            EnsurePortablePayload(script);

            if (visualReferences.TryGetValue(script.Id, out var attached) && attached.Length > 0)
            {
                throw new InvalidDataException(
                    $"Reusable Script '{script.Path}' has project HMI event associations. " +
                    "ScriptVisualEventReference is project composition and is not portable in reusable Script v1.");
            }

            foreach (var dependency in script.Dependencies
                         .OrderBy(item => (int)item.Kind)
                         .ThenBy(item => item.StableReference, StringComparer.Ordinal))
            {
                if (dependency.Kind != ScriptEngineeringDependencyKind.Script)
                {
                    throw new InvalidDataException(
                        $"Reusable Script '{script.Path}' depends on project-owned '{dependency.Kind}' content. " +
                        "Reusable Script v1 supports Script-to-Script dependencies only.");
                }

                var targetId = ParseScriptDependencyId(script, dependency);
                if (targetId == script.Id)
                    throw new InvalidDataException($"Reusable Script '{script.Path}' cannot depend on itself.");

                var target = scripts.Find(targetId)
                    ?? throw new InvalidDataException(
                        $"Reusable Script '{script.Path}' dependency '{targetId:D}' was not found in Working.");
                if (target.Scope != script.Scope)
                {
                    throw new InvalidDataException(
                        $"Reusable Script '{script.Path}' cannot depend on Script '{target.Path}' from scope '{target.Scope}'.");
                }

                Visit(target);
            }

            states[script.Id] = 2;
            closure[script.Id] = script;
        }

        Visit(root);

        var validation = new ScriptEngineeringValidator().Validate(
            new ScriptEngineeringModel(closure.Values.ToArray(), Array.Empty<ScriptVisualEventReference>()));
        var firstError = validation.Issues.FirstOrDefault(issue => issue.IsError);
        if (firstError is not null)
        {
            throw new InvalidDataException(
                $"Reusable Script validation failed: {firstError.Code}: {firstError.Message}");
        }

        return DirectDependencies(root);
    }

    public static void ValidateDeclaredDependencies(
        ScriptEngineeringDefinition script,
        ReusableLibraryResourceEntry resource,
        ReusableLibraryManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(manifest);

        EnsurePortablePayload(script);

        var expected = DirectDependencies(script)
            .Select(dependency => (dependency.Kind, dependency.ResourceId))
            .ToHashSet();
        var declared = (resource.Dependencies ?? Array.Empty<ReusableLibraryDependency>())
            .Select(dependency => (dependency.Kind, dependency.ResourceId))
            .ToHashSet();

        if (declared.Any(identity => identity.Kind != ReusableLibraryResourceKinds.Script))
        {
            throw new InvalidDataException(
                $"Reusable Script '{script.Path}' declares a non-Script library dependency.");
        }

        if (!expected.SetEquals(declared))
        {
            throw new InvalidDataException(
                $"Reusable Script '{script.Path}' declared dependency set does not match its canonical Script dependencies.");
        }

        foreach (var dependency in expected)
        {
            var target = manifest.Resources.SingleOrDefault(entry =>
                entry.Kind == dependency.Kind && entry.ResourceId == dependency.ResourceId);
            if (target is null)
            {
                throw new InvalidDataException(
                    $"Reusable Script '{script.Path}' dependency '{dependency.ResourceId:D}' is missing from the library.");
            }
        }
    }

    public static void ValidateLibraryModel(IReadOnlyCollection<ScriptEngineeringDefinition> scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);
        if (scripts.Count == 0)
            return;

        foreach (var script in scripts)
            EnsurePortablePayload(script);

        var validation = new ScriptEngineeringValidator().Validate(
            new ScriptEngineeringModel(scripts, Array.Empty<ScriptVisualEventReference>()));
        var firstError = validation.Issues.FirstOrDefault(issue => issue.IsError);
        if (firstError is not null)
        {
            throw new InvalidDataException(
                $"Reusable Script library model is invalid: {firstError.Code}: {firstError.Message}");
        }
    }

    public static ScriptEngineeringDefinition WithMetadata(
        ScriptEngineeringDefinition script,
        IReadOnlyDictionary<string, string>? metadata)
    {
        ArgumentNullException.ThrowIfNull(script);
        return new ScriptEngineeringDefinition(
            script.Id,
            script.Path,
            script.Name,
            script.Scope,
            script.Source,
            script.Enabled,
            script.Language,
            script.LanguageVersion,
            script.EntryPoints,
            script.Dependencies,
            script.Description,
            metadata);
    }

    private static IReadOnlyCollection<ReusableLibraryDependency> DirectDependencies(
        ScriptEngineeringDefinition script)
    {
        var dependencies = new Dictionary<Guid, ReusableLibraryDependency>();
        foreach (var dependency in script.Dependencies)
        {
            if (dependency.Kind != ScriptEngineeringDependencyKind.Script)
            {
                throw new InvalidDataException(
                    $"Reusable Script '{script.Path}' depends on project-owned '{dependency.Kind}' content. " +
                    "Reusable Script v1 supports Script-to-Script dependencies only.");
            }

            var targetId = ParseScriptDependencyId(script, dependency);
            dependencies.TryAdd(
                targetId,
                new ReusableLibraryDependency(ReusableLibraryResourceKinds.Script, targetId));
        }

        return dependencies.Values
            .OrderBy(dependency => dependency.ResourceId)
            .ToArray();
    }

    private static Guid ParseScriptDependencyId(
        ScriptEngineeringDefinition owner,
        ScriptEngineeringDependency dependency)
    {
        if (!Guid.TryParse(dependency.StableReference, out var targetId) || targetId == Guid.Empty)
        {
            throw new InvalidDataException(
                $"Reusable Script '{owner.Path}' contains invalid Script dependency identity '{dependency.StableReference}'.");
        }

        return targetId;
    }

    private static void EnsurePortablePayload(ScriptEngineeringDefinition script)
    {
        if (script.Id == Guid.Empty)
            throw new InvalidDataException("Reusable Script requires a stable non-empty Script ID.");
        if (string.IsNullOrWhiteSpace(script.Path))
            throw new InvalidDataException($"Reusable Script '{script.Id:D}' requires a stable path.");

        foreach (var entryPoint in script.EntryPoints)
        {
            if (entryPoint.EventKind is ScriptEngineeringEventKind.TagChanged or ScriptEngineeringEventKind.ClientMemoryChanged)
            {
                throw new InvalidDataException(
                    $"Reusable Script '{script.Path}' entry point '{entryPoint.EventKind}' contains a concrete project target. " +
                    "TAG and Client Memory event targets are not portable in reusable Script v1.");
            }
        }

        foreach (var dependency in script.Dependencies)
        {
            if (dependency.Kind != ScriptEngineeringDependencyKind.Script)
            {
                throw new InvalidDataException(
                    $"Reusable Script '{script.Path}' depends on project-owned '{dependency.Kind}' content. " +
                    "Reusable Script v1 supports Script-to-Script dependencies only.");
            }
        }
    }
}
