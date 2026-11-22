using Scada.Api.Realtime;
using Scada.Api.Runtime;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public static class InstallationDetachApi
{
    private const string PreviewAction = "installation.detach.preview";
    private const string ApplyAction = "installation.detach.apply";

    public static IEndpointRouteBuilder MapInstallationDetachEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var localRuntime = endpoints.ServiceProvider.GetRequiredService<LocalIdentityRuntimeOptions>();
        if (!localRuntime.Enabled) return endpoints;

        endpoints.MapPost("/api/installation/detach/preflight", async (
            InstallationDetachRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            InstallationDetachService detach,
            CancellationToken ct) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.SystemAdmin,
                cancellationToken: ct);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context, authorization, PreviewAction, "installation", "current");
                return failure;
            }

            var preview = await detach.PreflightAsync(request, ct);
            await audit.RecordAsync(
                context,
                authorization.Principal,
                PreviewAction,
                preview.CanDetach ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                "installation",
                preview.ProjectKey ?? "current",
                new Dictionary<string, string>
                {
                    ["canDetach"] = preview.CanDetach.ToString(),
                    ["unsavedWorking"] = preview.UnsavedWorking.ToString(),
                    ["runtimeActive"] = preview.RuntimeActive.ToString(),
                    ["authorityState"] = preview.AuthorityState,
                    ["licenseState"] = preview.LicenseState,
                    ["engineeringLocked"] = preview.EngineeringLocked.ToString(),
                    ["historianPreserved"] = preview.HistorianPreservedByDefault.ToString(),
                    ["requiredAcknowledgementCount"] = preview.RequiredAcknowledgements.Count.ToString(),
                    ["blockerCount"] = preview.Blockers.Count.ToString()
                });
            return Results.Ok(preview);
        });

        endpoints.MapPost("/api/installation/detach", async (
            InstallationDetachRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            InstallationDetachService detach,
            TagRealtimeHub realtime,
            LocalIdentityRuntimeOptions localRuntime,
            CancellationToken ct) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.SystemAdmin,
                cancellationToken: ct);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context, authorization, ApplyAction, "installation", "current");
                return failure;
            }

            var beforeSubject = authorization.Principal.SubjectId;
            try
            {
                var result = await detach.DetachAsync(request, ct);
                if (!result.Detached)
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        ApplyAction,
                        AuditOutcome.Failed,
                        "installation",
                        result.DetachedProjectKey ?? "current",
                        SafeDetails(result));
                    return Results.Conflict(result);
                }

                if (!string.IsNullOrWhiteSpace(beforeSubject))
                    realtime.RevokeSubject(beforeSubject);
                LocalIdentityApi.DeleteLocalCookie(context, localRuntime);

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    ApplyAction,
                    AuditOutcome.Succeeded,
                    "installation",
                    result.DetachedProjectKey ?? "current",
                    SafeDetails(result));
                return Results.Ok(result);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    ApplyAction,
                    AuditOutcome.Failed,
                    "installation",
                    "current",
                    new Dictionary<string, string>
                    {
                        ["stage"] = "transition",
                        ["errorType"] = ex.GetType().Name,
                        ["historianPreserved"] = bool.TrueString
                    });
                return Results.Conflict(new
                {
                    error = "Installation detach did not complete. The durable journals remain fail-closed for startup recovery."
                });
            }
        });

        return endpoints;
    }

    private static IReadOnlyDictionary<string, string> SafeDetails(InstallationDetachResult result) =>
        new Dictionary<string, string>
        {
            ["stage"] = result.Stage,
            ["detached"] = result.Detached.ToString(),
            ["detachedRevision"] = result.DetachedRevision?.ToString() ?? "none",
            ["authorityEpoch"] = result.AuthorityEpoch.ToString(),
            ["licenseState"] = result.LicenseState,
            ["licenseOutcome"] = result.LicenseOutcome,
            ["historianPreserved"] = result.HistorianPreserved.ToString(),
            ["signInRequired"] = result.SignInRequired.ToString(),
            ["issueCount"] = result.Issues.Count.ToString()
        };
}
