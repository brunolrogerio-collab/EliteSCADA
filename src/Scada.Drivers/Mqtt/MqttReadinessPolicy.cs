namespace Scada.Drivers.Mqtt;

public sealed record MqttReadinessEvidence(bool IsReady, string Reason);

/// <summary>
/// Pure MQTT source-activation rule. The first telemetry sample is deliberately
/// not required: a healthy subscribed broker connection may legitimately have
/// no current value yet.
/// </summary>
public static class MqttReadinessPolicy
{
    public static MqttReadinessEvidence Evaluate(
        bool brokerAuthenticated,
        bool subscriptionsAccepted)
    {
        if (!brokerAuthenticated)
            return new(false, "MQTT broker connection/authentication is not complete.");

        if (!subscriptionsAccepted)
            return new(false, "MQTT configured subscriptions have not been accepted.");

        return new(true, "MQTT broker connection is authenticated and subscriptions are active.");
    }
}
