using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;
using Scada.Engineering.VisualScripting;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class AssetEngineeringHandler
{
    private readonly IEngineeringAssetRegistry _assets;
    private readonly ITagRegistry _tags;

    public AssetEngineeringHandler(IEngineeringAssetRegistry assets, ITagRegistry tags)
    {
        _assets = assets;
        _tags = tags;
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        PreviewTemplates(package, mode, items);
        PreviewEquipment(package, mode, items);
        PreviewDynamos(package, mode, items);
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        foreach (var dto in package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>())
        {
            var existing = ResolveExistingTemplate(dto);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip) { skipped++; continue; }
            _assets.UpsertTemplate(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }

        foreach (var dto in package.Equipment ?? Array.Empty<EquipmentEngineeringDto>())
        {
            var existing = ResolveExistingEquipment(dto);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip) { skipped++; continue; }
            _assets.UpsertEquipment(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }

        foreach (var dto in package.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
        {
            var existing = ResolveExistingDynamo(dto);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip) { skipped++; continue; }
            _assets.UpsertDynamo(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }
    }

    private void PreviewTemplates(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var templates = package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>();
        var duplicates = EngineeringHandlerSupport.Duplicates(templates.Select(x => x.Key));

        foreach (var dto in templates)
        {
            var issues = EngineeringValidator.ValidateTemplate(dto).ToList();
            if (duplicates.Contains(dto.Key))
                issues.Add(new(
                    "TEMPLATE_DUPLICATE_IN_FILE",
                    $"Template key '{dto.Key}' appears more than once in the import package.",
                    ImportEntityKind.Template,
                    dto.Key,
                    true));

            EngineeringHandlerSupport.ValidateConcreteTagBindings(
                _tags, dto.Bindings, ImportEntityKind.Template, dto.Key, package, issues);

            EngineeringHandlerSupport.AddPreview(
                items, ImportEntityKind.Template, dto.Key, ResolveExistingTemplate(dto) is not null, mode, issues);
        }
    }

    private void PreviewEquipment(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var equipment = package.Equipment ?? Array.Empty<EquipmentEngineeringDto>();
        var duplicates = EngineeringHandlerSupport.Duplicates(equipment.Select(x => x.Path));

        foreach (var dto in equipment)
        {
            var issues = EngineeringValidator.ValidateEquipment(dto).ToList();
            if (duplicates.Contains(dto.Path))
                issues.Add(new(
                    "EQUIPMENT_DUPLICATE_IN_FILE",
                    $"Equipment path '{dto.Path}' appears more than once in the import package.",
                    ImportEntityKind.Equipment,
                    dto.Path,
                    true));

            if (!string.IsNullOrWhiteSpace(dto.TemplateKey) && !TemplateExists(dto.TemplateKey, package))
                issues.Add(new(
                    "EQUIPMENT_TEMPLATE_NOT_FOUND",
                    $"Template '{dto.TemplateKey}' referenced by equipment '{dto.Path}' was not found.",
                    ImportEntityKind.Equipment,
                    dto.Path,
                    true));

            EngineeringHandlerSupport.ValidateConcreteTagBindings(
                _tags, dto.Bindings, ImportEntityKind.Equipment, dto.Path, package, issues);

            EngineeringHandlerSupport.AddPreview(
                items, ImportEntityKind.Equipment, dto.Path, ResolveExistingEquipment(dto) is not null, mode, issues);
        }
    }

    private void PreviewDynamos(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var dynamos = package.Dynamos ?? Array.Empty<DynamoEngineeringDto>();
        var duplicates = EngineeringHandlerSupport.Duplicates(dynamos.Select(x => x.Key));

        foreach (var dto in dynamos)
        {
            var issues = EngineeringValidator.ValidateDynamo(dto).ToList();
            issues.AddRange(VisualCompositionEngineeringValidation.ValidateDynamo(dto));
            if (duplicates.Contains(dto.Key))
                issues.Add(new(
                    "DYNAMO_DUPLICATE_IN_FILE",
                    $"Dynamo key '{dto.Key}' appears more than once in the import package.",
                    ImportEntityKind.Dynamo,
                    dto.Key,
                    true));

            if (!string.IsNullOrWhiteSpace(dto.TemplateKey) && !TemplateExists(dto.TemplateKey, package))
                issues.Add(new(
                    "DYNAMO_TEMPLATE_NOT_FOUND",
                    $"Template '{dto.TemplateKey}' referenced by dynamo '{dto.Key}' was not found.",
                    ImportEntityKind.Dynamo,
                    dto.Key,
                    true));

            EngineeringHandlerSupport.ValidateConcreteTagBindings(
                _tags, dto.Bindings, ImportEntityKind.Dynamo, dto.Key, package, issues);
            ValidateDynamoParameterReferences(dto, package, issues);
            ValidateDynamoVisualElements(dto.Elements, dto.Key, package, issues);

            EngineeringHandlerSupport.AddPreview(
                items, ImportEntityKind.Dynamo, dto.Key, ResolveExistingDynamo(dto) is not null, mode, issues);
        }
    }

    private void ValidateDynamoVisualElements(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            if (element is null) continue;
            issues.AddRange(BuiltinVisualEngineeringValidation.Validate(
                element,
                ImportEntityKind.Dynamo,
                entityKey,
                package.SchemaVersion));
            issues.AddRange(VisualCompositionEngineeringValidation.ValidateElement(
                element,
                ImportEntityKind.Dynamo,
                entityKey));
            EngineeringHandlerSupport.ValidateConcreteTagBindings(
                _tags,
                element.Bindings,
                ImportEntityKind.Dynamo,
                entityKey,
                package,
                issues);
            ValidateDynamoVisualElements(element.Children, entityKey, package, issues);
        }
    }

    private void ValidateDynamoParameterReferences(
        DynamoEngineeringDto dynamo,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        foreach (var parameter in dynamo.Parameters ?? Array.Empty<DynamoParameterDefinitionEngineeringDto>())
        {
            var reference = parameter?.DefaultTagReference;
            if (reference is null || reference.TagId == Guid.Empty) continue;

            if (!TryResolveTagDataType(reference.TagId, package, out var dataType))
            {
                issues.Add(new(
                    "DYNAMO_PARAMETER_TAG_NOT_FOUND",
                    $"Dynamo parameter '{parameter!.Key}' references TAG identity '{reference.TagId:D}', which was not found in the prospective Engineering model.",
                    ImportEntityKind.Dynamo,
                    dynamo.Key,
                    true));
                continue;
            }

            if (reference.Selector is not null &&
                !TagBitSemantics.TryValidateSelector(dataType, reference.Selector, out var selectorError))
            {
                issues.Add(new(
                    "DYNAMO_PARAMETER_TAG_SELECTOR_INVALID",
                    $"Dynamo parameter '{parameter!.Key}' has an invalid TAG selector: {selectorError}",
                    ImportEntityKind.Dynamo,
                    dynamo.Key,
                    true));
            }
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

    private bool TemplateExists(string key, EngineeringPackage package) =>
        _assets.FindTemplateByKey(key) is not null ||
        (package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>())
            .Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    private EquipmentTemplateEngineeringDto? ResolveExistingTemplate(EquipmentTemplateEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _assets.FindTemplate(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _assets.FindTemplateByKey(dto.Key);
    }

    private EquipmentEngineeringDto? ResolveExistingEquipment(EquipmentEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _assets.FindEquipment(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _assets.FindEquipmentByPath(dto.Path);
    }

    private DynamoEngineeringDto? ResolveExistingDynamo(DynamoEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _assets.FindDynamo(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _assets.FindDynamoByKey(dto.Key);
    }
}
