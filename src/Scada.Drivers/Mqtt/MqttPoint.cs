using System.Text;
using Scada.Core.Tags;

namespace Scada.Drivers.Mqtt;

public enum MqttQosLevel
{
    AtMostOnce = 0,
    AtLeastOnce = 1,
    ExactlyOnce = 2
}

public enum MqttPayloadFormat
{
    Utf8Scalar,
    Json
}

public enum MqttRetainedValuePolicy
{
    MarkStaleWithoutSourceTimestamp,
    AcceptAsCurrent
}

public sealed record MqttPoint(
    TagDefinition Tag,
    string SubscribeTopic,
    MqttPayloadFormat PayloadFormat = MqttPayloadFormat.Utf8Scalar,
    string? JsonPointer = null,
    string? SourceTimestampJsonPointer = null,
    bool SourceTimestampRequired = false,
    MqttRetainedValuePolicy RetainedValuePolicy = MqttRetainedValuePolicy.MarkStaleWithoutSourceTimestamp,
    MqttQosLevel Qos = MqttQosLevel.AtLeastOnce,
    bool Writable = false,
    string? PublishTopic = null,
    MqttQosLevel PublishQos = MqttQosLevel.AtLeastOnce,
    bool PublishRetain = false,
    TimeSpan? FreshnessTimeout = null)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Tag);
        ValidateExactTopic(SubscribeTopic, nameof(SubscribeTopic));
        ValidateQos(Qos, nameof(Qos));
        ValidateQos(PublishQos, nameof(PublishQos));
        ValidateJsonPointer(JsonPointer, nameof(JsonPointer));
        ValidateJsonPointer(SourceTimestampJsonPointer, nameof(SourceTimestampJsonPointer));

        if (FreshnessTimeout.HasValue && FreshnessTimeout.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(FreshnessTimeout), "MQTT freshness timeout must be greater than zero when configured.");

        if (PayloadFormat != MqttPayloadFormat.Json && JsonPointer is not null)
            throw new InvalidOperationException("JSON Pointer can only be configured for JSON MQTT payloads.");

        if (PayloadFormat != MqttPayloadFormat.Json && SourceTimestampJsonPointer is not null)
            throw new InvalidOperationException("Source timestamp JSON Pointer can only be configured for JSON MQTT payloads.");

        if (SourceTimestampRequired && SourceTimestampJsonPointer is null)
            throw new InvalidOperationException("A required MQTT source timestamp needs a configured source timestamp JSON Pointer.");

        if (Writable)
        {
            if (Tag.ReadOnly)
                throw new InvalidOperationException($"MQTT TAG '{Tag.Path}' is read-only and cannot be configured for publish writes.");
            ValidateExactTopic(PublishTopic, nameof(PublishTopic));
        }
        else if (PublishTopic is not null || PublishRetain)
        {
            throw new InvalidOperationException("MQTT publish settings require Writable=true.");
        }
    }

    internal static void ValidateExactTopic(string? topic, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("MQTT topic is required.", parameterName);

        if (!string.Equals(topic, topic.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("MQTT topic must not contain surrounding whitespace.", parameterName);

        if (topic.IndexOf('\0') >= 0)
            throw new ArgumentException("MQTT topic must not contain a null character.", parameterName);

        if (topic.Contains('+', StringComparison.Ordinal) || topic.Contains('#', StringComparison.Ordinal))
            throw new ArgumentException(
                "Authoritative MQTT TAG mappings require an exact topic; wildcard filters are not persisted as TAG identity.",
                parameterName);

        if (Encoding.UTF8.GetByteCount(topic) > ushort.MaxValue)
            throw new ArgumentException("MQTT topic exceeds the protocol UTF-8 length limit.", parameterName);
    }

    private static void ValidateJsonPointer(string? pointer, string parameterName)
    {
        if (pointer is null || pointer.Length == 0) return;
        if (!pointer.StartsWith('/', StringComparison.Ordinal))
            throw new ArgumentException("JSON Pointer must be empty for the document root or start with '/'.", parameterName);
    }

    private static void ValidateQos(MqttQosLevel qos, string parameterName)
    {
        if (qos is < MqttQosLevel.AtMostOnce or > MqttQosLevel.ExactlyOnce)
            throw new ArgumentOutOfRangeException(parameterName, qos, "MQTT QoS must be 0, 1 or 2.");
    }
}
