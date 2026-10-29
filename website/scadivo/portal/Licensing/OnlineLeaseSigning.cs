using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Scadivo.Portal.Licensing;

public sealed class OnlineLeaseSigningOptions
{
    public const string ApprovedKeyId = "scadivo-online-lease-2026-01";
    public const string ApprovedPublicKeyFingerprint = "26a33c58eeeb1329e8901b2722776545be5a8a266016cc44295bf115960d59aa";

    public bool Enabled { get; set; }
    public string KeyId { get; set; } = ApprovedKeyId;
    public string? PrivateKeyFile { get; set; }

    public bool IsValid(string contentRoot, string dataProtectionDirectory)
    {
        if (!string.Equals(KeyId, ApprovedKeyId, StringComparison.Ordinal)) return false;
        if (!Enabled) return true;
        if (string.IsNullOrWhiteSpace(PrivateKeyFile) || !Path.IsPathRooted(PrivateKeyFile)) return false;

        try
        {
            var path = Path.GetFullPath(PrivateKeyFile);
            if (!File.Exists(path) || IsWithin(path, Path.GetFullPath(contentRoot)) || IsWithin(path, Path.GetFullPath(dataProtectionDirectory))) return false;
            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(path));
            if (rsa.KeySize != 3072) return false;
            var fingerprint = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(fingerprint), Convert.FromHexString(ApprovedPublicKeyFingerprint));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static bool IsWithin(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return relative == "." || (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }
}

public sealed record OnlineLeaseClaims(
    int SchemaVersion,
    string LicenseId,
    string OrganizationId,
    string ApplicationId,
    string ActivationId,
    string MachineFingerprint,
    string Tier,
    int ViewOnlySeats,
    int InteractiveSeats,
    bool HaRuntime,
    bool AssetProcessIntelligence,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset NotAfterUtc,
    string KeyId);

public interface IOnlineLeaseSigner
{
    bool IsEnabled { get; }
    string Sign(OnlineLeaseClaims claims);
}

public sealed class RsaPssOnlineLeaseSigner : IOnlineLeaseSigner, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RSA? privateKey;
    private readonly string keyId;

    public RsaPssOnlineLeaseSigner(Microsoft.Extensions.Options.IOptions<OnlineLeaseSigningOptions> options)
    {
        var settings = options.Value;
        keyId = settings.KeyId;
        if (!settings.Enabled) return;

        privateKey = RSA.Create();
        try
        {
            privateKey.ImportFromPem(File.ReadAllText(settings.PrivateKeyFile!));
            if (privateKey.KeySize != 3072 || !FingerprintMatches(privateKey))
                throw new InvalidOperationException("The configured online signing key is not the approved key.");
        }
        catch
        {
            privateKey?.Dispose();
            throw new InvalidOperationException("The online signing key could not be loaded.");
        }
    }

    public bool IsEnabled => privateKey is not null;

    public string Sign(OnlineLeaseClaims claims)
    {
        if (privateKey is null) throw new InvalidOperationException("Online license issuance is disabled.");
        if (claims.SchemaVersion != 1 || !string.Equals(claims.KeyId, keyId, StringComparison.Ordinal))
            throw new InvalidOperationException("The online lease claims do not match the configured signing purpose.");
        var payload = JsonSerializer.SerializeToUtf8Bytes(claims, JsonOptions);
        var signature = privateKey.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return $"ESLEASE1.{WebEncoders.Base64UrlEncode(payload)}.{WebEncoders.Base64UrlEncode(signature)}";
    }

    private static bool FingerprintMatches(RSA rsa)
    {
        var actual = SHA256.HashData(rsa.ExportSubjectPublicKeyInfo());
        var expected = Convert.FromHexString(OnlineLeaseSigningOptions.ApprovedPublicKeyFingerprint);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public void Dispose() => privateKey?.Dispose();
}
