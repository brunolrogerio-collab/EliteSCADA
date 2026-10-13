using System.Globalization;
using Scada.Api.Engineering;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Drivers.Abstractions;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.ProjectPackages;

public static class MatterWifiProtectedMaterialEndpoints
{
    private const string ResourceKind = "matter-wifi-protected-material";

    internal static IEndpointRouteBuilder MapMatterWifiProtectedMaterialEndpointsIfPreviewEnabled(
        this IEndpointRouteBuilder endpoints)
    {
        var environment = endpoints.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var configuration = endpoints.ServiceProvider.GetRequiredService<IConfiguration>();
        if (EngineeringDriverCatalogApi.IsMatterWifiPreviewEnabled(environment.EnvironmentName, configuration))
            endpoints.MapMatterWifiProtectedMaterialEndpoints();
        return endpoints;
    }

    private static IEndpointRouteBuilder MapMatterWifiProtectedMaterialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/engineering/data-sources/{id:guid}/matter-wifi/fabric-storage-key", async (
            Guid id,
            HttpRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            MatterWifiProtectedMaterialService protectedMaterial,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(context, authorization,
                    "engineering.data_source.matter_wifi.fabric_key.ensure", ResourceKind, id.ToString("D"));
                return failure;
            }

            if (!TryReadExpectedChangeVersion(request, out var expectedVersion))
                return Results.BadRequest(new { error = "Engineering Workspace version header is required and must be a non-negative integer." });
            if (request.ContentLength is > 0)
                return Results.BadRequest(new { error = "This operation generates its key on the host and does not accept request-body material." });

            var source = workspace.DataSources.Find(id);
            if (source is null) return Results.NotFound();
            if (!string.Equals(source.Driver, MatterWifiProtectedMaterial.DriverType, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "The Data Source is not a Matter Wi-Fi driver." });
            var projectKey = workspace.Describe().ProjectKey;
            if (string.IsNullOrWhiteSpace(projectKey))
                return Results.Conflict(new { error = "A stable project key is required to provision Matter fabric state." });

            try
            {
                await using var workspaceMutation = await workspace.AcquireMutationAsync(expectedVersion, cancellationToken);
                var current = workspace.DataSources.Find(id);
                if (current is null) return Results.NotFound();
                if (!string.Equals(current.Driver, MatterWifiProtectedMaterial.DriverType, StringComparison.OrdinalIgnoreCase))
                    return Results.Conflict(new { error = "Data Source driver changed before its Matter key could be provisioned." });

                var references = current.SecretReferences is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(current.SecretReferences, StringComparer.OrdinalIgnoreCase);
                var hasMarker = references.TryGetValue(MatterWifiProtectedMaterial.SecretField, out var marker);
                if (hasMarker && !string.Equals(marker, HostMaterialSlotReference.Marker, StringComparison.Ordinal))
                    return Results.Conflict(new { error = "Matter fabric key uses an unsupported reference; refusing to replace it implicitly." });

                var scope = MatterWifiProtectedMaterial.Scope(projectKey, id);
                await protectedMaterial.EnsureProvisionedAsync(
                    scope,
                    allowCreate: !hasMarker,
                    new ProtectedMaterialMutationContext(authorization.Principal, authorization.Decision!),
                    cancellationToken);

                references[MatterWifiProtectedMaterial.SecretField] = HostMaterialSlotReference.Marker;
                var updated = current with { SecretReferences = references };
                workspace.DataSources.Upsert(updated);
                await audit.RecordAsync(context, authorization.Principal,
                    "engineering.data_source.matter_wifi.fabric_key.ensure", AuditOutcome.Succeeded,
                    ResourceKind, id.ToString("D"), new Dictionary<string, string>
                    {
                        ["driverType"] = current.Driver,
                        ["action"] = "ensure-host-key",
                        ["keyMaterialReturned"] = "false"
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
                    error = "The project changed before the Matter key could be provisioned. Reload and try again.",
                    conflict.ExpectedChangeVersion,
                    conflict.CurrentChangeVersion
                });
            }
            catch (ProtectedMaterialException exception)
            {
                return exception.Code switch
                {
                    ProtectedMaterialErrorCodes.Unauthorized => Results.StatusCode(StatusCodes.Status403Forbidden),
                    ProtectedMaterialErrorCodes.CredentialRequired or ProtectedMaterialErrorCodes.ProtectedMaterialUnreadable =>
                        Results.Conflict(new { error = "Existing Matter fabric key is missing or invalid; it will not be replaced automatically.", code = exception.Code }),
                    ProtectedMaterialErrorCodes.KeyUnavailable or ProtectedMaterialErrorCodes.StoreUnavailable =>
                        Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                            title: "Matter protected-material storage is unavailable.",
                            extensions: new Dictionary<string, object?> { ["code"] = exception.Code }),
                    _ => Results.BadRequest(new { error = "Matter fabric key could not be provisioned.", code = exception.Code })
                };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Matter protected-material storage is unavailable.",
                    extensions: new Dictionary<string, object?> { ["code"] = ProtectedMaterialErrorCodes.StoreUnavailable });
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
}
