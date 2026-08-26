using Scada.Api.Security;
using Scada.Core.Alarms;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public sealed record TagBulkChanges(
    bool? ReadOnly = null,
    bool? HistorianEnabled = null,
    string? HistorianStrategy = null);

public sealed record AlarmBulkChanges(
    bool? Enabled = null,
    string? Priority = null,
    bool? RequiresAcknowledgement = null,
    bool? ShelvingAllowed = null);

public sealed record DataSourceBulkChanges(
    bool? Enabled = null);

public sealed record EngineeringBulkRequest(
    string EntityKind,
    IReadOnlyCollection<Guid> EntityIds,
    TagBulkChanges? Tags = null,
    AlarmBulkChanges? Alarms = null,
    DataSourceBulkChanges? DataSources = null);

public sealed record EngineeringBulkPreviewResult(
    long ChangeVersion,
    string EntityKind,
    int AffectedCount,
    ImportPreview Preview);

public sealed record EngineeringBulkApplyResult(
    long ChangeVersion,
    string EntityKind,
    int AffectedCount,
    ImportResult Result);

public static class EngineeringBulkEndpoints
{
    private const string WorkspaceVersionHeader = "x-elitescada-workspace-version";

    public static void MapEngineeringBulkEndpoints(this WebApplication app)
    {
        app.MapPost("/api/engineering/bulk/preview", (
            EngineeringBulkRequest request,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange) =>
        {
            var built = BuildCandidate(request, exchange.ExportPackage());
            if (built.Error is not null)
                return Results.BadRequest(new { error = built.Error });

            var preview = exchange.Preview(built.Package!, ImportMode.UpdateExisting);
            return Results.Ok(new EngineeringBulkPreviewResult(
                workspace.CaptureChangeVersion(),
                built.EntityKind!,
                built.AffectedCount,
                preview));
        }).RequireWorkspaceEngineeringRead();

        app.MapPost("/api/engineering/bulk/apply", async (
            EngineeringBulkRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    AuditActions.EngineeringBulkApply,
                    "engineering-workspace",
                    "bulk");
                return failure;
            }

            if (!TryReadExpectedVersion(context.Request, out var expectedChangeVersion))
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringBulkApply,
                    AuditOutcome.Failed,
                    "engineering-workspace",
                    "bulk",
                    new Dictionary<string, string> { ["reason"] = "missing-or-invalid-workspace-version" });
                return Results.BadRequest(new
                {
                    error = $"Header '{WorkspaceVersionHeader}' with a non-negative integer Workspace version is required."
                });
            }

            try
            {
                await using var mutation = await workspace.AcquireMutationAsync(
                    expectedChangeVersion,
                    context.RequestAborted);

                // Build from the authoritative live Workspace under the mutation lease.
                // The browser sends only entity IDs plus an explicitly supported homogeneous patch.
                var built = BuildCandidate(request, exchange.ExportPackage());
                if (built.Error is not null)
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        AuditActions.EngineeringBulkApply,
                        AuditOutcome.Failed,
                        "engineering-workspace",
                        "bulk",
                        new Dictionary<string, string>
                        {
                            ["reason"] = "invalid-request",
                            ["error"] = built.Error
                        });
                    return Results.BadRequest(new { error = built.Error });
                }

                var preview = exchange.Preview(built.Package!, ImportMode.UpdateExisting);
                if (!preview.CanApply)
                {
                    await audit.RecordAsync(
                        context,
                        authorization.Principal,
                        AuditActions.EngineeringBulkApply,
                        AuditOutcome.Failed,
                        "engineering-workspace",
                        "bulk",
                        new Dictionary<string, string>
                        {
                            ["reason"] = "preview-errors",
                            ["entityKind"] = built.EntityKind!,
                            ["affectedCount"] = built.AffectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["errorCount"] = preview.ErrorCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        });
                    return Results.BadRequest(new EngineeringBulkPreviewResult(
                        expectedChangeVersion,
                        built.EntityKind!,
                        built.AffectedCount,
                        preview));
                }

                var result = exchange.Apply(built.Package!, ImportMode.UpdateExisting);
                var hasErrors = result.Issues.Any(issue => issue.IsError);
                var resultingChangeVersion = workspace.CaptureChangeVersion();

                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringBulkApply,
                    hasErrors ? AuditOutcome.Failed : AuditOutcome.Succeeded,
                    "engineering-workspace",
                    "bulk",
                    new Dictionary<string, string>
                    {
                        ["entityKind"] = built.EntityKind!,
                        ["affectedCount"] = built.AffectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["expectedChangeVersion"] = expectedChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["resultingChangeVersion"] = resultingChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["updated"] = result.Updated.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                var response = new EngineeringBulkApplyResult(
                    resultingChangeVersion,
                    built.EntityKind!,
                    built.AffectedCount,
                    result);
                return hasErrors ? Results.BadRequest(response) : Results.Ok(response);
            }
            catch (EngineeringWorkspaceVersionConflictException conflict)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringBulkApply,
                    AuditOutcome.Failed,
                    "engineering-workspace",
                    "bulk",
                    new Dictionary<string, string>
                    {
                        ["reason"] = "workspace-version-conflict",
                        ["expectedChangeVersion"] = conflict.ExpectedChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["currentChangeVersion"] = conflict.CurrentChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });
                return Results.Conflict(new
                {
                    error = "Engineering Workspace changed after Bulk preview. Reload and preview the bulk change again.",
                    expectedChangeVersion = conflict.ExpectedChangeVersion,
                    currentChangeVersion = conflict.CurrentChangeVersion
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringBulkApply,
                    AuditOutcome.Failed,
                    "engineering-workspace",
                    "bulk",
                    new Dictionary<string, string>
                    {
                        ["reason"] = "unexpected-error",
                        ["errorType"] = ex.GetType().Name
                    });
                throw;
            }
        });
    }

    private static BulkCandidate BuildCandidate(EngineeringBulkRequest request, EngineeringPackage source)
    {
        if (string.IsNullOrWhiteSpace(request.EntityKind))
            return BulkCandidate.Invalid("EntityKind is required.");
        if (request.EntityIds is null || request.EntityIds.Count == 0)
            return BulkCandidate.Invalid("At least one entity ID must be selected.");

        var ids = request.EntityIds.ToHashSet();
        if (ids.Count != request.EntityIds.Count)
            return BulkCandidate.Invalid("Entity IDs must be unique.");

        switch (NormalizeKind(request.EntityKind))
        {
            case "tag":
                return BuildTagCandidate(request, source, ids);
            case "alarm":
                return BuildAlarmCandidate(request, source, ids);
            case "data-source":
                return BuildDataSourceCandidate(request, source, ids);
            default:
                return BulkCandidate.Invalid($"Unsupported bulk entity kind '{request.EntityKind}'.");
        }
    }

    private static BulkCandidate BuildTagCandidate(
        EngineeringBulkRequest request,
        EngineeringPackage source,
        HashSet<Guid> ids)
    {
        var changes = request.Tags;
        if (changes is null ||
            (changes.ReadOnly is null && changes.HistorianEnabled is null && changes.HistorianStrategy is null))
        {
            return BulkCandidate.Invalid("At least one supported TAG bulk change is required.");
        }

        if (changes.HistorianStrategy is not null && string.IsNullOrWhiteSpace(changes.HistorianStrategy))
            return BulkCandidate.Invalid("HistorianStrategy cannot be blank when supplied.");

        var selected = source.Tags.Where(tag => tag.Id.HasValue && ids.Contains(tag.Id.Value)).ToArray();
        var missing = MissingIds(ids, selected.Select(tag => tag.Id));
        if (missing.Count > 0)
            return BulkCandidate.Invalid($"TAG IDs not found: {string.Join(", ", missing)}.");

        var updated = selected.Select(tag =>
        {
            var historian = tag.Historian ?? new HistorianSettingsDto();
            if (changes.HistorianEnabled.HasValue || changes.HistorianStrategy is not null)
            {
                historian = historian with
                {
                    Enabled = changes.HistorianEnabled ?? historian.Enabled,
                    Strategy = changes.HistorianStrategy ?? historian.Strategy
                };
            }

            return tag with
            {
                ReadOnly = changes.ReadOnly ?? tag.ReadOnly,
                Historian = changes.HistorianEnabled.HasValue || changes.HistorianStrategy is not null
                    ? historian
                    : tag.Historian
            };
        }).ToArray();

        return BulkCandidate.Valid("tag", updated.Length, PartialPackage(source, tags: updated));
    }

    private static BulkCandidate BuildAlarmCandidate(
        EngineeringBulkRequest request,
        EngineeringPackage source,
        HashSet<Guid> ids)
    {
        var changes = request.Alarms;
        if (changes is null ||
            (changes.Enabled is null && changes.Priority is null &&
             changes.RequiresAcknowledgement is null && changes.ShelvingAllowed is null))
        {
            return BulkCandidate.Invalid("At least one supported Alarm bulk change is required.");
        }

        AlarmPriority? priority = null;
        if (changes.Priority is not null)
        {
            if (string.IsNullOrWhiteSpace(changes.Priority) ||
                !Enum.TryParse<AlarmPriority>(changes.Priority, true, out var parsedPriority))
            {
                return BulkCandidate.Invalid($"Unsupported Alarm priority '{changes.Priority}'.");
            }
            priority = parsedPriority;
        }

        var alarms = source.Alarms ?? Array.Empty<AlarmEngineeringDto>();
        var selected = alarms.Where(alarm => alarm.Id.HasValue && ids.Contains(alarm.Id.Value)).ToArray();
        var missing = MissingIds(ids, selected.Select(alarm => alarm.Id));
        if (missing.Count > 0)
            return BulkCandidate.Invalid($"Alarm IDs not found: {string.Join(", ", missing)}.");

        var updated = selected.Select(alarm => alarm with
        {
            Enabled = changes.Enabled ?? alarm.Enabled,
            Priority = priority ?? alarm.Priority,
            RequiresAcknowledgement = changes.RequiresAcknowledgement ?? alarm.RequiresAcknowledgement,
            ShelvingAllowed = changes.ShelvingAllowed ?? alarm.ShelvingAllowed
        }).ToArray();

        return BulkCandidate.Valid("alarm", updated.Length, PartialPackage(source, alarms: updated));
    }

    private static BulkCandidate BuildDataSourceCandidate(
        EngineeringBulkRequest request,
        EngineeringPackage source,
        HashSet<Guid> ids)
    {
        var changes = request.DataSources;
        if (changes?.Enabled is null)
            return BulkCandidate.Invalid("Data Source bulk editing currently supports only the Enabled property.");

        var dataSources = source.DataSources ?? Array.Empty<DataSourceEngineeringDto>();
        var selected = dataSources.Where(dataSource => dataSource.Id.HasValue && ids.Contains(dataSource.Id.Value)).ToArray();
        var missing = MissingIds(ids, selected.Select(dataSource => dataSource.Id));
        if (missing.Count > 0)
            return BulkCandidate.Invalid($"Data Source IDs not found: {string.Join(", ", missing)}.");

        var updated = selected.Select(dataSource => dataSource with
        {
            Enabled = changes.Enabled.Value
        }).ToArray();

        return BulkCandidate.Valid(
            "data-source",
            updated.Length,
            PartialPackage(source, dataSources: updated));
    }

    private static IReadOnlyCollection<Guid> MissingIds(
        HashSet<Guid> requested,
        IEnumerable<Guid?> resolved)
    {
        var found = resolved.Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        return requested.Where(id => !found.Contains(id)).OrderBy(id => id).ToArray();
    }

    private static EngineeringPackage PartialPackage(
        EngineeringPackage source,
        IReadOnlyCollection<TagEngineeringDto>? tags = null,
        IReadOnlyCollection<AlarmEngineeringDto>? alarms = null,
        IReadOnlyCollection<DataSourceEngineeringDto>? dataSources = null) =>
        new(
            source.Schema,
            source.SchemaVersion,
            DateTimeOffset.UtcNow,
            tags ?? Array.Empty<TagEngineeringDto>(),
            alarms ?? Array.Empty<AlarmEngineeringDto>(),
            dataSources ?? Array.Empty<DataSourceEngineeringDto>(),
            Array.Empty<EquipmentTemplateEngineeringDto>(),
            Array.Empty<EquipmentEngineeringDto>(),
            Array.Empty<DynamoEngineeringDto>(),
            Array.Empty<ScreenEngineeringDto>(),
            Array.Empty<PopupEngineeringDto>(),
            Array.Empty<SecurityRoleEngineeringDto>(),
            Array.Empty<CommandEngineeringDto>());

    private static string NormalizeKind(string value) => value.Trim().ToLowerInvariant() switch
    {
        "tag" or "tags" => "tag",
        "alarm" or "alarms" => "alarm",
        "datasource" or "data-source" or "data-sources" => "data-source",
        var other => other
    };

    private static bool TryReadExpectedVersion(HttpRequest request, out long expectedChangeVersion)
    {
        expectedChangeVersion = default;
        return request.Headers.TryGetValue(WorkspaceVersionHeader, out var header) &&
               header.Count == 1 &&
               long.TryParse(
                   header.ToString(),
                   System.Globalization.NumberStyles.None,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out expectedChangeVersion) &&
               expectedChangeVersion >= 0;
    }

    private sealed record BulkCandidate(
        string? EntityKind,
        int AffectedCount,
        EngineeringPackage? Package,
        string? Error)
    {
        public static BulkCandidate Invalid(string error) => new(null, 0, null, error);
        public static BulkCandidate Valid(string entityKind, int affectedCount, EngineeringPackage package) =>
            new(entityKind, affectedCount, package, null);
    }
}
