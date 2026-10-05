using Scada.Engineering.Contracts;
using Scada.Engineering.Media;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class MediaSourceEngineeringHandler(IMediaSourceEngineeringRegistry registry)
{
    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var sources = package.MediaSources ?? Array.Empty<MediaSourceEngineeringDto>();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(
            sources.Where(source => source is not null).Select(source => source.Key));
        foreach (var source in sources)
        {
            if (source is null)
            {
                items.Add(new ImportPreviewItem(
                    ImportEntityKind.MediaSource,
                    "<null>",
                    ImportOperation.Error,
                    [new ImportIssue(
                        "MEDIA_SOURCE_REQUIRED",
                        "A media source definition is required.",
                        ImportEntityKind.MediaSource,
                        "<null>",
                        true)]));
                continue;
            }

            var issues = MediaSourceEngineeringValidation.Validate(source)
                .Select(issue => new ImportIssue(
                    issue.Code,
                    issue.Message,
                    ImportEntityKind.MediaSource,
                    source.Key,
                    true))
                .ToList();
            if (duplicateKeys.Contains(source.Key))
                issues.Add(new(
                    "MEDIA_SOURCE_DUPLICATE_IN_FILE",
                    $"Media source key '{source.Key}' appears more than once in the import package.",
                    ImportEntityKind.MediaSource,
                    source.Key,
                    true));

            var byId = source.Id.HasValue ? registry.Find(source.Id.Value) : null;
            var byKey = registry.FindByKey(source.Key);
            if (byId is not null && byKey is not null && byId.Id != byKey.Id)
                issues.Add(new(
                    "MEDIA_SOURCE_KEY_CONFLICT",
                    $"Media source key '{source.Key}' is already assigned to a different stable identity.",
                    ImportEntityKind.MediaSource,
                    source.Key,
                    true));

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.MediaSource,
                source.Key,
                ResolveExisting(source) is not null,
                mode,
                issues);
        }
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        foreach (var source in package.MediaSources ?? Array.Empty<MediaSourceEngineeringDto>())
        {
            var existing = ResolveExisting(source);
            if (EngineeringHandlerSupport.Decide(existing is not null, mode) == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            registry.Upsert(source with { Id = existing?.Id ?? source.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }
    }

    private MediaSourceEngineeringDto? ResolveExisting(MediaSourceEngineeringDto source)
    {
        if (source.Id.HasValue && registry.Find(source.Id.Value) is { } byId)
            return byId;
        return registry.FindByKey(source.Key);
    }
}
