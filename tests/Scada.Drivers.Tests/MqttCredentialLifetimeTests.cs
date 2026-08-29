using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttCredentialLifetimeTests
{
    [Fact]
    public void Dispose_ZerosOwnedPasswordBufferAndPreventsFurtherAccess()
    {
        var buffer = "ephemeral-secret"u8.ToArray();
        var credentials = new MqttResolvedCredentials("operator", buffer);

        credentials.Dispose();

        Assert.All(buffer, value => Assert.Equal((byte)0, value));
        Assert.Throws<ObjectDisposedException>(() => _ = credentials.Password);
    }
}
