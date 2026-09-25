using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Api.Engineering;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;
using Scada.Engineering.Persistence;
using Scada.Engineering.Reports;
using Scada.Engineering.VisualAssets;
using Scada.Persistence.PostgreSql;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Persistence;

public static class EngineeringPersistenceApi
{
    private static readonly SemaphoreSlim FirstProjectGate = new(1, 1);

    public static void AddOptionalEngineeringPersistence(this WebApplicationBuilder builder)
    {
        builder.AddEngineeringDriverCatalog();

        builder.Services.TryAddSingleton<IVisualAssetEngineeringRegistry>(sp =>
            sp.GetRequiredService<EngineeringWorkspace>().VisualAssets);
        builder.Services.TryAddSingleton<IReportEngineeringRegistry>(sp =>
            new InMemoryReportEngineeringRegistry(
                sp.GetRequiredService<EngineeringWorkspace>().MarkDirty));

        var connectionString = builder.Configuration.GetConnectionString("EliteScada");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        builder.Services.TryAddSingleton<IEngineeringProjectStore>(_ =>
            new PostgreSqlEngineeringProjectStore(connectionString));
        builder.Services.TryAddSingleton<IEngineeringProjectCatalog>(_ =>
            new PostgreSqlEngineeringProjectCatalog(connectionString));
        builder.Services.TryAddSingleton<IEngineeringInstallationBindingStore>(_ =>
            new PostgreSqlEngineeringInstallationBindingStore(connectionString));
        builder.Services.TryAddSingleton<IEngineeringProjectPersistenceService, EngineeringProjectPersistenceService>();
        builder.Services.TryAddSingleton<IEngineeringWorkspaceCheckoutService, EngineeringWorkspaceCheckoutService>();
        builder.Services.TryAddSingleton<IEngineeringWorkingBootstrapService, EngineeringWorkingBootstrapService>();
        builder.Services.TryAddSingleton<IPublishedRuntimeActivationService, PublishedRuntimeActivationService>();
        builder.Services.TryAddSingleton<IPersistedRuntimeRecoveryService, PersistedRuntimeRecoveryService>();
    }

    public static async Task InitializeEngineeringPersistenceStorageAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        var persistence = app.Services.GetService<IEngineeringProjectPersistenceService>();
        if (persistence is null) return;

