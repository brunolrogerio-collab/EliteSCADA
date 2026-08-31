using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Core.Product;

namespace Scada.Security.Licensing;

public sealed record HardwareLicenseRequest(
    int Version,
    string HardwareFingerprint);

public sealed record ProductLicensePayload(
    int Version,
    string LicenseId,
    string HardwareFingerprint,
    int? MaxTags,
    bool UnlimitedTags,
    DateTimeOffset IssuedAtUtc,
    string? Customer = null);

public sealed record ProductLicenseValidationResult(
    bool Valid,
    ProductLicensePayload? License,
    string? Error = null);

public static class ProductLicenseCryptography
{
    public const string RequestPrefix = "ESREQ1";
    public const string LicensePrefix = "ESLIC1";
    private const int ContractVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string CreateHardwareRequestCode(string hardwareFingerprint)
    {
        var fingerprint = NormalizeFingerprint(hardwareFingerprint);
        var payload = new HardwareLicenseRequest(ContractVersion, fingerprint);
        return $"{RequestPrefix}.{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions))}";
    }

    public static HardwareLicenseRequest ParseHardwareRequestCode(string requestCode)
    {
        if (string.IsNullOrWhiteSpace(requestCode))
            throw new FormatException("Hardware request code is required.");

        var parts = requestCode.Trim().Split('.');
        if (parts.Length != 2 || !parts[0].Equals(RequestPrefix, StringComparison.Ordinal))
            throw new FormatException("Hardware request code has an unsupported format.");

        HardwareLicenseRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<HardwareLicenseRequest>(Base64UrlDecode(parts[1]), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            throw new FormatException("Hardware request code payload is invalid.", ex);
        }

        if (request is null || request.Version != ContractVersion)
            throw new FormatException("Hardware request code version is unsupported.");

        return request with { HardwareFingerprint = NormalizeFingerprint(request.HardwareFingerprint) };
    }

    public static (string PrivateKeyPem, string PublicKeyPem) GenerateSigningKeyPair()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (key.ExportPkcs8PrivateKeyPem(), key.ExportSubjectPublicKeyInfoPem());
    }

    public static string IssueLicense(
        string privateKeyPem,
        string hardwareRequestCode,
        string licenseId,
        int? maxTags,
        bool unlimitedTags,
        DateTimeOffset issuedAtUtc,
        string? customer = null)
    {
        if (string.IsNullOrWhiteSpace(privateKeyPem))
            throw new ArgumentException("Private signing key is required.", nameof(privateKeyPem));
        if (string.IsNullOrWhiteSpace(licenseId))
            throw new ArgumentException("License ID is required.", nameof(licenseId));
        ValidateCapacity(maxTags, unlimitedTags);

        var request = ParseHardwareRequestCode(hardwareRequestCode);
        var payload = new ProductLicensePayload(
            ContractVersion,
            licenseId.Trim(),
            request.HardwareFingerprint,
            unlimitedTags ? null : maxTags,
            unlimitedTags,
            issuedAtUtc.ToUniversalTime(),
            string.IsNullOrWhiteSpace(customer) ? null : customer.Trim());

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        byte[] signature;
        using (var key = ECDsa.Create())
        {
            key.ImportFromPem(privateKeyPem);
            signature = key.SignData(
                payloadBytes,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
        }

        return $"{LicensePrefix}.{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
    }

    public static ProductLicenseValidationResult ValidateLicense(
        string licenseCode,
        string publicKeyPem,
        string expectedHardwareFingerprint)
    {
        if (string.IsNullOrWhiteSpace(licenseCode))
            return new(false, null, "License code is empty.");
        if (string.IsNullOrWhiteSpace(publicKeyPem))
            return new(false, null, "No trusted license verification public key is configured.");

        try
        {
            var parts = licenseCode.Trim().Split('.');
            if (parts.Length != 3 || !parts[0].Equals(LicensePrefix, StringComparison.Ordinal))
                return new(false, null, "License format is unsupported.");

            var payloadBytes = Base64UrlDecode(parts[1]);
            var signature = Base64UrlDecode(parts[2]);

            using var key = ECDsa.Create();
            key.ImportFromPem(publicKeyPem);
            if (!key.VerifyData(
                    payloadBytes,
                    signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
            {
                return new(false, null, "License signature is invalid.");
            }

            var payload = JsonSerializer.Deserialize<ProductLicensePayload>(payloadBytes, JsonOptions);
            if (payload is null || payload.Version != ContractVersion)
                return new(false, null, "License version is unsupported.");

            ValidateCapacity(payload.MaxTags, payload.UnlimitedTags);
            var expected = NormalizeFingerprint(expectedHardwareFingerprint);
            var actual = NormalizeFingerprint(payload.HardwareFingerprint);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(actual)))
            {
                return new(false, null, "License belongs to different hardware.");
            }

            return new(true, payload with { HardwareFingerprint = actual });
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException or ArgumentException)
        {
            return new(false, null, "License payload is invalid.");
        }
    }

    private static void ValidateCapacity(int? maxTags, bool unlimitedTags)
    {
        if (unlimitedTags)
        {
            if (maxTags.HasValue)
                throw new ArgumentException("Unlimited licenses cannot also contain a finite TAG limit.");
            return;
        }

        if (!maxTags.HasValue || !ProductLicensePolicy.IsSupportedLicensedTagTier(maxTags.Value))
            throw new ArgumentException(
                $"Licensed TAG limit must be one of: {string.Join(", ", ProductLicensePolicy.LicensedTagTiers)}; or unlimited.");
    }

    private static string NormalizeFingerprint(string hardwareFingerprint)
    {
        if (string.IsNullOrWhiteSpace(hardwareFingerprint))
            throw new FormatException("Hardware fingerprint is required.");

        var normalized = hardwareFingerprint.Trim().ToUpperInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new FormatException("Hardware fingerprint must be a SHA-256 hexadecimal value.");
        return normalized;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException("Base64url value is empty.");

        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += padded.Length % 4 switch
        {
            0 => string.Empty,
            2 => "==",
            3 => "=",
            _ => throw new FormatException("Base64url value has invalid length.")
        };
        return Convert.FromBase64String(padded);
    }
}
