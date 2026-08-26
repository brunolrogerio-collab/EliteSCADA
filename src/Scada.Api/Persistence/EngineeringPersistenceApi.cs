using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.Persistence;
using Scada.Persistence.PostgreSql;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Persistence;

public static class EngineeringPersistenceApi
{
    public static void AddOptionalEngineeringPersistence(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("EliteScada");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        builder.Services.TryAddSingleton<IEngineeringProjectStore>(_ =>
            new PostgreSqlEngineeringProjectStore(connectionString));
        builder.Services.TryAddSingleton<IEngineeringProjectPersistenceService, EngineeringProjectPersistenceService>();
        builder.Services.TryAddSingleton<IEngineeringWorkspaceCheckoutService, EngineeringWorkspaceCheckoutService>();
        builder.Services.TryAddSingleton<IPublishedRuntimeActivationService, PublishedRuntimeActivationService>();
        builder.Services.TryAddSingleton<IPersistedRuntimeRecoveryService, PersistedRuntimeRecoveryService>();
    }

    public static async Task InitializeEngineeringPersistenceAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        var configuredProjectKey = app.Configuration["EngineeringRuntime:ProjectKey"];
        var persistence = app.Services.GetService<IEngineeringProjectPersistenceService>();
        if (persistence is null)
        {
            if (!string.IsNullOrWhiteSpace(configuredProjectKey))
            {
                throw new InvalidOperationException(
                    "EngineeringRuntime:ProjectKey is configured, but engineering persistence is unavailable. Configure ConnectionStrings:EliteScada before starting a persisted runtime.");
            }

            return;
        }

