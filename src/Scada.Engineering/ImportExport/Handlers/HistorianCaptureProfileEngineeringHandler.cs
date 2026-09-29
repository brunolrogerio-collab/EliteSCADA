using Scada.Engineering.Contracts;
using Scada.Engineering.Historian;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class HistorianCaptureProfileEngineeringHandler
{
    private readonly IHistorianCaptureProfileEngineeringRegistry _profiles;

    public HistorianCaptureProfileEngineeringHandler(IHistorianCaptureProfileEngineeringRegistry profiles)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var profiles = package.HistorianCaptureProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>();
        var validProfiles = profiles.Where(x => x is not null).ToArray();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(validProfiles.Select(x => x.Key));
        var duplicateIds = validProfiles
            .Where(x => x.Id.HasValue)
            .GroupBy(x => x.Id!.Value)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet();

        foreach (var profile in profiles)
        {
            if (profile is null)
            {
                EngineeringHandlerSupport.AddPreview(
                    items,
                    ImportEntityKind.HistorianCaptureProfile,
                    "<null>",
                    false,
                    mode,
                    [new(
                        "HISTORIAN_CAPTURE_PROFILE_NULL",
                        "Historian capture profile cannot be null.",
                        ImportEntityKind.HistorianCaptureProfile,
                        "<null>",
                        true)]);
                continue;
            }

            var entityKey = string.IsNullOrWhiteSpace(profile.Key)
                ? "<invalid-historian-capture-profile>"
                : profile.Key;

            var issues = HistorianCaptureProfileEngineeringValidation.Validate(profile)
                .Select(problem => new ImportIssue(
                    problem.Code,
                    problem.Message,
                    ImportEntityKind.HistorianCaptureProfile,
                    entityKey,
                    true))
                .ToList();

            if (!string.IsNullOrWhiteSpace(profile.Key) && duplicateKeys.Contains(profile.Key))
            {
                issues.Add(new(
                    "HISTORIAN_CAPTURE_PROFILE_DUPLICATE_KEY",
                    $"Historian capture profile key '{profile.Key}' appears more than once in the import package.",
                    ImportEntityKind.HistorianCaptureProfile,
                    entityKey,
                    true));
            }

            if (profile.Id.HasValue && duplicateIds.Contains(profile.Id.Value))
            {
                issues.Add(new(
                    "HISTORIAN_CAPTURE_PROFILE_DUPLICATE_ID",
                    $"Historian capture profile identity '{profile.Id.Value:D}' appears more than once in the import package.",
                    ImportEntityKind.HistorianCaptureProfile,
                    entityKey,
                    true));
            }

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.HistorianCaptureProfile,
                entityKey,
                ResolveExisting(profile) is not null,
                mode,
                issues);
        }

        ValidateDeleteDependencies(package, mode, items);
    }

    public void Apply(
        EngineeringPackage package,
        ImportMode mode,
        ref int created,
        ref int updated,
        ref int skipped)
    {
        foreach (var profile in package.HistorianCaptureProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>())
        {
            if (profile is null) continue;
            var existing = ResolveExisting(profile);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            _profiles.Upsert(profile with { Id = existing?.Id ?? profile.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }
    }

    private HistorianCaptureProfileEngineeringDto? ResolveExisting(HistorianCaptureProfileEngineeringDto profile)
    {
        if (profile.Id.HasValue)
        {
            var byId = _profiles.Find(profile.Id.Value);
            if (byId is not null) return byId;
        }

        return string.IsNullOrWhiteSpace(profile.Key) ? null : _profiles.FindByKey(profile.Key);
    }

    private void ValidateDeleteDependencies(
        EngineeringPackage package,
        ImportMode mode,
        List<ImportPreviewItem> items)
    {
        // The current import modes are additive/update-oriented and do not delete profiles.
        // Keep this guard explicit so any future replacement/delete mode must prove dependency safety.
        _ = package;
        _ = mode;
        _ = items;
    }
}
