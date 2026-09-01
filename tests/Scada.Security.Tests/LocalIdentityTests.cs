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
        var account = CreateAccount("Engineer", "Process Engineer", new[] { "developer" }, now);

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

        await store.CreateAsync(CreateAccount("Operator", "Operator", new[] { "operator" }, now));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreateAsync(CreateAccount("operator", "Operator Two", new[] { "operator" }, now)));
    }

    [Fact]
    public async Task InMemoryStore_MutationLeaseSerializesLogicalReadModifyWrite()
    {
        var store = new InMemoryLocalIdentityStore();
        var now = DateTimeOffset.UtcNow;
        var account = CreateAccount("Engineer", "Original Name", new[] { "developer" }, now);
        await store.CreateAsync(account);

        var firstHasRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task ChangeDisplayNameAsync()
        {
            await using var lease = await store.AcquireMutationLeaseAsync();
            var current = Assert.IsType<LocalUserAccount>(await store.FindByIdAsync(account.Id));
            firstHasRead.SetResult();
            await releaseFirst.Task;
            await store.UpdateAsync(current with
            {
                DisplayName = "Updated Name",
                UpdatedAtUtc = current.UpdatedAtUtc.AddMilliseconds(1)
            });
        }

        async Task ResetPasswordAsync()
        {
            await firstHasRead.Task;
            await using var lease = await store.AcquireMutationLeaseAsync();
            var current = Assert.IsType<LocalUserAccount>(await store.FindByIdAsync(account.Id));
            await store.UpdateAsync(current with
            {
                Credential = LocalPasswordHasher.Hash("replacement-safe-password", 100_000),
                UpdatedAtUtc = current.UpdatedAtUtc.AddMilliseconds(1)
            });
        }

        var displayUpdate = ChangeDisplayNameAsync();
        var passwordReset = ResetPasswordAsync();
        await firstHasRead.Task;
        await Task.Yield();
        Assert.False(passwordReset.IsCompleted);

        releaseFirst.SetResult();
        await Task.WhenAll(displayUpdate, passwordReset);

        var final = Assert.IsType<LocalUserAccount>(await store.FindByIdAsync(account.Id));
        Assert.Equal("Updated Name", final.DisplayName);
        Assert.True(LocalPasswordHasher.Verify("replacement-safe-password", final.Credential));
    }

    [Fact]
    public async Task InMemoryStore_MutationLeasePreservesCrossUserInvariant()
    {
        var store = new InMemoryLocalIdentityStore();
        var now = DateTimeOffset.UtcNow;
        var first = CreateAccount("AdminOne", "Admin One", new[] { "admin" }, now);
        var second = CreateAccount("AdminTwo", "Admin Two", new[] { "admin" }, now);
        await store.CreateAsync(first);
        await store.CreateAsync(second);

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<bool> TryDisableAsync(Guid id)
        {
            await start.Task;
            await using var lease = await store.AcquireMutationLeaseAsync();
            var users = await store.ListAsync();
            var enabledAdmins = users.Count(user =>
                user.IsEnabled && user.Roles.Contains("admin", StringComparer.OrdinalIgnoreCase));
            if (enabledAdmins <= 1) return false;

            var current = Assert.IsType<LocalUserAccount>(users.Single(user => user.Id == id));
            await store.UpdateAsync(current with
            {
                IsEnabled = false,
                UpdatedAtUtc = current.UpdatedAtUtc.AddMilliseconds(1)
            });
            return true;
        }

        var firstDisable = TryDisableAsync(first.Id);
        var secondDisable = TryDisableAsync(second.Id);
        start.SetResult();
        var results = await Task.WhenAll(firstDisable, secondDisable);

        Assert.Single(results, result => result);
        var final = await store.ListAsync();
        Assert.Single(final, user =>
            user.IsEnabled && user.Roles.Contains("admin", StringComparer.OrdinalIgnoreCase));
    }

    private static LocalUserAccount CreateAccount(
        string username,
        string displayName,
        IReadOnlyCollection<string> roles,
        DateTimeOffset now) => new(
        Guid.NewGuid(),
        username,
        LocalIdentityNormalization.NormalizeUsername(username),
        displayName,
        true,
        roles,
        LocalPasswordHasher.Hash("safe-development-password", 100_000),
        now,
        now);
}
