using Scada.Drivers.Abstractions;

namespace Scada.Drivers.AllenBradley;

public sealed record AllenBradleyReadinessEvidence(bool IsReady, string Reason);

public static class AllenBradleyReadinessPolicy
{
    public static AllenBradleyReadinessEvidence Evaluate(
        bool connected,
        CommunicationDriverOperationalState state,
        long readOperations)
    {
        if (!connected)
            return new(false, "EtherNet/IP/CIP session and route are not established.");

        if (readOperations <= 0)
            return new(false, "No bounded Logix acquisition attempt has completed yet.");

        return state switch
        {
            CommunicationDriverOperationalState.Healthy => new(true, "CIP session/route is established and acquisition has executed."),
            CommunicationDriverOperationalState.Degraded => new(true, "CIP acquisition is active with symbol-local degradation."),
            CommunicationDriverOperationalState.Reconnecting => new(false, "CIP session/route is reconnecting."),
            CommunicationDriverOperationalState.Starting => new(false, "CIP acquisition is still starting."),
            CommunicationDriverOperationalState.Stopping => new(false, "CIP acquisition is stopping."),
            _ => new(false, "CIP acquisition is stopped.")
        };
    }
}
