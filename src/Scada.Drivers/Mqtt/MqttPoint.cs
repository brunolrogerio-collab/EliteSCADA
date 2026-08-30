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
        if (Tag.Id == Guid.Empty)
            throw new ArgumentException("MQTT point requires a canonical TAG with a non-empty ID.", nameof(Tag));
        if (string.IsNullOrWhiteSpace(Tag.Name))
            throw new ArgumentException("MQTT point requires a canonical TAG name.", nameof(Tag));
        if (string.IsNullOrWhiteSpace(Tag.Path))
            throw new ArgumentException("MQTT point requires a canonical TAG path.", nameof(Tag));

        ValidateExactTopic(SubscribeTopic, nameof(SubscribeTopic));
        ValidateQos(Qos, nameof(Qos));

        if (PayloadFormat == MqttPayloadFormat.Utf8Scalar && JsonPointer is not null)
            throw new ArgumentException("JSON Pointer can only be configured for JSON payload format.", nameof(JsonPointer));
        if (PayloadFormat == MqttPayloadFormat.Utf8Scalar && SourceTimestampJsonPointer is not null)
            throw new ArgumentException("Source timestamp JSON Pointer can only be configured for JSON payload format.", nameof(SourceTimestampJsonPointer));

        ValidateJsonPointer(JsonPointer, nameof(JsonPointer));
        ValidateJsonPointer(SourceTimestampJsonPointer, nameof(SourceTimestampJsonPointer));
        if (SourceTimestampRequired && SourceTimestampJsonPointer is null)
            throw new ArgumentException("SourceTimestampRequired requires SourceTimestampJsonPointer.", nameof(SourceTimestampRequired));
        if (FreshnessTimeout.HasValue && FreshnessTimeout.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(FreshnessTimeout), "MQTT freshness timeout must be greater than zero.");

        if (Writable)
        {
            if (Tag.ReadOnly)
                throw new ArgumentException("Writable MQTT points require a writable canonical TAG.", nameof(Tag));
            ValidateExactTopic(PublishTopic, nameof(PublishTopic));
            ValidateQos(PublishQos, nameof(PublishQos));
        }
        else
        {
            if (PublishTopic is not null)
                throw new ArgumentException("Publish topic is only valid for writable MQTT points.", nameof(PublishTopic));
            if (PublishRetain)
                throw new ArgumentException("Publish retain is only valid for writable MQTT points.", nameof(PublishRetain));
        }
    }

    internal static void ValidateExactTopic(string? topic, string parameterName)
    {
        if (string.IsNullOrEmpty(topic))
            throw new ArgumentException("MQTT topic is required.", parameterName);
        if (!string.Equals(topic, topic.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("MQTT topic must not contain surrounding whitespace.", parameterName);

        MqttProtocolText.ValidateUtf8EncodedString(topic, parameterName, allowEmpty: false);

        if (topic.Contains('+', StringComparison.Ordinal) || topic.Contains('#', StringComparison.Ordinal))
            throw new ArgumentException(
                "Authoritative MQTT TAG mappings require an exact topic; wildcard filters are not persisted as TAG identity.",
                parameterName);
    }

    private static void ValidateJsonPointer(string? pointer, string parameterName)
    {
        if (pointer is null || pointer.Length == 0) return;
        if (!pointer.StartsWith("/", StringComparison.Ordinal))
            throw new ArgumentException("JSON Pointer must be empty for the document root or start with '/'.", parameterName);
    }

    private static void ValidateQos(MqttQosLevel qos, string parameterName)
    {
        if (qos is < MqttQosLevel.AtMostOnce or > MqttQosLevel.ExactlyOnce)
            throw new ArgumentOutOfRangeException(parameterName, qos, "MQTT QoS must be 0, 1 or 2.");
    }
}
