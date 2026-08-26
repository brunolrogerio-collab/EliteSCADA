using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Validation;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class DataSourceEngineeringHandler
{
    private readonly IDataSourceEngineeringRegistry _dataSources;

    public DataSourceEngineeringHandler(IDataSourceEngineeringRegistry dataSources) => _dataSources = dataSources;

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var dataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(dataSources.Select(x => x.Key));

        foreach (var dto in dataSources)
        {
            var issues = EngineeringValidator.ValidateDataSource(dto).ToList();
            issues.AddRange(MemoryEngineeringValidator.ValidateDataSource(dto));
            if (duplicateKeys.Contains(dto.Key))
                issues.Add(new(
                    "DATASOURCE_DUPLICATE_IN_FILE",
                    $"Data source key '{dto.Key}' appears more than once in the import file.",
                    ImportEntityKind.DataSource,
                    dto.Key,
                    true));

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.DataSource,
                dto.Key,
                ResolveExisting(dto) is not null,
                mode,
                issues);
        }
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        foreach (var dto in package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
        {
            var existing = ResolveExisting(dto);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            var value = dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() };
            _dataSources.Upsert(value);
            if (existing is null) created++; else updated++;
        }
    }

    private DataSourceEngineeringDto? ResolveExisting(DataSourceEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _dataSources.Snapshot().FirstOrDefault(x => x.Id == dto.Id.Value);
            if (byId is not null) return byId;
        }
        return _dataSources.FindByKey(dto.Key);
    }
}
