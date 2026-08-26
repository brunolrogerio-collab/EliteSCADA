using System.Collections.Concurrent;
using System.Globalization;
using Scada.Core.Abstractions;
using Scada.Core.Events;
using Scada.Core.Tags;

namespace Scada.Core.Alarms;

public sealed class InMemoryAlarmEngine : IAlarmEngine
{
    private readonly ConcurrentDictionary<Guid, AlarmDefinition> _definitions = new();
    private readonly ConcurrentDictionary<Guid, AlarmInstance> _instances = new();
    private readonly IScadaEventBus _eventBus;
    private readonly IDisposable _subscription;
    private readonly Action? _definitionsChanged;

    public InMemoryAlarmEngine(IScadaEventBus eventBus, Action? definitionsChanged = null)
    {
        _eventBus = eventBus;
        _definitionsChanged = definitionsChanged;
        _subscription = eventBus.Subscribe<TagValueChanged>(EvaluateAsync);
    }

    public AlarmDefinition Register(AlarmDefinition definition)
    {
        _definitions[definition.Id] = definition;
        _instances.AddOrUpdate(
            definition.Id,
            _ => NewNormal(definition),
            (_, current) => current with
            {
                Name = definition.Name,
                TagId = definition.TagId,
                Type = definition.Type,
                Priority = definition.Priority,
                Area = definition.Area,
                Message = definition.Message,
                State = definition.Enabled ? current.State : AlarmState.Disabled
            });
        _definitionsChanged?.Invoke();
        return definition;
    }

    public IReadOnlyCollection<AlarmDefinition> Definitions() => _definitions.Values.OrderBy(x => x.Name).ToArray();

    public IReadOnlyCollection<AlarmInstance> Snapshot(bool activeOnly = false)
    {
        var values = _instances.Values.AsEnumerable();
        if (activeOnly) values = values.Where(x => x.State is AlarmState.Active or AlarmState.Acknowledged);
        return values.OrderByDescending(x => x.Priority).ThenByDescending(x => x.LastTransition).ToArray();
    }

    public async ValueTask<bool> AcknowledgeAsync(Guid definitionId, string user, CancellationToken cancellationToken = default)
    {
        if (!_instances.TryGetValue(definitionId, out var current) || current.State != AlarmState.Active) return false;
        var next = current with
        {
            State = AlarmState.Acknowledged,
            AcknowledgedAt = DateTimeOffset.UtcNow,
            AcknowledgedBy = user,
            LastTransition = DateTimeOffset.UtcNow
        };
        _instances[definitionId] = next;
        await _eventBus.PublishAsync(new AlarmStateChanged(current, next, DateTimeOffset.UtcNow), cancellationToken);
        return true;
    }

    public void Clear()
    {
        _definitions.Clear();
        _instances.Clear();
        _definitionsChanged?.Invoke();
    }

    private async ValueTask EvaluateAsync(TagValueChanged evt)
    {
        foreach (var definition in _definitions.Values.Where(x => x.TagId == evt.Tag.Id && x.Enabled))
        {
            var active = IsActive(definition, evt.Current);
            var current = _instances.GetOrAdd(definition.Id, _ => NewNormal(definition));
            var nextState = active
                ? current.State == AlarmState.Acknowledged ? AlarmState.Acknowledged : AlarmState.Active
                : current.State is AlarmState.Active or AlarmState.Acknowledged ? AlarmState.Returned : AlarmState.Normal;

            if (nextState == current.State)
            {
                _instances[definition.Id] = current with { LastValue = evt.Current.Value };
                continue;
            }

            var now = DateTimeOffset.UtcNow;
            var next = current with
            {
                State = nextState,
                LastTransition = now,
                LastValue = evt.Current.Value,
                ActivatedAt = active && current.ActivatedAt is null ? now : current.ActivatedAt
            };
            _instances[definition.Id] = next;
            await _eventBus.PublishAsync(new AlarmStateChanged(current, next, now));
        }
    }

    private static bool IsActive(AlarmDefinition definition, TagValue value)
    {
        if (value.Quality != TagQuality.Good)
            return definition.Type == AlarmType.Communication;

        if (definition.Type == AlarmType.Communication) return false;
        if (definition.Type == AlarmType.Digital)
            return Convert.ToBoolean(value.Value, CultureInfo.InvariantCulture) == definition.DigitalActiveValue;

        if (!TryDouble(value.Value, out var numeric) || definition.Setpoint is null) return false;
        return definition.Type switch
        {
            AlarmType.High or AlarmType.HighHigh => numeric >= definition.Setpoint,
            AlarmType.Low or AlarmType.LowLow => numeric <= definition.Setpoint,
            _ => false
        };
    }

    private static bool TryDouble(object? value, out double result)
    {
        try { result = Convert.ToDouble(value, CultureInfo.InvariantCulture); return true; }
        catch { result = default; return false; }
    }

    private static AlarmInstance NewNormal(AlarmDefinition definition) =>
        new(definition.Id, definition.Name, definition.TagId, definition.Type, definition.Priority,
            definition.Enabled ? AlarmState.Normal : AlarmState.Disabled, DateTimeOffset.UtcNow, null,
            definition.Area, definition.Message);

    public void Dispose() => _subscription.Dispose();
}
