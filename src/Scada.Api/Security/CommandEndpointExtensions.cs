using Scada.Api.Runtime;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public static class CommandEndpointExtensions
{
    public static WebApplication MapCommandEndpoints(this WebApplication app)
    {
        app.MapPost("/api/commands/{id:guid}/execute", async (
            Guid id,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken ct) =>
        {
            if (!runtime.TryGetCommand(id, out var command) || command is null)
                return Results.NotFound();

            var runtimeBeforeAuthorization = runtime.Describe();
            var details = new Dictionary<string, string>
            {
                ["commandId"] = command.Id.ToString(),
                ["commandKind"] = command.Kind.ToString(),
                ["targetTagPath"] = command.TargetTagPath
            };

            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.CommandExecute,
                new AuthorizationResource(
                    Area: command.Area,
                    EquipmentPath: command.EquipmentPath,
                    TagPath: command.TargetTagPath,
                    CommandKey: command.Key),
                ct);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.CommandExecute,
                    "command",
                    command.Key,
                    details);
                return failure;
            }

            var runtimeBeforeExecution = runtime.Describe();
            if (runtimeBeforeExecution.Revision != runtimeBeforeAuthorization.Revision ||
                !string.Equals(
                    runtimeBeforeExecution.ProjectKey,
                    runtimeBeforeAuthorization.ProjectKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                details["reason"] = "runtime-changed-after-authorization";
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.CommandExecute,
                    AuditOutcome.Failed,
                    "command",
                    command.Key,
                    details);
                return Results.Conflict(new { error = "Active runtime changed while the command was being authorized. Retry the command." });
            }

            try
            {
                await runtime.ExecuteCommandAsync(id, ct);
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.CommandExecute,
                    AuditOutcome.Succeeded,
                    "command",
                    command.Key,
                    details);
                return Results.Accepted();
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                details["errorType"] = ex.GetType().Name;
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.CommandExecute,
                    AuditOutcome.Failed,
                    "command",
                    command.Key,
                    details);
                throw;
            }
        });

        return app;
    }
}
