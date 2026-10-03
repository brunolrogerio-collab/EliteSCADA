using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;
using Scada.Engineering.VisualScripting;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class AssetEngineeringHandler
{
    private readonly IEngineeringAssetRegistry _assets;
    private readonly ITagRegistry _tags;
    private readonly IVisualAssetEngineeringRegistry _visualAssets;

    public AssetEngineeringHandler(
        IEngineeringAssetRegistry assets,
        ITagRegistry tags,
        IVisualAssetEngineeringRegistry visualAssets)
    {
        _assets = assets;
        _tags = tags;
        _visualAssets = visualAssets;
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
            var normalized = NormalizeEquipmentTemplateReference(dto);
            _assets.UpsertEquipment(normalized with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }

        foreach (var dto in package.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
        {
            var existing = ResolveExistingDynamo(dto);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip) { skipped++; continue; }
            var normalized = NormalizeDynamoTemplateReference(dto);
            _assets.UpsertDynamo(normalized with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
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
            ValidateTemplateIdentityCollision(dto, issues);

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
            ValidateEquipmentIdentityCollision(dto, issues);
            ValidateTemplateReference(
                dto.TemplateId,
                dto.TemplateKey,
                "EQUIPMENT",
                ImportEntityKind.Equipment,
                dto.Path,
                package,
                issues);

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
            ValidateDynamoIdentityCollision(dto, issues);
            ValidateTemplateReference(
                dto.TemplateId,
                dto.TemplateKey,
                "DYNAMO",
                ImportEntityKind.Dynamo,
                dto.Key,
                package,
                issues);

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
            issues.AddRange(VisualAssetReferenceEngineeringValidation.Validate(
                element,
                ImportEntityKind.Dynamo,
                entityKey,
                package,
                _visualAssets));
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

    private EquipmentEngineeringDto NormalizeEquipmentTemplateReference(EquipmentEngineeringDto dto)
    {
        if (!dto.TemplateId.HasValue && string.IsNullOrWhiteSpace(dto.TemplateKey))
            return dto;

        var template = dto.TemplateId.HasValue
            ? _assets.FindTemplate(dto.TemplateId.Value)
            : null;
        template ??= string.IsNullOrWhiteSpace(dto.TemplateKey)
            ? null
            : _assets.FindTemplateByKey(dto.TemplateKey);
        if (template?.Id is null || template.Id == Guid.Empty)
            throw new InvalidOperationException(
                $"Equipment '{dto.Path}' Template reference could not be normalized to a stable identity.");

        return dto with
        {
            TemplateId = template.Id,
            TemplateKey = template.Key
        };
    }

    private DynamoEngineeringDto NormalizeDynamoTemplateReference(DynamoEngineeringDto dto)
    {
        if (!dto.TemplateId.HasValue && string.IsNullOrWhiteSpace(dto.TemplateKey))
            return dto;

        var template = dto.TemplateId.HasValue
            ? _assets.FindTemplate(dto.TemplateId.Value)
            : null;
        template ??= string.IsNullOrWhiteSpace(dto.TemplateKey)
            ? null
            : _assets.FindTemplateByKey(dto.TemplateKey);
        if (template?.Id is null || template.Id == Guid.Empty)
            throw new InvalidOperationException(
                $"Dynamo '{dto.Key}' Template reference could not be normalized to a stable identity.");

        return dto with
        {
            TemplateId = template.Id,
            TemplateKey = template.Key
        };
    }

    private void ValidateTemplateIdentityCollision(
        EquipmentTemplateEngineeringDto dto,
        List<ImportIssue> issues)
    {
        if (!dto.Id.HasValue || dto.Id == Guid.Empty || string.IsNullOrWhiteSpace(dto.Key))
            return;
        var byKey = _assets.FindTemplateByKey(dto.Key);
        if (byKey?.Id.HasValue == true && byKey.Id != dto.Id)
            issues.Add(new(
                "TEMPLATE_STABLE_ID_KEY_COLLISION",
                $"Template key '{dto.Key}' already belongs to stable identity '{byKey.Id.Value:D}', not incoming identity '{dto.Id.Value:D}'.",
                ImportEntityKind.Template,
                dto.Key,
                true));
    }

    private void ValidateEquipmentIdentityCollision(
        EquipmentEngineeringDto dto,
        List<ImportIssue> issues)
    {
        if (!dto.Id.HasValue || dto.Id == Guid.Empty || string.IsNullOrWhiteSpace(dto.Path))
            return;
        var byPath = _assets.FindEquipmentByPath(dto.Path);
        if (byPath?.Id.HasValue == true && byPath.Id != dto.Id)
            issues.Add(new(
                "EQUIPMENT_STABLE_ID_PATH_COLLISION",
                $"Equipment path '{dto.Path}' already belongs to stable identity '{byPath.Id.Value:D}', not incoming identity '{dto.Id.Value:D}'.",
                ImportEntityKind.Equipment,
                dto.Path,
                true));
    }

    private void ValidateDynamoIdentityCollision(
        DynamoEngineeringDto dto,
        List<ImportIssue> issues)
    {
        if (!dto.Id.HasValue || dto.Id == Guid.Empty || string.IsNullOrWhiteSpace(dto.Key))
            return;
        var byKey = _assets.FindDynamoByKey(dto.Key);
        if (byKey?.Id.HasValue == true && byKey.Id != dto.Id)
            issues.Add(new(
                "DYNAMO_STABLE_ID_KEY_COLLISION",
                $"Dynamo key '{dto.Key}' already belongs to stable identity '{byKey.Id.Value:D}', not incoming identity '{dto.Id.Value:D}'.",
                ImportEntityKind.Dynamo,
                dto.Key,
                true));
    }

    private void ValidateTemplateReference(
        Guid? templateId,
        string? templateKey,
        string codePrefix,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        if (templateId == Guid.Empty)
        {
            issues.Add(new(
                $"{codePrefix}_TEMPLATE_ID_INVALID",
                $"{kind} '{entityKey}' declares an empty Template identity.",
                kind,
                entityKey,
                true));
            return;
        }
        if (!templateId.HasValue && string.IsNullOrWhiteSpace(templateKey))
            return;

        var byId = templateId.HasValue ? FindTemplate(templateId.Value, package) : null;
        var byKey = !string.IsNullOrWhiteSpace(templateKey) ? FindTemplate(templateKey!, package) : null;

        if (templateId.HasValue && byId is null)
            issues.Add(new(
                $"{codePrefix}_TEMPLATE_ID_NOT_FOUND",
                $"Template identity '{templateId.Value:D}' referenced by {kind.ToString().ToLowerInvariant()} '{entityKey}' was not found.",
                kind,
                entityKey,
                true));
        if (!templateId.HasValue && !string.IsNullOrWhiteSpace(templateKey) && byKey is null)
            issues.Add(new(
                $"{codePrefix}_TEMPLATE_NOT_FOUND",
                $"Template '{templateKey}' referenced by {kind.ToString().ToLowerInvariant()} '{entityKey}' was not found.",
                kind,
                entityKey,
                true));
        if (byId?.Id.HasValue == true &&
            byKey?.Id.HasValue == true &&
            byId.Id != byKey.Id)
        {
            issues.Add(new(
                $"{codePrefix}_TEMPLATE_REFERENCE_MISMATCH",
                $"Template identity '{templateId:D}' and alias '{templateKey}' resolve to different Templates for '{entityKey}'.",
                kind,
                entityKey,
                true));
        }
    }

    private EquipmentTemplateEngineeringDto? FindTemplate(Guid id, EngineeringPackage package) =>
        _assets.FindTemplate(id) ??
        (package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>())
            .FirstOrDefault(x => x.Id == id);

    private EquipmentTemplateEngineeringDto? FindTemplate(string key, EngineeringPackage package) =>
        _assets.FindTemplateByKey(key) ??
        (package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>())
            .FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

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
