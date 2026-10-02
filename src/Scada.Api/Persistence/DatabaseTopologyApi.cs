using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Persistence;

public static class DatabaseTopologyApi
{
    public static void MapDatabaseTopologyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/database-topology");

        group.MapGet("/", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            DatabaseTopologyAdministrationService service,
            bool? refresh,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, cancellationToken);
            if (failure is not null) return failure;
            return Results.Ok(await service.GetStatusAsync(refresh ?? false, cancellationToken));
        });

        group.MapPost("/test", async (
            DatabaseConnectionTestRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            DatabaseTopologyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, cancellationToken);
            if (failure is not null) return failure;
            try
            {
                return Results.Ok(await service.TestConnectionAsync(
                    request.Endpoint,
                    request.RequireTimescale,
                    cancellationToken));
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "Database endpoint configuration is invalid." });
            }
        });

        group.MapPost("/compatibility", async (
            DatabaseRemoteProfileRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            DatabaseTopologyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, cancellationToken);
            if (failure is not null) return failure;
            try
            {
                return Results.Ok(await service.ValidateCompatibilityAsync(request, cancellationToken));
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "Database profile configuration is invalid." });
            }
        });

        group.MapPost("/prepare", async (
            DatabaseRemoteProfileRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            DatabaseTopologyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, cancellationToken);
            if (failure is not null) return failure;
            try
            {
                return Results.Ok(await service.PrepareAsync(request, cancellationToken));
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "Database profile configuration is invalid." });
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { error = "Database target could not be prepared safely." });
            }
        });

        group.MapPost("/operations/{operationId:guid}/copy", async (
            Guid operationId,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            DatabaseTopologyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, cancellationToken);
            if (failure is not null) return failure;
            try
            {
                return Results.Ok(await service.StartMigrationAsync(operationId, cancellationToken));
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { error = "Database migration cannot enter copy phase from its current state." });
            }
            catch
            {
                return Results.Json(
                    new { error = "Database copy failed. Active topology was not changed." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        group.MapPost("/operations/{operationId:guid}/verify", async (
            Guid operationId,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            DatabaseTopologyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, cancellationToken);
            if (failure is not null) return failure;
            try
            {
                var verification = await service.VerifyAsync(operationId, cancellationToken);
                return verification.Succeeded
                    ? Results.Ok(verification)
                    : Results.Json(verification, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { error = "Database migration cannot be verified from its current state." });
            }
            catch
            {
                return Results.Json(
                    new { error = "Database verification failed. Active topology was not changed." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        group.MapPost("/operations/{operationId:guid}/commit", async (
            Guid operationId,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            DatabaseTopologyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, cancellationToken);
            if (failure is not null) return failure;
            try
            {
                var result = await service.CommitCutoverAsync(operationId, cancellationToken);
                return result.Succeeded
                    ? Results.Ok(result)
                    : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { error = "Database cutover cannot be committed from its current state." });
            }
        });

        group.MapPost("/rollback", async (
            DatabaseRollbackRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            DatabaseTopologyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, cancellationToken);
            if (failure is not null) return failure;
            try
            {
                return Results.Ok(await service.RollbackAsync(request.OperationId, cancellationToken));
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { error = "No matching database topology rollback is available." });
            }
        });

        app.MapGet("/ready", async (
            DatabaseTopologyAdministrationService service,
            DatabaseRuntimeConnectionSet runtimeConnections,
            CancellationToken cancellationToken) =>
        {
            if (!runtimeConnections.RequireDurable && !runtimeConnections.HasDurablePrimary)
                return Results.Ok(new { status = "ready", database = "optional" });

            var status = await service.GetStatusAsync(refreshHealth: true, cancellationToken);
            var ready = status.PrimaryHealth is { Reachable: true, FailureCode: null };
            return ready
                ? Results.Ok(new { status = "ready", database = "reachable" })
                : Results.Json(
                    new { status = "not-ready", database = "unreachable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });
    }

    private static async Task<IResult?> AuthorizeAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        ApiAuthorizationService security,
        CancellationToken cancellationToken)
    {
        var authorization = await security.CheckRuntimeAsync(
            context,
            runtime,
            SecurityCapability.SystemAdmin,
            cancellationToken: cancellationToken);
        return authorization.FailureResult();
    }
}

public sealed record DatabaseConnectionTestRequest(
    DatabaseRemoteEndpointRequest Endpoint,
    bool RequireTimescale = false);

public sealed record DatabaseRollbackRequest(Guid? OperationId = null);
