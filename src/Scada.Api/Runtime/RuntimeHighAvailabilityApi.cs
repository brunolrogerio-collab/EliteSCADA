using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public sealed record RuntimeHaManualTransferBeginRequest(
    string SourceNodeId,
    string TargetNodeId,
    long ExpectedEpoch);

public sealed record RuntimeHaManualTransferCompleteRequest(
    Guid TransferId,
    string TargetNodeId,
    long ExpectedBreakEpoch);

public sealed record RuntimeHaControlledSwitchRequest(string TargetNodeId);

public sealed record RuntimeHaFailbackRequest(string? TargetNodeId = null);

public sealed record RuntimeHaRecoveryRequest(string? TargetNodeId = null);

public static class RuntimeHighAvailabilityApi
{
    public static IEndpointRouteBuilder MapRuntimeHighAvailabilityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/runtime/ha/topology", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            IProductLicenseService licensing,
            RuntimeHighAvailabilityService highAvailability,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(
                context,
                SecurityCapability.HighAvailabilityObserve);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            highAvailability.RefreshLocalReadiness(
                runtime.Describe(),
                licensing.CurrentVerification);
            return Results.Ok(highAvailability.Authority.Snapshot());
        });

        endpoints.MapGet("/api/runtime/ha/configuration", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            RuntimeHaHostConfigurationAuthority configuration,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(
                context,
                SecurityCapability.HighAvailabilityObserve);
            var failure = authorization.FailureResult();
            return failure ?? Results.Ok(configuration.Snapshot());
        });

        endpoints.MapPut("/api/runtime/ha/configuration", async (
            RuntimeHaHostConfigurationUpdateRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IProductLicenseService licensing,
            RuntimeHighAvailabilityService highAvailability,
            RuntimeHaHostConfigurationAuthority configuration,
            CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.HighAvailabilityTransfer,
                cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.HighAvailabilityConfigurationUpdate,
                    "ha-host-configuration",
                    highAvailability.LocalNodeId ?? "standalone");
                return failure;
            }

            var licenseFailure = HaRuntimeLicenseFailure(licensing);
            if (licenseFailure is not null) return licenseFailure;

            var result = await configuration.UpdateAsync(request, cancellationToken);
            var details = new Dictionary<string, string>
            {
                ["result"] = result.ReasonCode,
                ["generation"] = result.Snapshot.Generation.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                ["topologyVersion"] = result.Snapshot.Desired.TopologyVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                ["pendingRestart"] = result.Snapshot.PendingRestart.ToString(),
                ["peerAuthenticationConfigured"] =
                    result.Snapshot.Desired.PeerTransport.AuthenticationConfigured.ToString()
            };
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.HighAvailabilityConfigurationUpdate,
                result.Accepted ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                "ha-host-configuration",
                highAvailability.LocalNodeId ?? "standalone",
                details);

            if (result.Accepted)
            {
                return result.Snapshot.PendingRestart
                    ? Results.Accepted("/api/runtime/ha/configuration", result)
                    : Results.Ok(result);
            }

            return result.ReasonCode == "host-configuration-generation-stale"
                ? Results.Conflict(result)
                : Results.BadRequest(result);
        });

        endpoints.MapPost("/api/runtime/ha/transfers/begin", async (
            RuntimeHaManualTransferBeginRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IProductLicenseService licensing,
            RuntimeHighAvailabilityService highAvailability,
            CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.HighAvailabilityTransfer,
                cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.HighAvailabilityTransferBegin,
                    "ha-cluster",
                    highAvailability.ClusterId ?? "standalone");
                return failure;
            }

            var licenseFailure = HaRuntimeLicenseFailure(licensing);
            if (licenseFailure is not null) return licenseFailure;

            highAvailability.RefreshLocalReadiness(
                runtime.Describe(),
                licensing.CurrentVerification);

            if (highAvailability.LocalNodeId is { } localNodeId &&
                !localNodeId.Equals(
                    request.SourceNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new
                {
                    error = "Manual transfer must be initiated by the configured local source node."
                });
            }

            RuntimeHaPeerTransferResult operation;
            RuntimeHaTransitionResult result;
            try
            {
                operation = highAvailability.BeginManualTransfer(
                    request.TargetNodeId,
                    request.ExpectedEpoch);
                result = operation.Transition;
            }
            catch (KeyNotFoundException)
            {
                return Results.BadRequest(new { error = "The requested HA node is not present in the configured topology." });
            }

            var details = TransferDetails(
                request.SourceNodeId,
                request.TargetNodeId,
                request.ExpectedEpoch,
                result);
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.HighAvailabilityTransferBegin,
                result.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                "ha-cluster",
                highAvailability.ClusterId ?? "standalone",
                details);

            return result.Succeeded
                ? Results.Accepted(value: ProjectTransfer(operation))
                : Results.Conflict(ProjectTransfer(operation));
        });

        endpoints.MapPost("/api/runtime/ha/transfers/complete", async (
            RuntimeHaManualTransferCompleteRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IProductLicenseService licensing,
            RuntimeHighAvailabilityService highAvailability,
            CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.HighAvailabilityTransfer,
                cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.HighAvailabilityTransferComplete,
                    "ha-cluster",
                    highAvailability.ClusterId ?? "standalone");
                return failure;
            }

            var licenseFailure = HaRuntimeLicenseFailure(licensing);
            if (licenseFailure is not null) return licenseFailure;

            highAvailability.RefreshLocalReadiness(
                runtime.Describe(),
                licensing.CurrentVerification);

            RuntimeHaPeerTransferResult operation;
            RuntimeHaTransitionResult result;
            try
            {
                operation = highAvailability.CompleteManualTransfer(
                    request.TransferId,
                    request.TargetNodeId,
                    request.ExpectedBreakEpoch);
                result = operation.Transition;
            }
            catch (KeyNotFoundException)
            {
                return Results.BadRequest(new { error = "The requested HA node is not present in the configured topology." });
            }

            var details = new Dictionary<string, string>
            {
                ["transferId"] = request.TransferId.ToString("D"),
                ["targetNodeId"] = request.TargetNodeId,
                ["expectedBreakEpoch"] = request.ExpectedBreakEpoch.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                ["result"] = result.ReasonCode
            };
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.HighAvailabilityTransferComplete,
                result.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                "ha-cluster",
                highAvailability.ClusterId ?? "standalone",
                details);

            return result.Succeeded
                ? Results.Ok(ProjectTransfer(operation))
                : Results.Conflict(ProjectTransfer(operation));
        });

        endpoints.MapGet("/api/runtime/ha/authority", (
            HttpContext context,
            ApiAuthorizationService security,
            RuntimeHighAvailabilityService highAvailability,
            RuntimeHaProtectionCoordinator protection) =>
        {
            var authorization = security.CheckWorkspace(
                context,
                SecurityCapability.View);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            var topology = highAvailability.Snapshot();
            var active = topology.EffectiveActiveNodeId is null
                ? null
                : topology.Nodes.SingleOrDefault(node =>
                    node.NodeId.Equals(
                        topology.EffectiveActiveNodeId,
                        StringComparison.OrdinalIgnoreCase));
            var protectionState = protection.Diagnostics();
            return Results.Ok(new
            {
                schema = "elitescada.runtime-ha-authority",
                schemaVersion = 1,
                topology.Enabled,
                topology.ClusterId,
                topology.TopologyVersion,
                topology.AuthorityEpoch,
                topology.EffectiveActiveNodeId,
                topology.AmbiguousAuthority,
                blocked = topology.AmbiguousAuthority ||
                    topology.PendingTransfer is not null ||
                    (protectionState.Enabled &&
                     protectionState.Status is "blocked" or "degraded"),
                activeEndpoints = active?.Endpoints ?? Array.Empty<RuntimeHaEndpoint>(),
                protectionStatus = protectionState.Status,
                generatedAtUtc = topology.GeneratedAtUtc
            });
        });

        endpoints.MapGet("/api/runtime/ha/administration", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            RuntimeHaProtectionCoordinator protection,
            RuntimeHaPeerReplicationCoordinator peer,
            RuntimeHaHostConfigurationAuthority configuration,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(
                context,
                SecurityCapability.HighAvailabilityObserve);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            return Results.Ok(new
            {
                schema = "elitescada.runtime-ha-administration",
                schemaVersion = 1,
                protection = protection.Diagnostics(),
                peer = peer.Diagnostics(),
                configuration = configuration.Snapshot(),
                operations = protection.Operations()
            });
        });

        endpoints.MapGet("/api/runtime/ha/operations/{operationId:guid}", async (
            Guid operationId,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            RuntimeHaProtectionCoordinator protection,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(
                context,
                SecurityCapability.HighAvailabilityObserve);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            var operation = protection.GetOperation(operationId);
            return operation is null ? Results.NotFound() : Results.Ok(operation);
        });

        endpoints.MapPost("/api/runtime/ha/actions/switchover", async (
            RuntimeHaControlledSwitchRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IProductLicenseService licensing,
            RuntimeHighAvailabilityService highAvailability,
            RuntimeHaProtectionCoordinator protection,
            CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.HighAvailabilityTransfer,
                cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.HighAvailabilityTransferBegin,
                    "ha-cluster",
                    highAvailability.ClusterId ?? "standalone");
                return failure;
            }

            var licenseFailure = HaRuntimeLicenseFailure(licensing);
            if (licenseFailure is not null) return licenseFailure;

            var operation = await protection.RequestControlledSwitchAsync(
                request.TargetNodeId,
                "controlled-switchover",
                cancellationToken);
            await AuditProtectionOperationAsync(
                audit,
                context,
                authorization.Principal,
                highAvailability,
                operation);
            return operation.State == "completed"
                ? Results.Accepted($"/api/runtime/ha/operations/{operation.OperationId:D}", operation)
                : Results.Conflict(operation);
        });

        endpoints.MapPost("/api/runtime/ha/actions/failback", async (
            RuntimeHaFailbackRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IProductLicenseService licensing,
            RuntimeHighAvailabilityService highAvailability,
            RuntimeHaProtectionCoordinator protection,
            CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.HighAvailabilityTransfer,
                cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.HighAvailabilityTransferBegin,
                    "ha-cluster",
                    highAvailability.ClusterId ?? "standalone");
                return failure;
            }

            var licenseFailure = HaRuntimeLicenseFailure(licensing);
            if (licenseFailure is not null) return licenseFailure;

            var target = string.IsNullOrWhiteSpace(request.TargetNodeId)
                ? highAvailability.Authority.Definition.InitialActiveNodeId
                : request.TargetNodeId.Trim();
            if (string.IsNullOrWhiteSpace(target))
                return Results.BadRequest(new { error = "No failback target is configured." });

            var operation = await protection.RequestControlledSwitchAsync(
                target,
                "explicit-failback",
                cancellationToken);
            await AuditProtectionOperationAsync(
                audit,
                context,
                authorization.Principal,
                highAvailability,
                operation);
            return operation.State == "completed"
                ? Results.Accepted($"/api/runtime/ha/operations/{operation.OperationId:D}", operation)
                : Results.Conflict(operation);
        });

        endpoints.MapPost("/api/runtime/ha/actions/recovery", async (
            RuntimeHaRecoveryRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IProductLicenseService licensing,
            RuntimeHighAvailabilityService highAvailability,
            RuntimeHaProtectionCoordinator protection,
            CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.HighAvailabilityTransfer,
                cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.HighAvailabilityTransferBegin,
                    "ha-cluster",
                    highAvailability.ClusterId ?? "standalone");
                return failure;
            }

            var licenseFailure = HaRuntimeLicenseFailure(licensing);
            if (licenseFailure is not null) return licenseFailure;

            var operation = await protection.RequestRecoveryAsync(
                request.TargetNodeId,
                cancellationToken);
            await AuditProtectionOperationAsync(
                audit,
                context,
                authorization.Principal,
                highAvailability,
                operation);
            return operation.State == "completed"
                ? Results.Accepted($"/api/runtime/ha/operations/{operation.OperationId:D}", operation)
                : Results.Conflict(operation);
        });

        return endpoints;
    }

    private static async Task AuditProtectionOperationAsync(
        ApiAuditService audit,
        HttpContext context,
        SecurityPrincipal principal,
        RuntimeHighAvailabilityService highAvailability,
        RuntimeHaProtectionOperation operation)
    {
        var details = new Dictionary<string, string>
        {
            ["operationId"] = operation.OperationId.ToString("D"),
            ["kind"] = operation.Kind,
            ["state"] = operation.State,
            ["sourceNodeId"] = operation.SourceNodeId ?? string.Empty,
            ["targetNodeId"] = operation.TargetNodeId ?? string.Empty,
            ["epoch"] = operation.Epoch?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            ["result"] = operation.ReasonCode
        };
        await audit.RecordAsync(
            context,
            principal,
            AuditActions.HighAvailabilityTransferComplete,
            operation.State == "completed" ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            "ha-cluster",
            highAvailability.ClusterId ?? "standalone",
            details);
    }

    private static IResult? HaRuntimeLicenseFailure(IProductLicenseService licensing)
    {
        return RuntimeHaLicensePolicy.IsEntitled(licensing.CurrentVerification)
            ? null
            : Results.Json(new
            {
                code = "ha-runtime-license-required",
                error = "A valid product license with the HA Runtime entitlement is required to save, apply, or operate High Availability."
            }, statusCode: StatusCodes.Status403Forbidden);
    }

    private static object ProjectTransfer(RuntimeHaPeerTransferResult operation) =>
        new
        {
            operation.Transition.Succeeded,
            operation.Transition.ReasonCode,
            operation.Transition.Transfer,
            operation.Transition.Snapshot,
            operation.Handoff
        };

    private static IReadOnlyDictionary<string, string> TransferDetails(
        string sourceNodeId,
        string targetNodeId,
        long expectedEpoch,
        RuntimeHaTransitionResult result)
    {
        var details = new Dictionary<string, string>
        {
            ["sourceNodeId"] = sourceNodeId,
            ["targetNodeId"] = targetNodeId,
            ["expectedEpoch"] = expectedEpoch.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["result"] = result.ReasonCode
        };

        if (result.Transfer is not null)
        {
            details["transferId"] = result.Transfer.TransferId.ToString("D");
            details["breakEpoch"] = result.Transfer.BreakEpoch.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        return details;
    }
}

internal static class RuntimeHaLicensePolicy
{
    public static bool IsEntitled(LicenseVerificationResult verification) =>
        verification.State == LicenseState.Valid &&
        verification.SessionEntitlements?.HaRuntime == true;
}
