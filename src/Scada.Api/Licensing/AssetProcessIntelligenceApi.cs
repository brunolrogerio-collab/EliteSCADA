using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;
using Scada.Engineering.Persistence;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligenceScopeSelection(
    AssetProcessIntelligenceScopeKind Kind,
    Guid ScopeId);

public sealed record AssetProcessIntelligenceScopeConfigurationRequest(
    IReadOnlyCollection<AssetProcessIntelligenceScopeSelection>? Scopes);

public static class AssetProcessIntelligenceApi
{
    public static IEndpointRouteBuilder MapAssetProcessIntelligenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/engineering/asset-process-intelligence");

        group.MapGet("/scopes", (
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringAssetRegistry assets,
            IAssetProcessIntelligenceEngineeringRegistry intelligence,
            IProductLicenseService licensing,
            AssetProcessIntelligenceExecutionGate executionGate,
            ApiAuthorizationService security) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringView);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            var configuration = intelligence.Snapshot();
            var workspaceDescriptor = workspace.Describe();
            var configuredScopes = (configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
                .Select(scope => new
                {
                    kind = scope.Kind.ToString(),
                    scopeId = scope.ScopeId,
                    scope.Active,
                    executionArmed = workspaceDescriptor.ProjectKey is not null && workspaceDescriptor.BaseRevision.HasValue &&
                        executionGate.TryGetToken(
                            workspaceDescriptor.ProjectKey,
                            workspaceDescriptor.BaseRevision.Value,
                            scope.Kind,
                            scope.ScopeId,
                            out _)
                }).ToArray();
            var equipment = assets.SnapshotEquipment().Where(item => item.Id is { } id && id != Guid.Empty)
                .Select(item => new { kind = nameof(AssetProcessIntelligenceScopeKind.Equipment), scopeId = item.Id, item.Path, item.Name });
            var locations = assets.SnapshotLocations().Where(item => item.Id is { } id && id != Guid.Empty)
                .Select(item => new { kind = nameof(AssetProcessIntelligenceScopeKind.Location), scopeId = item.Id, path = (string?)null, item.Name });
            return Results.Ok(new
            {
                configuration = new { configuration.Version, scopes = configuredScopes },
                resources = equipment.Cast<object>().Concat(locations).ToArray(),
                licensed = licensing.CurrentVerification.HasAssetProcessIntelligence,
                execution = new
                {
                    stage = "Stage1",
                    workerRegistered = false,
                    queueDepth = 0,
                    activePermitCount = executionGate.ActivePermitCount,
                    projectKey = workspaceDescriptor.ProjectKey,
                    activeRevision = workspaceDescriptor.BaseRevision,
                    workspaceDirty = workspaceDescriptor.IsDirty
                }
            });
        });

        group.MapPut("/scopes", async (
            AssetProcessIntelligenceScopeConfigurationRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringAssetRegistry assets,
            IAssetProcessIntelligenceEngineeringRegistry intelligence,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context, authorization, AuditActions.AssetProcessIntelligenceConfigure,
                    "asset-process-intelligence", "scope-configuration");
                return failure;
            }
            if (request.Scopes is null)
                return Results.BadRequest(new { error = "INTELLIGENCE_SCOPE_LIST_REQUIRED" });
            if (request.Scopes.Any(scope => scope is null))
                return Results.BadRequest(new { error = "INTELLIGENCE_SCOPE_IDENTITY_INVALID" });
            if (workspace.Describe().ProjectKey is null)
                return Results.Conflict(new { error = "ENGINEERING_PROJECT_NOT_CHECKED_OUT" });

            await using var mutation = await workspace.AcquireMutationAsync(cancellationToken: cancellationToken);
            if (workspace.Describe().ProjectKey is null)
                return Results.Conflict(new { error = "ENGINEERING_PROJECT_NOT_CHECKED_OUT" });
            var current = intelligence.Snapshot();
            var activeByKey = (current.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
                .Where(scope => scope.Active)
                .ToDictionary(scope => (scope.Kind, scope.ScopeId));
            var requestedKeys = request.Scopes.Select(scope => (scope.Kind, scope.ScopeId)).ToHashSet();
            if (activeByKey.Keys.Any(key => !requestedKeys.Contains(key)))
                return Results.Conflict(new { error = "INTELLIGENCE_ACTIVE_SCOPE_MUST_BE_PAUSED" });

            var configuration = new AssetProcessIntelligenceEngineeringDto(
                Scopes: request.Scopes.Select(scope => new AssetProcessIntelligenceScopeEngineeringDto(
                    scope.Kind,
                    scope.ScopeId,
                    activeByKey.ContainsKey((scope.Kind, scope.ScopeId)))).ToArray());
            var issues = AssetProcessIntelligenceEngineeringValidator.Validate(
                configuration,
                assets.SnapshotEquipment(),
                assets.SnapshotLocations());
            if (issues.Count != 0)
                return Results.BadRequest(new { error = "INTELLIGENCE_SCOPE_CONFIGURATION_INVALID", issues });

            intelligence.Replace(configuration);
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.AssetProcessIntelligenceConfigure,
                AuditOutcome.Succeeded,
                "asset-process-intelligence",
                workspace.Describe().ProjectKey ?? "current",
                new Dictionary<string, string>
                {
                    ["scopeCount"] = configuration.Scopes!.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["activeScopeCount"] = configuration.Scopes.Count(scope => scope.Active).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["changeVersion"] = workspace.CaptureChangeVersion().ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            return Results.Ok(new { configuration, workspace = workspace.Describe() });
        });

        group.MapPost("/scopes/{kind}/{scopeId:guid}/activate", (
            string kind,
            Guid scopeId,
            HttpContext context,
            EngineeringWorkspace workspace,
            IAssetProcessIntelligenceEngineeringRegistry intelligence,
            IProductLicenseService licensing,
            ScadaRuntimeFacade runtime,
            RuntimeHighAvailabilityService highAvailability,
            AssetProcessIntelligenceExecutionGate executionGate,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
            SetScopeActivationAsync(true, kind, scopeId, context, workspace, intelligence, licensing,
                runtime, highAvailability, executionGate, security, audit));

        group.MapPost("/scopes/{kind}/{scopeId:guid}/pause", (
            string kind,
            Guid scopeId,
            HttpContext context,
            EngineeringWorkspace workspace,
            IAssetProcessIntelligenceEngineeringRegistry intelligence,
            IProductLicenseService licensing,
            ScadaRuntimeFacade runtime,
            RuntimeHighAvailabilityService highAvailability,
            AssetProcessIntelligenceExecutionGate executionGate,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
            SetScopeActivationAsync(false, kind, scopeId, context, workspace, intelligence, licensing,
                runtime, highAvailability, executionGate, security, audit));

        return endpoints;
    }

    private static async Task<IResult> SetScopeActivationAsync(
        bool active,
        string kindText,
        Guid scopeId,
        HttpContext context,
        EngineeringWorkspace workspace,
        IAssetProcessIntelligenceEngineeringRegistry intelligence,
        IProductLicenseService licensing,
        ScadaRuntimeFacade runtime,
        RuntimeHighAvailabilityService highAvailability,
        AssetProcessIntelligenceExecutionGate executionGate,
        ApiAuthorizationService security,
        ApiAuditService audit)
    {
        if (!Enum.TryParse<AssetProcessIntelligenceScopeKind>(kindText, ignoreCase: true, out var kind) ||
            !Enum.IsDefined(kind) || scopeId == Guid.Empty)
            return Results.NotFound();

        var resourceKind = kind switch
        {
            AssetProcessIntelligenceScopeKind.Equipment => AuthorizationResourceKind.Equipment,
            AssetProcessIntelligenceScopeKind.Location => AuthorizationResourceKind.Location,
            _ => (AuthorizationResourceKind?)null
        };
        if (!resourceKind.HasValue) return Results.NotFound();

        var manager = security.CheckWorkspace(
            context,
            SecurityCapability.AssetProcessIntelligenceManage,
            new AuthorizationResource(ResourceKind: resourceKind, ResourceId: scopeId));
        var engineering = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
        if (!manager.Allowed || !engineering.Allowed)
        {
            var denial = !manager.Allowed ? manager : engineering;
            await audit.RecordAuthorizationDeniedAsync(
                context,
                denial,
                active ? AuditActions.AssetProcessIntelligenceActivate : AuditActions.AssetProcessIntelligencePause,
                "asset-process-intelligence-scope",
                "scope");
            return denial.FailureResult() ?? Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (active && !licensing.CurrentVerification.HasAssetProcessIntelligence)
            return Results.Json(new { error = "INTELLIGENCE_ENTITLEMENT_REQUIRED" }, statusCode: StatusCodes.Status403Forbidden);

        var scope = (intelligence.Snapshot().Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
            .FirstOrDefault(candidate => candidate.Kind == kind && candidate.ScopeId == scopeId);
        if (scope is null) return Results.NotFound();

        var descriptor = workspace.Describe();
        // A pause is fail-safe even when the project cannot currently publish a
        // canonical revision: cancel local pending work before the lifecycle checks.
        if (!active && !string.IsNullOrWhiteSpace(descriptor.ProjectKey))
            executionGate.Pause(descriptor.ProjectKey, kind, scopeId);

        var runtimeDescriptor = runtime.Describe();
        if (string.IsNullOrWhiteSpace(descriptor.ProjectKey) || !descriptor.BaseRevision.HasValue ||
            !string.Equals(descriptor.ProjectKey, runtimeDescriptor.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
            descriptor.BaseRevision != runtimeDescriptor.Revision || !highAvailability.CanOwnIndustrialEffects())
            return Results.Conflict(new { error = "INTELLIGENCE_REQUIRES_EFFECTIVE_ACTIVE_PROJECT_REVISION" });
        if (descriptor.IsDirty)
            return Results.Conflict(new { error = "INTELLIGENCE_WORKSPACE_HAS_UNSAVED_CHANGES" });

        var persistence = context.RequestServices.GetService<IEngineeringProjectPersistenceService>();
        var activation = context.RequestServices.GetService<IPublishedRuntimeActivationService>();
        if (persistence is null || activation is null)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        EngineeringProjectSnapshot? persistedActive;
        try
        {
            persistedActive = await persistence.LoadActiveAsync(descriptor.ProjectKey, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (persistedActive?.Revision != descriptor.BaseRevision)
            return Results.Conflict(new { error = "INTELLIGENCE_PERSISTED_ACTIVE_REVISION_MISMATCH" });

        if (scope.Active == active)
        {
            if (active)
                executionGate.Activate(descriptor.ProjectKey, descriptor.BaseRevision.Value, kind, scopeId);
            else
                executionGate.Pause(descriptor.ProjectKey, kind, scopeId);
            await RecordScopeTransitionAsync(context, active, manager.Principal,
                descriptor.ProjectKey, descriptor.BaseRevision.Value, kind, scopeId, audit, "idempotent-explicit-request");
            return Results.Ok(new
            {
                kind,
                scopeId,
                active,
                persistedRevision = descriptor.BaseRevision,
                executionPermit = active,
                analyticsWorkerRegistered = false
            });
        }

        await using var mutation = await workspace.AcquireMutationAsync(cancellationToken: context.RequestAborted);
        var latest = workspace.Describe();
        if (!string.Equals(latest.ProjectKey, descriptor.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
            latest.BaseRevision != descriptor.BaseRevision)
            return Results.Conflict(new { error = "INTELLIGENCE_ACTIVE_REVISION_CHANGED" });
        var beforeConfiguration = intelligence.Snapshot();
        var action = active ? AuditActions.AssetProcessIntelligenceActivate : AuditActions.AssetProcessIntelligencePause;
        if (!intelligence.SetActive(kind, scopeId, active)) return Results.NotFound();
        // SetActive marks the Engineering workspace dirty. Capture after that mutation
        // so AcceptSave clears this exact change while preserving later concurrent edits.
        var saveVersion = workspace.CaptureChangeVersion();
        if (!active) executionGate.Pause(descriptor.ProjectKey, kind, scopeId);

        try
        {
            var snapshot = await persistence.SaveCurrentDerivedAsync(
                descriptor.ProjectKey,
                descriptor.ProjectName ?? descriptor.ProjectKey,
                descriptor.BaseRevision,
                manager.Principal.SubjectId,
                context.RequestAborted);
            var publication = await persistence.PublishRevisionAsync(
                snapshot.ProjectKey,
                snapshot.Revision,
                manager.Principal.SubjectId,
                context.RequestAborted);
            if (publication is null || !publication.Published)
            {
                intelligence.Replace(beforeConfiguration);
                workspace.RestoreDescriptor(descriptor);
                return Results.Conflict(new { error = "INTELLIGENCE_SCOPE_REVISION_NOT_PUBLISHABLE", revision = snapshot.Revision, preview = publication?.Preview });
            }

            var outcome = await activation.ActivateRevisionAsync(
                snapshot.ProjectKey,
                snapshot.Revision,
                manager.Principal.SubjectId,
                context.RequestAborted);
            if (!outcome.Activated)
            {
                intelligence.Replace(beforeConfiguration);
                workspace.RestoreDescriptor(descriptor);
                return Results.Conflict(new { error = "INTELLIGENCE_SCOPE_REVISION_NOT_ACTIVE", revision = snapshot.Revision, runtime = outcome.Runtime });
            }

            workspace.AcceptSave(
                snapshot.ProjectKey,
                snapshot.ProjectName,
                snapshot.Revision,
                snapshot.SavedAtUtc,
                saveVersion);
            if (active)
                executionGate.Activate(snapshot.ProjectKey, snapshot.Revision, kind, scopeId);
            else
                executionGate.Pause(snapshot.ProjectKey, kind, scopeId);

            await RecordScopeTransitionAsync(context, active, manager.Principal,
                snapshot.ProjectKey, snapshot.Revision, kind, scopeId, audit, "saved-published-activated");
            return Results.Ok(new
            {
                kind,
                scopeId,
                active,
                persistedRevision = snapshot.Revision,
                runtimeActivated = true,
                executionPermit = active,
                analyticsWorkerRegistered = false
            });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            intelligence.Replace(beforeConfiguration);
            workspace.RestoreDescriptor(descriptor);
            throw;
        }
        catch
        {
            intelligence.Replace(beforeConfiguration);
            workspace.RestoreDescriptor(descriptor);
            await audit.RecordAsync(
                context,
                manager.Principal,
                action,
                AuditOutcome.Failed,
                "asset-process-intelligence-scope",
                $"{kind}:{scopeId:D}",
                new Dictionary<string, string>
                {
                    ["error"] = "scope-transition-failed",
                    ["projectKey"] = descriptor.ProjectKey,
                    ["activeRevision"] = descriptor.BaseRevision?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"
                });
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static ValueTask RecordScopeTransitionAsync(
        HttpContext context,
        bool active,
        SecurityPrincipal principal,
        string projectKey,
        long revision,
        AssetProcessIntelligenceScopeKind kind,
        Guid scopeId,
        ApiAuditService audit,
        string transition) =>
        audit.RecordAsync(
            context,
            principal,
            active ? AuditActions.AssetProcessIntelligenceActivate : AuditActions.AssetProcessIntelligencePause,
            AuditOutcome.Succeeded,
            "asset-process-intelligence-scope",
            $"{kind}:{scopeId:D}",
            new Dictionary<string, string>
            {
                ["projectKey"] = projectKey,
                ["revision"] = revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["scopeKind"] = kind.ToString(),
                ["scopeId"] = scopeId.ToString("D"),
                ["transition"] = transition,
                ["analyticsWorkerRegistered"] = "false"
            });
}
