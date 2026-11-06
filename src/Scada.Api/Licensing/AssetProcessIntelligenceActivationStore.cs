using System.Collections.Concurrent;
using System.Text.Json;
using Npgsql;
using Scada.Core.Persistence;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Persistence.PostgreSql;

namespace Scada.Api.Licensing;

/// <summary>Authority-owned activation receipts. Engineering Active is never a receipt.</summary>
public sealed class AssetProcessIntelligenceActivationStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ConcurrentDictionary<(string, AssetProcessIntelligenceScopeKind, Guid), AssetProcessIntelligenceActivationEvidence> _owned = new();
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
        await PostgreSqlSharedSchemaInitialization.AcquireLockAsync(connection, transaction, cancellationToken);
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
        AssetProcessIntelligenceActivationEvidence evidence, CancellationToken cancellationToken = default,
        AssetProcessIntelligenceActivationEvidence? expectedPrevious = null)
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
        if (expectedPrevious is not null)
        {
            if (expectedPrevious.ProjectKey != evidence.ProjectKey || expectedPrevious.ScopeKind != evidence.ScopeKind ||
                expectedPrevious.ScopeId != evidence.ScopeId || !expectedPrevious.Active ||
                evidence.Revision <= expectedPrevious.Revision ||
                evidence.AuthorityEpoch != expectedPrevious.AuthorityEpoch ||
                evidence.PolicyVersion != expectedPrevious.PolicyVersion || evidence.PolicyDigest != expectedPrevious.PolicyDigest ||
                evidence.HaEpoch != expectedPrevious.HaEpoch)
                throw new InvalidOperationException("Only an unchanged activation fence may advance its revision.");
            await using var previous = KeyCommand("""
                SELECT evidence::text FROM elitescada.intelligence_activation_receipts
                WHERE project_key = @project AND scope_kind = @kind AND scope_id = @scope;
                """, connection, transaction, evidence.ProjectKey, evidence.ScopeKind, evidence.ScopeId);
            var json = await previous.ExecuteScalarAsync(cancellationToken) as string;
            var committed = json is null ? null : JsonSerializer.Deserialize<AssetProcessIntelligenceActivationEvidence>(json);
            if (committed != expectedPrevious)
                throw new InvalidOperationException("Activation changed or was paused during the revision transition.");
        }
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
        if (!await MatchesPolicyDigestAsync(connection, transaction, evidence, cancellationToken))
            throw new InvalidOperationException("Intelligence activation policy digest changed.");
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
        _owned[(next.ProjectKey, next.ScopeKind, next.ScopeId)] = next;
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

    /// <summary>Durably closes all receipts; a later restart cannot recover them.</summary>
    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default,
        string? retainedProject = null, long? retainedRevision = null)
    {
        await using var admission = await _writeAdmission.AcquireOptionalAsync("intelligence-invalidation", cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockAsync(connection, transaction, cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE elitescada.intelligence_activation_receipts
            SET generation = generation + 1, evidence = NULL, updated_at_utc = clock_timestamp()
            WHERE evidence IS NOT NULL AND NOT (
                @project IS NOT NULL AND project_key = @project AND (evidence->>'Revision')::bigint = @revision);
            """, connection, transaction);
        command.Parameters.AddWithValue("project", NpgsqlTypes.NpgsqlDbType.Text, (object?)retainedProject?.Trim().ToUpperInvariant() ?? DBNull.Value);
        command.Parameters.AddWithValue("revision", NpgsqlTypes.NpgsqlDbType.Bigint, (object?)retainedRevision ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Runs local recovery while the durable receipt is locked against pause/replacement.
    /// The caller must retain query-time canonical fence checks after this returns.
    /// </summary>
    public async Task<bool> TryRecoverAsync(AssetProcessIntelligenceActivationEvidence expected,
        Func<AssetProcessIntelligenceActivationEvidence, bool> recover,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(recover);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockAsync(connection, transaction, cancellationToken);
        await using var command = KeyCommand("""
            SELECT evidence::text FROM elitescada.intelligence_activation_receipts
            WHERE project_key = @project AND scope_kind = @kind AND scope_id = @scope
              AND generation = @generation;
            """, connection, transaction, expected.ProjectKey, expected.ScopeKind, expected.ScopeId);
        command.Parameters.AddWithValue("generation", expected.Generation);
        var json = await command.ExecuteScalarAsync(cancellationToken) as string;
        var current = json is null ? null : JsonSerializer.Deserialize<AssetProcessIntelligenceActivationEvidence>(json);
        if (current is null || current != expected || !current.Active)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        await using var fence = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM elitescada.authority_lifecycle_state lifecycle,
                              elitescada.authority_policy_state policy
                WHERE lifecycle.state_key = 'authority-lifecycle-v1'
                  AND lifecycle.state = 'AuthorityPresent' AND lifecycle.epoch = @epoch
                  AND policy.state_key = 'canonical-policy-v1' AND policy.version = @version);
            """, connection, transaction);
        fence.Parameters.AddWithValue("epoch", expected.AuthorityEpoch);
        fence.Parameters.AddWithValue("version", expected.PolicyVersion);
        if (await fence.ExecuteScalarAsync(cancellationToken) is not true)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        if (!await MatchesPolicyDigestAsync(connection, transaction, expected, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        // Callback only modifies the local gate; the caller cancels on transaction failure.
        var recovered = recover(current);
        await transaction.CommitAsync(cancellationToken);
        if (recovered) _owned[(current.ProjectKey, current.ScopeKind, current.ScopeId)] = current;
        return recovered;
    }
    public async Task InvalidateOwnedAsync(string? retainedProject = null, long? retainedRevision = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var entry in _owned.ToArray())
        {
            var receipt = entry.Value;
            if (string.Equals(receipt.ProjectKey, retainedProject, StringComparison.OrdinalIgnoreCase) &&
                receipt.Revision == retainedRevision) continue;
            await using var admission = await _writeAdmission.AcquireOptionalAsync("intelligence-owned-invalidation", cancellationToken);
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await LockAsync(connection, transaction, cancellationToken);
            await using var command = KeyCommand("""
                UPDATE elitescada.intelligence_activation_receipts
                SET generation = generation + 1, evidence = NULL, updated_at_utc = clock_timestamp()
                WHERE project_key = @project AND scope_kind = @kind AND scope_id = @scope
                  AND generation = @generation;
                """, connection, transaction, receipt.ProjectKey, receipt.ScopeKind, receipt.ScopeId);
            command.Parameters.AddWithValue("generation", receipt.Generation);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _owned.TryRemove(entry.Key, out _);
        }
    }
    private static async Task<bool> MatchesPolicyDigestAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, AssetProcessIntelligenceActivationEvidence expected, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT jsonb_build_object('Version', version, 'Roles', roles, 'Scopes', scopes,
                'PublicRuntime', public_runtime)::text
            FROM elitescada.authority_policy_state WHERE state_key = 'canonical-policy-v1';
            """, connection, transaction);
        var json = await command.ExecuteScalarAsync(ct) as string;
        var snapshot = json is null ? null : JsonSerializer.Deserialize<AuthorityPolicySnapshot>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return snapshot is not null && snapshot.Version == expected.PolicyVersion &&
            string.Equals(AuthorityPolicySnapshotDigest.Compute(snapshot), expected.PolicyDigest, StringComparison.Ordinal);
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
