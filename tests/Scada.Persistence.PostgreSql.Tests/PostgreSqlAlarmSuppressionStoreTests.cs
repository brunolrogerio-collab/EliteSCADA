using Scada.Core.Alarms;
using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlAlarmSuppressionStoreTests
{
    [Fact]
    public async Task Store_persists_project_snapshot_revision_expiry_and_release()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var projectKey = $"Integration.AlarmSuppression.{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var alarmId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        await using var store = new PostgreSqlAlarmSuppressionStore(connectionString);

        await store.SetAsync(new AlarmSuppressionRecord(
            projectKey,
            AlarmSuppressionTargetKind.Alarm,
            alarmId,
            AlarmSuppressionReasonCodes.Maintenance,
            "operator-1",
            now,
            now.AddMinutes(10)));
        await store.SetAsync(new AlarmSuppressionRecord(
            projectKey,
            AlarmSuppressionTargetKind.Location,
            locationId,
            AlarmSuppressionReasonCodes.Testing,
            "operator-2",
            now,
            null));

        var active = await store.ReadSnapshotAsync(projectKey, now.AddMinutes(1));
        Assert.Equal(2, active.Revision);
        Assert.Equal(2, active.Records.Count);

        var afterExpiry = await store.ReadSnapshotAsync(projectKey, now.AddMinutes(11));
        Assert.Equal(2, afterExpiry.Revision);
        Assert.Collection(afterExpiry.Records, record => Assert.Equal(locationId, record.TargetId));

        var released = await store.ReleaseAsync(projectKey, AlarmSuppressionTargetKind.Location, locationId);
        Assert.True(released.Changed);
        var empty = await store.ReadSnapshotAsync(projectKey, now.AddMinutes(12));
        Assert.Equal(3, empty.Revision);
        Assert.Empty(empty.Records);
    }

    [Fact]
    public async Task Store_serializes_concurrent_targets_and_rejects_stale_peer_snapshot()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var projectKey = $"Integration.AlarmSuppressionConcurrency.{Guid.NewGuid():N}";
        await using var first = new PostgreSqlAlarmSuppressionStore(connectionString);
        await using var second = new PostgreSqlAlarmSuppressionStore(connectionString);
        var now = DateTimeOffset.UtcNow;
        var targets = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToArray();

        await Task.WhenAll(targets.Select((targetId, index) =>
            (index % 2 == 0 ? first : second).SetAsync(new AlarmSuppressionRecord(
                projectKey,
                AlarmSuppressionTargetKind.Alarm,
                targetId,
                AlarmSuppressionReasonCodes.Maintenance,
                $"operator-{index}",
                now,
                null))));

        var concurrent = await first.ReadSnapshotAsync(projectKey, now);
        Assert.Equal(targets.Length, concurrent.Revision);
        Assert.Equal(targets.Length, concurrent.Records.Count);

        var replicaState = targets.Take(3).Select((targetId, index) => new AlarmSuppressionRecord(
            projectKey,
            AlarmSuppressionTargetKind.Alarm,
            targetId,
            AlarmSuppressionReasonCodes.Testing,
            $"peer-{index}",
            now,
            null,
            Version: index + 1)).ToArray();
        await second.ReplaceProjectSnapshotAsync(projectKey, 20, replicaState);

        var restored = await first.ReadSnapshotAsync(projectKey, now);
        Assert.Equal(20, restored.Revision);
        Assert.Equal(3, restored.Records.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            first.ReplaceProjectSnapshotAsync(projectKey, 19, replicaState));

        var unchanged = await second.ReadSnapshotAsync(projectKey, now);
        Assert.Equal(20, unchanged.Revision);
        Assert.Equal(3, unchanged.Records.Count);
    }
}
