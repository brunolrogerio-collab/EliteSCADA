using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.ProjectPackages;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.ProjectPackages;

public static class ProjectPackageEndpoints
{
    private const int MaximumRequestBytes = 64 * 1024 * 1024;

    public static IEndpointRouteBuilder MapProjectPackageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/project-package/export", (
            string? projectKey,
            string? projectName,
            IProjectPackageService packages) =>
        {
            var key = string.IsNullOrWhiteSpace(projectKey) ? "runtime" : projectKey.Trim();
            var name = string.IsNullOrWhiteSpace(projectName) ? "EliteSCADA Runtime" : projectName.Trim();
            var content = packages.Export(key, name);
            return Results.File(
                content,
                "application/vnd.elitescada.project-package",
                $"{SafeFileName(key)}{ProjectPackageService.PackageExtension}");
        });

        endpoints.MapPost("/api/project-package/inspect", async (
            HttpRequest request,
            IProjectPackageService packages,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var bytes = await ReadPackageAsync(request, cancellationToken);
                var inspection = packages.Inspect(bytes);
                return Results.Ok(new
                {
                    inspection.Manifest,
                    engineering = new
                    {
                        inspection.Engineering.Schema,
                        inspection.Engineering.SchemaVersion,
                        tags = inspection.Engineering.Tags.Count,
                        alarms = inspection.Engineering.Alarms.Count,
                        dataSources = inspection.Engineering.DataSources?.Count ?? 0,
                        templates = inspection.Engineering.Templates?.Count ?? 0,
                        equipment = inspection.Engineering.Equipment?.Count ?? 0,
                        dynamos = inspection.Engineering.Dynamos?.Count ?? 0,
                        screens = inspection.Engineering.Screens?.Count ?? 0,
                        popups = inspection.Engineering.Popups?.Count ?? 0,
                        securityRoles = inspection.Engineering.SecurityRoles?.Count ?? 0
                    }
                });
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        endpoints.MapPost("/api/project-package/import/preview", async (
            HttpRequest request,
            ImportMode? mode,
            IProjectPackageService packages,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var bytes = await ReadPackageAsync(request, cancellationToken);
                return Results.Ok(packages.Preview(bytes, mode ?? ImportMode.CreateAndUpdate));
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        endpoints.MapPost("/api/project-package/import/apply", async (
            HttpRequest request,
            HttpContext context,
            ImportMode? mode,
            IProjectPackageService packages,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringPackageRestore,
                    "engineering-workspace",
                    "current");
                return failure;
            }

            try
            {
                var importMode = mode ?? ImportMode.CreateAndUpdate;
                var bytes = await ReadPackageAsync(request, cancellationToken);
                var inspection = packages.Inspect(bytes);
                var preview = packages.Preview(bytes, importMode);
                if (!preview.CanApply)
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        AuditActions.EngineeringPackageRestore,
                        AuditOutcome.Failed,
                        "project-package",
                        inspection.Manifest.ProjectKey,
                        new Dictionary<string, string>
                        {
                            ["packageId"] = inspection.Manifest.PackageId.ToString(),
                            ["reason"] = "preview-errors",
                            ["errorCount"] = preview.ErrorCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        });
                    return Results.BadRequest(preview);
                }

                var result = packages.Apply(bytes, importMode);
                var hasErrors = result.Issues.Any(x => x.IsError);
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringPackageRestore,
                    hasErrors ? AuditOutcome.Failed : AuditOutcome.Succeeded,
                    "project-package",
                    inspection.Manifest.ProjectKey,
                    new Dictionary<string, string>
                    {
                        ["packageId"] = inspection.Manifest.PackageId.ToString(),
                        ["mode"] = importMode.ToString(),
                        ["created"] = result.Created.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["updated"] = result.Updated.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });
                return hasErrors ? Results.BadRequest(result) : Results.Ok(result);
            }
            catch (InvalidDataException ex)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringPackageRestore,
                    AuditOutcome.Failed,
                    "project-package",
                    "unresolved",
                    new Dictionary<string, string>
                    {
                        ["reason"] = "invalid-package",
                        ["errorType"] = ex.GetType().Name
                    });
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringPackageRestore,
                    AuditOutcome.Failed,
                    "project-package",
                    "unresolved",
                    new Dictionary<string, string> { ["errorType"] = ex.GetType().Name });
                throw;
            }
        });

        return endpoints;
    }

    private static async Task<byte[]> ReadPackageAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is > MaximumRequestBytes)
            throw new InvalidDataException("Project package request is too large.");

        using var output = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0;
        int read;
        while ((read = await request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaximumRequestBytes)
                throw new InvalidDataException("Project package request is too large.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "elitescada-project" : safe;
    }
}
