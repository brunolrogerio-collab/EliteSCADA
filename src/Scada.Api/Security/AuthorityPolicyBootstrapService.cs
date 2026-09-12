using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Engineering.Contracts;
using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

/// <summary>One-time, race-safe migration of a pre-AUTH-03 Engineering policy into canonical Authority storage.</summary>
public sealed class AuthorityPolicyBootstrapService(
    IAuthorityPolicyStore authority,
    ILocalIdentityStore identities,
    IEngineeringProjectCatalog? catalog,
    IEngineeringProjectStore? projects)
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

        var candidates = new List<AuthorityPolicySnapshot>();
        foreach (var project in await catalog.ListAsync(cancellationToken))
        {
            var snapshot = await projects.LoadLatestAsync(project.ProjectKey, cancellationToken);
            if (snapshot is null || snapshot.EngineeringSchemaVersion >= EngineeringExchangeSchema19) continue;
            var package = JsonSerializer.Deserialize<EngineeringPackage>(snapshot.EngineeringJson, Json)
                ?? throw new InvalidOperationException($"Legacy Engineering policy for project '{project.ProjectKey}' is invalid.");
            var roles = package.SecurityRoles?.ToArray() ?? Array.Empty<SecurityRoleEngineeringDto>();
            var scopes = package.SecurityScopes?.ToArray() ?? Array.Empty<SecurityScopeEngineeringDto>();
            if (roles.Length == 0) continue;
            InMemoryAuthorityPolicyStore.Validate(roles, scopes);
            candidates.Add(new AuthorityPolicySnapshot(0, roles, scopes));
        }

        if (candidates.Count != 1)
            throw new InvalidOperationException("Existing local identities require exactly one explicit pre-AUTH-03 Authority policy migration source. Restore an Authority backup or select a single project policy before startup.");

        var source = candidates[0];
        var migrated = await authority.TryReplaceAsync(0, source.Roles, source.Scopes, cancellationToken);
        if (!migrated.Applied && migrated.Snapshot.Roles.Count == 0)
            throw new InvalidOperationException("Canonical Security Authority migration conflicted without a policy state.");
    }

    private const int EngineeringExchangeSchema19 = 19;
}
