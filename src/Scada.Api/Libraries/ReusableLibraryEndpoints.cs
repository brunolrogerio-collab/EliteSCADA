using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Libraries;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Libraries;

public sealed record ReusableLibraryAccessDecision(
    ApiAuthorizationCheck Authorization,
    IResult? Failure,
    string? Reason);

public static class ReusableLibraryEndpoints
{
    public const string ExportRoute = "/api/engineering/libraries/export";
    public const string InspectRoute = "/api/engineering/libraries/inspect";

    public static IEndpointRouteBuilder MapReusableLibraryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(ExportRoute, async (
            ReusableLibraryExportRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
        {
            var access = CheckAccess(context, security, exchange);
            if (access.Failure is not null)
            {
                await RecordDeniedAsync(
                    context,
                    audit,
                    access,
                    AuditActions.EngineeringLibraryExport,
                    "reusable-library",
                    request.LibraryId == Guid.Empty ? "unresolved" : request.LibraryId.ToString("D"));
                return access.Failure;
            }

            try
            {
                var service = new ReusableLibraryPackageService(workspace.Assets, workspace.VisualAssets);
                var content = service.Export(request);
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryExport,
                    AuditOutcome.Succeeded,
                    "reusable-library",
                    request.LibraryId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["name"] = request.Name.Trim(),
                        ["version"] = request.Version.Trim(),
                        ["resourceCount"] = request.Resources.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                return Results.File(
                    content,
                    "application/vnd.elitescada.resource-library",
                    $"{SafeFileName(request.Name)}{ReusableLibraryPackageService.PackageExtension}");
            }
            catch (InvalidDataException ex)
            {
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryExport,
                    AuditOutcome.Failed,
                    "reusable-library",
                    request.LibraryId == Guid.Empty ? "unresolved" : request.LibraryId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["reason"] = "invalid-export-request",
                        ["errorType"] = ex.GetType().Name
                    });
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        endpoints.MapPost(InspectRoute, async (
            HttpRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var access = CheckAccess(context, security, exchange);
            if (access.Failure is not null)
            {
                await RecordDeniedAsync(
                    context,
                    audit,
                    access,
                    AuditActions.EngineeringLibraryInspect,
                    "reusable-library",
                    "unresolved");
                return access.Failure;
            }

            try
            {
                var bytes = await ReadLibraryAsync(request, cancellationToken);
                var service = new ReusableLibraryPackageService(workspace.Assets, workspace.VisualAssets);
                var inspection = service.Inspect(bytes);
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryInspect,
                    AuditOutcome.Succeeded,
                    "reusable-library",
                    inspection.Manifest.LibraryId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["name"] = inspection.Manifest.Name,
                        ["version"] = inspection.Manifest.Version,
                        ["resourceCount"] = inspection.Manifest.Resources.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });
                return Results.Ok(inspection);
            }
            catch (InvalidDataException ex)
            {
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryInspect,
                    AuditOutcome.Failed,
                    "reusable-library",
                    "unresolved",
                    new Dictionary<string, string>
                    {
                        ["reason"] = "invalid-library",
                        ["errorType"] = ex.GetType().Name
                    });
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return endpoints;
    }

    internal static ReusableLibraryAccessDecision CheckAccess(
        HttpContext context,
        ApiAuthorizationService security,
        IEngineeringExchangeService exchange)
    {
        var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
        var capabilityFailure = authorization.FailureResult();
        if (capabilityFailure is not null)
            return new ReusableLibraryAccessDecision(authorization, capabilityFailure, "capability");

        var lockFailure = EngineeringLockAccess.ProtectedEngineeringFailure(exchange);
        return lockFailure is null
            ? new ReusableLibraryAccessDecision(authorization, null, null)
            : new ReusableLibraryAccessDecision(authorization, lockFailure, "engineering-lock");
    }

    private static ValueTask RecordDeniedAsync(
        HttpContext context,
        ApiAuditService audit,
        ReusableLibraryAccessDecision access,
        string action,
        string targetKind,
        string targetId)
    {
        if (access.Reason == "capability")
            return audit.RecordAuthorizationDeniedAsync(context, access.Authorization, action, targetKind, targetId);

        return audit.RecordAsync(
            context,
            access.Authorization.Principal,
            action,
            AuditOutcome.Denied,
            targetKind,
            targetId,
            new Dictionary<string, string> { ["reason"] = access.Reason ?? "denied" });
    }

    private static async Task<byte[]> ReadLibraryAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is > ReusableLibraryPackageService.MaximumPackageBytes)
            throw new InvalidDataException("Reusable library request is too large.");

        using var output = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0;
        int read;
        while ((read = await request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > ReusableLibraryPackageService.MaximumPackageBytes)
                throw new InvalidDataException("Reusable library request is too large.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return output.ToArray();
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "elitescada-library" : safe;
    }
}
