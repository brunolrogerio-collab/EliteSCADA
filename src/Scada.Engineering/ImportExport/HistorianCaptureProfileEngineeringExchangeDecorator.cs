using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Engineering.Contracts;
using Scada.Engineering.Historian;
using Scada.Engineering.VisualAssets;
using Scada.Security.Authorization;

namespace Scada.Engineering.ImportExport;

/// <summary>
/// Adapts the canonical Historian Capture Profile Engineering registry into the
/// existing Engineering package lifecycle. Registry ownership and validation stay
/// in the Historian Engineering authority; this adapter only participates in
/// package export, Preview and Apply.
/// </summary>
public sealed class HistorianCaptureProfileEngineeringExchangeDecorator : IEngineeringExchangeService
{
    private readonly IEngineeringExchangeService _inner;
    private readonly IHistorianCaptureProfileEngineeringRegistry _profiles;

    public HistorianCaptureProfileEngineeringExchangeDecorator(
        IEngineeringExchangeService inner,
        IHistorianCaptureProfileEngineeringRegistry profiles)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public EngineeringPackage ExportPackage() =>
        _inner.ExportPackage() with { HistorianCaptureProfiles = _profiles.Snapshot() };

    public string ExportJson(bool indented = true) =>
        JsonSerializer.Serialize(ExportPackage(), JsonOptions(indented));

    public string ExportTagsCsv() => _inner.ExportTagsCsv();
    public string ExportAlarmsCsv() => _inner.ExportAlarmsCsv();
    public string ExportDataSourcesCsv() => _inner.ExportDataSourcesCsv();
    public EngineeringPackage ParseJson(string json) => _inner.ParseJson(json);
    public EngineeringPackage ParseTagsCsv(string csv) => _inner.ParseTagsCsv(csv);
    public EngineeringPackage ParseAlarmsCsv(string csv) => _inner.ParseAlarmsCsv(csv);
    public EngineeringPackage ParseDataSourcesCsv(string csv) => _inner.ParseDataSourcesCsv(csv);

    public ImportPreview Preview(EngineeringPackage package, ImportMode mode) =>
        Preview(package, mode, null);

    public ImportPreview Preview(
        EngineeringPackage package,
        ImportMode mode,
        EngineeringImportContext? context)
    {
        var basePreview = _inner.Preview(package, mode, context);
        var items = basePreview.Items.Concat(PreviewOwned(package, mode)).ToArray();
        return BuildPreview(mode, items);
    }

    public ImportResult Apply(EngineeringPackage package, ImportMode mode) =>
        Apply(package, mode, null);

    public ImportResult Apply(
        EngineeringPackage package,
        ImportMode mode,
        EngineeringImportContext? context)
    {
        var preview = Preview(package, mode, context);
        if (!preview.CanApply)
        {
            return new ImportResult(
                mode,
                0,
                0,
                preview.SkipCount,
                preview.Items.SelectMany(item => item.Issues).ToArray());
        }

        var core = _inner.Apply(package, mode, context);
        if (core.Issues.Any(issue => issue.IsError))
            return core;

        var created = core.Created;
        var updated = core.Updated;
        var skipped = core.Skipped;

        foreach (var profile in package.HistorianCaptureProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>())
        {
            var existing = ResolveExisting(profile);
            if (existing is null)
            {
                if (mode == ImportMode.UpdateExisting) { skipped++; continue; }
                _profiles.Upsert(profile);
                created++;
            }
            else
            {
                if (mode == ImportMode.CreateOnly) { skipped++; continue; }
                _profiles.Upsert(profile with { Id = profile.Id ?? existing.Id });
                updated++;
            }
        }

        return new ImportResult(mode, created, updated, skipped, core.Issues);
    }

