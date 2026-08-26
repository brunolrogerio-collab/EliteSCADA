using Scada.Core.Tags;

namespace Scada.Security.Authorization;

public enum TagAccessOperation
{
    Read,
    Write,
    Configure
}

public sealed class TagAccessAuthorization(ICapabilityAuthorizationService capabilities)
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
                .Intersect(principal.Roles, StringComparer.OrdinalIgnoreCase)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return allowed.Length > 0
                ? new AuthorizationDecision(true, capability, "TAG access policy explicitly grants this operation.", allowed)
                : AuthorizationDecision.Denied(capability, "TAG access policy does not grant this operation to any assigned role.");
        }

        return capabilities.Evaluate(
            principal,
            capability,
            new AuthorizationResource(TagPath: tag.Path));
    }
}
