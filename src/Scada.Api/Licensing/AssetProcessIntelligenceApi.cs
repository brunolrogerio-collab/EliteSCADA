using System.Text.Json;
using System.Text.Json.Nodes;
using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Security.Audit;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligenceScopeSelection(
    AssetProcessIntelligenceScopeKind Kind,
    Guid ScopeId);

public sealed record AssetProcessIntelligenceScopeConfigurationRequest(
    IReadOnlyCollection<AssetProcessIntelligenceScopeSelection>? Scopes);

public sealed record AssetProcessIntelligenceConfigurationRequest(
    IReadOnlyCollection<AssetProcessIntelligenceProfileEngineeringDto>? Profiles,
    IReadOnlyCollection<AssetProcessIntelligenceAssignmentEngineeringDto>? Assignments,
    IReadOnlyCollection<AssetProcessIntelligenceProcessStageEngineeringDto>? ProcessStages,
    int? SemanticContractVersion = null,
    IReadOnlyCollection<AssetProcessIntelligenceOperatingContextProfileSaveRequest>? OperatingContextProfiles = null,
    IReadOnlyCollection<AssetProcessIntelligenceOperatingContextProfileExpectedVersion>? ExpectedOperatingContextProfileVersions = null);

public sealed record AssetProcessIntelligenceOperatingContextProfileSaveRequest(
    Guid Id,
    string Key,
    string DisplayLabel,
    Guid AssignmentId,
    IReadOnlyCollection<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto>? Predicates);

public sealed record AssetProcessIntelligenceOperatingContextProfileExpectedVersion(Guid Id, int Version);

public static class AssetProcessIntelligenceApi
{
    public static IEndpointRouteBuilder MapAssetProcessIntelligenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/engineering/asset-process-intelligence");

