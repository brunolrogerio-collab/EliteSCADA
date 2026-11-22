using Scada.Core.Alarms;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class AlarmEngineeringHandler
{
    private readonly IAlarmEngine _alarms;
    private readonly TagEngineeringHandler _tags;
    private readonly IEngineeringAssetRegistry _assets;

    public AlarmEngineeringHandler(IAlarmEngine alarms, TagEngineeringHandler tags, IEngineeringAssetRegistry assets)
    {
        _alarms = alarms;
        _tags = tags;
        _assets = assets;
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
            else if (_tags.IsClientMemoryAlarmTarget(dto, package))
                issues.Add(new(
                    "CLIENT_MEMORY_ALARM_NOT_ALLOWED",
                    $"Alarm '{dto.Name}' targets Client Memory, which is runtime-client-local and cannot feed the global server alarm engine.",
                    ImportEntityKind.Alarm,
                    dto.Name,
                    true));

            ValidateOperationalTarget(dto, package, issues);

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
                dto.Metadata,
                dto.SoundProfile,
                dto.LocationId,
                dto.EquipmentId);

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

    private void ValidateOperationalTarget(
        AlarmEngineeringDto dto,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        var packageLocations = package.Locations ?? Array.Empty<LocationEngineeringDto>();
        var packageEquipment = package.Equipment ?? Array.Empty<EquipmentEngineeringDto>();
        var location = dto.LocationId.HasValue
            ? packageLocations.FirstOrDefault(item => item.Id == dto.LocationId) ?? _assets.FindLocation(dto.LocationId.Value)
            : null;
        var equipment = dto.EquipmentId.HasValue
            ? packageEquipment.FirstOrDefault(item => item.Id == dto.EquipmentId) ?? _assets.FindEquipment(dto.EquipmentId.Value)
            : null;

        if (dto.LocationId.HasValue && location is null)
            issues.Add(new(
                "ALARM_LOCATION_NOT_FOUND",
                $"Operational Location '{dto.LocationId:D}' for alarm '{dto.Name}' was not found.",
                ImportEntityKind.Alarm,
                dto.Name,
                true));

        if (dto.LocationId.HasValue && dto.EquipmentId.HasValue)
            issues.Add(new(
                "ALARM_OPERATIONAL_TARGET_AMBIGUOUS",
                $"Alarm '{dto.Name}' must link to either an Equipment or a Location, not both.",
                ImportEntityKind.Alarm,
                dto.Name,
                true));

        if (dto.EquipmentId.HasValue && equipment is null)
            issues.Add(new(
                "ALARM_EQUIPMENT_NOT_FOUND",
                $"Equipment '{dto.EquipmentId:D}' for alarm '{dto.Name}' was not found.",
                ImportEntityKind.Alarm,
                dto.Name,
                true));

    }
}
