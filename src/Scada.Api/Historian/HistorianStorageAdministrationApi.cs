using Microsoft.AspNetCore.Mvc;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Historian.Abstractions;
using Scada.Historian.Archives;
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
                    rawSampleStorageLimitBytes = policy?.RawSampleStorageLimitBytes,
                    rawSampleStorageLimitAction = policy?.RawSampleStorageLimitAction.ToString() ?? HistorianRawStorageLimitAction.WarnOnly.ToString(),
                    rawSampleLimitExceeded = policy?.RawSampleStorageLimitBytes is { } limitBytes &&
                        usage.RawSamplesBytes is { } rawBytes && rawBytes > limitBytes,
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
                    },
                    archive = await DescribeArchivesAsync(services, cancellationToken)
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

            if (request.ConfigureRawSampleStorageLimit && request.RawSampleStorageLimitBytes is <= 0)
                return Results.BadRequest(new { error = "Raw sample storage limit must be empty or a positive number of bytes." });
            if (request.ConfigureRawSampleStorageLimit &&
                !TryParseLimitAction(request.RawSampleStorageLimitAction, out _))
                return Results.BadRequest(new { error = "Raw sample storage limit action is invalid." });

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
                var limitAction = request.ConfigureRawSampleStorageLimit
                    ? ParseLimitAction(request.RawSampleStorageLimitAction)
                    : current?.RawSampleStorageLimitAction ?? HistorianRawStorageLimitAction.WarnOnly;
                var limitBytes = request.ConfigureRawSampleStorageLimit
                    ? request.RawSampleStorageLimitBytes
                    : current?.RawSampleStorageLimitBytes;
                var next = new HistorianStoragePolicy(
                    current?.Key ?? "engineering-database",
                    nextRule,
                    current?.Downsampling,
                    limitBytes,
                    limitAction);
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
                        ["rawSampleStorageLimitBytes"] = next.RawSampleStorageLimitBytes?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none",
                        ["rawSampleStorageLimitAction"] = next.RawSampleStorageLimitAction.ToString(),
                        ["adoptedUnmanagedPolicy"] = (current is null).ToString()
                    });

                return Results.Ok(new
                {
                    policyManaged = true,
                    rawRetentionEnabled = next.RawRetention.Enabled,
                    rawRetentionDays = next.RawRetention.Duration?.TotalDays,
                    rawSampleStorageLimitBytes = next.RawSampleStorageLimitBytes,
                    rawSampleStorageLimitAction = next.RawSampleStorageLimitAction.ToString(),
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

        group.MapPost("/archives", async (
            HistorianArchiveCreateRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IHistorian historian,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, audit, cancellationToken, AuditActions.HistorianArchiveCreate);
            if (failure is not null) return failure;
            var archive = services.GetService<IHistorianArchiveService>();
            if (historian is not TimescaleDbHistorian || archive is null)
                return Results.Conflict(new { error = "Historian archive requires the TimescaleDB provider." });
            if (request.DeleteSourceAfterVerification && !request.ConfirmDeleteSourceAfterVerification)
                return Results.UnprocessableEntity(new { error = "Confirm source removal after archive read-back verification." });

            try
            {
                var job = await archive.CreateAsync(
                    request.FromUtc,
                    request.ToUtc,
                    request.DeleteSourceAfterVerification,
                    cancellationToken);
                await audit.RecordAsync(
                    context,
                    security.GetPrincipal(context),
                    AuditActions.HistorianArchiveCreate,
                    AuditOutcome.Succeeded,
                    "historian-archive",
                    job.ArchiveId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["fromUtc"] = job.FromUtc.ToString("O"),
                        ["toUtc"] = job.ToUtc.ToString("O"),
                        ["deleteSourceAfterVerification"] = job.DeleteSourceAfterVerification.ToString()
                    });
                return Results.Accepted($"/api/admin/historian-storage/archives/{job.ArchiveId:D}", ToArchiveJobResponse(job));
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "Archive time range is invalid." });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Results.Json(new { error = "Configured Historian archive destination is unavailable." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        group.MapPut("/archives/schedule", async (
            HistorianArchiveScheduleRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IHistorian historian,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, audit, cancellationToken, AuditActions.HistorianArchiveScheduleConfigure);
            if (failure is not null) return failure;
            var archive = services.GetService<IHistorianArchiveService>();
            if (historian is not TimescaleDbHistorian || archive is null)
                return Results.Conflict(new { error = "Historian archive scheduling requires the TimescaleDB provider." });
            if (request.DeleteSourceAfterVerification && !request.ConfirmDeleteSourceAfterVerification)
                return Results.UnprocessableEntity(new { error = "Confirm source removal after each verified scheduled archive." });

            try
            {
                var schedule = await archive.SaveScheduleAsync(
                    new HistorianArchiveScheduleConfiguration(
                        request.Enabled,
                        request.StartFromUtc,
                        request.ArchiveAfterDays,
                        request.IntervalDays,
                        request.WindowDays,
                        request.KeepArchivesDays,
                        request.DeleteSourceAfterVerification),
                    cancellationToken);
                await audit.RecordAsync(
                    context,
                    security.GetPrincipal(context),
                    AuditActions.HistorianArchiveScheduleConfigure,
                    AuditOutcome.Succeeded,
                    "historian-archive-schedule",
                    "singleton",
                    new Dictionary<string, string>
                    {
                        ["enabled"] = schedule.Enabled.ToString(),
                        ["startFromUtc"] = schedule.StartFromUtc.ToString("O"),
                        ["nextFromUtc"] = schedule.NextFromUtc.ToString("O"),
                        ["archiveAfterDays"] = schedule.ArchiveAfterDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["intervalDays"] = schedule.IntervalDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["windowDays"] = schedule.WindowDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["keepArchivesDays"] = schedule.KeepArchivesDays?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "forever",
                        ["deleteSourceAfterVerification"] = schedule.DeleteSourceAfterVerification.ToString(),
                        ["nextRunAtUtc"] = schedule.NextRunAtUtc.ToString("O")
                    });
                return Results.Ok(schedule);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "Historian archive schedule settings are invalid." });
            }
        });

        group.MapPost("/archives/{archiveId:guid}/resume", async (
            Guid archiveId,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, audit, cancellationToken, AuditActions.HistorianArchiveResume);
            if (failure is not null) return failure;
            var archive = services.GetService<IHistorianArchiveService>();
            if (archive is null) return Results.Conflict(new { error = "Historian archive requires the TimescaleDB provider." });
            var job = await archive.ResumeAsync(archiveId, cancellationToken);
            if (job is null) return Results.NotFound();
            await audit.RecordAsync(
                context,
                security.GetPrincipal(context),
                AuditActions.HistorianArchiveResume,
                AuditOutcome.Succeeded,
                "historian-archive",
                archiveId.ToString("D"),
                new Dictionary<string, string> { ["state"] = job.State.ToString() });
            return Results.Accepted($"/api/admin/historian-storage/archives/{archiveId:D}", ToArchiveJobResponse(job));
        });

        group.MapGet("/archives/{archiveId:guid}/download", async (
            Guid archiveId,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, audit, cancellationToken, AuditActions.HistorianArchiveDownload);
            if (failure is not null) return failure;
            var archive = services.GetService<IHistorianArchiveService>();
            if (archive is null) return Results.Conflict(new { error = "Historian archive requires the TimescaleDB provider." });
            var stream = await archive.OpenCompletedArchiveAsync(archiveId, cancellationToken);
            if (stream is null) return Results.NotFound();
            await audit.RecordAsync(
                context,
                security.GetPrincipal(context),
                AuditActions.HistorianArchiveDownload,
                AuditOutcome.Succeeded,
                "historian-archive",
                archiveId.ToString("D"),
                new Dictionary<string, string>());
            return Results.File(stream, "application/vnd.scadivo.historian-archive", $"{archiveId:N}.scadhist", enableRangeProcessing: true);
        });

        group.MapPost("/archives/restore", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            IHistorian historian,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            var failure = await AuthorizeAsync(context, runtime, security, audit, cancellationToken, AuditActions.HistorianArchiveRestore);
            if (failure is not null) return failure;
            var archive = services.GetService<IHistorianArchiveService>();
            if (historian is not TimescaleDbHistorian || archive is null)
                return Results.Conflict(new { error = "Historian archive restore requires the TimescaleDB provider." });
            if (!context.Request.ContentType?.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase) ?? true)
                return Results.BadRequest(new { error = "Upload the archive as application/octet-stream." });

            try
            {
                var result = await archive.RestoreAsync(context.Request.Body, cancellationToken);
                await audit.RecordAsync(
                    context,
                    security.GetPrincipal(context),
                    AuditActions.HistorianArchiveRestore,
                    AuditOutcome.Succeeded,
                    "historian-archive",
                    result.ArchiveId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["samplesRestored"] = result.SamplesRestored.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["segmentsRestored"] = result.SegmentsRestored.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["segmentsAlreadyRestored"] = result.SegmentsAlreadyRestored.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["sha256"] = result.Sha256
                    });
                return Results.Ok(result);
            }
            catch (InvalidDataException)
            {
                return Results.BadRequest(new { error = "Historian archive failed format or integrity verification; no samples were restored." });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Results.Json(new { error = "Historian archive could not be read from the configured server storage." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).WithMetadata(new RequestSizeLimitAttribute(HistorianArchiveLimits.MaximumUploadBytes));
    }

    private static async Task<object> DescribeArchivesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var archive = services.GetService<IHistorianArchiveService>();
        if (archive is null) return new { supported = false, destination = (string?)null, jobs = Array.Empty<HistorianArchiveJob>() };
        var jobs = await archive.ListAsync(cancellationToken);
        return new
        {
            supported = true,
            destination = archive.DestinationDescription,
            schedule = await archive.GetScheduleAsync(cancellationToken),
            jobs = jobs.Select(ToArchiveJobResponse).ToArray()
        };
    }

    private static HistorianArchiveJobResponse ToArchiveJobResponse(HistorianArchiveJob job) => new(
        job.ArchiveId,
        job.FromUtc,
        job.ToUtc,
        job.NextSegmentFromUtc,
        job.NextSequence,
        job.State.ToString(),
        job.DeleteSourceAfterVerification,
        job.SamplesArchived,
        job.FailureCode,
        job.Sha256,
        job.ArchiveLengthBytes,
        job.CreatedAtUtc,
        job.UpdatedAtUtc,
        job.CompletedAtUtc,
        job.ArchiveFileRemovedAtUtc is null,
        job.ArchiveFileRemovedAtUtc);

    private static bool TryParseLimitAction(string? value, out HistorianRawStorageLimitAction action)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Enum.GetNames<HistorianRawStorageLimitAction>().Any(name => name.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            action = HistorianRawStorageLimitAction.WarnOnly;
            return string.IsNullOrWhiteSpace(value);
        }

        return Enum.TryParse(value, ignoreCase: true, out action) && Enum.IsDefined(action);
    }

    private static HistorianRawStorageLimitAction ParseLimitAction(string? value) =>
        TryParseLimitAction(value, out var action) ? action : HistorianRawStorageLimitAction.WarnOnly;

    private static async Task<IResult?> AuthorizeAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        ApiAuthorizationService security,
        ApiAuditService audit,
        CancellationToken cancellationToken,
        string action = AuditActions.HistorianStorageRetentionConfigure)
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
                action,
                "historian-storage-policy",
                "active");
        }
        return failure;
    }
}

