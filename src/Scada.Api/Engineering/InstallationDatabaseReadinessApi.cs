using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Engineering;

public sealed record InstallationDatabaseReadiness(bool Ready, bool RestartRequired, bool RecoveryRequired);

public static class InstallationDatabaseReadinessApi
{
    public static void MapInstallationDatabaseReadiness(this WebApplication app)
    {
        app.MapGet("/api/installation/database-readiness", async (
            HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security,
            DatabaseTopologyAdministrationService topology, CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(context, runtime,
                SecurityCapability.SystemAdmin, cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;
            // Reuse the canonical authenticated SQL/compatibility probe.
            var status = await topology.GetStatusAsync(refreshHealth: true, cancellationToken);
            var result = Project(status);
            return Results.Json(result, statusCode: result.Ready ? 200 : 503);
        });
    }

    internal static InstallationDatabaseReadiness Project(DatabaseTopologyStatus status)
    {
        static bool Healthy(DatabaseConnectionHealth? health) =>
            health is { Reachable: true, FailureCode: null, SchemaCompatible: true };
        var ready = Healthy(status.PrimaryHealth) &&
            (status.ActiveTopology.HistorianUsesPrimary || Healthy(status.HistorianHealth)) &&
            !status.RestartRequired && !status.RecoveryRequired && status.PendingOperationId is null;
        return new(ready, status.RestartRequired, status.RecoveryRequired);
    }
}
