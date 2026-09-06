using System.Text.Json;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.Libraries;

/// <summary>
/// Reusable-Dynamo dependency authority for canonical Dynamo composition v1.
/// It derives only resource dependencies that are already valid in the shipped
/// Engineering contract and rejects project-bound or unsupported composition
/// instead of weakening canonical validation for library reuse.
/// </summary>
public static class ReusableDynamoDependencyAnalyzer
{
    public static IReadOnlyCollection<ReusableLibraryDependency> AnalyzeWorking(
        DynamoEngineeringDto dynamo,
        IEngineeringAssetRegistry assets,
        IVisualAssetEngineeringRegistry visualAssets)
    {
        ArgumentNullException.ThrowIfNull(dynamo);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(visualAssets);

        return Analyze(
            dynamo,
            templateKey =>
            {
                var template = assets.FindTemplateByKey(templateKey)
                    ?? throw new InvalidDataException(
                        $"Dynamo '{dynamo.Key}' references template '{templateKey}', which was not found in Working.");
                if (!template.Id.HasValue || template.Id == Guid.Empty)
                    throw new InvalidDataException(
                        $"Dynamo '{dynamo.Key}' template dependency '{templateKey}' does not have stable identity.");
                return new ReusableLibraryDependency(
                    ReusableLibraryResourceKinds.EquipmentTemplate,
                    template.Id.Value);
            },
            assetId =>
            {
                var asset = visualAssets.FindAsset(assetId)
                    ?? throw new InvalidDataException(
                        $"Dynamo '{dynamo.Key}' references visual asset '{assetId:D}', which was not found in Working.");
                if (!asset.Id.HasValue || asset.Id == Guid.Empty)
                    throw new InvalidDataException(
                        $"Dynamo '{dynamo.Key}' visual asset dependency '{assetId:D}' does not have stable identity.");
                return new ReusableLibraryDependency(
                    ReusableLibraryResourceKinds.VisualAsset,
                    asset.Id.Value);
            });
    }

    public static IReadOnlyCollection<ReusableLibraryDependency> AnalyzeManifest(
        DynamoEngineeringDto dynamo,
        ReusableLibraryManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(dynamo);
        ArgumentNullException.ThrowIfNull(manifest);

        ReusableLibraryResourceEntry ResolveByKey(string kind, string key)
        {
            var matches = manifest.Resources
                .Where(resource =>
                    resource.Kind.Equals(kind, StringComparison.Ordinal) &&
                    resource.SourceKey.Equals(key, StringComparison.Ordinal))
                .ToArray();
            return matches.Length switch
            {
                1 => matches[0],
                0 => throw new InvalidDataException(
                    $"Dynamo '{dynamo.Key}' reusable dependency '{kind}:{key}' is missing from the library manifest."),
                _ => throw new InvalidDataException(
                    $"Dynamo '{dynamo.Key}' reusable dependency '{kind}:{key}' is ambiguous in the library manifest.")
            };
        }

        ReusableLibraryResourceEntry ResolveById(string kind, Guid id)
        {
            var matches = manifest.Resources
                .Where(resource =>
                    resource.Kind.Equals(kind, StringComparison.Ordinal) &&
                    resource.ResourceId == id)
                .ToArray();
            return matches.Length switch
            {
                1 => matches[0],
                0 => throw new InvalidDataException(
                    $"Dynamo '{dynamo.Key}' reusable dependency '{kind}:{id:D}' is missing from the library manifest."),
                _ => throw new InvalidDataException(
                    $"Dynamo '{dynamo.Key}' reusable dependency '{kind}:{id:D}' is ambiguous in the library manifest.")
            };
        }

        return Analyze(
            dynamo,
            templateKey =>
            {
                var resource = ResolveByKey(ReusableLibraryResourceKinds.EquipmentTemplate, templateKey);
                return new ReusableLibraryDependency(resource.Kind, resource.ResourceId);
            },
            assetId =>
            {
                var resource = ResolveById(ReusableLibraryResourceKinds.VisualAsset, assetId);
                return new ReusableLibraryDependency(resource.Kind, resource.ResourceId);
            });
    }

