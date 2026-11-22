using Scada.Engineering.Contracts;
using Scada.Security.Authorization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scada.Engineering.Security;

/// <summary>Versioned, canonical Authority policy state. Engineering may consume this state but must not mutate it directly.</summary>
public sealed record AuthorityPolicySnapshot(
    long Version,
    IReadOnlyCollection<SecurityRoleEngineeringDto> Roles,
    IReadOnlyCollection<SecurityScopeEngineeringDto> Scopes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuntimePublicAccessPolicy? PublicRuntime = null);

public sealed record AuthorityPolicyWriteResult(bool Applied, AuthorityPolicySnapshot Snapshot, string? Error = null);

public interface IAuthorityPolicyStore
{
    AuthorityPolicySnapshot Snapshot();
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<AuthorityPolicyWriteResult> TryReplaceAsync(
        long expectedVersion,
        IReadOnlyCollection<SecurityRoleEngineeringDto> roles,
        IReadOnlyCollection<SecurityScopeEngineeringDto> scopes,
        CancellationToken cancellationToken = default);

    Task<AuthorityPolicyWriteResult> TryReplacePublicRuntimeAsync(
        long expectedVersion,
        RuntimePublicAccessPolicy? publicRuntime,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthorityPolicyWriteResult(false, Snapshot(), "AUTHORITY_POLICY_PUBLIC_RUNTIME_UNSUPPORTED"));

    Task<AuthorityPolicyWriteResult> TryReplaceSnapshotAsync(
        long expectedVersion,
        AuthorityPolicySnapshot snapshot,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AuthorityPolicyWriteResult(false, Snapshot(), "AUTHORITY_POLICY_SNAPSHOT_REPLACE_UNSUPPORTED"));
}

public sealed record AuthorityPolicyReplicationApplyResult(
    bool Applied,
    AuthorityPolicySnapshot Snapshot,
    string? Error = null);

/// <summary>
/// Durable policy operations reserved for HA reconciliation. Applying a replica
/// is distinct from an authorized local policy mutation and never increments the
/// version on its own.
/// </summary>
public interface IAuthorityPolicyReplicationStore
{
    Task<AuthorityPolicySnapshot> RefreshAsync(CancellationToken cancellationToken = default);

    Task<AuthorityPolicyReplicationApplyResult> ApplyReplicatedSnapshotAsync(
        AuthorityPolicySnapshot snapshot,
        CancellationToken cancellationToken = default);
}

public static class AuthorityPolicySnapshotDigest
{
    public static string Compute(AuthorityPolicySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version < 0) throw new ArgumentOutOfRangeException(nameof(snapshot));
        InMemoryAuthorityPolicyStore.Validate(snapshot.Roles, snapshot.Scopes, snapshot.PublicRuntime);

        var canonicalSnapshot = InMemoryAuthorityPolicyStore.Copy(
            snapshot.Version,
            snapshot.Roles,
            snapshot.Scopes,
            snapshot.PublicRuntime);
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(canonicalSnapshot));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(writer, document.RootElement);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}

/// <summary>Marks an Engineering registry as a read-only projection of the canonical Authority.</summary>
public interface IAuthorityPolicyEngineeringRegistryView
{
    AuthorityPolicySnapshot AuthoritySnapshot();
}

public sealed class InMemoryAuthorityPolicyStore : IAuthorityPolicyStore, IAuthorityPolicyReplicationStore
{
    private readonly object _sync = new();
    private AuthorityPolicySnapshot _snapshot;

    public InMemoryAuthorityPolicyStore(
        IEnumerable<SecurityRoleEngineeringDto>? roles = null,
        IEnumerable<SecurityScopeEngineeringDto>? scopes = null)
    {
        var initialRoles = (roles ?? Array.Empty<SecurityRoleEngineeringDto>()).ToArray();
        var initialScopes = (scopes ?? Array.Empty<SecurityScopeEngineeringDto>()).ToArray();
        Validate(initialRoles, initialScopes);
        _snapshot = Copy(0, initialRoles, initialScopes);
    }

