using Scada.Api.Historian;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.HistoricalQueries;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;
using Scada.Security.Authorization;

namespace Scada.Api.Licensing;

public sealed class AssetProcessIntelligenceAnalyticsService(
    EngineeringWorkspace workspace,
    ScadaRuntimeFacade runtime,
    RuntimeHighAvailabilityService highAvailability,
    IProductLicenseService licensing,
    AssetProcessIntelligenceExecutionGate executionGate,
    ApiAuthorizationService security,
    IHistoricalQueryService historicalQueries,
    AuthorityPolicyHaCoordinator? authorityPolicyHa = null)
{
    public const int MaximumProfileSignals = AssetProcessIntelligenceProfileCoverageCatalogV1.MaximumSignals;
    public const int MaximumProfileRowsPerWindow = AssetProcessIntelligenceProfileCoverageCatalogV1.MaximumRowsPerWindow;

    public async Task<IResult> ExecuteAsync(
        HttpContext context,
        AssetProcessIntelligenceAnalyticsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var requestToken = cancellationToken == default ? context.RequestAborted : cancellationToken;

        var invalid = ValidateRequest(request);
        if (invalid is not null) return invalid;

        try
        {
            var trendAuthorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.TrendUse,
                cancellationToken: requestToken);
            var trendFailure = trendAuthorization.FailureResult();
            if (trendFailure is not null) return trendFailure;

            if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
                return Forbidden("INTELLIGENCE_ENTITLEMENT_REQUIRED");

            var planResult = await PrepareAsync(context, request, requestToken);
            if (planResult.Error is not null) return planResult.Error;
            var plan = planResult.Plan!;

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                requestToken,
                plan.PermitToken);
            var primary = await ReadPeriodAsync(context, request.Window!, request.BucketSeconds,
                plan, linkedCancellation.Token);
            if (primary.Error is not null) return primary.Error;

            AssetProcessIntelligenceAnalyticsPeriodResult? comparisonPeriod = null;
            AssetProcessIntelligenceAnalyticsComparison? comparison = null;
            if (request.ComparisonWindow is { } comparisonWindow)
            {
                var read = await ReadPeriodAsync(context, comparisonWindow, request.BucketSeconds,
                    plan, linkedCancellation.Token);
                if (read.Error is not null) return read.Error;
                comparisonPeriod = read.Period!;
                var currentSummary = primary.Period!.Summary;
                var comparisonSummary = comparisonPeriod.Summary;
                comparison = new AssetProcessIntelligenceAnalyticsComparison(
                    comparisonPeriod,
                    currentSummary.ValidSampleCount.HasValue && comparisonSummary.ValidSampleCount.HasValue
                        ? currentSummary.ValidSampleCount.Value - comparisonSummary.ValidSampleCount.Value
                        : null,
                    AssetProcessIntelligenceKpiV1.Difference(currentSummary.Minimum, comparisonSummary.Minimum),
                    AssetProcessIntelligenceKpiV1.Difference(currentSummary.Maximum, comparisonSummary.Maximum),
                    AssetProcessIntelligenceKpiV1.Difference(currentSummary.SampleMean, comparisonSummary.SampleMean),
                    AssetProcessIntelligenceKpiV1.Difference(currentSummary.FirstObserved?.Value, comparisonSummary.FirstObserved?.Value),
                    AssetProcessIntelligenceKpiV1.Difference(currentSummary.LastObserved?.Value, comparisonSummary.LastObserved?.Value),
                    AssetProcessIntelligenceKpiV1.Difference(currentSummary.Delta, comparisonSummary.Delta),
                    "not_assessed");
            }

            await using var finalMutation = await workspace.AcquireMutationAsync(
                plan.ChangeVersion,
                linkedCancellation.Token);
            var finalCheck = await RevalidateAsync(context, plan, linkedCancellation.Token);
            if (finalCheck is not null) return finalCheck;

            return Results.Ok(new AssetProcessIntelligenceAnalyticsResponse(
                ContractVersion: 1,
                CatalogVersion: AssetProcessIntelligenceKpiCatalogV1.CatalogVersion,
                RuleVersion: AssetProcessIntelligenceKpiCatalogV1.RuleVersion,
                ProjectKey: plan.ProjectKey,
                ActiveRevision: plan.Revision,
                ScopeKind: request.ScopeKind,
                ScopeId: request.ScopeId,
                TagId: plan.Tag.Id,
                TagPath: plan.Tag.Path,
                TagDataType: plan.Tag.DataType,
                Unit: plan.Tag.EngineeringUnit,
                Period: primary.Period!,
                Comparison: comparison,
                Provenance: new AssetProcessIntelligenceAnalyticsProvenance(
                    HistoricalDatasets.HistorianSamples,
                    HistoricalQueryContract.Version,
                    "raw Historian observations are counted separately from derived indicators",
                    AssetProcessIntelligenceKpiCatalogV1.RuleVersion,
                    plan.Assignments,
                    "not_assessed")));
        }
        catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Conflict("INTELLIGENCE_EXECUTION_PERMIT_CANCELLED");
        }
        catch (EngineeringWorkspaceVersionConflictException)
        {
            return Conflict("INTELLIGENCE_WORKSPACE_CHANGED");
        }
        catch (HistoricalQueryUnauthorizedException)
        {
            return Results.Unauthorized();
        }
        catch (HistoricalQueryForbiddenException)
        {
            return Forbidden("HISTORICAL_QUERY_FORBIDDEN");
        }
        catch (HistoricalQueryCursorException)
        {
            return Results.BadRequest(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_QUERY_CURSOR_INVALID", "Historical query cursor is invalid."));
        }
        catch (HistoricalQueryValidationException)
        {
            return Results.BadRequest(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_QUERY_INVALID", "The bounded Historian query was rejected."));
        }
        catch (HistoricalQueryProviderException)
        {
            return Results.Json(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_QUERY_UNAVAILABLE", "Historical data is unavailable."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidDataException)
        {
            return Results.Json(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_RESPONSE_INVALID", "The Historian response did not match the requested TAG and window."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public async Task<IResult> ExecuteNetObservedRateV1Async(
        HttpContext context,
        AssetProcessIntelligenceNetObservedRateV1QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var requestToken = cancellationToken == default ? context.RequestAborted : cancellationToken;
        var analyticsRequest = new AssetProcessIntelligenceAnalyticsQueryRequest(
            request.ScopeKind,
            request.ScopeId,
            request.TagId,
            request.Window);
        var invalid = ValidateRequest(analyticsRequest);
        if (invalid is not null) return invalid;

        try
        {
            var trendAuthorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.TrendUse,
                cancellationToken: requestToken);
            var trendFailure = trendAuthorization.FailureResult();
            if (trendFailure is not null) return trendFailure;

            if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
                return Forbidden("INTELLIGENCE_ENTITLEMENT_REQUIRED");

            var planResult = await PrepareAsync(context, analyticsRequest, requestToken);
            if (planResult.Error is not null) return planResult.Error;
            var plan = planResult.Plan!;

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                requestToken,
                plan.PermitToken);
            var read = await ReadObservationsAsync(
                context,
                request.Window!,
                plan,
                linkedCancellation.Token,
                densityErrorCode: "INTELLIGENCE_NET_OBSERVED_RATE_WINDOW_TOO_DENSE");
            if (read.Error is not null) return read.Error;

            await using var finalMutation = await workspace.AcquireMutationAsync(
                plan.ChangeVersion,
                linkedCancellation.Token);
            var finalCheck = await RevalidateAsync(context, plan, linkedCancellation.Token);
            if (finalCheck is not null) return finalCheck;

            return Results.Ok(new AssetProcessIntelligenceNetObservedRateV1Response(
                AssetProcessIntelligenceNetObservedRateV1Catalog.ContractVersion,
                AssetProcessIntelligenceNetObservedRateV1Catalog.CatalogVersion,
                AssetProcessIntelligenceNetObservedRateV1Catalog.RuleId,
                AssetProcessIntelligenceNetObservedRateV1Catalog.RuleVersion,
                plan.ProjectKey,
                plan.Revision,
                request.Window!,
                request.ScopeKind,
                request.ScopeId,
                plan.Tag.Id,
                plan.Tag.Path,
                plan.Tag.DataType,
                AssetProcessIntelligenceNetObservedRateV1Calculator.Calculate(
                    read.Observations!,
                    plan.Tag.EngineeringUnit),
                new AssetProcessIntelligenceNetObservedRateV1Provenance(
                    HistoricalDatasets.HistorianSamples,
                    HistoricalQueryContract.Version,
                    "persisted Historian observation/publication timestamp (`timestamp`); source/server timestamps are not used",
                    "finite numeric observations whose quality is Good, using the Stage 3A acceptance rule")));
        }
        catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Conflict("INTELLIGENCE_EXECUTION_PERMIT_CANCELLED");
        }
        catch (EngineeringWorkspaceVersionConflictException)
        {
            return Conflict("INTELLIGENCE_WORKSPACE_CHANGED");
        }
        catch (HistoricalQueryUnauthorizedException)
        {
            return Results.Unauthorized();
        }
        catch (HistoricalQueryForbiddenException)
        {
            return Forbidden("HISTORICAL_QUERY_FORBIDDEN");
        }
        catch (HistoricalQueryCursorException)
        {
            return Results.BadRequest(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_QUERY_CURSOR_INVALID", "Historical query cursor is invalid."));
        }
        catch (HistoricalQueryValidationException)
        {
            return Results.BadRequest(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_QUERY_INVALID", "The bounded Historian query was rejected."));
        }
        catch (HistoricalQueryProviderException)
        {
            return Results.Json(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_QUERY_UNAVAILABLE", "Historical data is unavailable."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidDataException)
        {
            return Results.Json(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_RESPONSE_INVALID", "The Historian response did not match the requested TAG and window."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public async Task<IResult> ExecuteProfileCoverageAsync(
        HttpContext context,
        AssetProcessIntelligenceProfileCoverageQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var requestToken = cancellationToken == default ? context.RequestAborted : cancellationToken;
        var invalid = ValidateProfileCoverageRequest(request);
        if (invalid is not null) return invalid;

        try
        {
            var trendAuthorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.TrendUse,
                cancellationToken: requestToken);
            var trendFailure = trendAuthorization.FailureResult();
            if (trendFailure is not null) return trendFailure;
            if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
                return Forbidden("INTELLIGENCE_ENTITLEMENT_REQUIRED");

            var planResult = await PrepareProfileCoverageAsync(context, request, requestToken);
            if (planResult.Error is not null) return planResult.Error;
            var plan = planResult.Plan!;
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                requestToken,
                plan.PermitToken);

            var signalResults = new List<AssetProcessIntelligenceProfileSignalCoverage>(plan.Signals.Count);
            var requiredTimestampSets = new List<HashSet<DateTimeOffset>>();
            var allAssignments = new List<AssetProcessIntelligenceAssignmentProvenance>();
            var totalRows = 0;
            foreach (var signalPlan in plan.Signals)
            {
                allAssignments.AddRange(signalPlan.Assignments);
                AssetProcessIntelligenceKpiSummary? summary = null;
                var status = "unsupported_non_numeric_tag_type";
                if (IsNumeric(signalPlan.Tag.DataType))
                {
                    var remainingRows = MaximumProfileRowsPerWindow - totalRows;
                    var read = await ReadPeriodAsync(
                        context,
                        request.Window!,
                        request.BucketSeconds,
                        signalPlan.Plan,
                        linkedCancellation.Token,
                        Math.Max(1, remainingRows),
                        "INTELLIGENCE_PROFILE_WINDOW_TOO_DENSE",
                        retainRawObservations: true);
                    if (read.Error is not null) return read.Error;
                    var period = read.Period!;
                    summary = period.Summary;
                    var rawObservations = read.RawObservations ??
                        throw new InvalidOperationException("Profile coverage requires retained historian observations.");
                    if (rawObservations.Count > remainingRows)
                        return ProfileWindowTooDense();
                    totalRows += rawObservations.Count;
                    status = summary.AcceptedSampleCount > 0
                        ? "descriptive_trend_available"
                        : "insufficient_no_good_numeric_samples";
                    if (signalPlan.Signal.Required)
                    {
                        requiredTimestampSets.Add(rawObservations
                            .Where(observation => AssetProcessIntelligenceKpiV1.TryGetAcceptedGoodNumericValue(observation, out _))
                            .Select(observation => observation.TimestampUtc)
                            .ToHashSet());
                    }
                    signalResults.Add(new AssetProcessIntelligenceProfileSignalCoverage(
                        signalPlan.Signal.Id,
                        signalPlan.Signal.Key,
                        signalPlan.Signal.SignalRole ?? string.Empty,
                        signalPlan.Signal.Required,
                        signalPlan.Tag.Id,
                        signalPlan.Tag.Path,
                        signalPlan.Tag.DataType,
                        signalPlan.Tag.EngineeringUnit,
                        status,
                        summary,
                        signalPlan.Assignments));
                    continue;
                }

                signalResults.Add(new AssetProcessIntelligenceProfileSignalCoverage(
                    signalPlan.Signal.Id,
                    signalPlan.Signal.Key,
                    signalPlan.Signal.SignalRole ?? string.Empty,
                    signalPlan.Signal.Required,
                    signalPlan.Tag.Id,
                    signalPlan.Tag.Path,
                    signalPlan.Tag.DataType,
                    signalPlan.Tag.EngineeringUnit,
                    status,
                    summary,
                    signalPlan.Assignments));
            }

            await using var finalMutation = await workspace.AcquireMutationAsync(
                plan.ChangeVersion,
                linkedCancellation.Token);
            foreach (var signalPlan in plan.Signals)
            {
                var finalCheck = await RevalidateAsync(context, signalPlan.Plan, linkedCancellation.Token);
                if (finalCheck is not null) return finalCheck;
            }

            var requiredSignalCount = plan.Signals.Count(signal => signal.Signal.Required);
            var requiredSignalsWithSamples = signalResults.Count(signal =>
                signal.Required && signal.Summary is { AcceptedSampleCount: > 0 });
            var intersectionCount = requiredTimestampSets.Count == 0 ||
                                    requiredTimestampSets.Count != requiredSignalCount
                ? 0
                : requiredTimestampSets.Skip(1)
                    .Aggregate(new HashSet<DateTimeOffset>(requiredTimestampSets[0]),
                        static (intersection, next) =>
                        {
                            intersection.IntersectWith(next);
                            return intersection;
                        })
                    .Count;
            var alignmentStatus = requiredSignalCount == 0
                ? "insufficient_no_required_signals"
                : requiredSignalsWithSamples != requiredSignalCount
                    ? "insufficient_required_signal_data"
                    : requiredSignalCount == 1
                        ? "single_required_signal_no_alignment_comparison"
                        : intersectionCount == 0
                            ? "insufficient_exact_historian_timestamp_overlap"
                            : "exact_historian_timestamp_overlap_observed";
            var assignments = Array.AsReadOnly(allAssignments
                .DistinctBy(item => (item.AssignmentId, item.SignalId))
                .OrderBy(item => item.EquipmentId)
                .ThenBy(item => item.ProfileId)
                .ThenBy(item => item.SignalId)
                .ToArray());

            return Results.Ok(new AssetProcessIntelligenceProfileCoverageResponse(
                ContractVersion: 1,
                RuleCatalogVersion: AssetProcessIntelligenceProfileCoverageCatalogV1.CatalogVersion,
                RuleVersion: AssetProcessIntelligenceProfileCoverageCatalogV1.RuleVersion,
                ProjectKey: plan.ProjectKey,
                ActiveRevision: plan.Revision,
                ScopeKind: request.ScopeKind,
                ScopeId: request.ScopeId,
                ProfileId: plan.Profile.Id,
                ProfileKey: plan.Profile.Key,
                ProfileName: plan.Profile.Name,
                Window: request.Window!,
                Signals: Array.AsReadOnly(signalResults.ToArray()),
                TemporalAlignment: new AssetProcessIntelligenceProfileTemporalAlignment(
                    requiredSignalCount,
                    requiredSignalsWithSamples,
                    intersectionCount,
                    alignmentStatus,
                    "distinct exact Historian event timestamps; not source acquisition time",
                    "unverified",
                    "unknown",
                    "unknown"),
                Provenance: new AssetProcessIntelligenceProfileCoverageProvenance(
                    HistoricalDatasets.HistorianSamples,
                    HistoricalQueryContract.Version,
                    AssetProcessIntelligenceProfileCoverageCatalogV1.RuleVersion,
                    assignments,
                    "metadata only; SignalRole is not interpreted",
                    "none; each descriptive trend retains the canonical TAG unit",
                    "unsupported_without_a_canonical_semantic_and_unit-dimension_contract")));
        }
        catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Conflict("INTELLIGENCE_EXECUTION_PERMIT_CANCELLED");
        }
        catch (EngineeringWorkspaceVersionConflictException)
        {
            return Conflict("INTELLIGENCE_WORKSPACE_CHANGED");
        }
        catch (HistoricalQueryUnauthorizedException)
        {
            return Results.Unauthorized();
        }
        catch (HistoricalQueryForbiddenException)
        {
            return Forbidden("HISTORICAL_QUERY_FORBIDDEN");
        }
        catch (HistoricalQueryCursorException)
        {
            return Results.BadRequest(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_QUERY_CURSOR_INVALID", "Historical query cursor is invalid."));
        }
        catch (HistoricalQueryValidationException)
        {
            return Results.BadRequest(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_QUERY_INVALID", "The bounded Historian query was rejected."));
        }
        catch (HistoricalQueryProviderException)
        {
            return Results.Json(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_QUERY_UNAVAILABLE", "Historical data is unavailable."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidDataException)
        {
            return Results.Json(new AssetProcessIntelligenceAnalyticsApiError(
                "HISTORICAL_RESPONSE_INVALID", "The Historian response did not match the requested TAG and window."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task<PreparedProfileCoverageResult> PrepareProfileCoverageAsync(
        HttpContext context,
        AssetProcessIntelligenceProfileCoverageQueryRequest request,
        CancellationToken cancellationToken)
    {
        var beforeLock = workspace.Describe();
        if (!IsWorkspaceRuntimeCoherent(beforeLock, runtime.Describe(), out var coherenceError))
            return PreparedProfileCoverageResult.Fail(coherenceError!);

        await using var mutation = await workspace.AcquireMutationAsync(beforeLock.ChangeVersion, cancellationToken);
        var descriptor = workspace.Describe();
        if (!IsWorkspaceRuntimeCoherent(descriptor, runtime.Describe(), out coherenceError))
            return PreparedProfileCoverageResult.Fail(coherenceError!);
        if (!highAvailability.CanOwnIndustrialEffects())
            return PreparedProfileCoverageResult.Fail(Conflict("INTELLIGENCE_RUNTIME_NOT_EFFECTIVE_ACTIVE"));
        if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
            return PreparedProfileCoverageResult.Fail(Forbidden("INTELLIGENCE_ENTITLEMENT_REQUIRED"));

        var policyVersion = 0L;
        var haEpoch = 0L;
        if (authorityPolicyHa is not null)
        {
            var authorityFence = await authorityPolicyHa.CheckAsync(cancellationToken);
            if (!authorityFence.Allowed)
                return PreparedProfileCoverageResult.Fail(Conflict(authorityFence.ReasonCode.ToUpperInvariant().Replace('-', '_')));
            policyVersion = authorityFence.PolicyVersion;
            haEpoch = authorityFence.HaEpoch;
            var refreshedTrendAuthorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.TrendUse,
                cancellationToken: cancellationToken);
            var refreshedTrendFailure = refreshedTrendAuthorization.FailureResult();
            if (refreshedTrendFailure is not null)
                return PreparedProfileCoverageResult.Fail(refreshedTrendFailure);
        }

        var configuration = workspace.AssetProcessIntelligence.Snapshot();
        if (configuration.Version != 2)
            return PreparedProfileCoverageResult.Fail(Conflict("INTELLIGENCE_V2_CONFIGURATION_REQUIRED"));
        var scopeMatches = (configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
            .Where(scope => scope.Kind == request.ScopeKind && scope.ScopeId == request.ScopeId)
            .Take(2)
            .ToArray();
        if (scopeMatches.Length != 1 || !scopeMatches[0].Active)
            return PreparedProfileCoverageResult.Fail(Conflict("INTELLIGENCE_SCOPE_NOT_ACTIVE_OR_AMBIGUOUS"));
        if (!executionGate.TryGetToken(
                descriptor.ProjectKey!,
                descriptor.BaseRevision!.Value,
                request.ScopeKind,
                request.ScopeId,
                out var permitToken,
                policyVersion,
                haEpoch))
            return PreparedProfileCoverageResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_REQUIRED"));

        var profileMatches = (configuration.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(profile => profile.Id == request.ProfileId)
            .Take(2)
            .ToArray();
        if (profileMatches.Length != 1)
            return PreparedProfileCoverageResult.Fail(NotFound("INTELLIGENCE_PROFILE_NOT_AVAILABLE"));
        var profile = profileMatches[0];
        var signals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>()).ToArray();
        if (signals.Length == 0)
            return PreparedProfileCoverageResult.Fail(BadRequest("INTELLIGENCE_PROFILE_SIGNALS_REQUIRED"));
        if (signals.Length > MaximumProfileSignals)
            return PreparedProfileCoverageResult.Fail(BadRequest("INTELLIGENCE_PROFILE_SIGNAL_LIMIT_EXCEEDED"));
        if (signals.Any(signal => signal is null || signal.Id == Guid.Empty || signal.TagId == Guid.Empty))
            return PreparedProfileCoverageResult.Fail(Conflict("INTELLIGENCE_PROFILE_SIGNAL_IDENTITY_INVALID"));
        if (signals.Select(signal => signal.TagId).Distinct().Count() != signals.Length)
            return PreparedProfileCoverageResult.Fail(BadRequest("INTELLIGENCE_PROFILE_DUPLICATE_TAG_REFERENCE_UNSUPPORTED"));

        var signalPlans = new List<ProfileSignalPlan>(signals.Length);
        foreach (var signal in signals.OrderBy(signal => signal.Key, StringComparer.Ordinal))
        {
            var resolution = ResolveScopeSources(
                request.ScopeKind,
                request.ScopeId,
                signal.TagId,
                configuration,
                workspace.Assets);
            if (resolution.Error is not null)
                return PreparedProfileCoverageResult.Fail(resolution.Error);
            var assignments = resolution.Assignments!
                .Where(item => item.ProfileId == profile.Id && item.SignalId == signal.Id)
                .ToArray();
            if (assignments.Length == 0)
                return PreparedProfileCoverageResult.Fail(NotFound("INTELLIGENCE_PROFILE_NOT_ASSIGNED_TO_SCOPE"));
            if (!runtime.TryGetTag(signal.TagId, out var tag) || tag is null)
                return PreparedProfileCoverageResult.Fail(NotFound("INTELLIGENCE_ANALYTICS_SOURCE_NOT_AVAILABLE"));

            var tagAuthorization = await security.CheckRuntimeTagAsync(
                context,
                runtime,
                tag,
                TagAccessOperation.Read,
                cancellationToken);
            var tagFailure = tagAuthorization.FailureResult();
            if (tagFailure is not null)
                return PreparedProfileCoverageResult.Fail(tagFailure);

            var plan = new PreparedPlan(
                descriptor.ProjectKey!,
                descriptor.BaseRevision!.Value,
                descriptor.ChangeVersion,
                tag,
                Array.AsReadOnly(assignments),
                permitToken,
                request.ScopeKind,
                request.ScopeId,
                policyVersion,
                haEpoch);
            signalPlans.Add(new ProfileSignalPlan(signal, tag, Array.AsReadOnly(assignments), plan));
        }

        if (permitToken.IsCancellationRequested)
            return PreparedProfileCoverageResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_CANCELLED"));
        return PreparedProfileCoverageResult.Ready(new PreparedProfilePlan(
            descriptor.ProjectKey!,
            descriptor.BaseRevision!.Value,
            descriptor.ChangeVersion,
            profile,
            Array.AsReadOnly(signalPlans.ToArray()),
            permitToken));
    }

    private async Task<PreparedPlanResult> PrepareAsync(
        HttpContext context,
        AssetProcessIntelligenceAnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var beforeLock = workspace.Describe();
        if (!IsWorkspaceRuntimeCoherent(beforeLock, runtime.Describe(), out var coherenceError))
            return PreparedPlanResult.Fail(coherenceError!);

        await using var mutation = await workspace.AcquireMutationAsync(
            beforeLock.ChangeVersion,
            cancellationToken);
        var descriptor = workspace.Describe();
        var runtimeDescriptor = runtime.Describe();
        if (!IsWorkspaceRuntimeCoherent(descriptor, runtimeDescriptor, out coherenceError))
            return PreparedPlanResult.Fail(coherenceError!);
        if (!highAvailability.CanOwnIndustrialEffects())
            return PreparedPlanResult.Fail(Conflict("INTELLIGENCE_RUNTIME_NOT_EFFECTIVE_ACTIVE"));
        if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
            return PreparedPlanResult.Fail(Forbidden("INTELLIGENCE_ENTITLEMENT_REQUIRED"));

        var policyVersion = 0L;
        var haEpoch = 0L;
        if (authorityPolicyHa is not null)
        {
            var authorityFence = await authorityPolicyHa.CheckAsync(cancellationToken);
            if (!authorityFence.Allowed)
                return PreparedPlanResult.Fail(Conflict(authorityFence.ReasonCode.ToUpperInvariant().Replace('-', '_')));
            policyVersion = authorityFence.PolicyVersion;
            haEpoch = authorityFence.HaEpoch;

            var refreshedTrendAuthorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.TrendUse,
                cancellationToken: cancellationToken);
            var refreshedTrendFailure = refreshedTrendAuthorization.FailureResult();
            if (refreshedTrendFailure is not null)
                return PreparedPlanResult.Fail(refreshedTrendFailure);
        }

        var configuration = workspace.AssetProcessIntelligence.Snapshot();
        if (configuration.Version != 2)
            return PreparedPlanResult.Fail(Conflict("INTELLIGENCE_V2_CONFIGURATION_REQUIRED"));
        var scopeMatches = (configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
            .Where(scope => scope.Kind == request.ScopeKind && scope.ScopeId == request.ScopeId)
            .Take(2)
            .ToArray();
        if (scopeMatches.Length > 1)
            return PreparedPlanResult.Fail(Conflict("INTELLIGENCE_SCOPE_CONFIGURATION_AMBIGUOUS"));
        var activeScope = scopeMatches.SingleOrDefault();
        if (activeScope is null || !activeScope.Active)
            return PreparedPlanResult.Fail(Conflict("INTELLIGENCE_SCOPE_NOT_ACTIVE"));

        if (!executionGate.TryGetToken(
                descriptor.ProjectKey!,
                descriptor.BaseRevision!.Value,
                request.ScopeKind,
                request.ScopeId,
                out var permitToken,
                policyVersion,
                haEpoch))
            return PreparedPlanResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_REQUIRED"));

        if (!runtime.TryGetTag(request.TagId, out var tag) || tag is null || !IsNumeric(tag.DataType))
            return PreparedPlanResult.Fail(NotFound("INTELLIGENCE_ANALYTICS_SOURCE_NOT_AVAILABLE"));

        var scopeResolution = ResolveScopeSources(
            request.ScopeKind,
            request.ScopeId,
            request.TagId,
            configuration,
            workspace.Assets);
        if (scopeResolution.Error is not null)
            return PreparedPlanResult.Fail(scopeResolution.Error);

        var tagAuthorization = await security.CheckRuntimeTagAsync(
            context,
            runtime,
            tag,
            TagAccessOperation.Read,
            cancellationToken);
        var tagFailure = tagAuthorization.FailureResult();
        if (tagFailure is not null)
            return PreparedPlanResult.Fail(tagFailure);

        if (permitToken.IsCancellationRequested)
            return PreparedPlanResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_CANCELLED"));

        return PreparedPlanResult.Ready(new PreparedPlan(
            descriptor.ProjectKey!,
            descriptor.BaseRevision!.Value,
            descriptor.ChangeVersion,
            tag,
            scopeResolution.Assignments!,
            permitToken,
            request.ScopeKind,
            request.ScopeId,
            policyVersion,
            haEpoch));
    }

    private async Task<PeriodReadResult> ReadPeriodAsync(
        HttpContext context,
        AssetProcessIntelligenceAnalyticsWindow window,
        int bucketSeconds,
        PreparedPlan plan,
        CancellationToken cancellationToken,
        int maximumRows = AssetProcessIntelligenceKpiCatalogV1.MaximumRowsPerWindow,
        string densityErrorCode = "INTELLIGENCE_ANALYTICS_WINDOW_TOO_DENSE",
        bool retainRawObservations = false)
    {
        var read = await ReadObservationsAsync(
            context,
            window,
            plan,
            cancellationToken,
            maximumRows,
            densityErrorCode,
            ProfileWindowTooDense());
        if (read.Error is not null) return PeriodReadResult.Fail(read.Error);

        var observations = read.Observations!;
        return PeriodReadResult.Ready(
            new AssetProcessIntelligenceAnalyticsPeriodResult(
                window,
                AssetProcessIntelligenceKpiV1.Calculate(observations, window, bucketSeconds)),
            retainRawObservations ? observations : null);
    }

    private async Task<ObservationReadResult> ReadObservationsAsync(
        HttpContext context,
        AssetProcessIntelligenceAnalyticsWindow window,
        PreparedPlan plan,
        CancellationToken cancellationToken,
        int maximumRows = AssetProcessIntelligenceKpiCatalogV1.MaximumRowsPerWindow,
        string densityErrorCode = "INTELLIGENCE_ANALYTICS_WINDOW_TOO_DENSE",
        IResult? remainingLimitError = null)
    {
        if (maximumRows < 1 || maximumRows > AssetProcessIntelligenceKpiCatalogV1.MaximumRowsPerWindow)
            throw new ArgumentOutOfRangeException(nameof(maximumRows));
        var observations = new List<AssetProcessIntelligenceRawObservation>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        var pageCount = 0;
        do
        {
            pageCount++;
            var maximumPages = AssetProcessIntelligenceKpiCatalogV1.MaximumRowsPerWindow /
                               AssetProcessIntelligenceKpiCatalogV1.QueryPageSize + 1;
            if (pageCount > maximumPages)
                throw new InvalidDataException("Historian pagination exceeded the bounded page count.");
            cancellationToken.ThrowIfCancellationRequested();
            await using var mutation = await workspace.AcquireMutationAsync(
                plan.ChangeVersion,
                cancellationToken);
            var validation = await RevalidateAsync(context, plan, cancellationToken);
            if (validation is not null) return ObservationReadResult.Fail(validation);

            var remainingRows = maximumRows - observations.Count;
            if (remainingRows <= 0)
                return ObservationReadResult.Fail(remainingLimitError ?? WindowTooDense(densityErrorCode));
            var pageSize = Math.Min(AssetProcessIntelligenceKpiCatalogV1.QueryPageSize, remainingRows);

            var request = new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Absolute(window.FromUtc, window.ToUtc),
                Filters:
                [new HistoricalFilter(
                    "tag.id",
                    HistoricalFilterOperator.Eq,
                    [HistoricalQueryValue.FromGuid(plan.Tag.Id)])],
                OrderBy: [new HistoricalSort("timestamp", HistoricalSortDirection.Ascending)],
                Page: new HistoricalPageRequest(pageSize, cursor));
            var response = await historicalQueries.QueryAsync(request, cancellationToken);
            ValidatePage(response, window, plan.Tag.Id, pageSize);
            if (observations.Count + response.Rows.Count > maximumRows)
                return ObservationReadResult.Fail(Results.Json(
                    new AssetProcessIntelligenceAnalyticsApiError(
                        densityErrorCode,
                        "The requested window exceeds the fixed observation limit; no partial result was returned."),
                    statusCode: StatusCodes.Status413PayloadTooLarge));

            var countAfterPage = observations.Count + response.Rows.Count;
            if (countAfterPage == maximumRows &&
                response.NextCursor is not null)
                return ObservationReadResult.Fail(Results.Json(
                    new AssetProcessIntelligenceAnalyticsApiError(
                        densityErrorCode,
                        "The requested window reaches the fixed observation limit while more rows remain; no partial result was returned."),
                    statusCode: StatusCodes.Status413PayloadTooLarge));

            foreach (var row in response.Rows)
                observations.Add(ToObservation(row));

            var postQueryValidation = await RevalidateAsync(context, plan, cancellationToken);
            if (postQueryValidation is not null) return ObservationReadResult.Fail(postQueryValidation);

            cursor = response.NextCursor;
            if (cursor is not null && !cursors.Add(cursor))
                throw new InvalidDataException("Historian pagination cursor repeated.");
        }
        while (cursor is not null);

        return ObservationReadResult.Ready(observations.AsReadOnly());
    }

    private async Task<IResult?> RevalidateAsync(
        HttpContext context,
        PreparedPlan plan,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var descriptor = workspace.Describe();
        var runtimeDescriptor = runtime.Describe();
        if (!IsWorkspaceRuntimeCoherent(descriptor, runtimeDescriptor, out var error))
            return error;
        if (descriptor.ChangeVersion != plan.ChangeVersion)
            return Conflict("INTELLIGENCE_WORKSPACE_CHANGED");
        if (!highAvailability.CanOwnIndustrialEffects())
            return Conflict("INTELLIGENCE_RUNTIME_NOT_EFFECTIVE_ACTIVE");
        if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
            return Forbidden("INTELLIGENCE_ENTITLEMENT_REQUIRED");
        if (authorityPolicyHa is not null)
        {
            var authorityFence = await authorityPolicyHa.CheckAsync(cancellationToken);
            if (!authorityFence.Allowed ||
                authorityFence.PolicyVersion != plan.AuthorityPolicyVersion ||
                authorityFence.HaEpoch != plan.HaEpoch)
                return Conflict(!authorityFence.Allowed
                    ? authorityFence.ReasonCode.ToUpperInvariant().Replace('-', '_')
                    : "INTELLIGENCE_AUTHORITY_POLICY_CHANGED");
        }
        if (!executionGate.TryGetToken(
                plan.ProjectKey,
                plan.Revision,
                plan.ScopeKind,
                plan.ScopeId,
                out var currentPermit,
                plan.AuthorityPolicyVersion,
                plan.HaEpoch) || currentPermit != plan.PermitToken || currentPermit.IsCancellationRequested)
            return Conflict("INTELLIGENCE_EXECUTION_PERMIT_REQUIRED");

        var trendAuthorization = await security.CheckRuntimeAsync(
            context,
            runtime,
            SecurityCapability.TrendUse,
            cancellationToken: cancellationToken);
        var trendFailure = trendAuthorization.FailureResult();
        if (trendFailure is not null) return trendFailure;

        if (!runtime.TryGetTag(plan.Tag.Id, out var activeTag) || activeTag is null ||
            !string.Equals(activeTag.Path, plan.Tag.Path, StringComparison.OrdinalIgnoreCase) ||
            activeTag.DataType != plan.Tag.DataType)
            return Conflict("INTELLIGENCE_ACTIVE_TAG_CHANGED");

        var authorization = await security.CheckRuntimeTagAsync(
            context,
            runtime,
            activeTag,
            TagAccessOperation.Read,
            cancellationToken);
        return authorization.FailureResult();
    }

    private static ScopeResolution ResolveScopeSources(
        AssetProcessIntelligenceScopeKind kind,
        Guid scopeId,
        Guid tagId,
        AssetProcessIntelligenceEngineeringDto configuration,
        IEngineeringAssetRegistry assets)
    {
        var profiles = (configuration.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .GroupBy(profile => profile.Id)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var equipment = assets.SnapshotEquipment()
            .Where(item => item.Id.HasValue && item.Id.Value != Guid.Empty)
            .GroupBy(item => item.Id!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var locations = assets.SnapshotLocations()
            .Where(item => item.Id.HasValue && item.Id.Value != Guid.Empty)
            .GroupBy(item => item.Id!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        if (kind == AssetProcessIntelligenceScopeKind.Equipment &&
            (!equipment.TryGetValue(scopeId, out var scopedEquipment) || scopedEquipment.Length != 1))
            return ScopeResolution.Fail(Conflict("INTELLIGENCE_SCOPE_RESOURCE_INVALID"));
        if (kind == AssetProcessIntelligenceScopeKind.Location &&
            (!locations.TryGetValue(scopeId, out var scopedLocation) || scopedLocation.Length != 1))
            return ScopeResolution.Fail(Conflict("INTELLIGENCE_SCOPE_RESOURCE_INVALID"));

        var matches = new List<AssetProcessIntelligenceAssignmentProvenance>();
        foreach (var assignment in configuration.Assignments ?? Array.Empty<AssetProcessIntelligenceAssignmentEngineeringDto>())
        {
            if (!equipment.TryGetValue(assignment.EquipmentId, out var equipmentMatches) || equipmentMatches.Length != 1)
                continue;
            var assignedEquipment = equipmentMatches[0];
            if (kind == AssetProcessIntelligenceScopeKind.Equipment && assignedEquipment.Id != scopeId)
                continue;
            if (kind == AssetProcessIntelligenceScopeKind.Location)
            {
                var ancestry = IsWithinLocation(assignedEquipment.LocationId, scopeId, locations);
                if (ancestry == AncestryResult.Invalid)
                    return ScopeResolution.Fail(Conflict("INTELLIGENCE_LOCATION_ANCESTRY_INVALID"));
                if (ancestry != AncestryResult.Yes) continue;
            }

            if (!profiles.TryGetValue(assignment.ProfileId, out var profileMatches) || profileMatches.Length != 1)
                continue;
            foreach (var signal in profileMatches[0].Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
            {
                if (signal.TagId != tagId) continue;
                matches.Add(new AssetProcessIntelligenceAssignmentProvenance(
                    assignment.Id,
                    assignment.EquipmentId,
                    assignment.ProfileId,
                    signal.Id,
                    signal.Key));
            }
        }

        if (matches.Count == 0)
            return ScopeResolution.Fail(NotFound("INTELLIGENCE_ANALYTICS_SOURCE_NOT_AVAILABLE"));
        var distinct = matches
            .DistinctBy(item => (item.AssignmentId, item.SignalId))
            .OrderBy(item => item.EquipmentId)
            .ThenBy(item => item.ProfileId)
            .ThenBy(item => item.SignalId)
            .ToArray();
        return ScopeResolution.Ready(Array.AsReadOnly(distinct));
    }

    private static AncestryResult IsWithinLocation(
        Guid? equipmentLocation,
        Guid targetLocation,
        IReadOnlyDictionary<Guid, LocationEngineeringDto[]> locations)
    {
        if (!equipmentLocation.HasValue || equipmentLocation.Value == Guid.Empty)
            return AncestryResult.No;
        var visited = new HashSet<Guid>();
        var current = equipmentLocation.Value;
        while (true)
        {
            if (!visited.Add(current)) return AncestryResult.Invalid;
            if (!locations.TryGetValue(current, out var matches) || matches.Length != 1)
                return AncestryResult.Invalid;
            if (current == targetLocation) return AncestryResult.Yes;
            var parent = matches[0].ParentLocationId;
            if (!parent.HasValue || parent.Value == Guid.Empty) return AncestryResult.No;
            current = parent.Value;
        }
    }

    private static IResult? ValidateRequest(AssetProcessIntelligenceAnalyticsQueryRequest request)
    {
        if (!Enum.IsDefined(request.ScopeKind) || request.ScopeId == Guid.Empty || request.TagId == Guid.Empty)
            return BadRequest("INTELLIGENCE_ANALYTICS_IDENTITY_INVALID");
        if (request.Window is null || !ValidWindow(request.Window))
            return BadRequest("INTELLIGENCE_ANALYTICS_WINDOW_INVALID");
        if (request.ComparisonWindow is { } comparison &&
            (!ValidWindow(comparison) ||
             comparison.ToUtc - comparison.FromUtc != request.Window.ToUtc - request.Window.FromUtc))
            return BadRequest("INTELLIGENCE_ANALYTICS_COMPARISON_WINDOW_INVALID");
        if (!AssetProcessIntelligenceKpiCatalogV1.AllowedBucketSizesSeconds.Contains(request.BucketSeconds))
            return BadRequest("INTELLIGENCE_ANALYTICS_BUCKET_SIZE_INVALID");
        return null;
    }

    private static IResult? ValidateProfileCoverageRequest(
        AssetProcessIntelligenceProfileCoverageQueryRequest request)
    {
        if (!Enum.IsDefined(request.ScopeKind) || request.ScopeId == Guid.Empty || request.ProfileId == Guid.Empty)
            return BadRequest("INTELLIGENCE_PROFILE_COVERAGE_IDENTITY_INVALID");
        if (request.Window is null || !ValidWindow(request.Window))
            return BadRequest("INTELLIGENCE_ANALYTICS_WINDOW_INVALID");
        if (!AssetProcessIntelligenceKpiCatalogV1.AllowedBucketSizesSeconds.Contains(request.BucketSeconds))
            return BadRequest("INTELLIGENCE_ANALYTICS_BUCKET_SIZE_INVALID");
        return null;
    }

    private static bool ValidWindow(AssetProcessIntelligenceAnalyticsWindow window) =>
        window.FromUtc.Offset == TimeSpan.Zero &&
        window.ToUtc.Offset == TimeSpan.Zero &&
        window.ToUtc > window.FromUtc &&
        window.ToUtc - window.FromUtc <= AssetProcessIntelligenceKpiCatalogV1.MaximumWindow;

    private static bool IsWorkspaceRuntimeCoherent(
        EngineeringWorkspaceDescriptor workspace,
        ScadaRuntimeDescriptor runtime,
        out IResult? error)
    {
        if (workspace.IsDirty)
        {
            error = Conflict("INTELLIGENCE_WORKSPACE_HAS_UNSAVED_CHANGES");
            return false;
        }
        if (string.IsNullOrWhiteSpace(workspace.ProjectKey) || !workspace.BaseRevision.HasValue ||
            !string.Equals(runtime.Mode, "engineering", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(runtime.ProjectKey) || !runtime.Revision.HasValue ||
            !string.Equals(workspace.ProjectKey, runtime.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
            workspace.BaseRevision.Value != runtime.Revision.Value)
        {
            error = Conflict("INTELLIGENCE_WORKSPACE_RUNTIME_REVISION_MISMATCH");
            return false;
        }
        error = null;
        return true;
    }

    private static void ValidatePage(
        HistoricalQueryResponse response,
        AssetProcessIntelligenceAnalyticsWindow window,
        Guid expectedTagId,
        int expectedPageSize)
    {
        if (response is null ||
            response.Version != HistoricalQueryContract.Version ||
            !string.Equals(response.Dataset, HistoricalDatasets.HistorianSamples, StringComparison.Ordinal) ||
            response.FromUtc != window.FromUtc || response.ToUtc != window.ToUtc ||
            response.FromUtc.Offset != TimeSpan.Zero || response.ToUtc.Offset != TimeSpan.Zero ||
            response.PageSize != expectedPageSize ||
            response.Rows is null || response.Rows.Count > expectedPageSize)
            throw new InvalidDataException("Historical response metadata does not match the request.");
        foreach (var row in response.Rows)
        {
            if (!row.Cells.TryGetValue("tag.id", out var tagCell) ||
                tagCell.Kind != HistoricalValueKind.Guid ||
                !Guid.TryParse(tagCell.Value, out var returnedTagId) || returnedTagId != expectedTagId)
                throw new InvalidDataException("Historical response contains a different TAG.");
            if (!row.Cells.TryGetValue("timestamp", out var timestamp) ||
                timestamp.Kind != HistoricalValueKind.DateTime ||
                !DateTimeOffset.TryParse(timestamp.Value, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var observedAt) ||
                observedAt.Offset != TimeSpan.Zero || observedAt < window.FromUtc || observedAt > window.ToUtc)
                throw new InvalidDataException("Historical response contains an invalid timestamp.");
        }
    }

    private static AssetProcessIntelligenceRawObservation ToObservation(HistoricalQueryRow row)
    {
        var timestamp = row.Cells["timestamp"].AsDateTime().ToUniversalTime();
        var quality = row.Cells.TryGetValue("quality", out var qualityValue) &&
                      qualityValue.Kind is HistoricalValueKind.Enum or HistoricalValueKind.String
            ? qualityValue.Value
            : null;
        var value = row.Cells.GetValueOrDefault("value");
        return new AssetProcessIntelligenceRawObservation(timestamp, quality, value);
    }

    private static bool IsNumeric(TagDataType type) => type is
        TagDataType.Int16 or TagDataType.Int32 or TagDataType.Int64 or TagDataType.Float or TagDataType.Double;

    private static IResult BadRequest(string code) => Results.BadRequest(
        new AssetProcessIntelligenceAnalyticsApiError(code, "The analytics query is invalid."));

    private static IResult NotFound(string code) => Results.NotFound(
        new AssetProcessIntelligenceAnalyticsApiError(code, "The requested source is not available."));

    private static IResult Forbidden(string code) => Results.Json(
        new AssetProcessIntelligenceAnalyticsApiError(code, "The analytics operation is not authorized."),
        statusCode: StatusCodes.Status403Forbidden);

    private static IResult Conflict(string code) => Results.Conflict(
        new AssetProcessIntelligenceAnalyticsApiError(code, "Analytics requires an unchanged, active and coherent runtime configuration."));

    private static IResult ProfileWindowTooDense() => Results.Json(
        new AssetProcessIntelligenceAnalyticsApiError(
            "INTELLIGENCE_PROFILE_WINDOW_TOO_DENSE",
            "The profile window exceeds the fixed total observation limit; no partial result was returned."),
        statusCode: StatusCodes.Status413PayloadTooLarge);

    private static IResult WindowTooDense(string code) => Results.Json(
        new AssetProcessIntelligenceAnalyticsApiError(
            code,
            "The requested window exceeds the fixed observation limit; no partial result was returned."),
        statusCode: StatusCodes.Status413PayloadTooLarge);

    private enum AncestryResult { No, Yes, Invalid }

    private sealed record PreparedPlan(
        string ProjectKey,
        long Revision,
        long ChangeVersion,
        TagDefinition Tag,
        IReadOnlyList<AssetProcessIntelligenceAssignmentProvenance> Assignments,
        CancellationToken PermitToken,
        AssetProcessIntelligenceScopeKind ScopeKind,
        Guid ScopeId,
        long AuthorityPolicyVersion,
        long HaEpoch);

    private sealed record PreparedPlanResult(PreparedPlan? Plan, IResult? Error)
    {
        public static PreparedPlanResult Ready(PreparedPlan plan) => new(plan, null);
        public static PreparedPlanResult Fail(IResult error) => new(null, error);
    }

    private sealed record ScopeResolution(
        IReadOnlyList<AssetProcessIntelligenceAssignmentProvenance>? Assignments,
        IResult? Error)
    {
        public static ScopeResolution Ready(IReadOnlyList<AssetProcessIntelligenceAssignmentProvenance> assignments) => new(assignments, null);
        public static ScopeResolution Fail(IResult error) => new(null, error);
    }

    private sealed record PeriodReadResult(AssetProcessIntelligenceAnalyticsPeriodResult? Period, IResult? Error)
    {
        public IReadOnlyList<AssetProcessIntelligenceRawObservation>? RawObservations { get; init; }

        public static PeriodReadResult Ready(
            AssetProcessIntelligenceAnalyticsPeriodResult period,
            IReadOnlyList<AssetProcessIntelligenceRawObservation>? observations) =>
            new(period, null) { RawObservations = observations };
        public static PeriodReadResult Fail(IResult error) => new(null, error);
    }

    private sealed record ObservationReadResult(
        IReadOnlyList<AssetProcessIntelligenceRawObservation>? Observations,
        IResult? Error)
    {
        public static ObservationReadResult Ready(IReadOnlyList<AssetProcessIntelligenceRawObservation> observations) =>
            new(observations, null);
        public static ObservationReadResult Fail(IResult error) => new(null, error);
    }

    private sealed record ProfileSignalPlan(
        AssetProcessIntelligenceSignalEngineeringDto Signal,
        TagDefinition Tag,
        IReadOnlyList<AssetProcessIntelligenceAssignmentProvenance> Assignments,
        PreparedPlan Plan);

    private sealed record PreparedProfilePlan(
        string ProjectKey,
        long Revision,
        long ChangeVersion,
        AssetProcessIntelligenceProfileEngineeringDto Profile,
        IReadOnlyList<ProfileSignalPlan> Signals,
        CancellationToken PermitToken);

    private sealed record PreparedProfileCoverageResult(PreparedProfilePlan? Plan, IResult? Error)
    {
        public static PreparedProfileCoverageResult Ready(PreparedProfilePlan plan) => new(plan, null);
        public static PreparedProfileCoverageResult Fail(IResult error) => new(null, error);
    }
}
