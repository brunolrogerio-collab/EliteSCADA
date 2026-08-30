using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Tests;

public sealed class CommunicationDriverProtectedMaterialTests
{
    [Fact]
    public void Request_RequiresCompleteExplicitScope()
    {
        var request = new CommunicationDriverProtectedMaterialRequest(
            ProjectKey: "project-1",
            DataSourceKey: "mqtt-primary",
            DriverType: "mqtt.industrial",
            Purpose: "mqtt.password",
            Reference: "secret://mqtt-primary/password");

        request.Validate();
    }

    [Theory]
    [InlineData("", "source", "mqtt.industrial", "mqtt.password", "secret://password")]
    [InlineData("project", "", "mqtt.industrial", "mqtt.password", "secret://password")]
    [InlineData("project", "source", "", "mqtt.password", "secret://password")]
    [InlineData("project", "source", "mqtt.industrial", "", "secret://password")]
    [InlineData("project", "source", "mqtt.industrial", "mqtt.password", "")]
    public void Request_RejectsMissingScope(
        string projectKey,
        string dataSourceKey,
        string driverType,
        string purpose,
        string reference)
    {
        var request = new CommunicationDriverProtectedMaterialRequest(
            projectKey,
            dataSourceKey,
            driverType,
            purpose,
            reference);

        Assert.Throws<ArgumentException>(request.Validate);
    }

    [Fact]
    public void Request_RejectsWhitespaceNormalizationAmbiguity()
    {
        var request = new CommunicationDriverProtectedMaterialRequest(
            ProjectKey: "project-1",
            DataSourceKey: "source-1",
            DriverType: " opcua.client ",
            Purpose: "opcua.password",
            Reference: "secret://opcua/password");

        Assert.Throws<ArgumentException>(request.Validate);
    }

    [Fact]
    public void Request_RejectsControlCharactersInReference()
    {
        var request = new CommunicationDriverProtectedMaterialRequest(
            ProjectKey: "project-1",
            DataSourceKey: "source-1",
            DriverType: "mqtt.industrial",
            Purpose: "mqtt.password",
            Reference: "secret://mqtt/password\nforged-diagnostic");

        Assert.Throws<ArgumentException>(request.Validate);
    }
}
