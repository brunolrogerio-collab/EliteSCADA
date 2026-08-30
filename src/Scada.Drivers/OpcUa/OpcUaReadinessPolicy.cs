namespace Scada.Drivers.OpcUa;

public sealed record OpcUaReadinessEvidence(bool IsReady, string Reason);

public static class OpcUaReadinessPolicy
{
    public static OpcUaReadinessEvidence Evaluate(
        bool secureSessionEstablished,
        bool acquisitionActivated)
    {
        if (!secureSessionEstablished)
            return new(false, "OPC UA secure endpoint/session is not established.");

        if (!acquisitionActivated)
            return new(false, "OPC UA subscriptions/monitored items or configured polling are not active.");

        return new(true, "OPC UA secure session is established and acquisition is active.");
    }
}