    public static void ValidateDeclaredDependencies(
        DynamoEngineeringDto dynamo,
        ReusableLibraryResourceEntry resource,
        ReusableLibraryManifest manifest)
    {
        var expected = AnalyzeManifest(dynamo, manifest)
            .Select(dependency => (dependency.Kind, dependency.ResourceId))
            .ToHashSet();
        var declared = (resource.Dependencies ?? Array.Empty<ReusableLibraryDependency>())
            .Select(dependency => (dependency.Kind, dependency.ResourceId))
            .ToHashSet();

        if (!expected.SetEquals(declared))
            throw new InvalidDataException(
                $"Dynamo '{dynamo.Key}' declared reusable dependencies do not exactly match dependencies derived from canonical content.");
    }

    private static IReadOnlyCollection<ReusableLibraryDependency> Analyze(
        DynamoEngineeringDto dynamo,
        Func<string, ReusableLibraryDependency> resolveTemplate,
        Func<Guid, ReusableLibraryDependency> resolveAsset)
    {
        var dependencies = new Dictionary<(string Kind, Guid ResourceId), ReusableLibraryDependency>();
        ValidatePortableBindings(dynamo.Bindings, $"Dynamo '{dynamo.Key}'");

        if (!string.IsNullOrWhiteSpace(dynamo.TemplateKey))
            Add(dependencies, resolveTemplate(dynamo.TemplateKey));

        foreach (var parameter in dynamo.Parameters ?? Array.Empty<DynamoParameterDefinitionEngineeringDto>())
        {
            if (parameter?.DefaultTagReference is { TagId: var tagId } && tagId != Guid.Empty)
                throw new InvalidDataException(
                    $"Dynamo '{dynamo.Key}' parameter '{parameter.Key}' has a concrete TAG default. Reusable Dynamos must keep project TAG references parameterized.");
        }

        AnalyzeElements(
            dynamo.Key,
            dynamo.Elements,
            dependencies,
            resolveAsset);

        return dependencies.Values
            .OrderBy(dependency => dependency.Kind, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.ResourceId)
            .ToArray();
    }

