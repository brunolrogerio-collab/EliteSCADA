using Scada.Persistence.PostgreSql;
using Scada.Security.Authentication;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlLocalAuthenticationSessionPolicyStoreTests
{
    private static string? ConnectionString => Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");

    [Fact]
    public async Task PolicySurvivesStoreRecreationAndUsesOptimisticVersioning()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;

        await using var first = new PostgreSqlLocalAuthenticationSessionPolicyStore(ConnectionString);
        await first.InitializeAsync(480);
        var before = await first.GetAsync();
        LocalAuthenticationSessionPolicy? updated = null;
        try
        {
            updated = await first.TryUpdateAsync(before.Version, 135);
            Assert.NotNull(updated);
            Assert.Equal(135, updated!.AccessTokenMinutes);

            await using var second = new PostgreSqlLocalAuthenticationSessionPolicyStore(ConnectionString);
            await second.InitializeAsync(480);
            Assert.Equal(updated, await second.GetAsync());
            Assert.Null(await second.TryUpdateAsync(before.Version, 60));
        }
        finally
        {
            var current = await first.GetAsync();
            if (updated is not null && current.Version == updated.Version &&
                current.AccessTokenMinutes != before.AccessTokenMinutes)
            {
                var restored = await first.TryUpdateAsync(current.Version, before.AccessTokenMinutes);
                Assert.NotNull(restored);
                Assert.Equal(before.AccessTokenMinutes, restored!.AccessTokenMinutes);
            }
        }
    }
}
