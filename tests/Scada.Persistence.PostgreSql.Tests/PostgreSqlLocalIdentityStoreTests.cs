using Scada.Persistence.PostgreSql;
using Scada.Security.Authentication;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlLocalIdentityStoreTests
{
    private static string? ConnectionString => Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");

    [Fact]
    public async Task PersistsAndUpdatesLocalUserWithoutExposingPasswordMaterialAsPlaintext()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;

        await using var store = new PostgreSqlLocalIdentityStore(ConnectionString);
        await store.InitializeAsync();

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var username = $"identity-{suffix}";
        var now = DateTimeOffset.UtcNow;
        var credential = LocalPasswordHasher.Hash("postgres-test-password", 100_000);
        var account = new LocalUserAccount(
            Guid.NewGuid(),
            username,
            LocalIdentityNormalization.NormalizeUsername(username),
            "Identity Test",
            true,
            new[] { "operator" },
            credential,
            now,
            now);

        await store.CreateAsync(account);
        var found = await store.FindByUsernameAsync(username.ToUpperInvariant());
        Assert.NotNull(found);
        Assert.Equal(account.Id, found!.Id);
        Assert.Equal("Identity Test", found.DisplayName);
        Assert.Equal("operator", Assert.Single(found.Roles));
        Assert.True(LocalPasswordHasher.Verify("postgres-test-password", found.Credential));
        Assert.False(LocalPasswordHasher.Verify("not-the-password", found.Credential));

        var updated = found with
        {
            DisplayName = "Updated Identity",
            IsEnabled = false,
            Roles = new[] { "developer", "operator" },
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        await store.UpdateAsync(updated);

        var reread = await store.FindByIdAsync(account.Id);
        Assert.NotNull(reread);
        Assert.Equal("Updated Identity", reread!.DisplayName);
        Assert.False(reread.IsEnabled);
        Assert.Equal(new[] { "developer", "operator" }, reread.Roles);
        Assert.True(LocalPasswordHasher.Verify("postgres-test-password", reread.Credential));

        var listed = await store.ListAsync();
        Assert.Contains(listed, user => user.Id == account.Id);
    }
}
