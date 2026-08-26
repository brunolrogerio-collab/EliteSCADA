using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Api.HostedServices;
using Scada.Api.Runtime;
using Scada.Persistence.PostgreSql;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public static class AuditApi
{
    public const string NextCursorHeader = "X-EliteSCADA-Audit-Next-Cursor";

    public static void AddConfiguredAudit(this WebApplicationBuilder builder)
    {
        var authenticationEnabled = builder.Configuration
            .GetSection("Authentication")
            .GetValue<bool>("Enabled");
        builder.AddLocalIdentity(authenticationEnabled);

        var queryPolicy = new AuditQueryPolicy(
            builder.Configuration.GetValue<int?>("Audit:Query:MaximumPageSize") ?? 1000);
        queryPolicy.Validate();

        var retentionPolicy = new AuditRetentionPolicy(
            Enabled: builder.Configuration.GetValue<bool?>("Audit:Retention:Enabled") ?? false,
            MaximumAge: builder.Configuration.GetValue<TimeSpan?>("Audit:Retention:MaximumAge"),
            BatchSize: builder.Configuration.GetValue<int?>("Audit:Retention:BatchSize") ?? 1000,
            Interval: builder.Configuration.GetValue<TimeSpan?>("Audit:Retention:Interval"),
            MaximumBatchesPerRun: builder.Configuration.GetValue<int?>("Audit:Retention:MaximumBatchesPerRun") ?? 100);
        retentionPolicy.Validate();
        if (retentionPolicy.Enabled && retentionPolicy.MaximumAge.HasValue && !retentionPolicy.Interval.HasValue)
        {
            throw new InvalidOperationException(
                "Audit:Retention:Interval is required when finite Audit retention is enabled.");
        }

        var bufferPolicy = new AuditBufferPolicy(
            Capacity: builder.Configuration.GetValue<int?>("Audit:Buffer:Capacity") ?? 1024,
            RetryDelay: builder.Configuration.GetValue<TimeSpan?>("Audit:Buffer:RetryDelay"),
            ShutdownFlushTimeout: builder.Configuration.GetValue<TimeSpan?>("Audit:Buffer:ShutdownFlushTimeout"));
        bufferPolicy.Validate();

        builder.Services.TryAddSingleton(queryPolicy);
        builder.Services.TryAddSingleton(retentionPolicy);
        builder.Services.TryAddSingleton(bufferPolicy);

        var connectionString = builder.Configuration.GetConnectionString("EliteScada");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            builder.Services.TryAddSingleton(sp =>
                new InMemoryAuditSink(sp.GetRequiredService<AuditQueryPolicy>()));
            builder.Services.TryAddSingleton<IAuditStore>(sp => sp.GetRequiredService<InMemoryAuditSink>());
        }
        else
        {
            builder.Services.TryAddSingleton(sp =>
                new PostgreSqlAuditStore(connectionString, sp.GetRequiredService<AuditQueryPolicy>()));
            builder.Services.TryAddSingleton<IAuditStore>(sp => sp.GetRequiredService<PostgreSqlAuditStore>());
        }

        builder.Services.TryAddSingleton(sp =>
            new BufferedAuditSink(
                sp.GetRequiredService<IAuditStore>(),
                sp.GetRequiredService<AuditBufferPolicy>()));
        builder.Services.TryAddSingleton<IAuditSink>(sp => sp.GetRequiredService<BufferedAuditSink>());
        builder.Services.TryAddSingleton(sp =>
            new AuditRetentionCoordinator(
                sp.GetRequiredService<IAuditStore>(),
                sp.GetRequiredService<AuditRetentionPolicy>()));

        if (retentionPolicy.Enabled && retentionPolicy.MaximumAge.HasValue)
            builder.Services.AddHostedService<AuditRetentionHostedService>();

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
            string? targetKind,
            string? targetId,
            string? area,
            string? correlationId,
            string? cursor,
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
                var after = string.IsNullOrWhiteSpace(cursor) ? null : DecodeCursor(cursor);
                var page = await store.QueryPageAsync(
                    new AuditQuery(
                        PageSize: limit ?? 100,
                        FromUtc: fromUtc,
                        ToUtc: toUtc,
                        SubjectId: subjectId,
                        Action: action,
                        Outcome: outcome,
                        TargetKind: targetKind,
                        TargetId: targetId,
                        Area: area,
                        CorrelationId: correlationId,
                        After: after),
                    cancellationToken);

                if (page.NextCursor is not null)
                    context.Response.Headers[NextCursorHeader] = EncodeCursor(page.NextCursor);

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.AuditRead,
                    AuditOutcome.Succeeded,
                    "audit",
                    "events",
                    new Dictionary<string, string>
                    {
                        ["resultCount"] = page.Events.Count.ToString(CultureInfo.InvariantCulture),
                        ["hasMore"] = (page.NextCursor is not null).ToString(CultureInfo.InvariantCulture)
                    });

                // Keep the established array response contract. Pagination metadata is carried by the cursor header.
                return Results.Ok(page.Events);
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
            catch (FormatException)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.AuditRead,
                    AuditOutcome.Failed,
                    "audit",
                    "events",
                    new Dictionary<string, string> { ["reason"] = "invalid-cursor" });
                return Results.BadRequest(new { error = "Audit cursor is invalid." });
            }
        });

        app.MapGet("/api/audit/diagnostics", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            IAuditStore store,
            BufferedAuditSink buffer,
            AuditRetentionPolicy retention,
            CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.SystemAdmin,
                cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            return Results.Ok(new
            {
                store = store.GetHealthSnapshot(),
                buffer = buffer.GetHealthSnapshot(),
                retention = new
                {
                    retention.Enabled,
                    retention.MaximumAge,
                    retention.BatchSize,
                    retention.Interval,
                    retention.MaximumBatchesPerRun,
                    finiteRetentionActive = retention.Enabled && retention.MaximumAge.HasValue
                }
            });
        });
    }

    private static string EncodeCursor(AuditCursor cursor)
    {
        var normalized = cursor.ToUtc();
        var payload = $"{normalized.TimestampUtc.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{normalized.Id:D}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static AuditCursor DecodeCursor(string encoded)
    {
        var base64 = encoded.Trim().Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        var payload = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        var separator = payload.IndexOf('|');
        if (separator <= 0 || separator == payload.Length - 1)
            throw new FormatException("Audit cursor payload is invalid.");

        if (!long.TryParse(payload[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var utcTicks) ||
            utcTicks < DateTimeOffset.MinValue.UtcTicks || utcTicks > DateTimeOffset.MaxValue.UtcTicks ||
            !Guid.TryParseExact(payload[(separator + 1)..], "D", out var id) || id == Guid.Empty)
        {
            throw new FormatException("Audit cursor payload is invalid.");
        }

        return new AuditCursor(new DateTimeOffset(utcTicks, TimeSpan.Zero), id);
    }
}
