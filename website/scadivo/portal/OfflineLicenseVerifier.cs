using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Scadivo.Portal;

internal sealed record VerifiedOfflineLicense(string Id, string Tier, int InteractiveSeats, int ViewOnlySeats,
    bool HaRuntime, bool Intelligence, DateTimeOffset? ExpiresAtUtc);

public sealed class OfflineLicenseVerifier
{
    internal const string KeyId = "elite-prod-2026-01";

    // Public offline trust anchor from ProductLicenseTrustAnchors. Signing stays in the offline generator.
    private const string PublicKey = """
-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAwi9N7igHBxvq8uktCkC0
+4ylZVXMJyk5pzgvHpqdkSqkEZr4UMeuxiWSpo8YztydY7LGnn/+XlLIKN2Z1WsS
zPVd6UCtyxIPNT1cyj4LbsXKPtO33sZ/n8PGmIJyhHBjofur2fuAcDiUeASCHRGA
z1XveZmOWs164RUf06pI+J/CWn3PcuyQepP+F38IYFtyFatFlBu2pB7Qwj6dJMWW
I5taeb/yOBk06CdqWMY7LQRTjWy3kkZGtAKzpg5OAKHONjSzSipfUa4IrOiz0ees
EvyW/zsnTafxS4vCixCHecainCUvBzMOKGCaIneDs682iWbjQkiPQsADPtQkEe4d
6MeJ1oa+TVzF1tdmImMW6mjaI4DOkbe/xcEjAPS4Nss+Or1g7OvcONuriIwNmb8G
0uFvx1ff1fufF6TqY/25efpZVHDRi0X809CpotoOUxHnb76B3yVv/USLEeAGC4NV
dSAubEMGJCxcjjMATLWZhvaoUjJxhBOSsxJZaWNbzWffAgMBAAE=
-----END PUBLIC KEY-----
""";

    private static readonly HashSet<string> Tiers = ["Tags500", "Tags1000", "Tags1500", "Tags3000", "Tags5000", "Unlimited"];

    private readonly Func<RSA> _createPublicKey;

    public OfflineLicenseVerifier() : this(CreateProductionPublicKey) { }

    internal OfflineLicenseVerifier(Func<RSA> createPublicKey) => _createPublicKey = createPublicKey;

    internal VerifiedOfflineLicense? Verify(string code, string fingerprint, DateTimeOffset now)
    {
        using var key = _createPublicKey();
        return Verify(code, fingerprint, now, key);
    }

    private static RSA CreateProductionPublicKey()
    {
        var key = RSA.Create();
        key.ImportFromPem(PublicKey);
        return key;
    }

    private static VerifiedOfflineLicense? Verify(string code, string fingerprint, DateTimeOffset now, RSA key)
    {
        if (code.Length > 8192) return null;
        var parts = code.Split('.');
        if (parts.Length != 3 || parts[0] is not ("ESLIC2" or "ESLIC3")) return null;
        if (parts.Skip(1).Any(part => part.Length == 0 ||
            part.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))) return null;

        try
        {
            var bytes = WebEncoders.Base64UrlDecode(parts[1]);
            if (!key.VerifyData(bytes, WebEncoders.Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                return null;

            using var document = JsonDocument.Parse(bytes);
            var payload = document.RootElement;
            var version = parts[0] == "ESLIC3" ? 3 : 2;
            var fields = new HashSet<string>(StringComparer.Ordinal)
            {
                "schemaVersion", "licenseId", "machineFingerprint", "tier", "issuedAtUtc", "notAfterUtc", "keyId",
                "interactiveSeats", "viewOnlySeats", "haRuntime"
            };
            if (version == 3) fields.Add("assetProcessIntelligence");
            if (payload.EnumerateObject().Any(property => !fields.Remove(property.Name)) || fields.Count != 0 ||
                payload.GetProperty("schemaVersion").GetInt32() != version || payload.GetProperty("keyId").GetString() != KeyId ||
                payload.GetProperty("machineFingerprint").GetString() != fingerprint)
                return null;

            var id = payload.GetProperty("licenseId").GetString();
            var tier = payload.GetProperty("tier").GetString();
            var interactive = payload.GetProperty("interactiveSeats").GetInt32();
            var viewOnly = payload.GetProperty("viewOnlySeats").GetInt32();
            var ha = payload.GetProperty("haRuntime").GetBoolean();
            var intelligence = version == 3 && payload.GetProperty("assetProcessIntelligence").GetBoolean();
            var issued = payload.GetProperty("issuedAtUtc").GetDateTimeOffset();
            var expiry = payload.GetProperty("notAfterUtc");
            DateTimeOffset? expires = expiry.ValueKind == JsonValueKind.Null ? null : expiry.GetDateTimeOffset();
            if (!Guid.TryParseExact(id, "D", out var licenseId) || licenseId == Guid.Empty || tier is null || !Tiers.Contains(tier) ||
                interactive < 0 || viewOnly < 0 || issued > now || (expires is { } end && (end <= issued || end <= now)))
                return null;
            return new(licenseId.ToString("D"), tier, interactive, viewOnly, ha, intelligence, expires);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException or CryptographicException or InvalidOperationException)
        {
            return null;
        }
    }
}
