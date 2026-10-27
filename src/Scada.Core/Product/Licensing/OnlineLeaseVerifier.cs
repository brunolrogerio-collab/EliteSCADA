using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Scada.Core.Product.Licensing;

/// <summary>Expected identities from the pending activation/renewal, never from the untrusted response.</summary>
public sealed record OnlineLeaseBinding(
    Guid LicenseId, Guid OrganizationId, Guid ApplicationId, Guid ActivationId, string MachineFingerprint);

/// <summary>Verified online claims only; deliberately not an installable offline license.</summary>
public sealed record OnlineLeaseClaims(
    int SchemaVersion, Guid LicenseId, Guid OrganizationId, Guid ApplicationId, Guid ActivationId,
    string MachineFingerprint, LicenseTier Tier, int ViewOnlySeats, int InteractiveSeats,
    bool HaRuntime, bool AssetProcessIntelligence, DateTimeOffset IssuedAtUtc,
    DateTimeOffset NotAfterUtc, string KeyId);

public enum OnlineLeaseVerificationStatus { Invalid, Valid, Expired }

/// <summary>Authenticated timing only, including for expired leases; contains no capabilities or quotas.</summary>
public sealed record OnlineLeaseVerificationEvidence(DateTimeOffset IssuedAtUtc, DateTimeOffset NotAfterUtc);

public sealed record OnlineLeaseVerificationResult(
    OnlineLeaseVerificationStatus Status, OnlineLeaseClaims? Claims = null, string? Diagnostic = null,
    OnlineLeaseVerificationEvidence? Evidence = null);

/// <summary>
/// Pure ESLEASE1 verification against the dedicated pinned online public key.
/// Does not install licenses, change Runtime entitlements, renew, or implement durable clock/replay protection.
/// The lifecycle caller must supply trusted time and persisted expected identities.
/// </summary>
public static class OnlineLeaseVerifier
{
    public const string Prefix = "ESLEASE1";
    public const int SchemaVersion = 1;
    public const int MaximumEnvelopeCharacters = 16384;
    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromHours(72);
    private const int MaximumPayloadBytes = 8192;
    private const int SignatureBytes = 384; // RSA 3072

    private static readonly HashSet<string> Properties = new(StringComparer.Ordinal)
    {
        "schemaVersion", "licenseId", "organizationId", "applicationId", "activationId",
        "machineFingerprint", "tier", "viewOnlySeats", "interactiveSeats", "haRuntime",
        "assetProcessIntelligence", "issuedAtUtc", "notAfterUtc", "keyId"
    };

    public static OnlineLeaseVerificationResult Verify(
        string? envelope, OnlineLeaseBinding expected, DateTimeOffset nowUtc)
    {
        using var key = ProductLicenseTrustAnchors.CreateOnlineLeasePublicKey();
        return VerifyWithKey(envelope, expected, nowUtc, key);
    }

    // Tests use ephemeral keys; production has no configurable override for the pinned trust anchor.
    internal static OnlineLeaseVerificationResult VerifyWithKey(
        string? envelope, OnlineLeaseBinding expected, DateTimeOffset nowUtc, RSA publicKey)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(publicKey);
        if (expected.LicenseId == Guid.Empty || expected.OrganizationId == Guid.Empty ||
            expected.ApplicationId == Guid.Empty || expected.ActivationId == Guid.Empty ||
            !IsFingerprint(expected.MachineFingerprint))
            return Invalid("Online lease expected binding is invalid.");
        if (string.IsNullOrWhiteSpace(envelope) || envelope.Length > MaximumEnvelopeCharacters)
            return Invalid("Online lease envelope is missing or exceeds its size limit.");

