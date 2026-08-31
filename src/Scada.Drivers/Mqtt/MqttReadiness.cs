using System.Globalization;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Mqtt;

/// <summary>
/// Protocol-specific MQTT readiness evidence. The public host activation seam is
/// ICommunicationDriverReadinessSource; this richer snapshot is retained only for
/// MQTT diagnostics and is projected onto the shared contract below.
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

public interface IMqttReadinessEvidenceSource : ICommunicationDriverReadinessSource
{
    MqttReadinessSnapshot GetMqttReadiness();

    CommunicationDriverReadinessSnapshot ICommunicationDriverReadinessSource.GetCommunicationReadiness()
    {
        var snapshot = GetMqttReadiness();
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["expectedSubscriptionCount"] = snapshot.ExpectedSubscriptionCount.ToString(CultureInfo.InvariantCulture),
            ["acceptedSubscriptionCount"] = snapshot.AcceptedSubscriptionCount.ToString(CultureInfo.InvariantCulture),
            ["initialHandshakeCompleted"] = snapshot.InitialHandshakeCompleted ? "true" : "false"
        };

        return new CommunicationDriverReadinessSnapshot(
            snapshot.DriverId,
            MqttDriverDescriptorProvider.DriverType,
            snapshot.State switch
            {
                MqttReadinessState.NotStarted => CommunicationDriverReadinessState.NotStarted,
                MqttReadinessState.Starting => CommunicationDriverReadinessState.Starting,
                MqttReadinessState.Ready => CommunicationDriverReadinessState.Ready,
                MqttReadinessState.Faulted => CommunicationDriverReadinessState.Faulted,
                MqttReadinessState.Stopped => CommunicationDriverReadinessState.Stopped,
                _ => throw new ArgumentOutOfRangeException(nameof(snapshot.State), snapshot.State, "Unsupported MQTT readiness state.")
            },
            snapshot.StateChangedAtUtc,
            snapshot.Detail,
            details);
    }
}
