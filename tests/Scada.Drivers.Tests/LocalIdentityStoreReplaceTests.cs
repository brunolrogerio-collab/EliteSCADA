using Scada.Persistence.PostgreSql;
using Scada.Security.Authentication;

namespace Scada.Drivers.Tests;

public sealed class LocalIdentityStoreReplaceTests
{
    private const string TestPassword = "Local-store-test-2026";

    [Fact]
    public async Task InMemoryReplaceAll_ReplacesCompleteAuthorityAndCopiesInput()
    {
        var store = new InMemoryLocalIdentityStore();
        var baseline = CreateAccount("baseline-admin", "Baseline", ["developer"]);
        await store.CreateAsync(baseline);

        var first = CreateAccount("restored-admin", "Restored Admin", ["developer"]);
        var second = CreateAccount("restored-operator", "Restored Operator", ["operator"]);
        await store.ReplaceAllAsync([first, second]);

        var users = await store.ListAsync();
        Assert.Equal(2, users.Count);
        Assert.DoesNotContain(users, user => user.Id == baseline.Id);
        Assert.Contains(users, user => user.Id == first.Id);
        Assert.Contains(users, user => user.Id == second.Id);

        first.Credential.Salt[0] ^= 0x7f;
        var durable = await store.FindByIdAsync(first.Id);
        Assert.NotNull(durable);
        Assert.NotEqual(first.Credential.Salt[0], durable!.Credential.Salt[0]);
        Assert.True(LocalPasswordHasher.Verify(TestPassword, durable.Credential));
    }

    [Fact]
    public async Task InMemoryReplaceAll_InvalidReplacementFailsBeforeMutation()
    {
        var store = new InMemoryLocalIdentityStore();
        var baseline = CreateAccount("baseline-admin", "Baseline", ["developer"]);
        await store.CreateAsync(baseline);

        var valid = CreateAccount("candidate-admin", "Candidate", ["developer"]);
        var duplicate = valid with { Id = Guid.NewGuid() };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.ReplaceAllAsync([valid, duplicate]));

        var users = await store.ListAsync();
        var remaining = Assert.Single(users);
        Assert.Equal(baseline.Id, remaining.Id);
        Assert.Equal(baseline.Username, remaining.Username);
    }

    [Fact]
    public async Task PostgreSqlReplaceAll_CommitsCompleteReplacementAndRollsBackMidTransactionFailure()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlLocalIdentityStore(connectionString);
        await store.InitializeAsync();

        var baseline = CreateAccount("baseline-admin", "Baseline", ["developer"]);
        await store.ReplaceAllAsync([baseline]);

        var restoredAdmin = CreateAccount("restored-admin", "Restored Admin", ["developer"]);
        var restoredOperator = CreateAccount("restored-operator", "Restored Operator", ["operator"]);
        await store.ReplaceAllAsync([restoredAdmin, restoredOperator]);

        var committed = await store.ListAsync();
        Assert.Equal(2, committed.Count);
        Assert.Contains(committed, user => user.Id == restoredAdmin.Id);
        Assert.Contains(committed, user => user.Id == restoredOperator.Id);
        Assert.DoesNotContain(committed, user => user.Id == baseline.Id);

        var insertedBeforeFailure = CreateAccount("transaction-admin", "Transaction Admin", ["developer"]);
        var postgresInvalid = CreateAccount("bad\0user", "Rejected by PostgreSQL", ["operator"]);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.ReplaceAllAsync([insertedBeforeFailure, postgresInvalid]));

        var afterRollback = await store.ListAsync();
        Assert.Equal(2, afterRollback.Count);
        Assert.Contains(afterRollback, user => user.Id == restoredAdmin.Id);
        Assert.Contains(afterRollback, user => user.Id == restoredOperator.Id);
        Assert.DoesNotContain(afterRollback, user => user.Id == insertedBeforeFailure.Id);
        Assert.DoesNotContain(afterRollback, user => user.Id == postgresInvalid.Id);
    }

    private static LocalUserAccount CreateAccount(
        string username,
        string displayName,
        IReadOnlyCollection<string> roles)
    {
        var now = DateTimeOffset.UtcNow;
        return new LocalUserAccount(
            Guid.NewGuid(),
            username,
            LocalIdentityNormalization.NormalizeUsername(username),
            displayName,
            true,
            roles,
            LocalPasswordHasher.Hash(TestPassword),
            now,
            now);
    }
}
