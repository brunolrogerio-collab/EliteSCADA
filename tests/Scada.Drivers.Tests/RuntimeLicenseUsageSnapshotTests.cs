using Scada.Persistence.PostgreSql;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

[Collection("RuntimeSessionPostgreSql")]
public sealed class RuntimeLicenseUsageSnapshotTests
{
    [Fact]
    public async Task MemorySnapshot_DoesNotObservePartialAdmissionDuringConcurrentReadersAndWriters()
    {
        var now = DateTimeOffset.UtcNow;
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => now);
        var runtime = new RuntimeSessionRuntimeIdentity("engineering", "concurrent", 1, now);
        var writers = Enumerable.Range(0, 40).Select(i => Task.Run(async () =>
            await store.AdmitAsync(new("user-" + i, "web", i % 2 == 0 ? "interactive" : "viewer", runtime, TimeSpan.FromMinutes(1)))));
        var readers = Enumerable.Range(0, 40).Select(_ => Task.Run(async () =>
        {
            var observed = await store.GetUsageSnapshotAsync(runtime);
            Assert.NotNull(observed);
            Assert.InRange(observed.Interactive, 0, 20);
            Assert.InRange(observed.ViewOnly, 0, 20);
        }));
        await Task.WhenAll(writers.Cast<Task>().Concat(readers));
        var final = await store.GetUsageSnapshotAsync(runtime);
        Assert.NotNull(final);
        Assert.Equal((20, 20), (final.Interactive, final.ViewOnly));
    }

    [Fact]
    public async Task MemorySnapshot_CountsLogicalClassesAndExpiry_AndReturnsUnavailableDuringTransition()
    {
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => now);
        var runtime = new RuntimeSessionRuntimeIdentity("engineering", "usage-project", 1, now);
        var interactive = new RuntimeSessionLeaseAdmission("operator", "web", "interactive", runtime, TimeSpan.FromMinutes(2));
        var viewer = new RuntimeSessionLeaseAdmission("operator", "elitego", "viewer", runtime, TimeSpan.FromMinutes(1));
        var first = await store.AdmitAsync(interactive);
        Assert.Equal(first.SessionId, (await store.AdmitAsync(interactive)).SessionId);
        await store.AdmitAsync(viewer);
        await store.AdmitAsync(new("other", "other", "viewer", runtime with { ProjectKey = "other" }, TimeSpan.FromMinutes(5)));
        var snapshot = await store.GetUsageSnapshotAsync(runtime);
        Assert.NotNull(snapshot);
        Assert.Equal((1, 1, now), (snapshot.Interactive, snapshot.ViewOnly, snapshot.ObservedAtUtc));
        now += TimeSpan.FromMinutes(1);
        snapshot = await store.GetUsageSnapshotAsync(runtime);
        Assert.NotNull(snapshot);
        Assert.Equal((1, 0), (snapshot.Interactive, snapshot.ViewOnly));
        var transition = await store.BeginAuthorityTransitionAsync("usage-test", now);
        Assert.Null(await store.GetUsageSnapshotAsync(runtime));
        Assert.True(await store.AbortAuthorityTransitionAsync(transition.TransitionId, transition.BaseAuthorityRevision));
        Assert.NotNull(await store.GetUsageSnapshotAsync(runtime));
    }

    [Fact]
    public async Task PostgreSqlSnapshot_UsesCanonicalRuntimeIdentityAndCountsLogicalSeats()
    {
        var connection = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await using var store = new PostgreSqlRuntimeSessionLeaseStore(connection);
        await store.InitializeAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var runtime = new RuntimeSessionRuntimeIdentity("engineering", "usage-" + suffix, 1, DateTimeOffset.UtcNow);
        var first = await store.AdmitAsync(new("usage-" + suffix, "web", "interactive", runtime, TimeSpan.FromMinutes(1)));
        var same = await store.AdmitAsync(new("usage-" + suffix, "web", "interactive", runtime, TimeSpan.FromMinutes(1)));
        var viewer = await store.AdmitAsync(new("usage-" + suffix, "elitego", "viewer", runtime, TimeSpan.FromMinutes(1)));
        try
        {
            Assert.Equal(first.SessionId, same.SessionId);
            var snapshot = await store.GetUsageSnapshotAsync(runtime);
            Assert.NotNull(snapshot);
            Assert.Equal((1, 1), (snapshot.Interactive, snapshot.ViewOnly));
            var other = await store.GetUsageSnapshotAsync(runtime with { ProjectKey = "usage-other-" + suffix });
            Assert.NotNull(other);
            Assert.Equal((0, 0), (other.Interactive, other.ViewOnly));
            await store.TerminateAsync(viewer.SessionId, viewer.SubjectId, viewer.ClientInstanceId, runtime, expectedGeneration: viewer.Generation);
            snapshot = await store.GetUsageSnapshotAsync(runtime);
            Assert.NotNull(snapshot);
            Assert.Equal((1, 0), (snapshot.Interactive, snapshot.ViewOnly));
            var transition = await store.BeginAuthorityTransitionAsync("usage-test", DateTimeOffset.UtcNow);
            try
            {
                Assert.Null(await store.GetUsageSnapshotAsync(runtime));
            }
            finally
            {
                Assert.True(await store.AbortAuthorityTransitionAsync(transition.TransitionId, transition.BaseAuthorityRevision));
            }
            Assert.NotNull(await store.GetUsageSnapshotAsync(runtime));
        }
        finally
        {
            await store.TerminateAsync(first.SessionId, first.SubjectId, first.ClientInstanceId, runtime);
            await using var data = Npgsql.NpgsqlDataSource.Create(connection);
            await using var command = data.CreateCommand("DELETE FROM elitescada.runtime_session_leases WHERE subject_id = @subject;");
            command.Parameters.AddWithValue("subject", "usage-" + suffix);
            await command.ExecuteNonQueryAsync();
        }
    }
}
