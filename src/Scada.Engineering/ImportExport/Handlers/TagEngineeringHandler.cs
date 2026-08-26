using System.Globalization;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Validation;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class TagEngineeringHandler
{
    private readonly ITagRegistry _tags;
    private readonly IDataSourceEngineeringRegistry _dataSources;

    public TagEngineeringHandler(ITagRegistry tags, IDataSourceEngineeringRegistry dataSources)
    {
        _tags = tags;
        _dataSources = dataSources;
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var dataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>();
        var duplicatePaths = EngineeringHandlerSupport.Duplicates(package.Tags.Select(x => x.Path));

        foreach (var dto in package.Tags)
        {
            var issues = EngineeringValidator.ValidateTag(dto).ToList();
            ValidateAccessPolicy(dto, issues);

            var dataSource = ResolveDataSource(dto.Source, package);
            issues.AddRange(MemoryEngineeringValidator.ValidateTag(dto, dataSource));

            if (duplicatePaths.Contains(dto.Path))
                issues.Add(new(
                    "TAG_DUPLICATE_IN_FILE",
                    $"Tag path '{dto.Path}' appears more than once in the import file.",
                    ImportEntityKind.Tag,
                    dto.Path,
                    true));

            if (package.SchemaVersion >= 2 &&
                !string.IsNullOrWhiteSpace(dto.Source) &&
                _dataSources.FindByKey(dto.Source) is null &&
                !dataSources.Any(x => x.Key.Equals(dto.Source, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new(
                    "TAG_DATASOURCE_NOT_FOUND",
                    $"Data source '{dto.Source}' referenced by tag '{dto.Path}' was not found.",
                    ImportEntityKind.Tag,
                    dto.Path,
                    true));
            }

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.Tag,
                dto.Path,
                ResolveExisting(dto) is not null,
                mode,
                issues);
        }
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        foreach (var dto in package.Tags)
        {
            var existing = ResolveExisting(dto);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            var tag = new TagDefinition(
                existing?.Id ?? dto.Id ?? Guid.NewGuid(),
                dto.Name,
                dto.Path,
                dto.DataType,
                dto.Source,
                dto.EngineeringUnit,
                dto.Description,
                dto.ReadOnly,
                BuildMetadata(dto),
                BuildAccessPolicy(dto.AccessPolicy));

            if (existing is null)
            {
                _tags.Register(tag);
                created++;
            }
            else
            {
                _tags.Upsert(tag);
                updated++;
            }
        }
    }

    public TagDefinition? ResolveAlarmTag(AlarmEngineeringDto dto)
    {
        if (dto.TagId.HasValue && _tags.TryGet(dto.TagId.Value, out var byId)) return byId;
        if (!string.IsNullOrWhiteSpace(dto.TagPath) && _tags.TryGetByPath(dto.TagPath, out var byPath)) return byPath;
        return null;
    }

    public TagDefinition? ResolveAlarmTagForPreview(AlarmEngineeringDto dto, EngineeringPackage package)
    {
        var existing = ResolveAlarmTag(dto);
        if (existing is not null) return existing;

        var imported = ResolveImportedAlarmTag(dto, package);
        if (imported is null) return null;

        return new TagDefinition(
            imported.Id ?? Guid.Empty,
            imported.Name,
            imported.Path,
            imported.DataType,
            imported.Source,
            imported.EngineeringUnit,
            imported.Description,
            imported.ReadOnly,
            BuildMetadata(imported),
            BuildAccessPolicy(imported.AccessPolicy));
    }

    public bool IsClientMemoryAlarmTarget(AlarmEngineeringDto dto, EngineeringPackage package)
    {
        var imported = ResolveImportedAlarmTag(dto, package);
        var source = imported?.Source ?? ResolveAlarmTag(dto)?.Source;
        return MemoryEngineeringValidator.IsClientMemoryDriver(ResolveDataSource(source, package)?.Driver);
    }

    private TagEngineeringDto? ResolveImportedAlarmTag(AlarmEngineeringDto dto, EngineeringPackage package)
    {
        TagEngineeringDto? imported = null;
        if (dto.TagId.HasValue) imported = package.Tags.FirstOrDefault(x => x.Id == dto.TagId);
        if (imported is null && !string.IsNullOrWhiteSpace(dto.TagPath))
            imported = package.Tags.FirstOrDefault(x => x.Path.Equals(dto.TagPath, StringComparison.OrdinalIgnoreCase));
        return imported;
    }

    private DataSourceEngineeringDto? ResolveDataSource(string? source, EngineeringPackage package)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        return (package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
                   .FirstOrDefault(x => x.Key.Equals(source, StringComparison.OrdinalIgnoreCase))
               ?? _dataSources.FindByKey(source);
    }

    private TagDefinition? ResolveExisting(TagEngineeringDto dto)
    {
        if (dto.Id.HasValue && _tags.TryGet(dto.Id.Value, out var byId)) return byId;
        return _tags.TryGetByPath(dto.Path, out var byPath) ? byPath : null;
    }

    private static void ValidateAccessPolicy(TagEngineeringDto dto, List<ImportIssue> issues)
    {
        if (dto.AccessPolicy is null) return;
        ValidateRoleList(dto.AccessPolicy.ReadRoles, "read", dto.Path, issues);
        ValidateRoleList(dto.AccessPolicy.WriteRoles, "write", dto.Path, issues);
        ValidateRoleList(dto.AccessPolicy.ConfigureRoles, "configure", dto.Path, issues);
    }

    private static void ValidateRoleList(
        IReadOnlyCollection<string>? roles,
        string operation,
        string tagPath,
        List<ImportIssue> issues)
    {
        if (roles is null) return;

        if (roles.Any(string.IsNullOrWhiteSpace))
            issues.Add(new(
                "TAG_ACCESS_ROLE_INVALID",
                $"TAG '{tagPath}' has a blank role in its {operation} access policy.",
                ImportEntityKind.Tag,
                tagPath,
                true));

        if (roles.Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1))
        {
            issues.Add(new(
                "TAG_ACCESS_ROLE_DUPLICATE",
                $"TAG '{tagPath}' repeats a role in its {operation} access policy.",
                ImportEntityKind.Tag,
                tagPath,
                true));
        }
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(TagEngineeringDto dto)
    {
        var result = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);

        Set(result, "address", dto.Address);
        Set(result, "scale.minimum", dto.ScaleMinimum);
        Set(result, "scale.maximum", dto.ScaleMaximum);
        Set(result, "historian.enabled", dto.Historian?.Enabled);
        Set(result, "historian.strategy", dto.Historian?.Strategy);
        Set(result, "historian.deadband", dto.Historian?.Deadband);
        Set(result, "historian.periodMs", dto.Historian?.PeriodMilliseconds);
        Set(result, "historian.maxPeriodMs", dto.Historian?.MaximumPeriodMilliseconds);
        MemoryEngineeringValueCodec.WriteToMetadata(result, dto.InitialValue);
        return result;
    }

    private static TagAccessPolicy? BuildAccessPolicy(TagAccessPolicyDto? dto) =>
        dto is null
            ? null
            : new TagAccessPolicy(dto.ReadRoles?.ToArray(), dto.WriteRoles?.ToArray(), dto.ConfigureRoles?.ToArray());

    private static void Set(Dictionary<string, string> map, string key, object? value)
    {
        if (value is not null)
            map[key] = Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }
}
