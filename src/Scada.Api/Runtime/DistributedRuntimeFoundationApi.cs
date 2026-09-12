using Scada.Api.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public static class DistributedRuntimeFoundationApi
{
    public const string ContractSchema = "elitescada.server-runtime-contract";
    public const int ContractSchemaVersion = 2;

    public static IEndpointRouteBuilder MapDistributedRuntimeFoundationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/runtime/sessions", async (
            RuntimeSessionAdmissionRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken cancellationToken) =>
        {
            var principal = security.GetPrincipal(context);
            if (!security.AuthenticationEnabled ||
                !principal.IsAuthenticated ||
                string.IsNullOrWhiteSpace(principal.SubjectId))
            {
                return Results.Unauthorized();
            }

            if (!RuntimeSessionLeaseRegistry.TryParseConnectionClass(
                    request.ConnectionClass,
                    out var connectionClass))
            {
                return Results.BadRequest(new
                {
                    error = "Runtime connectionClass must be 'viewer' or 'interactive'."
                });
            }

            if (string.IsNullOrWhiteSpace(request.ClientInstanceId) ||
                request.ClientInstanceId.Trim().Length > RuntimeSessionLeaseRegistry.MaximumClientInstanceIdLength)
            {
                return Results.BadRequest(new
                {
                    error = $"clientInstanceId is required and must not exceed {RuntimeSessionLeaseRegistry.MaximumClientInstanceIdLength} characters."
                });
            }

            // Admission is evaluated against canonical Runtime Authority before any requested
            // connection-class reduction. A Runtime lease can never manufacture View authority.
            var admission = await security.CheckRuntimeAsync(
                principal,
                runtime,
                SecurityCapability.View,
                cancellationToken: cancellationToken);
            var admissionFailure = admission.FailureResult();
            if (admissionFailure is not null) return admissionFailure;

            var before = runtime.Describe();
            var lease = security.RuntimeSessions.Admit(
                principal.SubjectId,
                request.ClientInstanceId,
                connectionClass,
                before);
            var after = runtime.Describe();
            if (!SameRuntime(before, after))
            {
                _ = security.RuntimeSessions.Terminate(
                    lease.SessionId,
                    lease.UserId,
                    lease.ClientInstanceId,
                    before);
                return RuntimeChanged();
            }

            return Results.Created(
                $"/api/runtime/sessions/{lease.SessionId}",
                ProjectLease(lease));
        });

        endpoints.MapPost("/api/runtime/sessions/{sessionId:guid}/heartbeat", async (
            Guid sessionId,
            RuntimeSessionHeartbeatRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken cancellationToken) =>
        {
            var principal = security.GetPrincipal(context);
            if (!security.AuthenticationEnabled ||
                !principal.IsAuthenticated ||
                string.IsNullOrWhiteSpace(principal.SubjectId))
            {
                return Results.Unauthorized();
            }

            var currentAuthority = await security.CheckRuntimeAsync(
                principal,
                runtime,
                SecurityCapability.View,
                cancellationToken: cancellationToken);
            var authorityFailure = currentAuthority.FailureResult();
            if (authorityFailure is not null) return authorityFailure;

            var validation = security.RuntimeSessions.Heartbeat(
                sessionId,
                principal.SubjectId,
                request.ClientInstanceId,
                runtime.Describe());
            return validation.IsValid && validation.Lease is not null
                ? Results.Ok(ProjectLease(validation.Lease))
                : LeaseFailure(validation);
        });

        endpoints.MapPost("/api/runtime/sessions/{sessionId:guid}/terminate", (
            Guid sessionId,
            RuntimeSessionTerminationRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security) =>
        {
            var principal = security.GetPrincipal(context);
            if (!security.AuthenticationEnabled ||
                !principal.IsAuthenticated ||
                string.IsNullOrWhiteSpace(principal.SubjectId))
            {
                return Results.Unauthorized();
            }

            var validation = security.RuntimeSessions.Terminate(
                sessionId,
                principal.SubjectId,
                request.ClientInstanceId,
                runtime.Describe());
            return validation.IsValid
                ? Results.NoContent()
                : LeaseFailure(validation);
        });

        endpoints.MapGet("/api/runtime/contract", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken cancellationToken) =>
        {
            if (security.AuthenticationEnabled)
            {
                var authorization = await security.CheckRuntimeAsync(
                    context,
                    runtime,
                    SecurityCapability.View,
                    cancellationToken: cancellationToken);
                var failure = authorization.FailureResult();
                if (failure is not null) return failure;
            }

            var before = runtime.Describe();
            var effectiveCapabilities = new List<string>();
            foreach (var capability in Enum.GetValues<SecurityCapability>())
            {
                if (!security.AuthenticationEnabled)
                {
                    effectiveCapabilities.Add(AuthorityPolicyContract.GetCapabilityId(capability));
                    continue;
                }

                var check = await security.CheckRuntimeAsync(
                    context,
                    runtime,
                    capability,
                    cancellationToken: cancellationToken);
                if (check.Allowed)
                    effectiveCapabilities.Add(AuthorityPolicyContract.GetCapabilityId(capability));
            }

            var after = runtime.Describe();
            if (!SameRuntime(before, after)) return RuntimeChanged();

            return Results.Ok(new
            {
                schema = ContractSchema,
                schemaVersion = ContractSchemaVersion,
                authorityPolicy = new
                {
                    schema = AuthorityPolicyContract.Schema,
                    schemaVersion = AuthorityPolicyContract.SchemaVersion,
                    capabilityIds = AuthorityPolicyContract.CapabilityIds
                },
                applicationAuthority = "backend-active-revision",
                runtime = new
                {
                    before.Mode,
                    before.ProjectKey,
                    before.Revision,
                    before.ActivatedAtUtc
                },
                canonicalBoundary = new
                {
                    tagValues = "tag-engine/current-cache",
                    eventDistribution = "event-bus",
                    directDriverAccess = false
                },
                domains = new[]
                {
                    "application",
                    "visual-assets",
                    "realtime-tags",
                    "historian",
                    "alarms",
                    "operational-events",
                    "commands",
                    "process-value-write"
                },
                sessionLease = new
                {
                    supported = security.AuthenticationEnabled,
                    header = ApiAuthorizationService.RuntimeSessionHeaderName,
                    leaseDurationSeconds = (int)security.RuntimeSessions.LeaseDuration.TotalSeconds,
                    connectionClasses = new[] { "viewer", "interactive" }
                },
                effectiveCapabilities
            });
        });

        return endpoints;
    }

    private static object ProjectLease(RuntimeSessionLease lease) => new
    {
        lease.SessionId,
        lease.UserId,
        lease.ClientInstanceId,
        connectionClass = lease.ConnectionClass.ToString().ToLowerInvariant(),
        lease.IssuedAtUtc,
        lease.LastHeartbeatUtc,
        lease.ExpiresAtUtc,
        lease.ServerNode,
        lease.ClusterId,
        runtime = new
        {
            mode = lease.RuntimeMode,
            projectKey = lease.RuntimeProjectKey,
            revision = lease.RuntimeRevision,
            activatedAtUtc = lease.RuntimeActivatedAtUtc
        }
    };

    private static IResult LeaseFailure(RuntimeSessionLeaseValidation validation)
    {
        var statusCode = validation.FailureCode switch
        {
            "session-not-found" => StatusCodes.Status404NotFound,
            "session-user-mismatch" or "session-client-mismatch" => StatusCodes.Status403Forbidden,
            "session-expired" or "runtime-changed" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status409Conflict
        };

        return Results.Json(
            new
            {
                error = "Runtime session lease is not valid.",
                code = validation.FailureCode ?? "unknown"
            },
            statusCode: statusCode);
    }

    private static bool SameRuntime(ScadaRuntimeDescriptor left, ScadaRuntimeDescriptor right) =>
        left.Revision == right.Revision &&
        left.ActivatedAtUtc == right.ActivatedAtUtc &&
        left.Mode.Equals(right.Mode, StringComparison.Ordinal) &&
        string.Equals(left.ProjectKey, right.ProjectKey, StringComparison.OrdinalIgnoreCase);

    private static IResult RuntimeChanged() => Results.Conflict(new
    {
        error = "Active Runtime changed while the server Runtime contract/session was being resolved. Retry against the new Runtime revision."
    });
}
