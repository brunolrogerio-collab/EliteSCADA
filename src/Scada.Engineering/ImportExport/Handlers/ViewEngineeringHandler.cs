using System.Text.Json;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;
using Scada.Engineering.VisualScripting;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class ViewEngineeringHandler
{
    private readonly IEngineeringViewRegistry _views;
    private readonly IEngineeringAssetRegistry _assets;
    private readonly ITagRegistry _tags;
    private readonly IVisualAssetEngineeringRegistry _visualAssets;

    public ViewEngineeringHandler(
        IEngineeringViewRegistry views,
        IEngineeringAssetRegistry assets,
        ITagRegistry tags,
        IVisualAssetEngineeringRegistry? visualAssets = null)
    {
        _views = views;
        _assets = assets;
        _tags = tags;
        _visualAssets = visualAssets ?? new InMemoryVisualAssetEngineeringRegistry();
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        PreviewScreens(package, mode, items);
        PreviewPopups(package, mode, items);
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        foreach (var dto in package.Screens ?? Array.Empty<ScreenEngineeringDto>())
        {
            var existing = ResolveExistingScreen(dto);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip) { skipped++; continue; }

            var normalized = VisualEngineeringPropertyMigration.NormalizeScreen(dto, package.SchemaVersion);
            _views.UpsertScreen(normalized with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }

        foreach (var dto in package.Popups ?? Array.Empty<PopupEngineeringDto>())
        {
            var existing = ResolveExistingPopup(dto);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip) { skipped++; continue; }

            var normalized = VisualEngineeringPropertyMigration.NormalizePopup(dto, package.SchemaVersion);
            _views.UpsertPopup(normalized with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }
    }

    private void PreviewScreens(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var screens = package.Screens ?? Array.Empty<ScreenEngineeringDto>();
        var validScreens = screens.Where(x => x is not null).ToArray();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(validScreens.Select(x => x.Key));
        var duplicateRoutes = EngineeringHandlerSupport.Duplicates(validScreens.Select(x => x.Route ?? string.Empty));

        foreach (var dto in screens)
        {
            if (dto is null)
            {
                EngineeringHandlerSupport.AddPreview(
                    items,
                    ImportEntityKind.Screen,
                    "<null>",
                    false,
                    mode,
                    [new("SCREEN_NULL", "Screen cannot be null.", ImportEntityKind.Screen, "<null>", true)]);
                continue;
            }

            var entityKey = string.IsNullOrWhiteSpace(dto.Key) ? "<invalid-screen>" : dto.Key;
            var issues = EngineeringValidator.ValidateScreen(dto).ToList();
            if (!string.IsNullOrWhiteSpace(dto.Key) && duplicateKeys.Contains(dto.Key))
                issues.Add(new(
                    "SCREEN_DUPLICATE_IN_FILE",
                    $"Screen key '{dto.Key}' appears more than once in the import package.",
                    ImportEntityKind.Screen,
                    entityKey,
                    true));

            if (!string.IsNullOrWhiteSpace(dto.Route) && duplicateRoutes.Contains(dto.Route))
                issues.Add(new(
                    "SCREEN_ROUTE_DUPLICATE",
                    $"Screen route '{dto.Route}' appears more than once in the import package.",
                    ImportEntityKind.Screen,
                    entityKey,
                    true));

            ValidateVisualReferences(dto.Elements, ImportEntityKind.Screen, entityKey, package, issues);
            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.Screen,
                entityKey,
                ResolveExistingScreen(dto) is not null,
                mode,
                issues);
        }
    }

    private void PreviewPopups(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var popups = package.Popups ?? Array.Empty<PopupEngineeringDto>();
        var validPopups = popups.Where(x => x is not null).ToArray();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(validPopups.Select(x => x.Key));

        foreach (var dto in popups)
        {
            if (dto is null)
            {
                EngineeringHandlerSupport.AddPreview(
                    items,
                    ImportEntityKind.Popup,
                    "<null>",
                    false,
                    mode,
                    [new("POPUP_NULL", "Popup cannot be null.", ImportEntityKind.Popup, "<null>", true)]);
                continue;
            }

            var entityKey = string.IsNullOrWhiteSpace(dto.Key) ? "<invalid-popup>" : dto.Key;
            var issues = EngineeringValidator.ValidatePopup(dto).ToList();
            if (!string.IsNullOrWhiteSpace(dto.Key) && duplicateKeys.Contains(dto.Key))
                issues.Add(new(
                    "POPUP_DUPLICATE_IN_FILE",
                    $"Popup key '{dto.Key}' appears more than once in the import package.",
                    ImportEntityKind.Popup,
                    entityKey,
                    true));

            if (!string.IsNullOrWhiteSpace(dto.TemplateKey) && !TemplateExists(dto.TemplateKey, package))
                issues.Add(new(
                    "POPUP_TEMPLATE_NOT_FOUND",
                    $"Template '{dto.TemplateKey}' referenced by popup '{dto.Key}' was not found.",
                    ImportEntityKind.Popup,
                    entityKey,
                    true));

            ValidateVisualReferences(dto.Elements, ImportEntityKind.Popup, entityKey, package, issues);
            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.Popup,
                entityKey,
                ResolveExistingPopup(dto) is not null,
                mode,
                issues);
        }
    }

    private void ValidateVisualReferences(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            // EngineeringValidator already records a null element as invalid input.
            // Reference/schema traversal must stop at that node instead of throwing.
            if (element is null)
                continue;

            issues.AddRange(BuiltinVisualEngineeringValidation.Validate(
                element,
                kind,
                entityKey,
                package.SchemaVersion));

            ValidateVisualAssetReference(element, kind, entityKey, package, issues);
            ValidateDynamicReferences(element, kind, entityKey, package, issues);

            EngineeringHandlerSupport.ValidateConcreteTagBindings(
                _tags, element.Bindings, kind, entityKey, package, issues);

            if (!string.IsNullOrWhiteSpace(element.DynamoKey) && !DynamoExists(element.DynamoKey, package))
                issues.Add(new(
                    "VISUAL_DYNAMO_NOT_FOUND",
                    $"Dynamo '{element.DynamoKey}' referenced by visual element '{element.Key}' was not found.",
                    kind,
                    entityKey,
                    true));

            if (!string.IsNullOrWhiteSpace(element.EquipmentPath) &&
                !EngineeringHandlerSupport.ContainsPlaceholder(element.EquipmentPath) &&
                !EquipmentExists(element.EquipmentPath, package))
            {
                issues.Add(new(
                    "VISUAL_EQUIPMENT_NOT_FOUND",
                    $"Equipment '{element.EquipmentPath}' referenced by visual element '{element.Key}' was not found.",
                    kind,
                    entityKey,
                    true));
            }

            ValidateVisualReferences(element.Children, kind, entityKey, package, issues);
        }
    }

    private void ValidateDynamicReferences(
        VisualElementEngineeringDto element,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        foreach (var propertyExpression in element.PropertyExpressions ?? Array.Empty<VisualPropertyExpressionEngineeringDto>())
        {
            if (propertyExpression?.Expression is not null)
                ValidateExpressionDependencies(propertyExpression.Expression, kind, entityKey, package, issues);
        }

        foreach (var condition in element.BooleanConditions ?? Array.Empty<VisualBooleanConditionEngineeringDto>())
        {
            if (condition?.Source is not null)
                ValidateValueSourceReferences(condition.Source, kind, entityKey, package, issues);
        }

        if (element.AnalogFill?.Source is not null)
            ValidateValueSourceReferences(element.AnalogFill.Source, kind, entityKey, package, issues);
    }

    private void ValidateValueSourceReferences(
        VisualValueSourceEngineeringDto source,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        switch (source.Kind)
        {
            case VisualValueSourceKind.Tag:
            case VisualValueSourceKind.ClientMemory:
                if (source.TagReference is not null)
                    ValidateDynamicTagReference(source.TagReference, source.ValueType, source.Target, kind, entityKey, package, issues);
                break;
            case VisualValueSourceKind.Expression:
                if (source.Expression is not null)
                    ValidateExpressionDependencies(source.Expression, kind, entityKey, package, issues);
                break;
        }
    }

    private void ValidateExpressionDependencies(
        VisualExpressionEngineeringDto expression,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        foreach (var dependency in expression.Dependencies ?? Array.Empty<VisualExpressionDependencyEngineeringDto>())
        {
            if (dependency?.TagReference is null)
                continue;
            ValidateDynamicTagReference(
                dependency.TagReference,
                dependency.ValueType,
                dependency.Target ?? dependency.Symbol,
                kind,
                entityKey,
                package,
                issues);
        }
    }

    private void ValidateDynamicTagReference(
        TagValueReference reference,
        VisualExpressionValueType declaredType,
        string? displayTarget,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        var label = string.IsNullOrWhiteSpace(displayTarget) ? reference.TagId.ToString("D") : displayTarget;
        if (reference.TagId == Guid.Empty)
            return; // Structural validator owns the empty-ID diagnostic.

        if (!TryResolveTagDataType(reference.TagId, package, out var dataType))
        {
            issues.Add(new(
                "VISUAL_DYNAMIC_REFERENCE_NOT_FOUND",
                $"Visual dynamic source '{label}' references TAG identity '{reference.TagId:D}', which was not found in the prospective Engineering model.",
                kind,
                entityKey,
                true));
            return;
        }

        if (reference.Selector is not null && !TagBitSemantics.TryValidateSelector(dataType, reference.Selector, out var selectorError))
        {
            issues.Add(new(
                "VISUAL_DYNAMIC_REFERENCE_SELECTOR_INVALID",
                $"Visual dynamic source '{label}' has an invalid TAG selector: {selectorError}",
                kind,
                entityKey,
                true));
            return;
        }

        var actualType = reference.Selector is not null
            ? VisualExpressionValueType.Boolean
            : ToExpressionValueType(dataType);
        if (!actualType.HasValue)
        {
            issues.Add(new(
                "VISUAL_DYNAMIC_REFERENCE_TYPE_INVALID",
                $"Visual dynamic source '{label}' references TAG type '{dataType}', which is not a supported Boolean/numeric expression dependency.",
                kind,
                entityKey,
                true));
            return;
        }

        if (actualType.Value != declaredType)
        {
            issues.Add(new(
                "VISUAL_DYNAMIC_REFERENCE_TYPE_MISMATCH",
                $"Visual dynamic source '{label}' declares {declaredType} but resolves to {actualType.Value}.",
                kind,
                entityKey,
                true));
        }
    }

    private bool TryResolveTagDataType(Guid tagId, EngineeringPackage package, out TagDataType dataType)
    {
        if (_tags.TryGet(tagId, out var existing) && existing is not null)
        {
            dataType = existing.DataType;
            return true;
        }

        var prospective = package.Tags.FirstOrDefault(tag => tag is not null && tag.Id == tagId);
        if (prospective is not null)
        {
            dataType = prospective.DataType;
            return true;
        }

        dataType = default;
        return false;
    }

    private static VisualExpressionValueType? ToExpressionValueType(TagDataType dataType) => dataType switch
    {
        TagDataType.Boolean => VisualExpressionValueType.Boolean,
        TagDataType.Int16 or TagDataType.Int32 or TagDataType.Int64 or TagDataType.Float or TagDataType.Double => VisualExpressionValueType.Number,
        _ => null
    };

    private void ValidateVisualAssetReference(
        VisualElementEngineeringDto element,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        // First-class project image assets were introduced in schema v13. Older
        // packages retain the Wave-07 syntactic assetRef contract without having
        // a project asset collection to resolve against.
        if (package.SchemaVersion < 13 ||
            !element.Type.Equals("core.image", StringComparison.Ordinal) ||
            element.Properties is null ||
            !element.Properties.TryGetValue("assetRef", out var serialized) ||
            serialized.ValueKind == JsonValueKind.Null)
            return;

        if (serialized.ValueKind != JsonValueKind.Object)
            return; // BuiltinVisualEngineeringValidation reports the malformed value.

        var fields = serialized.EnumerateObject().ToArray();
        if (fields.Length != 1 ||
            !fields[0].NameEquals("assetId") ||
            fields[0].Value.ValueKind != JsonValueKind.String)
            return; // The property-schema validator owns shape diagnostics.

        var reference = fields[0].Value.GetString();
        if (string.IsNullOrWhiteSpace(reference))
            return; // The public property validator already rejects an unstable identity.

        var guidText = reference.StartsWith("asset:", StringComparison.Ordinal)
            ? reference["asset:".Length..]
            : reference;

        if (!Guid.TryParse(guidText, out var assetId) || assetId == Guid.Empty)
        {
            issues.Add(new ImportIssue(
                "VISUAL_ASSET_REFERENCE_ID_INVALID",
                $"Visual element '{element.Key}' assetRef must identify a stable Visual Asset GUID.",
                kind,
                entityKey,
                true));
            return;
        }

        if (VisualAssetExists(assetId, package))
            return;

        issues.Add(new ImportIssue(
            "VISUAL_ASSET_REFERENCE_NOT_FOUND",
            $"Visual element '{element.Key}' references Visual Asset '{reference}', which does not exist in the prospective Engineering model.",
            kind,
            entityKey,
            true));
    }

    private bool VisualAssetExists(Guid id, EngineeringPackage package) =>
        _visualAssets.FindAsset(id) is not null ||
        (package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
            .Any(x => x is not null && x.Id == id);

    private bool TemplateExists(string key, EngineeringPackage package) =>
        _assets.FindTemplateByKey(key) is not null ||
        (package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>())
            .Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    private bool EquipmentExists(string path, EngineeringPackage package) =>
        _assets.FindEquipmentByPath(path) is not null ||
        (package.Equipment ?? Array.Empty<EquipmentEngineeringDto>())
            .Any(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

    private bool DynamoExists(string key, EngineeringPackage package) =>
        _assets.FindDynamoByKey(key) is not null ||
        (package.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
            .Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    private ScreenEngineeringDto? ResolveExistingScreen(ScreenEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _views.FindScreen(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return string.IsNullOrWhiteSpace(dto.Key) ? null : _views.FindScreenByKey(dto.Key);
    }

    private PopupEngineeringDto? ResolveExistingPopup(PopupEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _views.FindPopup(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return string.IsNullOrWhiteSpace(dto.Key) ? null : _views.FindPopupByKey(dto.Key);
    }
}
