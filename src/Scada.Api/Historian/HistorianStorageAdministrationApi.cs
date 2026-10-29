using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Historian.Abstractions;
using Scada.Historian.Policies;
using Scada.Historian.TimescaleDb;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Historian;

public static class HistorianStorageAdministrationApi
{
    public static void MapHistorianStorageAdministrationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/historian-storage");

        group.MapGet("/", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IHistorian historian,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, audit, cancellationToken);
            if (failure is not null) return failure;
            var store = services.GetService<IHistorianRetentionDownsamplingStore>();
            if (historian is not TimescaleDbHistorian || store is null)
                return Results.Ok(new
                {
                    provider = HistorianConfiguration.DescribeProvider(historian),
                    supported = false,
                    reason = "Historian storage measurements and retention administration require the TimescaleDB provider."
                });

            try
            {
                var policy = await store.GetAppliedPolicyAsync(cancellationToken);
                var usage = await store.GetStorageUsageAsync(cancellationToken);
                return Results.Ok(new
                {
                    provider = "timescaledb",
                    supported = true,
                    policyManaged = policy is not null,
                    rawRetentionEnabled = policy?.RawRetention.Enabled ?? false,
                    rawRetentionDays = policy?.RawRetention.Duration?.TotalDays,
                    aggregatePolicyPresent = policy?.EffectiveDownsampling.Count > 0,
                    policy,
                    usage = new
                    {
                        databaseBytes = usage.DatabaseBytes,
                        rawSamplesBytes = usage.RawSamplesBytes,
                        aggregateBytes = usage.AggregateBytes,
                        historianBytes = usage.HistorianBytes,
                        filesystemFreeBytes = (long?)null,
                        estimatedDailyGrowthBytes = (long?)null,
                        measuredAtUtc = usage.MeasuredAtUtc
                    }
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return Results.Json(
                    new { error = "Historian storage usage or policy could not be read from the active database." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        group.MapPut("/retention", async (
            HistorianRawRetentionRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IHistorian historian,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, audit, cancellationToken);
            if (failure is not null) return failure;
            var store = services.GetService<IHistorianRetentionDownsamplingStore>();
            if (historian is not TimescaleDbHistorian || store is null)
                return Results.Conflict(new { error = "Historian storage administration requires the TimescaleDB provider." });
            if (request.RawRetentionDays is <= 0 or > 36500 ||
                request.RawRetentionDays.HasValue && !double.IsFinite(request.RawRetentionDays.Value))
                return Results.BadRequest(new { error = "Raw retention days must be empty or a finite value between 0 and 36500." });

            try
            {
                var current = await store.GetAppliedPolicyAsync(cancellationToken);
                if (current is null && !request.AcknowledgeUnmanagedPolicyReplacement)
                    return Results.Conflict(new
                    {
                        error = "No SCADIVO policy is recorded. First application replaces existing Timescale retention and aggregate-refresh jobs; aggregate tiers absent from the submitted policy will be disabled. Explicit confirmation is required."
                    });

                var nextRule = request.RawRetentionDays is { } days
                    ? new HistorianRetentionRule(true, TimeSpan.FromDays(days))
                    : new HistorianRetentionRule();
                var next = new HistorianStoragePolicy(
                    current?.Key ?? "engineering-database",
                    nextRule,
                    current?.Downsampling);
                next.Validate();

                if (HistorianPolicySafety.RequiresExplicitDataExpirationApproval(current, next) &&
                    !request.AllowPotentialDataExpiration)
                    return Results.UnprocessableEntity(new
                    {
                        error = "This retention change can expire stored samples. Confirm the possible data expiration before applying it."
                    });

                await store.ApplyPolicyAsync(
                    next,
                    new HistorianPolicyApplyOptions(request.AllowPotentialDataExpiration),
                    cancellationToken);
                await audit.RecordAsync(
                    context,
                    security.GetPrincipal(context),
                    AuditActions.HistorianStorageRetentionConfigure,
                    AuditOutcome.Succeeded,
                    "historian-storage-policy",
                    next.Key,
                    new Dictionary<string, string>
                    {
                        ["rawRetentionEnabled"] = next.RawRetention.Enabled.ToString(),
                        ["rawRetentionDays"] = next.RawRetention.Duration?.TotalDays.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "none",
                        ["adoptedUnmanagedPolicy"] = (current is null).ToString()
                    });

                return Results.Ok(new
                {
                    policyManaged = true,
                    rawRetentionEnabled = next.RawRetention.Enabled,
                    rawRetentionDays = next.RawRetention.Duration?.TotalDays,
                    policy = next
                });
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "Historian retention policy is invalid." });
            }
            catch (InvalidOperationException)
            {
                return Results.UnprocessableEntity(new { error = "Historian retention policy was not applied because it may expire data without the required confirmation." });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return Results.Json(
                    new { error = "Historian retention policy could not be confirmed. Refresh the active policy before retrying." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });
    }

    private static async Task<IResult?> AuthorizeAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        ApiAuthorizationService security,
        ApiAuditService audit,
        CancellationToken cancellationToken)
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
                AuditActions.HistorianStorageRetentionConfigure,
                "historian-storage-policy",
                "active");
        }
        return failure;
    }
}

public sealed record HistorianRawRetentionRequest(
    double? RawRetentionDays,
    bool AcknowledgeUnmanagedPolicyReplacement = false,
    bool AllowPotentialDataExpiration = false);
