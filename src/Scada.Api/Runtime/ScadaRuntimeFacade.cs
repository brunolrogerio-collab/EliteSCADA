using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Simulation;

namespace Scada.Api.Runtime;

public sealed record ScadaRuntimeDescriptor(
    string Mode,
    string? ProjectKey,
    long? Revision,
    DateTimeOffset? ActivatedAtUtc,
    IReadOnlyCollection<DriverStatus> Drivers,
    int TagCount,
    int ActiveAlarmCount);

public sealed class ScadaRuntimeFacade(
    DemoRuntimeServices fallback,
    SimulationDriver fallbackDriver,
    IEngineeringRuntimeCoordinator engineeringRuntime)
{
    public bool IsEngineeringActive => engineeringRuntime.Describe().Revision.HasValue;

    public ScadaRuntimeDescriptor Describe()
    {
        var engineering = engineeringRuntime.Describe();
        if (engineering.Revision.HasValue)
        {
            return new ScadaRuntimeDescriptor(
                "engineering",
                engineering.ProjectKey,
                engineering.Revision,
                engineering.ActivatedAtUtc,
                engineering.Drivers,
                engineering.TagCount,
                engineering.ActiveAlarmCount);
        }

        return new ScadaRuntimeDescriptor(
            "simulation",
            null,
            null,
            null,
            new[] { fallbackDriver.Status },
            fallback.Registry.Snapshot().Count,
            fallback.Alarms.Snapshot(activeOnly: true).Count);
    }

    public IReadOnlyCollection<TagDefinition> Tags() =>
        IsEngineeringActive ? engineeringRuntime.Tags() : fallback.Registry.Snapshot();

    public IReadOnlyCollection<TagValue> CurrentValues() =>
        IsEngineeringActive ? engineeringRuntime.CurrentValues() : fallback.Cache.Snapshot();

    public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() =>
        IsEngineeringActive ? engineeringRuntime.AlarmDefinitions() : fallback.Alarms.Definitions();

    public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) =>
        IsEngineeringActive ? engineeringRuntime.Alarms(activeOnly) : fallback.Alarms.Snapshot(activeOnly);

    public IReadOnlyCollection<DriverStatus> Drivers() =>
        IsEngineeringActive ? engineeringRuntime.Describe().Drivers : new[] { fallbackDriver.Status };

    public bool TryGetTag(Guid tagId, out TagDefinition? tag)
    {
        if (IsEngineeringActive)
            return engineeringRuntime.TryGetTag(tagId, out tag);

        return fallback.Registry.TryGet(tagId, out tag);
    }

    public bool TryGetTagByPath(string path, out TagDefinition? tag)
    {
        if (IsEngineeringActive)
            return engineeringRuntime.TryGetTagByPath(path, out tag);

        return fallback.Registry.TryGetByPath(path, out tag);
    }

    public bool TryGetCurrent(Guid tagId, out TagValue? value)
    {
        if (IsEngineeringActive)
            return engineeringRuntime.TryGetCurrent(tagId, out value);

        return fallback.Cache.TryGet(tagId, out value);
    }

    public ValueTask<bool> AcknowledgeAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default) =>
        IsEngineeringActive
            ? engineeringRuntime.AcknowledgeAlarmAsync(alarmId, user, cancellationToken)
            : fallback.Alarms.AcknowledgeAsync(alarmId, user, cancellationToken);

    public ValueTask WriteAsync(
        Guid tagId,
        object? value,
        CancellationToken cancellationToken = default) =>
        IsEngineeringActive
            ? engineeringRuntime.WriteAsync(tagId, value, cancellationToken)
            : fallbackDriver.WriteAsync(tagId, value, cancellationToken);
}
