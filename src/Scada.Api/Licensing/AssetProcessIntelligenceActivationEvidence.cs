using Scada.Engineering.Contracts;
using Scada.Security.Authentication;

namespace Scada.Api.Licensing;

/// <summary>Durable activation evidence; never inferred from Engineering Active.</summary>
public sealed record AssetProcessIntelligenceActivationEvidence(
    long AuthorityEpoch,
    string ProjectKey,
    long Revision,
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    long PolicyVersion,
    string PolicyDigest,
    long HaEpoch,
    long Generation,
    bool Active)
{
    public bool Matches(
        AuthorityLifecycleSnapshot authority, string projectKey, long revision,
        AssetProcessIntelligenceScopeKind scopeKind, Guid scopeId,
        long policyVersion, string policyDigest, long haEpoch, long generation) =>
        Active && authority.AllowsAuthorization && AuthorityEpoch >= 0 && AuthorityEpoch == authority.Epoch &&
        !string.IsNullOrWhiteSpace(ProjectKey) &&
        string.Equals(ProjectKey.Trim(), projectKey?.Trim(), StringComparison.OrdinalIgnoreCase) &&
        Revision > 0 && Revision == revision && Enum.IsDefined(ScopeKind) && ScopeKind == scopeKind &&
        ScopeId != Guid.Empty && ScopeId == scopeId &&
        PolicyVersion >= 0 && PolicyVersion == policyVersion &&
        !string.IsNullOrWhiteSpace(PolicyDigest) &&
        string.Equals(PolicyDigest, policyDigest, StringComparison.Ordinal) &&
        HaEpoch >= 0 && HaEpoch == haEpoch && Generation > 0 && Generation == generation;
}
