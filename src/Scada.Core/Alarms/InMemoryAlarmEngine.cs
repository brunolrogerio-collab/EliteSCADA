using System.Globalization;
using System.Diagnostics;
using Scada.Core.Abstractions;
using Scada.Core.Events;
using Scada.Core.Tags;

namespace Scada.Core.Alarms;

public sealed class InMemoryAlarmEngine : IAlarmEngine
{
    private readonly Dictionary<Guid, AlarmDefinition> _definitions = [];
    private readonly Dictionary<Guid, AlarmInstance> _instances = [];
    private readonly Dictionary<Guid, AlarmState> _shelvedUnderlyingStates = [];
    private readonly Dictionary<Guid, TagValue> _latestValues = [];
    private readonly Dictionary<Guid, PendingActivation> _pendingActivations = [];
    private readonly object _sync = new();
    private readonly IScadaEventBus _eventBus;
    private readonly IDisposable _subscription;
    private readonly Action? _definitionsChanged;
    private readonly TimeProvider _timeProvider;
    private bool _disposed;

    public InMemoryAlarmEngine(
        IScadaEventBus eventBus,
        Action? definitionsChanged = null,
        TimeProvider? timeProvider = null)
    {
        _eventBus = eventBus;
        _definitionsChanged = definitionsChanged;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _subscription = eventBus.Subscribe<TagValueChanged>(EvaluateAsync);
    }

    public AlarmDefinition Register(AlarmDefinition definition)
    {
        lock (_sync)
        {
            CancelPendingActivationLocked(definition.Id);
            _definitions[definition.Id] = definition;
            if (_instances.TryGetValue(definition.Id, out var current))
            {
                _instances[definition.Id] = current with
                {
                    Name = definition.Name,
                    TagId = definition.TagId,
                    Type = definition.Type,
                    Priority = definition.Priority,
                    Area = definition.Area,
                    Message = definition.Message,
                    State = definition.Enabled ? current.State : AlarmState.Disabled
                };
            }
            else
            {
                _instances[definition.Id] = NewNormal(definition, _timeProvider.GetUtcNow());
            }

            if (!definition.Enabled)
                _shelvedUnderlyingStates.Remove(definition.Id);
        }

        _definitionsChanged?.Invoke();
        return definition;
    }

    public bool Remove(Guid definitionId)
    {
        bool removed;
        lock (_sync)
        {
            removed = _definitions.Remove(definitionId);
            if (!removed) return false;
            CancelPendingActivationLocked(definitionId);
            _instances.Remove(definitionId);
            _shelvedUnderlyingStates.Remove(definitionId);
            _latestValues.Remove(definitionId);
        }

        _definitionsChanged?.Invoke();
        return true;
    }

    public IReadOnlyCollection<AlarmDefinition> Definitions()
    {
        lock (_sync)
            return _definitions.Values.OrderBy(x => x.Name).ToArray();
    }

    public IReadOnlyCollection<AlarmInstance> Snapshot(bool activeOnly = false)
    {
        lock (_sync)
        {
            IEnumerable<AlarmInstance> values = _instances.Values;
            if (activeOnly)
                values = values.Where(x =>
                    x.State is AlarmState.Active or AlarmState.Acknowledged ||
                    (x.State == AlarmState.Returned && x.AcknowledgedAt is null));
            return values.OrderByDescending(x => x.Priority).ThenByDescending(x => x.LastTransition).ToArray();
        }
    }

    public async ValueTask<bool> AcknowledgeAsync(
        Guid definitionId,
        string user,
        CancellationToken cancellationToken = default)
    {
        AlarmStateChanged? stateChanged;
        lock (_sync)
        {
            if (!_instances.TryGetValue(definitionId, out var current) ||
                (current.State != AlarmState.Active && !(current.State == AlarmState.Returned && current.AcknowledgedAt is null)))
                return false;

            var now = _timeProvider.GetUtcNow();
            var next = current with
            {
                State = current.State == AlarmState.Active ? AlarmState.Acknowledged : AlarmState.Returned,
                AcknowledgedAt = now,
                AcknowledgedBy = user,
                LastTransition = now
            };
            _instances[definitionId] = next;
            stateChanged = new AlarmStateChanged(current, next, now);
        }

        await _eventBus.PublishAsync(stateChanged, cancellationToken);
        return true;
    }

