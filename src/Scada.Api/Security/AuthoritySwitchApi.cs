using System.Security.Cryptography;
using System.Text;
using Scada.Api.Realtime;
using Scada.Api.Runtime;
using Scada.Security.Audit;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed record AuthoritySwitchRequest(string Backup, string Password);

public static class AuthoritySwitchApi
{
    private const string SwitchAction = "auth.authority.switch";
    private const string Origin = "system-admin-api";

    public static IEndpointRouteBuilder MapAuthoritySwitchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var localRuntime = endpoints.ServiceProvider.GetRequiredService<LocalIdentityRuntimeOptions>();
        if (!localRuntime.Enabled) return endpoints;

        endpoints.MapPost("/api/auth/authority/switch", async (
            AuthoritySwitchRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            AuthoritySwitchService authoritySwitch,
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
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    SwitchAction,
                    "local-authority",
                    "switch",
                    new Dictionary<string, string> { ["requiredCapability"] = nameof(SecurityCapability.SystemAdmin) });
                return failure;
            }

            AuthorityLifecycleSnapshot? before = null;
            try
            {
                before = await lifecycle.GetAsync(ct);
                // The service holds the durable operation lease while it validates the target and
                // crosses detach/attach, so concurrent switches cannot prepare against stale state.
                var result = await authoritySwitch.SwitchAsync(request.Backup, request.Password, ct);
                foreach (var subject in result.Detach.RevokedSubjects)
                    realtime.RevokeSubject(subject);
                LocalIdentityApi.DeleteLocalCookie(context, localRuntime);

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    SwitchAction,
                    AuditOutcome.Succeeded,
                    "local-authority",
                    TargetFingerprint(request.Backup),
                    AuditDetails(before, result.Attach.After, "switched"));
                return Results.Ok(new
                {
                    switched = true,
                    lifecycle = result.Attach.After.State.ToString(),
                    epoch = result.Attach.After.Epoch,
                    signInRequired = true,
                    preview = result.Preview
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (ArgumentException)
            {
                await RecordFailedAsync(audit, context, authorization.Principal, lifecycle, request.Backup, before);
                return InvalidTarget();
            }
            catch (InvalidDataException)
            {
                await RecordFailedAsync(audit, context, authorization.Principal, lifecycle, request.Backup, before);
                return InvalidTarget();
            }
            catch (Exception)
            {
                await RecordFailedAsync(audit, context, authorization.Principal, lifecycle, request.Backup, before);
                return Results.Conflict(new
                {
                    error = "Authority switch could not be completed; any interrupted Authority transition remains fail-closed for recovery."
                });
            }
        });

        return endpoints;
    }

    private static async Task RecordFailedAsync(
        ApiAuditService audit,
        HttpContext context,
        SecurityPrincipal principal,
        IAuthorityLifecycleStore lifecycle,
        string backup,
        AuthorityLifecycleSnapshot? before)
    {
        var observed = await ReadObservedLifecycleAsync(lifecycle);
        await audit.RecordAsync(
            context,
            principal,
            SwitchAction,
            AuditOutcome.Failed,
            "local-authority",
            TargetFingerprint(backup),
            FailureAuditDetails(before, observed));
    }

    private static IReadOnlyDictionary<string, string> AuditDetails(
        AuthorityLifecycleSnapshot before,
        AuthorityLifecycleSnapshot after,
        string result) =>
        new Dictionary<string, string>
        {
            ["origin"] = Origin,
            ["lifecycleBefore"] = before.State.ToString(),
            ["lifecycleAfter"] = after.State.ToString(),
            ["epochBefore"] = before.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["epochAfter"] = after.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["result"] = result
        };

    private static IReadOnlyDictionary<string, string> FailureAuditDetails(
        AuthorityLifecycleSnapshot? before,
        AuthorityLifecycleSnapshot? observed)
    {
        var details = new Dictionary<string, string>
        {
            ["origin"] = Origin,
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

    private static async Task<AuthorityLifecycleSnapshot?> ReadObservedLifecycleAsync(IAuthorityLifecycleStore lifecycle)
    {
        try { return await lifecycle.GetAsync(CancellationToken.None); }
        catch { return null; }
    }

    private static string TargetFingerprint(string backup) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(backup ?? string.Empty)));

    private static IResult InvalidTarget() => Results.BadRequest(new
    {
        error = "Authority switch target could not be opened or does not contain a complete compatible Security Authority."
    });
}