    public AuthorityPolicySnapshot Snapshot()
    {
        lock (_sync) return Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes, _snapshot.PublicRuntime);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<AuthorityPolicySnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Snapshot());
    }

    public Task<AuthorityPolicyReplicationApplyResult> ApplyReplicatedSnapshotAsync(
        AuthorityPolicySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot.Version < 0)
            throw new InvalidDataException("Replicated Authority policy version must be non-negative.");
        Validate(snapshot.Roles, snapshot.Scopes, snapshot.PublicRuntime);
        var incoming = Copy(snapshot.Version, snapshot.Roles, snapshot.Scopes, snapshot.PublicRuntime);
        lock (_sync)
        {
            if (incoming.Version < _snapshot.Version)
                return Task.FromResult(new AuthorityPolicyReplicationApplyResult(
                    false,
                    Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes, _snapshot.PublicRuntime),
                    "AUTHORITY_POLICY_REPLICA_STALE"));

            var incomingDigest = AuthorityPolicySnapshotDigest.Compute(incoming);
            if (incoming.Version == _snapshot.Version)
            {
                var currentDigest = AuthorityPolicySnapshotDigest.Compute(_snapshot);
                return Task.FromResult(incomingDigest == currentDigest
                    ? new AuthorityPolicyReplicationApplyResult(true, Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes, _snapshot.PublicRuntime))
                    : new AuthorityPolicyReplicationApplyResult(false, Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes, _snapshot.PublicRuntime), "AUTHORITY_POLICY_REPLICA_DIGEST_MISMATCH"));
            }

            _snapshot = incoming;
            return Task.FromResult(new AuthorityPolicyReplicationApplyResult(
                true,
                Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes, _snapshot.PublicRuntime)));
        }
    }

    public Task<AuthorityPolicyWriteResult> TryReplaceAsync(
        long expectedVersion,
        IReadOnlyCollection<SecurityRoleEngineeringDto> roles,
        IReadOnlyCollection<SecurityScopeEngineeringDto> scopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(scopes);
        Validate(roles, scopes);
        lock (_sync)
        {
            if (_snapshot.Version != expectedVersion)
                return Task.FromResult(new AuthorityPolicyWriteResult(false, Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes, _snapshot.PublicRuntime), "AUTHORITY_POLICY_CONCURRENCY_CONFLICT"));

            _snapshot = Copy(checked(_snapshot.Version + 1), roles, scopes, _snapshot.PublicRuntime);
            return Task.FromResult(new AuthorityPolicyWriteResult(true, Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes, _snapshot.PublicRuntime)));
        }
    }

    public Task<AuthorityPolicyWriteResult> TryReplacePublicRuntimeAsync(
        long expectedVersion,
        RuntimePublicAccessPolicy? publicRuntime,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RuntimePublicAccessPolicy.Validate(publicRuntime);
        lock (_sync)
        {
            if (_snapshot.Version != expectedVersion)
                return Task.FromResult(new AuthorityPolicyWriteResult(false, Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes, _snapshot.PublicRuntime), "AUTHORITY_POLICY_CONCURRENCY_CONFLICT"));
            _snapshot = Copy(checked(_snapshot.Version + 1), _snapshot.Roles, _snapshot.Scopes, publicRuntime);
            return Task.FromResult(new AuthorityPolicyWriteResult(true, Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes, _snapshot.PublicRuntime)));
        }
    }

    public Task<AuthorityPolicyWriteResult> TryReplaceSnapshotAsync(
        long expectedVersion,
        AuthorityPolicySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(snapshot.Roles, snapshot.Scopes, snapshot.PublicRuntime);
        lock (_sync)
        {
            if (_snapshot.Version != expectedVersion)
                return Task.FromResult(new AuthorityPolicyWriteResult(false, Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes, _snapshot.PublicRuntime), "AUTHORITY_POLICY_CONCURRENCY_CONFLICT"));
            _snapshot = Copy(checked(_snapshot.Version + 1), snapshot.Roles, snapshot.Scopes, snapshot.PublicRuntime);
            return Task.FromResult(new AuthorityPolicyWriteResult(true, Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes, _snapshot.PublicRuntime)));
        }
    }

    public static AuthorityPolicySnapshot Copy(
        long version,
        IEnumerable<SecurityRoleEngineeringDto> roles,
        IEnumerable<SecurityScopeEngineeringDto> scopes,
        RuntimePublicAccessPolicy? publicRuntime = null) => new(
        version,
        roles.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray(),
        scopes.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray(),
        publicRuntime is null ? null : RuntimePublicAccessPolicy.Normalize(publicRuntime));

    public static void Validate(
        IEnumerable<SecurityRoleEngineeringDto> roles,
        IEnumerable<SecurityScopeEngineeringDto> scopes,
        RuntimePublicAccessPolicy? publicRuntime = null)
    {
        RuntimePublicAccessPolicy.Validate(publicRuntime);
        var roleList = roles.ToArray();
        var scopeList = scopes.ToArray();
        if (roleList.Any(role => !role.Id.HasValue || role.Id.Value == Guid.Empty) ||
            roleList.Select(role => role.Id!.Value).Distinct().Count() != roleList.Length ||
            roleList.Select(role => role.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != roleList.Length)
            throw new InvalidDataException("Authority policy roles must have unique stable IDs and keys.");
        if (scopeList.Any(scope => scope.Id == Guid.Empty) ||
            scopeList.Select(scope => scope.Id).Distinct().Count() != scopeList.Length ||
            scopeList.Select(scope => scope.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != scopeList.Length)
            throw new InvalidDataException("Authority policy scopes must have unique stable IDs and keys.");
        if (!SecurityScopeGraph.TryCreate(scopeList, out var graph, out var graphIssues))
            throw new InvalidDataException($"Authority policy scope hierarchy is invalid: {string.Join("; ", graphIssues.Select(issue => issue.Code))}");

        foreach (var role in roleList)
        {
            var issues = SecurityPolicyEngineeringValidator.Validate(role, graph, requiresStableScopeNode: true);
            if (issues.Any(issue => issue.IsError))
                throw new InvalidDataException($"Authority policy role '{role.Key}' is invalid: {string.Join("; ", issues.Where(issue => issue.IsError).Select(issue => issue.Code))}");
        }
    }
}

/// <summary>Read-only compatibility view for Engineering consumers.</summary>
public sealed class AuthorityPolicyRegistryView(IAuthorityPolicyStore store) : ISecurityPolicyEngineeringRegistry, IAuthorityPolicyEngineeringRegistryView
{
    public AuthorityPolicySnapshot AuthoritySnapshot() => store.Snapshot();
    public IReadOnlyCollection<SecurityRoleEngineeringDto> SnapshotRoles() => store.Snapshot().Roles;
    public IReadOnlyCollection<SecurityScopeEngineeringDto> SnapshotScopes() => store.Snapshot().Scopes;
    public SecurityRoleEngineeringDto? FindRole(Guid id) => SnapshotRoles().FirstOrDefault(role => role.Id == id);
    public SecurityRoleEngineeringDto? FindRoleByKey(string key) => SnapshotRoles().FirstOrDefault(role => string.Equals(role.Key, key, StringComparison.OrdinalIgnoreCase));
    public SecurityScopeEngineeringDto? FindScope(Guid id) => SnapshotScopes().FirstOrDefault(scope => scope.Id == id);
    public SecurityScopeEngineeringDto? FindScopeByKey(string key) => SnapshotScopes().FirstOrDefault(scope => string.Equals(scope.Key, key, StringComparison.OrdinalIgnoreCase));
    public void UpsertRole(SecurityRoleEngineeringDto role) => throw new InvalidOperationException("Security Authority is the canonical mutable policy owner.");
    public void UpsertScope(SecurityScopeEngineeringDto scope) => throw new InvalidOperationException("Security Authority is the canonical mutable policy owner.");
}

internal sealed record AuthorityPolicyReferenceValidation(
    bool IsValid,
    string? ErrorCode = null,
    string? Message = null)
{
    public static AuthorityPolicyReferenceValidation Valid { get; } = new(true);
}

/// <summary>Validates the immutable application-to-Authority binding carried by Engineering packages.</summary>
internal static class AuthorityPolicyReferenceValidator
{
    public static AuthorityPolicyReferenceValidation ValidateShape(
        AuthorityPolicyReferenceEngineeringDto? reference)
    {
        if (reference is null)
        {
            return Invalid(
                "SECURITY_AUTHORITY_POLICY_REFERENCE_REQUIRED",
                "Engineering package is missing the required canonical Security Authority reference.");
        }

        if (!string.Equals(reference.Contract, AuthorityPolicyContract.Schema, StringComparison.Ordinal) ||
            reference.ContractVersion != AuthorityPolicyContract.SchemaVersion)
        {
            return Invalid(
                "SECURITY_AUTHORITY_POLICY_REFERENCE_INVALID",
                "Engineering package Authority reference has an unsupported contract or contract version.");
        }

        if (reference.PolicyVersion < 0 ||
            reference.RoleIds is null ||
            reference.ScopeIds is null ||
            reference.RoleIds.Any(id => id == Guid.Empty) ||
            reference.ScopeIds.Any(id => id == Guid.Empty) ||
            reference.RoleIds.Distinct().Count() != reference.RoleIds.Count ||
            reference.ScopeIds.Distinct().Count() != reference.ScopeIds.Count)
        {
            return Invalid(
                "SECURITY_AUTHORITY_POLICY_REFERENCE_INVALID",
                "Engineering package Authority reference contains invalid stable policy identities.");
        }

        return AuthorityPolicyReferenceValidation.Valid;
    }

    public static AuthorityPolicyReferenceValidation ValidateCurrentIdentitySet(
        AuthorityPolicyReferenceEngineeringDto? reference,
        AuthorityPolicySnapshot authority)
    {
        var shape = ValidateShape(reference);
        if (!shape.IsValid) return shape;

        var expectedRoleIds = authority.Roles
            .Where(role => role.Id.HasValue)
            .Select(role => role.Id!.Value)
            .ToHashSet();
        var expectedScopeIds = authority.Scopes
            .Select(scope => scope.Id)
            .ToHashSet();
        var referenceRoleIds = reference!.RoleIds.ToHashSet();
        var referenceScopeIds = reference.ScopeIds.ToHashSet();

        // PolicyVersion records the canonical Authority revision when this Engineering
        // package was saved. Grants are mutable in the separately-owned Authority, so a
        // later role edit must not make an otherwise valid project impossible to reopen.
        // Stable role/scope identity changes still fail closed.
        if (!referenceRoleIds.SetEquals(expectedRoleIds) ||
            !referenceScopeIds.SetEquals(expectedScopeIds))
        {
            return Invalid(
                "SECURITY_AUTHORITY_POLICY_REFERENCE_MISMATCH",
                "Engineering package Authority identities do not match the currently configured canonical Security Authority policy.");
        }

        return AuthorityPolicyReferenceValidation.Valid;
    }

    /// <summary>
    /// Validates the portable Authority identity set without comparing its local
    /// optimistic-concurrency version. Used only by explicit recovery after the
    /// separately protected Authority backup has been restored.
    /// </summary>
    public static AuthorityPolicyReferenceValidation ValidateRestoredIdentitySet(
        AuthorityPolicyReferenceEngineeringDto? reference,
        AuthorityPolicySnapshot authority)
    {
        var shape = ValidateShape(reference);
        if (!shape.IsValid) return shape;

        var expectedRoleIds = authority.Roles
            .Where(role => role.Id.HasValue)
            .Select(role => role.Id!.Value)
            .ToHashSet();
        var expectedScopeIds = authority.Scopes
            .Select(scope => scope.Id)
            .ToHashSet();
        if (!reference!.RoleIds.ToHashSet().SetEquals(expectedRoleIds) ||
            !reference.ScopeIds.ToHashSet().SetEquals(expectedScopeIds))
        {
            return Invalid(
                "SECURITY_AUTHORITY_POLICY_REFERENCE_MISMATCH",
                "Engineering package Authority identities do not match the separately restored canonical Security Authority.");
        }

        return AuthorityPolicyReferenceValidation.Valid;
    }

    private static AuthorityPolicyReferenceValidation Invalid(string code, string message) =>
        new(false, code, message);
}
