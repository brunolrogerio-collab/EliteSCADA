using Scada.Api.Runtime;
using Scada.Engineering.ImportExport;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public static class EngineeringReadSecurityExtensions
{
    public static RouteHandlerBuilder RequireWorkspaceEngineeringRead(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<WorkspaceEngineeringReadFilter>();

    public static RouteHandlerBuilder RequireRuntimeEngineeringRead(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<RuntimeEngineeringReadFilter>();
}

public sealed class WorkspaceEngineeringReadFilter(
    ApiAuthorizationService security,
    IEngineeringExchangeService exchange) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
    {
        var context = invocationContext.HttpContext;
        if (security.AuthenticationEnabled)
        {
            var authorization = security.CheckWorkspace(
                context,
                SecurityCapability.EngineeringView);
            var failure = authorization.FailureResult();
            if (failure is not null)
                return ValueTask.FromResult<object?>(failure);
        }

        if (!EngineeringLockAccess.IsWorkspaceReadExempt(context.Request))
        {
            var lockFailure = EngineeringLockAccess.ProtectedEngineeringFailure(exchange);
            if (lockFailure is not null)
                return ValueTask.FromResult<object?>(lockFailure);
        }

        return next(invocationContext);
    }
}

public sealed class RuntimeEngineeringReadFilter(
    ApiAuthorizationService security,
    ScadaRuntimeFacade runtime) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
    {
        if (!security.AuthenticationEnabled)
            return await next(invocationContext);

        var authorization = await security.CheckRuntimeAsync(
            invocationContext.HttpContext,
            runtime,
            SecurityCapability.EngineeringView,
            cancellationToken: invocationContext.HttpContext.RequestAborted);
        var failure = authorization.FailureResult();
        return failure ?? await next(invocationContext);
    }
}
