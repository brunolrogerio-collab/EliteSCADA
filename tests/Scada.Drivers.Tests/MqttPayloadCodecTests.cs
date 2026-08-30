using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttPayloadCodecTests
{
    [Fact]
    public void PointValidationRejectsWildcardAsCanonicalTagIdentity()
    {
        var point = new MqttPoint(CreateTag(TagDataType.Double), "plant/+/temperature");

        var error = Assert.Throws<ArgumentException>(point.Validate);

        Assert.Contains("exact topic", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Utf8ScalarDecodeIsTypedAndInvariant()
    {
        var point = new MqttPoint(CreateTag(TagDataType.Double), "plant/tank/level");

        var decoded = MqttPayloadCodec.Decode(
            point,
            Encoding.UTF8.GetBytes("12.75"),
            retained: false,
            DateTimeOffset.Parse("2026-08-29T12:00:00Z"));

        Assert.Equal(12.75d, decoded.Value);
        Assert.Equal(TagQuality.Good, decoded.Quality);
        Assert.Null(decoded.SourceTimestamp);
        Assert.False(decoded.Retained);
    }

    [Fact]
    public void BooleanPayloadDoesNotSilentlyCoerceNumericText()
    {
        var point = new MqttPoint(CreateTag(TagDataType.Boolean), "plant/pump/running");

        Assert.Throws<MqttPayloadException>(() => MqttPayloadCodec.Decode(
            point,
            Encoding.UTF8.GetBytes("1"),
            retained: false,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void DateTimeScalarRequiresExplicitUtcOrOffset()
    {
        var point = new MqttPoint(CreateTag(TagDataType.DateTime), "plant/event/time");

        var error = Assert.Throws<MqttPayloadException>(() => MqttPayloadCodec.Decode(
            point,
            Encoding.UTF8.GetBytes("2026-08-29T16:15:00"),
            retained: false,
            DateTimeOffset.UtcNow));

        Assert.Contains("explicit", error.Message, StringComparison.OrdinalIgnoreCase);

        var decoded = MqttPayloadCodec.Decode(
            point,
            Encoding.UTF8.GetBytes("2026-08-29T16:15:00-03:00"),
            retained: false,
            DateTimeOffset.UtcNow);

        Assert.Equal(DateTimeOffset.Parse("2026-08-29T19:15:00Z"), decoded.Value);
    }

    [Fact]
    public void JsonPointerExtractsValueAndSourceTimestamp()
    {
        var point = new MqttPoint(
            CreateTag(TagDataType.Double),
            "plant/tank/state",
            MqttPayloadFormat.Json,
            JsonPointer: "/process/level",
            SourceTimestampJsonPointer: "/process/sourceTime",
            SourceTimestampRequired: true);

        var payload = Encoding.UTF8.GetBytes(
            "{\"process\":{\"level\":63.5,\"sourceTime\":\"2026-08-29T14:01:02Z\"}}");

        var decoded = MqttPayloadCodec.Decode(
            point,
            payload,
            retained: true,
            DateTimeOffset.Parse("2026-08-29T14:01:03Z"));

        Assert.Equal(63.5d, decoded.Value);
        Assert.Equal(DateTimeOffset.Parse("2026-08-29T14:01:02Z"), decoded.SourceTimestamp);
        Assert.Equal(TagQuality.Good, decoded.Quality);
        Assert.True(decoded.Retained);
    }

    [Fact]
    public void JsonSourceTimestampRequiresExplicitUtcOrOffset()
    {
        var point = new MqttPoint(
            CreateTag(TagDataType.Double),
            "plant/tank/state",
            MqttPayloadFormat.Json,
            JsonPointer: "/value",
            SourceTimestampJsonPointer: "/timestamp",
            SourceTimestampRequired: true);

        var error = Assert.Throws<MqttPayloadException>(() => MqttPayloadCodec.Decode(
            point,
            Encoding.UTF8.GetBytes("{\"value\":1.5,\"timestamp\":\"2026-08-29T14:01:02\"}"),
            retained: false,
            DateTimeOffset.UtcNow));

        Assert.Contains("explicit", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RetainedPayloadWithoutSourceTimestampIsStaleByDefault()
    {
        var point = new MqttPoint(CreateTag(TagDataType.Int32), "plant/counter");

        var decoded = MqttPayloadCodec.Decode(
            point,
            Encoding.UTF8.GetBytes("42"),
            retained: true,
            DateTimeOffset.UtcNow);

        Assert.Equal(42, decoded.Value);
        Assert.Equal(TagQuality.Stale, decoded.Quality);
    }

    [Fact]
    public void JsonPointerSupportsRfc6901Escapes()
    {
        var point = new MqttPoint(
            CreateTag(TagDataType.Int32),
            "plant/special",
            MqttPayloadFormat.Json,
            JsonPointer: "/a~1b/c~0d");

        var decoded = MqttPayloadCodec.Decode(
            point,
            Encoding.UTF8.GetBytes("{\"a/b\":{\"c~d\":7}}"),
            retained: false,
            DateTimeOffset.UtcNow);

        Assert.Equal(7, decoded.Value);
    }

    [Fact]
    public void JsonArrayPointerRejectsLeadingZeroIndex()
    {
        var point = new MqttPoint(
            CreateTag(TagDataType.Int32),
            "plant/array",
            MqttPayloadFormat.Json,
            JsonPointer: "/values/01");

        var error = Assert.Throws<MqttPayloadException>(() => MqttPayloadCodec.Decode(
            point,
            Encoding.UTF8.GetBytes("{\"values\":[10,20]}"),
            retained: false,
            DateTimeOffset.UtcNow));

        Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingConfiguredJsonFieldFailsClosed()
    {
        var point = new MqttPoint(
            CreateTag(TagDataType.Double),
            "plant/tank/state",
            MqttPayloadFormat.Json,
            JsonPointer: "/missing/value");

        var error = Assert.Throws<MqttPayloadException>(() => MqttPayloadCodec.Decode(
            point,
            Encoding.UTF8.GetBytes("{\"process\":{\"level\":1.0}}"),
            retained: false,
            DateTimeOffset.UtcNow));

        Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteEncodingRequiresExplicitWritablePointAndExactTagType()
    {
        var tag = CreateTag(TagDataType.Int32, readOnly: false);
        var point = new MqttPoint(
            tag,
            "plant/setpoint/readback",
            Writable: true,
            PublishTopic: "plant/setpoint/command");

        Assert.Equal("125", Encoding.UTF8.GetString(MqttPayloadCodec.Encode(point, 125)));
        Assert.Throws<MqttPayloadException>(() => MqttPayloadCodec.Encode(point, 125L));
    }

    [Fact]
    public void DateTimeWriteRejectsUnspecifiedKind()
    {
        var point = new MqttPoint(
            CreateTag(TagDataType.DateTime, readOnly: false),
            "plant/time/readback",
            Writable: true,
            PublishTopic: "plant/time/command");
        var ambiguous = new DateTime(2026, 8, 29, 16, 30, 0, DateTimeKind.Unspecified);

        var error = Assert.Throws<MqttPayloadException>(() => MqttPayloadCodec.Encode(point, ambiguous));

        Assert.Contains("ambiguous", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JsonFieldExtractionDoesNotInventWriteEnvelope()
    {
        var point = new MqttPoint(
            CreateTag(TagDataType.Double, readOnly: false),
            "plant/state",
            MqttPayloadFormat.Json,
            JsonPointer: "/value",
            Writable: true,
            PublishTopic: "plant/command");

        var error = Assert.Throws<InvalidOperationException>(() => MqttPayloadCodec.Encode(point, 10d));

        Assert.Contains("envelope", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TagDefinition CreateTag(TagDataType dataType, bool readOnly = true) => new(
        Guid.NewGuid(),
        "TestTag",
        "Plant.TestTag",
        dataType,
        "mqtt.raw:test",
        null,
        null,
        readOnly);
}