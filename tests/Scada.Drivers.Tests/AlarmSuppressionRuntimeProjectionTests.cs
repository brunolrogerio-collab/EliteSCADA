using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class AlarmSuppressionRuntimeProjectionTests
{
    [Fact]
    public void Location_scope_includes_selected_location_and_ancestors_but_not_textual_siblings()
    {
        var rootId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var selectedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var siblingId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var locations = new LocationEngineeringDto[]
        {
            new(rootId, "Area A"),
            new(selectedId, "Subarea A1", rootId),
            new(siblingId, "Area A1", rootId)
        };

        var scope = AlarmSuppressionRuntimeProjection.GetLocationScopeIds(selectedId, locations);

        Assert.Equal(new[] { selectedId, rootId }, scope);
        Assert.DoesNotContain(siblingId, scope);
    }

    [Fact]
    public void Equipment_link_resolves_location_from_the_active_application_record()
    {
        var equipmentId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var currentLocationId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        IReadOnlyDictionary<Guid, EquipmentEngineeringDto> equipment = new Dictionary<Guid, EquipmentEngineeringDto>
        {
            [equipmentId] = new(equipmentId, "Plant/Pump01", "Pump 01", LocationId: currentLocationId)
        };
        IReadOnlyDictionary<Guid, EquipmentEngineeringDto> noEquipment = new Dictionary<Guid, EquipmentEngineeringDto>();

        var resolved = AlarmSuppressionRuntimeProjection.ResolveAlarmOperationalLocationId(
            locationId: null,
            equipmentId,
            equipment);

        Assert.Equal(currentLocationId, resolved);
        Assert.Equal(currentLocationId, AlarmSuppressionRuntimeProjection.ResolveAlarmOperationalLocationId(
            locationId: currentLocationId,
            equipmentId: null,
            equipmentById: noEquipment));
        Assert.Null(AlarmSuppressionRuntimeProjection.ResolveAlarmOperationalLocationId(
            locationId: null,
            equipmentId: null,
            equipmentById: noEquipment));
    }

    [Fact]
    public void Alarm_location_resolution_fails_closed_for_missing_or_ambiguous_links()
    {
        var locationId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        IReadOnlyDictionary<Guid, EquipmentEngineeringDto> noEquipment = new Dictionary<Guid, EquipmentEngineeringDto>();

        Assert.Throws<InvalidOperationException>(() =>
            AlarmSuppressionRuntimeProjection.ResolveAlarmOperationalLocationId(null, equipmentId, noEquipment));
        Assert.Throws<InvalidOperationException>(() =>
            AlarmSuppressionRuntimeProjection.ResolveAlarmOperationalLocationId(locationId, equipmentId, noEquipment));
    }

    [Fact]
    public void ProjectAlarms_keeps_alarm_state_and_attaches_suppression_causes()
    {
        var definitionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);
        var alarm = new AlarmInstance(
            definitionId,
            "Pump pressure high",
            Guid.NewGuid(),
            AlarmType.High,
            AlarmPriority.High,
            AlarmState.Active,
            timestamp,
            12.5,
            "Authority.Area1",
            "Pressure exceeded limit",
            timestamp);
        var cause = new AlarmSuppressionCauseProjection(
            AlarmSuppressionTargetKind.Location,
            locationId,
            "Pump station",
            "Plant / Pump station",
            "operator",
            AlarmSuppressionReasonCodes.Maintenance,
            timestamp,
            timestamp.AddHours(1));
        var snapshot = new AlarmSuppressionRuntimeSnapshot(
            "project-1",
            7,
            3,
            [new AlarmSuppressionAlarmProjection(
                definitionId,
                alarm.Name,
                alarm.Area,
                locationId,
                null,
                cause.TargetPath,
                true,
                [cause])],
            []);

        var projected = Assert.Single(AlarmSuppressionRuntimeProjection.ProjectAlarms([alarm], snapshot));

        Assert.Equal(AlarmState.Active, projected.State);
        Assert.True(projected.IsSuppressed);
        Assert.Equal(cause, Assert.Single(projected.SuppressionCauses));
        Assert.Equal("Authority.Area1", projected.Area);
    }

    [Fact]
    public void ProjectAlarms_without_matching_suppression_is_explicitly_unsuppressed()
    {
        var alarm = new AlarmInstance(
            Guid.NewGuid(),
            "Pump pressure high",
            Guid.NewGuid(),
            AlarmType.High,
            AlarmPriority.High,
            AlarmState.Active,
            DateTimeOffset.UtcNow,
            12.5,
            null,
            null);
        var snapshot = new AlarmSuppressionRuntimeSnapshot("project-1", 1, 0, [], []);

        var projected = Assert.Single(AlarmSuppressionRuntimeProjection.ProjectAlarms([alarm], snapshot));

        Assert.Equal(AlarmState.Active, projected.State);
        Assert.False(projected.IsSuppressed);
        Assert.Empty(projected.SuppressionCauses);
    }
}
