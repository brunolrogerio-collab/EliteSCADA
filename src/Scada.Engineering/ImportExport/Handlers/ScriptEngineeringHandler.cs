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
        var rawIncoming = package.Scripts ?? Array.Empty<ScriptEngineeringDefinition>();
        if (rawIncoming.Any(script => script is null))
        {
            items.Add(new ImportPreviewItem(
                ImportEntityKind.Script,
                "<null-script>",
                ImportOperation.Error,
                [new ImportIssue(
                    "SCRIPT_NULL",
                    "Script definition cannot be null.",
                    ImportEntityKind.Script,
                    "<null-script>",
                    true)]));
        }

        var rawReferences = package.ScriptVisualEventReferences ?? Array.Empty<ScriptVisualEventReference>();
        if (rawReferences.Any(reference => reference is null))
        {
            items.Add(new ImportPreviewItem(
                ImportEntityKind.Script,
                "script-visual-references",
                ImportOperation.Error,
                [new ImportIssue(
                    "SCRIPT_VISUAL_REFERENCE_NULL",
                    "Visual Script reference cannot be null.",
                    ImportEntityKind.Script,
                    "script-visual-references",
                    true)]));
        }

        var incoming = rawIncoming
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
        var incomingReferences = rawReferences
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

        var orphanReferences = rawReferences
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
            .Where(issue => issue.ScriptId is { } issueScriptId
                ? !incomingIds.Contains(issueScriptId)
                : issue.EntityKey is null || !assignedIssueKeys.Contains(issue.EntityKey))
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

        var visualDefinitions = ProspectiveVisualDefinitions(package)
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

    private IEnumerable<ScriptEngineeringVisualDefinitionIdentity> ProspectiveVisualDefinitions(EngineeringPackage package)
    {
        var replacedScreenIds = new HashSet<Guid>();
        foreach (var incoming in package.Screens ?? Array.Empty<ScreenEngineeringDto>())
        {
            if (incoming is null)
                continue;

            var byId = FindScreenBySuppliedId(incoming.Id);
            var byKey = string.IsNullOrWhiteSpace(incoming.Key) ? null : _views.FindScreenByKey(incoming.Key);
            AddId(replacedScreenIds, byId?.Id);
            AddId(replacedScreenIds, byKey?.Id);

            var effectiveId = StableId(incoming.Id) ?? byId?.Id ?? byKey?.Id;
            if (effectiveId is { } id)
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "screen", incoming.Key);
        }

        foreach (var current in _views.SnapshotScreens())
        {
            if (current.Id is { } id && id != Guid.Empty && !replacedScreenIds.Contains(id))
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "screen", current.Key);
        }

        var replacedPopupIds = new HashSet<Guid>();
        foreach (var incoming in package.Popups ?? Array.Empty<PopupEngineeringDto>())
        {
            if (incoming is null)
                continue;

            var byId = FindPopupBySuppliedId(incoming.Id);
            var byKey = string.IsNullOrWhiteSpace(incoming.Key) ? null : _views.FindPopupByKey(incoming.Key);
            AddId(replacedPopupIds, byId?.Id);
            AddId(replacedPopupIds, byKey?.Id);

            var effectiveId = StableId(incoming.Id) ?? byId?.Id ?? byKey?.Id;
            if (effectiveId is { } id)
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "popup", incoming.Key);
        }

        foreach (var current in _views.SnapshotPopups())
        {
            if (current.Id is { } id && id != Guid.Empty && !replacedPopupIds.Contains(id))
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "popup", current.Key);
        }

        var replacedDynamoIds = new HashSet<Guid>();
        foreach (var incoming in package.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
        {
            if (incoming is null)
                continue;

            var byId = FindDynamoBySuppliedId(incoming.Id);
            var byKey = string.IsNullOrWhiteSpace(incoming.Key) ? null : _assets.FindDynamoByKey(incoming.Key);
            AddId(replacedDynamoIds, byId?.Id);
            AddId(replacedDynamoIds, byKey?.Id);

            // Dynamo registry generates a new ID when a legacy incoming Dynamo omits it,
            // so only an explicitly supplied stable ID is knowable during Preview.
            if (StableId(incoming.Id) is { } id)
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "dynamo", incoming.Key);
        }

        foreach (var current in _assets.SnapshotDynamos())
        {
            if (current.Id is { } id && id != Guid.Empty && !replacedDynamoIds.Contains(id))
                yield return new ScriptEngineeringVisualDefinitionIdentity(id, "dynamo", current.Key);
        }
    }

    private IEnumerable<ScriptEngineeringReference> ProspectiveVisualObjectReferences(EngineeringPackage package)
    {
        var replacedScreenIds = new HashSet<Guid>();
        foreach (var incoming in package.Screens ?? Array.Empty<ScreenEngineeringDto>())
        {
            if (incoming is null)
                continue;

            var byId = FindScreenBySuppliedId(incoming.Id);
            var byKey = string.IsNullOrWhiteSpace(incoming.Key) ? null : _views.FindScreenByKey(incoming.Key);
            AddId(replacedScreenIds, byId?.Id);
            AddId(replacedScreenIds, byKey?.Id);

            var existing = byId ?? byKey;
            var definitionId = StableId(incoming.Id) ?? existing?.Id;
            if (definitionId is not { } id)
                continue;

            foreach (var reference in VisualObjectReferences(id, incoming.Elements, existing?.Elements))
                yield return reference;
        }

        foreach (var current in _views.SnapshotScreens())
        {
            if (current.Id is not { } id || id == Guid.Empty || replacedScreenIds.Contains(id))
                continue;

            foreach (var reference in VisualObjectReferences(id, current.Elements))
                yield return reference;
        }

        var replacedPopupIds = new HashSet<Guid>();
        foreach (var incoming in package.Popups ?? Array.Empty<PopupEngineeringDto>())
        {
            if (incoming is null)
                continue;

            var byId = FindPopupBySuppliedId(incoming.Id);
            var byKey = string.IsNullOrWhiteSpace(incoming.Key) ? null : _views.FindPopupByKey(incoming.Key);
            AddId(replacedPopupIds, byId?.Id);
            AddId(replacedPopupIds, byKey?.Id);

            var existing = byId ?? byKey;
            var definitionId = StableId(incoming.Id) ?? existing?.Id;
            if (definitionId is not { } id)
                continue;

            foreach (var reference in VisualObjectReferences(id, incoming.Elements, existing?.Elements))
                yield return reference;
        }

        foreach (var current in _views.SnapshotPopups())
        {
            if (current.Id is not { } id || id == Guid.Empty || replacedPopupIds.Contains(id))
                continue;

            foreach (var reference in VisualObjectReferences(id, current.Elements))
                yield return reference;
        }
    }

    private ScreenEngineeringDto? FindScreenBySuppliedId(Guid? id) =>
        StableId(id) is { } stableId ? _views.FindScreen(stableId) : null;

    private PopupEngineeringDto? FindPopupBySuppliedId(Guid? id) =>
        StableId(id) is { } stableId ? _views.FindPopup(stableId) : null;

    private DynamoEngineeringDto? FindDynamoBySuppliedId(Guid? id) =>
        StableId(id) is { } stableId ? _assets.FindDynamo(stableId) : null;

    private static Guid? StableId(Guid? id) =>
        id is { } value && value != Guid.Empty ? value : null;

    private static void AddId(HashSet<Guid> ids, Guid? id)
    {
        if (id is { } value && value != Guid.Empty)
            ids.Add(value);
    }

    private static IEnumerable<ScriptEngineeringReference> VisualObjectReferences(
        Guid definitionId,
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        IReadOnlyCollection<VisualElementEngineeringDto>? existing = null)
    {
        if (elements is null)
            yield break;

        var existingByKey = BuildUniqueElementKeyIndex(existing);
        foreach (var element in elements)
        {
            if (element is null)
                continue;

            existingByKey.TryGetValue(element.Key ?? string.Empty, out var previous);
            var objectId = element.Id == Guid.Empty
                ? null
                : element.Id ?? StableId(previous?.Id);

            if (objectId is { } stableObjectId)
            {
                yield return new ScriptEngineeringReference(
                    ScriptEngineeringDependencyKind.VisualObject,
                    ScriptEngineeringReferenceKeys.VisualObject(definitionId, stableObjectId));
            }

            foreach (var nested in VisualObjectReferences(
                definitionId,
                element.Children,
                previous?.Children))
            {
                yield return nested;
            }
        }
    }

    private static Dictionary<string, VisualElementEngineeringDto> BuildUniqueElementKeyIndex(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements)
    {
        if (elements is null || elements.Count == 0)
            return new Dictionary<string, VisualElementEngineeringDto>(StringComparer.OrdinalIgnoreCase);

        return elements
            .Where(element => element is not null && !string.IsNullOrWhiteSpace(element.Key))
            .GroupBy(element => element.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
    }

    private static ImportIssue ToImportIssue(ScriptEngineeringValidationIssue issue, string fallbackKey) =>
        new(
            issue.Code,
            issue.Message,
            ImportEntityKind.Script,
            string.IsNullOrWhiteSpace(issue.EntityKey) ? fallbackKey : issue.EntityKey,
            issue.IsError);
}