    public async ValueTask<bool> ShelveAsync(
        Guid definitionId,
        string user,
        CancellationToken cancellationToken = default)
    {
        AlarmStateChanged stateChanged;
        lock (_sync)
        {
            if (!_definitions.TryGetValue(definitionId, out var definition) || !definition.ShelvingAllowed)
                return false;
            if (!_instances.TryGetValue(definitionId, out var current)) return false;
            if (current.State is AlarmState.Disabled or AlarmState.Shelved) return false;

            _shelvedUnderlyingStates[definitionId] = current.State;
            var now = _timeProvider.GetUtcNow();
            var next = current with
            {
                State = AlarmState.Shelved,
                LastTransition = now,
                ShelvedAt = now,
                ShelvedBy = user
            };
            _instances[definitionId] = next;
            stateChanged = new AlarmStateChanged(current, next, now);
        }

        await _eventBus.PublishAsync(stateChanged, cancellationToken);
        return true;
    }

    public async ValueTask<bool> UnshelveAsync(
        Guid definitionId,
        string user,
        CancellationToken cancellationToken = default)
    {
        _ = user;
        AlarmStateChanged stateChanged;
        lock (_sync)
        {
            if (!_definitions.TryGetValue(definitionId, out var definition)) return false;
            if (!_instances.TryGetValue(definitionId, out var current) || current.State != AlarmState.Shelved)
                return false;

            var underlying = _shelvedUnderlyingStates.Remove(definitionId, out var stored)
                ? stored
                : AlarmState.Normal;
            var now = _timeProvider.GetUtcNow();
            var next = current with
            {
                State = definition.Enabled ? underlying : AlarmState.Disabled,
                LastTransition = now,
                ShelvedAt = null,
                ShelvedBy = null
            };
            _instances[definitionId] = next;
            stateChanged = new AlarmStateChanged(current, next, now);
        }

        await _eventBus.PublishAsync(stateChanged, cancellationToken);
        return true;
    }

    public void Clear()
    {
        lock (_sync)
        {
            CancelAllPendingActivationsLocked();
            _definitions.Clear();
            _instances.Clear();
            _shelvedUnderlyingStates.Clear();
            _latestValues.Clear();
        }

        _definitionsChanged?.Invoke();
    }

    private async ValueTask EvaluateAsync(TagValueChanged evt)
    {
        List<AlarmStateChanged> stateChanges = [];
        lock (_sync)
        {
            if (_disposed) return;

            var definitions = _definitions.Values
                .Where(x => x.TagId == evt.Tag.Id && x.Enabled)
                .ToArray();
            foreach (var definition in definitions)
            {
                _latestValues[definition.Id] = evt.Current;
                var current = _instances.TryGetValue(definition.Id, out var found)
                    ? found
                    : _instances[definition.Id] = NewNormal(definition, _timeProvider.GetUtcNow());
                var active = IsActive(definition, evt.Current);
                var isShelved = current.State == AlarmState.Shelved;
                var underlyingState = isShelved
                    ? _shelvedUnderlyingStates.GetValueOrDefault(definition.Id, AlarmState.Normal)
                    : current.State;

                if (RequiresDelayedActivation(definition, underlyingState, active))
                {
                    if (!_pendingActivations.ContainsKey(definition.Id))
                        StartPendingActivationLocked(definition);
                    _instances[definition.Id] = current with { LastValue = evt.Current.Value };
                    continue;
                }

                if (!active)
                    CancelPendingActivationLocked(definition.Id);

                var nextState = NextState(underlyingState, active, current.AcknowledgedAt is not null);
                var beginsNewActivation = active && IsNewActivationSource(underlyingState);
                if (isShelved)
                {
                    _shelvedUnderlyingStates[definition.Id] = nextState;
                    _instances[definition.Id] = current with
                    {
                        LastValue = evt.Current.Value,
                        ActivatedAt = beginsNewActivation ? _timeProvider.GetUtcNow() : current.ActivatedAt,
                        AcknowledgedAt = beginsNewActivation ? null : current.AcknowledgedAt,
                        AcknowledgedBy = beginsNewActivation ? null : current.AcknowledgedBy
                    };
                    continue;
                }

                if (nextState == current.State)
                {
                    _instances[definition.Id] = current with { LastValue = evt.Current.Value };
                    continue;
                }

                var now = _timeProvider.GetUtcNow();
                var next = current with
                {
                    State = nextState,
                    LastTransition = now,
                    LastValue = evt.Current.Value,
                    ActivatedAt = beginsNewActivation ? now : active && current.ActivatedAt is null ? now : current.ActivatedAt,
                    AcknowledgedAt = beginsNewActivation ? null : current.AcknowledgedAt,
                    AcknowledgedBy = beginsNewActivation ? null : current.AcknowledgedBy
                };
                _instances[definition.Id] = next;
                stateChanges.Add(new AlarmStateChanged(current, next, now));
            }
        }

        foreach (var stateChange in stateChanges)
            await _eventBus.PublishAsync(stateChange);
    }

