using Scada.Api.Licensing;
using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public static class RuntimePublicSessionApi
{
    public static IEndpointRouteBuilder MapRuntimePublicSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/runtime/public/status", (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            IAuthorityPolicyStore policies) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var descriptor = runtime.Describe();
            var policy = policies.Snapshot().PublicRuntime;
            return Results.Ok(new { enabled = MatchesActiveRuntime(policy, descriptor) });
        });

        endpoints.MapPost("/api/runtime/public/session", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            IAuthorityPolicyStore policies,
            IProductLicenseService licensing,
            RuntimeHighAvailabilityService highAvailability) =>
        {
            if (!security.AuthenticationEnabled) return Results.NotFound();
            var issuer = context.RequestServices.GetService<JwtTokenIssuer>();
            if (issuer is null) return Results.Problem("Public Runtime session issuance is unavailable for this authentication configuration.", statusCode: StatusCodes.Status503ServiceUnavailable);

            var currentPolicy = policies.Snapshot();
            var descriptor = runtime.Describe();
            if (!MatchesActiveRuntime(currentPolicy.PublicRuntime, descriptor))
                return Results.NotFound();

            if (PublicRuntimeSessionSecurity.TryRead(context.User, out var existing))
            {
                if (existing.PolicyVersion == currentPolicy.Version)
                {
                    var validation = await security.RuntimeSessions.ValidateAsync(
                        existing.SessionId,
                        existing.SubjectId,
                        descriptor,
                        existing.ClientInstanceId,
                        context.RequestAborted);
                    if (validation.IsValid && validation.Lease is { ConnectionClass: RuntimeConnectionClass.ViewOnly } lease)
                        return Results.Ok(SessionResponse(lease));
                }
                ClearPublicCookie(context);
            }
            else if (security.GetPrincipal(context).IsAuthenticated)
            {
                return Results.Conflict(new { error = "An authenticated session is already active. Use the authenticated Runtime session." });
            }

            var authority = await security.RuntimeSessions.GetAuthorityStateAsync(context.RequestAborted);
            if (authority.TransitionPending)
                return Results.Json(new { error = RuntimeSessionSeatReservationReasonCode.AuthorityTransitionPending.ToString() }, statusCode: StatusCodes.Status503ServiceUnavailable);

            var capacity = RuntimeSessionSeatCapacityPolicy.Resolve(licensing.CurrentVerification);
            if (!capacity.IsAvailable || capacity.Capacity is null)
                return Results.Json(new { error = capacity.ReasonCode.ToString(), capacityReasonCode = capacity.ReasonCode.ToString() }, statusCode: StatusCodes.Status409Conflict);

            var before = runtime.Describe();
            highAvailability.RefreshLocalReadiness(before, licensing.CurrentVerification);
            if (highAvailability.Enabled && !highAvailability.CanOwnIndustrialEffects() &&
                !highAvailability.HasReadyPassiveRuntimeProjection(before))
                return Results.Json(new { error = "HA_RUNTIME_VIEW_NOT_READY" }, statusCode: StatusCodes.Status503ServiceUnavailable);

            var clientInstanceId = "public-" + Guid.NewGuid().ToString("N");
            var subjectId = "public-runtime:" + Guid.NewGuid().ToString("N");
            var seat = await security.RuntimeSessions.AdmitWithCapacityAsync(
                subjectId,
                clientInstanceId,
                RuntimeConnectionClass.ViewOnly,
                before,
                capacity.Capacity,
                authority.AuthorityRevision,
                serverNode: highAvailability.LocalNodeId,
                clusterId: highAvailability.ClusterId,
                cancellationToken: context.RequestAborted);
            if (!seat.IsAdmitted || seat.Lease is null)
                return Results.Json(new { error = seat.ReasonCode.ToString(), capacityReasonCode = seat.ReasonCode.ToString() }, statusCode: StatusCodes.Status409Conflict);

            var after = runtime.Describe();
            if (!SameRuntime(before, after) || !MatchesActiveRuntime(currentPolicy.PublicRuntime, after))
            {
                _ = await security.RuntimeSessions.TerminateAsync(
                    seat.Lease.SessionId, subjectId, clientInstanceId, before, context.RequestAborted);
                return Results.Conflict(new { error = "Runtime changed while the public view session was being opened." });
            }

            var latestPolicy = policies.Snapshot();
            if (latestPolicy.Version != currentPolicy.Version ||
                !MatchesActiveRuntime(latestPolicy.PublicRuntime, after))
            {
                _ = await security.RuntimeSessions.TerminateAsync(
                    seat.Lease.SessionId, subjectId, clientInstanceId, after, context.RequestAborted);
                return Results.Conflict(new { error = "Public Runtime policy changed while the view session was being opened." });
            }

            IssuedAccessToken issued;
            try
            {
                issued = await issuer.IssuePublicRuntimeSessionAsync(
                    subjectId,
                    seat.Lease.SessionId,
                    clientInstanceId,
                    currentPolicy.Version,
                    cancellationToken: context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                _ = await security.RuntimeSessions.TerminateAsync(
                    seat.Lease.SessionId, subjectId, clientInstanceId, after, CancellationToken.None);
                throw;
            }
            catch
            {
                _ = await security.RuntimeSessions.TerminateAsync(
                    seat.Lease.SessionId, subjectId, clientInstanceId, after, CancellationToken.None);
                return Results.Problem(
                    "Security Authority is unavailable for a public Runtime session.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var finalPolicy = policies.Snapshot();
            var finalRuntime = runtime.Describe();
            if (finalPolicy.Version != currentPolicy.Version ||
                !MatchesActiveRuntime(finalPolicy.PublicRuntime, finalRuntime) ||
                !SameRuntime(after, finalRuntime))
            {
                _ = await security.RuntimeSessions.TerminateAsync(
                    seat.Lease.SessionId, subjectId, clientInstanceId, finalRuntime, CancellationToken.None);
                return Results.Conflict(new { error = "Public Runtime changed while the view session was being issued." });
            }

            WritePublicCookie(context, issued);
            return Results.Created($"/api/runtime/public/session/{seat.Lease.SessionId}", SessionResponse(seat.Lease));
        });

        endpoints.MapPost("/api/runtime/public/session/heartbeat", async (
            HttpContext context,
            RuntimePublicSessionHeartbeatRequest request,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            IAuthorityPolicyStore policies) =>
        {
            if (!TryCurrentVisitor(context, policies, runtime, out var visitor, out var failure))
            {
                ClearPublicCookie(context);
                return failure!;
            }
            if (request.SessionId != visitor!.SessionId ||
                !string.Equals(request.ClientInstanceId, visitor.ClientInstanceId, StringComparison.Ordinal))
                return Results.Unauthorized();

            var validation = await security.RuntimeSessions.HeartbeatAsync(
                visitor.SessionId,
                visitor.SubjectId,
                visitor.ClientInstanceId,
                runtime.Describe(),
                context.RequestAborted);
            if (!validation.IsValid || validation.Lease is not { ConnectionClass: RuntimeConnectionClass.ViewOnly } lease)
            {
                ClearPublicCookie(context);
                return Results.Unauthorized();
            }

            var issuer = context.RequestServices.GetService<JwtTokenIssuer>();
            if (issuer is null) return Results.Problem("Public Runtime session renewal is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
            var issued = await issuer.IssuePublicRuntimeSessionAsync(
                visitor.SubjectId,
                visitor.SessionId,
                visitor.ClientInstanceId,
                visitor.PolicyVersion,
                cancellationToken: context.RequestAborted);
            WritePublicCookie(context, issued);
            return Results.Ok(SessionResponse(lease));
        });

        endpoints.MapPost("/api/runtime/public/session/terminate", async (
            HttpContext context,
            RuntimePublicSessionHeartbeatRequest request,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            IAuthorityPolicyStore policies) =>
        {
            if (!PublicRuntimeSessionSecurity.TryRead(context.User, out var visitor) ||
                request.SessionId != visitor.SessionId ||
                !string.Equals(request.ClientInstanceId, visitor.ClientInstanceId, StringComparison.Ordinal))
            {
                ClearPublicCookie(context);
                return Results.NoContent();
            }

            _ = await security.RuntimeSessions.TerminateAsync(
                visitor.SessionId,
                visitor.SubjectId,
                visitor.ClientInstanceId,
                runtime.Describe(),
                context.RequestAborted);
            ClearPublicCookie(context);
            return Results.NoContent();
        });

        return endpoints;
    }

    public static bool MatchesActiveRuntime(RuntimePublicAccessPolicy? policy, ScadaRuntimeDescriptor runtime) =>
        policy?.Enabled == true &&
        runtime.Mode.Equals("engineering", StringComparison.OrdinalIgnoreCase) &&
        runtime.Revision == policy.Revision &&
        string.Equals(runtime.ProjectKey, policy.ProjectKey, StringComparison.OrdinalIgnoreCase);

    public static bool TryCurrentVisitor(
        HttpContext context,
        IAuthorityPolicyStore policies,
        ScadaRuntimeFacade runtime,
        out PublicRuntimeSessionIdentity? visitor,
        out IResult? failure)
    {
        visitor = null;
        failure = null;
        if (!PublicRuntimeSessionSecurity.TryRead(context.User, out var parsed))
        {
            failure = Results.Unauthorized();
            return false;
        }

        var snapshot = policies.Snapshot();
        if (parsed.PolicyVersion != snapshot.Version || !MatchesActiveRuntime(snapshot.PublicRuntime, runtime.Describe()))
        {
            failure = Results.Json(new { error = "PUBLIC_RUNTIME_POLICY_REVOKED" }, statusCode: StatusCodes.Status403Forbidden);
            return false;
        }

        visitor = parsed;
        return true;
    }

    public static void WritePublicCookie(HttpContext context, IssuedAccessToken issued) =>
        context.Response.Cookies.Append(PublicRuntimeSessionSecurity.CookieName, issued.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = issued.ExpiresAtUtc,
            IsEssential = true
        });

    public static void ClearPublicCookie(HttpContext context) =>
        context.Response.Cookies.Delete(PublicRuntimeSessionSecurity.CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

    private static object SessionResponse(RuntimeSessionLease lease) => new
    {
        sessionId = lease.SessionId,
        clientInstanceId = lease.ClientInstanceId,
        requestedClass = "viewOnly",
        grantedClass = "viewOnly",
        expiresAtUtc = lease.ExpiresAtUtc
    };

    private static bool SameRuntime(ScadaRuntimeDescriptor left, ScadaRuntimeDescriptor right) =>
        left.Revision == right.Revision &&
        left.ActivatedAtUtc == right.ActivatedAtUtc &&
        left.Mode.Equals(right.Mode, StringComparison.Ordinal) &&
        string.Equals(left.ProjectKey, right.ProjectKey, StringComparison.OrdinalIgnoreCase);
}

public sealed record RuntimePublicSessionHeartbeatRequest(Guid SessionId, string ClientInstanceId);
