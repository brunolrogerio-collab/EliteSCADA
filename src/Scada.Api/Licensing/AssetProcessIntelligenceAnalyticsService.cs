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

    public async Task<IResult> ExecuteCurrentFrequencyComparisonV1Async(
        HttpContext context,
        AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var requestToken = cancellationToken == default ? context.RequestAborted : cancellationToken;
        var invalid = ValidateCurrentFrequencyComparisonRequest(request);
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

            var preparedResult = await PrepareCurrentFrequencyComparisonAsync(context, request, requestToken);
            if (preparedResult.Error is not null) return preparedResult.Error;
            var plan = preparedResult.Plan!;
            var revalidationPlans = new[] { plan.Current.Plan, plan.Frequency.Plan };
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                requestToken,
                plan.PermitToken);
            var budget = new CurrentFrequencyRawRowBudget();

            var currentPrimaryRead = await ReadCurrentFrequencyObservationsAsync(
                context, request.PrimaryWindow!, plan.Current.Plan, revalidationPlans, budget, linkedCancellation.Token);
            if (currentPrimaryRead.Error is not null) return currentPrimaryRead.Error;
            var frequencyPrimaryRead = await ReadCurrentFrequencyObservationsAsync(
                context, request.PrimaryWindow!, plan.Frequency.Plan, revalidationPlans, budget, linkedCancellation.Token);
            if (frequencyPrimaryRead.Error is not null) return frequencyPrimaryRead.Error;
            var currentComparisonRead = await ReadCurrentFrequencyObservationsAsync(
                context, request.ComparisonWindow!, plan.Current.Plan, revalidationPlans, budget, linkedCancellation.Token);
            if (currentComparisonRead.Error is not null) return currentComparisonRead.Error;
            var frequencyComparisonRead = await ReadCurrentFrequencyObservationsAsync(
                context, request.ComparisonWindow!, plan.Frequency.Plan, revalidationPlans, budget, linkedCancellation.Token);
            if (frequencyComparisonRead.Error is not null) return frequencyComparisonRead.Error;

            var currentUnit = plan.Current.Tag.ControlledEngineeringUnit!;
            var frequencyUnit = plan.Frequency.Tag.ControlledEngineeringUnit!;
            var primary = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
                request.PrimaryWindow!,
                currentPrimaryRead.Observations!,
                frequencyPrimaryRead.Observations!,
                currentUnit,
                frequencyUnit,
                request.MinimumFrequencyHz,
                request.MaximumFrequencyHz);
            var comparison = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
                request.ComparisonWindow!,
                currentComparisonRead.Observations!,
                frequencyComparisonRead.Observations!,
                currentUnit,
                frequencyUnit,
                request.MinimumFrequencyHz,
                request.MaximumFrequencyHz);
            var reusedSourceTimestamps = primary.Evidence.Pairs
                .Select(pair => pair.SourceTimestampUtc)
                .Intersect(comparison.Evidence.Pairs.Select(pair => pair.SourceTimestampUtc))
                .ToHashSet();
            if (reusedSourceTimestamps.Count > 0)
            {
                primary = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
                    request.PrimaryWindow!,
                    currentPrimaryRead.Observations!,
                    frequencyPrimaryRead.Observations!,
                    currentUnit,
                    frequencyUnit,
                    request.MinimumFrequencyHz,
                    request.MaximumFrequencyHz,
                    reusedSourceTimestamps);
                comparison = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
                    request.ComparisonWindow!,
                    currentComparisonRead.Observations!,
                    frequencyComparisonRead.Observations!,
                    currentUnit,
                    frequencyUnit,
                    request.MinimumFrequencyHz,
                    request.MaximumFrequencyHz,
                    reusedSourceTimestamps);
            }
            var metric = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateMetric(
                primary,
                comparison,
                reusedSourceTimestamps.Count > 0);

            await using var finalMutation = await workspace.AcquireMutationAsync(
                plan.ChangeVersion,
                linkedCancellation.Token);
            var finalCheck = await RevalidateCurrentFrequencyPlansAsync(
                context,
                revalidationPlans,
                linkedCancellation.Token);
            if (finalCheck is not null) return finalCheck;

            return Results.Ok(new AssetProcessIntelligenceCurrentFrequencyComparisonV1Response(
                AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.ContractVersion,
                AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.CatalogVersion,
                AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.RuleId,
                AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.RuleVersion,
                plan.ProjectKey,
                plan.Revision,
                request.ScopeKind,
                request.ScopeId,
                plan.Assignment.Id,
                plan.Assignment.EquipmentId,
                plan.Assignment.ProfileId,
                plan.Current.Description,
                plan.Frequency.Description,
                request.MinimumFrequencyHz,
                request.MaximumFrequencyHz,
                primary.Evidence,
                comparison.Evidence,
                budget.TotalRows,
                metric,
                new AssetProcessIntelligenceCurrentFrequencyComparisonV1Provenance(
                    HistoricalDatasets.HistorianSamplesV2,
                    2,
                    "Historian `timestamp` observation/publication time bounds provider filtering and paging only.",
                    "exact equality of the two TAGs' `source.timestamp` values after each carries explicit Synchronized evidence; both source timestamps must also lie inside that window.",
                    "TagValue.TimestampEvidenceState.Synchronized is required per row; it is not an acquisition-batch or clock-accuracy guarantee.",
                    plan.Frequency.Binding.FrequencyReference == "drive_electrical"
                        ? "Measured drive electrical output frequency in Hz; this is not shaft rotational speed."
                        : "Measured shaft rotational frequency in Hz; this is not drive electrical output frequency.",
                    "unverified; exact recorded source-time equality does not prove physical simultaneous acquisition.",
                    "unverified; frequency conditioning does not prove equal load or a globally equivalent process regime.",
                    "unknown",
                    "unknown",
                    "Arithmetic sample mean of paired current values in A where exact-paired measured frequency is inside the inclusive Hz range; primary mean minus comparison mean; 100 * difference / comparison mean only when the comparison mean is nonzero and the result is finite.")));
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

    public async Task<IResult> ExecuteContextualReferenceDeviationV1Async(
        HttpContext context,
        AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var requestToken = cancellationToken == default ? context.RequestAborted : cancellationToken;
        var invalid = ValidateContextualReferenceDeviationRequest(request);
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

            var preparedResult = await PrepareContextualReferenceDeviationAsync(context, request, requestToken);
            if (preparedResult.Error is not null) return preparedResult.Error;
            var plan = preparedResult.Plan!;
            var validationPlans = new[] { plan.TargetPlan, plan.ConditionPlan };
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(requestToken, plan.PermitToken);
            var budget = new ContextualReferenceRawRowBudget();

            var referenceTarget = await ReadContextualReferenceObservationsAsync(
                context, request.ReferenceWindow!, plan.TargetPlan, validationPlans, budget, linkedCancellation.Token);
            if (referenceTarget.Error is not null) return referenceTarget.Error;
            var referenceCondition = await ReadContextualReferenceObservationsAsync(
                context, request.ReferenceWindow!, plan.ConditionPlan, validationPlans, budget, linkedCancellation.Token);
            if (referenceCondition.Error is not null) return referenceCondition.Error;
            var evaluationTarget = await ReadContextualReferenceObservationsAsync(
                context, request.EvaluationWindow!, plan.TargetPlan, validationPlans, budget, linkedCancellation.Token);
            if (evaluationTarget.Error is not null) return evaluationTarget.Error;
            var evaluationCondition = await ReadContextualReferenceObservationsAsync(
                context, request.EvaluationWindow!, plan.ConditionPlan, validationPlans, budget, linkedCancellation.Token);
            if (evaluationCondition.Error is not null) return evaluationCondition.Error;

            var calculation = AssetProcessIntelligenceContextualReferenceDeviationV1Calculator.Calculate(
                request.ReferenceWindow!,
                request.EvaluationWindow!,
                referenceTarget.Observations!,
                referenceCondition.Observations!,
                evaluationTarget.Observations!,
                evaluationCondition.Observations!,
                plan.Condition.ControlledEngineeringUnit!,
                plan.ConditionLowerCanonical,
                plan.ConditionUpperCanonical);

            await using var finalMutation = await workspace.AcquireMutationAsync(plan.ChangeVersion, linkedCancellation.Token);
            var finalCheck = await RevalidateContextualReferencePlansAsync(context, validationPlans, linkedCancellation.Token);
            if (finalCheck is not null) return finalCheck;

            return Results.Ok(new AssetProcessIntelligenceContextualReferenceDeviationV1Response(
                AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.ContractVersion,
                AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.CatalogVersion,
                AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.RuleId,
                AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.RuleVersion,
                plan.ProjectKey,
                plan.Revision,
                request.ScopeKind,
                request.ScopeId,
                plan.Assignment.Id,
                plan.Assignment.EquipmentId,
                plan.Profile.Id,
                plan.TargetDescription,
                plan.ConditionDescription,
                request.ReferenceWindow!,
                request.EvaluationWindow!,
                plan.ConditionLowerCanonical,
                plan.ConditionUpperCanonical,
                plan.ConditionUnit.CanonicalUnit,
                calculation.ReferenceEvidence,
                calculation.EvaluationEvidence,
                calculation.ReferenceMedian,
                calculation.ReferenceMad,
                calculation.Status,
                calculation.ReasonCodes,
                budget.TotalRows,
                calculation.EvaluationSamples.Count,
                calculation.OmittedEvaluationSamples,
                calculation.EvaluationSamples,
                new AssetProcessIntelligenceContextualReferenceDeviationV1Provenance(
                    HistoricalDatasets.HistorianSamplesV2,
                    2,
                    "Historian observation timestamps bound query and paging; accepted source timestamps must independently fall inside their declared window.",
                    "exact equality of the target and condition TAG source.timestamp values; no tolerance, nearest-time join, interpolation, or carry-forward.",
                    "Both rows require explicit Synchronized source timestamp evidence; that evidence does not establish physical simultaneity or clock accuracy.",
                    "not_assessed",
                    "unknown",
                    plan.Target.EngineeringUnit ?? "tag engineering unit (as configured)",
                    "Reference median and MAD use exact synchronized target/condition pairs whose canonical condition value is within [lowerInclusive, upperExclusive). Evaluation raw difference is the target value minus the selected reference median. Robust score is 0.67448975 * raw difference / reference MAD only with at least 30 qualified reference pairs and positive MAD.")));
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

    public async Task<IResult> ExecuteContextualDeviationVectorV1Async(
        HttpContext context,
        AssetProcessIntelligenceContextualDeviationVectorV1QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var requestToken = cancellationToken == default ? context.RequestAborted : cancellationToken;
        var invalid = ValidateContextualDeviationVectorRequest(request);
        if (invalid is not null) return invalid;

        try
        {
            var trendAuthorization = await security.CheckRuntimeAsync(
                context, runtime, SecurityCapability.TrendUse, cancellationToken: requestToken);
            var trendFailure = trendAuthorization.FailureResult();
            if (trendFailure is not null) return trendFailure;
            if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
                return Forbidden("INTELLIGENCE_ENTITLEMENT_REQUIRED");

            var preparedResult = await PrepareContextualDeviationVectorAsync(context, request, requestToken);
            if (preparedResult.Error is not null) return preparedResult.Error;
            var plan = preparedResult.Plan!;
            var allPlans = plan.Signals.Select(signal => signal.Plan).ToArray();
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(requestToken, plan.PermitToken);
            var budget = new ContextualDeviationVectorRawRowBudget();
            var referenceRows = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>();
            var evaluationRows = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>();

            foreach (var signal in plan.Signals)
            {
                var read = await ReadContextualDeviationVectorObservationsAsync(context, request.ReferenceWindow!, signal.Plan,
                    allPlans, budget, linkedCancellation.Token);
                if (read.Error is not null) return read.Error;
                referenceRows.Add(signal.Signal.Id, read.Observations!);
            }
            foreach (var signal in plan.Signals)
            {
                var read = await ReadContextualDeviationVectorObservationsAsync(context, request.EvaluationWindow!, signal.Plan,
                    allPlans, budget, linkedCancellation.Token);
                if (read.Error is not null) return read.Error;
                evaluationRows.Add(signal.Signal.Id, read.Observations!);
            }

            var targetSignals = plan.Targets.Select(signal => signal.Description).ToArray();
            var calculation = AssetProcessIntelligenceContextualDeviationVectorV1Calculator.Calculate(
                targetSignals,
                plan.Condition.Description,
                request.ReferenceWindow!,
                request.EvaluationWindow!,
                referenceRows,
                evaluationRows,
                plan.ConditionUnit,
                plan.ConditionLowerCanonical,
                plan.ConditionUpperCanonical);

            await using var finalMutation = await workspace.AcquireMutationAsync(plan.ChangeVersion, linkedCancellation.Token);
            var finalCheck = await RevalidateContextualReferencePlansAsync(context, allPlans, linkedCancellation.Token);
            if (finalCheck is not null) return finalCheck;

            return Results.Ok(new AssetProcessIntelligenceContextualDeviationVectorV1Response(
                AssetProcessIntelligenceContextualDeviationVectorV1Catalog.ContractVersion,
                AssetProcessIntelligenceContextualDeviationVectorV1Catalog.CatalogVersion,
                AssetProcessIntelligenceContextualDeviationVectorV1Catalog.RuleId,
                AssetProcessIntelligenceContextualDeviationVectorV1Catalog.RuleVersion,
                plan.ProjectKey,
                plan.Revision,
                request.ScopeKind,
                request.ScopeId,
                plan.Assignment.Id,
                plan.Assignment.EquipmentId,
                plan.Profile.Id,
                Array.AsReadOnly(targetSignals),
                plan.Condition.Description,
                request.ReferenceWindow!,
                request.EvaluationWindow!,
                plan.ConditionLowerCanonical,
                plan.ConditionUpperCanonical,
                plan.ConditionUnit.CanonicalUnit,
                calculation.ReferenceEvidence,
                calculation.EvaluationEvidence,
                calculation.CompleteReferenceVectorCount,
                calculation.InRangeReferenceVectorCount,
                calculation.CompleteEvaluationVectorCount,
                calculation.EvaluationVectors.Count,
                calculation.OmittedEvaluationVectors,
                calculation.TargetStatistics,
                calculation.EvaluationVectors,
                budget.TotalRows,
                new AssetProcessIntelligenceContextualDeviationVectorV1Provenance(
                    HistoricalDatasets.HistorianSamplesV2,
                    2,
                    "Historian observation timestamps bound each query and page; source timestamps are independently checked against the selected window.",
                    "exact equality of non-null UTC source.timestamp across all selected target and condition TAGs; no nearest join, hold, interpolation, or imputation.",
                    "Every selected row requires Good finite numeric value and explicit Synchronized source timestamp evidence; this does not prove physical simultaneity or clock accuracy.",
                    "lowerInclusive <= canonical condition value < upperExclusive",
                    "not_assessed",
                    "unknown",
                    "Each target is summarized independently against the same complete in-range reference cohort; no combined anomaly classification, diagnosis, fault, or RUL is produced.")));
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

    public async Task<IResult> ExecutePerformanceV1BAsync(
        HttpContext context,
        AssetProcessIntelligencePerformanceV1BQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var requestToken = cancellationToken == default ? context.RequestAborted : cancellationToken;
        var invalid = ValidatePerformanceV1BRequest(request);
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

            var preparedResult = await PreparePerformanceV1BAsync(context, request, requestToken);
            if (preparedResult.Error is not null) return preparedResult.Error;
            var plan = preparedResult.Plan!;
            var allPlans = plan.Signals.Select(signal => signal.Plan).ToArray();
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                requestToken,
                plan.PermitToken);
            var budget = new PerformanceV1BRawRowBudget();
            var observations = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligencePerformanceV1BRawObservation>>();
            foreach (var signal in plan.Signals)
            {
                var read = await ReadPerformanceV1BObservationsAsync(
                    context,
                    request.Window!,
                    signal,
                    allPlans,
                    budget,
                    linkedCancellation.Token);
                if (read.Error is not null) return read.Error;
                observations.Add(signal.Signal.Id, read.Observations!);
            }

            var calculation = AssetProcessIntelligencePerformanceV1BCalculator.Calculate(
                plan.Rule,
                request.Window!,
                plan.Signals,
                observations);

            await using var finalMutation = await workspace.AcquireMutationAsync(
                plan.ChangeVersion,
                linkedCancellation.Token);
            var finalCheck = await RevalidatePerformanceV1BPlansAsync(context, allPlans, linkedCancellation.Token);
            if (finalCheck is not null) return finalCheck;

            return Results.Ok(new AssetProcessIntelligencePerformanceV1BResponse(
                AssetProcessIntelligencePerformanceV1BCatalog.ContractVersion,
                AssetProcessIntelligencePerformanceV1BCatalog.CatalogVersion,
                plan.Rule.Id,
                plan.Rule.Version,
                plan.ProjectKey,
                plan.Revision,
                request.ScopeKind,
                request.ScopeId,
                plan.Assignment.Id,
                plan.Assignment.EquipmentId,
                plan.Profile.Id,
                request.Window!,
                Array.AsReadOnly(plan.Signals.Select(signal => signal.Description).ToArray()),
                calculation.Inputs,
                calculation.Tuples,
                budget.TotalRows,
                calculation.Summary,
                new AssetProcessIntelligencePerformanceV1BProvenance(
                    HistoricalDatasets.HistorianSamplesV2,
                    2,
                    "Exact equality of each selected TAG's non-null source.timestamp with explicit Synchronized evidence; duplicate source-time groups are excluded regardless of quality.",
                    "unverified; source-time equality is not proof of physical co-acquisition or clock accuracy",
                    "unverified",
                    "unknown",
                    "unknown_without_expected_cadence",
                    plan.Rule.Id == "recorded-pressure-power-input-ratio.v1"
                        ? "Pressure-only power divided by directly measured total active electrical input; this is not hydraulic, motor, or overall system efficiency. No velocity/elevation correction or physical simultaneity is assumed."
                        : plan.Rule.Id == "recorded-differential-pressure-power.v1"
                            ? "Flow multiplied by directly measured outlet-minus-inlet differential pressure; pressure-only power, not total pump power or efficiency."
                            : "Recorded active electrical input power divided by recorded net-good production rate; not an integrated-window energy ratio.")));
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

    private async Task<PreparedCurrentFrequencyComparisonResult> PrepareCurrentFrequencyComparisonAsync(
        HttpContext context,
        AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest request,
        CancellationToken cancellationToken)
    {
        var beforeLock = workspace.Describe();
        if (!IsWorkspaceRuntimeCoherent(beforeLock, runtime.Describe(), out var coherenceError))
            return PreparedCurrentFrequencyComparisonResult.Fail(coherenceError!);

        await using var mutation = await workspace.AcquireMutationAsync(beforeLock.ChangeVersion, cancellationToken);
        var descriptor = workspace.Describe();
        if (!IsWorkspaceRuntimeCoherent(descriptor, runtime.Describe(), out coherenceError))
            return PreparedCurrentFrequencyComparisonResult.Fail(coherenceError!);
        if (!highAvailability.CanOwnIndustrialEffects())
            return PreparedCurrentFrequencyComparisonResult.Fail(Conflict("INTELLIGENCE_RUNTIME_NOT_EFFECTIVE_ACTIVE"));
        if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
            return PreparedCurrentFrequencyComparisonResult.Fail(Forbidden("INTELLIGENCE_ENTITLEMENT_REQUIRED"));

        var policyVersion = 0L;
        var haEpoch = 0L;
        if (authorityPolicyHa is not null)
        {
            var authorityFence = await authorityPolicyHa.CheckAsync(cancellationToken);
            if (!authorityFence.Allowed)
                return PreparedCurrentFrequencyComparisonResult.Fail(
                    Conflict(authorityFence.ReasonCode.ToUpperInvariant().Replace('-', '_')));
            policyVersion = authorityFence.PolicyVersion;
            haEpoch = authorityFence.HaEpoch;
            var refreshedTrendAuthorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.TrendUse,
                cancellationToken: cancellationToken);
            var refreshedTrendFailure = refreshedTrendAuthorization.FailureResult();
            if (refreshedTrendFailure is not null)
                return PreparedCurrentFrequencyComparisonResult.Fail(refreshedTrendFailure);
        }

        var configuration = workspace.AssetProcessIntelligence.Snapshot();
        if (configuration.Version != 2)
            return PreparedCurrentFrequencyComparisonResult.Fail(Conflict("INTELLIGENCE_V2_CONFIGURATION_REQUIRED"));
        var scopeMatches = (configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
            .Where(scope => scope.Kind == request.ScopeKind && scope.ScopeId == request.ScopeId)
            .Take(2)
            .ToArray();
        if (scopeMatches.Length != 1 || !scopeMatches[0].Active)
            return PreparedCurrentFrequencyComparisonResult.Fail(Conflict("INTELLIGENCE_SCOPE_NOT_ACTIVE_OR_AMBIGUOUS"));
        if (!executionGate.TryGetToken(
                descriptor.ProjectKey!,
                descriptor.BaseRevision!.Value,
                request.ScopeKind,
                request.ScopeId,
                out var permitToken,
                policyVersion,
                haEpoch))
            return PreparedCurrentFrequencyComparisonResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_REQUIRED"));

        var assignmentMatches = (configuration.Assignments ?? Array.Empty<AssetProcessIntelligenceAssignmentEngineeringDto>())
            .Where(assignment => assignment.Id == request.AssignmentId)
            .Take(2)
            .ToArray();
        if (assignmentMatches.Length != 1)
            return PreparedCurrentFrequencyComparisonResult.Fail(NotFound("INTELLIGENCE_ASSIGNMENT_NOT_AVAILABLE"));
        var assignment = assignmentMatches[0];
        var profileMatches = (configuration.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(profile => profile.Id == assignment.ProfileId)
            .Take(2)
            .ToArray();
        if (profileMatches.Length != 1)
            return PreparedCurrentFrequencyComparisonResult.Fail(Conflict("INTELLIGENCE_PROFILE_NOT_AVAILABLE_OR_AMBIGUOUS"));
        var profile = profileMatches[0];
        var signals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>()).ToArray();
        if (signals.Any(signal => signal is null || signal.Id == Guid.Empty || signal.TagId == Guid.Empty))
            return PreparedCurrentFrequencyComparisonResult.Fail(Conflict("INTELLIGENCE_PROFILE_SIGNAL_IDENTITY_INVALID"));
        var currentMatches = signals.Where(signal => signal.Id == request.CurrentSignalId).Take(2).ToArray();
        var frequencyMatches = signals.Where(signal => signal.Id == request.FrequencySignalId).Take(2).ToArray();
        if (currentMatches.Length != 1 || frequencyMatches.Length != 1 ||
            request.CurrentSignalId == request.FrequencySignalId)
            return PreparedCurrentFrequencyComparisonResult.Fail(BadRequest("INTELLIGENCE_CURRENT_FREQUENCY_SIGNAL_IDENTITY_INVALID"));
        var currentSignal = currentMatches[0];
        var frequencySignal = frequencyMatches[0];
        if (currentSignal.TagId == Guid.Empty || frequencySignal.TagId == Guid.Empty ||
            currentSignal.TagId == frequencySignal.TagId)
            return PreparedCurrentFrequencyComparisonResult.Fail(BadRequest("INTELLIGENCE_CURRENT_FREQUENCY_TAG_IDENTITY_INVALID"));

        var currentResolution = ResolveScopeSources(
            request.ScopeKind, request.ScopeId, currentSignal.TagId, configuration, workspace.Assets);
        if (currentResolution.Error is not null)
            return PreparedCurrentFrequencyComparisonResult.Fail(currentResolution.Error);
        var frequencyResolution = ResolveScopeSources(
            request.ScopeKind, request.ScopeId, frequencySignal.TagId, configuration, workspace.Assets);
        if (frequencyResolution.Error is not null)
            return PreparedCurrentFrequencyComparisonResult.Fail(frequencyResolution.Error);
        var currentAssignment = currentResolution.Assignments!
            .Where(item => item.AssignmentId == assignment.Id && item.ProfileId == profile.Id && item.SignalId == currentSignal.Id)
            .Take(2).ToArray();
        var frequencyAssignment = frequencyResolution.Assignments!
            .Where(item => item.AssignmentId == assignment.Id && item.ProfileId == profile.Id && item.SignalId == frequencySignal.Id)
            .Take(2).ToArray();
        if (currentAssignment.Length != 1 || frequencyAssignment.Length != 1 ||
            currentAssignment[0].EquipmentId != assignment.EquipmentId ||
            frequencyAssignment[0].EquipmentId != assignment.EquipmentId)
            return PreparedCurrentFrequencyComparisonResult.Fail(NotFound("INTELLIGENCE_ASSIGNMENT_NOT_IN_ACTIVE_SCOPE"));

        if (!runtime.TryGetTag(currentSignal.TagId, out var currentTag) || currentTag is null || !IsNumeric(currentTag.DataType) ||
            !runtime.TryGetTag(frequencySignal.TagId, out var frequencyTag) || frequencyTag is null || !IsNumeric(frequencyTag.DataType))
            return PreparedCurrentFrequencyComparisonResult.Fail(NotFound("INTELLIGENCE_ANALYTICS_SOURCE_NOT_AVAILABLE"));

        var currentBinding = currentSignal.SemanticBinding;
        var frequencyBinding = frequencySignal.SemanticBinding;
        if (AssetProcessIntelligenceSemanticCatalog.ValidateBinding(currentBinding, currentSignal.Id).Count != 0 ||
            currentBinding is null || currentBinding.RoleCode != "pump.electrical_current" ||
            string.IsNullOrWhiteSpace(currentBinding.CurrentBasis) ||
            AssetProcessIntelligenceSemanticCatalog.ValidateBinding(frequencyBinding, frequencySignal.Id).Count != 0 ||
            frequencyBinding is null || frequencyBinding.RoleCode != "pump.drive_frequency" ||
            frequencyBinding.SignalKind != "measured_output" ||
            string.IsNullOrWhiteSpace(frequencyBinding.FrequencyReference))
            return PreparedCurrentFrequencyComparisonResult.Fail(BadRequest("INTELLIGENCE_CURRENT_FREQUENCY_SEMANTICS_UNSUPPORTED"));

        var currentUnit = currentTag.ControlledEngineeringUnit;
        var frequencyUnit = frequencyTag.ControlledEngineeringUnit;
        if (currentUnit is null || currentUnit.CatalogVersion != ControlledEngineeringUnitCatalog.CurrentCatalogVersion ||
            currentUnit.Code != "unit.electric_current.ampere" ||
            !ControlledEngineeringUnitCatalog.TryResolve(currentUnit, out var currentDefinition) ||
            currentDefinition.Dimension != "electric_current" ||
            frequencyUnit is null || frequencyUnit.CatalogVersion != ControlledEngineeringUnitCatalog.CurrentCatalogVersion ||
            frequencyUnit.Code != "unit.frequency.hertz" ||
            !ControlledEngineeringUnitCatalog.TryResolve(frequencyUnit, out var frequencyDefinition) ||
            frequencyDefinition.Dimension != "frequency")
            return PreparedCurrentFrequencyComparisonResult.Fail(BadRequest("INTELLIGENCE_CURRENT_FREQUENCY_UNITS_UNSUPPORTED"));

        var currentAuthorization = await security.CheckRuntimeTagAsync(
            context, runtime, currentTag, TagAccessOperation.Read, cancellationToken);
        var currentFailure = currentAuthorization.FailureResult();
        if (currentFailure is not null)
            return PreparedCurrentFrequencyComparisonResult.Fail(currentFailure);
        var frequencyAuthorization = await security.CheckRuntimeTagAsync(
            context, runtime, frequencyTag, TagAccessOperation.Read, cancellationToken);
        var frequencyFailure = frequencyAuthorization.FailureResult();
        if (frequencyFailure is not null)
            return PreparedCurrentFrequencyComparisonResult.Fail(frequencyFailure);
        if (permitToken.IsCancellationRequested)
            return PreparedCurrentFrequencyComparisonResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_CANCELLED"));

        var currentPlan = new PreparedPlan(
            descriptor.ProjectKey!, descriptor.BaseRevision!.Value, descriptor.ChangeVersion,
            currentTag, Array.AsReadOnly(currentAssignment), permitToken, request.ScopeKind, request.ScopeId,
            policyVersion, haEpoch);
        var frequencyPlan = new PreparedPlan(
            descriptor.ProjectKey!, descriptor.BaseRevision!.Value, descriptor.ChangeVersion,
            frequencyTag, Array.AsReadOnly(frequencyAssignment), permitToken, request.ScopeKind, request.ScopeId,
            policyVersion, haEpoch);
        var currentDescription = DescribeCurrentFrequencySignal(currentSignal, currentTag, currentBinding, currentDefinition);
        var frequencyDescription = DescribeCurrentFrequencySignal(frequencySignal, frequencyTag, frequencyBinding, frequencyDefinition);
        return PreparedCurrentFrequencyComparisonResult.Ready(new PreparedCurrentFrequencyComparisonPlan(
            descriptor.ProjectKey!, descriptor.BaseRevision!.Value, descriptor.ChangeVersion,
            assignment, profile,
            new PreparedCurrentFrequencySignal(currentSignal, currentTag, currentBinding, currentDefinition, currentPlan, currentDescription),
            new PreparedCurrentFrequencySignal(frequencySignal, frequencyTag, frequencyBinding, frequencyDefinition, frequencyPlan, frequencyDescription),
            permitToken));
    }

    private async Task<PreparedContextualDeviationVectorPlanResult> PrepareContextualDeviationVectorAsync(
        HttpContext context,
        AssetProcessIntelligenceContextualDeviationVectorV1QueryRequest request,
        CancellationToken cancellationToken)
    {
        var beforeLock = workspace.Describe();
        if (!IsWorkspaceRuntimeCoherent(beforeLock, runtime.Describe(), out var coherenceError))
            return PreparedContextualDeviationVectorPlanResult.Fail(coherenceError!);

        await using var mutation = await workspace.AcquireMutationAsync(beforeLock.ChangeVersion, cancellationToken);
        var descriptor = workspace.Describe();
        if (!IsWorkspaceRuntimeCoherent(descriptor, runtime.Describe(), out coherenceError))
            return PreparedContextualDeviationVectorPlanResult.Fail(coherenceError!);
        if (!highAvailability.CanOwnIndustrialEffects())
            return PreparedContextualDeviationVectorPlanResult.Fail(Conflict("INTELLIGENCE_RUNTIME_NOT_EFFECTIVE_ACTIVE"));
        if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
            return PreparedContextualDeviationVectorPlanResult.Fail(Forbidden("INTELLIGENCE_ENTITLEMENT_REQUIRED"));

        var policyVersion = 0L;
        var haEpoch = 0L;
        if (authorityPolicyHa is not null)
        {
            var authorityFence = await authorityPolicyHa.CheckAsync(cancellationToken);
            if (!authorityFence.Allowed)
                return PreparedContextualDeviationVectorPlanResult.Fail(
                    Conflict(authorityFence.ReasonCode.ToUpperInvariant().Replace('-', '_')));
            policyVersion = authorityFence.PolicyVersion;
            haEpoch = authorityFence.HaEpoch;
            var refreshedTrendAuthorization = await security.CheckRuntimeAsync(
                context, runtime, SecurityCapability.TrendUse, cancellationToken: cancellationToken);
            var refreshedTrendFailure = refreshedTrendAuthorization.FailureResult();
            if (refreshedTrendFailure is not null)
                return PreparedContextualDeviationVectorPlanResult.Fail(refreshedTrendFailure);
        }

        var configuration = workspace.AssetProcessIntelligence.Snapshot();
        if (configuration.Version != 2)
            return PreparedContextualDeviationVectorPlanResult.Fail(Conflict("INTELLIGENCE_V2_CONFIGURATION_REQUIRED"));
        var scopes = (configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
            .Where(scope => scope.Kind == request.ScopeKind && scope.ScopeId == request.ScopeId).Take(2).ToArray();
        if (scopes.Length != 1 || !scopes[0].Active)
            return PreparedContextualDeviationVectorPlanResult.Fail(Conflict("INTELLIGENCE_SCOPE_NOT_ACTIVE_OR_AMBIGUOUS"));
        if (!executionGate.TryGetToken(descriptor.ProjectKey!, descriptor.BaseRevision!.Value,
                request.ScopeKind, request.ScopeId, out var permitToken, policyVersion, haEpoch))
            return PreparedContextualDeviationVectorPlanResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_REQUIRED"));

        var assignments = (configuration.Assignments ?? Array.Empty<AssetProcessIntelligenceAssignmentEngineeringDto>())
            .Where(item => item.Id == request.AssignmentId).Take(2).ToArray();
        if (assignments.Length != 1)
            return PreparedContextualDeviationVectorPlanResult.Fail(NotFound("INTELLIGENCE_ASSIGNMENT_NOT_AVAILABLE"));
        var assignment = assignments[0];
        var profiles = (configuration.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(item => item.Id == assignment.ProfileId).Take(2).ToArray();
        if (profiles.Length != 1)
            return PreparedContextualDeviationVectorPlanResult.Fail(Conflict("INTELLIGENCE_PROFILE_NOT_AVAILABLE_OR_AMBIGUOUS"));
        var profile = profiles[0];
        var signals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>()).ToArray();
        if (signals.Any(signal => signal is null || signal.Id == Guid.Empty || signal.TagId == Guid.Empty))
            return PreparedContextualDeviationVectorPlanResult.Fail(Conflict("INTELLIGENCE_PROFILE_SIGNAL_IDENTITY_INVALID"));

        var allIds = request.TargetSignalIds!.Append(request.ConditionSignalId).ToArray();
        var selectedSignals = new List<AssetProcessIntelligenceSignalEngineeringDto>(allIds.Length);
        foreach (var signalId in allIds)
        {
            var matches = signals.Where(signal => signal.Id == signalId).Take(2).ToArray();
            if (matches.Length != 1)
                return PreparedContextualDeviationVectorPlanResult.Fail(
                    BadRequest("INTELLIGENCE_CONTEXTUAL_DEVIATION_VECTOR_SIGNAL_IDENTITY_INVALID"));
            selectedSignals.Add(matches[0]);
        }
        if (selectedSignals.Select(signal => signal.TagId).Distinct().Count() != selectedSignals.Count)
            return PreparedContextualDeviationVectorPlanResult.Fail(
                BadRequest("INTELLIGENCE_CONTEXTUAL_DEVIATION_VECTOR_TAG_IDENTITY_INVALID"));

        var descriptions = new List<PreparedContextualDeviationVectorSignal>(selectedSignals.Count);
        foreach (var signal in selectedSignals)
        {
            var resolution = ResolveScopeSources(request.ScopeKind, request.ScopeId, signal.TagId, configuration, workspace.Assets);
            if (resolution.Error is not null) return PreparedContextualDeviationVectorPlanResult.Fail(resolution.Error);
            var provenance = resolution.Assignments!
                .Where(item => item.AssignmentId == assignment.Id && item.ProfileId == profile.Id && item.SignalId == signal.Id)
                .Take(2).ToArray();
            if (provenance.Length != 1 || provenance[0].EquipmentId != assignment.EquipmentId)
                return PreparedContextualDeviationVectorPlanResult.Fail(NotFound("INTELLIGENCE_ASSIGNMENT_NOT_IN_ACTIVE_SCOPE"));
            if (!runtime.TryGetTag(signal.TagId, out var tag) || tag is null || !IsNumeric(tag.DataType))
                return PreparedContextualDeviationVectorPlanResult.Fail(NotFound("INTELLIGENCE_ANALYTICS_SOURCE_NOT_AVAILABLE"));
            if (permitToken.IsCancellationRequested)
                return PreparedContextualDeviationVectorPlanResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_CANCELLED"));
            var authorization = await security.CheckRuntimeTagAsync(context, runtime, tag, TagAccessOperation.Read, cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null) return PreparedContextualDeviationVectorPlanResult.Fail(failure);

            var tagUnit = ControlledEngineeringSemanticsResolver.TryResolveUnit(tag.ControlledEngineeringUnit, out var unit)
                ? unit
                : null;
            var plan = new PreparedPlan(descriptor.ProjectKey!, descriptor.BaseRevision!.Value, descriptor.ChangeVersion,
                tag, Array.AsReadOnly(provenance), permitToken, request.ScopeKind, request.ScopeId, policyVersion, haEpoch);
            var description = new AssetProcessIntelligenceContextualDeviationVectorV1Signal(
                signal.Id, signal.Key, tag.Id, tag.Path, tag.EngineeringUnit,
                tag.ControlledEngineeringUnit?.Code, tagUnit?.CanonicalUnit);
            descriptions.Add(new(signal, tag, plan, description));
        }

        var condition = descriptions[^1];
        if (!ControlledEngineeringSemanticsResolver.TryResolveUnit(condition.Tag.ControlledEngineeringUnit, out var conditionUnit))
            return PreparedContextualDeviationVectorPlanResult.Fail(
                BadRequest("INTELLIGENCE_CONTEXTUAL_DEVIATION_VECTOR_CONDITION_UNIT_UNSUPPORTED"));
        if (permitToken.IsCancellationRequested)
            return PreparedContextualDeviationVectorPlanResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_CANCELLED"));

        return PreparedContextualDeviationVectorPlanResult.Ready(new PreparedContextualDeviationVectorPlan(
            descriptor.ProjectKey!, descriptor.BaseRevision!.Value, descriptor.ChangeVersion,
            assignment, profile, Array.AsReadOnly(descriptions.Take(descriptions.Count - 1).ToArray()),
            condition, conditionUnit, request.ConditionLowerInclusiveCanonical,
            request.ConditionUpperExclusiveCanonical, permitToken));
    }

    private async Task<ContextualDeviationVectorObservationReadResult> ReadContextualDeviationVectorObservationsAsync(
        HttpContext context,
        AssetProcessIntelligenceAnalyticsWindow window,
        PreparedPlan tagPlan,
        IReadOnlyList<PreparedPlan> allPlans,
        ContextualDeviationVectorRawRowBudget budget,
        CancellationToken cancellationToken)
    {
        var observations = new List<AssetProcessIntelligenceContextualDeviationVectorRawObservation>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        var pageCount = 0;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++pageCount > AssetProcessIntelligenceContextualDeviationVectorV1Catalog.MaximumRawRowsAcrossQuery /
                AssetProcessIntelligenceContextualDeviationVectorV1Catalog.QueryPageSize + 2)
                throw new InvalidDataException("Historian pagination exceeded the bounded contextual-vector page count.");

            await using var mutation = await workspace.AcquireMutationAsync(tagPlan.ChangeVersion, cancellationToken);
            var validation = await RevalidateContextualReferencePlansAsync(context, allPlans, cancellationToken);
            if (validation is not null) return ContextualDeviationVectorObservationReadResult.Fail(validation);
            var remaining = AssetProcessIntelligenceContextualDeviationVectorV1Catalog.MaximumRawRowsAcrossQuery - budget.TotalRows;
            // At the exact aggregate limit, issue a one-row probe so an empty remaining TAG/window is still valid;
            // any additional observation or cursor proves truncation and causes an all-or-nothing 413.
            var pageSize = Math.Max(1, Math.Min(AssetProcessIntelligenceContextualDeviationVectorV1Catalog.QueryPageSize, remaining));
            var query = new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamplesV2,
                HistoricalTimeRange.Absolute(window.FromUtc, window.ToUtc),
                Version: 2,
                Filters: [new HistoricalFilter("tag.id", HistoricalFilterOperator.Eq, [HistoricalQueryValue.FromGuid(tagPlan.Tag.Id)])],
                OrderBy: [new HistoricalSort("timestamp", HistoricalSortDirection.Ascending)],
                Page: new HistoricalPageRequest(pageSize, cursor));
            var response = await historicalQueries.QueryAsync(query, cancellationToken);
            ValidateCurrentFrequencyPage(response, window, tagPlan.Tag.Id, pageSize);
            validation = await RevalidateContextualReferencePlansAsync(context, allPlans, cancellationToken);
            if (validation is not null) return ContextualDeviationVectorObservationReadResult.Fail(validation);
            var projected = budget.TotalRows + response.Rows.Count;
            if (projected > AssetProcessIntelligenceContextualDeviationVectorV1Catalog.MaximumRawRowsAcrossQuery ||
                (projected == AssetProcessIntelligenceContextualDeviationVectorV1Catalog.MaximumRawRowsAcrossQuery && response.NextCursor is not null))
                return ContextualDeviationVectorObservationReadResult.Fail(
                    WindowTooDense("INTELLIGENCE_CONTEXTUAL_DEVIATION_VECTOR_WINDOW_TOO_DENSE"));
            foreach (var row in response.Rows)
            {
                var observation = ToContextualReferenceObservation(row);
                observations.Add(new AssetProcessIntelligenceContextualDeviationVectorRawObservation(
                    observation.ObservedTimestampUtc, observation.Quality, observation.Value,
                    observation.SourceTimestampUtc, observation.SourceTimestampEvidence));
            }
            budget.TotalRows = projected;
            cursor = response.NextCursor;
            if (cursor is not null && !cursors.Add(cursor))
                throw new InvalidDataException("Historian pagination cursor repeated.");
        }
        while (cursor is not null);
        return ContextualDeviationVectorObservationReadResult.Ready(observations.AsReadOnly());
    }

    private async Task<PreparedContextualReferencePlanResult> PrepareContextualReferenceDeviationAsync(
        HttpContext context,
        AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest request,
        CancellationToken cancellationToken)
    {
        var beforeLock = workspace.Describe();
        if (!IsWorkspaceRuntimeCoherent(beforeLock, runtime.Describe(), out var coherenceError))
            return PreparedContextualReferencePlanResult.Fail(coherenceError!);

        await using var mutation = await workspace.AcquireMutationAsync(beforeLock.ChangeVersion, cancellationToken);
        var descriptor = workspace.Describe();
        if (!IsWorkspaceRuntimeCoherent(descriptor, runtime.Describe(), out coherenceError))
            return PreparedContextualReferencePlanResult.Fail(coherenceError!);
        if (!highAvailability.CanOwnIndustrialEffects())
            return PreparedContextualReferencePlanResult.Fail(Conflict("INTELLIGENCE_RUNTIME_NOT_EFFECTIVE_ACTIVE"));
        if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
            return PreparedContextualReferencePlanResult.Fail(Forbidden("INTELLIGENCE_ENTITLEMENT_REQUIRED"));

        var policyVersion = 0L;
        var haEpoch = 0L;
        if (authorityPolicyHa is not null)
        {
            var authorityFence = await authorityPolicyHa.CheckAsync(cancellationToken);
            if (!authorityFence.Allowed)
                return PreparedContextualReferencePlanResult.Fail(
                    Conflict(authorityFence.ReasonCode.ToUpperInvariant().Replace('-', '_')));
            policyVersion = authorityFence.PolicyVersion;
            haEpoch = authorityFence.HaEpoch;
            var refreshedTrendAuthorization = await security.CheckRuntimeAsync(
                context, runtime, SecurityCapability.TrendUse, cancellationToken: cancellationToken);
            var refreshedTrendFailure = refreshedTrendAuthorization.FailureResult();
            if (refreshedTrendFailure is not null)
                return PreparedContextualReferencePlanResult.Fail(refreshedTrendFailure);
        }

        var configuration = workspace.AssetProcessIntelligence.Snapshot();
        if (configuration.Version != 2)
            return PreparedContextualReferencePlanResult.Fail(Conflict("INTELLIGENCE_V2_CONFIGURATION_REQUIRED"));
        var scopes = (configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
            .Where(scope => scope.Kind == request.ScopeKind && scope.ScopeId == request.ScopeId).Take(2).ToArray();
        if (scopes.Length != 1 || !scopes[0].Active)
            return PreparedContextualReferencePlanResult.Fail(Conflict("INTELLIGENCE_SCOPE_NOT_ACTIVE_OR_AMBIGUOUS"));
        if (!executionGate.TryGetToken(descriptor.ProjectKey!, descriptor.BaseRevision!.Value,
                request.ScopeKind, request.ScopeId, out var permitToken, policyVersion, haEpoch))
            return PreparedContextualReferencePlanResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_REQUIRED"));

        var assignments = (configuration.Assignments ?? Array.Empty<AssetProcessIntelligenceAssignmentEngineeringDto>())
            .Where(item => item.Id == request.AssignmentId).Take(2).ToArray();
        if (assignments.Length != 1)
            return PreparedContextualReferencePlanResult.Fail(NotFound("INTELLIGENCE_ASSIGNMENT_NOT_AVAILABLE"));
        var assignment = assignments[0];
        var profiles = (configuration.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(item => item.Id == assignment.ProfileId).Take(2).ToArray();
        if (profiles.Length != 1)
            return PreparedContextualReferencePlanResult.Fail(Conflict("INTELLIGENCE_PROFILE_NOT_AVAILABLE_OR_AMBIGUOUS"));
        var profile = profiles[0];
        var signals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>()).ToArray();
        if (signals.Any(signal => signal is null || signal.Id == Guid.Empty || signal.TagId == Guid.Empty))
            return PreparedContextualReferencePlanResult.Fail(Conflict("INTELLIGENCE_PROFILE_SIGNAL_IDENTITY_INVALID"));
        var targets = signals.Where(signal => signal.Id == request.TargetSignalId).Take(2).ToArray();
        var conditions = signals.Where(signal => signal.Id == request.ConditionSignalId).Take(2).ToArray();
        if (targets.Length != 1 || conditions.Length != 1 || request.TargetSignalId == request.ConditionSignalId ||
            targets[0].TagId == conditions[0].TagId)
            return PreparedContextualReferencePlanResult.Fail(BadRequest("INTELLIGENCE_CONTEXTUAL_REFERENCE_SIGNAL_IDENTITY_INVALID"));
        var targetSignal = targets[0];
        var conditionSignal = conditions[0];

        var targetResolution = ResolveScopeSources(request.ScopeKind, request.ScopeId, targetSignal.TagId, configuration, workspace.Assets);
        if (targetResolution.Error is not null) return PreparedContextualReferencePlanResult.Fail(targetResolution.Error);
        var conditionResolution = ResolveScopeSources(request.ScopeKind, request.ScopeId, conditionSignal.TagId, configuration, workspace.Assets);
        if (conditionResolution.Error is not null) return PreparedContextualReferencePlanResult.Fail(conditionResolution.Error);
        var targetAssignment = targetResolution.Assignments!
            .Where(item => item.AssignmentId == assignment.Id && item.ProfileId == profile.Id && item.SignalId == targetSignal.Id)
            .Take(2).ToArray();
        var conditionAssignment = conditionResolution.Assignments!
            .Where(item => item.AssignmentId == assignment.Id && item.ProfileId == profile.Id && item.SignalId == conditionSignal.Id)
            .Take(2).ToArray();
        if (targetAssignment.Length != 1 || conditionAssignment.Length != 1 ||
            targetAssignment[0].EquipmentId != assignment.EquipmentId || conditionAssignment[0].EquipmentId != assignment.EquipmentId)
            return PreparedContextualReferencePlanResult.Fail(NotFound("INTELLIGENCE_ASSIGNMENT_NOT_IN_ACTIVE_SCOPE"));
        if (!runtime.TryGetTag(targetSignal.TagId, out var targetTag) || targetTag is null || !IsNumeric(targetTag.DataType) ||
            !runtime.TryGetTag(conditionSignal.TagId, out var conditionTag) || conditionTag is null || !IsNumeric(conditionTag.DataType))
            return PreparedContextualReferencePlanResult.Fail(NotFound("INTELLIGENCE_ANALYTICS_SOURCE_NOT_AVAILABLE"));
        if (!ControlledEngineeringSemanticsResolver.TryResolveUnit(conditionTag.ControlledEngineeringUnit, out var conditionUnit))
            return PreparedContextualReferencePlanResult.Fail(BadRequest("INTELLIGENCE_CONTEXTUAL_REFERENCE_CONDITION_UNIT_UNSUPPORTED"));
        var lowerCanonical = request.ConditionLowerInclusiveCanonical;
        var upperCanonical = request.ConditionUpperExclusiveCanonical;

        var targetAuthorization = await security.CheckRuntimeTagAsync(context, runtime, targetTag, TagAccessOperation.Read, cancellationToken);
        var targetFailure = targetAuthorization.FailureResult();
        if (targetFailure is not null) return PreparedContextualReferencePlanResult.Fail(targetFailure);
        var conditionAuthorization = await security.CheckRuntimeTagAsync(context, runtime, conditionTag, TagAccessOperation.Read, cancellationToken);
        var conditionFailure = conditionAuthorization.FailureResult();
        if (conditionFailure is not null) return PreparedContextualReferencePlanResult.Fail(conditionFailure);
        if (permitToken.IsCancellationRequested)
            return PreparedContextualReferencePlanResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_CANCELLED"));

        var targetPlan = new PreparedPlan(descriptor.ProjectKey!, descriptor.BaseRevision!.Value, descriptor.ChangeVersion,
            targetTag, Array.AsReadOnly(targetAssignment), permitToken, request.ScopeKind, request.ScopeId, policyVersion, haEpoch);
        var conditionPlan = new PreparedPlan(descriptor.ProjectKey!, descriptor.BaseRevision!.Value, descriptor.ChangeVersion,
            conditionTag, Array.AsReadOnly(conditionAssignment), permitToken, request.ScopeKind, request.ScopeId, policyVersion, haEpoch);
        var targetUnit = ControlledEngineeringSemanticsResolver.TryResolveUnit(targetTag.ControlledEngineeringUnit, out var targetDefinition)
            ? targetDefinition
            : null;
        var targetDescription = DescribeContextualReferenceSignal(targetSignal, targetTag, targetUnit);
        var conditionDescription = DescribeContextualReferenceSignal(conditionSignal, conditionTag, conditionUnit);
        return PreparedContextualReferencePlanResult.Ready(new PreparedContextualReferencePlan(
            descriptor.ProjectKey!, descriptor.BaseRevision!.Value, descriptor.ChangeVersion, assignment, profile,
            targetTag, conditionTag, targetPlan, conditionPlan, targetDescription, conditionDescription, conditionUnit,
            lowerCanonical, upperCanonical, permitToken));
    }

    private static AssetProcessIntelligenceContextualReferenceDeviationV1Signal DescribeContextualReferenceSignal(
        AssetProcessIntelligenceSignalEngineeringDto signal,
        TagDefinition tag,
        ControlledEngineeringUnitDefinition? unit) => new(
        signal.Id, signal.Key, tag.Id, tag.Path, tag.DataType, tag.EngineeringUnit,
        tag.ControlledEngineeringUnit?.Code, unit?.CanonicalUnit);

    private async Task<ContextualReferenceObservationReadResult> ReadContextualReferenceObservationsAsync(
        HttpContext context,
        AssetProcessIntelligenceAnalyticsWindow window,
        PreparedPlan tagPlan,
        IReadOnlyList<PreparedPlan> allPlans,
        ContextualReferenceRawRowBudget budget,
        CancellationToken cancellationToken)
    {
        var observations = new List<AssetProcessIntelligenceContextualReferenceRawObservation>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        var pageCount = 0;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (budget.TotalRows >= AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.MaximumRawRowsAcrossQuery)
                return ContextualReferenceObservationReadResult.Fail(WindowTooDense("INTELLIGENCE_CONTEXTUAL_REFERENCE_WINDOW_TOO_DENSE"));
            if (++pageCount > AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.MaximumRawRowsAcrossQuery /
                AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.QueryPageSize + 1)
                throw new InvalidDataException("Historian pagination exceeded the bounded contextual-reference page count.");

            await using var mutation = await workspace.AcquireMutationAsync(tagPlan.ChangeVersion, cancellationToken);
            var validation = await RevalidateContextualReferencePlansAsync(context, allPlans, cancellationToken);
            if (validation is not null) return ContextualReferenceObservationReadResult.Fail(validation);
            var remaining = AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.MaximumRawRowsAcrossQuery - budget.TotalRows;
            var pageSize = Math.Min(AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.QueryPageSize, remaining);
            var query = new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamplesV2,
                HistoricalTimeRange.Absolute(window.FromUtc, window.ToUtc),
                Version: 2,
                Filters: [new HistoricalFilter("tag.id", HistoricalFilterOperator.Eq, [HistoricalQueryValue.FromGuid(tagPlan.Tag.Id)])],
                OrderBy: [new HistoricalSort("timestamp", HistoricalSortDirection.Ascending)],
                Page: new HistoricalPageRequest(pageSize, cursor));
            var response = await historicalQueries.QueryAsync(query, cancellationToken);
            ValidateCurrentFrequencyPage(response, window, tagPlan.Tag.Id, pageSize);
            validation = await RevalidateContextualReferencePlansAsync(context, allPlans, cancellationToken);
            if (validation is not null) return ContextualReferenceObservationReadResult.Fail(validation);
            var projected = budget.TotalRows + response.Rows.Count;
            if (projected > AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.MaximumRawRowsAcrossQuery ||
                (projected == AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.MaximumRawRowsAcrossQuery && response.NextCursor is not null))
                return ContextualReferenceObservationReadResult.Fail(WindowTooDense("INTELLIGENCE_CONTEXTUAL_REFERENCE_WINDOW_TOO_DENSE"));
            foreach (var row in response.Rows) observations.Add(ToContextualReferenceObservation(row));
            budget.TotalRows = projected;
            cursor = response.NextCursor;
            if (cursor is not null && !cursors.Add(cursor))
                throw new InvalidDataException("Historian pagination cursor repeated.");
        }
        while (cursor is not null);
        return ContextualReferenceObservationReadResult.Ready(observations.AsReadOnly());
    }

    private async Task<IResult?> RevalidateContextualReferencePlansAsync(
        HttpContext context,
        IReadOnlyList<PreparedPlan> plans,
        CancellationToken cancellationToken)
    {
        foreach (var plan in plans)
        {
            var validation = await RevalidateAsync(context, plan, cancellationToken);
            if (validation is not null) return validation;
            if (!runtime.TryGetTag(plan.Tag.Id, out var activeTag) || activeTag is null ||
                !string.Equals(activeTag.EngineeringUnit, plan.Tag.EngineeringUnit, StringComparison.Ordinal) ||
                activeTag.ControlledEngineeringUnit != plan.Tag.ControlledEngineeringUnit)
                return Conflict("INTELLIGENCE_ACTIVE_TAG_SEMANTICS_CHANGED");
        }
        return null;
    }

    private static AssetProcessIntelligenceCurrentFrequencyComparisonV1Signal DescribeCurrentFrequencySignal(
        AssetProcessIntelligenceSignalEngineeringDto signal,
        TagDefinition tag,
        AssetProcessIntelligenceSignalSemanticBindingEngineeringDto binding,
        ControlledEngineeringUnitDefinition unit) =>
        new(signal.Id, signal.Key, tag.Id, tag.Path, tag.DataType, binding.RoleCode,
            binding.CurrentBasis, binding.SignalKind, binding.FrequencyReference,
            tag.EngineeringUnit, tag.ControlledEngineeringUnit!.Code, unit.CanonicalUnit);

    private async Task<CurrentFrequencyObservationReadResult> ReadCurrentFrequencyObservationsAsync(
        HttpContext context,
        AssetProcessIntelligenceAnalyticsWindow window,
        PreparedPlan tagPlan,
        IReadOnlyList<PreparedPlan> allPlans,
        CurrentFrequencyRawRowBudget budget,
        CancellationToken cancellationToken)
    {
        var observations = new List<AssetProcessIntelligenceCurrentFrequencyRawObservation>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        var pageCount = 0;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (budget.TotalRows >= AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.MaximumRawRowsAcrossQuery)
                return CurrentFrequencyObservationReadResult.Fail(WindowTooDense("INTELLIGENCE_CURRENT_FREQUENCY_WINDOW_TOO_DENSE"));
            if (++pageCount > AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.MaximumRawRowsAcrossQuery /
                AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.QueryPageSize + 1)
                throw new InvalidDataException("Historian pagination exceeded the bounded page count.");

            await using var mutation = await workspace.AcquireMutationAsync(tagPlan.ChangeVersion, cancellationToken);
            var validation = await RevalidateCurrentFrequencyPlansAsync(context, allPlans, cancellationToken);
            if (validation is not null) return CurrentFrequencyObservationReadResult.Fail(validation);

            var remainingRows = AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.MaximumRawRowsAcrossQuery - budget.TotalRows;
            var pageSize = Math.Min(AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.QueryPageSize, remainingRows);
            var request = new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamplesV2,
                HistoricalTimeRange.Absolute(window.FromUtc, window.ToUtc),
                Version: 2,
                Filters: [new HistoricalFilter("tag.id", HistoricalFilterOperator.Eq,
                    [HistoricalQueryValue.FromGuid(tagPlan.Tag.Id)])],
                OrderBy: [new HistoricalSort("timestamp", HistoricalSortDirection.Ascending)],
                Page: new HistoricalPageRequest(pageSize, cursor));
            var response = await historicalQueries.QueryAsync(request, cancellationToken);
            ValidateCurrentFrequencyPage(response, window, tagPlan.Tag.Id, pageSize);
            validation = await RevalidateCurrentFrequencyPlansAsync(context, allPlans, cancellationToken);
            if (validation is not null) return CurrentFrequencyObservationReadResult.Fail(validation);
            if (budget.TotalRows + response.Rows.Count > AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.MaximumRawRowsAcrossQuery ||
                (budget.TotalRows + response.Rows.Count == AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.MaximumRawRowsAcrossQuery &&
                 response.NextCursor is not null))
                return CurrentFrequencyObservationReadResult.Fail(WindowTooDense("INTELLIGENCE_CURRENT_FREQUENCY_WINDOW_TOO_DENSE"));

            foreach (var row in response.Rows)
                observations.Add(ToCurrentFrequencyObservation(row));
            budget.TotalRows += response.Rows.Count;

            cursor = response.NextCursor;
            if (cursor is not null && !cursors.Add(cursor))
                throw new InvalidDataException("Historian pagination cursor repeated.");
        }
        while (cursor is not null);

        return CurrentFrequencyObservationReadResult.Ready(observations.AsReadOnly());
    }

    private async Task<IResult?> RevalidateCurrentFrequencyPlansAsync(
        HttpContext context,
        IReadOnlyList<PreparedPlan> plans,
        CancellationToken cancellationToken)
    {
        foreach (var plan in plans)
        {
            var validation = await RevalidateAsync(context, plan, cancellationToken);
            if (validation is not null) return validation;
        }
        return null;
    }

    private async Task<PreparedPerformanceV1BResult> PreparePerformanceV1BAsync(
        HttpContext context,
        AssetProcessIntelligencePerformanceV1BQueryRequest request,
        CancellationToken cancellationToken)
    {
        var rule = AssetProcessIntelligencePerformanceV1BCatalog.FindRule(request.RuleId);
        if (rule is null)
            return PreparedPerformanceV1BResult.Fail(BadRequest("INTELLIGENCE_PERFORMANCE_RULE_UNSUPPORTED"));
        var expectedRoles = ExpectedPerformanceRoles(rule.Id);
        var requestedIds = request.SignalIds!;
        var expectedSignalCount = rule.Id == "specific-active-energy-at-recorded-production-rate.v1"
            ? 2
            : expectedRoles.Count;
        if (requestedIds.Count != expectedSignalCount || requestedIds.Distinct().Count() != requestedIds.Count)
            return PreparedPerformanceV1BResult.Fail(BadRequest("INTELLIGENCE_PERFORMANCE_SIGNAL_SELECTION_INVALID"));

        var beforeLock = workspace.Describe();
        if (!IsWorkspaceRuntimeCoherent(beforeLock, runtime.Describe(), out var coherenceError))
            return PreparedPerformanceV1BResult.Fail(coherenceError!);

        await using var mutation = await workspace.AcquireMutationAsync(beforeLock.ChangeVersion, cancellationToken);
        var descriptor = workspace.Describe();
        if (!IsWorkspaceRuntimeCoherent(descriptor, runtime.Describe(), out coherenceError))
            return PreparedPerformanceV1BResult.Fail(coherenceError!);
        if (!highAvailability.CanOwnIndustrialEffects())
            return PreparedPerformanceV1BResult.Fail(Conflict("INTELLIGENCE_RUNTIME_NOT_EFFECTIVE_ACTIVE"));
        if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
            return PreparedPerformanceV1BResult.Fail(Forbidden("INTELLIGENCE_ENTITLEMENT_REQUIRED"));

        var policyVersion = 0L;
        var haEpoch = 0L;
        if (authorityPolicyHa is not null)
        {
            var authorityFence = await authorityPolicyHa.CheckAsync(cancellationToken);
            if (!authorityFence.Allowed)
                return PreparedPerformanceV1BResult.Fail(
                    Conflict(authorityFence.ReasonCode.ToUpperInvariant().Replace('-', '_')));
            policyVersion = authorityFence.PolicyVersion;
            haEpoch = authorityFence.HaEpoch;
            var refreshedTrendAuthorization = await security.CheckRuntimeAsync(
                context, runtime, SecurityCapability.TrendUse, cancellationToken: cancellationToken);
            var refreshedTrendFailure = refreshedTrendAuthorization.FailureResult();
            if (refreshedTrendFailure is not null)
                return PreparedPerformanceV1BResult.Fail(refreshedTrendFailure);
        }

        var configuration = workspace.AssetProcessIntelligence.Snapshot();
        if (configuration.Version != 2)
            return PreparedPerformanceV1BResult.Fail(Conflict("INTELLIGENCE_V2_CONFIGURATION_REQUIRED"));
        var scopeMatches = (configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
            .Where(scope => scope.Kind == request.ScopeKind && scope.ScopeId == request.ScopeId)
            .Take(2)
            .ToArray();
        if (scopeMatches.Length != 1 || !scopeMatches[0].Active)
            return PreparedPerformanceV1BResult.Fail(Conflict("INTELLIGENCE_SCOPE_NOT_ACTIVE_OR_AMBIGUOUS"));
        if (!executionGate.TryGetToken(
                descriptor.ProjectKey!, descriptor.BaseRevision!.Value, request.ScopeKind, request.ScopeId,
                out var permitToken, policyVersion, haEpoch))
            return PreparedPerformanceV1BResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_REQUIRED"));

        var assignmentMatches = (configuration.Assignments ?? Array.Empty<AssetProcessIntelligenceAssignmentEngineeringDto>())
            .Where(assignment => assignment.Id == request.AssignmentId)
            .Take(2)
            .ToArray();
        if (assignmentMatches.Length != 1)
            return PreparedPerformanceV1BResult.Fail(NotFound("INTELLIGENCE_ASSIGNMENT_NOT_AVAILABLE"));
        var assignment = assignmentMatches[0];
        if (assignment.EquipmentId == Guid.Empty || assignment.ProfileId != request.ProfileId)
            return PreparedPerformanceV1BResult.Fail(BadRequest("INTELLIGENCE_PERFORMANCE_ASSIGNMENT_PROFILE_MISMATCH"));

        var profileMatches = (configuration.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(profile => profile.Id == request.ProfileId)
            .Take(2)
            .ToArray();
        if (profileMatches.Length != 1)
            return PreparedPerformanceV1BResult.Fail(Conflict("INTELLIGENCE_PROFILE_NOT_AVAILABLE_OR_AMBIGUOUS"));
        var profile = profileMatches[0];
        var profileSignals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>()).ToArray();
        if (profileSignals.Any(signal => signal is null || signal.Id == Guid.Empty || signal.TagId == Guid.Empty))
            return PreparedPerformanceV1BResult.Fail(Conflict("INTELLIGENCE_PROFILE_SIGNAL_IDENTITY_INVALID"));

        var selected = new List<AssetProcessIntelligenceSignalEngineeringDto>(requestedIds.Count);
        foreach (var signalId in requestedIds)
        {
            var matches = profileSignals.Where(signal => signal.Id == signalId).Take(2).ToArray();
            if (matches.Length != 1)
                return PreparedPerformanceV1BResult.Fail(BadRequest("INTELLIGENCE_PERFORMANCE_SIGNAL_IDENTITY_INVALID"));
            selected.Add(matches[0]);
        }
        if (selected.Select(signal => signal.TagId).Distinct().Count() != selected.Count)
            return PreparedPerformanceV1BResult.Fail(BadRequest("INTELLIGENCE_PERFORMANCE_DUPLICATE_TAG_REFERENCE_UNSUPPORTED"));

        var roleSignals = new Dictionary<string, AssetProcessIntelligenceSignalEngineeringDto>(StringComparer.Ordinal);
        foreach (var signal in selected)
        {
            var binding = signal.SemanticBinding;
            if (binding is null || AssetProcessIntelligenceSemanticCatalogResolver.ValidateBinding(binding, signal.Id).Count != 0 ||
                !expectedRoles.Contains(binding.RoleCode, StringComparer.Ordinal) ||
                !roleSignals.TryAdd(binding.RoleCode, signal))
                return PreparedPerformanceV1BResult.Fail(BadRequest("INTELLIGENCE_PERFORMANCE_SEMANTICS_UNSUPPORTED"));
        }
        var rolesMatchRule = rule.Id == "specific-active-energy-at-recorded-production-rate.v1"
            ? roleSignals.Count == 2 && roleSignals.ContainsKey("equipment.active_input_power") &&
              roleSignals.Keys.Count(role => role.StartsWith("process.net_good_production_", StringComparison.Ordinal)) == 1
            : roleSignals.Count == expectedRoles.Count && expectedRoles.All(role => roleSignals.ContainsKey(role));
        if (!rolesMatchRule)
            return PreparedPerformanceV1BResult.Fail(BadRequest("INTELLIGENCE_PERFORMANCE_SEMANTICS_UNSUPPORTED"));

        var preparedSignals = new List<AssetProcessIntelligencePerformanceV1BPreparedSignal>(selected.Count);
        foreach (var signal in selected.OrderBy(item => item.SemanticBinding!.RoleCode, StringComparer.Ordinal))
        {
            var binding = signal.SemanticBinding!;
            if (!runtime.TryGetTag(signal.TagId, out var tag) || tag is null || !IsNumeric(tag.DataType))
                return PreparedPerformanceV1BResult.Fail(NotFound("INTELLIGENCE_ANALYTICS_SOURCE_NOT_AVAILABLE"));
            if (!TryResolvePerformanceUnit(binding.RoleCode, binding.CatalogVersion, binding,
                    tag.ControlledEngineeringUnit, out var unit))
                return PreparedPerformanceV1BResult.Fail(BadRequest("INTELLIGENCE_PERFORMANCE_UNITS_UNSUPPORTED"));
            var resolution = ResolveScopeSources(request.ScopeKind, request.ScopeId, tag.Id, configuration, workspace.Assets);
            if (resolution.Error is not null)
                return PreparedPerformanceV1BResult.Fail(resolution.Error);
            var provenanceMatches = resolution.Assignments!
                .Where(item => item.AssignmentId == assignment.Id && item.EquipmentId == assignment.EquipmentId &&
                               item.ProfileId == assignment.ProfileId && item.SignalId == signal.Id)
                .Take(2)
                .ToArray();
            if (provenanceMatches.Length != 1)
                return PreparedPerformanceV1BResult.Fail(NotFound("INTELLIGENCE_ASSIGNMENT_NOT_IN_ACTIVE_SCOPE"));

            var tagAuthorization = await security.CheckRuntimeTagAsync(
                context, runtime, tag, TagAccessOperation.Read, cancellationToken);
            var tagFailure = tagAuthorization.FailureResult();
            if (tagFailure is not null)
                return PreparedPerformanceV1BResult.Fail(tagFailure);

            var bindingDimension = AssetProcessIntelligenceSemanticCatalogResolver.DimensionForRole(
                binding.CatalogVersion, binding.RoleCode);
            if (bindingDimension is null || !string.Equals(bindingDimension, unit.Dimension, StringComparison.Ordinal))
                return PreparedPerformanceV1BResult.Fail(BadRequest("INTELLIGENCE_PERFORMANCE_SEMANTICS_UNSUPPORTED"));
            var plan = new PreparedPlan(
                descriptor.ProjectKey!, descriptor.BaseRevision!.Value, descriptor.ChangeVersion,
                tag, Array.AsReadOnly(provenanceMatches), permitToken, request.ScopeKind, request.ScopeId,
                policyVersion, haEpoch);
            var description = new AssetProcessIntelligencePerformanceV1BSignal(
                signal.Id, signal.Key, tag.Id, tag.Path, tag.DataType, binding.CatalogVersion,
                binding.RoleCode, binding.CurrentBasis, binding.SignalKind, binding.FrequencyReference,
                binding.PressureReference, binding.FlowDirection, tag.ControlledEngineeringUnit!.Code,
                tag.ControlledEngineeringUnit.CatalogVersion,
                unit.CanonicalUnit);
            preparedSignals.Add(new(signal, tag, binding, unit, description, plan));
        }

        if (permitToken.IsCancellationRequested)
            return PreparedPerformanceV1BResult.Fail(Conflict("INTELLIGENCE_EXECUTION_PERMIT_CANCELLED"));
        return PreparedPerformanceV1BResult.Ready(new PreparedPerformanceV1BPlan(
            descriptor.ProjectKey!, descriptor.BaseRevision!.Value, descriptor.ChangeVersion,
            assignment, profile, rule, Array.AsReadOnly(preparedSignals.ToArray()), permitToken));
    }

    private static bool TryResolvePerformanceUnit(
        string role,
        int semanticCatalogVersion,
        AssetProcessIntelligenceSignalSemanticBindingEngineeringDto binding,
        ControlledEngineeringUnitReference? reference,
        out ControlledEngineeringUnitDefinition definition)
    {
        definition = default!;
        if (!ControlledEngineeringSemanticsResolver.TryResolveUnit(reference, out definition)) return false;

        return role switch
        {
            "equipment.active_input_power" => semanticCatalogVersion == 2 && binding.SignalKind == "measured_total_input" &&
                reference is not null && reference.CatalogVersion == 2 &&
                (reference.Code == "unit.power.watt" || reference.Code == "unit.power.kilowatt") && definition.Dimension == "power",
            "process.net_good_production_volume_rate" => semanticCatalogVersion == 2 && binding.SignalKind == "measured_output" &&
                reference is not null && reference.CatalogVersion == 2 &&
                reference.Code == "unit.volumetric_flow.cubic_metre_per_second" && definition.Dimension == "volumetric_flow",
            "process.net_good_production_mass_rate" => semanticCatalogVersion == 2 && binding.SignalKind == "measured_output" &&
                reference is not null && reference.CatalogVersion == 2 &&
                (reference.Code == "unit.mass_flow.kilogram_per_second" || reference.Code == "unit.mass_flow.kilogram_per_hour") && definition.Dimension == "mass_flow",
            "process.net_good_production_item_rate" => semanticCatalogVersion == 2 && binding.SignalKind == "measured_output" &&
                reference is not null && reference.CatalogVersion == 2 &&
                (reference.Code == "unit.item_rate.item_per_second" || reference.Code == "unit.item_rate.item_per_hour") && definition.Dimension == "item_rate",
            "pump.discharge_volumetric_flow" => semanticCatalogVersion == 1 && binding.FlowDirection == "pump_to_process" &&
                definition.Dimension == "volumetric_flow" &&
                reference is not null &&
                ((reference.CatalogVersion == 1 && reference.Code == "unit.volumetric_flow.cubic_metre_per_hour") ||
                 (reference.CatalogVersion == 2 && reference.Code == "unit.volumetric_flow.cubic_metre_per_second")),
            "pump.direct_differential_pressure" => semanticCatalogVersion == 2 && binding.SignalKind == "measured_output" &&
                binding.PressureReference == "differential_outlet_minus_inlet" && definition.Dimension == "pressure" &&
                reference is not null &&
                ((reference.CatalogVersion == 1 && reference.Code == "unit.pressure.bar") ||
                 (reference.CatalogVersion == 2 && (reference.Code == "unit.pressure.pascal" || reference.Code == "unit.pressure.kilopascal"))),
            _ => false
        };
    }

    private async Task<PerformanceV1BObservationReadResult> ReadPerformanceV1BObservationsAsync(
        HttpContext context,
        AssetProcessIntelligenceAnalyticsWindow window,
        AssetProcessIntelligencePerformanceV1BPreparedSignal signal,
        IReadOnlyList<PreparedPlan> allPlans,
        PerformanceV1BRawRowBudget budget,
        CancellationToken cancellationToken)
    {
        var observations = new List<AssetProcessIntelligencePerformanceV1BRawObservation>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        DateTimeOffset? lastObservedTimestamp = null;
        var pageCount = 0;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++pageCount > AssetProcessIntelligencePerformanceV1BCatalog.MaximumRawRowsAcrossInputs /
                AssetProcessIntelligencePerformanceV1BCatalog.QueryPageSize + 1)
                throw new InvalidDataException("Historian pagination exceeded the bounded page count.");

            await using var mutation = await workspace.AcquireMutationAsync(signal.Plan.ChangeVersion, cancellationToken);
            var validation = await RevalidatePerformanceV1BPlansAsync(context, allPlans, cancellationToken);
            if (validation is not null) return PerformanceV1BObservationReadResult.Fail(validation);
            var remainingRows = AssetProcessIntelligencePerformanceV1BCatalog.MaximumRawRowsAcrossInputs - budget.TotalRows;
            // At the aggregate cap, probe remaining TAGs with a one-row page so an
            // exactly-full query can still succeed when those TAGs have no observations.
            var pageSize = remainingRows == 0
                ? 1
                : Math.Min(AssetProcessIntelligencePerformanceV1BCatalog.QueryPageSize, remainingRows);
            var request = new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamplesV2,
                HistoricalTimeRange.Absolute(window.FromUtc, window.ToUtc),
                Version: 2,
                Filters: [new HistoricalFilter("tag.id", HistoricalFilterOperator.Eq,
                    [HistoricalQueryValue.FromGuid(signal.Tag.Id)])],
                OrderBy: [new HistoricalSort("timestamp", HistoricalSortDirection.Ascending)],
                Page: new HistoricalPageRequest(pageSize, cursor));
            var response = await historicalQueries.QueryAsync(request, cancellationToken);
            ValidateCurrentFrequencyPage(response, window, signal.Tag.Id, pageSize);
            var postReadValidation = await RevalidatePerformanceV1BPlansAsync(context, allPlans, cancellationToken);
            if (postReadValidation is not null) return PerformanceV1BObservationReadResult.Fail(postReadValidation);
            if (budget.TotalRows + response.Rows.Count > AssetProcessIntelligencePerformanceV1BCatalog.MaximumRawRowsAcrossInputs ||
                (budget.TotalRows + response.Rows.Count == AssetProcessIntelligencePerformanceV1BCatalog.MaximumRawRowsAcrossInputs &&
                 response.NextCursor is not null))
                return PerformanceV1BObservationReadResult.Fail(WindowTooDense("INTELLIGENCE_PERFORMANCE_WINDOW_TOO_DENSE"));

            foreach (var row in response.Rows)
            {
                var observation = ToPerformanceV1BObservation(row);
                if (lastObservedTimestamp.HasValue && observation.ObservedTimestampUtc < lastObservedTimestamp.Value)
                    throw new InvalidDataException("Historian v2 rows were not returned in ascending observed timestamp order.");
                lastObservedTimestamp = observation.ObservedTimestampUtc;
                observations.Add(observation);
            }
            budget.TotalRows += response.Rows.Count;
            cursor = response.NextCursor;
            if (cursor is not null && !cursors.Add(cursor))
                throw new InvalidDataException("Historian pagination cursor repeated.");
        }
        while (cursor is not null);

        return PerformanceV1BObservationReadResult.Ready(observations.AsReadOnly());
    }

    private async Task<IResult?> RevalidatePerformanceV1BPlansAsync(
        HttpContext context,
        IReadOnlyList<PreparedPlan> plans,
        CancellationToken cancellationToken)
    {
        foreach (var plan in plans)
        {
            var validation = await RevalidateAsync(context, plan, cancellationToken);
            if (validation is not null) return validation;
        }
        return null;
    }

    private static AssetProcessIntelligencePerformanceV1BRawObservation ToPerformanceV1BObservation(HistoricalQueryRow row)
    {
        var timestamp = row.Cells["timestamp"].AsDateTime();
        var quality = row.Cells["quality"].Kind is HistoricalValueKind.Enum or HistoricalValueKind.String
            ? row.Cells["quality"].Value : null;
        DateTimeOffset? sourceTimestamp = row.Cells["source.timestamp"].Kind == HistoricalValueKind.DateTime
            ? row.Cells["source.timestamp"].AsDateTime() : null;
        var sourceEvidence = row.Cells["source.timestamp.status"].Kind is HistoricalValueKind.Enum or HistoricalValueKind.String
            ? row.Cells["source.timestamp.status"].Value : null;
        return new(timestamp, quality, row.Cells.GetValueOrDefault("value"), sourceTimestamp, sourceEvidence);
    }

    private static IReadOnlyList<string> ExpectedPerformanceRoles(string ruleId) => ruleId switch
    {
        "specific-active-energy-at-recorded-production-rate.v1" =>
            ["equipment.active_input_power", "process.net_good_production_volume_rate", "process.net_good_production_mass_rate", "process.net_good_production_item_rate"],
        "recorded-differential-pressure-power.v1" =>
            ["pump.discharge_volumetric_flow", "pump.direct_differential_pressure"],
        "recorded-pressure-power-input-ratio.v1" =>
            ["pump.discharge_volumetric_flow", "pump.direct_differential_pressure", "equipment.active_input_power"],
        _ => Array.Empty<string>()
    };

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

    private static IResult? ValidateCurrentFrequencyComparisonRequest(
        AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest request)
    {
        if (!Enum.IsDefined(request.ScopeKind) || request.ScopeId == Guid.Empty ||
            request.AssignmentId == Guid.Empty || request.CurrentSignalId == Guid.Empty ||
            request.FrequencySignalId == Guid.Empty || request.CurrentSignalId == request.FrequencySignalId)
            return BadRequest("INTELLIGENCE_CURRENT_FREQUENCY_IDENTITY_INVALID");
        if (request.PrimaryWindow is null || request.ComparisonWindow is null ||
            !ValidCurrentFrequencyWindow(request.PrimaryWindow) || !ValidCurrentFrequencyWindow(request.ComparisonWindow) ||
            request.PrimaryWindow.ToUtc - request.PrimaryWindow.FromUtc !=
            request.ComparisonWindow.ToUtc - request.ComparisonWindow.FromUtc)
            return BadRequest("INTELLIGENCE_CURRENT_FREQUENCY_WINDOWS_INVALID");
        var primary = request.PrimaryWindow;
        var comparison = request.ComparisonWindow;
        if (!(primary.ToUtc < comparison.FromUtc || comparison.ToUtc < primary.FromUtc))
            return BadRequest("INTELLIGENCE_CURRENT_FREQUENCY_WINDOWS_MUST_BE_DISJOINT");
        if (!double.IsFinite(request.MinimumFrequencyHz) || !double.IsFinite(request.MaximumFrequencyHz) ||
            request.MinimumFrequencyHz > request.MaximumFrequencyHz)
            return BadRequest("INTELLIGENCE_CURRENT_FREQUENCY_RANGE_INVALID");
        return null;
    }

    private static IResult? ValidateContextualReferenceDeviationRequest(
        AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest request)
    {
        if (!Enum.IsDefined(request.ScopeKind) || request.ScopeId == Guid.Empty || request.AssignmentId == Guid.Empty ||
            request.TargetSignalId == Guid.Empty || request.ConditionSignalId == Guid.Empty ||
            request.TargetSignalId == request.ConditionSignalId)
            return BadRequest("INTELLIGENCE_CONTEXTUAL_REFERENCE_IDENTITY_INVALID");
        if (request.ReferenceWindow is null || request.EvaluationWindow is null ||
            !ValidCurrentFrequencyWindow(request.ReferenceWindow) || !ValidCurrentFrequencyWindow(request.EvaluationWindow))
            return BadRequest("INTELLIGENCE_CONTEXTUAL_REFERENCE_WINDOWS_INVALID");
        if (request.ReferenceWindow.ToUtc >= request.EvaluationWindow.FromUtc)
            return BadRequest("INTELLIGENCE_CONTEXTUAL_REFERENCE_EVALUATION_WINDOW_MUST_BE_LATER");
        if (!double.IsFinite(request.ConditionLowerInclusiveCanonical) || !double.IsFinite(request.ConditionUpperExclusiveCanonical) ||
            request.ConditionLowerInclusiveCanonical >= request.ConditionUpperExclusiveCanonical)
            return BadRequest("INTELLIGENCE_CONTEXTUAL_REFERENCE_CONDITION_RANGE_INVALID");
        return null;
    }

    private static IResult? ValidateContextualDeviationVectorRequest(
        AssetProcessIntelligenceContextualDeviationVectorV1QueryRequest request)
    {
        if (!Enum.IsDefined(request.ScopeKind) || request.ScopeId == Guid.Empty || request.AssignmentId == Guid.Empty ||
            request.ConditionSignalId == Guid.Empty)
            return BadRequest("INTELLIGENCE_CONTEXTUAL_DEVIATION_VECTOR_IDENTITY_INVALID");
        if (request.TargetSignalIds is null ||
            request.TargetSignalIds.Count is < AssetProcessIntelligenceContextualDeviationVectorV1Catalog.MinimumTargetSignals or
                > AssetProcessIntelligenceContextualDeviationVectorV1Catalog.MaximumTargetSignals ||
            request.TargetSignalIds.Any(id => id == Guid.Empty || id == request.ConditionSignalId) ||
            request.TargetSignalIds.Distinct().Count() != request.TargetSignalIds.Count)
            return BadRequest("INTELLIGENCE_CONTEXTUAL_DEVIATION_VECTOR_SIGNAL_SELECTION_INVALID");
        if (request.ReferenceWindow is null || request.EvaluationWindow is null ||
            request.ReferenceWindow.FromUtc.Offset != TimeSpan.Zero || request.ReferenceWindow.ToUtc.Offset != TimeSpan.Zero ||
            request.ReferenceWindow.ToUtc <= request.ReferenceWindow.FromUtc ||
            request.ReferenceWindow.ToUtc - request.ReferenceWindow.FromUtc > AssetProcessIntelligenceContextualDeviationVectorV1Catalog.MaximumWindow ||
            request.EvaluationWindow.FromUtc.Offset != TimeSpan.Zero || request.EvaluationWindow.ToUtc.Offset != TimeSpan.Zero ||
            request.EvaluationWindow.ToUtc <= request.EvaluationWindow.FromUtc ||
            request.EvaluationWindow.ToUtc - request.EvaluationWindow.FromUtc > AssetProcessIntelligenceContextualDeviationVectorV1Catalog.MaximumWindow)
            return BadRequest("INTELLIGENCE_CONTEXTUAL_DEVIATION_VECTOR_WINDOWS_INVALID");
        if (request.ReferenceWindow.ToUtc >= request.EvaluationWindow.FromUtc)
            return BadRequest("INTELLIGENCE_CONTEXTUAL_DEVIATION_VECTOR_EVALUATION_WINDOW_MUST_BE_LATER");
        if (!double.IsFinite(request.ConditionLowerInclusiveCanonical) ||
            !double.IsFinite(request.ConditionUpperExclusiveCanonical) ||
            request.ConditionLowerInclusiveCanonical >= request.ConditionUpperExclusiveCanonical)
            return BadRequest("INTELLIGENCE_CONTEXTUAL_DEVIATION_VECTOR_CONDITION_RANGE_INVALID");
        return null;
    }

    private static IResult? ValidatePerformanceV1BRequest(AssetProcessIntelligencePerformanceV1BQueryRequest request)
    {
        if (!Enum.IsDefined(request.ScopeKind) || request.ScopeId == Guid.Empty ||
            request.AssignmentId == Guid.Empty || request.ProfileId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.RuleId))
            return BadRequest("INTELLIGENCE_PERFORMANCE_IDENTITY_INVALID");
        if (AssetProcessIntelligencePerformanceV1BCatalog.FindRule(request.RuleId) is null)
            return BadRequest("INTELLIGENCE_PERFORMANCE_RULE_UNSUPPORTED");
        if (request.SignalIds is null || request.SignalIds.Count == 0 ||
            request.SignalIds.Any(id => id == Guid.Empty) ||
            request.SignalIds.Distinct().Count() != request.SignalIds.Count)
            return BadRequest("INTELLIGENCE_PERFORMANCE_SIGNAL_SELECTION_INVALID");
        if (request.Window is null ||
            request.Window.FromUtc.Offset != TimeSpan.Zero || request.Window.ToUtc.Offset != TimeSpan.Zero ||
            request.Window.ToUtc <= request.Window.FromUtc ||
            request.Window.ToUtc - request.Window.FromUtc > AssetProcessIntelligencePerformanceV1BCatalog.MaximumWindow)
            return BadRequest("INTELLIGENCE_PERFORMANCE_WINDOW_INVALID");
        return null;
    }

    private static bool ValidWindow(AssetProcessIntelligenceAnalyticsWindow window) =>
        window.FromUtc.Offset == TimeSpan.Zero &&
        window.ToUtc.Offset == TimeSpan.Zero &&
        window.ToUtc > window.FromUtc &&
        window.ToUtc - window.FromUtc <= AssetProcessIntelligenceKpiCatalogV1.MaximumWindow;

    private static bool ValidCurrentFrequencyWindow(AssetProcessIntelligenceAnalyticsWindow window) =>
        window.FromUtc.Offset == TimeSpan.Zero &&
        window.ToUtc.Offset == TimeSpan.Zero &&
        window.ToUtc > window.FromUtc &&
        window.ToUtc - window.FromUtc <= AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.MaximumWindow;

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

    private static void ValidateCurrentFrequencyPage(
        HistoricalQueryResponse response,
        AssetProcessIntelligenceAnalyticsWindow window,
        Guid expectedTagId,
        int expectedPageSize)
    {
        if (response is null || response.Version != 2 ||
            !string.Equals(response.Dataset, HistoricalDatasets.HistorianSamplesV2, StringComparison.Ordinal) ||
            response.FromUtc != window.FromUtc || response.ToUtc != window.ToUtc ||
            response.FromUtc.Offset != TimeSpan.Zero || response.ToUtc.Offset != TimeSpan.Zero ||
            response.PageSize != expectedPageSize || response.Rows is null ||
            response.Rows.Count > expectedPageSize || response.Columns is null)
            throw new InvalidDataException("Historical v2 response metadata does not match the request.");

        var requiredFields = new Dictionary<string, HistoricalFieldType>(StringComparer.Ordinal)
        {
            ["tag.id"] = HistoricalFieldType.Guid,
            ["quality"] = HistoricalFieldType.Enum,
            ["value"] = HistoricalFieldType.Scalar,
            ["timestamp"] = HistoricalFieldType.DateTime,
            ["source.timestamp"] = HistoricalFieldType.DateTime,
            ["source.timestamp.status"] = HistoricalFieldType.Enum
        };
        if (response.Columns.Any(column => column is null))
            throw new InvalidDataException("Historical v2 response contains a null column descriptor.");
        foreach (var required in requiredFields)
        {
            var columns = response.Columns.Where(column => column is not null && column.Field == required.Key).Take(2).ToArray();
            if (columns.Length != 1 || columns[0].Type != required.Value)
                throw new InvalidDataException("Historical v2 response columns do not match the contract.");
        }

        foreach (var row in response.Rows)
        {
            if (row is null || row.Cells is null ||
                !row.Cells.TryGetValue("tag.id", out var tagCell) || tagCell is null || tagCell.Kind != HistoricalValueKind.Guid ||
                !Guid.TryParse(tagCell.Value, out var returnedTagId) || returnedTagId != expectedTagId)
                throw new InvalidDataException("Historical v2 response contains a different TAG.");
            if (!row.Cells.TryGetValue("timestamp", out var timestamp) || timestamp is null ||
                timestamp.Kind != HistoricalValueKind.DateTime ||
                !DateTimeOffset.TryParse(timestamp.Value, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var observedAt) ||
                observedAt.Offset != TimeSpan.Zero || observedAt < window.FromUtc || observedAt > window.ToUtc)
                throw new InvalidDataException("Historical v2 response contains an invalid observation timestamp.");
            if (!row.Cells.TryGetValue("quality", out var quality) || quality is null ||
                quality.Kind is not (HistoricalValueKind.Enum or HistoricalValueKind.String or HistoricalValueKind.Null))
                throw new InvalidDataException("Historical v2 response contains malformed quality evidence.");
            if (!row.Cells.TryGetValue("value", out var value) || value is null ||
                value.Kind is not (HistoricalValueKind.Int16 or HistoricalValueKind.Int32 or HistoricalValueKind.Int64 or
                    HistoricalValueKind.Float or HistoricalValueKind.Double or HistoricalValueKind.Number or
                    HistoricalValueKind.String or HistoricalValueKind.Boolean or HistoricalValueKind.Null))
                throw new InvalidDataException("Historical v2 response contains a malformed value.");
            if (!row.Cells.TryGetValue("source.timestamp", out var sourceTimestamp) || sourceTimestamp is null ||
                sourceTimestamp.Kind is not (HistoricalValueKind.DateTime or HistoricalValueKind.Null))
                throw new InvalidDataException("Historical v2 response contains malformed source timestamp evidence.");
            if (sourceTimestamp.Kind == HistoricalValueKind.DateTime &&
                (!DateTimeOffset.TryParse(sourceTimestamp.Value, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var parsedSourceTimestamp) ||
                 parsedSourceTimestamp.Offset != TimeSpan.Zero))
                throw new InvalidDataException("Historical v2 response contains an invalid source timestamp.");
            if (!row.Cells.TryGetValue("source.timestamp.status", out var evidence) || evidence is null ||
                evidence.Kind is not (HistoricalValueKind.Enum or HistoricalValueKind.String or HistoricalValueKind.Null))
                throw new InvalidDataException("Historical v2 response contains malformed source timestamp status.");
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

    private static AssetProcessIntelligenceCurrentFrequencyRawObservation ToCurrentFrequencyObservation(
        HistoricalQueryRow row)
    {
        var timestamp = row.Cells["timestamp"].AsDateTime();
        var quality = row.Cells["quality"].Kind is HistoricalValueKind.Enum or HistoricalValueKind.String
            ? row.Cells["quality"].Value
            : null;
        DateTimeOffset? sourceTimestamp = row.Cells["source.timestamp"].Kind == HistoricalValueKind.DateTime
            ? row.Cells["source.timestamp"].AsDateTime()
            : null;
        var sourceEvidence = row.Cells["source.timestamp.status"].Kind is HistoricalValueKind.Enum or HistoricalValueKind.String
            ? row.Cells["source.timestamp.status"].Value
            : null;
        return new(timestamp, quality, row.Cells["value"], sourceTimestamp, sourceEvidence);
    }

    private static AssetProcessIntelligenceContextualReferenceRawObservation ToContextualReferenceObservation(
        HistoricalQueryRow row)
    {
        var timestamp = row.Cells["timestamp"].AsDateTime();
        var quality = row.Cells["quality"].Kind is HistoricalValueKind.Enum or HistoricalValueKind.String
            ? row.Cells["quality"].Value
            : null;
        DateTimeOffset? sourceTimestamp = row.Cells["source.timestamp"].Kind == HistoricalValueKind.DateTime
            ? row.Cells["source.timestamp"].AsDateTime()
            : null;
        var sourceEvidence = row.Cells["source.timestamp.status"].Kind is HistoricalValueKind.Enum or HistoricalValueKind.String
            ? row.Cells["source.timestamp.status"].Value
            : null;
        return new(timestamp, quality, row.Cells["value"], sourceTimestamp, sourceEvidence);
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

    internal sealed record PreparedPlan(
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

    private sealed record PreparedCurrentFrequencySignal(
        AssetProcessIntelligenceSignalEngineeringDto Signal,
        TagDefinition Tag,
        AssetProcessIntelligenceSignalSemanticBindingEngineeringDto Binding,
        ControlledEngineeringUnitDefinition Unit,
        PreparedPlan Plan,
        AssetProcessIntelligenceCurrentFrequencyComparisonV1Signal Description);

    private sealed record PreparedCurrentFrequencyComparisonPlan(
        string ProjectKey,
        long Revision,
        long ChangeVersion,
        AssetProcessIntelligenceAssignmentEngineeringDto Assignment,
        AssetProcessIntelligenceProfileEngineeringDto Profile,
        PreparedCurrentFrequencySignal Current,
        PreparedCurrentFrequencySignal Frequency,
        CancellationToken PermitToken);

    private sealed record PreparedCurrentFrequencyComparisonResult(
        PreparedCurrentFrequencyComparisonPlan? Plan,
        IResult? Error)
    {
        public static PreparedCurrentFrequencyComparisonResult Ready(PreparedCurrentFrequencyComparisonPlan plan) => new(plan, null);
        public static PreparedCurrentFrequencyComparisonResult Fail(IResult error) => new(null, error);
    }

    private sealed record PreparedContextualReferencePlan(
        string ProjectKey,
        long Revision,
        long ChangeVersion,
        AssetProcessIntelligenceAssignmentEngineeringDto Assignment,
        AssetProcessIntelligenceProfileEngineeringDto Profile,
        TagDefinition Target,
        TagDefinition Condition,
        PreparedPlan TargetPlan,
        PreparedPlan ConditionPlan,
        AssetProcessIntelligenceContextualReferenceDeviationV1Signal TargetDescription,
        AssetProcessIntelligenceContextualReferenceDeviationV1Signal ConditionDescription,
        ControlledEngineeringUnitDefinition ConditionUnit,
        double ConditionLowerCanonical,
        double ConditionUpperCanonical,
        CancellationToken PermitToken);

    private sealed record PreparedContextualReferencePlanResult(PreparedContextualReferencePlan? Plan, IResult? Error)
    {
        public static PreparedContextualReferencePlanResult Ready(PreparedContextualReferencePlan plan) => new(plan, null);
        public static PreparedContextualReferencePlanResult Fail(IResult error) => new(null, error);
    }

    private sealed record PreparedContextualDeviationVectorSignal(
        AssetProcessIntelligenceSignalEngineeringDto Signal,
        TagDefinition Tag,
        PreparedPlan Plan,
        AssetProcessIntelligenceContextualDeviationVectorV1Signal Description);

    private sealed record PreparedContextualDeviationVectorPlan(
        string ProjectKey,
        long Revision,
        long ChangeVersion,
        AssetProcessIntelligenceAssignmentEngineeringDto Assignment,
        AssetProcessIntelligenceProfileEngineeringDto Profile,
        IReadOnlyList<PreparedContextualDeviationVectorSignal> Targets,
        PreparedContextualDeviationVectorSignal Condition,
        ControlledEngineeringUnitDefinition ConditionUnit,
        double ConditionLowerCanonical,
        double ConditionUpperCanonical,
        CancellationToken PermitToken)
    {
        public IReadOnlyList<PreparedContextualDeviationVectorSignal> Signals => Targets.Append(Condition).ToArray();
    }

    private sealed record PreparedContextualDeviationVectorPlanResult(
        PreparedContextualDeviationVectorPlan? Plan,
        IResult? Error)
    {
        public static PreparedContextualDeviationVectorPlanResult Ready(PreparedContextualDeviationVectorPlan plan) => new(plan, null);
        public static PreparedContextualDeviationVectorPlanResult Fail(IResult error) => new(null, error);
    }

    private sealed class CurrentFrequencyRawRowBudget
    {
        public int TotalRows { get; set; }
    }

    private sealed record CurrentFrequencyObservationReadResult(
        IReadOnlyList<AssetProcessIntelligenceCurrentFrequencyRawObservation>? Observations,
        IResult? Error)
    {
        public static CurrentFrequencyObservationReadResult Ready(
            IReadOnlyList<AssetProcessIntelligenceCurrentFrequencyRawObservation> observations) => new(observations, null);
        public static CurrentFrequencyObservationReadResult Fail(IResult error) => new(null, error);
    }

    private sealed class ContextualReferenceRawRowBudget
    {
        public int TotalRows { get; set; }
    }

    private sealed class ContextualDeviationVectorRawRowBudget
    {
        public int TotalRows { get; set; }
    }

    private sealed record ContextualDeviationVectorObservationReadResult(
        IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>? Observations,
        IResult? Error)
    {
        public static ContextualDeviationVectorObservationReadResult Ready(
            IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation> observations) => new(observations, null);
        public static ContextualDeviationVectorObservationReadResult Fail(IResult error) => new(null, error);
    }

    private sealed record ContextualReferenceObservationReadResult(
        IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation>? Observations,
        IResult? Error)
    {
        public static ContextualReferenceObservationReadResult Ready(
            IReadOnlyList<AssetProcessIntelligenceContextualReferenceRawObservation> observations) => new(observations, null);
        public static ContextualReferenceObservationReadResult Fail(IResult error) => new(null, error);
    }

    private sealed record PreparedPerformanceV1BPlan(
        string ProjectKey,
        long Revision,
        long ChangeVersion,
        AssetProcessIntelligenceAssignmentEngineeringDto Assignment,
        AssetProcessIntelligenceProfileEngineeringDto Profile,
        AssetProcessIntelligencePerformanceV1BRule Rule,
        IReadOnlyList<AssetProcessIntelligencePerformanceV1BPreparedSignal> Signals,
        CancellationToken PermitToken);

    private sealed record PreparedPerformanceV1BResult(PreparedPerformanceV1BPlan? Plan, IResult? Error)
    {
        public static PreparedPerformanceV1BResult Ready(PreparedPerformanceV1BPlan plan) => new(plan, null);
        public static PreparedPerformanceV1BResult Fail(IResult error) => new(null, error);
    }

    private sealed class PerformanceV1BRawRowBudget
    {
        public int TotalRows { get; set; }
    }

    private sealed record PerformanceV1BObservationReadResult(
        IReadOnlyList<AssetProcessIntelligencePerformanceV1BRawObservation>? Observations,
        IResult? Error)
    {
        public static PerformanceV1BObservationReadResult Ready(
            IReadOnlyList<AssetProcessIntelligencePerformanceV1BRawObservation> observations) => new(observations, null);
        public static PerformanceV1BObservationReadResult Fail(IResult error) => new(null, error);
    }
}
