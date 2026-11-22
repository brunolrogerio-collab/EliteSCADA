using Scada.Api.Persistence;
using Scada.Api.Runtime;

namespace Scada.Api.Security;

public sealed class ApiMutationAuditAdmissionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ApiAuditService audit,
        ApiAuthorizationService security,
        DatabaseMaintenanceGate? maintenanceGate = null)
    {
        if (!RequiresDurableAdmission(context.Request))
        {
            await next(context);
            return;
        }

        try
        {
            var isDatabaseControlRequest =
                context.Request.Path.StartsWithSegments("/api/admin/database-topology");
            var allowMaintenanceAudit = maintenanceGate?.IsActive == true && isDatabaseControlRequest;
            using var maintenanceAuditAdmission =
                allowMaintenanceAudit
                    ? maintenanceGate!.AllowMaintenanceControlAuditAdmission()
                    : null;
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

        // The authenticated peer transport is a high-frequency state exchange, not a user
        // command. Auditing every heartbeat-sized replication POST creates unbounded audit
        // writes and prevents database maintenance from reaching a stable snapshot.
        if (HttpMethods.IsPost(request.Method) &&
            string.Equals(
                request.Path.Value,
                RuntimeHaPeerTransportOptions.ReplicationPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HttpMethods.IsPost(request.Method) ||
               HttpMethods.IsPut(request.Method) ||
               HttpMethods.IsPatch(request.Method) ||
               HttpMethods.IsDelete(request.Method);
    }
}
