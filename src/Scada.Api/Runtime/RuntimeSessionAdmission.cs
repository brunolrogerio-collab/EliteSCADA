using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

/// <summary>
/// Stable server-side reason codes for Runtime admission. These describe class eligibility only;
/// they do not claim that a concurrent license seat has been reserved.
/// </summary>
public enum RuntimeSessionAdmissionReasonCode
{
    ExplicitViewOnly,
    AuthorityReadOnly,
    InteractiveEligible,
    ExistingLeaseRetained
}

/// <summary>
/// The requested class is untrusted client input. The granted class is the restrictive result of
/// canonical Authority evaluation and is persisted as the logical lease connection class.
/// Capacity reservation deliberately belongs to the next shared-quota slice.
/// </summary>
public sealed record RuntimeSessionAdmissionDecision(
    RuntimeConnectionClass RequestedClass,
    RuntimeConnectionClass GrantedClass,
    RuntimeSessionAdmissionReasonCode ReasonCode)
{
    public bool RequiresCapacityReservation => false;
}

public static class RuntimeSessionAdmissionPolicy
{
    private static readonly SecurityCapability[] RuntimeMutationCapabilities =
    [
        SecurityCapability.CommandExecute,
        SecurityCapability.ProcessValueWrite,
        SecurityCapability.AlarmAcknowledge,
        SecurityCapability.AlarmShelve,
        SecurityCapability.TrendSave
    ];

    public static IReadOnlyList<SecurityCapability> MutationCapabilities => RuntimeMutationCapabilities;

    public static RuntimeSessionAdmissionDecision Resolve(
        RuntimeConnectionClass requestedClass,
        IEnumerable<AuthorizationDecision> authorityDecisions)
    {
        ArgumentNullException.ThrowIfNull(authorityDecisions);

        if (requestedClass == RuntimeConnectionClass.ViewOnly)
        {
            return new RuntimeSessionAdmissionDecision(
                requestedClass,
                RuntimeConnectionClass.ViewOnly,
                RuntimeSessionAdmissionReasonCode.ExplicitViewOnly);
        }

        var hasRuntimeMutation = authorityDecisions.Any(
            decision => decision.Allowed && RuntimeMutationCapabilities.Contains(decision.Capability));
        return hasRuntimeMutation
            ? new RuntimeSessionAdmissionDecision(
                requestedClass,
                RuntimeConnectionClass.Interactive,
                RuntimeSessionAdmissionReasonCode.InteractiveEligible)
            : new RuntimeSessionAdmissionDecision(
                requestedClass,
                RuntimeConnectionClass.ViewOnly,
                RuntimeSessionAdmissionReasonCode.AuthorityReadOnly);
    }

    public static RuntimeSessionAdmissionDecision RetainExistingLease(
        RuntimeSessionAdmissionDecision requestedDecision,
        RuntimeConnectionClass persistedGrantedClass) =>
        persistedGrantedClass == requestedDecision.GrantedClass
            ? requestedDecision
            : new RuntimeSessionAdmissionDecision(
                requestedDecision.RequestedClass,
                persistedGrantedClass,
                RuntimeSessionAdmissionReasonCode.ExistingLeaseRetained);

    public static bool IsRuntimeMutation(SecurityCapability capability) =>
        RuntimeMutationCapabilities.Contains(capability);
}
