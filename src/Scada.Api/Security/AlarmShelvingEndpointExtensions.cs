using Scada.Api.Runtime;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public static class AlarmShelvingEndpointExtensions
{
    public static WebApplication MapAlarmShelvingEndpoints(this WebApplication app)
    {
        app.MapPost("/api/alarms/{id:guid}/shelve", async (
            Guid id,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken ct) =>
            await ChangeShelvingAsync(id, shelve: true, context, runtime, security, audit, ct));

        app.MapPost("/api/alarms/{id:guid}/unshelve", async (
            Guid id,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken ct) =>
            await ChangeShelvingAsync(id, shelve: false, context, runtime, security, audit, ct));

        return app;
    }

    private static async Task<IResult> ChangeShelvingAsync(
        Guid id,
        bool shelve,
        HttpContext context,
        ScadaRuntimeFacade runtime,
        ApiAuthorizationService security,
        ApiAuditService audit,
        CancellationToken cancellationToken)
    {
        var definition = runtime.AlarmDefinitions().FirstOrDefault(alarm => alarm.Id == id);
        if (definition is null) return Results.NotFound();

        if (shelve && !definition.ShelvingAllowed)
            return Results.BadRequest(new { error = "Alarm does not allow shelving." });

        var operation = shelve ? "shelve" : "unshelve";
        var details = new Dictionary<string, string>
        {
            ["alarmName"] = definition.Name,
            ["operation"] = operation
        };

        var authorization = await security.CheckRuntimeAsync(
            context,
            runtime,
            SecurityCapability.AlarmShelve,
            new AuthorizationResource(Area: definition.Area),
            cancellationToken);
        var failure = authorization.FailureResult();
        if (failure is not null)
        {
            await audit.RecordAuthorizationDeniedAsync(
                context,
                authorization,
                AuditActions.AlarmShelve,
                "alarm",
                id.ToString(),
                details);
            return failure;
        }

        var actor = authorization.Principal.DisplayName ?? authorization.Principal.SubjectId;
        try
        {
            var changed = shelve
                ? await runtime.ShelveAlarmAsync(id, actor, cancellationToken)
                : await runtime.UnshelveAlarmAsync(id, actor, cancellationToken);

            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.AlarmShelve,
                changed ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                "alarm",
                id.ToString(),
                details);

            if (changed) return Results.Ok();
            return Results.Conflict(new
            {
                error = shelve
                    ? "Alarm cannot be shelved in its current state."
                    : "Alarm is not currently shelved."
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            details["errorType"] = ex.GetType().Name;
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.AlarmShelve,
                AuditOutcome.Failed,
                "alarm",
                id.ToString(),
                details);
            throw;
        }
    }
}
