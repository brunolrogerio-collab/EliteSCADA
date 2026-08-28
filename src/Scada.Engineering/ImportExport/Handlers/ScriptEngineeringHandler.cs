using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Scripts;
using Scada.Engineering.Validation;
using Scada.Engineering.Views;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class ScriptEngineeringHandler
{
    private readonly IScriptEngineeringRegistry _registry;
    private readonly ITagRegistry _tags;
    private readonly IDataSourceEngineeringRegistry _dataSources;
    private readonly IEngineeringAssetRegistry _assets;
    private readonly IEngineeringViewRegistry _views;
    private readonly ScriptEngineeringValidator _validator = new();

    public ScriptEngineeringHandler(
        IScriptEngineeringRegistry registry,
        ITagRegistry tags,
        IDataSourceEngineeringRegistry dataSources,
        IEngineeringAssetRegistry assets,
        IEngineeringViewRegistry views)
    {
        _registry = registry;
        _tags = tags;
        _dataSources = dataSources;
        _assets = assets;
        _views = views;
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var incoming = (package.Scripts ?? Array.Empty<ScriptEngineeringDefinition>())
            .Where(script => script is not null)
            .ToArray();
        var incomingIds = incoming.Select(script => script.Id).ToHashSet();
        var selected = incoming
            .Where(script => EngineeringHandlerSupport.Decide(_registry.Find(script.Id) is not null, mode) != ImportOperation.Skip)
            .ToArray();
        var selectedIds = selected.Select(script => script.Id).ToHashSet();

        var prospectiveScripts = _registry.SnapshotScripts()
            .Where(script => !selectedIds.Contains(script.Id))
            .Concat(selected)
            .ToArray();

        var currentReferences = _registry.SnapshotVisualEventReferences()
            .Where(reference => !selectedIds.Contains(reference.ScriptId));
        var incomingReferences = (package.ScriptVisualEventReferences ?? Array.Empty<ScriptVisualEventReference>())
            .Where(reference => reference is not null && selectedIds.Contains(reference.ScriptId));
        var prospectiveReferences = currentReferences.Concat(incomingReferences).ToArray();

        var referenceResolver = BuildReferenceResolver(package);
        var validation = _validator.Validate(
            new ScriptEngineeringModel(prospectiveScripts, prospectiveReferences),
            referenceResolver.ToValidationCatalog());

        foreach (var script in incoming)
        {
            var issues = validation.Issues
                .Where(issue => issue.ScriptId == script.Id ||
                    string.Equals(issue.EntityKey, script.Path, StringComparison.Ordinal))
                .Select(issue => ToImportIssue(issue, script.Path))
                .ToList();

            if (script.Id != Guid.Empty &&
                _registry.FindByPath(script.Path) is { } pathOwner &&
                pathOwner.Id != script.Id)
            {
                issues.Add(new ImportIssue(
                    "SCRIPT_PATH_OWNED_BY_DIFFERENT_ID",
                    $"Script path '{script.Path}' is already owned by stable Script ID '{pathOwner.Id:D}'.",
                    ImportEntityKind.Script,
                    script.Path,
                    true));
            }

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.Script,
                string.IsNullOrWhiteSpace(script.Path) ? script.Id.ToString("D") : script.Path,
                script.Id != Guid.Empty && _registry.Find(script.Id) is not null,
                mode,
                issues);
        }

        var orphanReferences = (package.ScriptVisualEventReferences ?? Array.Empty<ScriptVisualEventReference>())
            .Where(reference => reference is not null && !incomingIds.Contains(reference.ScriptId))
            .ToArray();
        if (orphanReferences.Length > 0)
        {
            items.Add(new ImportPreviewItem(
                ImportEntityKind.Script,
                "script-visual-references",
                ImportOperation.Error,
                [new ImportIssue(
                    "SCRIPT_VISUAL_REFERENCE_WITHOUT_SCRIPT",
                    "Visual Script references in an import package must belong to a Script included in that same package.",
                    ImportEntityKind.Script,
                    "script-visual-references",
                    true)]));
        }

        var assignedIssueKeys = incoming
            .SelectMany(script => new[] { script.Id.ToString("D"), script.Path })
            .ToHashSet(StringComparer.Ordinal);
        var unassigned = validation.Issues
            .Where(issue => issue.ScriptId is null &&
                (issue.EntityKey is null || !assignedIssueKeys.Contains(issue.EntityKey)))
            .ToArray();
        if (unassigned.Length > 0)
        {
            items.Add(new ImportPreviewItem(
                ImportEntityKind.Script,
                "script-model",
                ImportOperation.Error,
                unassigned.Select(issue => ToImportIssue(issue, issue.EntityKey ?? "script-model")).ToArray()));
        }

        if (prospectiveScripts.Length > 0 && referenceResolver.CatalogIssues.Count > 0)
        {
            items.Add(new ImportPreviewItem(
                ImportEntityKind.Script,
                "script-reference-catalog",
                ImportOperation.Error,
                referenceResolver.CatalogIssues
                    .Select(issue => new ImportIssue(
                        issue.Code,
                        issue.Message,
                        ImportEntityKind.Script,
                        issue.EntityKey,
                        true))
                    .ToArray()));
        }
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        var references = package.ScriptVisualEventReferences ?? Array.Empty<ScriptVisualEventReference>();

        foreach (var script in package.Scripts ?? Array.Empty<ScriptEngineeringDefinition>())
        {
            var existing = script.Id == Guid.Empty ? null : _registry.Find(script.Id);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            _registry.Upsert(script);
            _registry.ReplaceVisualEventReferences(
                script.Id,
                references.Where(reference => reference.ScriptId == script.Id).ToArray());

            if (existing is null) created++; else updated++;
        }
    }

    private ScriptEngineeringReferenceResolver BuildReferenceResolver(EngineeringPackage package)
    {
        var tags = CurrentTags()
            .Concat(package.Tags)
            .GroupBy(tag => tag.Id)
            .Select(group => group.Last())
            .ToArray();

        var dataSources = _dataSources.Snapshot()
            .Concat(package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
            .Where(source => source is not null && !string.IsNullOrWhiteSpace(source.Key))
            .GroupBy(source => source.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();

        var visualDefinitions = CurrentVisualDefinitions()
            .Concat(PackageVisualDefinitions(package))
            .GroupBy(definition => definition.Id)
            .Select(group => group.Last())
            .ToArray();

        var visualObjectReferences = ProspectiveVisualObjectReferences(package)
            .GroupBy(reference => (reference.Kind, reference.StableReference))
            .Select(group => group.Last())
            .ToArray();

        return ScriptEngineeringReferenceResolver.Create(
            tags,
            dataSources,
            visualDefinitions,
            visualObjectReferences);
    }

    private IEnumerable<TagEngineeringDto> CurrentTags() =>
        _tags.Snapshot().Select(tag => new TagEngineeringDto(
            tag.Id,
            tag.Name,
            tag.Path,
            tag.DataType,
            tag.Source,
            EngineeringUnit: tag.EngineeringUnit,
            Description: tag.Description,
            ReadOnly: tag.ReadOnly));

    private IEnumerable<ScriptEngineeringVisualDefinitionIdentity> CurrentVisualDefinitions()
    {
        foreach (var screen in _views.SnapshotScreens())
            if (screen.Id is { } id && id != Guid.Empty)
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "screen", screen.Key);
        foreach (var popup in _views.SnapshotPopups())
            if (popup.Id is { } id && id != Guid.Empty)
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "popup", popup.Key);
        foreach (var dynamo in _assets.SnapshotDynamos())
            if (dynamo.Id is { } id && id != Guid.Empty)
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "dynamo", dynamo.Key);
    }

    private static IEnumerable<ScriptEngineeringVisualDefinitionIdentity> PackageVisualDefinitions(EngineeringPackage package)
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

    private IEnumerable<ScriptEngineeringReference> ProspectiveVisualObjectReferences(EngineeringPackage package)
    {
        var incomingDefinitionIds = PackageViewDefinitionIds(package).ToHashSet();

        foreach (var screen in _views.SnapshotScreens())
        {
            if (screen.Id is not { } definitionId || definitionId == Guid.Empty || incomingDefinitionIds.Contains(definitionId))
                continue;

            foreach (var reference in VisualObjectReferences(definitionId, screen.Elements))
                yield return reference;
        }

        foreach (var popup in _views.SnapshotPopups())
        {
            if (popup.Id is not { } definitionId || definitionId == Guid.Empty || incomingDefinitionIds.Contains(definitionId))
                continue;

            foreach (var reference in VisualObjectReferences(definitionId, popup.Elements))
                yield return reference;
        }

        foreach (var screen in package.Screens ?? Array.Empty<ScreenEngineeringDto>())
        {
            if (screen is null || screen.Id is not { } definitionId || definitionId == Guid.Empty)
                continue;

            foreach (var reference in VisualObjectReferences(definitionId, screen.Elements))
                yield return reference;
        }

        foreach (var popup in package.Popups ?? Array.Empty<PopupEngineeringDto>())
        {
            if (popup is null || popup.Id is not { } definitionId || definitionId == Guid.Empty)
                continue;

            foreach (var reference in VisualObjectReferences(definitionId, popup.Elements))
                yield return reference;
        }
    }

    private static IEnumerable<Guid> PackageViewDefinitionIds(EngineeringPackage package)
    {
        foreach (var screen in package.Screens ?? Array.Empty<ScreenEngineeringDto>())
            if (screen is not null && screen.Id is { } id && id != Guid.Empty)
                yield return id;

        foreach (var popup in package.Popups ?? Array.Empty<PopupEngineeringDto>())
            if (popup is not null && popup.Id is { } id && id != Guid.Empty)
                yield return id;
    }

    private static IEnumerable<ScriptEngineeringReference> VisualObjectReferences(
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

            foreach (var nested in VisualObjectReferences(definitionId, element.Children))
                yield return nested;
        }
    }

    private static ImportIssue ToImportIssue(ScriptEngineeringValidationIssue issue, string fallbackKey) =>
        new(
            issue.Code,
            issue.Message,
            ImportEntityKind.Script,
            string.IsNullOrWhiteSpace(issue.EntityKey) ? fallbackKey : issue.EntityKey,
            issue.IsError);
}
