using Scada.Engineering.Contracts;
using Scada.Engineering.Reports;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class ReportEngineeringHandler
{
    private readonly IReportEngineeringRegistry _reports;

    public ReportEngineeringHandler(IReportEngineeringRegistry reports)
    {
        _reports = reports ?? throw new ArgumentNullException(nameof(reports));
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var reports = package.Reports ?? Array.Empty<ReportEngineeringDto>();
        var validReports = reports.Where(x => x is not null).ToArray();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(validReports.Select(x => x.Key));

        foreach (var dto in reports)
        {
            if (dto is null)
            {
                EngineeringHandlerSupport.AddPreview(
                    items,
                    ImportEntityKind.Report,
                    "<null>",
                    false,
                    mode,
                    [new("REPORT_NULL", "Report cannot be null.", ImportEntityKind.Report, "<null>", true)]);
                continue;
            }

            var entityKey = string.IsNullOrWhiteSpace(dto.Key) ? "<invalid-report>" : dto.Key;
            var issues = ReportEngineeringValidation.Validate(dto)
                .Select(problem => new ImportIssue(
                    problem.Code,
                    problem.Message,
                    ImportEntityKind.Report,
                    entityKey,
                    true))
                .ToList();

            if (!string.IsNullOrWhiteSpace(dto.Key) && duplicateKeys.Contains(dto.Key))
                issues.Add(new(
                    "REPORT_DUPLICATE_IN_FILE",
                    $"Report key '{dto.Key}' appears more than once in the import package.",
                    ImportEntityKind.Report,
                    entityKey,
                    true));

            ValidateAssetReferences(dto, package, entityKey, issues);
            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.Report,
                entityKey,
                ResolveExisting(dto) is not null,
                mode,
                issues);
        }
    }

    public void Apply(
        EngineeringPackage package,
        ImportMode mode,
        ref int created,
        ref int updated,
        ref int skipped)
    {
        foreach (var dto in package.Reports ?? Array.Empty<ReportEngineeringDto>())
        {
            if (dto is null) continue;
            var existing = ResolveExisting(dto);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            _reports.Upsert(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }
    }

    private ReportEngineeringDto? ResolveExisting(ReportEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _reports.Find(dto.Id.Value);
            if (byId is not null) return byId;
        }

        return string.IsNullOrWhiteSpace(dto.Key) ? null : _reports.FindByKey(dto.Key);
    }

    private static void ValidateAssetReferences(
        ReportEngineeringDto report,
        EngineeringPackage package,
        string entityKey,
        List<ImportIssue> issues)
    {
        var prospectiveAssets = (package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
            .Where(x => x is not null && x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        foreach (var control in (report.Sections ?? Array.Empty<ReportSectionEngineeringDto>())
                     .Where(x => x is not null)
                     .SelectMany(x => x.Controls ?? Array.Empty<ReportControlEngineeringDto>())
                     .Where(x => x is not null && x.Kind == ReportControlKind.Image && x.AssetId.HasValue))
        {
            if (prospectiveAssets.Contains(control.AssetId!.Value)) continue;
            issues.Add(new(
                "REPORT_ASSET_NOT_FOUND",
                $"Report image control '{control.Key}' references visual asset '{control.AssetId.Value:D}', which was not found in the prospective import package.",
                ImportEntityKind.Report,
                entityKey,
                true));
        }
    }
}
