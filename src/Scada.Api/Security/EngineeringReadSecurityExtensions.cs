using Scada.Api.Runtime;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public static class EngineeringReadSecurityExtensions
{
    public static RouteHandlerBuilder RequireWorkspaceEngineeringRead(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<WorkspaceEngineeringReadFilter>();

    public static RouteHandlerBuilder RequireRuntimeEngineeringRead(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<RuntimeEngineeringReadFilter>();
}

public sealed class WorkspaceEngineeringReadFilter(ApiAuthorizationService security) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
    {
        if (!security.AuthenticationEnabled)
            return next(invocationContext);

        var authorization = security.CheckWorkspace(
            invocationContext.HttpContext,
            SecurityCapability.EngineeringModify);
        var failure = authorization.FailureResult();
        return failure is null
            ? next(invocationContext)
            : ValueTask.FromResult<object?>(failure);
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
            SecurityCapability.EngineeringModify,
            cancellationToken: invocationContext.HttpContext.RequestAborted);
        var failure = authorization.FailureResult();
        return failure ?? await next(invocationContext);
    }
}
