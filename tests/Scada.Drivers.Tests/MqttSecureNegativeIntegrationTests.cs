using System.Globalization;
using System.Text;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttSecureNegativeIntegrationTests
{
    private const string HostVariable = "ELITESCADA_MQTT_INTEGRATION_HOST";

    [Fact]
    [Trait("Category", "BrokerSecurityNegative")]
    [Trait("Scenario", "InvalidCredentials")]
    public async Task SecureBrokerRejectsInvalidCredentialsFailClosed()
    {
        var host = Environment.GetEnvironmentVariable(HostVariable);
        if (string.IsNullOrWhiteSpace(host))
            return;

        var port = ParsePortEnvironment("ELITESCADA_MQTT_INTEGRATION_PORT", 8883);
        var username = RequireEnvironment("ELITESCADA_MQTT_INTEGRATION_USERNAME");
        var rejectedPassword = RequireEnvironment("ELITESCADA_MQTT_INTEGRATION_PASSWORD");

        foreach (var protocol in ParseProtocols(Environment.GetEnvironmentVariable("ELITESCADA_MQTT_INTEGRATION_PROTOCOLS")))
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await using var transport = new MqttNetClientTransport();
            using var credentials = new MqttResolvedCredentials(
                username,
                Encoding.UTF8.GetBytes(rejectedPassword));

            var exception = await Assert.ThrowsAsync<MqttTransportException>(async () =>
                await transport.ConnectAsync(
                    CreateSettings(host, port, protocol, $"security-bad-auth-{ProtocolToken(protocol)}-{Guid.NewGuid():N}"),
                    credentials,
                    timeout.Token));

            Assert.False(transport.IsConnected);
            Assert.True(
                exception.IsPermanent,
                $"Invalid MQTT credentials must be classified as a permanent connection failure for {ProtocolToken(protocol)}.");
            Assert.False(
                exception.ToString().Contains(rejectedPassword, StringComparison.Ordinal),
                "MQTT connection diagnostics must not expose password material.");
        }
    }

    [Fact]
    [Trait("Category", "BrokerSecurityNegative")]
    [Trait("Scenario", "RevokedCertificate")]
    public async Task SecureBrokerRejectsRevokedCertificateFailClosed()
    {
        var host = Environment.GetEnvironmentVariable(HostVariable);
        if (string.IsNullOrWhiteSpace(host))
            return;

        var port = ParsePortEnvironment("ELITESCADA_MQTT_INTEGRATION_PORT", 8883);
        var username = RequireEnvironment("ELITESCADA_MQTT_INTEGRATION_USERNAME");
        var password = RequireEnvironment("ELITESCADA_MQTT_INTEGRATION_PASSWORD");

        foreach (var protocol in ParseProtocols(Environment.GetEnvironmentVariable("ELITESCADA_MQTT_INTEGRATION_PROTOCOLS")))
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await using var transport = new MqttNetClientTransport();
            using var credentials = new MqttResolvedCredentials(
                username,
                Encoding.UTF8.GetBytes(password));

            _ = await Assert.ThrowsAsync<MqttTransportException>(async () =>
                await transport.ConnectAsync(
                    CreateSettings(host, port, protocol, $"security-revoked-cert-{ProtocolToken(protocol)}-{Guid.NewGuid():N}"),
                    credentials,
                    timeout.Token));

            Assert.False(transport.IsConnected);
        }
    }

    private static MqttConnectionSettings CreateSettings(
        string host,
        int port,
        MqttProtocolMode protocol,
        string clientId) =>
        new(
            host.Trim(),
            port,
            UseTls: true,
            clientId,
            ProtocolMode: protocol,
            ConnectTimeout: TimeSpan.FromSeconds(10),
            ReconnectMinimumDelay: TimeSpan.FromMilliseconds(100),
            ReconnectMaximumDelay: TimeSpan.FromSeconds(1),
            CleanSession: protocol == MqttProtocolMode.Mqtt311,
            CleanStart: protocol == MqttProtocolMode.Mqtt5,
            SessionExpirySeconds: protocol == MqttProtocolMode.Mqtt5 ? 0U : null,
            MaximumInboundPayloadBytes: 64 * 1024,
            MaximumBufferedMessages: 8);

    private static IReadOnlyList<MqttProtocolMode> ParseProtocols(string? value)
    {
        var tokens = string.IsNullOrWhiteSpace(value)
            ? new[] { "mqtt5", "mqtt311" }
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var protocols = new List<MqttProtocolMode>(tokens.Length);
        foreach (var token in tokens)
        {
            protocols.Add(token.ToLowerInvariant() switch
            {
                "mqtt5" or "5" or "5.0" => MqttProtocolMode.Mqtt5,
                "mqtt311" or "3.1.1" or "311" => MqttProtocolMode.Mqtt311,
                _ => throw new InvalidOperationException(
                    $"Unsupported MQTT integration protocol '{token}'. Use mqtt5 and/or mqtt311.")
            });
        }

        return protocols.Distinct().ToArray();
    }

    private static int ParsePortEnvironment(string name, int defaultValue)
    {
        var text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(text))
            return defaultValue;

        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
            parsed is >= 1 and <= 65535)
        {
            return parsed;
        }

        throw new InvalidOperationException($"Environment variable '{name}' must be a TCP port from 1 to 65535.");
    }

    private static string RequireEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Environment variable '{name}' is required for secure negative MQTT integration tests.")
            : value;
    }

    private static string ProtocolToken(MqttProtocolMode protocol) => protocol switch
    {
        MqttProtocolMode.Mqtt5 => "mqtt5",
        MqttProtocolMode.Mqtt311 => "mqtt311",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null)
    };
}
