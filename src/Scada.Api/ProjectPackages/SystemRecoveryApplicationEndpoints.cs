using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Reports;
using Scada.Security.Audit;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.ProjectPackages;

public static class SystemRecoveryApplicationEndpoints
{
    private const int MaximumRequestBytes = ProjectPackageService.MaximumPackageBytes;
    private const string BootstrapPreviewAction = "engineering.system_recovery.bootstrap.application.preview";
    private const string PreviewAction = "engineering.system_recovery.application.preview";
    private const string ApplyAction = "engineering.system_recovery.application.apply";

    public static IEndpointRouteBuilder MapSystemRecoveryApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var localRuntime = endpoints.ServiceProvider.GetRequiredService<LocalIdentityRuntimeOptions>();
        if (!localRuntime.Enabled) return endpoints;

        endpoints.MapPost("/api/system-recovery/bootstrap/application/preview", async (
            HttpContext context,
            CancellationToken ct) =>
        {
            var limiter = context.RequestServices.GetRequiredService<LocalLoginAttemptLimiter>();
            var remoteKey = $"system-recovery-application-preview:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
            if (!limiter.TryAcquire(remoteKey))
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);

            var bootstrap = context.RequestServices.GetRequiredService<LocalIdentityBootstrapService>();
            var bootstrapStatus = await LocalIdentityApi.ResolveBootstrapStatusAsync(
                context,
                localRuntime,
                bootstrap,
                ct);
            if (!bootstrapStatus.Required || !bootstrapStatus.Available)
            {
                return Results.Conflict(new
                {
                    error = "Anonymous application recovery preview is available only while this server can prove the installation is empty.",
                    reason = bootstrapStatus.BlockedReason
                });
            }

            var service = Resolve(context);
            if (service is null) return Disabled();

            try
            {
                var bytes = await ReadPackageAsync(context.Request, ct);
                var preview = await service.PreviewAsync(
                    bytes,
                    currentUser: null,
                    requireCurrentUserAdmission: false,
                    ct);
                var audit = context.RequestServices.GetRequiredService<ApiAuditService>();
                await audit.RecordAsync(
                    context,
                    AnonymousPrincipal(),
                    BootstrapPreviewAction,
                    preview.CanApply ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                    "project-package",
                    preview.Manifest.ProjectKey,
                    new Dictionary<string, string>
                    {
                        ["packageId"] = preview.Manifest.PackageId.ToString(),
                        ["runtimeBindingMatches"] = preview.RuntimeBindingMatches.ToString(),
                        ["blockingIssueCount"] = preview.Blockers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                return preview.CanApply
                    ? Results.Ok(preview)
                    : Results.Json(preview, statusCode: StatusCodes.Status409Conflict);
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        endpoints.MapPost("/api/system-recovery/application/preview", async (
            HttpContext context,
            CancellationToken ct) =>
        {
            var actor = await ResolveLocalActorAsync(context, ct);
            if (actor.Failure is not null) return actor.Failure;
            var service = Resolve(context);
            if (service is null) return Disabled();

            try
            {
                var bytes = await ReadPackageAsync(context.Request, ct);
                var preview = await service.PreviewAsync(
                    bytes,
                    actor.Account,
                    requireCurrentUserAdmission: true,
                    ct);
                var audit = context.RequestServices.GetRequiredService<ApiAuditService>();
                await audit.RecordAsync(
                    context,
                    actor.Principal!,
                    PreviewAction,
                    preview.CanApply ? AuditOutcome.Succeeded : AuditOutcome.Denied,
                    "project-package",
                    preview.Manifest.ProjectKey,
                    new Dictionary<string, string>
                    {
                        ["packageId"] = preview.Manifest.PackageId.ToString(),
                        ["bootstrapAuthority"] = (preview.CurrentUserAdmission?.BootstrapAuthority == true).ToString(),
                        ["prospectiveAdmission"] = (preview.CurrentUserAdmission?.Allowed == true).ToString(),
                        ["blockingIssueCount"] = preview.Blockers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                if (preview.CurrentUserAdmission?.Allowed != true)
                    return Results.Json(preview, statusCode: StatusCodes.Status403Forbidden);
                if (!preview.ProjectCatalogEmpty || !preview.RuntimeBindingMatches)
                    return Results.Json(preview, statusCode: StatusCodes.Status409Conflict);
                return preview.ImportPreview.CanApply
                    ? Results.Ok(preview)
                    : Results.BadRequest(preview);
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        endpoints.MapPost("/api/system-recovery/application/apply", async (
            HttpContext context,
            CancellationToken ct) =>
        {
            var actor = await ResolveLocalActorAsync(context, ct);
            if (actor.Failure is not null) return actor.Failure;
            var service = Resolve(context);
            if (service is null) return Disabled();

            ProjectPackageManifest? manifest = null;
            try
            {
                var bytes = await ReadPackageAsync(context.Request, ct);
                var preview = await service.PreviewAsync(
                    bytes,
                    actor.Account,
                    requireCurrentUserAdmission: true,
                    ct);
                manifest = preview.Manifest;
                if (preview.CurrentUserAdmission?.Allowed != true)
                {
                    await RecordApplyAuditAsync(
                        context,
                        actor.Principal!,
                        preview.Manifest,
                        AuditOutcome.Denied,
                        "authority-admission",
                        preview.Blockers.Count,
                        recovered: false);
                    return Results.Json(preview, statusCode: StatusCodes.Status403Forbidden);
                }
                if (!preview.ProjectCatalogEmpty || !preview.RuntimeBindingMatches)
                {
                    await RecordApplyAuditAsync(
                        context,
                        actor.Principal!,
                        preview.Manifest,
                        AuditOutcome.Failed,
                        "preflight-conflict",
                        preview.Blockers.Count,
                        recovered: false);
                    return Results.Json(preview, statusCode: StatusCodes.Status409Conflict);
                }
                if (!preview.ImportPreview.CanApply)
                    return Results.BadRequest(preview);

                var savedBy = actor.Account!.DisplayName;
                var result = await service.ApplyAsync(bytes, actor.Account, savedBy, ct);
                await RecordApplyAuditAsync(
                    context,
                    actor.Principal!,
                    result.Manifest,
                    result.Recovered ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                    result.Stage,
                    result.Issues.Count,
                    result.Recovered);

                if (result.Recovered) return Results.Ok(result);
                if (result.DurableRevisionSaved)
                    return Results.Json(result, statusCode: StatusCodes.Status422UnprocessableEntity);
                return Results.BadRequest(result);
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                if (manifest is not null)
                {
                    await RecordApplyAuditAsync(
                        context,
                        actor.Principal!,
                        manifest,
                        AuditOutcome.Failed,
                        ex.GetType().Name,
                        issueCount: 1,
                        recovered: false);
                }
                throw;
            }
        });

        return endpoints;
    }

    private static SystemRecoveryApplicationService? Resolve(HttpContext context)
    {
        var services = context.RequestServices;
        var persistence = services.GetService<IEngineeringProjectPersistenceService>();
        var catalog = services.GetService<IEngineeringProjectCatalog>();
        var activation = services.GetService<IPublishedRuntimeActivationService>();
        if (persistence is null || catalog is null || activation is null) return null;

        return new SystemRecoveryApplicationService(
            services.GetRequiredService<IProjectPackageService>(),
            persistence,
            catalog,
            activation,
            services.GetRequiredService<ILocalIdentityStore>(),
            services.GetRequiredService<EngineeringWorkspace>(),
            services.GetRequiredService<IEngineeringExchangeService>(),
            services.GetRequiredService<IGatewayEngineeringRegistry>(),
            services.GetRequiredService<IReportEngineeringRegistry>(),
            services.GetRequiredService<InitialInstallationGate>(),
            services.GetRequiredService<IConfiguration>());
    }

    private static async Task<(SecurityPrincipal? Principal, LocalUserAccount? Account, IResult? Failure)> ResolveLocalActorAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var security = context.RequestServices.GetRequiredService<ApiAuthorizationService>();
        var principal = security.GetPrincipal(context);
        if (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.SubjectId))
            return (principal, null, Results.Unauthorized());

        var identityProvider = context.User.FindFirst(JwtTokenIssuer.IdentityProviderClaim)?.Value;
        if (!string.Equals(identityProvider, JwtTokenIssuer.LocalIdentityProvider, StringComparison.Ordinal))
        {
            return (
                principal,
                null,
                Results.Json(
                    new { error = "System Recovery requires an authenticated local Authority identity." },
                    statusCode: StatusCodes.Status403Forbidden));
        }

        if (!Guid.TryParse(principal.SubjectId, out var userId))
        {
            return (
                principal,
                null,
                Results.Json(
                    new { error = "Authenticated local Authority subject is invalid." },
                    statusCode: StatusCodes.Status403Forbidden));
        }

        var identities = context.RequestServices.GetRequiredService<ILocalIdentityStore>();
        var account = await identities.FindByIdAsync(userId, cancellationToken);
        if (account is null || !account.IsEnabled)
        {
            return (
                principal,
                null,
                Results.Json(
                    new { error = "Authenticated local Authority identity is unavailable or disabled." },
                    statusCode: StatusCodes.Status403Forbidden));
        }

        return (principal, account, null);
    }

    private static async Task RecordApplyAuditAsync(
        HttpContext context,
        SecurityPrincipal principal,
        ProjectPackageManifest manifest,
        AuditOutcome outcome,
        string stage,
        int issueCount,
        bool recovered)
    {
        var audit = context.RequestServices.GetRequiredService<ApiAuditService>();
        await audit.RecordAsync(
            context,
            principal,
            ApplyAction,
            outcome,
            "project-package",
            manifest.ProjectKey,
            new Dictionary<string, string>
            {
                ["packageId"] = manifest.PackageId.ToString(),
                ["stage"] = stage,
                ["issueCount"] = issueCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["recovered"] = recovered.ToString()
            });
    }

    private static async Task<byte[]> ReadPackageAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
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

    private static SecurityPrincipal AnonymousPrincipal() =>
        new("anonymous", null, Array.Empty<string>(), false);

    private static IResult Disabled() => Results.Json(
        new { error = "System Recovery application persistence is not configured." },
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
