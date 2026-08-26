using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.ProjectPackages;
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
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            try
            {
                var bytes = await ReadPackageAsync(request, cancellationToken);
                var preview = packages.Preview(bytes, mode ?? ImportMode.CreateAndUpdate);
                if (!preview.CanApply) return Results.BadRequest(preview);
                return Results.Ok(packages.Apply(bytes, mode ?? ImportMode.CreateAndUpdate));
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
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
