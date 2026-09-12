using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;

namespace Scada.Persistence.PostgreSql;

/// <summary>PostgreSQL-backed canonical Authority policy with compare-and-swap versioning.</summary>
public sealed class PostgreSqlAuthorityPolicyStore : IAuthorityPolicyStore, IAsyncDisposable
{
    private const long MutationLock = 4993446713136202564;
    private const string StateKey = "canonical-policy-v1";
    private readonly object _sync = new();
    private readonly NpgsqlDataSource _dataSource;
    private AuthorityPolicySnapshot _snapshot = new(0, Array.Empty<SecurityRoleEngineeringDto>(), Array.Empty<SecurityScopeEngineeringDto>());

    public PostgreSqlAuthorityPolicyStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public AuthorityPolicySnapshot Snapshot()
    {
        lock (_sync) return InMemoryAuthorityPolicyStore.Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE SCHEMA IF NOT EXISTS elitescada;
            CREATE TABLE IF NOT EXISTS elitescada.schema_migrations (
                migration_key text PRIMARY KEY,
                applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp());
            CREATE TABLE IF NOT EXISTS elitescada.authority_policy_state (
                state_key text PRIMARY KEY,
                version bigint NOT NULL CHECK (version >= 0),
                roles jsonb NOT NULL,
                scopes jsonb NOT NULL,
                updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp());
            INSERT INTO elitescada.schema_migrations (migration_key) VALUES ('019_authority_policy_v1') ON CONFLICT (migration_key) DO NOTHING;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using (var command = new NpgsqlCommand(sql, connection)) await command.ExecuteNonQueryAsync(cancellationToken);
        var loaded = await LoadAsync(connection, null, cancellationToken);
        lock (_sync) _snapshot = loaded;
    }

    public async Task<AuthorityPolicyWriteResult> TryReplaceAsync(long expectedVersion, IReadOnlyCollection<SecurityRoleEngineeringDto> roles, IReadOnlyCollection<SecurityScopeEngineeringDto> scopes, CancellationToken cancellationToken = default)
    {
        InMemoryAuthorityPolicyStore.Validate(roles, scopes);
        var candidate = InMemoryAuthorityPolicyStore.Copy(checked(expectedVersion + 1), roles, scopes);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key);", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("key", NpgsqlDbType.Bigint, MutationLock);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var current = await LoadAsync(connection, transaction, cancellationToken);
        if (current.Version != expectedVersion)
        {
            await transaction.CommitAsync(cancellationToken);
            lock (_sync) _snapshot = current;
            return new AuthorityPolicyWriteResult(false, InMemoryAuthorityPolicyStore.Copy(current.Version, current.Roles, current.Scopes), "AUTHORITY_POLICY_CONCURRENCY_CONFLICT");
        }

        const string save = """
            INSERT INTO elitescada.authority_policy_state (state_key, version, roles, scopes)
            VALUES (@key, @version, @roles::jsonb, @scopes::jsonb)
            ON CONFLICT (state_key) DO UPDATE SET version = EXCLUDED.version, roles = EXCLUDED.roles,
                scopes = EXCLUDED.scopes, updated_at_utc = clock_timestamp();
            """;
        await using (var command = new NpgsqlCommand(save, connection, transaction))
        {
            command.Parameters.AddWithValue("key", StateKey);
            command.Parameters.AddWithValue("version", candidate.Version);
            command.Parameters.AddWithValue("roles", JsonSerializer.Serialize(candidate.Roles));
            command.Parameters.AddWithValue("scopes", JsonSerializer.Serialize(candidate.Scopes));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        lock (_sync) _snapshot = candidate;
        return new AuthorityPolicyWriteResult(true, InMemoryAuthorityPolicyStore.Copy(candidate.Version, candidate.Roles, candidate.Scopes));
    }

    private static async Task<AuthorityPolicySnapshot> LoadAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT version, roles::text, scopes::text FROM elitescada.authority_policy_state WHERE state_key = @key;", connection, transaction);
        command.Parameters.AddWithValue("key", StateKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new AuthorityPolicySnapshot(0, Array.Empty<SecurityRoleEngineeringDto>(), Array.Empty<SecurityScopeEngineeringDto>());
        var roles = JsonSerializer.Deserialize<SecurityRoleEngineeringDto[]>(reader.GetString(1)) ?? throw new InvalidDataException("Persisted Authority roles are invalid.");
        var scopes = JsonSerializer.Deserialize<SecurityScopeEngineeringDto[]>(reader.GetString(2)) ?? throw new InvalidDataException("Persisted Authority scopes are invalid.");
        InMemoryAuthorityPolicyStore.Validate(roles, scopes);
        return InMemoryAuthorityPolicyStore.Copy(reader.GetInt64(0), roles, scopes);
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
