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
