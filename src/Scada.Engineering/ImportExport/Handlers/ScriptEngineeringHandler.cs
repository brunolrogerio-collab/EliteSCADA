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
        var incoming = (package.Scripts ?? Array.Empty<ScriptEngineeringDefinition>()).ToArray();
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
            .Where(reference => selectedIds.Contains(reference.ScriptId));
        var prospectiveReferences = currentReferences.Concat(incomingReferences).ToArray();

        var validation = _validator.Validate(
            new ScriptEngineeringModel(prospectiveScripts, prospectiveReferences),
            BuildReferenceCatalog(package));

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
            .Where(reference => !incomingIds.Contains(reference.ScriptId))
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

    private ScriptEngineeringReferenceCatalog BuildReferenceCatalog(EngineeringPackage package)
    {
        var references = new List<ScriptEngineeringReference>();
        var incomingDataSources = (package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
            .Where(source => !string.IsNullOrWhiteSpace(source.Key))
            .ToDictionary(source => source.Key, source => source, StringComparer.OrdinalIgnoreCase);

        foreach (var tag in CurrentTags().Concat(package.Tags).GroupBy(tag => tag.Id).Select(group => group.Last()))
        {
            if (!tag.Id.HasValue || tag.Id.Value == Guid.Empty) continue;

            references.Add(new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.Tag,
                ScriptEngineeringReferenceKeys.Tag(tag.Id.Value)));

            var source = ResolveDataSource(tag.Source, incomingDataSources);
            if (source is null) continue;

            if (MemoryEngineeringValidator.IsClientMemoryDriver(source.Driver))
            {
                references.Add(new ScriptEngineeringReference(
                    ScriptEngineeringDependencyKind.ClientMemoryTag,
                    ScriptEngineeringReferenceKeys.Tag(tag.Id.Value)));
            }
            else if (MemoryEngineeringValidator.IsServerMemoryDriver(source.Driver))
            {
                references.Add(new ScriptEngineeringReference(
                    ScriptEngineeringDependencyKind.ServerMemoryTag,
                    ScriptEngineeringReferenceKeys.Tag(tag.Id.Value)));
            }
        }

        foreach (var id in CurrentVisualDefinitionIds().Concat(PackageVisualDefinitionIds(package)).Distinct())
        {
            references.Add(new ScriptEngineeringReference(
                ScriptEngineeringDependencyKind.VisualDefinition,
                ScriptEngineeringReferenceKeys.VisualDefinition(id)));
        }

        return new ScriptEngineeringReferenceCatalog(references);
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

    private DataSourceEngineeringDto? ResolveDataSource(
        string? sourceKey,
        IReadOnlyDictionary<string, DataSourceEngineeringDto> incoming)
    {
        if (string.IsNullOrWhiteSpace(sourceKey)) return null;
        return incoming.GetValueOrDefault(sourceKey) ?? _dataSources.FindByKey(sourceKey);
    }

    private IEnumerable<Guid> CurrentVisualDefinitionIds()
    {
        foreach (var screen in _views.SnapshotScreens())
            if (screen.Id is { } id && id != Guid.Empty) yield return id;
        foreach (var popup in _views.SnapshotPopups())
            if (popup.Id is { } id && id != Guid.Empty) yield return id;
        foreach (var dynamo in _assets.SnapshotDynamos())
            if (dynamo.Id is { } id && id != Guid.Empty) yield return id;
    }

    private static IEnumerable<Guid> PackageVisualDefinitionIds(EngineeringPackage package)
    {
        foreach (var screen in package.Screens ?? Array.Empty<ScreenEngineeringDto>())
            if (screen.Id is { } id && id != Guid.Empty) yield return id;
        foreach (var popup in package.Popups ?? Array.Empty<PopupEngineeringDto>())
            if (popup.Id is { } id && id != Guid.Empty) yield return id;
        foreach (var dynamo in package.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
            if (dynamo.Id is { } id && id != Guid.Empty) yield return id;
    }

    private static ImportIssue ToImportIssue(ScriptEngineeringValidationIssue issue, string fallbackKey) =>
        new(
            issue.Code,
            issue.Message,
            ImportEntityKind.Script,
            string.IsNullOrWhiteSpace(issue.EntityKey) ? fallbackKey : issue.EntityKey,
            issue.IsError);
}
