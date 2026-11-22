using Scada.Security.Authentication;

namespace Scada.Security.Tests;

public sealed class LocalAuthenticationSessionPolicyTests
{
    [Fact]
    public async Task InMemoryStore_PreservesPolicyAndRejectsStaleUpdates()
    {
        var store = new InMemoryLocalAuthenticationSessionPolicyStore();
        await store.InitializeAsync(90);

        var initial = await store.GetAsync();
        Assert.Equal(1, initial.Version);
        Assert.Equal(90, initial.AccessTokenMinutes);

        var updated = await store.TryUpdateAsync(initial.Version, 240);
        Assert.NotNull(updated);
        Assert.Equal(2, updated!.Version);
        Assert.Equal(240, updated.AccessTokenMinutes);

        Assert.Null(await store.TryUpdateAsync(initial.Version, 60));
        Assert.Equal(updated, await store.GetAsync());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(1441)]
    public async Task Store_RejectsValuesOutsideSupportedRange(int minutes)
    {
        var store = new InMemoryLocalAuthenticationSessionPolicyStore();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.InitializeAsync(minutes));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.TryUpdateAsync(1, minutes));
    }
}
