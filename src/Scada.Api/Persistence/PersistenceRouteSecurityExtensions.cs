using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Scada.Api.Persistence;

internal static class PersistenceRouteSecurityExtensions
{
    // Decorate the persistence route group once so lifecycle authorization/audit and
    // mutable-Working concurrency controls cannot be accidentally omitted by individual
    // persistence endpoint mappings.
    public static RouteGroupBuilder MapGroup(this WebApplication app, string prefix)
    {
        var group = EndpointRouteBuilderExtensions.MapGroup(app, prefix);
        group.AddEndpointFilter<EngineeringPersistenceSecurityFilter>();
        group.AddEndpointFilter<EngineeringPersistenceApplyConcurrencyFilter>();
        return group;
    }
}
