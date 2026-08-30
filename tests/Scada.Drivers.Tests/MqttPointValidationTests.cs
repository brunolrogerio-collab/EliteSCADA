using Scada.Core.Tags;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttPointValidationTests
{
    [Fact]
    public void UndefinedPayloadFormatIsRejectedBeforeCodecSelection()
    {
        var point = new MqttPoint(
            CreateTag(),
            "plant/validation/payload-format",
            PayloadFormat: (MqttPayloadFormat)int.MaxValue);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => point.Validate());

        Assert.Equal("PayloadFormat", error.ParamName);
    }

    [Fact]
    public void UndefinedRetainedPolicyCannotFallThroughToGoodQuality()
    {
        var point = new MqttPoint(
            CreateTag(),
            "plant/validation/retained-policy",
            RetainedValuePolicy: (MqttRetainedValuePolicy)int.MaxValue);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MqttPayloadCodec.Decode(
                point,
                "12.5"u8,
                retained: true,
                DateTimeOffset.UtcNow));

        Assert.Equal("RetainedValuePolicy", error.ParamName);
    }

    [Fact]
    public void UndefinedPublishQosIsRejectedEvenWhenPointIsCurrentlyReadOnly()
    {
        var point = new MqttPoint(
            CreateTag(),
            "plant/validation/publish-qos",
            PublishQos: (MqttQosLevel)int.MaxValue);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => point.Validate());

        Assert.Equal("PublishQos", error.ParamName);
    }

    private static TagDefinition CreateTag() => new(
        Guid.NewGuid(),
        "Value",
        $"Plant.Validation.Value.{Guid.NewGuid():N}",
        TagDataType.Double,
        "mqtt.raw:validation",
        null,
        null,
        true);
}
