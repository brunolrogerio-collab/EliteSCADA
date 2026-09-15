using Scada.Engineering.Contracts;
using Scada.Security.Authorization;

namespace Scada.Engineering.Security;

/// <summary>Versioned, canonical Authority policy state. Engineering may consume this state but must not mutate it directly.</summary>
public sealed record AuthorityPolicySnapshot(
    long Version,
    IReadOnlyCollection<SecurityRoleEngineeringDto> Roles,
    IReadOnlyCollection<SecurityScopeEngineeringDto> Scopes);

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
}

/// <summary>Marks an Engineering registry as a read-only projection of the canonical Authority.</summary>
public interface IAuthorityPolicyEngineeringRegistryView
{
    AuthorityPolicySnapshot AuthoritySnapshot();
}

public sealed class InMemoryAuthorityPolicyStore : IAuthorityPolicyStore
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
        lock (_sync) return Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

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
                return Task.FromResult(new AuthorityPolicyWriteResult(false, Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes), "AUTHORITY_POLICY_CONCURRENCY_CONFLICT"));

            _snapshot = Copy(checked(_snapshot.Version + 1), roles, scopes);
            return Task.FromResult(new AuthorityPolicyWriteResult(true, Copy(_snapshot.Version, _snapshot.Roles, _snapshot.Scopes)));
        }
    }

    public static AuthorityPolicySnapshot Copy(
        long version,
        IEnumerable<SecurityRoleEngineeringDto> roles,
        IEnumerable<SecurityScopeEngineeringDto> scopes) => new(
        version,
        roles.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray(),
        scopes.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray());

    public static void Validate(
        IEnumerable<SecurityRoleEngineeringDto> roles,
        IEnumerable<SecurityScopeEngineeringDto> scopes)
    {
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

    public static AuthorityPolicyReferenceValidation ValidateExact(
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

        if (reference.PolicyVersion != authority.Version ||
            !referenceRoleIds.SetEquals(expectedRoleIds) ||
            !referenceScopeIds.SetEquals(expectedScopeIds))
        {
            return Invalid(
                "SECURITY_AUTHORITY_POLICY_REFERENCE_MISMATCH",
                "Engineering package Authority reference does not match the currently configured canonical Security Authority policy.");
        }

        return AuthorityPolicyReferenceValidation.Valid;
    }

    private static AuthorityPolicyReferenceValidation Invalid(string code, string message) =>
        new(false, code, message);
}
