using Scada.Core.Alarms;
using Scada.Engineering.Contracts;

namespace Scada.Api.Runtime;

public sealed record AlarmSuppressionCauseProjection(
    AlarmSuppressionTargetKind TargetKind,
    Guid TargetId,
    string TargetName,
    string TargetPath,
    string Actor,
    string ReasonCode,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset? ExpiresAtUtc);

public sealed record AlarmSuppressionAlarmProjection(
    Guid AlarmId,
    string AlarmName,
    string? Area,
    Guid? LocationId,
    Guid? EquipmentId,
    string? LocationPath,
    bool IsSuppressed,
    IReadOnlyCollection<AlarmSuppressionCauseProjection> Causes);

public sealed record AlarmSuppressionLocationProjection(
    Guid Id,
    Guid? ParentLocationId,
    string Name,
    string Path,
    int AlarmCount,
    int SuppressedAlarmCount);

public sealed record AlarmSuppressionRuntimeSnapshot(
    string ProjectKey,
    long EngineeringRevision,
    long SuppressionRevision,
    IReadOnlyCollection<AlarmSuppressionAlarmProjection> Alarms,
    IReadOnlyCollection<AlarmSuppressionLocationProjection> Locations);

public sealed record RuntimeAlarmProjection(
    Guid DefinitionId,
    string Name,
    Guid TagId,
    AlarmType Type,
    AlarmPriority Priority,
    AlarmState State,
    DateTimeOffset LastTransition,
    object? LastValue,
    string? Area,
    string? Message,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgedBy,
    DateTimeOffset? ShelvedAt,
    string? ShelvedBy,
    bool IsSuppressed,
    IReadOnlyCollection<AlarmSuppressionCauseProjection> SuppressionCauses);

