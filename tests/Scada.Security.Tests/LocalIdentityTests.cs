using Scada.Security.Authentication;

namespace Scada.Security.Tests;

public sealed class LocalIdentityTests
{
    [Fact]
    public void PasswordHasher_UsesRandomSaltAndRejectsWrongPassword()
    {
        const string password = "correct-horse-battery-staple";
        var first = LocalPasswordHasher.Hash(password, 100_000);
        var second = LocalPasswordHasher.Hash(password, 100_000);

        Assert.True(LocalPasswordHasher.Verify(password, first));
        Assert.True(LocalPasswordHasher.Verify(password, second));
        Assert.False(LocalPasswordHasher.Verify("incorrect-password-value", first));
        Assert.False(first.Salt.SequenceEqual(second.Salt));
        Assert.False(first.Hash.SequenceEqual(second.Hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("elevenchars")]
    public void PasswordHasher_RejectsWeakLengthBaseline(string password)
    {
        Assert.ThrowsAny<ArgumentException>(() => LocalPasswordHasher.Hash(password));
    }

    [Fact]
    public void Normalization_IsCaseInsensitiveAndRolesAreDeduplicated()
    {
        Assert.Equal("ENGINEER", LocalIdentityNormalization.NormalizeUsername("  Engineer  "));
        Assert.Equal(
            new[] { "developer", "operator" },
            LocalIdentityNormalization.NormalizeRoles(new[] { "operator", "Developer", "developer", " " })
                .Select(role => role.ToLowerInvariant())
                .ToArray());
    }

    [Fact]
    public async Task InMemoryStore_FindsCaseInsensitiveUserAndReturnsCopies()
    {
        var store = new InMemoryLocalIdentityStore();
        var now = DateTimeOffset.UtcNow;
        var account = new LocalUserAccount(
            Guid.NewGuid(),
            "Engineer",
            LocalIdentityNormalization.NormalizeUsername("Engineer"),
            "Process Engineer",
            true,
            new[] { "developer" },
            LocalPasswordHasher.Hash("safe-development-password", 100_000),
            now,
            now);

        await store.CreateAsync(account);
        Assert.Equal(1, await store.CountAsync());

        var found = await store.FindByUsernameAsync("engineer");
        Assert.NotNull(found);
        Assert.Equal(account.Id, found!.Id);
        Assert.Equal("developer", Assert.Single(found.Roles));

        found.Credential.Hash[0] ^= 0xff;
        var foundAgain = await store.FindByIdAsync(account.Id);
        Assert.NotNull(foundAgain);
        Assert.True(LocalPasswordHasher.Verify("safe-development-password", foundAgain!.Credential));
    }

    [Fact]
    public async Task InMemoryStore_RejectsDuplicateNormalizedUsername()
    {
        var store = new InMemoryLocalIdentityStore();
        var now = DateTimeOffset.UtcNow;
        LocalUserAccount Create(string username) => new(
            Guid.NewGuid(),
            username,
            LocalIdentityNormalization.NormalizeUsername(username),
            username,
            true,
            new[] { "operator" },
            LocalPasswordHasher.Hash("another-safe-password", 100_000),
            now,
            now);

        await store.CreateAsync(Create("Operator"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateAsync(Create("operator")));
    }
}