public sealed record HistorianRawRetentionRequest(
    double? RawRetentionDays,
    bool AcknowledgeUnmanagedPolicyReplacement = false,
    bool AllowPotentialDataExpiration = false,
    bool ConfigureRawSampleStorageLimit = false,
    long? RawSampleStorageLimitBytes = null,
    string? RawSampleStorageLimitAction = null);

public sealed record HistorianArchiveCreateRequest(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    bool DeleteSourceAfterVerification = false,
    bool ConfirmDeleteSourceAfterVerification = false);

public sealed record HistorianArchiveScheduleRequest(
    bool Enabled,
    DateTimeOffset StartFromUtc,
    int ArchiveAfterDays,
    int IntervalDays,
    int WindowDays,
    int? KeepArchivesDays,
    bool DeleteSourceAfterVerification = false,
    bool ConfirmDeleteSourceAfterVerification = false);

public sealed record HistorianArchiveJobResponse(
    Guid ArchiveId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    DateTimeOffset NextSegmentFromUtc,
    int NextSequence,
    string State,
    bool DeleteSourceAfterVerification,
    long SamplesArchived,
    string? FailureCode,
    string? Sha256,
    long? ArchiveLengthBytes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool ArchiveFileAvailable,
    DateTimeOffset? ArchiveFileRemovedAtUtc);
