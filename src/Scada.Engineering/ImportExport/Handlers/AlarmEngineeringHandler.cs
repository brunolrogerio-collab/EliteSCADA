using Scada.Core.Alarms;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class AlarmEngineeringHandler
{
    private readonly IAlarmEngine _alarms;
    private readonly TagEngineeringHandler _tags;

    public AlarmEngineeringHandler(IAlarmEngine alarms, TagEngineeringHandler tags)
    {
        _alarms = alarms;
        _tags = tags;
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        foreach (var dto in package.Alarms)
        {
            var issues = EngineeringValidator.ValidateAlarm(dto).ToList();
            if (_tags.ResolveAlarmTagForPreview(dto, package) is null)
                issues.Add(new(
                    "ALARM_TAG_NOT_FOUND",
                    $"Referenced tag for alarm '{dto.Name}' was not found in current registry or package.",
                    ImportEntityKind.Alarm,
                    dto.Name,
                    true));

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.Alarm,
                dto.Name,
                ResolveExisting(dto) is not null,
                mode,
                issues);
        }
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        foreach (var dto in package.Alarms)
        {
            var existing = ResolveExisting(dto);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            var tag = _tags.ResolveAlarmTag(dto)!;
            var definition = new AlarmDefinition(
                existing?.Id ?? dto.Id ?? Guid.NewGuid(),
                dto.Name,
                tag.Id,
                dto.Type,
                dto.Priority,
                dto.Setpoint,
                dto.DigitalActiveValue,
                dto.Area,
                dto.Message,
                dto.Enabled,
                dto.AlarmClass,
                dto.ActivationDelayMilliseconds.HasValue
                    ? TimeSpan.FromMilliseconds(dto.ActivationDelayMilliseconds.Value)
                    : null,
                dto.RequiresAcknowledgement,
                dto.ShelvingAllowed,
                dto.Metadata);

            _alarms.Register(definition);
            if (existing is null) created++; else updated++;
        }
    }

    private AlarmDefinition? ResolveExisting(AlarmEngineeringDto dto)
    {
        if (dto.Id.HasValue)
            return _alarms.Definitions().FirstOrDefault(x => x.Id == dto.Id.Value);

        var tag = _tags.ResolveAlarmTag(dto);
        return tag is null
            ? null
            : _alarms.Definitions().FirstOrDefault(x =>
                x.TagId == tag.Id && x.Name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase));
    }
}
