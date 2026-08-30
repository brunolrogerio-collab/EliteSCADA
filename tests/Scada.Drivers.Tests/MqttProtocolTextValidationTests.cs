using Scada.Core.Tags;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttProtocolTextValidationTests
{
    [Fact]
    public void ExactTopicRejectsIllFormedUtf16InsteadOfReplacementEncoding()
    {
        var tag = TagDefinition.Create(
            "Temperature",
            "Plant.Temperature",
            TagDataType.Double,
            source: "mqtt-main");
        var point = new MqttPoint(tag, "plant/\uD800/temperature");

        Assert.Throws<ArgumentException>(() => point.Validate());
    }

    [Fact]
    public void ClientIdRejectsIllFormedUtf16AndProtocolByteOverflow()
    {
        var invalidUnicode = new MqttConnectionSettings(
            "broker.example.invalid",
            1883,
            false,
            "elite-\uD800");
        var oversized = new MqttConnectionSettings(
            "broker.example.invalid",
            1883,
            false,
            new string('\u00E9', 32_768));

        Assert.Throws<ArgumentException>(() => invalidUnicode.Validate());
        Assert.Throws<ArgumentException>(() => oversized.Validate());
    }

    [Fact]
    public void UsernameRejectsMalformedProtocolTextButPasswordRemainsBinary()
    {
        Assert.Throws<ArgumentException>(() => new MqttResolvedCredentials("operator\uD800"));
        Assert.Throws<ArgumentException>(() => new MqttResolvedCredentials("operator\0hidden"));

        using var credentials = new MqttResolvedCredentials(
            "operator",
            new byte[] { 0x00, 0xFF, 0x80 });

        Assert.Equal(new byte[] { 0x00, 0xFF, 0x80 }, credentials.Password.ToArray());
    }

    [Fact]
    public void ValidAstralUnicodeRemainsValidProtocolText()
    {
        var tag = TagDefinition.Create(
            "Temperature",
            "Plant.Temperature",
            TagDataType.Double,
            source: "mqtt-main");
        var point = new MqttPoint(tag, "plant/\U0001F680/temperature");
        var settings = new MqttConnectionSettings(
            "broker.example.invalid",
            1883,
            false,
            "elite-\U0001F680");

        point.Validate();
        settings.Validate();
        using var credentials = new MqttResolvedCredentials("operator-\U0001F680");
    }
}
