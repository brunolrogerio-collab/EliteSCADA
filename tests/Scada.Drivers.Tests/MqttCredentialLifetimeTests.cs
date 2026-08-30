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

    [Fact]
    public void MaximumProtocolPasswordLengthIsAcceptedAndZeroized()
    {
        var buffer = Enumerable.Repeat((byte)0xA5, MqttResolvedCredentials.MaximumProtocolPasswordBytes).ToArray();
        var credentials = new MqttResolvedCredentials("operator", buffer);

        Assert.Equal(MqttResolvedCredentials.MaximumProtocolPasswordBytes, credentials.Password.Length);

        credentials.Dispose();

        Assert.All(buffer, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void OversizedProtocolPasswordIsRejectedAndZeroized()
    {
        var buffer = Enumerable.Repeat(
            (byte)0xA5,
            MqttResolvedCredentials.MaximumProtocolPasswordBytes + 1).ToArray();

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MqttResolvedCredentials("operator", buffer));

        Assert.Equal("password", error.ParamName);
        Assert.All(buffer, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void FailedUsernameValidationStillZeroizesOwnedPasswordBuffer()
    {
        var buffer = "sensitive-on-invalid-username"u8.ToArray();

        Assert.Throws<ArgumentException>(() =>
            new MqttResolvedCredentials("operator\0hidden", buffer));

        Assert.All(buffer, value => Assert.Equal((byte)0, value));
    }
}
