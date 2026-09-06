using Scada.Api.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Security;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed record EngineeringLockConfigureRequest(string Secret, bool LockImmediately = false);
public sealed record EngineeringLockSecretRequest(string Secret);

public static class EngineeringLockEndpointExtensions
{
    private static readonly EngineeringLockSecretService Secrets = new();

    public static void MapEngineeringLockEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/engineering/lock/status", (
            HttpContext context,
            ApiAuthorizationService security,
            IEngineeringExchangeService exchange) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            return Results.Ok(ToStatus(exchange));
        });

        endpoints.MapGet("/api/engineering/lock/administration-context", (
            HttpContext context,
            ApiAuthorizationService security,
            IEngineeringExchangeService exchange,
            EngineeringWorkspace workspace) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            var descriptor = workspace.Describe();
            return Results.Ok(new
            {
                workspace = new
                {
                    descriptor.ProjectKey,
                    descriptor.ProjectName,
                    descriptor.BaseRevision,
                    descriptor.IsDirty,
                    descriptor.ChangeVersion
                },
                canonical = new
                {
                    schema = EngineeringExchangeService.CurrentSchema,
                    schemaVersion = EngineeringExchangeService.CurrentSchemaVersion
                }
            });
        });

        endpoints.MapPost("/api/engineering/lock/configure", async (
            EngineeringLockConfigureRequest request,
            HttpContext context,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IEngineeringExchangeService exchange,
            EngineeringWorkspace workspace) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringLockConfigure,
                    "engineering-lock",
                    "current");
                return failure;
            }

            try
            {
                var configured = await MutateWorkingStateAsync(
                    workspace,
                    exchange,
                    current =>
                    {
                        if (current.Locked) throw new EngineeringLockedMutationException();
                        return Secrets.Configure(request.Secret, request.LockImmediately);
                    },
                    context.RequestAborted);

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringLockConfigure,
                    AuditOutcome.Succeeded,
                    "engineering-lock",
                    "current",
                    new Dictionary<string, string>
                    {
                        ["locked"] = configured.Locked.ToString()
                    });
                return Results.Ok(ToStatus(configured));
            }
            catch (EngineeringLockedMutationException)
            {
                await RecordDeniedByLockAsync(context, audit, authorization, AuditActions.EngineeringLockConfigure);
                return LockedFailure();
            }
            catch (ArgumentException exception)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringLockConfigure,
                    AuditOutcome.Failed,
                    "engineering-lock",
                    "current",
                    new Dictionary<string, string> { ["reason"] = "invalid-secret" });
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        endpoints.MapPost("/api/engineering/lock/lock", async (
            HttpContext context,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IEngineeringExchangeService exchange,
            EngineeringWorkspace workspace) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringLockLock,
                    "engineering-lock",
                    "current");
                return failure;
            }

            try
            {
                var locked = await MutateWorkingStateAsync(
                    workspace,
                    exchange,
                    Secrets.Lock,
                    context.RequestAborted);
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringLockLock,
                    AuditOutcome.Succeeded,
                    "engineering-lock",
                    "current");
                return Results.Ok(ToStatus(locked));
            }
            catch (InvalidOperationException exception)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringLockLock,
                    AuditOutcome.Failed,
                    "engineering-lock",
                    "current",
                    new Dictionary<string, string> { ["reason"] = "not-configured" });
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        endpoints.MapPost("/api/engineering/lock/unlock", async (
            EngineeringLockSecretRequest request,
            HttpContext context,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IEngineeringExchangeService exchange,
            EngineeringWorkspace workspace) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringLockUnlock,
                    "engineering-lock",
                    "current");
                return failure;
            }

            try
            {
                var unlocked = await MutateWorkingStateAsync(
                    workspace,
                    exchange,
                    current => Secrets.Unlock(current, request.Secret),
                    context.RequestAborted);
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringLockUnlock,
                    AuditOutcome.Succeeded,
                    "engineering-lock",
                    "current");
                return Results.Ok(ToStatus(unlocked));
            }
            catch (UnauthorizedAccessException)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringLockUnlock,
                    AuditOutcome.Denied,
                    "engineering-lock",
                    "current",
                    new Dictionary<string, string> { ["reason"] = "verification-failed" });
                return Results.Json(
                    new { error = "Engineering Lock remains locked." },
                    statusCode: StatusCodes.Status403Forbidden);
            }
        });

        endpoints.MapPost("/api/engineering/lock/clear", async (
            HttpContext context,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IEngineeringExchangeService exchange,
            EngineeringWorkspace workspace) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringLockClear,
                    "engineering-lock",
                    "current");
                return failure;
            }

            try
            {
                var cleared = await MutateWorkingStateAsync(
                    workspace,
                    exchange,
                    current =>
                    {
                        if (current.Locked) throw new EngineeringLockedMutationException();
                        return Secrets.Clear();
                    },
                    context.RequestAborted);
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringLockClear,
                    AuditOutcome.Succeeded,
                    "engineering-lock",
                    "current");
                return Results.Ok(ToStatus(cleared));
            }
            catch (EngineeringLockedMutationException)
            {
                await RecordDeniedByLockAsync(context, audit, authorization, AuditActions.EngineeringLockClear);
                return LockedFailure();
            }
        });
    }

    internal static async Task<EngineeringLockEngineeringDto> MutateWorkingStateAsync(
        EngineeringWorkspace workspace,
        IEngineeringExchangeService exchange,
        Func<EngineeringLockEngineeringDto, EngineeringLockEngineeringDto> transition,
        CancellationToken cancellationToken = default)
    {
        await using var mutation = await workspace.AcquireMutationAsync(
            cancellationToken: cancellationToken);

        var current = EngineeringLockAccess.Current(exchange);
        var next = EngineeringLockContract.Normalize(transition(current));
        if (next == current) return current;

        EngineeringLockAccess.Replace(exchange, next);
        workspace.MarkDirty();
        return next;
    }

    private static object ToStatus(IEngineeringExchangeService exchange)
    {
        try
        {
            return ToStatus(EngineeringLockAccess.Current(exchange));
        }
        catch (InvalidDataException)
        {
            return new { configured = true, locked = true };
        }
    }

    private static object ToStatus(EngineeringLockEngineeringDto state)
    {
        var normalized = EngineeringLockContract.Normalize(state);
        return new
        {
            configured = normalized.Verifier is not null,
            locked = normalized.Locked
        };
    }

    private static IResult LockedFailure() => Results.Json(
        new { error = "Engineering is locked. Unlock the current application first." },
        statusCode: StatusCodes.Status403Forbidden);

    private static Task RecordDeniedByLockAsync(
        HttpContext context,
        ApiAuditService audit,
        ApiAuthorizationCheck authorization,
        string action) =>
        audit.RecordAsync(
            context,
            authorization.Principal,
            action,
            AuditOutcome.Denied,
            "engineering-lock",
            "current",
            new Dictionary<string, string> { ["reason"] = "engineering-lock" }).AsTask();

    private sealed class EngineeringLockedMutationException : InvalidOperationException;
}
