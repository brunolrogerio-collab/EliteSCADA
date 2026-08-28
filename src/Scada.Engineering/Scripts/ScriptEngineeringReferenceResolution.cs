using Scada.Core.Sources;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Scripts;

public sealed record ScriptEngineeringVisualDefinitionIdentity(
    Guid Id,
    string Kind,
    string Key);

public sealed record ScriptEngineeringReferenceTarget(
    ScriptEngineeringDependencyKind Kind,
    string StableReference,
    Guid? EntityId = null,
    string? EntityPath = null,
    string? SourceKey = null,
    string? SourceType = null);

public sealed record ScriptEngineeringReferenceCatalogIssue(
    string Code,
    string Message,
    string EntityKey);

public sealed record ScriptEngineeringReferenceResolution(
    ScriptEngineeringDependencyKind Kind,
    string StableReference,
    ScriptEngineeringReferenceTarget? Target,
    string? DiagnosticCode = null,
    string? DiagnosticMessage = null)
{
    public bool IsResolved => Target is not null && DiagnosticCode is null;
}

/// <summary>
/// Builds and resolves stable Script dependency references without exposing
/// concrete drivers, source-provider instances or runtime infrastructure.
/// Client Memory is never catalogued as a generic shared TAG. Server Memory
/// remains readable through the generic TAG boundary and is also classified
/// explicitly for Server-Memory-specific Script dependencies.
/// </summary>
public sealed class ScriptEngineeringReferenceResolver
{
    private enum TagReferenceClass
    {
        SharedTag,
        ClientMemory,
        ServerMemory
    }

    private readonly IReadOnlyDictionary<string, ScriptEngineeringReferenceTarget[]> _targetsByKey;
    private readonly IReadOnlyDictionary<string, ScriptEngineeringReferenceTarget[]> _targetsByStableReference;

