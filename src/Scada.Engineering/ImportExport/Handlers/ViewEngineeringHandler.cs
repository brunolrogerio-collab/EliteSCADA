using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;
using Scada.Engineering.Views;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class ViewEngineeringHandler
{
    private readonly IEngineeringViewRegistry _views;
    private readonly IEngineeringAssetRegistry _assets;
    private readonly ITagRegistry _tags;

    public ViewEngineeringHandler(
        IEngineeringViewRegistry views,
        IEngineeringAssetRegistry assets,
        ITagRegistry tags)
    {
        _views = views;
        _assets = assets;
        _tags = tags;
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
            _views.UpsertScreen(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }

        foreach (var dto in package.Popups ?? Array.Empty<PopupEngineeringDto>())
        {
            var existing = ResolveExistingPopup(dto);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip) { skipped++; continue; }
            _views.UpsertPopup(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }
    }

    private void PreviewScreens(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var screens = package.Screens ?? Array.Empty<ScreenEngineeringDto>();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(screens.Select(x => x.Key));
        var duplicateRoutes = EngineeringHandlerSupport.Duplicates(screens.Select(x => x.Route ?? string.Empty));

        foreach (var dto in screens)
        {
            var issues = EngineeringValidator.ValidateScreen(dto).ToList();
            if (duplicateKeys.Contains(dto.Key))
                issues.Add(new(
                    "SCREEN_DUPLICATE_IN_FILE",
                    $"Screen key '{dto.Key}' appears more than once in the import package.",
                    ImportEntityKind.Screen,
                    dto.Key,
                    true));

            if (!string.IsNullOrWhiteSpace(dto.Route) && duplicateRoutes.Contains(dto.Route))
                issues.Add(new(
                    "SCREEN_ROUTE_DUPLICATE",
                    $"Screen route '{dto.Route}' appears more than once in the import package.",
                    ImportEntityKind.Screen,
                    dto.Key,
                    true));

            ValidateVisualReferences(dto.Elements, ImportEntityKind.Screen, dto.Key, package, issues);
            EngineeringHandlerSupport.AddPreview(
                items, ImportEntityKind.Screen, dto.Key, ResolveExistingScreen(dto) is not null, mode, issues);
        }
    }

    private void PreviewPopups(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var popups = package.Popups ?? Array.Empty<PopupEngineeringDto>();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(popups.Select(x => x.Key));

        foreach (var dto in popups)
        {
            var issues = EngineeringValidator.ValidatePopup(dto).ToList();
            if (duplicateKeys.Contains(dto.Key))
                issues.Add(new(
                    "POPUP_DUPLICATE_IN_FILE",
                    $"Popup key '{dto.Key}' appears more than once in the import package.",
                    ImportEntityKind.Popup,
                    dto.Key,
                    true));

            if (!string.IsNullOrWhiteSpace(dto.TemplateKey) && !TemplateExists(dto.TemplateKey, package))
                issues.Add(new(
                    "POPUP_TEMPLATE_NOT_FOUND",
                    $"Template '{dto.TemplateKey}' referenced by popup '{dto.Key}' was not found.",
                    ImportEntityKind.Popup,
                    dto.Key,
                    true));

            ValidateVisualReferences(dto.Elements, ImportEntityKind.Popup, dto.Key, package, issues);
            EngineeringHandlerSupport.AddPreview(
                items, ImportEntityKind.Popup, dto.Key, ResolveExistingPopup(dto) is not null, mode, issues);
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
        return _views.FindScreenByKey(dto.Key);
    }

    private PopupEngineeringDto? ResolveExistingPopup(PopupEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _views.FindPopup(dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _views.FindPopupByKey(dto.Key);
    }
}
