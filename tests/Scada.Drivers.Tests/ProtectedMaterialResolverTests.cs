using System.Text;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Tests;

public sealed class ProtectedMaterialResolverTests
{
    [Fact]
    public async Task Resolver_RequiresExactScopeAndReturnsZeroingLease()
    {
        const string environmentVariable = "ELITESCADA_DRIVER_SECRET_TEST_MQTT";
        var registration = new CommunicationDriverProtectedMaterialRegistration(
            "mqtt-password-prod",
            "project-a",
            "mqtt.runtime",
            "mqtt.raw",
            "mqtt.password",
            environmentVariable,
            ContentType: "text/plain");
        var resolver = new EnvironmentCommunicationDriverProtectedMaterialResolver(
            [registration],
            name => name == environmentVariable ? "super-secret" : null);
        var request = new CommunicationDriverProtectedMaterialRequest(
            "project-a",
            "mqtt.runtime",
            "mqtt.raw",
            "mqtt.password",
            "mqtt-password-prod");

        var lease = await resolver.ResolveAsync(request);
        var material = lease.Material;
        Assert.Equal("super-secret", Encoding.UTF8.GetString(material.Span));
        Assert.Equal("text/plain", lease.ContentType);

        await lease.DisposeAsync();

        Assert.True(material.Span.ToArray().All(value => value == 0));
        Assert.True(lease.Material.IsEmpty);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await resolver.ResolveAsync(request with { DataSourceKey = "mqtt.other" });
        });
    }

    [Fact]
    public async Task Resolver_FailsClosedForUnknownMissingAndMalformedMaterial()
    {
        var resolver = new EnvironmentCommunicationDriverProtectedMaterialResolver(
            [new CommunicationDriverProtectedMaterialRegistration(
                "client-key",
                "project-a",
                "source-a",
                "opcua.client",
                "opcua.client-private-key",
                "ELITESCADA_DRIVER_SECRET_CLIENT_KEY",
                Encoding: "base64")],
            _ => "%%%not-base64%%%");

        var request = new CommunicationDriverProtectedMaterialRequest(
            "project-a",
            "source-a",
            "opcua.client",
            "opcua.client-private-key",
            "client-key");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await resolver.ResolveAsync(request);
        });
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            await resolver.ResolveAsync(request with { Reference = "unknown" });
        });

        Assert.Throws<ArgumentException>(() => new EnvironmentCommunicationDriverProtectedMaterialResolver(
            [new CommunicationDriverProtectedMaterialRegistration(
                "bad-env",
                "project-a",
                "source-a",
                "mqtt.raw",
                "mqtt.password",
                "PATH")],
            _ => "secret"));
    }
}
