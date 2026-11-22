using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed record SetAlarmSuppressionRequest(
    string ReasonCode,
    DateTimeOffset? ExpiresAtUtc = null);

public static class AlarmSuppressionEndpointExtensions
{
    public static WebApplication MapAlarmSuppressionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/alarms/suppressions", GetSnapshotAsync);
        app.MapPut("/api/alarms/suppressions/{targetKind}/{targetId:guid}", SetSuppressionAsync);
        app.MapDelete("/api/alarms/suppressions/{targetKind}/{targetId:guid}", ReleaseSuppressionAsync);
        return app;
    }

    private static async Task<IResult> GetSnapshotAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        AlarmSuppressionRuntimeProjection projection,
        ApiAuthorizationService security,
        CancellationToken cancellationToken)
    {
        if (!runtime.IsEngineeringActive)
            return Results.Ok(new { projectKey = (string?)null, engineeringRevision = (long?)null, suppressionRevision = 0L, alarms = Array.Empty<AlarmSuppressionAlarmProjection>(), locations = Array.Empty<AlarmSuppressionLocationProjection>() });

        var tagAccess = await security.GetReadableRuntimeTagsAsync(context, runtime, cancellationToken);
        var failure = tagAccess.FailureResult();
        if (failure is not null) return failure;

        try
        {
            var snapshot = await projection.BuildAsync(runtime, cancellationToken);
            var definitions = runtime.AlarmDefinitions().ToDictionary(item => item.Id);
            var visible = new List<AlarmSuppressionAlarmProjection>();
            foreach (var alarm in snapshot.Alarms)
            {
                if (!definitions.TryGetValue(alarm.AlarmId, out var definition) ||
                    !tagAccess.Tags.Any(tag => tag.Id == definition.TagId))
                    continue;
                if (await security.CanViewRuntimeResourceAsync(
                        tagAccess.Principal,
                        runtime,
                        new AuthorizationResource(Area: definition.Area),
                        cancellationToken))
                    visible.Add(alarm);
            }

            var visibleLocationIds = VisibleLocationAndAncestorIds(visible, snapshot.Locations);
            var locations = snapshot.Locations.Where(location => visibleLocationIds.Contains(location.Id)).ToArray();
            return Results.Ok(new
            {
                snapshot.ProjectKey,
                snapshot.EngineeringRevision,
                snapshot.SuppressionRevision,
                Alarms = visible,
                Locations = locations
            });
        }
        catch (AlarmSuppressionUnavailableException)
        {
            return Results.Problem("Durable alarm suppression storage is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Results.Problem("Alarm suppression state could not be read safely.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> SetSuppressionAsync(
        string targetKind,
        Guid targetId,
        SetAlarmSuppressionRequest request,
        HttpContext context,
        ScadaRuntimeFacade runtime,
        AlarmSuppressionRuntimeProjection projection,
        IAlarmSuppressionStore store,
        ApiAuthorizationService security,
        ApiAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!TryParseTargetKind(targetKind, out var kind) || targetId == Guid.Empty)
            return Results.BadRequest(new { error = "Alarm suppression target is invalid." });
        if (request is null || !AlarmSuppressionReasonCodes.IsValid(request.ReasonCode))
            return Results.BadRequest(new { error = "Choose a supported suppression reason." });

        var now = DateTimeOffset.UtcNow;
        var expiresAt = request.ExpiresAtUtc?.ToUniversalTime();
        if (expiresAt.HasValue && expiresAt.Value <= now)
            return Results.BadRequest(new { error = "Suppression expiry must be in the future." });

        AlarmSuppressionRuntimeSnapshot snapshot;
        try
        {
            snapshot = await projection.BuildAsync(runtime, cancellationToken);
        }
        catch (AlarmSuppressionUnavailableException)
        {
            return Results.Problem("Durable alarm suppression storage is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Results.Conflict(new { error = "The active Runtime targets could not be resolved safely." });
        }

        var affected = ResolveAffectedAlarms(kind, targetId, snapshot);
        if (affected is null) return Results.NotFound();
        if (affected.Count == 0)
            return Results.Conflict(new { error = "The selected Location has no alarms linked to its operational hierarchy." });

        var definitions = runtime.AlarmDefinitions().ToDictionary(item => item.Id);
        var affectedDefinitions = new List<AlarmDefinition>(affected.Count);
        foreach (var item in affected)
        {
            if (!definitions.TryGetValue(item.AlarmId, out var definition))
                return Results.Conflict(new { error = "The active alarm definitions changed; refresh and try again." });
            if (!definition.ShelvingAllowed)
                return Results.Conflict(new { error = "At least one affected alarm does not allow operator suppression." });
            affectedDefinitions.Add(definition);
        }

        var authorization = await AuthorizeAllAsync(
            context,
            runtime,
            security,
            affected,
            definitions,
            cancellationToken);
        if (authorization.Failure is not null)
        {
            await audit.RecordAuthorizationDeniedAsync(
                context,
                authorization.Failure,
                AuditActions.AlarmSuppression,
                TargetKindName(kind),
                targetId.ToString("D"),
                AuditDetails("suppress", snapshot, affected.Count, request.ReasonCode, expiresAt,
                    result: "authorization-denied"));
            return authorization.Failure.FailureResult()!;
        }

        if (!SameRuntime(snapshot, runtime.Describe()))
            return Results.Conflict(new { error = "The active Runtime changed before suppression was saved; refresh and try again." });

        var actor = authorization.Principal?.DisplayName ?? authorization.Principal?.SubjectId ?? "unknown";
        try
        {
            var result = await store.SetAsync(
                new AlarmSuppressionRecord(
                    snapshot.ProjectKey,
                    kind,
                    targetId,
                    request.ReasonCode,
                    actor,
                    now,
                    expiresAt),
                cancellationToken);
            await audit.RecordAsync(
                context,
                authorization.Principal!,
                AuditActions.AlarmSuppression,
                result.Changed ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                TargetKindName(kind),
                targetId.ToString("D"),
                AuditDetails("suppress", snapshot, affected.Count, request.ReasonCode, expiresAt,
                    result.Previous, result.Current, result.Changed ? "applied" : "unchanged"));
            return Results.Ok(new { changed = result.Changed, affectedAlarmCount = affected.Count });
        }
        catch (AlarmSuppressionUnavailableException)
        {
            return Results.Problem("Durable alarm suppression storage is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> ReleaseSuppressionAsync(
        string targetKind,
        Guid targetId,
        HttpContext context,
        ScadaRuntimeFacade runtime,
        AlarmSuppressionRuntimeProjection projection,
        IAlarmSuppressionStore store,
        ApiAuthorizationService security,
        ApiAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!TryParseTargetKind(targetKind, out var kind) || targetId == Guid.Empty)
            return Results.BadRequest(new { error = "Alarm suppression target is invalid." });

        AlarmSuppressionRuntimeSnapshot snapshot;
        try
        {
            snapshot = await projection.BuildAsync(runtime, cancellationToken);
        }
        catch (AlarmSuppressionUnavailableException)
        {
            return Results.Problem("Durable alarm suppression storage is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Results.Conflict(new { error = "The active Runtime targets could not be resolved safely." });
        }

        var affected = ResolveAffectedAlarms(kind, targetId, snapshot) ?? Array.Empty<AlarmSuppressionAlarmProjection>();
        var definitions = runtime.AlarmDefinitions().ToDictionary(item => item.Id);
        var affectedDefinitions = affected.Where(item => definitions.ContainsKey(item.AlarmId))
            .Select(item => definitions[item.AlarmId]).ToArray();
        var authorization = await AuthorizeAllAsync(
            context,
            runtime,
            security,
            affected,
            definitions,
            cancellationToken,
            requireGlobalWhenEmpty: true);
        if (authorization.Failure is not null)
        {
            await audit.RecordAuthorizationDeniedAsync(
                context,
                authorization.Failure,
                AuditActions.AlarmSuppression,
                TargetKindName(kind),
                targetId.ToString("D"),
                AuditDetails("release", snapshot, affectedDefinitions.Length, null, null,
                    result: "authorization-denied"));
            return authorization.Failure.FailureResult()!;
        }

        if (!SameRuntime(snapshot, runtime.Describe()))
            return Results.Conflict(new { error = "The active Runtime changed before suppression was released; refresh and try again." });

        try
        {
            var result = await store.ReleaseAsync(snapshot.ProjectKey, kind, targetId, cancellationToken);
            await audit.RecordAsync(
                context,
                authorization.Principal!,
                AuditActions.AlarmSuppression,
                result.Changed ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                TargetKindName(kind),
                targetId.ToString("D"),
                AuditDetails("release", snapshot, affectedDefinitions.Length, null, null,
                    result.Previous, null, result.Changed ? "released" : "not-found"));
            return result.Changed
                ? Results.Ok(new { changed = true, affectedAlarmCount = affectedDefinitions.Length })
                : Results.NotFound();
        }
        catch (AlarmSuppressionUnavailableException)
        {
            return Results.Problem("Durable alarm suppression storage is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static IReadOnlyCollection<AlarmSuppressionAlarmProjection>? ResolveAffectedAlarms(
        AlarmSuppressionTargetKind kind,
        Guid targetId,
        AlarmSuppressionRuntimeSnapshot snapshot)
    {
        if (kind == AlarmSuppressionTargetKind.Alarm)
            return snapshot.Alarms.Where(item => item.AlarmId == targetId).ToArray();

        if (!snapshot.Locations.Any(location => location.Id == targetId)) return null;
        var locations = snapshot.Locations.ToDictionary(item => item.Id);
        return snapshot.Alarms.Where(alarm =>
        {
            if (!alarm.LocationId.HasValue) return false;
            var current = alarm.LocationId;
            var visited = new HashSet<Guid>();
            while (current.HasValue && visited.Add(current.Value))
            {
                if (current.Value == targetId) return true;
                current = locations.TryGetValue(current.Value, out var location) ? location.ParentLocationId : null;
            }
            return false;
        }).ToArray();
    }

    private static async Task<AuthorizationResult> AuthorizeAllAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        ApiAuthorizationService security,
        IReadOnlyCollection<AlarmSuppressionAlarmProjection> alarmTargets,
        IReadOnlyDictionary<Guid, AlarmDefinition> definitions,
        CancellationToken cancellationToken,
        bool requireGlobalWhenEmpty = false)
    {
        if (alarmTargets.Count == 0)
        {
            if (!requireGlobalWhenEmpty)
                return new(null, new ApiAuthorizationCheck(security.GetPrincipal(context),
                    AuthorizationDecision.Denied(SecurityCapability.AlarmShelve, "No alarm target could be authorized.")));
            var global = await security.CheckRuntimeAsync(context, runtime, SecurityCapability.AlarmShelve, null, cancellationToken);
            return global.Allowed ? new(global.Principal, null) : new(null, global);
        }

        SecurityPrincipal? principal = null;
        foreach (var target in alarmTargets)
        {
            if (!definitions.TryGetValue(target.AlarmId, out var definition))
                return new(null, new ApiAuthorizationCheck(security.GetPrincipal(context),
                    AuthorizationDecision.Denied(SecurityCapability.AlarmShelve, "An affected alarm definition could not be resolved.")));

            // Authority owns the permission graph. Resolve the stable linked resource through
            // that graph; the operational Location tree is used only to calculate impact.
            // Keep Area on the resource for existing policy checks, but never infer Authority
            // ancestry from the operational parent chain or a display path.
            var resource = target.EquipmentId is { } equipmentId
                ? new AuthorizationResource(
                    Area: definition.Area,
                    ResourceKind: AuthorizationResourceKind.Equipment,
                    ResourceId: equipmentId)
                : target.LocationId is { } locationId
                    ? new AuthorizationResource(
                        Area: definition.Area,
                        ResourceKind: AuthorizationResourceKind.Location,
                        ResourceId: locationId)
                    : new AuthorizationResource(Area: definition.Area);
            var check = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.AlarmShelve,
                resource,
                cancellationToken);
            if (!check.Allowed) return new(null, check);
            principal ??= check.Principal;
        }

        return new(principal, null);
    }

    private static HashSet<Guid> VisibleLocationAndAncestorIds(
        IReadOnlyCollection<AlarmSuppressionAlarmProjection> alarms,
        IReadOnlyCollection<AlarmSuppressionLocationProjection> locations)
    {
        var byId = locations.ToDictionary(item => item.Id);
        var visible = new HashSet<Guid>();
        foreach (var alarm in alarms)
        {
            var current = alarm.LocationId;
            while (current.HasValue && visible.Add(current.Value) && byId.TryGetValue(current.Value, out var location))
                current = location.ParentLocationId;
        }
        return visible;
    }

    private static Dictionary<string, string> AuditDetails(
        string operation,
        AlarmSuppressionRuntimeSnapshot snapshot,
        int affectedCount,
        string? reasonCode,
        DateTimeOffset? expiresAtUtc,
        AlarmSuppressionRecord? previous = null,
        AlarmSuppressionRecord? current = null,
        string result = "not-applied") => new()
    {
        ["operation"] = operation,
        ["result"] = result,
        ["projectKey"] = snapshot.ProjectKey,
        ["engineeringRevision"] = snapshot.EngineeringRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["suppressionRevision"] = snapshot.SuppressionRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["affectedAlarmCount"] = affectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["reasonCode"] = reasonCode ?? string.Empty,
        ["expiresAtUtc"] = FormatAuditTime(expiresAtUtc),
        ["beforeActive"] = (previous is not null).ToString().ToLowerInvariant(),
        ["beforeVersion"] = previous?.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        ["beforeReasonCode"] = previous?.ReasonCode ?? string.Empty,
        ["beforeExpiresAtUtc"] = FormatAuditTime(previous?.ExpiresAtUtc),
        ["afterActive"] = (current is not null).ToString().ToLowerInvariant(),
        ["afterVersion"] = current?.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        ["afterReasonCode"] = current?.ReasonCode ?? string.Empty,
        ["afterExpiresAtUtc"] = FormatAuditTime(current?.ExpiresAtUtc)
    };

    private static string FormatAuditTime(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("O") ?? "none";

    private static bool SameRuntime(AlarmSuppressionRuntimeSnapshot snapshot, ScadaRuntimeDescriptor current) =>
        snapshot.ProjectKey.Equals(current.ProjectKey, StringComparison.Ordinal) &&
        snapshot.EngineeringRevision == current.Revision;

    private static string TargetKindName(AlarmSuppressionTargetKind kind) => kind switch
    {
        AlarmSuppressionTargetKind.Alarm => "alarm",
        AlarmSuppressionTargetKind.Location => "location",
        _ => "unknown"
    };

    private static bool TryParseTargetKind(string value, out AlarmSuppressionTargetKind kind)
    {
        kind = value.ToLowerInvariant() switch
        {
            "alarm" => AlarmSuppressionTargetKind.Alarm,
            "location" => AlarmSuppressionTargetKind.Location,
            _ => 0
        };
        return Enum.IsDefined(kind);
    }

    private sealed record AuthorizationResult(
        SecurityPrincipal? Principal,
        ApiAuthorizationCheck? Failure);
}