    private IReadOnlyCollection<ImportPreviewItem> PreviewOwned(
        EngineeringPackage package,
        ImportMode mode)
    {
        var items = new List<ImportPreviewItem>();
        var packageProfiles = package.HistorianCaptureProfiles
            ?? Array.Empty<HistorianCaptureProfileEngineeringDto>();
        var packageById = new Dictionary<Guid, HistorianCaptureProfileEngineeringDto>();
        var seenIds = new HashSet<Guid>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in packageProfiles)
        {
            if (profile is null)
            {
                var issue = Issue(
                    "HISTORIAN_CAPTURE_PROFILE_NULL",
                    "Historian capture profile package entry cannot be null.",
                    ImportEntityKind.HistorianCaptureProfile,
                    "<null>");
                items.Add(new ImportPreviewItem(
                    ImportEntityKind.HistorianCaptureProfile,
                    "<null>",
                    ImportOperation.Error,
                    [issue]));
                continue;
            }

            var key = profile.Key ?? string.Empty;
            var issues = HistorianCaptureProfileEngineeringValidation.Validate(profile)
                .Select(problem => Issue(
                    problem.Code,
                    problem.Message,
                    ImportEntityKind.HistorianCaptureProfile,
                    key))
                .ToList();

            if (profile.Id.HasValue)
            {
                if (!seenIds.Add(profile.Id.Value))
                {
                    issues.Add(Issue(
                        "HISTORIAN_CAPTURE_PROFILE_ID_DUPLICATE",
                        $"Historian capture profile Id '{profile.Id.Value:D}' is duplicated in the package.",
                        ImportEntityKind.HistorianCaptureProfile,
                        key));
                }
                else if (profile.Id.Value != Guid.Empty)
                {
                    packageById[profile.Id.Value] = profile;
                }
            }

            if (!string.IsNullOrWhiteSpace(key) && !seenKeys.Add(key.Trim()))
            {
                issues.Add(Issue(
                    "HISTORIAN_CAPTURE_PROFILE_KEY_DUPLICATE",
                    $"Historian capture profile key '{key}' is duplicated in the package.",
                    ImportEntityKind.HistorianCaptureProfile,
                    key));
            }

            var existing = ResolveExisting(profile);
            var byKey = _profiles.FindByKey(key);
            if (profile.Id.HasValue && byKey is not null && byKey.Id != profile.Id)
            {
                issues.Add(Issue(
                    "HISTORIAN_CAPTURE_PROFILE_IDENTITY_CONFLICT",
                    $"Historian capture profile key '{key}' belongs to a different stable Id.",
                    ImportEntityKind.HistorianCaptureProfile,
                    key));
            }

            items.Add(new ImportPreviewItem(
                ImportEntityKind.HistorianCaptureProfile,
                key,
                issues.Count > 0 ? ImportOperation.Error : Operation(mode, existing is not null),
                issues));
        }

        foreach (var tag in package.Tags)
        {
            if (!tag.HistorianCaptureProfileId.HasValue)
                continue;

            var profileId = tag.HistorianCaptureProfileId.Value;
            var profile = packageById.GetValueOrDefault(profileId) ?? _profiles.Find(profileId);
            var issues = new List<ImportIssue>();
            if (profile is null)
            {
                issues.Add(Issue(
                    "HISTORIAN_CAPTURE_PROFILE_NOT_FOUND",
                    $"TAG '{tag.Path}' references Historian capture profile '{profileId:D}', which is not available in the Engineering target/package.",
                    ImportEntityKind.Tag,
                    tag.Path));
            }
            else
            {
                issues.AddRange(
                    HistorianCaptureProfileEngineeringValidation.ValidateForTag(profile, tag.DataType)
                        .Select(problem => Issue(
                            problem.Code,
                            $"TAG '{tag.Path}': {problem.Message}",
                            ImportEntityKind.Tag,
                            tag.Path)));
            }

            if (issues.Count > 0)
            {
                items.Add(new ImportPreviewItem(
                    ImportEntityKind.Tag,
                    tag.Path,
                    ImportOperation.Error,
                    issues));
            }
        }

        return items;
    }

    private HistorianCaptureProfileEngineeringDto? ResolveExisting(
        HistorianCaptureProfileEngineeringDto profile) =>
        profile.Id.HasValue
            ? _profiles.Find(profile.Id.Value) ?? _profiles.FindByKey(profile.Key)
            : _profiles.FindByKey(profile.Key);

    private static ImportOperation Operation(ImportMode mode, bool exists) => (mode, exists) switch
    {
        (ImportMode.CreateOnly, false) => ImportOperation.Create,
        (ImportMode.CreateOnly, true) => ImportOperation.Skip,
        (ImportMode.UpdateExisting, false) => ImportOperation.Skip,
        (ImportMode.UpdateExisting, true) => ImportOperation.Update,
        (ImportMode.CreateAndUpdate, false) => ImportOperation.Create,
        (ImportMode.CreateAndUpdate, true) => ImportOperation.Update,
        _ => ImportOperation.Error
    };

    private static ImportPreview BuildPreview(
        ImportMode mode,
        IReadOnlyCollection<ImportPreviewItem> items) =>
        new(
            mode,
            items.Count(item => item.Operation == ImportOperation.Create),
            items.Count(item => item.Operation == ImportOperation.Update),
            items.Count(item => item.Operation == ImportOperation.Skip),
            items.Count(item => item.Operation == ImportOperation.Error),
            items);

    private static ImportIssue Issue(
        string code,
        string message,
        ImportEntityKind kind,
        string key) =>
        new(code, message, kind, key, true);

    private static JsonSerializerOptions JsonOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = indented
        };
        options.Converters.Add(new SecurityCapabilityJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        return options;
    }
}
