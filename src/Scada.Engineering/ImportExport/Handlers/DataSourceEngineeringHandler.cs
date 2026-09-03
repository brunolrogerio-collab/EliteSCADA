using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Validation;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class DataSourceEngineeringHandler
{
    private readonly IDataSourceEngineeringRegistry _registry;
    private readonly ITagRegistry _tags;
    private readonly IAlarmEngine _alarms;
    private readonly ICommandEngineeringRegistry _commands;
    private readonly IDataSourceConfigurationValidator? _configurationValidator;

    public DataSourceEngineeringHandler(
        IDataSourceEngineeringRegistry registry,
        ITagRegistry tags,
        IAlarmEngine alarms,
        ICommandEngineeringRegistry commands,
        IDataSourceConfigurationValidator? configurationValidator = null)
    {
        _registry = registry;
        _tags = tags;
        _alarms = alarms;
        _commands = commands;
        _configurationValidator = configurationValidator;
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var dataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>();
        var duplicates = EngineeringHandlerSupport.Duplicates(dataSources.Select(x => x.Key));

        foreach (var dto in dataSources)
        {
            var issues = EngineeringValidator.ValidateDataSource(dto).ToList();
            issues.AddRange(MemoryEngineeringValidator.ValidateDataSource(dto));
            if (_configurationValidator is not null)
                issues.AddRange(_configurationValidator.Validate(dto));
            ValidateClientMemoryTransition(dto, package, issues);

            if (duplicates.Contains(dto.Key))
                issues.Add(new(
                    "DATASOURCE_DUPLICATE_IN_FILE",
                    $"Data source key '{dto.Key}' appears more than once in the import package.",
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

            _registry.Upsert(dto with { Id = existing?.Id ?? dto.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }
    }

    private void ValidateClientMemoryTransition(
        DataSourceEngineeringDto dataSource,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        if (!MemoryEngineeringValidator.IsClientMemoryDriver(dataSource.Driver))
            return;

        foreach (var currentTag in _tags.Snapshot()
                     .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase)))
        {
            var imported = package.Tags.FirstOrDefault(tag =>
                tag.Id == currentTag.Id ||
                tag.Path.Equals(currentTag.Path, StringComparison.OrdinalIgnoreCase));
            var effective = imported ?? EngineeringDtoMapper.ToDto(currentTag);

            // A TAG explicitly moved to another source in the same package is no
            // longer affected by this Data Source driver transition.
            if (!string.Equals(effective.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
                continue;

            if (effective.Historian?.Enabled == true)
            {
                issues.Add(new(
                    "CLIENT_MEMORY_EXISTING_HISTORIAN_NOT_ALLOWED",
                    $"Data source '{dataSource.Key}' cannot become Client Memory while TAG '{effective.Path}' remains configured for the global historian.",
                    ImportEntityKind.DataSource,
                    dataSource.Key,
                    true));
            }

            if (_alarms.Definitions().Any(alarm => alarm.TagId == currentTag.Id))
            {
                issues.Add(new(
                    "CLIENT_MEMORY_EXISTING_ALARM_NOT_ALLOWED",
                    $"Data source '{dataSource.Key}' cannot become Client Memory while TAG '{effective.Path}' remains targeted by the global alarm engine.",
                    ImportEntityKind.DataSource,
                    dataSource.Key,
                    true));
            }

            var currentCommands = _commands.Snapshot().Where(command =>
                command.TargetTagId == currentTag.Id ||
                (!string.IsNullOrWhiteSpace(command.TargetTagPath) &&
                 command.TargetTagPath.Equals(currentTag.Path, StringComparison.OrdinalIgnoreCase)));

            foreach (var currentCommand in currentCommands)
            {
                var importedCommand = (package.Commands ?? Array.Empty<CommandEngineeringDto>()).FirstOrDefault(command =>
                    (currentCommand.Id.HasValue && command.Id == currentCommand.Id) ||
                    command.Key.Equals(currentCommand.Key, StringComparison.OrdinalIgnoreCase));
                var effectiveCommand = importedCommand ?? currentCommand;
                var stillTargetsClientTag =
                    effectiveCommand.TargetTagId == currentTag.Id ||
                    (!string.IsNullOrWhiteSpace(effectiveCommand.TargetTagPath) &&
                     effectiveCommand.TargetTagPath.Equals(effective.Path, StringComparison.OrdinalIgnoreCase));
                if (!stillTargetsClientTag)
                    continue;

                issues.Add(new(
                    "CLIENT_MEMORY_EXISTING_COMMAND_NOT_ALLOWED",
                    $"Data source '{dataSource.Key}' cannot become Client Memory while TAG '{effective.Path}' remains targeted by server-side Command '{effectiveCommand.Key}'.",
                    ImportEntityKind.DataSource,
                    dataSource.Key,
                    true));
            }
        }
    }

    private DataSourceEngineeringDto? ResolveExisting(DataSourceEngineeringDto dto)
    {
        if (dto.Id.HasValue)
        {
            var byId = _registry.Find(dto.Id.Value);
            if (byId is not null) return byId;
        }

        return _registry.FindByKey(dto.Key);
    }
}
