using Scada.Engineering.Security;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed record EngineeringLockConfigureRequest(string Secret, bool LockImmediately = false);
public sealed record EngineeringLockSecretRequest(string Secret);

public static class EngineeringLockEndpointExtensions
{
    public static void MapEngineeringLockEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/engineering/lock/status", (
            HttpContext context,
            ApiAuthorizationService security,
            IEngineeringLockRegistry registry) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            return Results.Ok(ToStatus(registry.Snapshot()));
        });

        endpoints.MapPost("/api/engineering/lock/configure", async (
            EngineeringLockConfigureRequest request,
            HttpContext context,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IEngineeringLockRegistry registry,
            EngineeringLockSecretService secrets) =>
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

            if (EngineeringLockAccess.IsLocked(registry))
            {
                await RecordDeniedByLockAsync(context, audit, authorization, AuditActions.EngineeringLockConfigure);
                return LockedFailure();
            }

            EngineeringLockEngineeringDto configured;
            try
            {
                configured = secrets.Configure(request.Secret, request.LockImmediately);
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

            registry.Replace(configured);
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
        });

        endpoints.MapPost("/api/engineering/lock/lock", async (
            HttpContext context,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IEngineeringLockRegistry registry,
            EngineeringLockSecretService secrets) =>
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
                var locked = secrets.Lock(registry.Snapshot());
                registry.Replace(locked);
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
            IEngineeringLockRegistry registry,
            EngineeringLockSecretService secrets) =>
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
                var unlocked = secrets.Unlock(registry.Snapshot(), request.Secret);
                registry.Replace(unlocked);
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
            IEngineeringLockRegistry registry,
            EngineeringLockSecretService secrets) =>
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

            if (EngineeringLockAccess.IsLocked(registry))
            {
                await RecordDeniedByLockAsync(context, audit, authorization, AuditActions.EngineeringLockClear);
                return LockedFailure();
            }

            var cleared = secrets.Clear();
            registry.Replace(cleared);
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.EngineeringLockClear,
                AuditOutcome.Succeeded,
                "engineering-lock",
                "current");
            return Results.Ok(ToStatus(cleared));
        });
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
}
