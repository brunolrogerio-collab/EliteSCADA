using System.Security.Cryptography;
using Scada.Core.Product.Licensing;

namespace Scada.Core.Tests;

public sealed class ProductLicenseTrustAnchorsTests
{
    [Fact]
    public void OnlinePublicKey_HasSeparatePinnedIdentityAndNoPrivateMaterial()
    {
        using var key = ProductLicenseTrustAnchors.CreateOnlineLeasePublicKey();
        Assert.Equal("scadivo-online-lease-2026-01", ProductLicenseTrustAnchors.OnlineLeaseKeyId);
        Assert.Equal(3072, key.KeySize);
        Assert.Equal(ProductLicenseTrustAnchors.OnlineLeasePublicKeySha256,
            Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo())).ToLowerInvariant());
        Assert.Throws<CryptographicException>(() => key.ExportParameters(includePrivateParameters: true));
    }

    [Fact]
    public void ProductionPublicKey_HasPinnedIdentitySizeAndFingerprint()
    {
        using var key = ProductLicenseTrustAnchors.CreateProductionPublicKey();

        var fingerprint = Convert.ToHexString(
            SHA256.HashData(key.ExportSubjectPublicKeyInfo())).ToLowerInvariant();

        Assert.Equal("elite-prod-2026-01", ProductLicenseTrustAnchors.ProductionKeyId);
        Assert.Equal(3072, key.KeySize);
        Assert.Equal(ProductLicenseTrustAnchors.ProductionPublicKeySha256, fingerprint);
    }

    [Fact]
    public void BuiltInVerificationKeys_ContainsProductionAuthority()
    {
        var keys = ProductLicenseTrustAnchors.CreateBuiltInVerificationKeys();
        try
        {
            Assert.True(keys.TryGetValue(ProductLicenseTrustAnchors.ProductionKeyId, out var key));
            Assert.NotNull(key);
            Assert.Equal(3072, key.KeySize);
            Assert.Single(keys);
            Assert.False(keys.ContainsKey(ProductLicenseTrustAnchors.OnlineLeaseKeyId));
        }
        finally
        {
            foreach (var key in keys.Values.Distinct())
                key.Dispose();
        }
    }
}
