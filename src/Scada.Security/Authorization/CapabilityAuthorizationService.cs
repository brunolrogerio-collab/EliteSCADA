namespace Scada.Security.Authorization;

public interface ICapabilityAuthorizationService
{
    AuthorizationDecision Evaluate(
        SecurityPrincipal principal,
        SecurityCapability capability,
        AuthorizationResource? resource = null);
}

public sealed class InMemoryCapabilityAuthorizationService : ICapabilityAuthorizationService
{
    private readonly object _sync = new();
    private Dictionary<string, RolePolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryCapabilityAuthorizationService(IEnumerable<RolePolicy>? policies = null)
    {
        ReplacePolicies(policies ?? Array.Empty<RolePolicy>());
    }

    public void ReplacePolicies(IEnumerable<RolePolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        var next = new Dictionary<string, RolePolicy>(StringComparer.OrdinalIgnoreCase);

        foreach (var policy in policies)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(policy.Key);
            ArgumentException.ThrowIfNullOrWhiteSpace(policy.Name);
            ArgumentNullException.ThrowIfNull(policy.Grants);

            if (!next.TryAdd(policy.Key.Trim(), policy with { Key = policy.Key.Trim() }))
                throw new InvalidOperationException($"A role policy with key '{policy.Key}' already exists.");
        }

        lock (_sync)
            _policies = next;
    }

    public AuthorizationDecision Evaluate(
        SecurityPrincipal principal,
        SecurityCapability capability,
        AuthorizationResource? resource = null)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (!principal.IsAuthenticated)
            return AuthorizationDecision.Denied(capability, "The principal is not authenticated.");

        if (string.IsNullOrWhiteSpace(principal.SubjectId))
            return AuthorizationDecision.Denied(capability, "The principal subject id is missing.");

        var target = resource ?? new AuthorizationResource();
        RolePolicy[] policies;
        lock (_sync)
            policies = principal.Roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => _policies.GetValueOrDefault(role.Trim()))
                .Where(policy => policy is not null)
                .Cast<RolePolicy>()
                .ToArray();

        var matched = policies
            .Where(policy => policy.Grants.Any(grant =>
                grant.Capability == capability &&
                (grant.Scope?.Matches(target) ?? true)))
            .Select(policy => policy.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return matched.Length > 0
            ? new AuthorizationDecision(true, capability, "Capability granted by role policy.", matched)
            : AuthorizationDecision.Denied(capability, "No assigned role grants the requested capability for this scope.");
    }
}
