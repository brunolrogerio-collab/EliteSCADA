using System.Text.Json;
using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.VisualAssets;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.VisualAssets;

public static class VisualAssetEndpoints
{
    public static IEndpointRouteBuilder MapVisualAssetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/engineering/visual-assets", (IVisualAssetEngineeringRegistry assets) =>
            Results.Ok(assets.SnapshotAssets()))
            .RequireWorkspaceEngineeringRead();

        endpoints.MapGet("/api/engineering/visual-assets/{id:guid}/content", (
            Guid id,
            HttpContext context,
            IVisualAssetEngineeringRegistry assets) =>
        {
            var metadata = assets.FindAsset(id);
            if (metadata is null) return Results.NotFound();

            var payload = assets.FindPayload(metadata.Sha256);
            if (payload is null ||
                payload.ByteLength != metadata.ByteLength ||
                !payload.MediaType.Equals(metadata.MediaType, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    "Visual asset content is missing or inconsistent with canonical Engineering metadata.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            context.Response.Headers.ETag = $"\"{metadata.Sha256.ToLowerInvariant()}\"";
            context.Response.Headers.CacheControl = "private, no-cache";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (metadata.MediaType.Equals(VisualAssetContentInspector.PdfMediaType, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; base-uri 'none'; form-action 'none'";
                context.Response.Headers["Content-Disposition"] = "inline";
            }
            return Results.File(payload.Content, metadata.MediaType);
        }).RequireWorkspaceEngineeringRead();

        endpoints.MapPut("/api/engineering/visual-assets/{id:guid}", async (
            Guid id,
            VisualAssetRenameRequest? body,
            HttpRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IVisualAssetEngineeringRegistry assets,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(context, authorization, AuditActions.EngineeringAssetRename, "visual-asset", id.ToString());
                return failure;
            }

            if (!TryReadExpectedChangeVersion(request, out var expectedChangeVersion))
                return Results.BadRequest(new { error = "Engineering Workspace version header is required and must be a non-negative integer." });
            var name = body?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
                return Results.BadRequest(new { error = "Asset display name must contain between 1 and 128 characters." });

            try
            {
                await using var mutation = await workspace.AcquireMutationAsync(expectedChangeVersion, cancellationToken);
                var current = assets.FindAsset(id);
                if (current is null) return Results.NotFound();
                var renamed = current with { Name = name };
                assets.UpsertAsset(renamed);
                await audit.RecordAsync(context, authorization.Principal, AuditActions.EngineeringAssetRename, AuditOutcome.Succeeded,
                    "visual-asset", id.ToString(), new Dictionary<string, string> { ["key"] = current.Key });
                return Results.Ok(new { asset = renamed, workspaceVersion = workspace.CaptureChangeVersion() });
            }
            catch (EngineeringWorkspaceVersionConflictException conflict)
            {
                return Results.Conflict(new { error = "The project changed before the asset could be renamed. Reload and try again.", conflict.ExpectedChangeVersion, conflict.CurrentChangeVersion });
            }
        });

        endpoints.MapDelete("/api/engineering/visual-assets/{id:guid}", async (
            Guid id,
            HttpRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            IVisualAssetEngineeringRegistry assets,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(context, authorization, AuditActions.EngineeringAssetDelete, "visual-asset", id.ToString());
                return failure;
            }

            if (!TryReadExpectedChangeVersion(request, out var expectedChangeVersion))
                return Results.BadRequest(new { error = "Engineering Workspace version header is required and must be a non-negative integer." });

            try
            {
                await using var mutation = await workspace.AcquireMutationAsync(expectedChangeVersion, cancellationToken);
                var asset = assets.FindAsset(id);
                if (asset is null) return Results.NotFound();
                var packageJson = JsonSerializer.SerializeToElement(exchange.ExportPackage());
                if (ContainsAssetReference(packageJson, id))
                    return Results.Conflict(new { error = "This image is still used by the project. Remove its references before deleting it." });

                assets.RemoveAsset(id);
                await audit.RecordAsync(context, authorization.Principal, AuditActions.EngineeringAssetDelete, AuditOutcome.Succeeded,
                    "visual-asset", id.ToString(), new Dictionary<string, string> { ["key"] = asset.Key });
                return Results.NoContent();
            }
            catch (EngineeringWorkspaceVersionConflictException conflict)
            {
                return Results.Conflict(new { error = "The project changed before the asset could be deleted. Reload and try again.", conflict.ExpectedChangeVersion, conflict.CurrentChangeVersion });
            }
        });

        endpoints.MapPost("/api/engineering/visual-assets/import", async (
            string? key,
            string? name,
            string? fileName,
            HttpRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IVisualAssetEngineeringRegistry assets,
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
                    AuditActions.EngineeringAssetImport,
                    "visual-asset",
                    "new");
                return failure;
            }

            if (!TryReadExpectedChangeVersion(request, out var expectedChangeVersion))
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringAssetImport,
                    AuditOutcome.Failed,
                    "visual-asset",
                    "new",
                    new Dictionary<string, string> { ["reason"] = "invalid-or-missing-workspace-version" });
                return Results.BadRequest(new { error = "Engineering Workspace version header is required and must be a non-negative integer." });
            }

            try
            {
                var content = await ReadAssetAsync(request, cancellationToken);
                var inspection = VisualAssetContentInspector.InspectAndCanonicalize(content);
                var payload = VisualAssetPayload.Create(inspection.MediaType, inspection.CanonicalContent);
                var id = Guid.NewGuid();
                var normalizedKey = string.IsNullOrWhiteSpace(key) ? $"asset.{id:N}" : key.Trim();
                var originalFileName = NormalizeFileName(fileName);
                var normalizedName = string.IsNullOrWhiteSpace(name) ? originalFileName : name.Trim();

                var metadata = new VisualAssetEngineeringDto(
                    Id: id,
                    Key: normalizedKey,
                    Name: normalizedName,
                    OriginalFileName: originalFileName,
                    MediaType: inspection.MediaType,
                    ByteLength: payload.ByteLength,
                    Sha256: payload.Sha256,
                    PixelWidth: inspection.PixelWidth,
                    PixelHeight: inspection.PixelHeight,
                    Metadata: inspection.CanonicalMetadata?.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.Ordinal));

                var validationContext = new EngineeringImportContext(
                    new Dictionary<string, VisualAssetPayload>(StringComparer.OrdinalIgnoreCase)
                    {
                        [payload.Sha256] = payload
                    });
                var issues = VisualAssetEngineeringValidator.Validate(metadata, assets, validationContext);
                if (issues.Any(x => x.IsError))
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        AuditActions.EngineeringAssetImport,
                        AuditOutcome.Failed,
                        "visual-asset",
                        id.ToString(),
                        new Dictionary<string, string>
                        {
                            ["reason"] = "validation-errors",
                            ["errorCount"] = issues.Count(x => x.IsError).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        });
                    return Results.BadRequest(new { issues });
                }

                await using var mutation = await workspace.AcquireMutationAsync(
                    expectedChangeVersion,
                    cancellationToken);

                if (assets.FindAssetByKey(normalizedKey) is not null)
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        AuditActions.EngineeringAssetImport,
                        AuditOutcome.Failed,
                        "visual-asset",
                        id.ToString(),
                        new Dictionary<string, string> { ["reason"] = "duplicate-key" });
                    return Results.Conflict(new { error = $"Visual asset key '{normalizedKey}' already exists." });
                }

                assets.PutPayload(payload);
                assets.UpsertAsset(metadata);

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringAssetImport,
                    AuditOutcome.Succeeded,
                    "visual-asset",
                    id.ToString(),
                    new Dictionary<string, string>
                    {
                        ["key"] = normalizedKey,
                        ["mediaType"] = inspection.MediaType,
                        ["byteLength"] = payload.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["sha256"] = payload.Sha256,
                        ["expectedChangeVersion"] = expectedChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["resultingChangeVersion"] = workspace.CaptureChangeVersion().ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                return Results.Ok(new
                {
                    asset = metadata,
                    assetRef = new { assetId = $"asset:{id:D}" },
                    workspaceVersion = workspace.CaptureChangeVersion()
                });
            }
            catch (EngineeringWorkspaceVersionConflictException conflict)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringAssetImport,
                    AuditOutcome.Failed,
                    "visual-asset",
                    "new",
                    new Dictionary<string, string>
                    {
                        ["reason"] = "workspace-version-conflict",
                        ["expectedChangeVersion"] = conflict.ExpectedChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["currentChangeVersion"] = conflict.CurrentChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });
                return Results.Conflict(new
                {
                    error = "Engineering Workspace changed before the asset could be imported. Reload and try again.",
                    expectedChangeVersion = conflict.ExpectedChangeVersion,
                    currentChangeVersion = conflict.CurrentChangeVersion
                });
            }
            catch (InvalidDataException ex)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringAssetImport,
                    AuditOutcome.Failed,
                    "visual-asset",
                    "new",
                    new Dictionary<string, string>
                    {
                        ["reason"] = "invalid-visual-asset",
                        ["errorType"] = ex.GetType().Name
                    });
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringAssetImport,
                    AuditOutcome.Failed,
                    "visual-asset",
                    "new",
                    new Dictionary<string, string> { ["errorType"] = ex.GetType().Name });
                throw;
            }
        });

        return endpoints;
    }

    private static async Task<byte[]> ReadAssetAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is > VisualAssetEngineeringValidator.MaximumVideoPayloadBytes)
            throw new InvalidDataException("Visual asset payload exceeds the supported size limit.");

        using var output = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0L;
        int read;
        while ((read = await request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > VisualAssetEngineeringValidator.MaximumVideoPayloadBytes)
                throw new InvalidDataException("Visual asset payload exceeds the supported size limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (output.Length == 0)
            throw new InvalidDataException("Visual asset payload is empty.");

        return output.ToArray();
    }

    private static bool TryReadExpectedChangeVersion(HttpRequest request, out long expectedChangeVersion)
    {
        expectedChangeVersion = 0;
        if (!request.Headers.TryGetValue("x-elitescada-workspace-version", out var expectedHeader) ||
            expectedHeader.Count != 1 ||
            !long.TryParse(
                expectedHeader.ToString(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedExpectedVersion) ||
            parsedExpectedVersion < 0)
            return false;

        expectedChangeVersion = parsedExpectedVersion;
        return true;
    }

    private static string NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "imported-image";

        var normalized = fileName.Trim().Replace('\\', '/');
        var leaf = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(leaf))
            return "imported-image";
        if (leaf.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new InvalidDataException("Visual asset filename contains unsupported control characters.");
        if (leaf.Length > 512)
            throw new InvalidDataException("Visual asset filename exceeds the supported length.");

        return leaf;
    }

    private static bool ContainsAssetReference(JsonElement element, Guid id)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                return value is not null && (
                    value.Equals(id.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
                    value.Equals(id.ToString("N"), StringComparison.OrdinalIgnoreCase) ||
                    value.Equals($"asset:{id:D}", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals($"asset:{id:N}", StringComparison.OrdinalIgnoreCase));
            case JsonValueKind.Array:
                return element.EnumerateArray().Any(child => ContainsAssetReference(child, id));
            case JsonValueKind.Object:
                return element.EnumerateObject().Any(property =>
                    !property.Name.Equals("visualAssets", StringComparison.OrdinalIgnoreCase) &&
                    ContainsAssetReference(property.Value, id));
            default:
                return false;
        }
    }

    private sealed record VisualAssetRenameRequest(string? Name);
}