        // Storage/schema initialization is an explicit startup phase and must have
        // completed before canonical Authority hydration. Repeating it here remains
        // safe for focused hosts that call this method directly.
        await app.InitializeEngineeringPersistenceStorageAsync(cancellationToken);
        var bindingStore = app.Services.GetService<IEngineeringInstallationBindingStore>();
        if (bindingStore is not null)
            await bindingStore.InitializeAsync(cancellationToken);
    }

    public static async Task InitializeEngineeringPersistenceAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        var configuredRuntimeProjectKey = app.Configuration["EngineeringRuntime:ProjectKey"];
        var configuredWorkingProjectKey = app.Configuration["EngineeringWorking:ProjectKey"];
        var configuredWorkingRevisionValue = app.Configuration["EngineeringWorking:Revision"];
        var persistence = app.Services.GetService<IEngineeringProjectPersistenceService>();
        if (persistence is null)
        {
            if (!string.IsNullOrWhiteSpace(configuredRuntimeProjectKey) ||
                !string.IsNullOrWhiteSpace(configuredWorkingProjectKey) ||
                !string.IsNullOrWhiteSpace(configuredWorkingRevisionValue))
            {
                throw new InvalidOperationException(
                    "Engineering Working/Runtime persistence is configured, but engineering persistence is unavailable. Configure ConnectionStrings:EliteScada before selecting a persisted project.");
            }

            app.Services.GetRequiredService<EngineeringWorkspace>().InitializeDemo();
            return;
        }

        long? configuredWorkingRevision = null;
        if (!string.IsNullOrWhiteSpace(configuredWorkingRevisionValue))
        {
            if (!long.TryParse(configuredWorkingRevisionValue, out var revision) || revision < 1)
                throw new InvalidOperationException("EngineeringWorking:Revision must be a positive integer.");
            configuredWorkingRevision = revision;
        }

        await persistence.InitializeAsync(cancellationToken);
        var bindingStore = app.Services.GetService<IEngineeringInstallationBindingStore>();
        if (bindingStore is null)
        {
            // Compatibility for focused/unit hosts that inject persistence directly instead
            // of using AddOptionalEngineeringPersistence. Production PostgreSQL wiring always
            // registers the durable FND-07 binding store.
            var legacyBootstrap = app.Services.GetRequiredService<IEngineeringWorkingBootstrapService>();
            var legacyBootstrapResult = await legacyBootstrap.BootstrapAsync(
                configuredWorkingProjectKey,
                configuredWorkingRevision,
                configuredRuntimeProjectKey,
                cancellationToken);
            if (legacyBootstrapResult.Source == EngineeringWorkingBootstrapSource.EmptyCatalog &&
                app.Configuration.GetValue<bool>("Engineering:InitializeDemoWhenEmpty"))
                app.Services.GetRequiredService<EngineeringWorkspace>().InitializeDemo();
            await app.RecoverConfiguredEngineeringRuntimeAsync(cancellationToken);
            return;
        }

        var binding = await bindingStore.GetAsync(cancellationToken);
        if (binding.State == EngineeringInstallationBindingState.Legacy)
        {
            var catalog = app.Services.GetRequiredService<IEngineeringProjectCatalog>();
            var projects = await catalog.ListAsync(cancellationToken);
            string? adopted = null;
            if (!string.IsNullOrWhiteSpace(configuredRuntimeProjectKey) &&
                projects.Any(project => project.ProjectKey.Equals(configuredRuntimeProjectKey, StringComparison.OrdinalIgnoreCase)))
                adopted = configuredRuntimeProjectKey.Trim();
            else if (projects.Count == 1)
                adopted = projects.Single().ProjectKey;
            else if (projects.Count > 1)
                throw new InvalidOperationException(
                    "FND-07 cannot infer the attached Application from multiple persisted projects. Configure EngineeringRuntime:ProjectKey.");
            binding = await bindingStore.AdoptLegacyAsync(adopted, cancellationToken);
        }

        if (binding.State is EngineeringInstallationBindingState.Neutral or
            EngineeringInstallationBindingState.DetachInProgress)
        {
            app.Services.GetRequiredService<EngineeringWorkspace>().ResetToNeutral();
            return;
        }

        var bootstrap = app.Services.GetRequiredService<IEngineeringWorkingBootstrapService>();
        var bootstrapResult = await bootstrap.BootstrapAsync(
            configuredWorkingProjectKey,
            configuredWorkingRevision,
            binding.ProjectKey ?? configuredRuntimeProjectKey,
            cancellationToken);
        if (bootstrapResult.Source == EngineeringWorkingBootstrapSource.EmptyCatalog &&
            app.Configuration.GetValue<bool>("Engineering:InitializeDemoWhenEmpty"))
            app.Services.GetRequiredService<EngineeringWorkspace>().InitializeDemo();
        await app.RecoverConfiguredEngineeringRuntimeAsync(cancellationToken);
    }

    public static async Task<PersistedRuntimeRecoveryResult?> RecoverConfiguredEngineeringRuntimeAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        var binding = app.Services.GetService<IEngineeringInstallationBindingStore>() is { } bindingStore
            ? await bindingStore.GetAsync(cancellationToken)
            : null;
        if (binding?.State is EngineeringInstallationBindingState.Neutral or
            EngineeringInstallationBindingState.DetachInProgress or
            EngineeringInstallationBindingState.AttachInProgress)
            return null;

        var projectKey = binding?.ProjectKey ?? app.Configuration["EngineeringRuntime:ProjectKey"];
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
        app.MapEngineeringDriverCatalogEndpoints();

        var group = app.MapGroup("/api/engineering/persistence");

        group.MapGet("/status", async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            var catalog = ResolveCatalog(context);
            return Results.Ok(new
            {
                enabled = persistence is not null,
                provider = persistence is null ? null : "postgresql",
                configuredProjectKey = ResolveConfiguredProjectKey(context),
                hasProjects = catalog is null ? (bool?)null : await catalog.HasAnyAsync(cancellationToken)
            });
        });

        group.MapPost("/projects/first", async (
            EngineeringFirstProjectRequest request,
            EngineeringWorkspace workspace,
            IGatewayEngineeringRegistry gateways,
            IReportEngineeringRegistry reports,
            HttpContext context,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            var catalog = ResolveCatalog(context);
            if (persistence is null || catalog is null) return Disabled();

            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    "engineering.project.create_first",
                    "engineering-project",
                    request.ProjectKey?.Trim() ?? "new");
                return failure;
            }

            var projectKey = request.ProjectKey?.Trim() ?? string.Empty;
            var projectName = request.ProjectName?.Trim() ?? string.Empty;
            if (projectKey.Length is < 1 or > 200)
                return Results.BadRequest(new { error = "Project key must contain between 1 and 200 characters." });
            if (projectName.Length is < 1 or > 300)
                return Results.BadRequest(new { error = "Project name must contain between 1 and 300 characters." });

            await FirstProjectGate.WaitAsync(cancellationToken);
            try
            {
                var bindingStore = context.RequestServices.GetService<IEngineeringInstallationBindingStore>();
                EngineeringInstallationBindingSnapshot? attaching = null;
                if (bindingStore is not null)
                {
                    var currentBinding = await bindingStore.GetAsync(cancellationToken);
                    if (currentBinding.State == EngineeringInstallationBindingState.Neutral)
                        attaching = await bindingStore.BeginAttachAsync(projectKey, cancellationToken);
                    else if (currentBinding.State != EngineeringInstallationBindingState.Legacy)
                        return Results.Conflict(new { error = "Another Application is already attached to this installation." });
                }

                if (await catalog.HasAnyAsync(cancellationToken))
                {
                    return Results.Conflict(new
                    {
                        error = "A persisted Engineering project already exists. First-project setup is closed."
                    });
                }

                var savedBy = authorization.Principal.DisplayName ?? authorization.Principal.SubjectId;
                EngineeringProjectSnapshot snapshot;
                try
                {
                    snapshot = await SaveFirstProjectAsync(
                        projectKey,
                        new EngineeringSaveRequest(projectName, savedBy),
                        persistence,
                        workspace,
                        gateways,
                        reports,
                        cancellationToken);
                }
                catch
                {
                    if (bindingStore is not null)
                    {
                        var currentBinding = await bindingStore.GetAsync(CancellationToken.None);
                        if (currentBinding.State == EngineeringInstallationBindingState.AttachInProgress &&
                            string.Equals(currentBinding.ProjectKey, projectKey, StringComparison.OrdinalIgnoreCase))
                            await bindingStore.AbortAttachAsync(projectKey, CancellationToken.None);
                    }
                    throw;
                }

                if (bindingStore is not null)
                {
                    var currentBinding = await bindingStore.GetAsync(cancellationToken);
                    if (currentBinding.State == EngineeringInstallationBindingState.AttachInProgress)
                        await bindingStore.CompleteAttachAsync(projectKey, cancellationToken);
                }

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    "engineering.project.create_first",
                    AuditOutcome.Succeeded,
                    "engineering-project",
                    snapshot.ProjectKey,
                    new Dictionary<string, string>
                    {
                        ["projectName"] = snapshot.ProjectName,
                        ["revision"] = snapshot.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                return Results.Created(
                    $"/api/engineering/persistence/{Uri.EscapeDataString(snapshot.ProjectKey)}/latest",
                    new
                    {
                        revision = ToMetadata(snapshot),
                        workspace = workspace.Describe()
                    });
            }
            finally
            {
                FirstProjectGate.Release();
            }
        });

        group.MapPost("/{projectKey}/save", async (
            string projectKey,
            EngineeringSaveRequest request,
            EngineeringWorkspace workspace,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();
            if (string.IsNullOrWhiteSpace(request.ProjectName))
                return Results.BadRequest(new { error = "Project name is required." });

            var snapshot = await SaveCurrentAsync(
                projectKey,
                request,
                persistence,
                workspace,
                cancellationToken);

            return Results.Ok(ToMetadata(snapshot));
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
            HttpContext context,
            CancellationToken cancellationToken) => ActivatePublishedAsync(
                projectKey,
                request,
                ResolveConfiguredProjectKey(context),
                ResolveActivation(context),
                cancellationToken));

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
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var checkout = ResolveCheckout(context);
            if (checkout is null) return Disabled();

            var outcome = await checkout.CheckoutAsync(projectKey, revision, cancellationToken);
            if (outcome is null) return Results.NotFound();

            var response = new
            {
                revision = ToMetadata(outcome.Snapshot),
                checkedOut = outcome.CheckedOut,
                preview = outcome.Preview,
                apply = outcome.ApplyResult,
                workspace = outcome.Workspace
            };

            return outcome.CheckedOut
                ? Results.Ok(response)
                : Results.BadRequest(response);
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
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var result = await persistence.ApplyLatestAsync(
                projectKey,
                mode ?? ImportMode.CreateAndUpdate,
                cancellationToken);

            return result is null ? Results.NotFound() : ToApplyResult(result);
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
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var result = await persistence.PublishRevisionAsync(
                projectKey,
                revision,
                request.PublishedBy,
                cancellationToken);

            if (result is null) return Results.NotFound();
            if (!result.Published)
                return Results.BadRequest(new
                {
                    revision = ToMetadata(result.Snapshot),
                    preview = result.Preview,
                    published = false
                });

            var lifecycle = await persistence.GetLifecycleAsync(projectKey, cancellationToken);
            return Results.Ok(new
            {
                revision = ToMetadata(result.Snapshot),
                publication = result.Publication,
                lifecycle
            });
        });

        group.MapPost("/{projectKey}/revisions/{revision:long}/apply", async (
            string projectKey,
            long revision,
            ImportMode? mode,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();

            var result = await persistence.ApplyRevisionAsync(
                projectKey,
                revision,
                mode ?? ImportMode.CreateAndUpdate,
                cancellationToken);

            return result is null ? Results.NotFound() : ToApplyResult(result);
        });
    }

    private static IEngineeringProjectPersistenceService? Resolve(HttpContext context) =>
        context.RequestServices.GetService<IEngineeringProjectPersistenceService>();

    private static IEngineeringProjectCatalog? ResolveCatalog(HttpContext context) =>
        context.RequestServices.GetService<IEngineeringProjectCatalog>();

    internal static async Task<EngineeringProjectSnapshot> SaveCurrentAsync(
        string projectKey,
        EngineeringSaveRequest request,
        IEngineeringProjectPersistenceService persistence,
        EngineeringWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        await using var mutation = await workspace.AcquireMutationAsync(
            cancellationToken: cancellationToken);
        var before = workspace.Describe();
        var saveVersion = workspace.CaptureChangeVersion();
        var basedOnRevision = before.ProjectKey?.Equals(projectKey, StringComparison.OrdinalIgnoreCase) == true
            ? before.BaseRevision
            : null;

        var snapshot = await persistence.SaveCurrentDerivedAsync(
            projectKey,
            request.ProjectName,
            basedOnRevision,
            request.SavedBy,
            cancellationToken);

        workspace.AcceptSave(
            snapshot.ProjectKey,
            snapshot.ProjectName,
            snapshot.Revision,
            snapshot.SavedAtUtc,
            saveVersion);
        return snapshot;
    }

    internal static async Task<EngineeringProjectSnapshot> SaveFirstProjectAsync(
        string projectKey,
        EngineeringSaveRequest request,
        IEngineeringProjectPersistenceService persistence,
        EngineeringWorkspace workspace,
        IGatewayEngineeringRegistry gateways,
        IReportEngineeringRegistry reports,
        CancellationToken cancellationToken = default)
    {
        await using var mutation = await workspace.AcquireMutationAsync(
            cancellationToken: cancellationToken);

        workspace.Clear();
        gateways.Clear();
        reports.Clear();
        foreach (var dynamo in BuiltinDynamoLibrary.Create())
            workspace.Assets.UpsertDynamo(dynamo);
        workspace.SecurityPolicies.UpsertRole(BuiltInSecurityRoleDefaults.CreateInitialDeveloperRole());

        var saveVersion = workspace.CaptureChangeVersion();
        var snapshot = await persistence.SaveCurrentDerivedAsync(
            projectKey,
            request.ProjectName,
            basedOnRevision: null,
            request.SavedBy,
            cancellationToken);

        workspace.AcceptSave(
            snapshot.ProjectKey,
            snapshot.ProjectName,
            snapshot.Revision,
            snapshot.SavedAtUtc,
            saveVersion);
        return snapshot;
    }

    private static IEngineeringWorkspaceCheckoutService? ResolveCheckout(HttpContext context) =>
        context.RequestServices.GetService<IEngineeringWorkspaceCheckoutService>();

    private static IPublishedRuntimeActivationService? ResolveActivation(HttpContext context) =>
        context.RequestServices.GetService<IPublishedRuntimeActivationService>();

    internal static async Task<IResult> ActivatePublishedAsync(
        string projectKey,
        EngineeringActivateRequest request,
        string? configuredProjectKey,
        IPublishedRuntimeActivationService? activationService,
        CancellationToken cancellationToken = default)
    {
        if (activationService is null) return Disabled();

        if (string.IsNullOrWhiteSpace(configuredProjectKey))
        {
            return Results.Conflict(new
            {
                error = "EngineeringRuntime:ProjectKey must be configured before activating a persisted runtime."
            });
        }

        if (!configuredProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new
            {
                error = $"This runtime instance is bound to project '{configuredProjectKey}', not '{projectKey}'."
            });
        }

        var outcome = await activationService.ActivateAsync(
            projectKey,
            request.ActivatedBy,
            cancellationToken);

        if (!outcome.Found || outcome.Snapshot is null)
            return Results.NotFound(new { error = "Project has no published revision." });

        var response = new
        {
            revision = ToMetadata(outcome.Snapshot),
            activated = outcome.Activated,
            runtime = outcome.Runtime,
            activation = outcome.Activation,
            lifecycle = outcome.Lifecycle
        };

        return outcome.Activated
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

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

public sealed record EngineeringSaveRequest(string ProjectName, string? SavedBy = null);
public sealed record EngineeringPublishRequest(string? PublishedBy = null);
public sealed record EngineeringActivateRequest(string? ActivatedBy = null);
public sealed record EngineeringFirstProjectRequest(string ProjectKey, string ProjectName);
