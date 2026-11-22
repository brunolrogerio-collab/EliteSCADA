using Scada.Core.Tags;

namespace Scada.Security.Authorization;

public enum TagAccessOperation
{
    Read,
    Write,
    Configure
}

public sealed class TagAccessAuthorization(
    ICapabilityAuthorizationService capabilities,
    Func<TagDefinition, AuthorizationResource>? resourceFactory = null)
{
    public AuthorizationDecision Evaluate(
        SecurityPrincipal principal,
        TagDefinition tag,
        TagAccessOperation operation)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(tag);

        var capability = operation switch
        {
            TagAccessOperation.Read => SecurityCapability.TagRead,
            TagAccessOperation.Write => SecurityCapability.ProcessValueWrite,
            TagAccessOperation.Configure => SecurityCapability.EngineeringModify,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        var resource = resourceFactory?.Invoke(tag) ?? new AuthorizationResource(
            TagPath: tag.Path,
            ResourceKind: AuthorizationResourceKind.Tag,
            ResourceId: tag.Id);
        var decision = capabilities.Evaluate(
            principal,
            capability,
            resource);
        if (!decision.Allowed) return decision;

        var explicitRoles = operation switch
        {
            TagAccessOperation.Read => tag.AccessPolicy?.ReadRoles,
            TagAccessOperation.Write => tag.AccessPolicy?.WriteRoles,
            TagAccessOperation.Configure => tag.AccessPolicy?.ConfigureRoles,
            _ => null
        };

        if (explicitRoles is not null)
        {
            var allowed = explicitRoles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Intersect(decision.MatchedRoles, StringComparer.OrdinalIgnoreCase)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return allowed.Length > 0
                ? decision with
                {
                    MatchedRoles = allowed,
                    Reason = "Capability granted through a role allowed by the TAG access policy restriction."
                }
                : AuthorizationDecision.Denied(capability, "TAG access policy does not grant this operation through any capability-granting role.");
        }

        return decision;
    }
}