        group.MapGet("/semantic-contracts/v2/catalog", (
            HttpContext context,
            ApiAuthorizationService security) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringView);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            return Results.Ok(new
            {
                supportedSemanticContractVersions = new[] { 1, 2 },
                semanticCatalogs = new[]
                {
                    new VersionedAssetProcessIntelligenceSemanticCatalog(
                        AssetProcessIntelligenceSemanticCatalog.CurrentCatalogVersion,
                        AssetProcessIntelligenceSemanticCatalog.All),
                    new VersionedAssetProcessIntelligenceSemanticCatalog(
                        AssetProcessIntelligenceSemanticCatalogV2.CurrentCatalogVersion,
                        AssetProcessIntelligenceSemanticCatalogV2.All)
                },
                controlledEngineeringUnitCatalogs = new[]
                {
                    new VersionedControlledEngineeringUnitCatalog(
                        ControlledEngineeringUnitCatalog.CurrentCatalogVersion,
                        ControlledEngineeringUnitCatalog.All),
                    new VersionedControlledEngineeringUnitCatalog(
                        ControlledEngineeringUnitCatalogV2.CurrentCatalogVersion,
                        ControlledEngineeringUnitCatalogV2.All)
                }
            });
        });

        group.MapGet("/scopes", async (
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringAssetRegistry assets,
            IAssetProcessIntelligenceEngineeringRegistry intelligence,
            IProductLicenseService licensing,
            AssetProcessIntelligenceExecutionGate executionGate,
            ApiAuthorizationService security,
            IEngineeringExchangeService exchange) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringView);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            var configuration = intelligence.Snapshot();
            var workspaceDescriptor = workspace.Describe();
            var authorityHa = context.RequestServices.GetService<AuthorityPolicyHaCoordinator>();
            var fenceAllowed = true;
            var fenceReason = "standalone";
            var policyVersion = 0L;
            var haEpoch = 0L;
            if (authorityHa is not null)
            {
                var fence = await authorityHa.CheckAsync(context.RequestAborted);
                fenceAllowed = fence.Allowed;
                fenceReason = fence.ReasonCode;
                policyVersion = fence.PolicyVersion;
                haEpoch = fence.HaEpoch;
            }
            var configuredScopes = (configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
                .Select(scope =>
                {
                    var permit = workspaceDescriptor.ProjectKey is null
                        ? null
                        : executionGate.Describe(workspaceDescriptor.ProjectKey, scope.Kind, scope.ScopeId);
                    return new
                    {
                        kind = scope.Kind.ToString(),
                        scopeId = scope.ScopeId,
                        scope.Active,
                        canManage = security.CheckWorkspace(context, SecurityCapability.AssetProcessIntelligenceManage,
                            new AuthorizationResource(ResourceKind: scope.Kind == AssetProcessIntelligenceScopeKind.Equipment
                                ? AuthorizationResourceKind.Equipment : AuthorizationResourceKind.Location, ResourceId: scope.ScopeId)).Allowed,
                        executionArmed = fenceAllowed && workspaceDescriptor.ProjectKey is not null &&
                            workspaceDescriptor.BaseRevision.HasValue &&
                            executionGate.TryGetToken(
                                workspaceDescriptor.ProjectKey,
                                workspaceDescriptor.BaseRevision.Value,
                                scope.Kind,
                                scope.ScopeId,
                                out _,
                                policyVersion,
                                haEpoch),
                        permitRevision = permit?.Revision,
                        permitAuthorityPolicyVersion = permit?.AuthorityPolicyVersion,
                        permitHaEpoch = permit?.HaEpoch
                    };
                }).ToArray();
            var equipment = assets.SnapshotEquipment().Where(item => item.Id is { } id && id != Guid.Empty)
                .Select(item => new { kind = nameof(AssetProcessIntelligenceScopeKind.Equipment), scopeId = item.Id, item.Path, item.Name, canManage = security.CheckWorkspace(context, SecurityCapability.AssetProcessIntelligenceManage, new AuthorizationResource(ResourceKind: AuthorizationResourceKind.Equipment, ResourceId: item.Id)).Allowed });
            var locations = assets.SnapshotLocations().Where(item => item.Id is { } id && id != Guid.Empty)
                .Select(item => new { kind = nameof(AssetProcessIntelligenceScopeKind.Location), scopeId = item.Id, path = (string?)null, item.Name, canManage = security.CheckWorkspace(context, SecurityCapability.AssetProcessIntelligenceManage, new AuthorizationResource(ResourceKind: AuthorizationResourceKind.Location, ResourceId: item.Id)).Allowed });
            var tags = exchange.ExportPackage().Tags.Where(item => item.Id is { } id && id != Guid.Empty)
                .Select(item => new
                {
                    id = item.Id!.Value,
                    item.Name,
                    item.Path,
                    dataType = item.DataType.ToString(),
                    item.EngineeringUnit,
                    item.ControlledEngineeringUnit
                })
                .ToArray();
            return Results.Ok(new
            {
                configuration = new
                {
                    configuration.Version,
                    scopes = configuredScopes,
                    profiles = configuration.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>(),
                    assignments = configuration.Assignments ?? Array.Empty<AssetProcessIntelligenceAssignmentEngineeringDto>(),
                    processStages = configuration.ProcessStages ?? Array.Empty<AssetProcessIntelligenceProcessStageEngineeringDto>(),
                    operatingContextProfiles = configuration.OperatingContextProfiles ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>()
                },
                resources = equipment.Cast<object>().Concat(locations).ToArray(),
                tags,
                licensed = licensing.CurrentVerification.HasAssetProcessIntelligence,
                startupRecoveryPending = executionGate.StartupRecoveryPending,
                authorityFence = new
                {
                    allowed = fenceAllowed,
                    reasonCode = fenceReason,
                    policyVersion,
                    haEpoch
                },
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
                Version: current.Version,
                Scopes: request.Scopes.Select(scope => new AssetProcessIntelligenceScopeEngineeringDto(
                    scope.Kind,
                    scope.ScopeId,
                    activeByKey.ContainsKey((scope.Kind, scope.ScopeId)))).ToArray(),
                Profiles: current.Profiles,
                Assignments: current.Assignments,
                ProcessStages: current.ProcessStages,
                OperatingContextProfiles: current.OperatingContextProfiles);
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

        group.MapPut("/configuration", async (
            AssetProcessIntelligenceConfigurationRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IAssetProcessIntelligenceEngineeringRegistry intelligence,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context, authorization, AuditActions.AssetProcessIntelligenceConfigurationConfigure,
                    "asset-process-intelligence", "configuration");
                return failure;
            }
            if (request.Profiles is null || request.Assignments is null || request.ProcessStages is null)
                return Results.BadRequest(new { error = "INTELLIGENCE_CONFIGURATION_LISTS_REQUIRED" });
            if (request.Profiles.Any(item => item is null) || request.Assignments.Any(item => item is null) || request.ProcessStages.Any(item => item is null))
                return Results.BadRequest(new { error = "INTELLIGENCE_CONFIGURATION_ENTRY_INVALID" });
            if (request.SemanticContractVersion is not null and not (1 or 2))
                return Results.BadRequest(new { error = "INTELLIGENCE_SEMANTIC_CONTRACT_VERSION_UNSUPPORTED" });
            var incomingBindings = request.Profiles
                .Where(profile => profile is not null)
                .SelectMany(profile => profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                .Where(signal => signal?.SemanticBinding is not null)
                .Select(signal => signal.SemanticBinding!)
                .ToArray();
            if (incomingBindings.Length != 0 && request.SemanticContractVersion is null)
                return Results.BadRequest(new { error = "INTELLIGENCE_SEMANTIC_CONTRACT_VERSION_REQUIRED" });
            if (workspace.Describe().ProjectKey is null)
                return Results.Conflict(new { error = "ENGINEERING_PROJECT_NOT_CHECKED_OUT" });

            await using var mutation = await workspace.AcquireMutationAsync(cancellationToken: cancellationToken);
            var descriptor = workspace.Describe();
            if (descriptor.ProjectKey is null)
                return Results.Conflict(new { error = "ENGINEERING_PROJECT_NOT_CHECKED_OUT" });

            var current = intelligence.Snapshot();
            if (request.SemanticContractVersion is null && OmitsPersistedSemanticBindings(current, request.Profiles))
                return Results.Conflict(new { error = "INTELLIGENCE_SEMANTIC_ANNOTATION_CONFLICT" });
            if (request.SemanticContractVersion.GetValueOrDefault() < 2 &&
                (OmitsPersistedVersion2SemanticBindings(current, request.Profiles) || AddsOrChangesVersion2SemanticBindings(current, request.Profiles)))
                return Results.Conflict(new { error = "INTELLIGENCE_SEMANTIC_ANNOTATION_CONFLICT" });

            IReadOnlyCollection<AssetProcessIntelligenceOperatingContextProfileEngineeringDto> contextProfiles;
            var contextAuthoringRequested = request.OperatingContextProfiles is not null;
            if (!contextAuthoringRequested)
            {
                // Older clients cannot erase a configuration they do not understand.
                contextProfiles = current.OperatingContextProfiles ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>();
            }
            else
            {
                var incomingContextProfiles = request.OperatingContextProfiles!;
                var expectedVersions = request.ExpectedOperatingContextProfileVersions ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileExpectedVersion>();
                if (expectedVersions.Any(item => item is null || item.Id == Guid.Empty || item.Version < 1) ||
                    expectedVersions.Select(item => item.Id).Distinct().Count() != expectedVersions.Count)
                    return Results.BadRequest(new { error = "INTELLIGENCE_OPERATING_CONTEXT_EXPECTED_VERSIONS_INVALID" });
                var expectedById = expectedVersions.ToDictionary(item => item.Id, item => item.Version);
                var persistedContexts = current.OperatingContextProfiles ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>();
                if (persistedContexts.Any(profile => !expectedById.TryGetValue(profile.Id, out var expected) || expected != profile.Version) ||
                    expectedById.Count != persistedContexts.Count)
                    return Results.Conflict(new { error = "INTELLIGENCE_OPERATING_CONTEXT_PROFILE_VERSION_CONFLICT" });
                if (incomingContextProfiles.Any(item => item is null || item.Id == Guid.Empty || item.Predicates is null) ||
                    incomingContextProfiles.Select(item => item.Id).Distinct().Count() != incomingContextProfiles.Count)
                    return Results.BadRequest(new { error = "INTELLIGENCE_OPERATING_CONTEXT_PROFILE_ENTRY_INVALID" });

                var persistedById = persistedContexts.ToDictionary(item => item.Id);
                var versioned = new List<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>();
                foreach (var incoming in incomingContextProfiles)
                {
                    if (persistedById.TryGetValue(incoming.Id, out var persisted))
                    {
                        var expected = expectedById[incoming.Id];
                        if (expected != persisted.Version)
                            return Results.Conflict(new { error = "INTELLIGENCE_OPERATING_CONTEXT_PROFILE_VERSION_CONFLICT" });
                        var candidate = new AssetProcessIntelligenceOperatingContextProfileEngineeringDto(
                            incoming.Id, incoming.Key, incoming.DisplayLabel, incoming.AssignmentId, persisted.Version, incoming.Predicates);
                        versioned.Add(ContextProfileSemanticallyEqual(persisted, candidate)
                            ? candidate
                            : candidate with { Version = checked(persisted.Version + 1) });
                    }
                    else
                    {
                        versioned.Add(new AssetProcessIntelligenceOperatingContextProfileEngineeringDto(
                            incoming.Id, incoming.Key, incoming.DisplayLabel, incoming.AssignmentId, 1, incoming.Predicates));
                    }
                }
                contextProfiles = versioned;
            }

            var shouldUseVersion3 = current.Version == 3 || contextAuthoringRequested || contextProfiles.Count != 0;
            var configuration = new AssetProcessIntelligenceEngineeringDto(
                Version: shouldUseVersion3 ? 3 : 2,
                Scopes: current.Scopes,
                Profiles: request.Profiles,
                Assignments: request.Assignments,
                ProcessStages: request.ProcessStages,
                OperatingContextProfiles: contextProfiles);
            var canonical = exchange.ExportPackage();
            var issues = AssetProcessIntelligenceEngineeringValidator.Validate(
                configuration,
                canonical.Equipment,
                canonical.Locations,
                canonical.Tags);
            if (issues.Count != 0)
                return Results.BadRequest(new { error = "INTELLIGENCE_CONFIGURATION_INVALID", issues });

            if ((configuration.OperatingContextProfiles?.Count ?? 0) > 0 || (current.OperatingContextProfiles?.Count ?? 0) > 0)
            {
                var assignmentById = (configuration.Assignments ?? Array.Empty<AssetProcessIntelligenceAssignmentEngineeringDto>())
                    .ToDictionary(item => item.Id);
                var priorAssignmentById = (current.Assignments ?? Array.Empty<AssetProcessIntelligenceAssignmentEngineeringDto>())
                    .ToDictionary(item => item.Id);
                var requiredEquipmentIds = (configuration.OperatingContextProfiles ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>())
                    .Where(profile => assignmentById.ContainsKey(profile.AssignmentId))
                    .Select(profile => assignmentById[profile.AssignmentId].EquipmentId)
                    .Concat((current.OperatingContextProfiles ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>())
                        .Where(profile => priorAssignmentById.ContainsKey(profile.AssignmentId))
                        .Select(profile => priorAssignmentById[profile.AssignmentId].EquipmentId))
                    .Distinct()
                    .ToArray();
                foreach (var equipmentId in requiredEquipmentIds)
                {
                    var manager = security.CheckWorkspace(
                        context,
                        SecurityCapability.AssetProcessIntelligenceManage,
                        new AuthorizationResource(ResourceKind: AuthorizationResourceKind.Equipment, ResourceId: equipmentId));
                    if (!manager.Allowed)
                    {
                        await audit.RecordAuthorizationDeniedAsync(
                            context, manager, AuditActions.AssetProcessIntelligenceConfigurationConfigure,
                            "asset-process-intelligence", "operating-context-profile");
                        return manager.FailureResult() ?? Results.StatusCode(StatusCodes.Status403Forbidden);
                    }
                }
            }

            intelligence.Replace(configuration);
            var saved = intelligence.Snapshot();
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.AssetProcessIntelligenceConfigurationConfigure,
                AuditOutcome.Succeeded,
                "asset-process-intelligence",
                descriptor.ProjectKey,
                new Dictionary<string, string>
                {
                    ["profileCount"] = (saved.Profiles?.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["assignmentCount"] = (saved.Assignments?.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["processStageCount"] = (saved.ProcessStages?.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["changeVersion"] = workspace.CaptureChangeVersion().ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            return Results.Ok(new { configuration = saved, workspace = workspace.Describe() });
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

    private static bool OmitsPersistedSemanticBindings(
        AssetProcessIntelligenceEngineeringDto current,
        IReadOnlyCollection<AssetProcessIntelligenceProfileEngineeringDto> replacementProfiles)
    {
        var persisted = (current.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(profile => profile is not null)
            .SelectMany(profile => (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                .Where(signal => signal?.SemanticBinding is not null)
                .Select(signal => (Key: (profile.Id, signal!.Id), Signal: signal)))
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.First().Signal);
        var replacement = SignalsByProfileAndId(replacementProfiles);
        return persisted.Any(item =>
            !replacement.TryGetValue(item.Key, out var candidate) ||
            candidate.TagId != item.Value.TagId ||
            candidate.SemanticBinding != item.Value.SemanticBinding);
    }

    private static bool ContextProfileSemanticallyEqual(
        AssetProcessIntelligenceOperatingContextProfileEngineeringDto left,
        AssetProcessIntelligenceOperatingContextProfileEngineeringDto right)
    {
        if (left.Id != right.Id ||
            !string.Equals(left.Key, right.Key, StringComparison.Ordinal) ||
            !string.Equals(left.DisplayLabel, right.DisplayLabel, StringComparison.Ordinal) ||
            left.AssignmentId != right.AssignmentId)
            return false;

        var leftPredicates = (left.Predicates ?? Array.Empty<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto>())
            .OrderBy(item => item.TagId)
            .ThenBy(item => item.Kind)
            .ToArray();
        var rightPredicates = (right.Predicates ?? Array.Empty<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto>())
            .OrderBy(item => item.TagId)
            .ThenBy(item => item.Kind)
            .ToArray();
        return leftPredicates.SequenceEqual(rightPredicates);
    }

    private static bool OmitsPersistedVersion2SemanticBindings(
        AssetProcessIntelligenceEngineeringDto current,
        IReadOnlyCollection<AssetProcessIntelligenceProfileEngineeringDto> replacementProfiles)
    {
        var replacement = SignalsByProfileAndId(replacementProfiles);
        return PersistedVersion2SemanticBindings(current).Any(persisted =>
            !replacement.TryGetValue((persisted.ProfileId, persisted.Signal.Id), out var candidate) ||
            candidate.TagId != persisted.Signal.TagId ||
            candidate.SemanticBinding != persisted.Signal.SemanticBinding);
    }

    private static bool AddsOrChangesVersion2SemanticBindings(
        AssetProcessIntelligenceEngineeringDto current,
        IReadOnlyCollection<AssetProcessIntelligenceProfileEngineeringDto> replacementProfiles)
    {
        var persisted = PersistedVersion2SemanticBindings(current)
            .ToDictionary(item => (item.ProfileId, item.Signal.Id), item => item.Signal);
        return SignalsByProfileAndId(replacementProfiles)
            .Where(item => item.Value.SemanticBinding?.CatalogVersion == 2)
            .Any(item => !persisted.TryGetValue(item.Key, out var existing) ||
                         existing.TagId != item.Value.TagId ||
                         existing.SemanticBinding != item.Value.SemanticBinding);
    }

    private static IEnumerable<(Guid ProfileId, AssetProcessIntelligenceSignalEngineeringDto Signal)> PersistedVersion2SemanticBindings(
        AssetProcessIntelligenceEngineeringDto configuration) =>
        (configuration.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
        .Where(profile => profile is not null)
        .SelectMany(profile => (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
            .Where(signal => signal?.SemanticBinding?.CatalogVersion == 2)
            .Select(signal => (profile.Id, signal!)));

    private static Dictionary<(Guid ProfileId, Guid SignalId), AssetProcessIntelligenceSignalEngineeringDto> SignalsByProfileAndId(
        IReadOnlyCollection<AssetProcessIntelligenceProfileEngineeringDto> profiles) => profiles
            .Where(profile => profile is not null)
            .SelectMany(profile => (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                .Where(signal => signal is not null)
                .Select(signal => (Key: (profile.Id, signal!.Id), Signal: signal)))
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.First().Signal);

    // Authority policy content is never published from Engineering. Only its
    // reference version may refresh from the live canonical export; contract,
    // role identities and scope identities must still match exactly.
    internal static bool MatchesOperationalBaseline(
        EngineeringPackage expected, EngineeringPackage actual, EngineeringPackage canonical)
    {
        expected = expected with { ExportedAt = DateTimeOffset.UnixEpoch, Tags = expected.Tags.Select(NormalizeHistorianMetadata).ToArray() };
        actual = actual with { ExportedAt = DateTimeOffset.UnixEpoch, Tags = actual.Tags.Select(NormalizeHistorianMetadata).ToArray() };
        if (expected.AuthorityPolicyReference is { } reference && canonical.AuthorityPolicyReference is { } live)
            expected = expected with { AuthorityPolicyReference = reference with { PolicyVersion = live.PolicyVersion } };
        return JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected), JsonSerializer.SerializeToNode(actual));
    }

    internal static TagEngineeringDto NormalizeHistorianMetadata(TagEngineeringDto tag)
    {
        if (tag.Metadata is null || tag.Historian is null) return tag;
        var metadata = new Dictionary<string, string>(tag.Metadata);
        if (metadata.TryGetValue("historian.enabled", out var enabled) &&
            bool.TryParse(enabled, out var parsed) && parsed == tag.Historian.Enabled)
            metadata.Remove("historian.enabled");
        if (metadata.TryGetValue("historian.strategy", out var strategy) && strategy == tag.Historian.Strategy)
            metadata.Remove("historian.strategy");
        return tag with { Metadata = metadata.Count == 0 ? null : metadata };
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

        var authorityHa = context.RequestServices.GetService<AuthorityPolicyHaCoordinator>();
        if (authorityHa is not null)
        {
            try { await authorityHa.ReadCurrentSnapshotAsync(context.RequestAborted); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
            catch { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        }

        var manager = security.CheckWorkspace(
            context,
            SecurityCapability.AssetProcessIntelligenceManage,
            new AuthorizationResource(ResourceKind: resourceKind, ResourceId: scopeId));
        var engineering = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
        var viewer = security.CheckWorkspace(context, SecurityCapability.EngineeringView);
        if (!manager.Allowed || (!engineering.Allowed && !viewer.Allowed))
        {
            var denial = !manager.Allowed ? manager : viewer;
            await audit.RecordAuthorizationDeniedAsync(
                context,
                denial,
                active ? AuditActions.AssetProcessIntelligenceActivate : AuditActions.AssetProcessIntelligencePause,
                "asset-process-intelligence-scope",
                "scope");
            return denial.FailureResult() ?? Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        AuthorityPolicyHaReadiness? activationFence = null;
        if (active && authorityHa is not null)
        {
            activationFence = await authorityHa.CheckAsync(context.RequestAborted);
            if (!activationFence.Allowed)
                return Results.Conflict(new { error = activationFence.ReasonCode.ToUpperInvariant().Replace('-', '_') });
        }

        var activationStore = context.RequestServices.GetService<AssetProcessIntelligenceActivationStore>();
        var durableGeneration = 0L;
        async Task PersistActivationAsync(string projectKey, long revision)
        {
            if (activationStore is null) return;
            var lifecycle = context.RequestServices.GetRequiredService<IAuthorityLifecycleStore>();
            var authority = await lifecycle.GetAsync(context.RequestAborted);
            if (!authority.AllowsAuthorization || activationFence is not { Allowed: true, PolicyDigest: not null })
                throw new InvalidOperationException("Intelligence activation requires a verifiable Authority fence.");
            var committedReceipt = await activationStore.ActivateAsync(new AssetProcessIntelligenceActivationEvidence(
                authority.Epoch, projectKey, revision, kind, scopeId,
                activationFence.PolicyVersion, activationFence.PolicyDigest, activationFence.HaEpoch, 1, true),
                context.RequestAborted);
            durableGeneration = committedReceipt.Generation;
        }
        if (active && !licensing.CurrentVerification.HasAssetProcessIntelligence)
            return Results.Json(new { error = "INTELLIGENCE_ENTITLEMENT_REQUIRED" }, statusCode: StatusCodes.Status403Forbidden);

        // Serialize the entire transition, including the clean/active checks. A
        // restricted manager must never publish a concurrent author's Working.
        await using var mutation = await workspace.AcquireMutationAsync(cancellationToken: context.RequestAborted);
        var scope = (intelligence.Snapshot().Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
            .FirstOrDefault(candidate => candidate.Kind == kind && candidate.ScopeId == scopeId);
        if (scope is null) return Results.NotFound();

        var descriptor = workspace.Describe();
        if (!engineering.Allowed &&
            (!string.Equals(context.Request.Headers["X-Intelligence-Project-Key"], descriptor.ProjectKey, StringComparison.Ordinal) ||
             !long.TryParse(context.Request.Headers["X-Intelligence-Active-Revision"], out var expectedRevision) ||
             expectedRevision != descriptor.BaseRevision))
            return Results.Conflict(new { error = "INTELLIGENCE_ACTIVE_REVISION_CHANGED" });
        // A pause is fail-safe even when the project cannot currently publish a
        // canonical revision: cancel local pending work before the lifecycle checks.
        if (!active && !string.IsNullOrWhiteSpace(descriptor.ProjectKey))
            executionGate.Pause(descriptor.ProjectKey, kind, scopeId);

        if (!active && activationStore is not null && !string.IsNullOrWhiteSpace(descriptor.ProjectKey))
            await activationStore.PauseAsync(descriptor.ProjectKey, kind, scopeId, context.RequestAborted);
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

        if (!engineering.Allowed)
        {
            var exchange = context.RequestServices.GetService<IEngineeringExchangeService>();
            if (exchange is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            try
            {
                var expected = exchange.ParseJson(persistedActive!.EngineeringJson) with { ExportedAt = DateTimeOffset.UnixEpoch };
                var current = exchange.ExportPackage() with { ExportedAt = DateTimeOffset.UnixEpoch };
                if (!MatchesOperationalBaseline(expected, current, current))
                    return Results.Conflict(new { error = "INTELLIGENCE_WORKSPACE_DIFFERS_FROM_ACTIVE_REVISION" });
            }
            catch
            {
                return Results.Conflict(new { error = "INTELLIGENCE_WORKSPACE_DIFFERS_FROM_ACTIVE_REVISION" });
            }
        }

        if (scope.Active == active)
        {
            if (active)
            {
                await PersistActivationAsync(descriptor.ProjectKey, descriptor.BaseRevision.Value);
                executionGate.Activate(
                    descriptor.ProjectKey,
                    descriptor.BaseRevision.Value,
                    kind,
                    scopeId,
                    activationFence?.PolicyVersion ?? 0,
                    activationFence?.HaEpoch ?? 0, durableGeneration);
            }
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

        var latest = workspace.Describe();
        if (!string.Equals(latest.ProjectKey, descriptor.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
            latest.BaseRevision != descriptor.BaseRevision)
            return Results.Conflict(new { error = "INTELLIGENCE_ACTIVE_REVISION_CHANGED" });
        var carried = new List<AssetProcessIntelligenceActivationEvidence>();
        if (activationStore is not null && authorityHa is not null)
        {
            var exchange = context.RequestServices.GetRequiredService<IEngineeringExchangeService>();
            var baseline = exchange.ParseJson(persistedActive!.EngineeringJson);
            var carryBaselineMatches = MatchesOperationalBaseline(baseline, exchange.ExportPackage(), exchange.ExportPackage());
            var preserveFence = await authorityHa.CheckAsync(context.RequestAborted);
            foreach (var other in intelligence.Snapshot().Scopes ?? [])
            {
                if (!carryBaselineMatches || !preserveFence.Allowed) break;
                if (!other.Active || (other.Kind == kind && other.ScopeId == scopeId)) continue;
                if (!executionGate.TryGetToken(descriptor.ProjectKey, descriptor.BaseRevision.Value,
                    other.Kind, other.ScopeId, out _, preserveFence.PolicyVersion, preserveFence.HaEpoch)) continue;
                var receipt = await activationStore.ReadAsync(descriptor.ProjectKey, other.Kind, other.ScopeId, context.RequestAborted);
                if (receipt is not null && receipt.Revision == descriptor.BaseRevision.Value) carried.Add(receipt);
            }
        }
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
            if (!engineering.Allowed)
            {
                var exchange = context.RequestServices.GetRequiredService<IEngineeringExchangeService>();
                var original = exchange.ParseJson(persistedActive!.EngineeringJson);
                var expected = original with
                {
                    ExportedAt = DateTimeOffset.UnixEpoch,
                    AssetProcessIntelligence = original.AssetProcessIntelligence! with
                    {
                        Scopes = original.AssetProcessIntelligence.Scopes!.Select(item =>
                            item.Kind == kind && item.ScopeId == scopeId ? item with { Active = active } : item).ToArray()
                    }
                };
                var saved = exchange.ParseJson(snapshot.EngineeringJson) with { ExportedAt = DateTimeOffset.UnixEpoch };
                // Validate the immutable saved revision, not only the pre-save
                // Working. Even an uncoordinated writer cannot add publication.
                if (!MatchesOperationalBaseline(expected, saved, exchange.ExportPackage()))
                {
                    intelligence.SetActive(kind, scopeId, scope.Active);
                    return Results.Conflict(new { error = "INTELLIGENCE_SCOPE_TRANSITION_HAS_UNRELATED_CHANGES" });
                }
            }
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

            if (activationStore is not null)
            {
                for (var index = 0; index < carried.Count; index++)
                    carried[index] = await activationStore.ActivateAsync(
                        carried[index] with { Revision = snapshot.Revision }, context.RequestAborted,
                        expectedPrevious: carried[index]);
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
            {
                activationFence = authorityHa is null
                    ? null
                    : await authorityHa.CheckAsync(context.RequestAborted);
                if (activationFence is { Allowed: false })
                {
                    executionGate.Pause(snapshot.ProjectKey, kind, scopeId);
                    return Results.Conflict(new { error = activationFence.ReasonCode.ToUpperInvariant().Replace('-', '_') });
                }
                await PersistActivationAsync(snapshot.ProjectKey, snapshot.Revision);
                executionGate.Activate(
                    snapshot.ProjectKey,
                    snapshot.Revision,
                    kind,
                    scopeId,
                    activationFence?.PolicyVersion ?? 0,
                    activationFence?.HaEpoch ?? 0, durableGeneration);
            }
            else
                executionGate.Pause(snapshot.ProjectKey, kind, scopeId);

            if (activationStore is not null && authorityHa is not null && carried.Count > 0)
            {
                var carryFence = await authorityHa.CheckAsync(context.RequestAborted);
                if (carryFence.Allowed)
                    foreach (var receipt in carried)
                    {
                        if (receipt.PolicyVersion != carryFence.PolicyVersion || receipt.PolicyDigest != carryFence.PolicyDigest ||
                            receipt.HaEpoch != carryFence.HaEpoch) continue;
                        await activationStore.TryRecoverAsync(receipt, current =>
                        {
                            executionGate.Activate(current.ProjectKey, current.Revision, current.ScopeKind, current.ScopeId,
                                current.PolicyVersion, current.HaEpoch, current.Generation);
                            return true;
                        }, context.RequestAborted);
                    }
            }
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
