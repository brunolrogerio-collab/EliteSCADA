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
    private readonly IAlarmEngine _alarms;

    public TagEngineeringHandler(
        ITagRegistry tags,
        IDataSourceEngineeringRegistry dataSources,
        IAlarmEngine alarms)
    {
        _tags = tags;
        _dataSources = dataSources;
        _alarms = alarms;
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var dataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>();
        var duplicatePaths = EngineeringHandlerSupport.Duplicates(package.Tags.Select(x => x.Path));

        foreach (var dto in package.Tags)
        {
            var issues = EngineeringValidator.ValidateTag(dto).ToList();
            ValidateAccessPolicy(dto, issues);
            issues.AddRange(CommunicationTagBindingEngineeringValidator.Validate(dto, package.SchemaVersion));

            var dataSource = ResolveDataSource(dto.Source, package);
            issues.AddRange(MemoryEngineeringValidator.ValidateTag(dto, dataSource));
            ValidateClientMemoryTransition(dto, dataSource, issues);

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

            var metadata = dto.Metadata is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(dto.Address)) metadata["address"] = dto.Address;

            var accessPolicy = dto.AccessPolicy is null
                ? null
                : new TagAccessPolicy(
                    dto.AccessPolicy.ReadRoles?.ToArray(),
                    dto.AccessPolicy.WriteRoles?.ToArray(),
                    dto.AccessPolicy.ConfigureRoles?.ToArray());

            var tag = new TagDefinition(
                existing?.Id ?? dto.Id ?? Guid.NewGuid(),
                dto.Name,
                dto.Path,
                dto.DataType,
                dto.Source,
                dto.EngineeringUnit,
                dto.Description,
                dto.ReadOnly,
                metadata,
                accessPolicy);

            if (operation == ImportOperation.Create)
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

    private TagDefinition? ResolveExisting(TagEngineeringDto dto)
    {
        if (dto.Id.HasValue && _tags.TryGet(dto.Id.Value, out var byId) && byId is not null) return byId;
        return _tags.TryGetByPath(dto.Path, out var byPath) ? byPath : null;
    }

    private DataSourceEngineeringDto? ResolveDataSource(string? source, EngineeringPackage package)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        return (package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
                   .FirstOrDefault(x => x.Key.Equals(source, StringComparison.OrdinalIgnoreCase))
               ?? _dataSources.FindByKey(source);
    }

    private void ValidateClientMemoryTransition(
        TagEngineeringDto dto,
        DataSourceEngineeringDto? dataSource,
        List<ImportIssue> issues)
    {
        var existing = ResolveExisting(dto);
        if (existing is null || !InternalMemoryEngineering.IsClientMemory(dataSource)) return;
        if (existing.ReadOnly)
        {
            issues.Add(new(
                "CLIENT_MEMORY_EXISTING_TAG_READONLY",
                $"Existing TAG '{dto.Path}' is read-only and cannot be converted to Client Memory without first making it writable.",
                ImportEntityKind.Tag,
                dto.Path,
                true));
        }
    }

    private static void ValidateAccessPolicy(TagEngineeringDto dto, List<ImportIssue> issues)
    {
        if (dto.AccessPolicy is null) return;

        ValidateRoles(dto.AccessPolicy.ReadRoles, "readRoles", dto.Path, issues);
        ValidateRoles(dto.AccessPolicy.WriteRoles, "writeRoles", dto.Path, issues);
        ValidateRoles(dto.AccessPolicy.ConfigureRoles, "configureRoles", dto.Path, issues);
    }

    private static void ValidateRoles(
        IReadOnlyCollection<string>? roles,
        string field,
        string path,
        List<ImportIssue> issues)
    {
        if (roles is null) return;
        foreach (var role in roles)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                issues.Add(new(
                    "TAG_ACCESS_ROLE_EMPTY",
                    $"TAG '{path}' has an empty {field} role.",
                    ImportEntityKind.Tag,
                    path,
                    true));
                continue;
            }

            if (!string.Equals(role, role.Trim(), StringComparison.Ordinal))
            {
                issues.Add(new(
                    "TAG_ACCESS_ROLE_WHITESPACE",
                    $"TAG '{path}' {field} role '{role}' has leading or trailing whitespace.",
                    ImportEntityKind.Tag,
                    path,
                    true));
            }
        }
    }
}