    private bool RequiresDelayedActivation(AlarmDefinition definition, AlarmState current, bool active) =>
        active && definition.ActivationDelay is { } delay && delay > TimeSpan.Zero && IsNewActivationSource(current);

    private void StartPendingActivationLocked(AlarmDefinition definition)
    {
        if (definition.ActivationDelay is not { } delay || delay <= TimeSpan.Zero) return;
        var pending = new PendingActivation();
        _pendingActivations[definition.Id] = pending;
        _ = CompletePendingActivationAsync(definition.Id, pending, delay);
    }

    private async Task CompletePendingActivationAsync(Guid definitionId, PendingActivation pending, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, pending.Cancellation.Token).ConfigureAwait(false);
            AlarmStateChanged? stateChanged = null;
            lock (_sync)
            {
                if (!_pendingActivations.TryGetValue(definitionId, out var currentPending) ||
                    !ReferenceEquals(currentPending, pending))
                    return;
                _pendingActivations.Remove(definitionId);

                if (_disposed ||
                    !_definitions.TryGetValue(definitionId, out var definition) ||
                    !definition.Enabled ||
                    !_instances.TryGetValue(definitionId, out var current) ||
                    !_latestValues.TryGetValue(definitionId, out var latestValue) ||
                    !IsActive(definition, latestValue))
                    return;

                var isShelved = current.State == AlarmState.Shelved;
                var underlyingState = isShelved
                    ? _shelvedUnderlyingStates.GetValueOrDefault(definitionId, AlarmState.Normal)
                    : current.State;
                if (!IsNewActivationSource(underlyingState)) return;

                var now = _timeProvider.GetUtcNow();
                var nextState = NextState(underlyingState, active: true, current.AcknowledgedAt is not null);
                if (nextState == underlyingState) return;

                var next = current with
                {
                    State = isShelved ? AlarmState.Shelved : nextState,
                    LastTransition = isShelved ? current.LastTransition : now,
                    LastValue = latestValue.Value,
                    ActivatedAt = now,
                    AcknowledgedAt = null,
                    AcknowledgedBy = null
                };
                _instances[definitionId] = next;
                if (isShelved)
                    _shelvedUnderlyingStates[definitionId] = nextState;
                else
                    stateChanged = new AlarmStateChanged(current, next, now);
            }

            if (stateChanged is not null)
                await _eventBus.PublishAsync(stateChanged).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (pending.Cancellation.IsCancellationRequested)
        {
            // A normal return, definition update, removal, or engine shutdown canceled the delay.
        }
        catch (Exception exception)
        {
            Trace.TraceError("Delayed alarm activation failed for definition {0}: {1}", definitionId, exception);
        }
        finally
        {
            pending.Dispose();
        }
    }

    private void CancelPendingActivationLocked(Guid definitionId)
    {
        if (_pendingActivations.Remove(definitionId, out var pending))
            pending.Cancel();
    }

    private void CancelAllPendingActivationsLocked()
    {
        foreach (var pending in _pendingActivations.Values)
            pending.Cancel();
        _pendingActivations.Clear();
    }

    private static bool IsNewActivationSource(AlarmState state) =>
        state is AlarmState.Normal or AlarmState.Returned;

    private static AlarmState NextState(AlarmState current, bool active, bool acknowledged) =>
        active
            ? current == AlarmState.Acknowledged ? AlarmState.Acknowledged : AlarmState.Active
            : current is AlarmState.Active or AlarmState.Acknowledged || (current == AlarmState.Returned && !acknowledged)
                ? AlarmState.Returned
                : AlarmState.Normal;

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

    private static AlarmInstance NewNormal(AlarmDefinition definition, DateTimeOffset now) =>
        new(definition.Id, definition.Name, definition.TagId, definition.Type, definition.Priority,
            definition.Enabled ? AlarmState.Normal : AlarmState.Disabled, now, null,
            definition.Area, definition.Message);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            CancelAllPendingActivationsLocked();
        }

        _subscription.Dispose();
    }

    private sealed class PendingActivation : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();

        public CancellationTokenSource Cancellation => _cancellation;
        public void Cancel() => _cancellation.Cancel();
        public void Dispose() => _cancellation.Dispose();
    }
}