        await persistence.InitializeAsync(cancellationToken);
        await app.RecoverConfiguredEngineeringRuntimeAsync(cancellationToken);
    }

    public static async Task<PersistedRuntimeRecoveryResult?> RecoverConfiguredEngineeringRuntimeAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        var projectKey = app.Configuration["EngineeringRuntime:ProjectKey"];
        if (string.IsNullOrWhiteSpace(projectKey)) return null;

        var recovery = app.Services.GetService<IPersistedRuntimeRecoveryService>();
        if (recovery is null)
        {
            throw new InvalidOperationException(
                "Engineering runtime recovery is unavailable although a runtime project is configured.");
        }

        var result = await recovery.RecoverAsync(projectKey, cancellationToken);
        if (result.PersistedActiveRevision.HasValue && !result.Recovered)
        {
            var issues = result.Runtime is null
                ? "The persisted active engineering snapshot could not be loaded."
                : string.Join("; ",
                    result.Runtime.CompilationIssues
                        .Where(x => x.IsError)
                        .Select(x => $"{x.Code}: {x.Message}")
                        .Concat(result.Runtime.RuntimeIssues
                            .Where(x => x.IsError)
                            .Select(x => $"{x.Code}: {x.Message}")));

            throw new InvalidOperationException(
                $"Persisted active revision {result.PersistedActiveRevision} for project '{projectKey}' could not be recovered. {issues}");
        }

        return result;
    }

    public static void MapEngineeringPersistenceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/engineering/persistence");

        group.MapGet("/status", (HttpContext context) => Results.Ok(new
        {
            enabled = Resolve(context) is not null,
            provider = Resolve(context) is null ? null : "postgresql",
            configuredProjectKey = ResolveConfiguredProjectKey(context)
        }));

        group.MapPost("/{projectKey}/save", async (
            string projectKey,
            EngineeringSaveRequest request,
            EngineeringWorkspace workspace,
            ScadaRuntimeFacade runtime,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var authorization = await AuthorizeEngineeringMutationAsync(context, runtime, cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await AuditDeniedAsync(context, authorization, AuditActions.EngineeringSave, projectKey);
                return failure;
            }

            var audit = ResolveAudit(context);
            if (string.IsNullOrWhiteSpace(request.ProjectName))
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringSave,
                    AuditOutcome.Failed,
                    "engineering-project",
                    projectKey,
                    new Dictionary<string, string> { ["reason"] = "project-name-required" });
                return Results.BadRequest(new { error = "Project name is required." });
            }

            try
            {
                var before = workspace.Describe();
                var saveVersion = workspace.CaptureChangeVersion();
                var basedOnRevision = before.ProjectKey?.Equals(projectKey, StringComparison.OrdinalIgnoreCase) == true
                    ? before.BaseRevision
                    : null;
                var actor = Actor(authorization.Principal);

                var snapshot = await persistence.SaveCurrentDerivedAsync(
                    projectKey,
                    request.ProjectName,
                    basedOnRevision,
                    actor,
                    cancellationToken);

                workspace.AcceptSave(
                    snapshot.ProjectKey,
                    snapshot.ProjectName,
                    snapshot.Revision,
                    snapshot.SavedAtUtc,
                    saveVersion);

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringSave,
                    AuditOutcome.Succeeded,
                    "engineering-project",
                    snapshot.ProjectKey,
                    new Dictionary<string, string>
                    {
                        ["revision"] = snapshot.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["basedOnRevision"] = snapshot.BasedOnRevision?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"
                    });

                return Results.Ok(ToMetadata(snapshot));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await AuditExceptionAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringSave,
                    projectKey,
                    ex);
                throw;
            }
        });

        group.MapGet("/{projectKey}/lifecycle", async (
            string projectKey,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            return Results.Ok(await persistence.GetLifecycleAsync(projectKey, cancellationToken));
        });

        group.MapGet("/{projectKey}/runtime", async (
            string projectKey,
            ScadaRuntimeFacade runtime,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var lifecycle = await persistence.GetLifecycleAsync(projectKey, cancellationToken);
            var live = runtime.Describe();
            var consistent = lifecycle.ActiveRevision.HasValue
                ? live.ProjectKey?.Equals(projectKey, StringComparison.OrdinalIgnoreCase) == true &&
                  live.Revision == lifecycle.ActiveRevision
                : live.Revision is null;

            return Results.Ok(new
            {
                projectKey,
                configuredProjectKey = ResolveConfiguredProjectKey(context),
                consistent,
                durable = lifecycle,
                live
            });
        });

        group.MapPost("/{projectKey}/published/activate", async (
            string projectKey,
            EngineeringActivateRequest request,
            ScadaRuntimeFacade runtime,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var activationService = ResolveActivation(context);
            if (activationService is null) return Disabled();

            var authorization = await AuthorizeEngineeringMutationAsync(context, runtime, cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await AuditDeniedAsync(context, authorization, AuditActions.EngineeringActivate, projectKey);
                return failure;
            }

            var audit = ResolveAudit(context);
            var configuredProjectKey = ResolveConfiguredProjectKey(context);
            if (string.IsNullOrWhiteSpace(configuredProjectKey))
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringActivate,
                    AuditOutcome.Failed,
                    "engineering-project",
                    projectKey,
                    new Dictionary<string, string> { ["reason"] = "runtime-project-not-configured" });
                return Results.Conflict(new
                {
                    error = "EngineeringRuntime:ProjectKey must be configured before activating a persisted runtime."
                });
            }

            if (!configuredProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringActivate,
                    AuditOutcome.Failed,
                    "engineering-project",
                    projectKey,
                    new Dictionary<string, string>
                    {
                        ["reason"] = "runtime-project-mismatch",
                        ["configuredProjectKey"] = configuredProjectKey
                    });
                return Results.Conflict(new
                {
                    error = $"This runtime instance is bound to project '{configuredProjectKey}', not '{projectKey}'."
                });
            }

            try
            {
                var actor = Actor(authorization.Principal);
                _ = request; // Legacy ActivatedBy is ignored; the authenticated principal is authoritative.
                var outcome = await activationService.ActivateAsync(
                    projectKey,
                    actor,
                    cancellationToken);

                if (!outcome.Found || outcome.Snapshot is null)
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        AuditActions.EngineeringActivate,
                        AuditOutcome.Failed,
                        "engineering-project",
                        projectKey,
                        new Dictionary<string, string> { ["reason"] = "published-revision-not-found" });
                    return Results.NotFound(new { error = "Project has no published revision." });
                }

                var response = new
                {
                    revision = ToMetadata(outcome.Snapshot),
                    activated = outcome.Activated,
                    runtime = outcome.Runtime,
                    activation = outcome.Activation,
                    lifecycle = outcome.Lifecycle
                };

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringActivate,
                    outcome.Activated ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                    "engineering-project",
                    projectKey,
                    new Dictionary<string, string>
                    {
                        ["revision"] = outcome.Snapshot.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["reason"] = outcome.Activated ? "activated" : "runtime-activation-rejected"
                    });

                return outcome.Activated
                    ? Results.Ok(response)
                    : Results.Json(response, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await AuditExceptionAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringActivate,
                    projectKey,
                    ex);
                throw;
            }
        });

        group.MapGet("/{projectKey}/revisions", async (
            string projectKey,
            int? limit,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var revisions = await persistence.ListRevisionsAsync(
                projectKey,
                limit ?? 50,
                cancellationToken);

            return Results.Ok(revisions.Select(ToMetadata));
        });

        group.MapPost("/{projectKey}/revisions/{revision:long}/checkout", async (
            string projectKey,
            long revision,
            ScadaRuntimeFacade runtime,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var checkout = ResolveCheckout(context);
            if (checkout is null) return Disabled();

            var authorization = await AuthorizeEngineeringMutationAsync(context, runtime, cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await AuditDeniedAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringCheckout,
                    projectKey,
                    RevisionDetails(revision));
                return failure;
            }

            var audit = ResolveAudit(context);
            try
            {
                var outcome = await checkout.CheckoutAsync(projectKey, revision, cancellationToken);
                if (outcome is null)
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        AuditActions.EngineeringCheckout,
                        AuditOutcome.Failed,
                        "engineering-project",
                        projectKey,
                        MergeDetails(RevisionDetails(revision), "reason", "revision-not-found"));
                    return Results.NotFound();
                }

                var response = new
                {
                    revision = ToMetadata(outcome.Snapshot),
                    checkedOut = outcome.CheckedOut,
                    preview = outcome.Preview,
                    apply = outcome.ApplyResult,
                    workspace = outcome.Workspace
                };

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringCheckout,
                    outcome.CheckedOut ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                    "engineering-project",
                    projectKey,
                    new Dictionary<string, string>
                    {
                        ["revision"] = revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["previewErrors"] = outcome.Preview.ErrorCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                return outcome.CheckedOut
                    ? Results.Ok(response)
                    : Results.BadRequest(response);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await AuditExceptionAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringCheckout,
                    projectKey,
                    ex,
                    RevisionDetails(revision));
                throw;
            }
        });

        group.MapGet("/{projectKey}/latest", async (
            string projectKey,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var snapshot = await persistence.LoadLatestAsync(projectKey, cancellationToken);
            return snapshot is null ? Results.NotFound() : Results.Ok(ToMetadata(snapshot));
        });

        group.MapPost("/{projectKey}/latest/preview", async (
            string projectKey,
            ImportMode? mode,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var preview = await persistence.PreviewLatestAsync(
                projectKey,
                mode ?? ImportMode.CreateAndUpdate,
                cancellationToken);

            return preview is null
                ? Results.NotFound()
                : Results.Ok(new { revision = ToMetadata(preview.Snapshot), preview = preview.Preview });
        });

        group.MapPost("/{projectKey}/latest/apply", async (
            string projectKey,
            ImportMode? mode,
            ScadaRuntimeFacade runtime,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var authorization = await AuthorizeEngineeringMutationAsync(context, runtime, cancellationToken);
            var importMode = mode ?? ImportMode.CreateAndUpdate;
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await AuditDeniedAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringPersistenceApply,
                    projectKey,
                    new Dictionary<string, string>
                    {
                        ["source"] = "latest",
                        ["mode"] = importMode.ToString()
                    });
                return failure;
            }

            var audit = ResolveAudit(context);
            try
            {
                var result = await persistence.ApplyLatestAsync(projectKey, importMode, cancellationToken);
                if (result is null)
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        AuditActions.EngineeringPersistenceApply,
                        AuditOutcome.Failed,
                        "engineering-project",
                        projectKey,
                        new Dictionary<string, string>
                        {
                            ["source"] = "latest",
                            ["mode"] = importMode.ToString(),
                            ["reason"] = "revision-not-found"
                        });
                    return Results.NotFound();
                }

                var hasErrors = result.Issues.Any(x => x.IsError);
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringPersistenceApply,
                    hasErrors ? AuditOutcome.Failed : AuditOutcome.Succeeded,
                    "engineering-project",
                    projectKey,
                    new Dictionary<string, string>
                    {
                        ["source"] = "latest",
                        ["mode"] = importMode.ToString(),
                        ["created"] = result.Created.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["updated"] = result.Updated.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["skipped"] = result.Skipped.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                return ToApplyResult(result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await AuditExceptionAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringPersistenceApply,
                    projectKey,
                    ex,
                    new Dictionary<string, string>
                    {
                        ["source"] = "latest",
                        ["mode"] = importMode.ToString()
                    });
                throw;
            }
        });

        group.MapPost("/{projectKey}/revisions/{revision:long}/preview", async (
            string projectKey,
            long revision,
            ImportMode? mode,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var preview = await persistence.PreviewRevisionAsync(
                projectKey,
                revision,
                mode ?? ImportMode.CreateAndUpdate,
                cancellationToken);

            return preview is null
                ? Results.NotFound()
                : Results.Ok(new { revision = ToMetadata(preview.Snapshot), preview = preview.Preview });
        });

        group.MapPost("/{projectKey}/revisions/{revision:long}/publish", async (
            string projectKey,
            long revision,
            EngineeringPublishRequest request,
            ScadaRuntimeFacade runtime,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var authorization = await AuthorizeEngineeringMutationAsync(context, runtime, cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await AuditDeniedAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringPublish,
                    projectKey,
                    RevisionDetails(revision));
                return failure;
            }

            var audit = ResolveAudit(context);
            try
            {
                var actor = Actor(authorization.Principal);
                _ = request; // Legacy PublishedBy is ignored; the authenticated principal is authoritative.
                var result = await persistence.PublishRevisionAsync(
                    projectKey,
                    revision,
                    actor,
                    cancellationToken);

                if (result is null)
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        AuditActions.EngineeringPublish,
                        AuditOutcome.Failed,
                        "engineering-project",
                        projectKey,
                        MergeDetails(RevisionDetails(revision), "reason", "revision-not-found"));
                    return Results.NotFound();
                }

                if (!result.Published)
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        AuditActions.EngineeringPublish,
                        AuditOutcome.Failed,
                        "engineering-project",
                        projectKey,
                        new Dictionary<string, string>
                        {
                            ["revision"] = revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["reason"] = "preview-errors",
                            ["errorCount"] = result.Preview.ErrorCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        });
                    return Results.BadRequest(new
                    {
                        revision = ToMetadata(result.Snapshot),
                        preview = result.Preview,
                        published = false
                    });
                }

                var lifecycle = await persistence.GetLifecycleAsync(projectKey, cancellationToken);
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringPublish,
                    AuditOutcome.Succeeded,
                    "engineering-project",
                    projectKey,
                    RevisionDetails(revision));

                return Results.Ok(new
                {
                    revision = ToMetadata(result.Snapshot),
                    publication = result.Publication,
                    lifecycle
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await AuditExceptionAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringPublish,
                    projectKey,
                    ex,
                    RevisionDetails(revision));
                throw;
            }
        });

        group.MapPost("/{projectKey}/revisions/{revision:long}/apply", async (
            string projectKey,
            long revision,
            ImportMode? mode,
            ScadaRuntimeFacade runtime,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var authorization = await AuthorizeEngineeringMutationAsync(context, runtime, cancellationToken);
            var importMode = mode ?? ImportMode.CreateAndUpdate;
            var details = new Dictionary<string, string>
            {
                ["source"] = "revision",
                ["revision"] = revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["mode"] = importMode.ToString()
            };
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await AuditDeniedAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringPersistenceApply,
                    projectKey,
                    details);
                return failure;
            }

            var audit = ResolveAudit(context);
            try
            {
                var result = await persistence.ApplyRevisionAsync(
                    projectKey,
                    revision,
                    importMode,
                    cancellationToken);

                if (result is null)
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        AuditActions.EngineeringPersistenceApply,
                        AuditOutcome.Failed,
                        "engineering-project",
                        projectKey,
                        MergeDetails(details, "reason", "revision-not-found"));
                    return Results.NotFound();
                }

                var hasErrors = result.Issues.Any(x => x.IsError);
                var resultDetails = new Dictionary<string, string>(details)
                {
                    ["created"] = result.Created.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["updated"] = result.Updated.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["skipped"] = result.Skipped.ToString(System.Globalization.CultureInfo.InvariantCulture)
                };
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringPersistenceApply,
                    hasErrors ? AuditOutcome.Failed : AuditOutcome.Succeeded,
                    "engineering-project",
                    projectKey,
                    resultDetails);

                return ToApplyResult(result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await AuditExceptionAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringPersistenceApply,
                    projectKey,
                    ex,
                    details);
                throw;
            }
        });
    }

    private static async Task<ApiAuthorizationCheck> AuthorizeEngineeringMutationAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        CancellationToken cancellationToken) =>
        await context.RequestServices
            .GetRequiredService<ApiAuthorizationService>()
            .CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.EngineeringModify,
                cancellationToken: cancellationToken);

    private static ApiAuditService ResolveAudit(HttpContext context) =>
        context.RequestServices.GetRequiredService<ApiAuditService>();

    private static string Actor(SecurityPrincipal principal) =>
        string.IsNullOrWhiteSpace(principal.DisplayName)
            ? principal.SubjectId
            : principal.DisplayName.Trim();

    private static ValueTask AuditDeniedAsync(
        HttpContext context,
        ApiAuthorizationCheck authorization,
        string action,
        string projectKey,
        IReadOnlyDictionary<string, string>? details = null) =>
        ResolveAudit(context).RecordAuthorizationDeniedAsync(
            context,
            authorization,
            action,
            "engineering-project",
            projectKey,
            details);

    private static ValueTask AuditExceptionAsync(
        HttpContext context,
        ApiAuthorizationCheck authorization,
        string action,
        string projectKey,
        Exception exception,
        IReadOnlyDictionary<string, string>? details = null)
    {
        var merged = details is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(details);
        merged["errorType"] = exception.GetType().Name;

        return ResolveAudit(context).RecordAsync(
            context,
            authorization.Principal,
            action,
            AuditOutcome.Failed,
            "engineering-project",
            projectKey,
            merged);
    }

    private static Dictionary<string, string> RevisionDetails(long revision) =>
        new()
        {
            ["revision"] = revision.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

    private static Dictionary<string, string> MergeDetails(
        IReadOnlyDictionary<string, string> source,
        string key,
        string value)
    {
        var merged = new Dictionary<string, string>(source)
        {
            [key] = value
        };
        return merged;
    }

    private static IEngineeringProjectPersistenceService? Resolve(HttpContext context) =>
        context.RequestServices.GetService<IEngineeringProjectPersistenceService>();

    private static IEngineeringWorkspaceCheckoutService? ResolveCheckout(HttpContext context) =>
        context.RequestServices.GetService<IEngineeringWorkspaceCheckoutService>();

    private static IPublishedRuntimeActivationService? ResolveActivation(HttpContext context) =>
        context.RequestServices.GetService<IPublishedRuntimeActivationService>();

    private static string? ResolveConfiguredProjectKey(HttpContext context) =>
        context.RequestServices.GetRequiredService<IConfiguration>()["EngineeringRuntime:ProjectKey"];

    private static IResult Disabled() => Results.Json(
        new
        {
            error = "Engineering persistence is not configured.",
            configuration = "ConnectionStrings:EliteScada"
        },
        statusCode: StatusCodes.Status503ServiceUnavailable);

    private static IResult ToApplyResult(ImportResult result) =>
        result.Issues.Any(x => x.IsError)
            ? Results.BadRequest(result)
            : Results.Ok(result);

    private static object ToMetadata(EngineeringProjectSnapshot snapshot) => new
    {
        snapshot.Revision,
        snapshot.ProjectKey,
        snapshot.ProjectName,
        snapshot.EngineeringSchema,
        snapshot.EngineeringSchemaVersion,
        snapshot.SavedAtUtc,
        snapshot.SavedBy,
        snapshot.BasedOnRevision
    };
}

// Legacy actor fields remain for wire compatibility only. The JWT principal is authoritative.
public sealed record EngineeringSaveRequest(string ProjectName, string? SavedBy = null);
public sealed record EngineeringPublishRequest(string? PublishedBy = null);
public sealed record EngineeringActivateRequest(string? ActivatedBy = null);
