using System.Text.Json;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Scripts;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.Libraries;

/// <summary>
/// Reusable Screen/Popup dependency and portability authority for the current
/// canonical visual composition model. It derives only dependencies that can be
/// copied into a self-contained project and rejects project-bound behavior rather
/// than silently dropping it from a reusable library.
/// </summary>
public static class ReusableViewDependencyAnalyzer
{
    public static IReadOnlyCollection<ReusableLibraryDependency> AnalyzeWorking(
        ScreenEngineeringDto screen,
        IEngineeringAssetRegistry assets,
        IVisualAssetEngineeringRegistry visualAssets,
        IScriptEngineeringRegistry? scripts = null)
    {
        ArgumentNullException.ThrowIfNull(screen);
        RequireStableIdentity(screen.Id, $"Screen '{screen.Key}'");
        ValidateNoScriptCoupling(screen.Id!.Value, $"Screen '{screen.Key}'", scripts);
        return Analyze(
            $"Screen '{screen.Key}'",
            templateKey: null,
            screen.Elements,
            key => ResolveWorkingDynamo(key, assets, $"Screen '{screen.Key}'"),
            id => ResolveWorkingAsset(id, visualAssets, $"Screen '{screen.Key}'"),
            _ => throw new InvalidDataException("Screen cannot declare a Template dependency."));
    }

    public static IReadOnlyCollection<ReusableLibraryDependency> AnalyzeWorking(
        PopupEngineeringDto popup,
        IEngineeringAssetRegistry assets,
        IVisualAssetEngineeringRegistry visualAssets,
        IScriptEngineeringRegistry? scripts = null)
    {
        ArgumentNullException.ThrowIfNull(popup);
        RequireStableIdentity(popup.Id, $"Popup '{popup.Key}'");
        ValidateNoScriptCoupling(popup.Id!.Value, $"Popup '{popup.Key}'", scripts);
        return Analyze(
            $"Popup '{popup.Key}'",
            popup.TemplateKey,
            popup.Elements,
            key => ResolveWorkingDynamo(key, assets, $"Popup '{popup.Key}'"),
            id => ResolveWorkingAsset(id, visualAssets, $"Popup '{popup.Key}'"),
            key => ResolveWorkingTemplate(key, assets, $"Popup '{popup.Key}'"));
    }

    public static IReadOnlyCollection<ReusableLibraryDependency> AnalyzeManifest(
        ScreenEngineeringDto screen,
        ReusableLibraryManifest manifest) =>
        Analyze(
            $"Screen '{screen.Key}'",
            templateKey: null,
            screen.Elements,
            key => ResolveManifestByKey(manifest, ReusableLibraryResourceKinds.Dynamo, key, $"Screen '{screen.Key}'"),
            id => ResolveManifestById(manifest, ReusableLibraryResourceKinds.VisualAsset, id, $"Screen '{screen.Key}'"),
            _ => throw new InvalidDataException("Screen cannot declare a Template dependency."));

    public static IReadOnlyCollection<ReusableLibraryDependency> AnalyzeManifest(
        PopupEngineeringDto popup,
        ReusableLibraryManifest manifest) =>
        Analyze(
            $"Popup '{popup.Key}'",
            popup.TemplateKey,
            popup.Elements,
            key => ResolveManifestByKey(manifest, ReusableLibraryResourceKinds.Dynamo, key, $"Popup '{popup.Key}'"),
            id => ResolveManifestById(manifest, ReusableLibraryResourceKinds.VisualAsset, id, $"Popup '{popup.Key}'"),
            key => ResolveManifestByKey(manifest, ReusableLibraryResourceKinds.EquipmentTemplate, key, $"Popup '{popup.Key}'"));

    public static void ValidateDeclaredDependencies(
        ScreenEngineeringDto screen,
        ReusableLibraryResourceEntry resource,
        ReusableLibraryManifest manifest) =>
        ValidateDeclared(resource, AnalyzeManifest(screen, manifest), $"Screen '{screen.Key}'");

