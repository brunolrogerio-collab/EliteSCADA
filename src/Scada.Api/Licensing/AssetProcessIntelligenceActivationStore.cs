using System.Text.Json;
using Npgsql;
using Scada.Core.Persistence;
using Scada.Engineering.Contracts;

namespace Scada.Api.Licensing;

/// <summary>Authority-owned activation receipts. Engineering Active is never a receipt.</summary>
public sealed class AssetProcessIntelligenceActivationStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IDurableWriteAdmission? _writeAdmission;
    public AssetProcessIntelligenceActivationStore(string connectionString, IDurableWriteAdmission? writeAdmission = null)
    {
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _writeAdmission = writeAdmission;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var admission = await _writeAdmission.AcquireOptionalAsync("intelligence-activation-schema", cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // Share the canonical policy mutation lock with activation and pause.
        await LockAsync(connection, transaction, cancellationToken);
        await using var command = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS elitescada.intelligence_activation_receipts (
                project_key text NOT NULL,
                scope_kind integer NOT NULL,
                scope_id uuid NOT NULL,
                generation bigint NOT NULL CHECK (generation > 0),
                evidence jsonb NULL,
                updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
                PRIMARY KEY(project_key, scope_kind, scope_id));
            INSERT INTO elitescada.schema_migrations (migration_key) VALUES ('intelligence_activation_receipts_v1') ON CONFLICT (migration_key) DO NOTHING;
            """, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AssetProcessIntelligenceActivationEvidence> ActivateAsync(
        AssetProcessIntelligenceActivationEvidence evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!evidence.Active || evidence.AuthorityEpoch <= 0 || evidence.Revision <= 0 ||
            evidence.ScopeId == Guid.Empty || !Enum.IsDefined(evidence.ScopeKind) ||
            string.IsNullOrWhiteSpace(evidence.ProjectKey) || string.IsNullOrWhiteSpace(evidence.PolicyDigest) ||
            evidence.PolicyVersion < 0 || evidence.HaEpoch < 0)
            throw new ArgumentException("A complete current activation fence is required.", nameof(evidence));
        await using var admission = await _writeAdmission.AcquireOptionalAsync("intelligence-activation", cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockAsync(connection, transaction, cancellationToken);
        await using (var fence = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM elitescada.authority_lifecycle_state lifecycle,
                              elitescada.authority_policy_state policy
                WHERE lifecycle.state_key = 'authority-lifecycle-v1'
                  AND lifecycle.state = 'AuthorityPresent' AND lifecycle.epoch = @epoch
                  AND policy.state_key = 'canonical-policy-v1' AND policy.version = @version);
            """, connection, transaction))
        {
            fence.Parameters.AddWithValue("epoch", evidence.AuthorityEpoch);
            fence.Parameters.AddWithValue("version", evidence.PolicyVersion);
            if (await fence.ExecuteScalarAsync(cancellationToken) is not true)
                throw new InvalidOperationException("Intelligence activation Authority fence changed.");
        }
        var next = evidence with { ProjectKey = evidence.ProjectKey.Trim().ToUpperInvariant() };
        await using var command = KeyCommand("""
            INSERT INTO elitescada.intelligence_activation_receipts
                (project_key, scope_kind, scope_id, generation, evidence)
            VALUES (@project, @kind, @scope, 1, NULL)
            ON CONFLICT (project_key, scope_kind, scope_id) DO UPDATE
                SET generation = intelligence_activation_receipts.generation + 1,
                    evidence = NULL, updated_at_utc = clock_timestamp()
            RETURNING generation;
            """, connection, transaction, next.ProjectKey, next.ScopeKind, next.ScopeId);
        next = next with { Generation = (long)(await command.ExecuteScalarAsync(cancellationToken))! };
        await using var save = KeyCommand("""
            UPDATE elitescada.intelligence_activation_receipts SET evidence = @evidence::jsonb
            WHERE project_key = @project AND scope_kind = @kind AND scope_id = @scope;
            """, connection, transaction, next.ProjectKey, next.ScopeKind, next.ScopeId);
        save.Parameters.AddWithValue("evidence", JsonSerializer.Serialize(next));
        await save.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return next;
    }

    public async Task PauseAsync(string projectKey, AssetProcessIntelligenceScopeKind kind, Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        await using var admission = await _writeAdmission.AcquireOptionalAsync("intelligence-pause", cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockAsync(connection, transaction, cancellationToken);
        await using var command = KeyCommand("""
            INSERT INTO elitescada.intelligence_activation_receipts
                (project_key, scope_kind, scope_id, generation, evidence)
            VALUES (@project, @kind, @scope, 1, NULL)
            ON CONFLICT (project_key, scope_kind, scope_id) DO UPDATE
                SET generation = intelligence_activation_receipts.generation + 1,
                    evidence = NULL, updated_at_utc = clock_timestamp();
            """, connection, transaction, projectKey, kind, scopeId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AssetProcessIntelligenceActivationEvidence?> ReadAsync(string projectKey,
        AssetProcessIntelligenceScopeKind kind, Guid scopeId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = KeyCommand("""
            SELECT evidence::text FROM elitescada.intelligence_activation_receipts
            WHERE project_key = @project AND scope_kind = @kind AND scope_id = @scope;
            """, connection, null, projectKey, kind, scopeId);
        return await command.ExecuteScalarAsync(cancellationToken) is string json
            ? JsonSerializer.Deserialize<AssetProcessIntelligenceActivationEvidence>(json) : null;
    }

    private static async Task LockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(4993446713136202564);", connection, transaction);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static NpgsqlCommand KeyCommand(string sql, NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string projectKey, AssetProcessIntelligenceScopeKind kind, Guid scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
        if (!Enum.IsDefined(kind) || scopeId == Guid.Empty) throw new ArgumentException("A stable scope is required.");
        var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("project", projectKey.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("kind", (int)kind);
        command.Parameters.AddWithValue("scope", scopeId);
        return command;
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}

