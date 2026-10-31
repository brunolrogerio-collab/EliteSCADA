using Npgsql;
using NpgsqlTypes;
using Scada.Core.Persistence;
using Scada.Security.Authentication;

namespace Scada.Persistence.PostgreSql;

/// <summary>Durable local-login session policy shared by API instances using the same database.</summary>
public sealed class PostgreSqlLocalAuthenticationSessionPolicyStore : ILocalAuthenticationSessionPolicyStore, IAsyncDisposable
{
    private const string PolicyKey = "local-authentication-session-v1";
    private readonly NpgsqlDataSource _dataSource;
    private readonly IDurableWriteAdmission? _writeAdmission;

    public PostgreSqlLocalAuthenticationSessionPolicyStore(
        string connectionString,
        IDurableWriteAdmission? writeAdmission = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));

        _dataSource = NpgsqlDataSource.Create(connectionString);
        _writeAdmission = writeAdmission;
    }

    public async Task InitializeAsync(int initialAccessTokenMinutes, CancellationToken cancellationToken = default)
    {
        LocalAuthenticationSessionPolicy.ValidateAccessTokenMinutes(initialAccessTokenMinutes);

        const string sql = """
            CREATE SCHEMA IF NOT EXISTS elitescada;
            CREATE TABLE IF NOT EXISTS elitescada.schema_migrations (
                migration_key text PRIMARY KEY,
                applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp());
            CREATE TABLE IF NOT EXISTS elitescada.local_authentication_session_policy (
                policy_key text PRIMARY KEY,
                version bigint NOT NULL CHECK (version > 0),
                access_token_minutes integer NOT NULL
                    CHECK (access_token_minutes BETWEEN 5 AND 1440),
                updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp());
            INSERT INTO elitescada.schema_migrations (migration_key)
            VALUES ('026_local_authentication_session_policy')
            ON CONFLICT (migration_key) DO NOTHING;
            INSERT INTO elitescada.local_authentication_session_policy (
                policy_key, version, access_token_minutes)
            VALUES (@policy_key, 1, @access_token_minutes)
            ON CONFLICT (policy_key) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await PostgreSqlSharedSchemaInitialization.AcquireLockAsync(connection, transaction, cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("policy_key", NpgsqlDbType.Text, PolicyKey);
        command.Parameters.AddWithValue("access_token_minutes", NpgsqlDbType.Integer, initialAccessTokenMinutes);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<LocalAuthenticationSessionPolicy> GetAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT version, access_token_minutes
            FROM elitescada.local_authentication_session_policy
            WHERE policy_key = @policy_key;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("policy_key", NpgsqlDbType.Text, PolicyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Local authentication session policy is not initialized.");

        return new LocalAuthenticationSessionPolicy(reader.GetInt64(0), reader.GetInt32(1));
    }

    public async Task<LocalAuthenticationSessionPolicy?> TryUpdateAsync(
        long expectedVersion,
        int accessTokenMinutes,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion < 1) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        LocalAuthenticationSessionPolicy.ValidateAccessTokenMinutes(accessTokenMinutes);
        await using var durableWrite = await _writeAdmission.AcquireOptionalAsync(
            "local-authentication-session-policy",
            cancellationToken);

        const string sql = """
            UPDATE elitescada.local_authentication_session_policy
            SET version = version + 1,
                access_token_minutes = @access_token_minutes,
                updated_at_utc = clock_timestamp()
            WHERE policy_key = @policy_key
              AND version = @expected_version
            RETURNING version, access_token_minutes;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("policy_key", NpgsqlDbType.Text, PolicyKey);
        command.Parameters.AddWithValue("expected_version", NpgsqlDbType.Bigint, expectedVersion);
        command.Parameters.AddWithValue("access_token_minutes", NpgsqlDbType.Integer, accessTokenMinutes);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            _ = await GetAsync(cancellationToken);
            return null;
        }

        return new LocalAuthenticationSessionPolicy(reader.GetInt64(0), reader.GetInt32(1));
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