    public static void ValidateDeclaredDependencies(
        PopupEngineeringDto popup,
        ReusableLibraryResourceEntry resource,
        ReusableLibraryManifest manifest) =>
        ValidateDeclared(resource, AnalyzeManifest(popup, manifest), $"Popup '{popup.Key}'");

    private static IReadOnlyCollection<ReusableLibraryDependency> Analyze(
        string owner,
        string? templateKey,
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        Func<string, ReusableLibraryDependency> resolveDynamo,
        Func<Guid, ReusableLibraryDependency> resolveAsset,
        Func<string, ReusableLibraryDependency> resolveTemplate)
    {
        var dependencies = new Dictionary<(string Kind, Guid ResourceId), ReusableLibraryDependency>();
        if (!string.IsNullOrWhiteSpace(templateKey))
            Add(dependencies, resolveTemplate(templateKey));

        AnalyzeElements(owner, elements, dependencies, resolveDynamo, resolveAsset);
        return dependencies.Values
            .OrderBy(dependency => dependency.Kind, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.ResourceId)
            .ToArray();
    }

    private static void AnalyzeElements(
        string owner,
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        IDictionary<(string Kind, Guid ResourceId), ReusableLibraryDependency> dependencies,
        Func<string, ReusableLibraryDependency> resolveDynamo,
        Func<Guid, ReusableLibraryDependency> resolveAsset)
    {
        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            if (element is null)
                throw new InvalidDataException($"{owner} contains a null visual element.");

            if (!string.IsNullOrWhiteSpace(element.EquipmentPath) && !ContainsPlaceholder(element.EquipmentPath))
                throw new InvalidDataException(
                    $"{owner} element '{element.Key}' has concrete equipment path '{element.EquipmentPath}'. Reusable views must keep project equipment context parameterized.");

            ValidatePortableBindings(element.Bindings, $"{owner} element '{element.Key}'");
            ValidatePortableDynamicSources(element, owner);

            foreach (var parameter in element.DynamoParameters ?? Array.Empty<DynamoParameterValueEngineeringDto>())
            {
                if (parameter?.TagReference is { TagId: var tagId } && tagId != Guid.Empty)
                    throw new InvalidDataException(
                        $"{owner} element '{element.Key}' parameter '{parameter.Key}' carries a concrete TAG reference. Reusable views must keep project data parameterized.");
            }

            if ((element.Actions?.Count ?? 0) > 0)
                throw new InvalidDataException(
                    $"{owner} element '{element.Key}' contains navigation/command actions. Screen/Popup action target closure is not reusable-library enabled in v1.");

            if (!string.IsNullOrWhiteSpace(element.DynamoKey))
                Add(dependencies, resolveDynamo(element.DynamoKey));

            if (string.Equals(element.Type, "core.image", StringComparison.Ordinal) &&
                element.Properties is not null &&
                element.Properties.TryGetValue("assetRef", out var assetReference) &&
                assetReference.ValueKind != JsonValueKind.Null)
            {
                Add(dependencies, resolveAsset(ParseVisualAssetReference(assetReference, owner, element.Key)));
            }

            AnalyzeElements(owner, element.Children, dependencies, resolveDynamo, resolveAsset);
        }
    }

    private static void ValidatePortableBindings(
        IReadOnlyCollection<EngineeringBindingDto>? bindings,
        string owner)
    {
        foreach (var binding in bindings ?? Array.Empty<EngineeringBindingDto>())
        {
            if (binding is null || binding.Kind is not (EngineeringBindingKind.Tag or EngineeringBindingKind.ClientMemory))
                continue;

            if (binding.TagReference is { TagId: var tagId } && tagId != Guid.Empty)
                throw new InvalidDataException(
                    $"{owner} binding '{binding.Key}' carries a concrete project data identity.");
            if (!ContainsPlaceholder(binding.Target))
                throw new InvalidDataException(
                    $"{owner} binding '{binding.Key}' targets concrete project data '{binding.Target}'. Reusable views must use placeholders/parameters instead.");
        }
    }

    private static void ValidatePortableDynamicSources(VisualElementEngineeringDto element, string owner)
    {
        foreach (var expression in element.PropertyExpressions ?? Array.Empty<VisualPropertyExpressionEngineeringDto>())
            ValidatePortableExpression(expression?.Expression, owner, element.Key);

        foreach (var condition in element.BooleanConditions ?? Array.Empty<VisualBooleanConditionEngineeringDto>())
            ValidatePortableSource(condition?.Source, owner, element.Key);

        if (element.AnalogFill is not null)
            ValidatePortableSource(element.AnalogFill.Source, owner, element.Key);
    }

    private static void ValidatePortableSource(
        VisualValueSourceEngineeringDto? source,
        string owner,
        string elementKey)
    {
        if (source is null) return;

        if (source.TagReference is { TagId: var tagId } && tagId != Guid.Empty)
            throw new InvalidDataException(
                $"{owner} element '{elementKey}' contains a concrete project data reference in dynamic behavior.");

        if (source.Kind is VisualValueSourceKind.Tag or VisualValueSourceKind.ClientMemory)
        {
            if (!ContainsPlaceholder(source.Target))
                throw new InvalidDataException(
                    $"{owner} element '{elementKey}' contains concrete dynamic source '{source.Target}'. Reusable views must keep project data parameterized.");
        }

        ValidatePortableExpression(source.Expression, owner, elementKey);
    }

    private static void ValidatePortableExpression(
        VisualExpressionEngineeringDto? expression,
        string owner,
        string elementKey)
    {
        var dependency = expression?.Dependencies?.FirstOrDefault();
        if (dependency is not null)
            throw new InvalidDataException(
                $"{owner} element '{elementKey}' expression dependency '{dependency.Symbol}' is bound to project data. Reusable view expression dependencies are not enabled until a parameterized canonical representation exists.");
    }

    private static void ValidateNoScriptCoupling(Guid viewId, string owner, IScriptEngineeringRegistry? scripts)
    {
        if (scripts is null) return;

        if (scripts.SnapshotVisualEventReferences().Any(reference => reference.VisualDefinitionId == viewId))
            throw new InvalidDataException(
                $"{owner} has project HMI Script event associations. Reusable Screen/Popup v1 does not silently detach Script behavior.");

        var viewKey = ScriptEngineeringReferenceKeys.VisualDefinition(viewId);
        var objectPrefix = viewKey + "/";
        foreach (var script in scripts.SnapshotScripts())
        {
            if (script.Dependencies.Any(dependency =>
                    (dependency.Kind == ScriptEngineeringDependencyKind.VisualDefinition &&
                     dependency.StableReference.Equals(viewKey, StringComparison.Ordinal)) ||
                    (dependency.Kind == ScriptEngineeringDependencyKind.VisualObject &&
                     dependency.StableReference.StartsWith(objectPrefix, StringComparison.Ordinal))))
            {
                throw new InvalidDataException(
                    $"{owner} is referenced by project Script '{script.Path}'. Reusable Screen/Popup v1 does not silently detach Script/HMI coupling.");
            }
        }
    }

    private static ReusableLibraryDependency ResolveWorkingDynamo(
        string key,
        IEngineeringAssetRegistry assets,
        string owner)
    {
        var dynamo = assets.FindDynamoByKey(key)
            ?? throw new InvalidDataException($"{owner} references Dynamo '{key}', which was not found in Working.");
        RequireStableIdentity(dynamo.Id, $"Dynamo '{key}'");
        return new ReusableLibraryDependency(ReusableLibraryResourceKinds.Dynamo, dynamo.Id!.Value);
    }

    private static ReusableLibraryDependency ResolveWorkingTemplate(
        string key,
        IEngineeringAssetRegistry assets,
        string owner)
    {
        var template = assets.FindTemplateByKey(key)
            ?? throw new InvalidDataException($"{owner} references template '{key}', which was not found in Working.");
        RequireStableIdentity(template.Id, $"Template '{key}'");
        return new ReusableLibraryDependency(ReusableLibraryResourceKinds.EquipmentTemplate, template.Id!.Value);
    }

    private static ReusableLibraryDependency ResolveWorkingAsset(
        Guid id,
        IVisualAssetEngineeringRegistry visualAssets,
        string owner)
    {
        var asset = visualAssets.FindAsset(id)
            ?? throw new InvalidDataException($"{owner} references Visual Asset '{id:D}', which was not found in Working.");
        RequireStableIdentity(asset.Id, $"Visual Asset '{asset.Key}'");
        return new ReusableLibraryDependency(ReusableLibraryResourceKinds.VisualAsset, asset.Id!.Value);
    }

    private static ReusableLibraryDependency ResolveManifestByKey(
        ReusableLibraryManifest manifest,
        string kind,
        string key,
        string owner)
    {
        var matches = manifest.Resources
            .Where(resource => resource.Kind == kind && resource.SourceKey.Equals(key, StringComparison.Ordinal))
            .ToArray();
        return matches.Length switch
        {
            1 => new ReusableLibraryDependency(kind, matches[0].ResourceId),
            0 => throw new InvalidDataException($"{owner} reusable dependency '{kind}:{key}' is missing from the library manifest."),
            _ => throw new InvalidDataException($"{owner} reusable dependency '{kind}:{key}' is ambiguous in the library manifest.")
        };
    }

    private static ReusableLibraryDependency ResolveManifestById(
        ReusableLibraryManifest manifest,
        string kind,
        Guid id,
        string owner)
    {
        var matches = manifest.Resources
            .Where(resource => resource.Kind == kind && resource.ResourceId == id)
            .ToArray();
        return matches.Length switch
        {
            1 => new ReusableLibraryDependency(kind, id),
            0 => throw new InvalidDataException($"{owner} reusable dependency '{kind}:{id:D}' is missing from the library manifest."),
            _ => throw new InvalidDataException($"{owner} reusable dependency '{kind}:{id:D}' is ambiguous in the library manifest.")
        };
    }

    private static Guid ParseVisualAssetReference(JsonElement reference, string owner, string elementKey)
    {
        if (reference.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{owner} element '{elementKey}' assetRef must be the canonical object form.");

        var properties = reference.EnumerateObject().ToArray();
        if (properties.Length != 1 ||
            !properties[0].NameEquals("assetId") ||
            properties[0].Value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"{owner} element '{elementKey}' assetRef must contain only the canonical assetId field.");

        var value = properties[0].Value.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{owner} element '{elementKey}' assetRef requires a stable asset identity.");

        var candidate = value.StartsWith("asset:", StringComparison.Ordinal) ? value["asset:".Length..] : value;
        if (!Guid.TryParse(candidate, out var assetId) || assetId == Guid.Empty)
            throw new InvalidDataException($"{owner} element '{elementKey}' assetRef '{value}' is not a stable Visual Asset GUID.");
        return assetId;
    }

    private static void ValidateDeclared(
        ReusableLibraryResourceEntry resource,
        IReadOnlyCollection<ReusableLibraryDependency> expectedDependencies,
        string owner)
    {
        var expected = expectedDependencies.Select(item => (item.Kind, item.ResourceId)).ToHashSet();
        var declared = (resource.Dependencies ?? Array.Empty<ReusableLibraryDependency>())
            .Select(item => (item.Kind, item.ResourceId))
            .ToHashSet();
        if (!expected.SetEquals(declared))
            throw new InvalidDataException(
                $"{owner} declared reusable dependencies do not exactly match dependencies derived from canonical content.");
    }

    private static void RequireStableIdentity(Guid? id, string owner)
    {
        if (!id.HasValue || id.Value == Guid.Empty)
            throw new InvalidDataException($"{owner} does not have stable identity.");
    }

    private static bool ContainsPlaceholder(string? value) =>
        value?.Contains('{', StringComparison.Ordinal) == true ||
        value?.Contains('}', StringComparison.Ordinal) == true;

    private static void Add(
        IDictionary<(string Kind, Guid ResourceId), ReusableLibraryDependency> dependencies,
        ReusableLibraryDependency dependency) =>
        dependencies.TryAdd((dependency.Kind, dependency.ResourceId), dependency);
}
