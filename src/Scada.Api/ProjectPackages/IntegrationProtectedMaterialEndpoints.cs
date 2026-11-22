using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Drivers.Abstractions;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.ProjectPackages;

public static class IntegrationProtectedMaterialEndpoints
{
    private const string ResourceKind = "data-source-protected-material";
    private const int MaximumMaterialBytes = 1024 * 1024;

    public static IEndpointRouteBuilder MapIntegrationProtectedMaterialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/engineering/data-sources/{id:guid}/protected-material/{purpose}", async (
            Guid id,
            string purpose,
            HttpRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IntegrationProtectedMaterialService protectedMaterial,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(context, authorization,
                    "engineering.data_source.protected_material.configure", ResourceKind, id.ToString("D"));
                return failure;
            }

            if (!TryReadExpectedChangeVersion(request, out var expectedVersion))
                return Results.BadRequest(new { error = "Engineering Workspace version header is required and must be a non-negative integer." });
            var source = workspace.DataSources.Find(id);
            if (source is null) return Results.NotFound();
            var field = IntegrationProtectedMaterialScopePolicy.SecretField(source.Driver, purpose);
            if (field is null)
                return Results.BadRequest(new { error = "This Data Source does not support the requested protected-material purpose." });
            if (string.IsNullOrWhiteSpace(workspace.Describe().ProjectKey))
                return Results.Conflict(new { error = "A stable project key is required to provision integration material." });
            if (!string.Equals(request.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
                request.ContentLength is not { } contentLength || contentLength is < 1 or > MaximumMaterialBytes)
                return Results.BadRequest(new { error = "Send 1 byte to 1 MiB of raw material as application/octet-stream." });

            var material = new byte[contentLength];
            try
            {
                try
                {
                    await request.Body.ReadExactlyAsync(material, cancellationToken);
                }
                catch (EndOfStreamException)
                {
                    return Results.BadRequest(new { error = "Protected material body ended before the declared content length." });
                }

                await using var workspaceMutation = await workspace.AcquireMutationAsync(expectedVersion, cancellationToken);
                var current = workspace.DataSources.Find(id);
                if (current is null) return Results.NotFound();
                if (!string.Equals(current.Driver, source.Driver, StringComparison.OrdinalIgnoreCase) ||
                    IntegrationProtectedMaterialScopePolicy.SecretField(current.Driver, purpose) != field)
                    return Results.Conflict(new { error = "Data Source driver changed before the protected material could be provisioned." });

                var scope = HostAuthorityDriverMaterialResolver.Scope(
                    workspace.Describe().ProjectKey!, id, current.Driver, purpose);
                await protectedMaterial.ConfigureAsync(
                    scope,
                    material,
                    new ProtectedMaterialMutationContext(authorization.Principal, authorization.Decision!),
                    cancellationToken);

                var references = current.SecretReferences is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(current.SecretReferences, StringComparer.OrdinalIgnoreCase);
                references[field] = HostMaterialSlotReference.Marker;
                var updated = current with { SecretReferences = references };
                workspace.DataSources.Upsert(updated);
                await audit.RecordAsync(context, authorization.Principal,
                    "engineering.data_source.protected_material.configure", AuditOutcome.Succeeded,
                    ResourceKind, id.ToString("D"), new Dictionary<string, string>
                    {
                        ["driverType"] = current.Driver,
                        ["purpose"] = purpose
                    });
                return Results.Ok(new
                {
                    dataSource = updated,
                    workspaceVersion = workspace.CaptureChangeVersion(),
                    configured = true
                });
            }
            catch (EngineeringWorkspaceVersionConflictException conflict)
            {
                return Results.Conflict(new
                {
                    error = "The project changed before protected material could be provisioned. Reload and try again.",
                    conflict.ExpectedChangeVersion,
                    conflict.CurrentChangeVersion
                });
            }
            catch (ProtectedMaterialException exception)
            {
                return exception.Code switch
                {
                    ProtectedMaterialErrorCodes.Unauthorized => Results.StatusCode(StatusCodes.Status403Forbidden),
                    ProtectedMaterialErrorCodes.KeyUnavailable or ProtectedMaterialErrorCodes.StoreUnavailable =>
                        Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                            title: "Protected integration material storage is unavailable.",
                            extensions: new Dictionary<string, object?> { ["code"] = exception.Code }),
                    _ => Results.BadRequest(new { error = "Protected integration material could not be applied.", code = exception.Code })
                };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Protected integration material storage is unavailable.",
                    extensions: new Dictionary<string, object?> { ["code"] = ProtectedMaterialErrorCodes.StoreUnavailable });
            }
            finally
            {
                CryptographicOperations.ZeroMemory(material);
            }
        }).WithMetadata(new RequestSizeLimitAttribute(MaximumMaterialBytes));

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
}
