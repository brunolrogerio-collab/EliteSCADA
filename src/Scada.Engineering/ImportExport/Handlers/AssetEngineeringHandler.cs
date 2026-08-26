using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;

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

            EngineeringHandlerSupport.AddPreview(
                items, ImportEntityKind.Dynamo, dto.Key, ResolveExistingDynamo(dto) is not null, mode, issues);
        }
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