        try
        {
            var parts = envelope.Split('.', StringSplitOptions.None);
            if (parts.Length != 3 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
                return Invalid("Online lease envelope format is invalid.");

            var payload = Decode(parts[1], MaximumPayloadBytes);
            var signature = Decode(parts[2], SignatureBytes);
            if (publicKey.KeySize != 3072 || signature.Length != SignatureBytes)
                return Invalid("Online lease signing key or signature size is invalid.");

            var claims = Parse(payload);
            if (claims.SchemaVersion != SchemaVersion ||
                !string.Equals(claims.KeyId, ProductLicenseTrustAnchors.OnlineLeaseKeyId, StringComparison.Ordinal))
                return Invalid("Online lease schema or signing purpose is unsupported.");
            if (!publicKey.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                return Invalid("Online lease signature is invalid.");
            if (claims.LicenseId != expected.LicenseId || claims.OrganizationId != expected.OrganizationId ||
                claims.ApplicationId != expected.ApplicationId || claims.ActivationId != expected.ActivationId ||
                !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(claims.MachineFingerprint),
                    Encoding.ASCII.GetBytes(expected.MachineFingerprint)))
                return Invalid("Online lease does not match the expected activation binding.");
            if (claims.NotAfterUtc <= claims.IssuedAtUtc ||
                claims.NotAfterUtc - claims.IssuedAtUtc > MaximumLifetime)
                return Invalid("Online lease validity interval must be positive and at most 72 hours.");
            if (claims.IssuedAtUtc > nowUtc)
                return Invalid("Online lease issue time is in the future.");
            if (nowUtc >= claims.NotAfterUtc)
                return new(OnlineLeaseVerificationStatus.Expired, Diagnostic: "Online lease has expired.",
                    Evidence: new(claims.IssuedAtUtc, claims.NotAfterUtc));

            return new(OnlineLeaseVerificationStatus.Valid, claims,
                Evidence: new(claims.IssuedAtUtc, claims.NotAfterUtc));
        }
        catch (Exception exception) when (exception is FormatException or JsonException or
            ArgumentException or InvalidOperationException or CryptographicException or OverflowException)
        {
            // Do not echo tokens, untrusted property values or platform crypto exception details.
            return Invalid("Online lease encoding or claims are invalid.");
        }
    }

    private static OnlineLeaseClaims Parse(byte[] payload)
    {
        using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 4 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
            if (!Properties.Contains(property.Name) || !seen.Add(property.Name)) throw new JsonException();
        if (seen.Count != Properties.Count) throw new JsonException();

        var machine = root.GetProperty("machineFingerprint").GetString();
        var tierText = root.GetProperty("tier").GetString();
        if (!IsFingerprint(machine) || tierText is null ||
            !Enum.TryParse<LicenseTier>(tierText, ignoreCase: false, out var tier) ||
            !Enum.IsDefined(tier) || tier.ToString() != tierText) throw new JsonException();
        var viewOnly = root.GetProperty("viewOnlySeats").GetInt32();
        var interactive = root.GetProperty("interactiveSeats").GetInt32();
        if (viewOnly < 0 || interactive < 0) throw new JsonException();

        return new(root.GetProperty("schemaVersion").GetInt32(),
            ReadId(root, "licenseId"), ReadId(root, "organizationId"),
            ReadId(root, "applicationId"), ReadId(root, "activationId"), machine!, tier,
            viewOnly, interactive, root.GetProperty("haRuntime").GetBoolean(),
            root.GetProperty("assetProcessIntelligence").GetBoolean(),
            ReadUtc(root, "issuedAtUtc"), ReadUtc(root, "notAfterUtc"),
            root.GetProperty("keyId").GetString() ?? throw new JsonException());
    }

    private static Guid ReadId(JsonElement root, string property)
    {
        if (!Guid.TryParseExact(root.GetProperty(property).GetString(), "D", out var value) || value == Guid.Empty)
            throw new JsonException();
        return value;
    }

    private static DateTimeOffset ReadUtc(JsonElement root, string property)
    {
        var element = root.GetProperty(property);
        var text = element.GetString();
        // Offset-free timestamps would depend on the host timezone. Portal emits explicit UTC.
        if (text is null || (!text.EndsWith('Z') && !text.EndsWith("+00:00", StringComparison.Ordinal)))
            throw new JsonException();
        var value = element.GetDateTimeOffset();
        if (value.Offset != TimeSpan.Zero) throw new JsonException();
        return value;
    }

    private static bool IsFingerprint(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static byte[] Decode(string value, int maximumBytes)
    {
        if (value.Length == 0 || value.Length > (maximumBytes * 4 + 2) / 3 ||
            value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new FormatException();
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 += (base64.Length % 4) switch { 0 => "", 2 => "==", 3 => "=", _ => throw new FormatException() };
        var bytes = Convert.FromBase64String(base64);
        if (bytes.Length > maximumBytes ||
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') != value)
            throw new FormatException();
        return bytes;
    }

    private static OnlineLeaseVerificationResult Invalid(string diagnostic) =>
        new(OnlineLeaseVerificationStatus.Invalid, Diagnostic: diagnostic);
}