    private ScriptEngineeringReferenceResolver(
        IEnumerable<ScriptEngineeringReferenceTarget> targets,
        IEnumerable<ScriptEngineeringReferenceCatalogIssue>? catalogIssues = null)
    {
        var ordered = targets
            .OrderBy(target => (int)target.Kind)
            .ThenBy(target => target.StableReference, StringComparer.Ordinal)
            .ThenBy(target => target.EntityPath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(target => target.EntityId)
            .ToArray();

        References = Array.AsReadOnly(ordered);
        _targetsByKey = ordered
            .GroupBy(target => ToKey(target.Kind, target.StableReference), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        _targetsByStableReference = ordered
            .GroupBy(target => target.StableReference, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        var issues = new List<ScriptEngineeringReferenceCatalogIssue>(
            catalogIssues ?? Array.Empty<ScriptEngineeringReferenceCatalogIssue>());

        foreach (var duplicate in _targetsByKey
            .Where(pair => pair.Value.Length > 1)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var target = duplicate.Value[0];
            issues.Add(new ScriptEngineeringReferenceCatalogIssue(
                "SCRIPT_REFERENCE_TARGET_AMBIGUOUS",
                $"Stable reference '{target.Kind}:{target.StableReference}' resolves to more than one canonical target.",
                target.StableReference));
        }

        CatalogIssues = Array.AsReadOnly(issues
            .OrderBy(issue => issue.EntityKey, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyCollection<ScriptEngineeringReferenceTarget> References { get; }

    public IReadOnlyCollection<ScriptEngineeringReferenceCatalogIssue> CatalogIssues { get; }

    public ScriptEngineeringReferenceCatalog ToValidationCatalog() =>
        new(References.Select(reference =>
            new ScriptEngineeringReference(reference.Kind, reference.StableReference)));

    public ScriptEngineeringReferenceResolution Resolve(
        ScriptEngineeringDependencyKind kind,
        string stableReference)
    {
        if (!Enum.IsDefined(typeof(ScriptEngineeringDependencyKind), kind))
        {
            return Failure(
                kind,
                stableReference ?? string.Empty,
                "SCRIPT_REFERENCE_KIND_INVALID",
                $"Dependency kind '{kind}' is not supported.");
        }

        if (string.IsNullOrWhiteSpace(stableReference))
        {
            return Failure(
                kind,
                string.Empty,
                "SCRIPT_REFERENCE_REQUIRED",
                $"Dependency '{kind}' requires a stable reference.");
        }

        if (!TryNormalizeStableReference(kind, stableReference, out var normalized))
        {
            return Failure(
                kind,
                stableReference.Trim(),
                "SCRIPT_REFERENCE_FORMAT_INVALID",
                $"Stable reference '{stableReference}' is not valid for dependency kind '{kind}'.");
        }

        if (_targetsByKey.TryGetValue(ToKey(kind, normalized), out var exact))
        {
            if (exact.Length == 1)
                return new ScriptEngineeringReferenceResolution(kind, normalized, exact[0]);

            return Failure(
                kind,
                normalized,
                "SCRIPT_REFERENCE_AMBIGUOUS",
                $"Stable reference '{kind}:{normalized}' resolves to more than one canonical target.");
        }

        if (_targetsByStableReference.TryGetValue(normalized, out var otherKinds))
        {
            var actualKinds = otherKinds
                .Select(target => target.Kind)
                .Distinct()
                .OrderBy(actual => (int)actual)
                .Select(actual => actual.ToString());

            return Failure(
                kind,
                normalized,
                "SCRIPT_REFERENCE_KIND_MISMATCH",
                $"Stable reference '{normalized}' exists, but is classified as {string.Join(", ", actualKinds)} rather than {kind}.");
        }

        return Failure(
            kind,
            normalized,
            "SCRIPT_REFERENCE_MISSING",
            $"Required dependency '{kind}:{normalized}' could not be resolved.");
    }

    public ScriptEngineeringReferenceResolution ResolveForScope(
        ScriptEngineeringScope scope,
        ScriptEngineeringDependency dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);

        if (!IsAllowedForScope(scope, dependency.Kind))
        {
            return Failure(
                dependency.Kind,
                dependency.StableReference,
                "SCRIPT_REFERENCE_SCOPE_INVALID",
                $"Dependency '{dependency.Kind}' is not valid for script scope '{scope}'.");
        }

        return Resolve(dependency.Kind, dependency.StableReference);
    }

    public static bool IsAllowedForScope(
        ScriptEngineeringScope scope,
        ScriptEngineeringDependencyKind kind)
    {
        if (!Enum.IsDefined(typeof(ScriptEngineeringScope), scope) ||
            !Enum.IsDefined(typeof(ScriptEngineeringDependencyKind), kind))
            return false;

        return scope switch
        {
            ScriptEngineeringScope.ClientVisual =>
                kind != ScriptEngineeringDependencyKind.ServerMemoryTag,

            ScriptEngineeringScope.Server =>
                kind is not (
                    ScriptEngineeringDependencyKind.ClientMemoryTag or
                    ScriptEngineeringDependencyKind.VisualDefinition or
                    ScriptEngineeringDependencyKind.VisualObject),

            _ => false
        };
    }

    public static ScriptEngineeringReferenceResolver FromEngineeringPackage(
        EngineeringPackage package,
        IEnumerable<ScriptEngineeringReference>? additionalReferences = null)
    {
        ArgumentNullException.ThrowIfNull(package);

        var visualObjectReferences = EnumerateVisualObjectReferences(package);
        var references = additionalReferences is null
            ? visualObjectReferences
            : visualObjectReferences.Concat(additionalReferences);

        return Create(
            package.Tags,
            package.DataSources ?? Array.Empty<DataSourceEngineeringDto>(),
            EnumerateVisualDefinitions(package),
            references);
    }

    public static ScriptEngineeringReferenceResolver Create(
        IEnumerable<TagEngineeringDto> tags,
        IEnumerable<DataSourceEngineeringDto> dataSources,
        IEnumerable<ScriptEngineeringVisualDefinitionIdentity>? visualDefinitions = null,
        IEnumerable<ScriptEngineeringReference>? additionalReferences = null)
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(dataSources);

        var targets = new List<ScriptEngineeringReferenceTarget>();
        var issues = new List<ScriptEngineeringReferenceCatalogIssue>();
        var dataSourcesByKey = dataSources
            .Where(source => source is not null && !string.IsNullOrWhiteSpace(source.Key))
            .GroupBy(source => source.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(source => source.Driver, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(source => source.Name, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags
            .Where(tag => tag is not null)
            .OrderBy(tag => tag.Path, StringComparer.Ordinal)
            .ThenBy(tag => tag.Id))
        {
            if (tag.Id is not { } tagId || tagId == Guid.Empty)
                continue;

            if (!TryClassifyTag(tag, dataSourcesByKey, out var classification, out var sourceType, out var ambiguity))
            {
                issues.Add(new ScriptEngineeringReferenceCatalogIssue(
                    "SCRIPT_REFERENCE_DATASOURCE_AMBIGUOUS",
                    ambiguity ?? $"TAG '{tag.Path}' source classification is ambiguous.",
                    tag.Path));
                continue;
            }

            foreach (var kind in ReferenceKinds(classification))
            {
                targets.Add(new ScriptEngineeringReferenceTarget(
                    kind,
                    ScriptEngineeringReferenceKeys.Tag(tagId),
                    tagId,
                    tag.Path,
                    tag.Source,
                    sourceType));
            }
        }

        foreach (var visual in visualDefinitions ?? Array.Empty<ScriptEngineeringVisualDefinitionIdentity>())
        {
            if (visual is null || visual.Id == Guid.Empty)
                continue;

            var context = string.IsNullOrWhiteSpace(visual.Kind)
                ? visual.Key
                : $"{visual.Kind}:{visual.Key}";

            targets.Add(new ScriptEngineeringReferenceTarget(
                ScriptEngineeringDependencyKind.VisualDefinition,
                ScriptEngineeringReferenceKeys.VisualDefinition(visual.Id),
                visual.Id,
                context));
        }

        foreach (var additional in additionalReferences ?? Array.Empty<ScriptEngineeringReference>())
        {
            if (additional is null ||
                !TryNormalizeStableReference(additional.Kind, additional.StableReference, out var normalized))
                continue;

            targets.Add(new ScriptEngineeringReferenceTarget(
                additional.Kind,
                normalized));
        }

        return new ScriptEngineeringReferenceResolver(targets, issues);
    }

    public static bool TryNormalizeStableReference(
        ScriptEngineeringDependencyKind kind,
        string stableReference,
        out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(stableReference) ||
            !Enum.IsDefined(typeof(ScriptEngineeringDependencyKind), kind))
            return false;

        var candidate = stableReference.Trim();
        if (kind == ScriptEngineeringDependencyKind.VisualObject)
        {
            var parts = candidate.Split('/', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !Guid.TryParse(parts[0], out var definitionId) || definitionId == Guid.Empty ||
                !Guid.TryParse(parts[1], out var objectId) || objectId == Guid.Empty)
                return false;

            normalized = ScriptEngineeringReferenceKeys.VisualObject(definitionId, objectId);
            return true;
        }

        if (!Guid.TryParse(candidate, out var id) || id == Guid.Empty)
            return false;

        normalized = kind switch
        {
            ScriptEngineeringDependencyKind.Script => ScriptEngineeringReferenceKeys.Script(id),
            ScriptEngineeringDependencyKind.VisualDefinition => ScriptEngineeringReferenceKeys.VisualDefinition(id),
            ScriptEngineeringDependencyKind.Tag or
            ScriptEngineeringDependencyKind.ClientMemoryTag or
            ScriptEngineeringDependencyKind.ServerMemoryTag => ScriptEngineeringReferenceKeys.Tag(id),
            ScriptEngineeringDependencyKind.Resource => ScriptEngineeringReferenceKeys.Resource(id),
            _ => string.Empty
        };

        return normalized.Length > 0;
    }

    private static bool TryClassifyTag(
        TagEngineeringDto tag,
        IReadOnlyDictionary<string, DataSourceEngineeringDto[]> dataSourcesByKey,
        out TagReferenceClass classification,
        out string? sourceType,
        out string? ambiguity)
    {
        classification = TagReferenceClass.SharedTag;
        sourceType = null;
        ambiguity = null;

        if (string.IsNullOrWhiteSpace(tag.Source))
            return true;

        if (dataSourcesByKey.TryGetValue(tag.Source, out var sources))
        {
            var classifications = sources
                .Select(source => ClassifyDriver(source.Driver))
                .Distinct()
                .ToArray();

            if (classifications.Length != 1)
            {
                ambiguity = $"TAG '{tag.Path}' source '{tag.Source}' maps to conflicting Data Source reference classifications.";
                return false;
            }

            classification = classifications[0];
            sourceType = sources[0].Driver;
            return true;
        }

        classification = ClassifyDriver(tag.Source);
        sourceType = tag.Source;
        return true;
    }

    private static TagReferenceClass ClassifyDriver(string? driver)
    {
        if (string.Equals(
                driver,
                BuiltInSourceProviderDescriptors.ClientMemory.TypeKey,
                StringComparison.OrdinalIgnoreCase))
            return TagReferenceClass.ClientMemory;

        if (string.Equals(
                driver,
                BuiltInSourceProviderDescriptors.ServerMemory.TypeKey,
                StringComparison.OrdinalIgnoreCase))
            return TagReferenceClass.ServerMemory;

        return TagReferenceClass.SharedTag;
    }

    private static IReadOnlyCollection<ScriptEngineeringDependencyKind> ReferenceKinds(
        TagReferenceClass classification) =>
        classification switch
        {
            TagReferenceClass.ClientMemory =>
                [ScriptEngineeringDependencyKind.ClientMemoryTag],

            TagReferenceClass.ServerMemory =>
                [ScriptEngineeringDependencyKind.Tag, ScriptEngineeringDependencyKind.ServerMemoryTag],

            _ => [ScriptEngineeringDependencyKind.Tag]
        };

    private static IEnumerable<ScriptEngineeringVisualDefinitionIdentity> EnumerateVisualDefinitions(
        EngineeringPackage package)
    {
        foreach (var screen in package.Screens ?? Array.Empty<ScreenEngineeringDto>())
            if (screen is not null && screen.Id is { } id && id != Guid.Empty)
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "screen", screen.Key);

        foreach (var popup in package.Popups ?? Array.Empty<PopupEngineeringDto>())
            if (popup is not null && popup.Id is { } id && id != Guid.Empty)
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "popup", popup.Key);

        foreach (var dynamo in package.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
            if (dynamo is not null && dynamo.Id is { } id && id != Guid.Empty)
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "dynamo", dynamo.Key);
    }

    private static IEnumerable<ScriptEngineeringReference> EnumerateVisualObjectReferences(
        EngineeringPackage package)
    {
        foreach (var screen in package.Screens ?? Array.Empty<ScreenEngineeringDto>())
        {
            if (screen is null || screen.Id is not { } definitionId || definitionId == Guid.Empty)
                continue;

            foreach (var reference in EnumerateVisualObjectReferences(definitionId, screen.Elements))
                yield return reference;
        }

        foreach (var popup in package.Popups ?? Array.Empty<PopupEngineeringDto>())
        {
            if (popup is null || popup.Id is not { } definitionId || definitionId == Guid.Empty)
                continue;

            foreach (var reference in EnumerateVisualObjectReferences(definitionId, popup.Elements))
                yield return reference;
        }
    }

    private static IEnumerable<ScriptEngineeringReference> EnumerateVisualObjectReferences(
        Guid definitionId,
        IReadOnlyCollection<VisualElementEngineeringDto>? elements)
    {
        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            if (element is null)
                continue;

            if (element.Id is { } objectId && objectId != Guid.Empty)
            {
                yield return new ScriptEngineeringReference(
                    ScriptEngineeringDependencyKind.VisualObject,
                    ScriptEngineeringReferenceKeys.VisualObject(definitionId, objectId));
            }

            foreach (var nested in EnumerateVisualObjectReferences(definitionId, element.Children))
                yield return nested;
        }
    }

    private static string ToKey(
        ScriptEngineeringDependencyKind kind,
        string stableReference) =>
        $"{(int)kind}:{stableReference}";

    private static ScriptEngineeringReferenceResolution Failure(
        ScriptEngineeringDependencyKind kind,
        string stableReference,
        string code,
        string message) =>
        new(kind, stableReference, null, code, message);
}
