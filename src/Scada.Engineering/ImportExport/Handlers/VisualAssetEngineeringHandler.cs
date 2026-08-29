using Scada.Engineering.Contracts;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class VisualAssetEngineeringHandler
{
    private readonly IVisualAssetEngineeringRegistry _registry;

    public VisualAssetEngineeringHandler(IVisualAssetEngineeringRegistry registry)
    {
        _registry = registry;
    }

    public void Preview(
        EngineeringPackage package,
        ImportMode mode,
        List<ImportPreviewItem> items,
        EngineeringImportContext? context = null)
    {
        var assets = package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(
            assets.Where(x => x is not null).Select(x => x!.Key));
        var duplicateIds = assets
            .Where(x => x?.Id is not null)
            .GroupBy(x => x!.Id!.Value)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet();

        foreach (var asset in assets)
        {
            var key = asset?.Key ?? "<null>";
            var issues = VisualAssetEngineeringValidator.Validate(asset, _registry, context).ToList();

            if (asset is not null && duplicateKeys.Contains(asset.Key))
            {
                issues.Add(new ImportIssue(
                    "VISUAL_ASSET_DUPLICATE_KEY",
                    $"Visual asset key '{asset.Key}' appears more than once in the import package.",
                    ImportEntityKind.VisualAsset,
                    asset.Key,
                    true));
            }

            if (asset?.Id is not null && duplicateIds.Contains(asset.Id.Value))
            {
                issues.Add(new ImportIssue(
                    "VISUAL_ASSET_DUPLICATE_ID",
                    $"Visual asset ID '{asset.Id}' appears more than once in the import package.",
                    ImportEntityKind.VisualAsset,
                    key,
                    true));
            }

            if (asset?.Id is not null)
            {
                var existingByKey = _registry.FindAssetByKey(asset.Key);
                if (existingByKey?.Id is not null && existingByKey.Id.Value != asset.Id.Value)
                {
                    issues.Add(new ImportIssue(
                        "VISUAL_ASSET_ID_KEY_CONFLICT",
                        $"Visual asset key '{asset.Key}' already belongs to stable asset ID '{existingByKey.Id}'. Import cannot silently replace that identity with '{asset.Id}'.",
                        ImportEntityKind.VisualAsset,
                        key,
                        true));
                }
            }

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.VisualAsset,
                key,
                asset is not null && ResolveExisting(asset) is not null,
                mode,
                issues);
        }
    }

    public void Apply(
        EngineeringPackage package,
        ImportMode mode,
        ref int created,
        ref int updated,
        ref int skipped,
        EngineeringImportContext? context = null)
    {
        foreach (var asset in package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
        {
            if (asset is null)
                continue;

            var existing = ResolveExisting(asset);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            var normalizedHash = asset.Sha256.ToLowerInvariant();
            if (context is not null && context.TryGetVisualAssetPayload(normalizedHash, out var supplied))
                _registry.PutPayload(supplied with { Sha256 = normalizedHash });

            if (!_registry.HasPayload(normalizedHash))
                throw new InvalidDataException($"Visual asset payload '{normalizedHash}' is unavailable during Apply.");

            _registry.UpsertAsset(asset with
            {
                Id = existing?.Id ?? asset.Id ?? Guid.NewGuid(),
                Sha256 = normalizedHash
            });

            if (existing is null) created++; else updated++;
        }
    }

    private VisualAssetEngineeringDto? ResolveExisting(VisualAssetEngineeringDto asset)
    {
        if (asset.Id.HasValue)
        {
            var byId = _registry.FindAsset(asset.Id.Value);
            if (byId is not null) return byId;
        }

        return _registry.FindAssetByKey(asset.Key);
    }
}
