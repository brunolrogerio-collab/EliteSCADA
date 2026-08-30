using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Bacnet;

public sealed record BacnetReadinessEvidence(bool IsReady, string Reason);

/// <summary>
/// Pure BACnet/IP readiness rule. Point quality is intentionally not an input:
/// optional companion-property failures or individual bad objects must not make
/// an otherwise operational BACnet Data Source unready.
/// </summary>
public static class BacnetReadinessPolicy
{
    public static BacnetReadinessEvidence Evaluate(
        bool? deviceReachable,
        CommunicationDriverOperationalState state,
        int configuredPointCount)
    {
        if (configuredPointCount <= 0)
            return new(false, "No BACnet points are configured for acquisition.");

        if (deviceReachable != true)
            return new(false, "BACnet Device Instance reachability has not been established.");

        return state switch
        {
            CommunicationDriverOperationalState.Healthy => new(true, "BACnet Device Instance is reachable and acquisition is active."),
            CommunicationDriverOperationalState.Degraded => new(true, "BACnet acquisition is active with point/protocol-local degradation."),
            CommunicationDriverOperationalState.Starting => new(false, "BACnet acquisition is still starting."),
            CommunicationDriverOperationalState.Reconnecting => new(false, "BACnet Device Instance reachability is being re-established."),
            CommunicationDriverOperationalState.Stopping => new(false, "BACnet acquisition is stopping."),
            _ => new(false, "BACnet acquisition is stopped.")
        };
    }
}
