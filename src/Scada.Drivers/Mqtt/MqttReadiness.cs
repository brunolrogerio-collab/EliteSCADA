namespace Scada.Drivers.Mqtt;

/// <summary>
/// Protocol-local readiness evidence for the parked MQTT Driver branch. This is
/// intentionally not the shared DriverHost activation contract; the Coordinator
/// owns that future common seam.
/// </summary>
public enum MqttReadinessState
{
    NotStarted,
    Starting,
    Ready,
    Faulted,
    Stopped
}

public sealed record MqttReadinessSnapshot(
    string DriverId,
    MqttReadinessState State,
    DateTimeOffset StateChangedAtUtc,
    int ExpectedSubscriptionCount,
    int AcceptedSubscriptionCount,
    bool InitialHandshakeCompleted,
    string? Detail = null);

public interface IMqttReadinessEvidenceSource
{
    MqttReadinessSnapshot GetMqttReadiness();
}
