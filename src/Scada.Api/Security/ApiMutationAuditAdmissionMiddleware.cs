namespace Scada.Api.Security;

public sealed class ApiMutationAuditAdmissionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ApiAuditService audit,
        ApiAuthorizationService security)
    {
        if (!RequiresDurableAdmission(context.Request))
        {
            await next(context);
            return;
        }

        try
        {
            await audit.RecordMutationAdmissionAsync(
                context,
                security.GetPrincipal(context),
                context.RequestAborted);
        }
        catch (AuditAdmissionUnavailableException)
        {
            if (context.Response.HasStarted) throw;

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    error = "Audit persistence is unavailable. The protected mutation was not executed."
                },
                cancellationToken: context.RequestAborted);
            return;
        }

        await next(context);
    }

    public static bool RequiresDurableAdmission(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments("/api")) return false;

        return HttpMethods.IsPost(request.Method) ||
               HttpMethods.IsPut(request.Method) ||
               HttpMethods.IsPatch(request.Method) ||
               HttpMethods.IsDelete(request.Method);
    }
}
