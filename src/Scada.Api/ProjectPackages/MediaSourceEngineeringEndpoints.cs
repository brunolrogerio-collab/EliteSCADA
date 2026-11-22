using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.Media;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.ProjectPackages;

public static class MediaSourceEngineeringEndpoints
{
    private const string ResourceKind = "media-source";

    public static IEndpointRouteBuilder MapMediaSourceEngineeringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/engineering/media-sources", (IMediaSourceEngineeringRegistry sources) =>
            Results.Ok(sources.Snapshot()))
            .RequireWorkspaceEngineeringRead();

        endpoints.MapGet("/api/engineering/media-sources/{id:guid}", (
            Guid id,
            IMediaSourceEngineeringRegistry sources) =>
                sources.Find(id) is { } source ? Results.Ok(source) : Results.NotFound())
            .RequireWorkspaceEngineeringRead();

        endpoints.MapGet("/api/engineering/media-sources/{id:guid}/credential-state", async (
            Guid id,
            HttpContext context,
            EngineeringWorkspace workspace,
            IMediaSourceEngineeringRegistry sources,
            MediaSourceProtectedCredentialService credentials,
            CancellationToken cancellationToken) =>
        {
            if (sources.Find(id) is null) return Results.NotFound();
            if (!TryCredentialScope(workspace, id, out var scope))
                return Results.Conflict(new { error = "A stable project key is required to manage media credentials." });
            try
            {
                var state = await credentials.GetPublicStateAsync(scope, cancellationToken);
                return Results.Ok(state);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return CredentialStoreUnavailable();
            }
        }).RequireWorkspaceEngineeringRead();

        endpoints.MapPut("/api/engineering/media-sources/{id:guid}/credential", async (
            Guid id,
            MediaSourceCredentialRequest? body,
            HttpContext context,
            EngineeringWorkspace workspace,
            IMediaSourceEngineeringRegistry sources,
            MediaSourceProtectedCredentialService credentials,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(context, authorization, "engineering.media_source.credential.configure",
                    ResourceKind, id.ToString("D"));
                return failure;
            }
            if (sources.Find(id) is null) return Results.NotFound();
            if (body is null) return Results.BadRequest(new { error = "A media source credential is required." });
            var validation = body.Validate();
            if (validation is not null) return Results.BadRequest(new { error = validation });
            if (!TryCredentialScope(workspace, id, out var scope))
                return Results.Conflict(new { error = "A stable project key is required to manage media credentials." });

            try
            {
                await credentials.ConfigureAsync(
                    scope,
                    body,
                    new ProtectedMaterialMutationContext(authorization.Principal, authorization.Decision!),
                    cancellationToken);
                return Results.Ok(new ProtectedMaterialPublicState(true));
            }
            catch (ProtectedMaterialException exception)
            {
                return ProtectedMaterialFailure(exception);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return CredentialStoreUnavailable();
            }
        }).WithMetadata(new RequestSizeLimitAttribute(32 * 1024));

        endpoints.MapDelete("/api/engineering/media-sources/{id:guid}/credential", async (
            Guid id,
            HttpContext context,
            EngineeringWorkspace workspace,
            IMediaSourceEngineeringRegistry sources,
            MediaSourceProtectedCredentialService credentials,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(context, authorization, "engineering.media_source.credential.delete",
                    ResourceKind, id.ToString("D"));
                return failure;
            }
            if (sources.Find(id) is null) return Results.NotFound();
            if (!TryCredentialScope(workspace, id, out var scope))
                return Results.Conflict(new { error = "A stable project key is required to manage media credentials." });
            try
            {
                await credentials.DeleteAsync(
                    scope,
                    new ProtectedMaterialMutationContext(authorization.Principal, authorization.Decision!),
                    cancellationToken);
                return Results.NoContent();
            }
            catch (ProtectedMaterialException exception)
            {
                return ProtectedMaterialFailure(exception);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return CredentialStoreUnavailable();
            }
        });

        endpoints.MapPost("/api/engineering/media-sources", async (
            MediaSourceEngineeringDto? body,
            HttpRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IMediaSourceEngineeringRegistry sources,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(context, authorization, "engineering.media_source.create",
                    ResourceKind, body?.Key ?? "new");
                return failure;
            }
            if (!TryReadExpectedChangeVersion(request, out var expectedVersion))
                return Results.BadRequest(new { error = "Engineering Workspace version header is required and must be a non-negative integer." });
            if (body is null) return Results.BadRequest(new { error = "A media source definition is required." });

            var candidate = body with { Id = body.Id ?? Guid.NewGuid() };
            var issues = MediaSourceEngineeringValidation.Validate(candidate);
            if (issues.Count > 0)
                return Results.ValidationProblem(issues.GroupBy(issue => issue.Code)
                    .ToDictionary(group => group.Key, group => group.Select(issue => issue.Message).ToArray()));
            if (candidate.Id.HasValue && sources.Find(candidate.Id.Value) is not null)
                return Results.Conflict(new { error = "A media source with this ID already exists." });
            if (sources.FindByKey(candidate.Key) is not null)
                return Results.Conflict(new { error = "A media source with this key already exists." });

            try
            {
                await using var mutation = await workspace.AcquireMutationAsync(expectedVersion, cancellationToken);
                if (sources.Find(candidate.Id!.Value) is not null || sources.FindByKey(candidate.Key) is not null)
                    return Results.Conflict(new { error = "A media source with this ID or key already exists." });
                sources.Upsert(candidate);
                await audit.RecordAsync(context, authorization.Principal, "engineering.media_source.create", AuditOutcome.Succeeded,
                    ResourceKind, candidate.Id!.Value.ToString("D"), new Dictionary<string, string> { ["key"] = candidate.Key });
                return Results.Created($"/api/engineering/media-sources/{candidate.Id:D}", candidate);
            }
            catch (EngineeringWorkspaceVersionConflictException conflict)
            {
                return Results.Conflict(new { error = "The project changed before the media source could be created. Reload and try again.", conflict.ExpectedChangeVersion, conflict.CurrentChangeVersion });
            }
        });

        endpoints.MapPut("/api/engineering/media-sources/{id:guid}", async (
            Guid id,
            MediaSourceEngineeringDto? body,
            HttpRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IMediaSourceEngineeringRegistry sources,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(context, authorization, "engineering.media_source.update",
                    ResourceKind, id.ToString("D"));
                return failure;
            }
            if (!TryReadExpectedChangeVersion(request, out var expectedVersion))
                return Results.BadRequest(new { error = "Engineering Workspace version header is required and must be a non-negative integer." });
            if (body is null) return Results.BadRequest(new { error = "A media source definition is required." });
            var existing = sources.Find(id);
            if (existing is null) return Results.NotFound();

            var candidate = body with { Id = id };
            var issues = MediaSourceEngineeringValidation.Validate(candidate);
            if (issues.Count > 0)
                return Results.ValidationProblem(issues.GroupBy(issue => issue.Code)
                    .ToDictionary(group => group.Key, group => group.Select(issue => issue.Message).ToArray()));
            var byKey = sources.FindByKey(candidate.Key);
            if (byKey is not null && byKey.Id != id)
                return Results.Conflict(new { error = "A media source with this key already exists." });

            try
            {
                await using var mutation = await workspace.AcquireMutationAsync(expectedVersion, cancellationToken);
                if (sources.Find(id) is null) return Results.NotFound();
                var currentByKey = sources.FindByKey(candidate.Key);
                if (currentByKey is not null && currentByKey.Id != id)
                    return Results.Conflict(new { error = "A media source with this key already exists." });
                sources.Upsert(candidate);
                await audit.RecordAsync(context, authorization.Principal, "engineering.media_source.update", AuditOutcome.Succeeded,
                    ResourceKind, id.ToString("D"), new Dictionary<string, string> { ["key"] = candidate.Key });
                return Results.Ok(new { source = candidate, workspaceVersion = workspace.CaptureChangeVersion() });
            }
            catch (EngineeringWorkspaceVersionConflictException conflict)
            {
                return Results.Conflict(new { error = "The project changed before the media source could be updated. Reload and try again.", conflict.ExpectedChangeVersion, conflict.CurrentChangeVersion });
            }
        });

        endpoints.MapDelete("/api/engineering/media-sources/{id:guid}", async (
            Guid id,
            HttpRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IMediaSourceEngineeringRegistry sources,
            ApiAuthorizationService security,
            ApiAuditService audit,
            MediaSourceProtectedCredentialService credentials,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(context, authorization, "engineering.media_source.delete",
                    ResourceKind, id.ToString("D"));
                return failure;
            }
            if (!TryReadExpectedChangeVersion(request, out var expectedVersion))
                return Results.BadRequest(new { error = "Engineering Workspace version header is required and must be a non-negative integer." });
            var existing = sources.Find(id);
            if (existing is null) return Results.NotFound();

            try
            {
                await using var mutation = await workspace.AcquireMutationAsync(expectedVersion, cancellationToken);
                if (sources.Find(id) is null) return Results.NotFound();
                if (!TryCredentialScope(workspace, id, out var scope))
                    return Results.Conflict(new { error = "A stable project key is required to delete this media source." });
                await credentials.DeleteAsync(
                    scope,
                    new ProtectedMaterialMutationContext(authorization.Principal, authorization.Decision!),
                    cancellationToken);
                sources.Remove(id);
                await audit.RecordAsync(context, authorization.Principal, "engineering.media_source.delete", AuditOutcome.Succeeded,
                    ResourceKind, id.ToString("D"), new Dictionary<string, string> { ["key"] = existing.Key });
                return Results.NoContent();
            }
            catch (EngineeringWorkspaceVersionConflictException conflict)
            {
                return Results.Conflict(new { error = "The project changed before the media source could be deleted. Reload and try again.", conflict.ExpectedChangeVersion, conflict.CurrentChangeVersion });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return CredentialStoreUnavailable();
            }
        });

        return endpoints;
    }

    private static bool TryReadExpectedChangeVersion(HttpRequest request, out long expectedVersion)
    {
        expectedVersion = 0;
        return request.Headers.TryGetValue("x-elitescada-workspace-version", out var value) &&
               value.Count == 1 &&
               long.TryParse(value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out expectedVersion) &&
               expectedVersion >= 0;
    }

    private static bool TryCredentialScope(EngineeringWorkspace workspace, Guid sourceId, out ProtectedMaterialScope scope)
    {
        var projectKey = workspace.Describe().ProjectKey;
        if (string.IsNullOrWhiteSpace(projectKey))
        {
            scope = null!;
            return false;
        }
        scope = new("project:" + projectKey, ProtectedMaterialResourceKinds.MediaSource, sourceId.ToString("D"),
            ProtectedMaterialPurposes.ConnectionCredential);
        return true;
    }

    private static IResult ProtectedMaterialFailure(ProtectedMaterialException exception) =>
        exception.Code switch
        {
            ProtectedMaterialErrorCodes.Unauthorized => Results.StatusCode(StatusCodes.Status403Forbidden),
            ProtectedMaterialErrorCodes.KeyUnavailable or ProtectedMaterialErrorCodes.StoreUnavailable =>
                Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Media source credential storage is unavailable.", extensions: new Dictionary<string, object?> { ["code"] = exception.Code }),
            _ => Results.BadRequest(new { error = "Media source credential could not be applied.", code = exception.Code })
        };

    private static IResult CredentialStoreUnavailable() =>
        Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Media source credential storage is unavailable.",
            extensions: new Dictionary<string, object?> { ["code"] = ProtectedMaterialErrorCodes.StoreUnavailable });
}
