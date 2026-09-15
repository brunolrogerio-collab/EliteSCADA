using Npgsql;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Persistence.PostgreSql;
using Scada.Security.Authorization;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlAuthorityPolicyStoreTests
{
    private static string? ConnectionString => Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");

    [Fact]
    public async Task PersistsPolicyAcrossRestartAndRejectsStaleCompareAndSwap()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;

        await ClearAsync();
        var roleId = Guid.Parse("94000000-0000-0000-0000-000000000001");
        var scopeId = Guid.Parse("94000000-0000-0000-0000-000000000002");
        var scopes = new[] { new SecurityScopeEngineeringDto(scopeId, "plant", "Plant", SecurityScopeNodeKind.Plant) };
        var roles = new[]
        {
            new SecurityRoleEngineeringDto(roleId, "arbitrary-role", "Arbitrary", Grants:
            [new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringView, new AuthorizationScopeEngineeringDto(ScopeNodeId: scopeId))])
        };

        await using (var first = new PostgreSqlAuthorityPolicyStore(ConnectionString))
        {
            await first.InitializeAsync();
            var applied = await first.TryReplaceAsync(0, roles, scopes);
            Assert.True(applied.Applied);
            Assert.Equal(1, applied.Snapshot.Version);
        }

        await using (var restarted = new PostgreSqlAuthorityPolicyStore(ConnectionString))
        {
            await restarted.InitializeAsync();
            var loaded = restarted.Snapshot();
            Assert.Equal(1, loaded.Version);
            Assert.Equal(roleId, Assert.Single(loaded.Roles).Id);
            Assert.Equal(scopeId, Assert.Single(loaded.Scopes).Id);

            var stale = await restarted.TryReplaceAsync(0, roles, scopes);
            Assert.False(stale.Applied);
            Assert.Equal("AUTHORITY_POLICY_CONCURRENCY_CONFLICT", stale.Error);
        }

        await ClearAsync();
    }

    private static async Task ClearAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM elitescada.authority_policy_state WHERE state_key = 'canonical-policy-v1';", connection);
        try { await command.ExecuteNonQueryAsync(); }
        catch (PostgresException exception) when (exception.SqlState == "42P01") { }
    }
}
