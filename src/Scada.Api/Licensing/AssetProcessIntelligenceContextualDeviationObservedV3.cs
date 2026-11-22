using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;

namespace Scada.Api.Licensing;

/// <summary>
/// Explicit request contract for analyses aligned by the time Historian recorded each reading.
/// This is intentionally separate from the frozen synchronized-source-time V2 request.
/// </summary>
public sealed record AssetProcessIntelligenceContextualDeviationObservedV3QueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid AssignmentId,
    IReadOnlyList<Guid>? TargetSignalIds,
    Guid FirstContextSignalId,
    double FirstContextLowerInclusiveCanonical,
    double FirstContextUpperExclusiveCanonical,
    Guid SecondContextSignalId,
    double SecondContextLowerInclusiveCanonical,
    double SecondContextUpperExclusiveCanonical,
    AssetProcessIntelligenceAnalyticsWindow? ReferenceWindow,
    AssetProcessIntelligenceAnalyticsWindow? EvaluationWindow,
    string TimestampBasis,
    int TemporalToleranceSeconds = 0)
{
    internal AssetProcessIntelligenceContextualDeviationVectorV2QueryRequest ToV2Request() => new(
        ScopeKind,
        ScopeId,
        AssignmentId,
        TargetSignalIds,
        FirstContextSignalId,
        FirstContextLowerInclusiveCanonical,
        FirstContextUpperExclusiveCanonical,
        SecondContextSignalId,
        SecondContextLowerInclusiveCanonical,
        SecondContextUpperExclusiveCanonical,
        ReferenceWindow,
        EvaluationWindow,
        TemporalToleranceSeconds);
}
