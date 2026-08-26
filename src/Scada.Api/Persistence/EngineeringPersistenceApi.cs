using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Engineering.Contracts;
using Scada.Engineering.Persistence;
using Scada.Persistence.PostgreSql;

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
        builder.Services.TryAddSingleton<IPublishedRuntimeActivationService, PublishedRuntimeActivationService>();
    }

    public static async Task InitializeEngineeringPersistenceAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        var persistence = app.Services.GetService<IEngineeringProjectPersistenceService>();
        if (persistence is not null)
            await persistence.InitializeAsync(cancellationToken);
    }

    public static void MapEngineeringPersistenceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/engineering/persistence");

        group.MapGet("/status", (HttpContext context) => Results.Ok(new
        {
            enabled = Resolve(context) is not null,
            provider = Resolve(context) is null ? null : "postgresql"
        }));

        group.MapPost("/{projectKey}/save", async (
            string projectKey,
            EngineeringSaveRequest request,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var persistence = Resolve(context);
            if (persistence is null) return Disabled();
            if (string.IsNullOrWhiteSpace(request.ProjectName))
                return Results.BadRequest(new { error = "Project name is required." });

            var snapshot = await persistence.SaveCurrentAsync(
                projectKey,
                request.ProjectName,
                request.SavedBy,
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

        group.MapPost("/{projectKey}/published/activate", async (
            string projectKey,
            EngineeringActivateRequest request,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var activationService = ResolveActivation(context);
            if (activationService is null) return Disabled();

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

    private static IPublishedRuntimeActivationService? ResolveActivation(HttpContext context) =>
        context.RequestServices.GetService<IPublishedRuntimeActivationService>();

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
        snapshot.SavedBy
    };
}

public sealed record EngineeringSaveRequest(string ProjectName, string? SavedBy = null);
public sealed record EngineeringPublishRequest(string? PublishedBy = null);
public sealed record EngineeringActivateRequest(string? ActivatedBy = null);
