using Scada.Api.Security;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Persistence;

public sealed class EngineeringPersistenceSecurityFilter(
    ApiAuthorizationService security,
    ApiAuditService audit) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
    {
        var context = invocationContext.HttpContext;
        var operation = ResolveOperation(context.Request.Method, context.Request.Path.Value);
        if (operation is null)
            return await next(invocationContext);

        var authorization = security.CheckWorkspace(
            context,
            SecurityCapability.EngineeringModify);
        var failure = authorization.FailureResult();
        if (failure is not null)
        {
            await audit.RecordAuthorizationDeniedAsync(
                context,
                authorization,
                operation.Action,
                operation.TargetKind,
                ResolveTargetId(context),
                ResolveDetails(context));
            return failure;
        }

        ReplaceCallerSuppliedActor(invocationContext, authorization.Principal.SubjectId!);

        try
        {
            var result = await next(invocationContext);
            var outcome = IsFailureResult(result)
                ? AuditOutcome.Failed
                : AuditOutcome.Succeeded;

            await audit.RecordAsync(
                context,
                authorization.Principal,
                operation.Action,
                outcome,
                operation.TargetKind,
                ResolveTargetId(context),
                ResolveDetails(context));

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
        {
            var details = new Dictionary<string, string>(ResolveDetails(context))
            {
                ["errorType"] = ex.GetType().Name
            };
            await audit.RecordAsync(
                context,
                authorization.Principal,
                operation.Action,
                AuditOutcome.Failed,
                operation.TargetKind,
                ResolveTargetId(context),
                details);
            throw;
        }
    }

    private static void ReplaceCallerSuppliedActor(
        EndpointFilterInvocationContext context,
        string subjectId)
    {
        for (var i = 0; i < context.Arguments.Count; i++)
        {
            context.Arguments[i] = context.Arguments[i] switch
            {
                EngineeringSaveRequest request => request with { SavedBy = subjectId },
                EngineeringPublishRequest request => request with { PublishedBy = subjectId },
                EngineeringActivateRequest request => request with { ActivatedBy = subjectId },
                var argument => argument
            };
        }
    }

    private static bool IsFailureResult(object? result)
    {
        if (result is not IStatusCodeHttpResult statusResult)
            return false;

        return statusResult.StatusCode is >= 400;
    }

    private static LifecycleOperation? ResolveOperation(string method, string? path)
    {
        if (!HttpMethods.IsPost(method) || string.IsNullOrWhiteSpace(path))
            return null;

        if (path.EndsWith("/save", StringComparison.OrdinalIgnoreCase))
            return new(AuditActions.EngineeringSave, "engineering-project");
        if (path.EndsWith("/publish", StringComparison.OrdinalIgnoreCase))
            return new(AuditActions.EngineeringPublish, "engineering-revision");
        if (path.EndsWith("/published/activate", StringComparison.OrdinalIgnoreCase))
            return new(AuditActions.EngineeringActivate, "engineering-project");
        if (path.EndsWith("/checkout", StringComparison.OrdinalIgnoreCase))
            return new(AuditActions.EngineeringCheckout, "engineering-revision");
        if (path.EndsWith("/apply", StringComparison.OrdinalIgnoreCase))
            return new(AuditActions.EngineeringImportApply, "engineering-revision");

        return null;
    }

    private static string ResolveTargetId(HttpContext context)
    {
        var projectKey = context.Request.RouteValues.TryGetValue("projectKey", out var project)
            ? project?.ToString()
            : null;
        var revision = context.Request.RouteValues.TryGetValue("revision", out var value)
            ? value?.ToString()
            : null;

        return string.IsNullOrWhiteSpace(revision)
            ? projectKey ?? "unknown"
            : $"{projectKey ?? "unknown"}@{revision}";
    }

    private static Dictionary<string, string> ResolveDetails(HttpContext context)
    {
        var details = new Dictionary<string, string>();
        if (context.Request.RouteValues.TryGetValue("projectKey", out var project) && project is not null)
            details["projectKey"] = project.ToString()!;
        if (context.Request.RouteValues.TryGetValue("revision", out var revision) && revision is not null)
            details["revision"] = revision.ToString()!;
        return details;
    }

    private sealed record LifecycleOperation(string Action, string TargetKind);
}
