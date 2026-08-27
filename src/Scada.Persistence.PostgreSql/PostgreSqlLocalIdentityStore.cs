using Npgsql;
using NpgsqlTypes;
using Scada.Security.Authentication;

namespace Scada.Persistence.PostgreSql;

public sealed class PostgreSqlLocalIdentityStore : ILocalIdentityStore, IAsyncDisposable
{
    private const string InitializeSql = """
        SELECT pg_advisory_xact_lock(4993446713136202561);

        CREATE SCHEMA IF NOT EXISTS elitescada;

        CREATE TABLE IF NOT EXISTS elitescada.schema_migrations (
            migration_key text PRIMARY KEY,
            applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
        );

        CREATE TABLE IF NOT EXISTS elitescada.local_users (
            id uuid PRIMARY KEY,
            username varchar(200) NOT NULL,
            normalized_username varchar(200) NOT NULL UNIQUE,
            display_name varchar(300) NOT NULL,
            is_enabled boolean NOT NULL DEFAULT true,
            roles text[] NOT NULL DEFAULT ARRAY[]::text[],
            password_salt bytea NOT NULL,
            password_hash bytea NOT NULL,
            password_iterations integer NOT NULL CHECK (password_iterations >= 100000),
            created_at_utc timestamptz NOT NULL,
            updated_at_utc timestamptz NOT NULL,
            CHECK (length(username) BETWEEN 3 AND 200),
            CHECK (length(normalized_username) BETWEEN 3 AND 200),
            CHECK (length(display_name) BETWEEN 1 AND 300),
            CHECK (octet_length(password_salt) >= 16),
            CHECK (octet_length(password_hash) >= 16)
        );

        CREATE INDEX IF NOT EXISTS ix_local_users_enabled_username
            ON elitescada.local_users (is_enabled, normalized_username);

        INSERT INTO elitescada.schema_migrations (migration_key)
        VALUES ('006_local_identity_users')
        ON CONFLICT (migration_key) DO NOTHING;
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlLocalIdentityStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(InitializeSql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand("SELECT count(*)::integer FROM elitescada.local_users;");
        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    public async Task<LocalUserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = LocalIdentityNormalization.NormalizeUsername(username);
        const string sql = """
            SELECT id, username, normalized_username, display_name, is_enabled, roles,
                   password_salt, password_hash, password_iterations, created_at_utc, updated_at_utc
            FROM elitescada.local_users
            WHERE normalized_username = @normalized_username;
            """;
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("normalized_username", normalized);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<LocalUserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, username, normalized_username, display_name, is_enabled, roles,
                   password_salt, password_hash, password_iterations, created_at_utc, updated_at_utc
            FROM elitescada.local_users
            WHERE id = @id;
            """;
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<IReadOnlyCollection<LocalUserAccount>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, username, normalized_username, display_name, is_enabled, roles,
                   password_salt, password_hash, password_iterations, created_at_utc, updated_at_utc
            FROM elitescada.local_users
            ORDER BY normalized_username, id;
            """;
        await using var command = _dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<LocalUserAccount>();
        while (await reader.ReadAsync(cancellationToken)) users.Add(Read(reader));
        return users;
    }

    public async Task CreateAsync(LocalUserAccount account, CancellationToken cancellationToken = default)
    {
        Validate(account);
        const string sql = """
            INSERT INTO elitescada.local_users (
                id, username, normalized_username, display_name, is_enabled, roles,
                password_salt, password_hash, password_iterations, created_at_utc, updated_at_utc)
            VALUES (
                @id, @username, @normalized_username, @display_name, @is_enabled, @roles,
                @password_salt, @password_hash, @password_iterations, @created_at_utc, @updated_at_utc);
            """;
        await using var command = _dataSource.CreateCommand(sql);
        Bind(command, account);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(LocalUserAccount account, CancellationToken cancellationToken = default)
    {
        Validate(account);
        const string sql = """
            UPDATE elitescada.local_users
            SET username = @username,
                normalized_username = @normalized_username,
                display_name = @display_name,
                is_enabled = @is_enabled,
                roles = @roles,
                password_salt = @password_salt,
                password_hash = @password_hash,
                password_iterations = @password_iterations,
                updated_at_utc = @updated_at_utc
            WHERE id = @id;
            """;
        await using var command = _dataSource.CreateCommand(sql);
        Bind(command, account);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected != 1) throw new KeyNotFoundException($"Local user '{account.Id}' was not found.");
    }

    private static void Bind(NpgsqlCommand command, LocalUserAccount account)
    {
        command.Parameters.AddWithValue("id", account.Id);
        command.Parameters.AddWithValue("username", account.Username.Trim());
        command.Parameters.AddWithValue("normalized_username", account.NormalizedUsername);
        command.Parameters.AddWithValue("display_name", account.DisplayName.Trim());
        command.Parameters.AddWithValue("is_enabled", account.IsEnabled);
        command.Parameters.AddWithValue(
            "roles",
            NpgsqlDbType.Array | NpgsqlDbType.Text,
            LocalIdentityNormalization.NormalizeRoles(account.Roles).ToArray());
        command.Parameters.AddWithValue("password_salt", NpgsqlDbType.Bytea, account.Credential.Salt);
        command.Parameters.AddWithValue("password_hash", NpgsqlDbType.Bytea, account.Credential.Hash);
        command.Parameters.AddWithValue("password_iterations", account.Credential.Iterations);
        command.Parameters.AddWithValue("created_at_utc", account.CreatedAtUtc);
        command.Parameters.AddWithValue("updated_at_utc", account.UpdatedAtUtc);
    }

    private static LocalUserAccount Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetBoolean(4),
        reader.GetFieldValue<string[]>(5),
        new PasswordCredential(
            reader.GetFieldValue<byte[]>(6),
            reader.GetFieldValue<byte[]>(7),
            reader.GetInt32(8)),
        reader.GetFieldValue<DateTimeOffset>(9),
        reader.GetFieldValue<DateTimeOffset>(10));

    private static void Validate(LocalUserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Id == Guid.Empty) throw new ArgumentException("Local user ID is required.", nameof(account));
        if (LocalIdentityNormalization.NormalizeUsername(account.Username) != account.NormalizedUsername)
            throw new ArgumentException("Normalized username does not match username.", nameof(account));
        if (string.IsNullOrWhiteSpace(account.DisplayName) || account.DisplayName.Trim().Length > 300)
            throw new ArgumentException("Display name is required and must not exceed 300 characters.", nameof(account));
        if (account.Credential.Iterations < 100_000 || account.Credential.Salt.Length < 16 || account.Credential.Hash.Length < 16)
            throw new ArgumentException("Password credential is invalid.", nameof(account));
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
