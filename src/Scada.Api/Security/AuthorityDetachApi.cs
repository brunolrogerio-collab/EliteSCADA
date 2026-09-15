using Scada.Api.Realtime;
using Scada.Api.Runtime;
using Scada.Security.Audit;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public static class AuthorityDetachApi
{
    private const string DetachAction = "auth.authority.detach";
    private const string Origin = "system-admin-api";
    private const string Target = "security-authority";

    public static IEndpointRouteBuilder MapAuthorityDetachEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var localRuntime = endpoints.ServiceProvider.GetRequiredService<LocalIdentityRuntimeOptions>();
        if (!localRuntime.Enabled) return endpoints;

        endpoints.MapPost("/api/auth/authority/detach", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            AuthorityDetachService detach,
            IAuthorityLifecycleStore lifecycle,
            TagRealtimeHub realtime,
            CancellationToken ct) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.SystemAdmin,
                cancellationToken: ct);
            if (!authorization.Allowed)
            {
                var failure = authorization.FailureResult() ?? Results.Forbid();
                await audit.RecordAuthorizationDeniedAsync(context, authorization, DetachAction, "local-authority", Target);
                return failure;
            }

            AuthorityLifecycleSnapshot? before = null;
            try
            {
                before = await lifecycle.GetAsync(ct);
                var result = await detach.DetachAsync(ct);
                foreach (var subject in result.RevokedSubjects)
                    realtime.RevokeSubject(subject);
                LocalIdentityApi.DeleteLocalCookie(context, localRuntime);

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    DetachAction,
                    AuditOutcome.Succeeded,
                    "local-authority",
                    Target,
                    AuditDetails(result, result.AlreadyDetached ? "already-detached" : "detached"));
                return Results.Ok(new
                {
                    detached = !result.AlreadyDetached,
                    lifecycle = result.After.State.ToString(),
                    epoch = result.After.Epoch,
                    signInRequired = true
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                var observed = await ReadObservedLifecycleAsync(lifecycle);
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    DetachAction,
                    AuditOutcome.Failed,
                    "local-authority",
                    Target,
                    FailureAuditDetails(before, observed));
                return Results.Conflict(new { error = "Authority detach could not be completed; the installation remains fail-closed for recovery." });
            }
        });

        return endpoints;
    }

    private static IReadOnlyDictionary<string, string> AuditDetails(AuthorityDetachResult result, string outcome) =>
        new Dictionary<string, string>
        {
            ["origin"] = Origin,
            ["target"] = Target,
            ["lifecycleBefore"] = result.Before.State.ToString(),
            ["lifecycleAfter"] = result.After.State.ToString(),
            ["epochBefore"] = result.Before.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["epochAfter"] = result.After.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["result"] = outcome
        };

    private static async Task<AuthorityLifecycleSnapshot?> ReadObservedLifecycleAsync(IAuthorityLifecycleStore lifecycle)
    {
        try
        {
            return await lifecycle.GetAsync(CancellationToken.None);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string> FailureAuditDetails(
        AuthorityLifecycleSnapshot? before,
        AuthorityLifecycleSnapshot? observed)
    {
        var details = new Dictionary<string, string>
        {
            ["origin"] = Origin,
            ["target"] = Target,
            ["result"] = "failed"
        };
        if (before is not null)
        {
            details["lifecycleBefore"] = before.State.ToString();
            details["epochBefore"] = before.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (observed is not null)
        {
            details["lifecycleObserved"] = observed.State.ToString();
            details["epochObserved"] = observed.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return details;
    }
}
