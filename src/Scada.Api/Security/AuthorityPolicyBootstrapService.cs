using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Engineering.Contracts;
using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

/// <summary>
/// Deployment-time selector for the one legacy Engineering project that may seed the
/// canonical Authority on an existing installation. This value is intentionally read
/// before startup; it is not an anonymous recovery or runtime project-selection path.
/// </summary>
public sealed record AuthorityPolicyBootstrapOptions(string? MigrationSourceProjectKey)
{
    public const string ConfigurationPath = "Authentication:Local:AuthorityMigration:SourceProjectKey";

    public string? NormalizedMigrationSourceProjectKey =>
        string.IsNullOrWhiteSpace(MigrationSourceProjectKey)
            ? null
            : MigrationSourceProjectKey.Trim();
}

/// <summary>One-time, race-safe migration of a pre-AUTH-03 Engineering policy into canonical Authority storage.</summary>
public sealed class AuthorityPolicyBootstrapService(
    IAuthorityPolicyStore authority,
    ILocalIdentityStore identities,
    IEngineeringProjectCatalog? catalog,
    IEngineeringProjectStore? projects,
    AuthorityPolicyBootstrapOptions options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new SecurityCapabilityJsonConverter(), new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
    };

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await authority.InitializeAsync(cancellationToken);
        if (authority.Snapshot().Roles.Count != 0) return;

        var users = await identities.ListAsync(cancellationToken);
        if (users.Count == 0)
        {
            var bootstrap = BuiltInSecurityRoleDefaults.CreateInitialDeveloperRole();
            var seeded = await authority.TryReplaceAsync(0, [bootstrap], Array.Empty<SecurityScopeEngineeringDto>(), cancellationToken);
            if (!seeded.Applied && seeded.Snapshot.Roles.Count == 0)
                throw new InvalidOperationException("Canonical Security Authority initialization conflicted without a policy state.");
            return;
        }

        if (catalog is null || projects is null)
            throw new InvalidOperationException("Existing local identities require an explicit Authority migration source; Engineering persistence is unavailable.");

        var configuredSource = options.NormalizedMigrationSourceProjectKey;
        var projectsToInspect = (await catalog.ListAsync(cancellationToken))
            .Where(project => configuredSource is null ||
                string.Equals(project.ProjectKey, configuredSource, StringComparison.OrdinalIgnoreCase))
            .OrderBy(project => project.ProjectKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (configuredSource is not null && projectsToInspect.Length != 1)
        {
            throw new InvalidOperationException(
                $"Configured Authority migration source '{configuredSource}' was not found. " +
                $"Set {AuthorityPolicyBootstrapOptions.ConfigurationPath} to one existing pre-AUTH-03 project key.");
        }

        var candidates = new List<LegacyAuthorityPolicyMigrationCandidate>();
        foreach (var project in projectsToInspect)
        {
            var snapshot = await projects.LoadLatestAsync(project.ProjectKey, cancellationToken);
            if (snapshot is null || snapshot.EngineeringSchemaVersion >= EngineeringExchangeSchema19) continue;
            var package = JsonSerializer.Deserialize<EngineeringPackage>(snapshot.EngineeringJson, Json)
                ?? throw new InvalidOperationException($"Legacy Engineering policy for project '{project.ProjectKey}' is invalid.");
            var roles = package.SecurityRoles?.ToArray() ?? Array.Empty<SecurityRoleEngineeringDto>();
            var scopes = package.SecurityScopes?.ToArray() ?? Array.Empty<SecurityScopeEngineeringDto>();
            if (roles.Length == 0) continue;
            InMemoryAuthorityPolicyStore.Validate(roles, scopes);
            candidates.Add(new LegacyAuthorityPolicyMigrationCandidate(
                project.ProjectKey,
                new AuthorityPolicySnapshot(0, roles, scopes)));
        }

        if (candidates.Count != 1)
        {
            if (configuredSource is not null)
            {
                throw new InvalidOperationException(
                    $"Configured Authority migration source '{configuredSource}' does not contain a valid pre-AUTH-03 policy. " +
                    "Restore an Authority backup or choose a project with one valid legacy policy.");
            }

            var candidateKeys = candidates.Count == 0
                ? "none"
                : string.Join(", ", candidates.Select(candidate => candidate.ProjectKey));
            throw new InvalidOperationException(
                "Existing local identities require exactly one pre-AUTH-03 Authority policy migration source; " +
                $"found {candidateKeys}. Set {AuthorityPolicyBootstrapOptions.ConfigurationPath} before startup to select one project, or restore an Authority backup.");
        }

        var source = candidates[0].Policy;
        var migrated = await authority.TryReplaceAsync(0, source.Roles, source.Scopes, cancellationToken);
        if (!migrated.Applied && migrated.Snapshot.Roles.Count == 0)
            throw new InvalidOperationException("Canonical Security Authority migration conflicted without a policy state.");
    }

    private sealed record LegacyAuthorityPolicyMigrationCandidate(
        string ProjectKey,
        AuthorityPolicySnapshot Policy);

    private const int EngineeringExchangeSchema19 = 19;
}
