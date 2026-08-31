using Scada.Security.Licensing;

namespace Scada.Security.Tests;

public sealed class ProductLicenseCryptographyTests
{
    private const string HardwareA = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string HardwareB = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(1500)]
    [InlineData(3000)]
    [InlineData(5000)]
    public void SignedFiniteLicense_ValidatesOnlyForRequestedHardware(int maxTags)
    {
        var (privateKey, publicKey) = ProductLicenseCryptography.GenerateSigningKeyPair();
        var request = ProductLicenseCryptography.CreateHardwareRequestCode(HardwareA);
        var code = ProductLicenseCryptography.IssueLicense(
            privateKey,
            request,
            $"test-{maxTags}",
            maxTags,
            false,
            DateTimeOffset.Parse("2026-08-31T12:00:00Z"),
            "Test Customer");

        var valid = ProductLicenseCryptography.ValidateLicense(code, publicKey, HardwareA);
        var wrongMachine = ProductLicenseCryptography.ValidateLicense(code, publicKey, HardwareB);

        Assert.True(valid.Valid);
        Assert.Equal(maxTags, valid.License!.MaxTags);
        Assert.False(valid.License.UnlimitedTags);
        Assert.Equal("Test Customer", valid.License.Customer);
        Assert.False(wrongMachine.Valid);
        Assert.Contains("different hardware", wrongMachine.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnlimitedLicense_ValidatesWithoutFiniteTagLimit()
    {
        var (privateKey, publicKey) = ProductLicenseCryptography.GenerateSigningKeyPair();
        var request = ProductLicenseCryptography.CreateHardwareRequestCode(HardwareA);
        var code = ProductLicenseCryptography.IssueLicense(
            privateKey,
            request,
            "unlimited-1",
            null,
            true,
            DateTimeOffset.UtcNow);

        var result = ProductLicenseCryptography.ValidateLicense(code, publicKey, HardwareA);

        Assert.True(result.Valid);
        Assert.True(result.License!.UnlimitedTags);
        Assert.Null(result.License.MaxTags);
    }

    [Fact]
    public void TamperedLicense_FailsSignatureValidation()
    {
        var (privateKey, publicKey) = ProductLicenseCryptography.GenerateSigningKeyPair();
        var request = ProductLicenseCryptography.CreateHardwareRequestCode(HardwareA);
        var code = ProductLicenseCryptography.IssueLicense(
            privateKey,
            request,
            "test-500",
            500,
            false,
            DateTimeOffset.UtcNow);

        var parts = code.Split('.');
        var payload = parts[1];
        var changed = payload[..^1] + (payload[^1] == 'A' ? 'B' : 'A');
        var tampered = $"{parts[0]}.{changed}.{parts[2]}";

        var result = ProductLicenseCryptography.ValidateLicense(tampered, publicKey, HardwareA);

        Assert.False(result.Valid);
    }

    [Fact]
    public void HardwareRequestCode_IsDeterministicAndContainsOnlyFingerprintPayload()
    {
        var first = ProductLicenseCryptography.CreateHardwareRequestCode(HardwareA.ToLowerInvariant());
        var second = ProductLicenseCryptography.CreateHardwareRequestCode(HardwareA);
        var parsed = ProductLicenseCryptography.ParseHardwareRequestCode(first);

        Assert.Equal(first, second);
        Assert.Equal(HardwareA, parsed.HardwareFingerprint);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(250)]
    [InlineData(6000)]
    public void Issuer_RejectsUnsupportedCommercialTagTiers(int maxTags)
    {
        var (privateKey, _) = ProductLicenseCryptography.GenerateSigningKeyPair();
        var request = ProductLicenseCryptography.CreateHardwareRequestCode(HardwareA);

        Assert.Throws<ArgumentException>(() => ProductLicenseCryptography.IssueLicense(
            privateKey,
            request,
            "unsupported",
            maxTags,
            false,
            DateTimeOffset.UtcNow));
    }
}
