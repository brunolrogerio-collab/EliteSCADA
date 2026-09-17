using Npgsql;
using Scada.Persistence.PostgreSql;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

[Collection("RuntimeSessionPostgreSql")]
public sealed class PostgreSqlRuntimeSessionLeaseStoreTests
{
    [Fact]
    public async Task PostgreSqlLeaseStore_ConvergesLogicalAdmissionAcrossInstances_AndUsesGenerationCas()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES") ??
            Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var subject = $"fnd03-session-{Guid.NewGuid():N}";
        var client = "elitego-logical-client";
        var runtime = Runtime("project-a", 7);
        await using var first = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await using var second = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await first.InitializeAsync();
        await second.InitializeAsync();

        try
        {
            var admission = Admission(subject, client, runtime, TimeSpan.FromMinutes(1));
            var concurrent = await Task.WhenAll(
                first.AdmitAsync(admission),
                second.AdmitAsync(admission));
            var lease = Assert.Single(concurrent.DistinctBy(candidate => candidate.SessionId));
            Assert.Equal(1, lease.Generation);

            var renewed = await first.HeartbeatAsync(
                lease.SessionId,
                subject,
                client,
                runtime,
                expectedGeneration: lease.Generation);
            Assert.True(renewed.IsValid);
            Assert.Equal(2, renewed.Lease!.Generation);

            var staleTerminate = await second.TerminateAsync(
                lease.SessionId,
                subject,
                client,
                runtime,
                expectedGeneration: lease.Generation);
            Assert.False(staleTerminate.IsValid);
            Assert.Equal("session-changed", staleTerminate.FailureCode);

            var terminated = await second.TerminateAsync(
                lease.SessionId,
                subject,
                client,
                runtime,
                expectedGeneration: renewed.Lease.Generation);
            Assert.True(terminated.IsValid);
        }
        finally
        {
            await DeleteSubjectAsync(connectionString, subject);
        }
    }

    [Fact]
    public async Task PostgreSqlLeaseStore_ExpiresReAdmits_BindsIdentity_AndRollsBackPostMutationFailure()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES") ??
            Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var subject = $"fnd03-expiry-{Guid.NewGuid():N}";
        var client = "web-logical-client";
        var runtime = Runtime("project-a", 7);
        await using var store = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await store.InitializeAsync();

        try
        {
            var first = await store.AdmitAsync(Admission(subject, client, runtime, TimeSpan.FromMilliseconds(2)));
            await Task.Delay(TimeSpan.FromMilliseconds(25));
            var reAdmitted = await store.AdmitAsync(Admission(subject, client, runtime, TimeSpan.FromMinutes(1)));
            Assert.NotEqual(first.SessionId, reAdmitted.SessionId);

            var subjectMismatch = await store.ValidateAsync(reAdmitted.SessionId, "another-subject", runtime, client);
            Assert.Equal("session-user-mismatch", subjectMismatch.FailureCode);
            var clientMismatch = await store.ValidateAsync(reAdmitted.SessionId, subject, runtime, "other-client");
            Assert.Equal("session-client-mismatch", clientMismatch.FailureCode);
            var runtimeMismatch = await store.ValidateAsync(reAdmitted.SessionId, subject, Runtime("project-a", 8), client);
            Assert.Equal("runtime-changed", runtimeMismatch.FailureCode);

            var valid = await store.AdmitAsync(Admission(subject, client, runtime, TimeSpan.FromMinutes(1)));
            // The changed runtime makes the store deactivate this logical identity first. The
            // positive extreme duration overflows only while building the replacement lease,
            // inside the already-open PostgreSQL transaction. Rollback must restore `valid`.
            var postMutationFailure = new RuntimeSessionLeaseAdmission(
                subject,
                client,
                "viewer",
                Runtime("project-b", 9),
                TimeSpan.MaxValue);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.AdmitAsync(postMutationFailure));

            var stillValid = await store.ValidateAsync(valid.SessionId, subject, runtime, client);
            Assert.True(stillValid.IsValid);
            Assert.Equal(valid.SessionId, stillValid.Lease!.SessionId);

            // A second initialization is the migration-compatibility check: the versioned DDL is idempotent.
            await store.InitializeAsync();
            Assert.True((await store.ValidateAsync(valid.SessionId, subject, runtime, client)).IsValid);
        }
        finally
        {
            await DeleteSubjectAsync(connectionString, subject);
        }
    }

    [Fact]
    public async Task PostgreSqlLeaseStore_ReconnectDownscopesViewOnly_WithoutChangingLogicalIdentity()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES") ??
            Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var subject = $"fnd03-downscope-{Guid.NewGuid():N}";
        const string client = "runtime-admission-client";
        var runtime = Runtime("project-a", 7);
        await using var store = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await store.InitializeAsync();

        try
        {
            var interactive = await store.AdmitAsync(
                Admission(subject, client, runtime, TimeSpan.FromMinutes(1), "interactive"));
            var viewOnly = await store.AdmitAsync(
                Admission(subject, client, runtime, TimeSpan.FromMinutes(1), "viewer"));
            var changedBackToInteractive = await store.AdmitAsync(
                Admission(subject, client, runtime, TimeSpan.FromMinutes(1), "interactive"));

            Assert.Equal(interactive.SessionId, viewOnly.SessionId);
            Assert.Equal("viewer", viewOnly.GrantedConnectionClass);
            Assert.Equal(interactive.Generation + 1, viewOnly.Generation);
            Assert.Equal(viewOnly.SessionId, changedBackToInteractive.SessionId);
            Assert.Equal("viewer", changedBackToInteractive.GrantedConnectionClass);
            Assert.Equal(viewOnly.Generation, changedBackToInteractive.Generation);
        }
        finally
        {
            await DeleteSubjectAsync(connectionString, subject);
        }
    }

    [Fact]
    public async Task PostgreSqlLeaseStore_AtomicallyEnforcesSharedSeatCapacityAcrossStoreInstances()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES") ??
            Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var subject = $"fnd03-capacity-{Guid.NewGuid():N}";
        var runtime = Runtime("project-a", 7);
        var capacity = new RuntimeSessionSeatCapacity(InteractiveSeats: 1, ViewOnlySeats: 0);
        await using var first = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await using var second = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await first.InitializeAsync();
        await second.InitializeAsync();

        try
        {
            var authority = await first.GetAuthorityStateAsync();
            Assert.False(authority.TransitionPending);

            var sameIdentity = new RuntimeSessionLeaseCapacityAdmission(
                Admission(subject, "logical-client", runtime, TimeSpan.FromMinutes(1), "interactive"),
                capacity,
                authority.AuthorityRevision);
            var concurrent = await Task.WhenAll(
                first.AdmitWithCapacityAsync(sameIdentity),
                second.AdmitWithCapacityAsync(sameIdentity));
            Assert.All(concurrent, result => Assert.True(result.IsAdmitted));
            Assert.Single(concurrent.Select(result => result.Lease!.SessionId).Distinct());

            var anotherIdentity = new RuntimeSessionLeaseCapacityAdmission(
                Admission(subject, "other-client", runtime, TimeSpan.FromMinutes(1), "interactive"),
                capacity,
                authority.AuthorityRevision);
            var rejected = await second.AdmitWithCapacityAsync(anotherIdentity);
            Assert.False(rejected.IsAdmitted);
            Assert.Equal(RuntimeSessionSeatReservationReasonCode.InteractiveQuotaExhaustedNoEligibleViewOnly, rejected.ReasonCode);
        }
        finally
        {
            await DeleteSubjectAsync(connectionString, subject);
        }
    }

    private static RuntimeSessionLeaseAdmission Admission(
        string subject,
        string client,
        RuntimeSessionRuntimeIdentity runtime,
        TimeSpan duration,
        string grantedConnectionClass = "viewer") =>
        new(subject, client, grantedConnectionClass, runtime, duration);

    private static RuntimeSessionRuntimeIdentity Runtime(string project, long revision) =>
        new("engineering", project, revision, DateTimeOffset.Parse("2026-09-15T00:00:00Z"));

    private static async Task DeleteSubjectAsync(string connectionString, string subject)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(
            "DELETE FROM elitescada.runtime_session_leases WHERE subject_id = @subject_id;");
        command.Parameters.AddWithValue("subject_id", subject);
        await command.ExecuteNonQueryAsync();
    }
}
