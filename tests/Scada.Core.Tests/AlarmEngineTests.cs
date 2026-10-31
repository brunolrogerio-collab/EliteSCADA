using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Xunit;

namespace Scada.Core.Tests;

public sealed class AlarmEngineTests
{
    private static readonly DateTimeOffset TestStart = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task High_alarm_activates_and_can_be_acknowledged()
    {
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create("High Pressure", tag.Id, AlarmType.High, AlarmPriority.High, 10));
        Assert.True(definition.RequiresAcknowledgement);

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11.2));
        Assert.Equal(AlarmState.Active, alarms.Snapshot().Single().State);

        Assert.True(await alarms.AcknowledgeAsync(definition.Id, "operator"));
        Assert.Equal(AlarmState.Acknowledged, alarms.Snapshot().Single().State);
    }

    [Fact]
    public async Task High_alarm_activation_delay_waits_for_the_full_sustained_interval()
    {
        var clock = new ManualTimeProvider(TestStart);
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        var transition = new TaskCompletionSource<AlarmStateChanged>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = bus.Subscribe<AlarmStateChanged>(change =>
        {
            transition.TrySetResult(change);
            return ValueTask.CompletedTask;
        });
        using var alarms = new InMemoryAlarmEngine(bus, timeProvider: clock);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create(
            "High Pressure", tag.Id, AlarmType.High, AlarmPriority.High,
            setpoint: 10, activationDelay: TimeSpan.FromSeconds(10)));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11.2));
        Assert.Equal(AlarmState.Normal, Assert.Single(alarms.Snapshot()).State);

        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(AlarmState.Normal, Assert.Single(alarms.Snapshot()).State);

        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitForStateAsync(alarms, definition.Id, AlarmState.Active);
        var published = await transition.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var active = Assert.Single(alarms.Snapshot());
        Assert.Equal(TestStart.AddSeconds(10), active.ActivatedAt);
        Assert.Equal(TestStart.AddSeconds(10), active.LastTransition);
        Assert.Equal(AlarmState.Normal, published.Previous.State);
        Assert.Equal(AlarmState.Active, published.Current.State);
        Assert.Equal(TestStart.AddSeconds(10), published.OccurredAt);
    }

    [Fact]
    public async Task High_alarm_short_condition_cancels_delay_and_next_activation_starts_a_new_interval()
    {
        var clock = new ManualTimeProvider(TestStart);
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus, timeProvider: clock);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create(
            "High Pressure", tag.Id, AlarmType.High, AlarmPriority.High,
            setpoint: 10, activationDelay: TimeSpan.FromSeconds(5)));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        clock.Advance(TimeSpan.FromSeconds(4));
        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 9));
        clock.Advance(TimeSpan.FromSeconds(10));
        await WaitForNoPendingWorkAsync();
        Assert.Equal(AlarmState.Normal, Assert.Single(alarms.Snapshot()).State);

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 12));
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(AlarmState.Normal, Assert.Single(alarms.Snapshot()).State);
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitForStateAsync(alarms, definition.Id, AlarmState.Active);
        Assert.Equal(TestStart.AddSeconds(19), Assert.Single(alarms.Snapshot()).ActivatedAt);
    }

    [Fact]
    public async Task Activation_delays_are_independent_for_alarms_on_the_same_tag()
    {
        var clock = new ManualTimeProvider(TestStart);
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus, timeProvider: clock);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var fast = alarms.Register(AlarmDefinition.Create(
            "High Pressure", tag.Id, AlarmType.High, AlarmPriority.High,
            setpoint: 10, activationDelay: TimeSpan.FromSeconds(5)));
        var slow = alarms.Register(AlarmDefinition.Create(
            "Very High Pressure", tag.Id, AlarmType.HighHigh, AlarmPriority.Critical,
            setpoint: 10, activationDelay: TimeSpan.FromSeconds(10)));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForStateAsync(alarms, fast.Id, AlarmState.Active);
        Assert.Equal(AlarmState.Normal, alarms.Snapshot().Single(x => x.DefinitionId == slow.Id).State);

        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForStateAsync(alarms, slow.Id, AlarmState.Active);
    }

    [Fact]
    public async Task Bad_quality_cancels_numeric_delay_and_starts_communication_alarm_delay()
    {
        var clock = new ManualTimeProvider(TestStart);
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus, timeProvider: clock);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var numeric = alarms.Register(AlarmDefinition.Create(
            "High Pressure", tag.Id, AlarmType.High, AlarmPriority.High,
            setpoint: 10, activationDelay: TimeSpan.FromSeconds(5)));
        var communication = alarms.Register(AlarmDefinition.Create(
            "Pressure Communication", tag.Id, AlarmType.Communication, AlarmPriority.Critical,
            activationDelay: TimeSpan.FromSeconds(5)));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        clock.Advance(TimeSpan.FromSeconds(2));
        await cache.UpdateAsync(tag, new TagValue(tag.Id, null, TestStart.AddSeconds(2), TagQuality.Bad));
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForStateAsync(alarms, communication.Id, AlarmState.Active);
        Assert.Equal(AlarmState.Normal, alarms.Snapshot().Single(x => x.DefinitionId == numeric.Id).State);

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForStateAsync(alarms, numeric.Id, AlarmState.Active);
    }

    [Fact]
    public async Task Shelved_alarm_keeps_tracking_delayed_underlying_activation()
    {
        var clock = new ManualTimeProvider(TestStart);
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus, timeProvider: clock);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create(
            "High Pressure", tag.Id, AlarmType.High, AlarmPriority.High,
            setpoint: 10, activationDelay: TimeSpan.FromSeconds(5)));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        Assert.True(await alarms.ShelveAsync(definition.Id, "operator"));
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForNoPendingWorkAsync();

        Assert.Equal(AlarmState.Shelved, Assert.Single(alarms.Snapshot()).State);
        Assert.True(await alarms.UnshelveAsync(definition.Id, "operator"));
        var restored = Assert.Single(alarms.Snapshot());
        Assert.Equal(AlarmState.Active, restored.State);
        Assert.Equal(TestStart.AddSeconds(5), restored.ActivatedAt);
    }

    [Fact]
    public async Task Disabling_alarm_cancels_pending_activation()
    {
        var clock = new ManualTimeProvider(TestStart);
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus, timeProvider: clock);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = AlarmDefinition.Create(
            "High Pressure", tag.Id, AlarmType.High, AlarmPriority.High,
            setpoint: 10, activationDelay: TimeSpan.FromSeconds(5));
        alarms.Register(definition);

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        alarms.Register(definition with { Enabled = false });
        clock.Advance(TimeSpan.FromSeconds(10));
        await WaitForNoPendingWorkAsync();

        Assert.Equal(AlarmState.Disabled, Assert.Single(alarms.Snapshot()).State);
    }

    [Fact]
    public async Task Replacing_alarm_definition_cancels_pending_activation()
    {
        var clock = new ManualTimeProvider(TestStart);
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus, timeProvider: clock);
        var originalTag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var replacementTag = TagDefinition.Create("Temperature", "Demo.Temperature", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create(
            "High Pressure", originalTag.Id, AlarmType.High, AlarmPriority.High,
            setpoint: 10, activationDelay: TimeSpan.FromSeconds(5)));

        await cache.UpdateAsync(originalTag, TagValue.Good(originalTag.Id, 11));
        alarms.Register(definition with { TagId = replacementTag.Id });
        clock.Advance(TimeSpan.FromSeconds(10));
        await WaitForNoPendingWorkAsync();

        var instance = Assert.Single(alarms.Snapshot());
        Assert.Equal(replacementTag.Id, instance.TagId);
        Assert.Equal(AlarmState.Normal, instance.State);
    }

    [Fact]
    public async Task Removing_alarm_definition_cancels_pending_activation()
    {
        var clock = new ManualTimeProvider(TestStart);
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus, timeProvider: clock);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create(
            "High Pressure", tag.Id, AlarmType.High, AlarmPriority.High,
            setpoint: 10, activationDelay: TimeSpan.FromSeconds(5)));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        Assert.True(alarms.Remove(definition.Id));
        clock.Advance(TimeSpan.FromSeconds(10));
        await WaitForNoPendingWorkAsync();

        Assert.Empty(alarms.Snapshot());
    }

    [Fact]
    public async Task Clearing_alarm_definitions_cancels_pending_activation()
    {
        var clock = new ManualTimeProvider(TestStart);
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus, timeProvider: clock);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        alarms.Register(AlarmDefinition.Create(
            "High Pressure", tag.Id, AlarmType.High, AlarmPriority.High,
            setpoint: 10, activationDelay: TimeSpan.FromSeconds(5)));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        alarms.Clear();
        clock.Advance(TimeSpan.FromSeconds(10));
        await WaitForNoPendingWorkAsync();

        Assert.Empty(alarms.Definitions());
        Assert.Empty(alarms.Snapshot());
    }

    [Fact]
    public async Task Disposing_alarm_engine_cancels_pending_activation()
    {
        var clock = new ManualTimeProvider(TestStart);
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        var alarms = new InMemoryAlarmEngine(bus, timeProvider: clock);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        alarms.Register(AlarmDefinition.Create(
            "High Pressure", tag.Id, AlarmType.High, AlarmPriority.High,
            setpoint: 10, activationDelay: TimeSpan.FromSeconds(5)));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        alarms.Dispose();
        clock.Advance(TimeSpan.FromSeconds(10));
        await WaitForNoPendingWorkAsync();

        Assert.Equal(AlarmState.Normal, Assert.Single(alarms.Snapshot()).State);
    }

    [Fact]
    public async Task Unacknowledged_alarm_remains_visible_and_acknowledgeable_after_returning_to_normal()
    {
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create("High Pressure", tag.Id, AlarmType.High, AlarmPriority.High, 10));
        Assert.True(definition.RequiresAcknowledgement);

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11.2));
        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 8.0));
        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 7.5));

        var returnedUnacknowledged = Assert.Single(alarms.Snapshot(activeOnly: true));
        Assert.Equal(AlarmState.Returned, returnedUnacknowledged.State);
        Assert.Null(returnedUnacknowledged.AcknowledgedAt);
        Assert.True(await alarms.AcknowledgeAsync(definition.Id, "operator"));
        Assert.Empty(alarms.Snapshot(activeOnly: true));
        Assert.Equal("operator", Assert.Single(alarms.Snapshot()).AcknowledgedBy);

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 7.0));
        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 12.0));
        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 8.0));
        var nextActivationReturned = Assert.Single(alarms.Snapshot(activeOnly: true));
        Assert.Equal(AlarmState.Returned, nextActivationReturned.State);
        Assert.Null(nextActivationReturned.AcknowledgedAt);
    }

    [Fact]
    public async Task Returned_alarm_without_acknowledgement_requirement_leaves_active_list_and_reactivation_starts_new_cycle()
    {
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create(
            "High Pressure", tag.Id, AlarmType.High, AlarmPriority.High, 10,
            requiresAcknowledgement: false));

        Assert.False(definition.RequiresAcknowledgement);

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        Assert.Equal(AlarmState.Active, Assert.Single(alarms.Snapshot(activeOnly: true)).State);

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 9));
        var returnedWithoutAck = Assert.Single(alarms.Snapshot());
        Assert.Equal(AlarmState.Returned, returnedWithoutAck.State);
        Assert.Null(returnedWithoutAck.AcknowledgedAt);
        Assert.Empty(alarms.Snapshot(activeOnly: true));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        var reactivated = Assert.Single(alarms.Snapshot(activeOnly: true));
        Assert.Equal(AlarmState.Active, reactivated.State);
        Assert.Null(reactivated.AcknowledgedAt);
        Assert.Null(reactivated.AcknowledgedBy);

        Assert.True(await alarms.AcknowledgeAsync(definition.Id, "operator"));
        Assert.Equal(AlarmState.Acknowledged, Assert.Single(alarms.Snapshot(activeOnly: true)).State);
        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 9));
        var acknowledgedReturn = Assert.Single(alarms.Snapshot());
        Assert.Equal(AlarmState.Returned, acknowledgedReturn.State);
        Assert.Equal("operator", acknowledgedReturn.AcknowledgedBy);
        Assert.Empty(alarms.Snapshot(activeOnly: true));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        var newActivation = Assert.Single(alarms.Snapshot(activeOnly: true));
        Assert.Equal(AlarmState.Active, newActivation.State);
        Assert.Null(newActivation.AcknowledgedAt);
        Assert.Null(newActivation.AcknowledgedBy);
        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 9));

        var unacknowledgedReturn = Assert.Single(alarms.Snapshot());
        Assert.Equal(AlarmState.Returned, unacknowledgedReturn.State);
        Assert.Null(unacknowledgedReturn.AcknowledgedAt);
        Assert.Empty(alarms.Snapshot(activeOnly: true));
        Assert.True(await alarms.AcknowledgeAsync(definition.Id, "operator-2"));
        Assert.Equal("operator-2", Assert.Single(alarms.Snapshot()).AcknowledgedBy);
        Assert.Empty(alarms.Snapshot(activeOnly: true));
    }

    [Fact]
    public async Task Shelved_return_without_acknowledgement_requirement_stays_out_of_active_list_after_unshelving()
    {
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create(
            "High Pressure", tag.Id, AlarmType.High, AlarmPriority.High, 10,
            requiresAcknowledgement: false));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11));
        Assert.True(await alarms.ShelveAsync(definition.Id, "operator"));
        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 9));

        Assert.Equal(AlarmState.Shelved, Assert.Single(alarms.Snapshot()).State);
        Assert.Empty(alarms.Snapshot(activeOnly: true));
        Assert.True(await alarms.UnshelveAsync(definition.Id, "operator"));

        var returned = Assert.Single(alarms.Snapshot());
        Assert.Equal(AlarmState.Returned, returned.State);
        Assert.Null(returned.AcknowledgedAt);
        Assert.Empty(alarms.Snapshot(activeOnly: true));
    }

    [Fact]
    public async Task Shelved_alarm_stays_suppressed_and_unshelve_restores_latest_underlying_state()
    {
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create("High Pressure", tag.Id, AlarmType.High, AlarmPriority.High, 10));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 8.0));
        Assert.True(await alarms.ShelveAsync(definition.Id, "developer"));

        var shelved = alarms.Snapshot().Single();
        Assert.Equal(AlarmState.Shelved, shelved.State);
        Assert.Equal("developer", shelved.ShelvedBy);
        Assert.NotNull(shelved.ShelvedAt);
        Assert.Empty(alarms.Snapshot(activeOnly: true));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 12.0));
        Assert.Equal(AlarmState.Shelved, alarms.Snapshot().Single().State);

        Assert.True(await alarms.UnshelveAsync(definition.Id, "developer"));
        var unshelved = alarms.Snapshot().Single();
        Assert.Equal(AlarmState.Active, unshelved.State);
        Assert.Null(unshelved.ShelvedAt);
        Assert.Null(unshelved.ShelvedBy);
    }

    [Fact]
    public async Task Alarm_that_disallows_shelving_cannot_be_shelved()
    {
        IScadaEventBus bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create(
            "High Pressure",
            tag.Id,
            AlarmType.High,
            AlarmPriority.High,
            setpoint: 10,
            shelvingAllowed: false));

        Assert.False(await alarms.ShelveAsync(definition.Id, "developer"));
        Assert.Equal(AlarmState.Normal, alarms.Snapshot().Single().State);
    }

    private static async Task WaitForStateAsync(IAlarmEngine alarms, Guid definitionId, AlarmState state)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (alarms.Snapshot().Single(x => x.DefinitionId == definitionId).State == state)
                return;
            await Task.Delay(1);
        }

        Assert.Equal(state, alarms.Snapshot().Single(x => x.DefinitionId == definitionId).State);
    }

    private static async Task WaitForNoPendingWorkAsync()
    {
        // Give cancellation/completion continuations a chance to run without moving the test clock.
        for (var attempt = 0; attempt < 50; attempt++)
            await Task.Delay(1);
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private readonly object _sync = new();
        private readonly List<ManualTimer> _timers = [];
        private DateTimeOffset _now = start;
        private long _timestamp;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_sync) return _now;
        }

        public override long GetTimestamp()
        {
            lock (_sync) return _timestamp;
        }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            lock (_sync) _timers.Add(timer);
            Change(timer, dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
            List<(TimerCallback Callback, object? State)> callbacks = [];
            lock (_sync)
            {
                _now += duration;
                _timestamp += duration.Ticks;
                foreach (var timer in _timers.ToArray())
                {
                    if (!timer.IsScheduled || timer.DueTimestamp > _timestamp) continue;
                    callbacks.Add((timer.Callback, timer.State));
                    if (timer.PeriodTicks > 0)
                    {
                        do { timer.DueTimestamp += timer.PeriodTicks; }
                        while (timer.DueTimestamp <= _timestamp);
                    }
                    else
                    {
                        timer.IsScheduled = false;
                    }
                }
            }

            foreach (var (callback, state) in callbacks)
                callback(state);
        }

        private bool Change(ManualTimer timer, TimeSpan dueTime, TimeSpan period)
        {
            lock (_sync)
            {
                if (timer.IsDisposed) return false;
                if (dueTime < TimeSpan.Zero && dueTime != Timeout.InfiniteTimeSpan)
                    throw new ArgumentOutOfRangeException(nameof(dueTime));
                if (period < TimeSpan.Zero && period != Timeout.InfiniteTimeSpan)
                    throw new ArgumentOutOfRangeException(nameof(period));

                timer.IsScheduled = dueTime != Timeout.InfiniteTimeSpan;
                timer.DueTimestamp = timer.IsScheduled ? _timestamp + dueTime.Ticks : 0;
                timer.PeriodTicks = period > TimeSpan.Zero ? period.Ticks : 0;
                return true;
            }
        }

        private void Dispose(ManualTimer timer)
        {
            lock (_sync)
            {
                timer.IsDisposed = true;
                timer.IsScheduled = false;
                _timers.Remove(timer);
            }
        }

        private sealed class ManualTimer(
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state) : ITimer
        {
            public TimerCallback Callback { get; } = callback;
            public object? State { get; } = state;
            public long DueTimestamp { get; set; }
            public long PeriodTicks { get; set; }
            public bool IsScheduled { get; set; }
            public bool IsDisposed { get; set; }

            public bool Change(TimeSpan dueTime, TimeSpan period) => owner.Change(this, dueTime, period);
            public void Dispose() => owner.Dispose(this);
            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
