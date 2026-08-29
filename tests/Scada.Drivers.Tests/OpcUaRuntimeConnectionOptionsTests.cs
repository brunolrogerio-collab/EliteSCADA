using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaRuntimeConnectionOptionsTests
{
    [Fact]
    public void Validate_AcceptsSecureAnonymousConfigurationUsingReferencesOnly()
    {
        var options = new OpcUaRuntimeConnectionOptions(
            "opc.tcp://plc01:4840",
            "SignAndEncrypt",
            "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256",
            ClientCertificateReference: "cert://machine/opcua-client",
            ApprovedServerApplicationUri: "urn:vendor:server",
            ApprovedServerCertificateSha256: "AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99");

        options.Validate();

        Assert.Equal(64, options.NormalizedApprovedServerCertificateSha256?.Length);
        Assert.Equal(TimeSpan.FromMinutes(1), options.EffectiveSessionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), options.EffectivePublishingInterval);
    }

    [Fact]
    public void Validate_UserNameRequiresOpaquePasswordReference()
    {
        var options = SecureOptions() with
        {
            AuthenticationMode = OpcUaRuntimeAuthenticationMode.UserName,
            UserName = "operator",
            PasswordSecretReference = null
        };

        var error = Assert.Throws<ArgumentException>(options.Validate);
        Assert.Contains("secret reference", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_CertificateAuthenticationRequiresUserCertificateReference()
    {
        var options = SecureOptions() with
        {
            AuthenticationMode = OpcUaRuntimeAuthenticationMode.Certificate,
            UserCertificateReference = null
        };

        var error = Assert.Throws<ArgumentException>(options.Validate);
        Assert.Contains("user certificate reference", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_SecureModeRequiresClientApplicationCertificateReference()
    {
        var options = SecureOptions() with { ClientCertificateReference = null };

        var error = Assert.Throws<ArgumentException>(options.Validate);
        Assert.Contains("client application certificate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_DoesNotAllowSilentSecurityModeAndPolicyMismatch()
    {
        var noneWithSecurePolicy = new OpcUaRuntimeConnectionOptions(
            "opc.tcp://localhost:4840",
            "None",
            "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256");
        var secureWithNonePolicy = SecureOptions() with
        {
            SecurityPolicyUri = "http://opcfoundation.org/UA/SecurityPolicy#None"
        };

        Assert.Throws<ArgumentException>(noneWithSecurePolicy.Validate);
        Assert.Throws<ArgumentException>(secureWithNonePolicy.Validate);
    }

    [Fact]
    public void Validate_RejectsOutOfRangeSessionAndPublishingIntervals()
    {
        var shortSession = SecureOptions() with { SessionTimeout = TimeSpan.FromSeconds(1) };
        var fastPublishing = SecureOptions() with { PublishingInterval = TimeSpan.FromMilliseconds(10) };

        Assert.Throws<ArgumentOutOfRangeException>(shortSession.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(fastPublishing.Validate);
    }

    private static OpcUaRuntimeConnectionOptions SecureOptions() => new(
        "opc.tcp://localhost:4840",
        "SignAndEncrypt",
        "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256",
        ClientCertificateReference: "cert://machine/opcua-client");
}
