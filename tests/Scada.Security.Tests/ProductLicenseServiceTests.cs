using Scada.Core.Product;
using Scada.Security.Licensing;

namespace Scada.Security.Tests;

public sealed class ProductLicenseServiceTests : IDisposable
{
    private const string HardwareA = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string HardwareB = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "EliteScadaLicenseTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void MissingLicense_IsDemoWithTwoHundredTagsAndThreeHundredMinutes()
    {
        var service = CreateService(HardwareA, publicKeyPem: null);

        var snapshot = service.Current();
        var atLimit = service.EvaluateRuntime(ProductLicensePolicy.DemoMaxTags);
        var overLimit = service.EvaluateRuntime(ProductLicensePolicy.DemoMaxTags + 1);

        Assert.Equal(ProductLicenseMode.Demo, snapshot.Mode);
        Assert.Equal(ProductLicensePolicy.DemoMaxTags, snapshot.MaxTags);
        Assert.Equal(ProductLicensePolicy.DemoMaxContinuousRuntime, snapshot.MaxContinuousRuntime);
        Assert.True(atLimit.Allowed);
        Assert.Equal(ProductLicensePolicy.DemoMaxContinuousRuntime, atLimit.MaxContinuousRuntime);
        Assert.False(overLimit.Allowed);
        Assert.Equal(ProductLicensePolicy.DemoTagLimitIssueCode, overLimit.IssueCode);
    }

    [Fact]
    public void ValidFiniteLicense_RemovesTimeLimitAndEnforcesSignedTagTier()
    {
        var (privateKey, publicKey) = ProductLicenseCryptography.GenerateSigningKeyPair();
        var service = CreateService(HardwareA, publicKey);
        var request = service.Current().HardwareRequestCode;
        var license = ProductLicenseCryptography.IssueLicense(
            privateKey,
            request,
            "customer-1000",
            1000,
            false,
            DateTimeOffset.UtcNow,
            "Customer A");

        var install = service.Install(license);
        var atLimit = service.EvaluateRuntime(1000);
        var overLimit = service.EvaluateRuntime(1001);

        Assert.True(install.Installed);
        Assert.Equal(ProductLicenseMode.Licensed, install.License.Mode);
        Assert.Null(install.License.MaxContinuousRuntime);
        Assert.True(atLimit.Allowed);
        Assert.Null(atLimit.MaxContinuousRuntime);
        Assert.False(overLimit.Allowed);
        Assert.Equal(ProductLicensePolicy.LicenseTagLimitIssueCode, overLimit.IssueCode);
    }

    [Fact]
    public void UnlimitedLicense_RemovesBothRuntimeAndTagLimits()
    {
        var (privateKey, publicKey) = ProductLicenseCryptography.GenerateSigningKeyPair();
        var service = CreateService(HardwareA, publicKey);
        var license = ProductLicenseCryptography.IssueLicense(
            privateKey,
            service.Current().HardwareRequestCode,
            "unlimited",
            null,
            true,
            DateTimeOffset.UtcNow);

        Assert.True(service.Install(license).Installed);
        var permit = service.EvaluateRuntime(100_000);

        Assert.True(permit.Allowed);
        Assert.True(permit.License.UnlimitedTags);
        Assert.Null(permit.MaxContinuousRuntime);
    }

    [Fact]
    public void InstalledLicenseForAnotherMachine_BlocksRuntimeInsteadOfFallingBackToDemo()
    {
        var (privateKey, publicKey) = ProductLicenseCryptography.GenerateSigningKeyPair();
        var issuerMachine = CreateService(HardwareA, publicKey);
        var otherRequest = issuerMachine.Current().HardwareRequestCode;
        var license = ProductLicenseCryptography.IssueLicense(
            privateKey,
            otherRequest,
            "wrong-machine",
            500,
            false,
            DateTimeOffset.UtcNow);

        Directory.CreateDirectory(_directory);
        File.WriteAllText(LicensePath(), license);
        var runtimeMachine = CreateService(HardwareB, publicKey);
        var permit = runtimeMachine.EvaluateRuntime(1);

        Assert.False(permit.Allowed);
        Assert.Equal(ProductLicenseMode.Invalid, permit.License.Mode);
        Assert.Equal(ProductLicensePolicy.InvalidLicenseIssueCode, permit.IssueCode);
    }

    [Fact]
    public void InvalidInstall_DoesNotOverwriteExistingValidLicense()
    {
        var (privateKey, publicKey) = ProductLicenseCryptography.GenerateSigningKeyPair();
        var service = CreateService(HardwareA, publicKey);
        var valid = ProductLicenseCryptography.IssueLicense(
            privateKey,
            service.Current().HardwareRequestCode,
            "valid-500",
            500,
            false,
            DateTimeOffset.UtcNow);
        Assert.True(service.Install(valid).Installed);

        var rejected = service.Install("ESLIC1.invalid.invalid");

        Assert.False(rejected.Installed);
        Assert.Equal("valid-500", service.Current().LicenseId);
    }

    private FileProductLicenseService CreateService(string fingerprint, string? publicKeyPem) =>
        new(new FixedHardware(fingerprint), LicensePath(), publicKeyPem);

    private string LicensePath() => Path.Combine(_directory, "license.escadalicense");

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class FixedHardware(string fingerprint) : IHardwareFingerprintProvider
    {
        public string GetHardwareFingerprint() => fingerprint;
        public string GetHardwareRequestCode() => ProductLicenseCryptography.CreateHardwareRequestCode(fingerprint);
    }
}