    private static void AnalyzeElements(
        string ownerKey,
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        IDictionary<(string Kind, Guid ResourceId), ReusableLibraryDependency> dependencies,
        Func<Guid, ReusableLibraryDependency> resolveAsset)
    {
        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            if (element is null)
                throw new InvalidDataException($"Dynamo '{ownerKey}' contains a null visual element.");

            if (!string.IsNullOrWhiteSpace(element.EquipmentPath) && !ContainsPlaceholder(element.EquipmentPath))
                throw new InvalidDataException(
                    $"Dynamo '{ownerKey}' element '{element.Key}' has concrete equipment path '{element.EquipmentPath}'. Reusable Dynamos must keep project equipment context parameterized.");

            ValidatePortableBindings(element.Bindings, $"Dynamo '{ownerKey}' element '{element.Key}'");
            ValidatePortableDynamicSources(element, ownerKey);

            foreach (var parameter in element.DynamoParameters ?? Array.Empty<DynamoParameterValueEngineeringDto>())
            {
                if (parameter?.TagReference is { TagId: var tagId } && tagId != Guid.Empty)
                    throw new InvalidDataException(
                        $"Dynamo '{ownerKey}' element '{element.Key}' parameter '{parameter.Key}' carries a concrete TAG reference. Reusable Dynamos must keep project TAG references parameterized.");
            }

            if ((element.Actions?.Count ?? 0) > 0)
                throw new InvalidDataException(
                    $"Dynamo '{ownerKey}' element '{element.Key}' contains navigation/command actions. Action target dependencies are not reusable-library enabled in the Dynamo slice yet.");

            if (!string.IsNullOrWhiteSpace(element.DynamoKey))
                throw new InvalidDataException(
                    $"Dynamo '{ownerKey}' element '{element.Key}' nests Dynamo '{element.DynamoKey}'. Canonical Dynamo composition version 1 does not support nested Dynamos, so reusable libraries cannot enable that composition implicitly.");

            if (string.Equals(element.Type, "core.image", StringComparison.Ordinal) &&
                element.Properties is not null &&
                element.Properties.TryGetValue("assetRef", out var assetReference) &&
                assetReference.ValueKind != JsonValueKind.Null)
            {
                Add(dependencies, resolveAsset(ParseVisualAssetReference(assetReference, ownerKey, element.Key)));
            }

            AnalyzeElements(ownerKey, element.Children, dependencies, resolveAsset);
        }
    }

    private static void ValidatePortableBindings(
        IReadOnlyCollection<EngineeringBindingDto>? bindings,
        string owner)
    {
        foreach (var binding in bindings ?? Array.Empty<EngineeringBindingDto>())
        {
            if (binding is null) continue;
            if (binding.Kind is not (EngineeringBindingKind.Tag or EngineeringBindingKind.ClientMemory))
                continue;

            if (binding.TagReference is { TagId: var tagId } && tagId != Guid.Empty)
                throw new InvalidDataException(
                    $"{owner} binding '{binding.Key}' carries a concrete project data identity. Reusable Dynamos must use placeholders/parameters instead.");
            if (!ContainsPlaceholder(binding.Target))
                throw new InvalidDataException(
                    $"{owner} binding '{binding.Key}' targets concrete project data '{binding.Target}'. Reusable Dynamos must use placeholders/parameters instead.");
        }
    }

    private static void ValidatePortableDynamicSources(VisualElementEngineeringDto element, string ownerKey)
    {
        foreach (var expression in element.PropertyExpressions ?? Array.Empty<VisualPropertyExpressionEngineeringDto>())
            ValidatePortableExpression(expression?.Expression, ownerKey, element.Key);

        foreach (var condition in element.BooleanConditions ?? Array.Empty<VisualBooleanConditionEngineeringDto>())
            ValidatePortableSource(condition?.Source, ownerKey, element.Key);

        if (element.AnalogFill is not null)
            ValidatePortableSource(element.AnalogFill.Source, ownerKey, element.Key);
    }

    private static void ValidatePortableSource(
        VisualValueSourceEngineeringDto? source,
        string ownerKey,
        string elementKey)
    {
        if (source is null) return;

        if (source.TagReference is { TagId: var tagId } && tagId != Guid.Empty)
            throw new InvalidDataException(
                $"Dynamo '{ownerKey}' element '{elementKey}' contains a concrete project data reference in dynamic behavior.");

        if (source.Kind is VisualValueSourceKind.Tag or VisualValueSourceKind.ClientMemory)
        {
            if (!ContainsPlaceholder(source.Target))
                throw new InvalidDataException(
                    $"Dynamo '{ownerKey}' element '{elementKey}' contains concrete dynamic source '{source.Target}'. Reusable Dynamos must keep project data parameterized.");
        }

        ValidatePortableExpression(source.Expression, ownerKey, elementKey);
    }

    private static void ValidatePortableExpression(
        VisualExpressionEngineeringDto? expression,
        string ownerKey,
        string elementKey)
    {
        var dependency = expression?.Dependencies?.FirstOrDefault();
        if (dependency is not null)
            throw new InvalidDataException(
                $"Dynamo '{ownerKey}' element '{elementKey}' expression dependency '{dependency.Symbol}' is bound to project data. Reusable Dynamo expression dependencies are not enabled until a parameterized canonical representation exists.");
    }

    private static Guid ParseVisualAssetReference(
        JsonElement reference,
        string ownerKey,
        string elementKey)
    {
        if (reference.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(
                $"Dynamo '{ownerKey}' element '{elementKey}' assetRef must be the canonical object form.");

        var properties = reference.EnumerateObject().ToArray();
        if (properties.Length != 1 ||
            !properties[0].NameEquals("assetId") ||
            properties[0].Value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException(
                $"Dynamo '{ownerKey}' element '{element.Key}' assetRef must contain only the canonical assetId field.");

        var value = properties[0].Value.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException(
                $"Dynamo '{ownerKey}' element '{elementKey}' assetRef requires a stable asset identity.");

        var candidate = value.StartsWith("asset:", StringComparison.Ordinal)
            ? value["asset:".Length..]
            : value;
        if (!Guid.TryParse(candidate, out var assetId) || assetId == Guid.Empty)
            throw new InvalidDataException(
                $"Dynamo '{ownerKey}' element '{elementKey}' assetRef '{value}' is not a stable project asset GUID.");
        return assetId;
    }

    private static bool ContainsPlaceholder(string? value) =>
        value?.Contains('{', StringComparison.Ordinal) == true ||
        value?.Contains('}', StringComparison.Ordinal) == true;

    private static void Add(
        IDictionary<(string Kind, Guid ResourceId), ReusableLibraryDependency> dependencies,
        ReusableLibraryDependency dependency)
    {
        dependencies.TryAdd((dependency.Kind, dependency.ResourceId), dependency);
    }
}