public sealed class AlarmSuppressionRuntimeProjection(Scada.Core.Alarms.IAlarmSuppressionStore store, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<AlarmSuppressionRuntimeSnapshot> BuildAsync(
        ScadaRuntimeFacade runtime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var before = runtime.Describe();
        if (string.IsNullOrWhiteSpace(before.ProjectKey) || !before.Revision.HasValue)
            throw new InvalidOperationException("Alarm suppression requires an active Engineering project revision.");
        var application = runtime.CaptureActiveApplication()
            ?? throw new InvalidOperationException("The active Engineering application snapshot is unavailable.");
        var locations = BuildLocations(application.Locations ?? Array.Empty<LocationEngineeringDto>());
        var equipment = BuildEquipment(application.Equipment ?? Array.Empty<EquipmentEngineeringDto>());
        var definitions = runtime.AlarmDefinitions();
        var stored = await store.ReadSnapshotAsync(before.ProjectKey, _timeProvider.GetUtcNow(), cancellationToken);
        if (!stored.ProjectKey.Equals(before.ProjectKey, StringComparison.Ordinal))
            throw new InvalidOperationException("Alarm suppression storage returned a different project snapshot.");
        var after = runtime.Describe();
        if (before.ProjectKey != after.ProjectKey || before.Revision != after.Revision ||
            !before.ActivatedAtUtc.HasValue || before.ActivatedAtUtc != after.ActivatedAtUtc)
            throw new InvalidOperationException("The active Runtime changed while alarm suppression state was being read.");

        var activeByTarget = stored.Records.ToDictionary(
            record => (record.TargetKind, record.TargetId));
        var alarmProjections = new List<AlarmSuppressionAlarmProjection>(definitions.Count);
        foreach (var definition in definitions)
        {
            var alarmLocationId = ResolveAlarmOperationalLocationId(
                definition.LocationId,
                definition.EquipmentId,
                equipment);
            LocationNode? alarmLocation = null;
            if (alarmLocationId.HasValue && !locations.TryGetValue(alarmLocationId.Value, out alarmLocation))
                throw new InvalidOperationException($"Alarm '{definition.Name}' refers to a missing operational Location.");

            var causes = new List<AlarmSuppressionCauseProjection>();
            if (activeByTarget.TryGetValue((AlarmSuppressionTargetKind.Alarm, definition.Id), out var individual))
                causes.Add(ProjectCause(individual, definition.Name, definition.Name));

            if (alarmLocation is not null)
            {
                foreach (var ancestorId in GetLocationScopeIds(alarmLocation))
                {
                    if (activeByTarget.TryGetValue((AlarmSuppressionTargetKind.Location, ancestorId), out var locationRecord))
                    {
                        var causeLocation = locations[ancestorId];
                        causes.Add(ProjectCause(locationRecord, causeLocation.Name, causeLocation.Path));
                    }
                }
            }

            alarmProjections.Add(new(
                definition.Id,
                definition.Name,
                definition.Area,
                alarmLocationId,
                definition.EquipmentId,
                alarmLocation?.Path,
                causes.Count > 0,
                causes.OrderBy(cause => cause.TargetKind).ThenBy(cause => cause.TargetPath, StringComparer.OrdinalIgnoreCase).ToArray()));
        }

        var locationProjections = locations.Values
            .OrderBy(location => location.Path, StringComparer.OrdinalIgnoreCase)
            .Select(location =>
            {
                var descendants = locations.Values
                    .Where(candidate => candidate.AncestorIds.Contains(location.Id))
                    .Select(candidate => candidate.Id)
                    .Append(location.Id)
                    .ToHashSet();
                var affected = alarmProjections.Where(alarm => alarm.LocationId.HasValue && descendants.Contains(alarm.LocationId.Value)).ToArray();
                return new AlarmSuppressionLocationProjection(
                    location.Id,
                    location.ParentId,
                    location.Name,
                    location.Path,
                    affected.Length,
                    affected.Count(alarm => alarm.IsSuppressed));
            })
            .ToArray();

        return new(before.ProjectKey, before.Revision.Value, stored.Revision, alarmProjections, locationProjections);
    }

    public static IReadOnlyCollection<RuntimeAlarmProjection> ProjectAlarms(
        IReadOnlyCollection<AlarmInstance> alarms,
        AlarmSuppressionRuntimeSnapshot snapshot)
    {
        var byId = snapshot.Alarms.ToDictionary(item => item.AlarmId);
        return alarms.Select(alarm =>
        {
            var suppression = byId.GetValueOrDefault(alarm.DefinitionId);
            return new RuntimeAlarmProjection(
                alarm.DefinitionId,
                alarm.Name,
                alarm.TagId,
                alarm.Type,
                alarm.Priority,
                alarm.State,
                alarm.LastTransition,
                alarm.LastValue,
                alarm.Area,
                alarm.Message,
                alarm.ActivatedAt,
                alarm.AcknowledgedAt,
                alarm.AcknowledgedBy,
                alarm.ShelvedAt,
                alarm.ShelvedBy,
                suppression?.IsSuppressed ?? false,
                suppression?.Causes ?? Array.Empty<AlarmSuppressionCauseProjection>());
        }).ToArray();
    }

    public static IReadOnlyCollection<Guid> GetLocationScopeIds(
        Guid locationId,
        IReadOnlyCollection<LocationEngineeringDto> locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        if (!locations.Any(item => item.Id == locationId))
            throw new InvalidOperationException("The requested operational Location is not present in the active tree.");
        var nodes = BuildLocations(locations);
        if (!nodes.TryGetValue(locationId, out var location))
            throw new InvalidOperationException("The requested operational Location has no stable identity.");
        return GetLocationScopeIds(location);
    }

    public static Guid? ResolveAlarmOperationalLocationId(
        Guid? locationId,
        Guid? equipmentId,
        IReadOnlyDictionary<Guid, EquipmentEngineeringDto> equipmentById)
    {
        ArgumentNullException.ThrowIfNull(equipmentById);
        if (locationId.HasValue && equipmentId.HasValue)
            throw new InvalidOperationException("An alarm cannot link to both an equipment and a Location.");
        if (locationId.HasValue)
        {
            if (locationId.Value == Guid.Empty)
                throw new InvalidOperationException("Alarm Location identity is invalid.");
            return locationId;
        }
        if (!equipmentId.HasValue) return null;
        if (equipmentId.Value == Guid.Empty || !equipmentById.TryGetValue(equipmentId.Value, out var equipment))
            throw new InvalidOperationException("The linked alarm equipment is missing or has an invalid identity in the active application.");
        if (equipment.LocationId == Guid.Empty)
            throw new InvalidOperationException("The linked alarm equipment has an invalid operational Location identity.");
        return equipment.LocationId;
    }

    private static IReadOnlyCollection<Guid> GetLocationScopeIds(LocationNode location) =>
        location.AncestorIds.Prepend(location.Id).ToArray();

    private static Dictionary<Guid, LocationNode> BuildLocations(IReadOnlyCollection<LocationEngineeringDto> source)
    {
        var locations = source.Where(item => item.Id.HasValue)
            .ToDictionary(item => item.Id!.Value, item => item);
        var result = new Dictionary<Guid, LocationNode>();
        foreach (var (id, location) in locations)
        {
            var ancestors = new List<Guid>();
            var seen = new HashSet<Guid> { id };
            var parent = location.ParentLocationId;
            while (parent.HasValue)
            {
                if (!seen.Add(parent.Value))
                    throw new InvalidOperationException("The active operational Location tree contains a cycle.");
                if (!locations.TryGetValue(parent.Value, out var parentLocation))
                    throw new InvalidOperationException("The active operational Location tree contains a missing parent.");
                ancestors.Add(parent.Value);
                parent = parentLocation.ParentLocationId;
            }

            var pathIds = ancestors.AsEnumerable().Reverse().Append(id);
            var path = string.Join(" / ", pathIds.Select(locationId => locations[locationId].Name));
            result.Add(id, new LocationNode(id, location.ParentLocationId, location.Name, path, ancestors));
        }

        return result;
    }

    private static Dictionary<Guid, EquipmentEngineeringDto> BuildEquipment(
        IReadOnlyCollection<EquipmentEngineeringDto> source)
    {
        var result = new Dictionary<Guid, EquipmentEngineeringDto>();
        foreach (var equipment in source)
        {
            if (!equipment.Id.HasValue) continue;
            if (equipment.Id.Value == Guid.Empty || !result.TryAdd(equipment.Id.Value, equipment))
                throw new InvalidOperationException("The active application contains an invalid or duplicate equipment identity.");
        }
        return result;
    }

    private static AlarmSuppressionCauseProjection ProjectCause(
        AlarmSuppressionRecord record,
        string name,
        string path) => new(
            record.TargetKind,
            record.TargetId,
            name,
            path,
            record.Actor,
            record.ReasonCode,
            record.AppliedAtUtc,
            record.ExpiresAtUtc);

    private sealed record LocationNode(
        Guid Id,
        Guid? ParentId,
        string Name,
        string Path,
        IReadOnlyCollection<Guid> AncestorIds);
}
