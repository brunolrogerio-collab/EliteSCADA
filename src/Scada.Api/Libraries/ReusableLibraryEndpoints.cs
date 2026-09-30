using System.IO.Compression;
using System.Text.Json;
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
    public const string CatalogRoute = "/api/engineering/libraries";
    public const string AssociateRoute = "/api/engineering/libraries/associate";
    public const string ExportRoute = "/api/engineering/libraries/export";
    public const string InspectRoute = "/api/engineering/libraries/inspect";

    private static ReusableLibraryCatalogStore Catalog => ReusableLibraryCatalogStore.Shared;

    public static IEndpointRouteBuilder MapReusableLibraryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(CatalogRoute, (
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security) =>
        {
            var access = CheckAccess(context, security, exchange, SecurityCapability.EngineeringView);
            return access.Failure ?? Results.Ok(Catalog.Snapshot(CatalogScope(workspace)));
        });

        endpoints.MapGet("/api/engineering/libraries/{libraryId:guid}/resources", (
            Guid libraryId,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security) =>
        {
            var access = CheckAccess(context, security, exchange, SecurityCapability.EngineeringView);
            if (access.Failure is not null) return access.Failure;

            var entry = Catalog.Find(CatalogScope(workspace), libraryId);
            if (entry is null) return Results.NotFound();

            return Results.Ok(new
            {
                library = new ReusableLibraryCatalogDescriptor(
                    entry.LibraryId,
                    entry.Name,
                    entry.Version,
                    entry.ContentSha256,
                    entry.ResourceCount,
                    entry.ByteLength),
                resources = entry.Inspection.Manifest.Resources
            });
        });

        endpoints.MapGet("/api/engineering/libraries/{libraryId:guid}/resources/{resourceId:guid}/preview", (
            Guid libraryId,
            Guid resourceId,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security) =>
        {
            var access = CheckAccess(context, security, exchange, SecurityCapability.EngineeringView);
            if (access.Failure is not null) return access.Failure;

            var entry = Catalog.Find(CatalogScope(workspace), libraryId);
            if (entry is null) return Results.NotFound();
            var resource = entry.Inspection.Manifest.Resources.SingleOrDefault(item => item.ResourceId == resourceId);
            if (resource is null) return Results.NotFound();

            try
            {
                using var archive = OpenCatalogArchive(entry.Content);
                var payload = ReadJsonPayload(archive, resource.PayloadPath);
                var dynamoIds = (resource.Dependencies ?? Array.Empty<ReusableLibraryDependency>())
                    .Where(item => item.Kind == ReusableLibraryResourceKinds.Dynamo)
                    .Select(item => item.ResourceId)
                    .ToHashSet();
                if (resource.Kind == ReusableLibraryResourceKinds.Dynamo)
                    dynamoIds.Add(resource.ResourceId);

                var dynamos = entry.Inspection.Manifest.Resources
                    .Where(item => item.Kind == ReusableLibraryResourceKinds.Dynamo && dynamoIds.Contains(item.ResourceId))
                    .Select(item => ReadJsonPayload(archive, item.PayloadPath))
                    .ToArray();

                return Results.Ok(new
                {
                    library = new ReusableLibraryCatalogDescriptor(
                        entry.LibraryId,
                        entry.Name,
                        entry.Version,
                        entry.ContentSha256,
                        entry.ResourceCount,
                        entry.ByteLength),
                    resource,
                    payload,
                    dynamos,
                    workingChanged = false
                });
            }
            catch (InvalidDataException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message, workingChanged = false });
            }
        });

        endpoints.MapGet("/api/engineering/libraries/{libraryId:guid}/assets/{assetId:guid}/content", (
            Guid libraryId,
            Guid assetId,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security) =>
        {
            var access = CheckAccess(context, security, exchange, SecurityCapability.EngineeringView);
            if (access.Failure is not null) return access.Failure;

            var entry = Catalog.Find(CatalogScope(workspace), libraryId);
            if (entry is null) return Results.NotFound();
            var resource = entry.Inspection.Manifest.Resources.SingleOrDefault(item =>
                item.ResourceId == assetId && item.Kind == ReusableLibraryResourceKinds.VisualAsset);
            if (resource is null) return Results.NotFound();

            try
            {
                using var archive = OpenCatalogArchive(entry.Content);
                var payload = ReadJsonPayload(archive, resource.PayloadPath);
                var sha256 = RequiredJsonString(payload, "sha256");
                var mediaType = RequiredJsonString(payload, "mediaType");
                var assetPath = $"assets/{sha256.ToLowerInvariant()}";
                var manifestFile = entry.Inspection.Manifest.Files.SingleOrDefault(file => file.Path == assetPath)
                    ?? throw new InvalidDataException($"Visual asset '{assetId:D}' content is missing from the associated library.");
                var bytes = ReadArchiveEntry(archive, assetPath, ReusableLibraryPackageService.MaximumAssetBytes);
                if (bytes.LongLength != manifestFile.Length)
                    throw new InvalidDataException($"Visual asset '{assetId:D}' content length no longer matches its validated manifest.");
                return Results.File(bytes, mediaType);
            }
            catch (InvalidDataException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message });
            }
        });

        endpoints.MapPost(AssociateRoute, async (
            HttpRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var access = CheckAccess(context, security, exchange, SecurityCapability.EngineeringModify);
            if (access.Failure is not null)
            {
                await RecordDeniedAsync(
                    context,
                    audit,
                    access,
                    AuditActions.EngineeringLibraryAssociate,
                    "reusable-library",
                    "unresolved");
                return access.Failure;
            }

            try
            {
                var bytes = await ReadLibraryAsync(request, cancellationToken);
                var service = new ReusableLibraryPackageService(
                    workspace.Assets,
                    workspace.VisualAssets,
                    workspace.Scripts,
                    workspace.Views);
                var inspection = service.Inspect(bytes);
                var result = Catalog.Associate(CatalogScope(workspace), bytes, inspection);

                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryAssociate,
                    AuditOutcome.Succeeded,
                    "reusable-library",
                    result.Library.LibraryId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["name"] = result.Library.Name,
                        ["version"] = result.Library.Version,
                        ["contentSha256"] = result.Library.ContentSha256,
                        ["resourceCount"] = result.Library.ResourceCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["added"] = result.Added.ToString()
                    });

                return Results.Ok(new
                {
                    association = result.Library,
                    added = result.Added,
                    workingChanged = false
                });
            }
            catch (ReusableLibraryAssociationConflictException ex)
            {
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryAssociate,
                    AuditOutcome.Failed,
                    "reusable-library",
                    ex.LibraryId.ToString("D"),
                    new Dictionary<string, string> { ["reason"] = "different-content-already-associated" });
                return Results.Conflict(new { error = ex.Message });
            }
            catch (InvalidDataException ex)
            {
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryAssociate,
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

        endpoints.MapDelete("/api/engineering/libraries/{libraryId:guid}", async (
            Guid libraryId,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
        {
            var access = CheckAccess(context, security, exchange, SecurityCapability.EngineeringModify);
            if (access.Failure is not null)
            {
                await RecordDeniedAsync(
                    context,
                    audit,
                    access,
                    AuditActions.EngineeringLibraryDisassociate,
                    "reusable-library",
                    libraryId.ToString("D"));
                return access.Failure;
            }

            if (!Catalog.Disassociate(CatalogScope(workspace), libraryId)) return Results.NotFound();

            await audit.RecordAsync(
                context,
                access.Authorization.Principal,
                AuditActions.EngineeringLibraryDisassociate,
                AuditOutcome.Succeeded,
                "reusable-library",
                libraryId.ToString("D"),
                new Dictionary<string, string> { ["workingChanged"] = bool.FalseString });
            return Results.Ok(new { libraryId, workingChanged = false });
        });

        endpoints.MapPost(ExportRoute, async (
            ReusableLibraryExportRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
        {
            var access = CheckAccess(context, security, exchange, SecurityCapability.EngineeringView);
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
                var service = new ReusableLibraryPackageService(
                    workspace.Assets,
                    workspace.VisualAssets,
                    workspace.Scripts,
                    workspace.Views);
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
            var access = CheckAccess(context, security, exchange, SecurityCapability.EngineeringView);
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
                var service = new ReusableLibraryPackageService(
                    workspace.Assets,
                    workspace.VisualAssets,
                    workspace.Scripts,
                    workspace.Views);
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
        IEngineeringExchangeService exchange,
        SecurityCapability capability = SecurityCapability.EngineeringModify)
    {
        var authorization = security.CheckWorkspace(context, capability);
        var capabilityFailure = authorization.FailureResult();
        if (capabilityFailure is not null)
            return new ReusableLibraryAccessDecision(authorization, capabilityFailure, "capability");

        var lockFailure = EngineeringLockAccess.ProtectedEngineeringFailure(exchange);
        return lockFailure is null
            ? new ReusableLibraryAccessDecision(authorization, null, null)
            : new ReusableLibraryAccessDecision(authorization, lockFailure, "engineering-lock");
    }

    internal static string CatalogScope(EngineeringWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var projectKey = workspace.Describe().ProjectKey;
        return string.IsNullOrWhiteSpace(projectKey)
            ? $"working:{workspace.SessionId:D}"
            : $"project:{projectKey.Trim()}";
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

    private static ZipArchive OpenCatalogArchive(byte[] content)
    {
        try
        {
            return new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            throw new InvalidDataException("Associated reusable library content cannot be opened for preview.", ex);
        }
    }

    private static JsonElement ReadJsonPayload(ZipArchive archive, string path)
    {
        var bytes = ReadArchiveEntry(archive, path, ReusableLibraryPackageService.MaximumResourceBytes);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Reusable resource preview payload '{path}' is invalid JSON.", ex);
        }
    }

    private static byte[] ReadArchiveEntry(ZipArchive archive, string path, int maximumBytes)
    {
        var entry = archive.GetEntry(path)
            ?? throw new InvalidDataException($"Reusable library preview entry '{path}' is missing.");
        if (entry.Length < 0 || entry.Length > maximumBytes)
            throw new InvalidDataException($"Reusable library preview entry '{path}' exceeds its safety limit.");
        using var input = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            total = checked(total + read);
            if (total > maximumBytes)
                throw new InvalidDataException($"Reusable library preview entry '{path}' exceeds its safety limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static string RequiredJsonString(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
            throw new InvalidDataException($"Reusable visual asset preview metadata '{propertyName}' is missing.");
        return property.GetString()!;
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "elitescada-library" : safe;
    }
}
