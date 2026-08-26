using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Scada.Api.Persistence;

internal static class PersistenceRouteSecurityExtensions
{
    // This namespace-local overload deliberately decorates the persistence route group
    // without changing the public endpoint contract or duplicating route definitions.
    public static RouteGroupBuilder MapGroup(this WebApplication app, string prefix)
    {
        var group = EndpointRouteBuilderExtensions.MapGroup(app, prefix);
        group.AddEndpointFilter<EngineeringPersistenceSecurityFilter>();
        return group;
    }
}
