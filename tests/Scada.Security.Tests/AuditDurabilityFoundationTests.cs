using System.Collections.Concurrent;
using Scada.Security.Audit;

namespace Scada.Security.Tests;

public sealed class AuditDurabilityFoundationTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 8, 26, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InMemoryStore_QueriesCombinedFiltersAndKeysetPagesDeterministically()
    {
        var store = new InMemoryAuditSink(new AuditQueryPolicy(MaximumPageSize: 10));
        var ids = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000003")
        };

        foreach (var id in ids)
        {
            await store.WriteAsync(new AuditEvent(
                id,
                FixedTime,
                "operator-1",
                "Operator",
                AuditActions.CommandExecute,
                AuditOutcome.Succeeded,
                "command",
                "pump.start",
                new Dictionary<string, string> { ["kind"] = "test" },
                "corr-1",
                "Area1"));
        }

        await store.WriteAsync(new AuditEvent(
            Guid.Parse("00000000-0000-0000-0000-000000000004"),
            FixedTime.AddMinutes(-1),
            "other",
            null,
            AuditActions.TagWrite,
            AuditOutcome.Denied,
            "tag",
            "Plant.P01.Setpoint",
            CorrelationId: "corr-other",
            Area: "Area2"));

        var firstPage = await store.QueryPageAsync(new AuditQuery(
            PageSize: 2,
            FromUtc: FixedTime.AddSeconds(-1),
            ToUtc: FixedTime.AddSeconds(1),
            SubjectId: "operator-1",
            Action: AuditActions.CommandExecute,
            Outcome: AuditOutcome.Succeeded,
            TargetKind: "command",
            TargetId: "pump.start",
            Area: "Area1",
            CorrelationId: "corr-1"));

        Assert.Equal(new[] { ids[2], ids[1] }, firstPage.Events.Select(x => x.Id));
        Assert.NotNull(firstPage.NextCursor);

        var secondPage = await store.QueryPageAsync(new AuditQuery(
            PageSize: 2,
            SubjectId: "operator-1",
            Action: AuditActions.CommandExecute,
            Outcome: AuditOutcome.Succeeded,
            TargetKind: "command",
            TargetId: "pump.start",
            Area: "Area1",
            CorrelationId: "corr-1",
            After: firstPage.NextCursor));

        var remaining = Assert.Single(secondPage.Events);
        Assert.Equal(ids[0], remaining.Id);
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task InMemoryStore_EnforcesConfiguredMaximumPageSize()
    {
        var store = new InMemoryAuditSink(new AuditQueryPolicy(MaximumPageSize: 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.QueryPageAsync(new AuditQuery(PageSize: 3)));
    }

    [Fact]
    public async Task InMemoryStore_SanitizesSensitiveMetadataAtStorageBoundary()
    {
        var store = new InMemoryAuditSink();
        await store.WriteAsync(new AuditEvent(
            Guid.NewGuid(),
            FixedTime,
            "operator",
            "Operator",
            AuditActions.AuthenticationLogin,
            AuditOutcome.Failed,
            "identity",
            "local",
            new Dictionary<string, string>
            {
                ["password"] = "never-store-this",
                ["jwt"] = "aaaaaaaa.bbbbbbbb.cccccccc",
                ["safe"] = "diagnostic",
                ["authorizationValue"] = "ignored",
                ["note"] = "Bearer abcdefghijklmnopqrstuvwxyz"
            }));

        var stored = Assert.Single(store.Snapshot());
        Assert.NotNull(stored.Details);
        Assert.False(stored.Details!.ContainsKey("password"));
        Assert.False(stored.Details.ContainsKey("jwt"));
        Assert.False(stored.Details.ContainsKey("authorizationValue"));
        Assert.Equal("diagnostic", stored.Details["safe"]);
        Assert.Equal("[REDACTED]", stored.Details["note"]);
    }

    [Fact]
    public async Task Retention_RemovesOnlyEventsStrictlyOlderThanCutoff()
    {
        var store = new InMemoryAuditSink();
        var cutoff = FixedTime.AddDays(-30);
        var before = EventAt(cutoff.AddTicks(-1), "before");
        var exact = EventAt(cutoff, "exact");
        var after = EventAt(cutoff.AddTicks(1), "after");
        await store.WriteAsync(before);
        await store.WriteAsync(exact);
        await store.WriteAsync(after);

        var retention = new AuditRetentionCoordinator(
            store,
            new AuditRetentionPolicy(
                Enabled: true,
                MaximumAge: TimeSpan.FromDays(30),
                BatchSize: 10,
                MaximumBatchesPerRun: 2));

        var result = await retention.RunOnceAsync(FixedTime);

        Assert.True(result.Executed);
        Assert.Equal(cutoff, result.CutoffUtc);
        Assert.Equal(1, result.DeletedCount);
        Assert.DoesNotContain(store.Snapshot(), x => x.Id == before.Id);
        Assert.Contains(store.Snapshot(), x => x.Id == exact.Id);
        Assert.Contains(store.Snapshot(), x => x.Id == after.Id);
    }

    [Fact]
    public async Task Retention_DisabledOrIndefiniteDoesNotDeleteEvents()
    {
        var store = new InMemoryAuditSink();
        await store.WriteAsync(EventAt(FixedTime.AddYears(-10), "old"));

        var disabled = new AuditRetentionCoordinator(
            store,
            new AuditRetentionPolicy(Enabled: false, MaximumAge: TimeSpan.FromDays(30)));
        var disabledResult = await disabled.RunOnceAsync(FixedTime);
        Assert.False(disabledResult.Executed);
        Assert.Single(store.Snapshot());

        var indefinite = new AuditRetentionCoordinator(
            store,
            new AuditRetentionPolicy(Enabled: true, MaximumAge: null));
        var indefiniteResult = await indefinite.RunOnceAsync(FixedTime);
        Assert.False(indefiniteResult.Executed);
        Assert.Single(store.Snapshot());
    }

    [Fact]
    public async Task Retention_IsBoundedByBatchAndMaximumBatchesPerRun()
    {
        var store = new InMemoryAuditSink();
        for (var index = 0; index < 5; index++)
            await store.WriteAsync(EventAt(FixedTime.AddYears(-2).AddSeconds(index), $"old-{index}"));

        var retention = new AuditRetentionCoordinator(
            store,
            new AuditRetentionPolicy(
                Enabled: true,
                MaximumAge: TimeSpan.FromDays(365),
                BatchSize: 2,
                MaximumBatchesPerRun: 2));

        var first = await retention.RunOnceAsync(FixedTime);
        Assert.Equal(4, first.DeletedCount);
        Assert.Equal(2, first.BatchCount);
        Assert.True(first.BacklogMayRemain);
        Assert.Single(store.Snapshot());

        var second = await retention.RunOnceAsync(FixedTime);
        Assert.Equal(1, second.DeletedCount);
        Assert.False(second.BacklogMayRemain);
        Assert.Empty(store.Snapshot());
    }

    [Fact]
    public async Task Store_HandlesConcurrentAppendsWithoutLosingEvents()
    {
        var store = new InMemoryAuditSink();
        var writes = Enumerable.Range(0, 200)
            .Select(index => store.WriteAsync(EventAt(FixedTime, $"event-{index}")).AsTask());

        await Task.WhenAll(writes);

        Assert.Equal(200, store.Snapshot().Count);
        Assert.Equal(200, store.Snapshot().Select(x => x.Id).Distinct().Count());
        Assert.Equal(200, store.GetHealthSnapshot().PersistedCount);
    }

    [Fact]
    public async Task BufferedSink_RetriesTemporaryFailureAndReportsHealth()
    {
        var inner = new RecoveringAuditSink(failuresBeforeSuccess: 2);
        await using var buffer = new BufferedAuditSink(
            inner,
            new AuditBufferPolicy(
                Capacity: 4,
                RetryDelay: TimeSpan.FromMilliseconds(5),
                ShutdownFlushTimeout: TimeSpan.FromSeconds(1)));

        await buffer.WriteAsync(EventAt(FixedTime, "retry"));
        await WaitForAsync(() => inner.Events.Count == 1, TimeSpan.FromSeconds(2));

        var health = buffer.GetHealthSnapshot();
        Assert.Equal(1, health.SuccessfullyForwardedCount);
        Assert.True(health.ForwardFailureCount >= 2);
        Assert.Equal(0, health.RejectedCount);
        Assert.NotNull(health.LastForwardedAtUtc);
        Assert.NotNull(health.LastFailureAtUtc);
    }

    [Fact]
    public async Task BufferedSink_RejectsOverflowExplicitlyInsteadOfSilentlyDropping()
    {
        var inner = new BlockingAuditSink();
        await using var buffer = new BufferedAuditSink(
            inner,
            new AuditBufferPolicy(
                Capacity: 1,
                RetryDelay: TimeSpan.FromMilliseconds(5),
                ShutdownFlushTimeout: TimeSpan.FromSeconds(1)));

        await buffer.WriteAsync(EventAt(FixedTime, "first"));
        await inner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await buffer.WriteAsync(EventAt(FixedTime, "second"));

        await Assert.ThrowsAsync<AuditBufferFullException>(() =>
            buffer.WriteAsync(EventAt(FixedTime, "third")).AsTask());

        inner.Release.TrySetResult(true);
        await WaitForAsync(
            () => buffer.GetHealthSnapshot().SuccessfullyForwardedCount == 2,
            TimeSpan.FromSeconds(5));

        Assert.Equal(1, buffer.GetHealthSnapshot().RejectedCount);
    }

    [Fact]
    public void Policies_RejectAbsurdConfiguration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuditQueryPolicy(0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuditRetentionPolicy(BatchSize: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuditRetentionPolicy(Interval: TimeSpan.Zero).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuditBufferPolicy(Capacity: 0).Validate());
    }

    private static AuditEvent EventAt(DateTimeOffset timestamp, string targetId) => new(
        Guid.NewGuid(),
        timestamp,
        "test-subject",
        null,
        AuditActions.AuditRead,
        AuditOutcome.Succeeded,
        "test",
        targetId);

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException("Timed out waiting for audit test condition.");
            await Task.Delay(10);
        }
    }

    private sealed class RecoveringAuditSink(int failuresBeforeSuccess) : IAuditSink
    {
        private int _remainingFailures = failuresBeforeSuccess;
        public ConcurrentQueue<AuditEvent> Events { get; } = new();

        public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Decrement(ref _remainingFailures) >= 0)
                throw new InvalidOperationException("temporary audit storage outage");

            Events.Enqueue(auditEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingAuditSink : IAuditSink
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
        }
    }
}
