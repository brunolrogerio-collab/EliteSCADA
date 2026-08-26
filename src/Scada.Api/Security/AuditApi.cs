using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Api.Runtime;
using Scada.Persistence.PostgreSql;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public static class AuditApi
{
    public static void AddConfiguredAudit(this WebApplicationBuilder builder)
    {
        var authenticationEnabled = builder.Configuration
            .GetSection("Authentication")
            .GetValue<bool>("Enabled");
        builder.AddLocalIdentity(authenticationEnabled);

        var connectionString = builder.Configuration.GetConnectionString("EliteScada");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            builder.Services.TryAddSingleton<InMemoryAuditSink>();
            builder.Services.TryAddSingleton<IAuditStore>(sp => sp.GetRequiredService<InMemoryAuditSink>());
            builder.Services.TryAddSingleton<IAuditSink>(sp => sp.GetRequiredService<InMemoryAuditSink>());
        }
        else
        {
            builder.Services.TryAddSingleton(_ => new PostgreSqlAuditStore(connectionString));
            builder.Services.TryAddSingleton<IAuditStore>(sp => sp.GetRequiredService<PostgreSqlAuditStore>());
            builder.Services.TryAddSingleton<IAuditSink>(sp => sp.GetRequiredService<PostgreSqlAuditStore>());
        }

        builder.Services.TryAddSingleton<ApiAuditService>();
    }

    public static async Task InitializeAuditAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        await app.Services.GetRequiredService<IAuditStore>().InitializeAsync(cancellationToken);
        await app.InitializeLocalIdentityAsync();
    }

    public static void MapAuditEndpoints(this WebApplication app)
    {
        app.MapLocalIdentityEndpoints();

        app.MapGet("/api/audit", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IAuditStore store,
            int? limit,
            string? subjectId,
            string? action,
            AuditOutcome? outcome,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.SystemAdmin,
                cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.AuditRead,
                    "audit",
                    "events");
                return failure;
            }

            try
            {
                var events = await store.QueryAsync(
                    limit ?? 100,
                    subjectId,
                    action,
                    outcome,
                    fromUtc,
                    toUtc,
                    cancellationToken);

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.AuditRead,
                    AuditOutcome.Succeeded,
                    "audit",
                    "events",
                    new Dictionary<string, string>
                    {
                        ["resultCount"] = events.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                return Results.Ok(events);
            }
            catch (ArgumentException ex)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.AuditRead,
                    AuditOutcome.Failed,
                    "audit",
                    "events",
                    new Dictionary<string, string> { ["reason"] = ex.Message });
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}
