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
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.HighAvailabilityObserve,
                cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            highAvailability.RefreshLocalReadiness(
                runtime.Describe(),
                licensing.CurrentVerification);
            return Results.Ok(highAvailability.Authority.Snapshot());
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

        return endpoints;
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
