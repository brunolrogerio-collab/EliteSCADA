using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Scada.Core.Product.Licensing;

public enum LicenseTier
{
    Tags500,
    Tags1000,
    Tags1500,
    Tags3000,
    Tags5000,
    Unlimited
}

public enum LicenseState
{
    Demo,
    Valid,
    Invalid
}

public static class LicensingPolicy
{
    public const int DemoMaxTags = 200;
    public static readonly TimeSpan DemoMaxContinuousRun = TimeSpan.FromMinutes(300);

    public static int? MaximumTags(LicenseTier tier) => tier switch
    {
        LicenseTier.Tags500 => 500,
        LicenseTier.Tags1000 => 1000,
        LicenseTier.Tags1500 => 1500,
        LicenseTier.Tags3000 => 3000,
        LicenseTier.Tags5000 => 5000,
        LicenseTier.Unlimited => null,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unsupported EliteSCADA license tier.")
    };

    public static string TierDisplayName(LicenseTier tier) => tier switch
    {
        LicenseTier.Tags500 => "500",
        LicenseTier.Tags1000 => "1000",
        LicenseTier.Tags1500 => "1500",
        LicenseTier.Tags3000 => "3000",
        LicenseTier.Tags5000 => "5000",
        LicenseTier.Unlimited => "Unlimited",
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unsupported EliteSCADA license tier.")
    };
}

public sealed record MachineRequestPayload(
    int SchemaVersion,
    string MachineFingerprint);

public sealed record EliteScadaLicensePayload(
    int SchemaVersion,
    string LicenseId,
    string MachineFingerprint,
    LicenseTier Tier,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? NotAfterUtc,
    string KeyId);

public sealed record LicenseVerificationResult(
    LicenseState State,
    EliteScadaLicensePayload? License = null,
    string? Diagnostic = null)
{
    public static LicenseVerificationResult Demo() => new(LicenseState.Demo);
    public static LicenseVerificationResult Invalid(string diagnostic) => new(LicenseState.Invalid, Diagnostic: diagnostic);
    public static LicenseVerificationResult Valid(EliteScadaLicensePayload license) => new(LicenseState.Valid, license);
}

public sealed record RunEntitlementDecision(
    bool Allowed,
    LicenseState LicenseState,
    int? MaximumTags,
    TimeSpan? MaximumContinuousRun,
    string? Diagnostic,
    LicenseTier? Tier = null);

public static class ProductEntitlementEvaluator
{
    public static RunEntitlementDecision Evaluate(LicenseVerificationResult verification, int projectTagCount)
    {
        ArgumentNullException.ThrowIfNull(verification);
        if (projectTagCount < 0)
            throw new ArgumentOutOfRangeException(nameof(projectTagCount));

        if (verification.State == LicenseState.Invalid)
        {
            return new RunEntitlementDecision(
                false,
                LicenseState.Invalid,
                null,
                null,
                verification.Diagnostic ?? "The installed EliteSCADA license is invalid.");
        }

        if (verification.State == LicenseState.Demo)
        {
            var allowed = projectTagCount <= LicensingPolicy.DemoMaxTags;
            return new RunEntitlementDecision(
                allowed,
                LicenseState.Demo,
                LicensingPolicy.DemoMaxTags,
                allowed ? LicensingPolicy.DemoMaxContinuousRun : null,
                allowed
                    ? null
                    : $"Demo Run supports at most {LicensingPolicy.DemoMaxTags} TAGs. Engineering data was not changed.");
        }

        if (verification.License is null)
            return new RunEntitlementDecision(false, LicenseState.Invalid, null, null, "Valid license state has no license payload.");

        var maximumTags = LicensingPolicy.MaximumTags(verification.License.Tier);
        var withinCapacity = maximumTags is null || projectTagCount <= maximumTags.Value;
        return new RunEntitlementDecision(
            withinCapacity,
            LicenseState.Valid,
            maximumTags,
            null,
            withinCapacity
                ? null
                : $"Licensed Run supports at most {maximumTags} TAGs. Engineering data was not changed.",
            verification.License.Tier);
    }
}

public static class MachineFingerprint
{
    public static string HashIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            throw new ArgumentException("Machine identity is required.", nameof(identity));

        var canonical = identity.Trim().ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

public interface IMachineIdentityProvider
{
    string GetMachineFingerprint();
}

public sealed class DefaultMachineIdentityProvider : IMachineIdentityProvider
{
    public string GetMachineFingerprint()
    {
        var identity = TryReadWindowsMachineGuid() ?? TryReadLinuxMachineId();
        if (string.IsNullOrWhiteSpace(identity))
            throw new InvalidOperationException("A stable machine identity could not be resolved. License request generation fails closed.");

        return MachineFingerprint.HashIdentity(identity);
    }

    private static string? TryReadWindowsMachineGuid()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", writable: false);
        return key?.GetValue("MachineGuid") as string;
    }

    private static string? TryReadLinuxMachineId()
    {
        if (!OperatingSystem.IsLinux())
            return null;

        foreach (var path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
        {
            if (!File.Exists(path))
                continue;
            var value = File.ReadAllText(path).Trim();
            if (value.Length > 0)
                return value;
        }

        return null;
    }
}

public static class EliteScadaLicenseCodec
{
    public const int CurrentSchemaVersion = 1;
    public const string RequestPrefix = "ESREQ1";
    public const string LicensePrefix = "ESLIC1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string CreateMachineRequest(string machineFingerprint)
    {
        ValidateFingerprint(machineFingerprint);
        var payload = new MachineRequestPayload(CurrentSchemaVersion, machineFingerprint);
        return $"{RequestPrefix}.{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions))}";
    }

    public static bool TryParseMachineRequest(string requestCode, out MachineRequestPayload? payload, out string? diagnostic)
    {
        payload = null;
        diagnostic = null;
        try
        {
            var parts = (requestCode ?? string.Empty).Trim().Split('.', StringSplitOptions.None);
            if (parts.Length != 2 || !string.Equals(parts[0], RequestPrefix, StringComparison.Ordinal))
            {
                diagnostic = "Machine request code format is invalid.";
                return false;
            }

            payload = JsonSerializer.Deserialize<MachineRequestPayload>(Base64UrlDecode(parts[1]), JsonOptions);
            if (payload is null || payload.SchemaVersion != CurrentSchemaVersion)
            {
                diagnostic = "Machine request schema is unsupported.";
                payload = null;
                return false;
            }

            ValidateFingerprint(payload.MachineFingerprint);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            diagnostic = $"Machine request code is invalid: {ex.Message}";
            payload = null;
            return false;
        }
    }

    public static string CreateSignedLicense(EliteScadaLicensePayload payload, RSA privateKey)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(privateKey);
        ValidatePayload(payload);

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var signature = privateKey.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return $"{LicensePrefix}.{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
    }

    public static LicenseVerificationResult VerifyLicense(
        string? licenseCode,
        string expectedMachineFingerprint,
        IReadOnlyDictionary<string, RSA> publicKeys,
        DateTimeOffset nowUtc)
    {
        ValidateFingerprint(expectedMachineFingerprint);
        ArgumentNullException.ThrowIfNull(publicKeys);

        if (string.IsNullOrWhiteSpace(licenseCode))
            return LicenseVerificationResult.Demo();

        try
        {
            var parts = licenseCode.Trim().Split('.', StringSplitOptions.None);
            if (parts.Length != 3 || !string.Equals(parts[0], LicensePrefix, StringComparison.Ordinal))
                return LicenseVerificationResult.Invalid("Installed license format is invalid.");

            var payloadBytes = Base64UrlDecode(parts[1]);
            var signature = Base64UrlDecode(parts[2]);
            var payload = JsonSerializer.Deserialize<EliteScadaLicensePayload>(payloadBytes, JsonOptions);
            if (payload is null)
                return LicenseVerificationResult.Invalid("Installed license payload is missing.");

            ValidatePayload(payload);
            if (!publicKeys.TryGetValue(payload.KeyId, out var publicKey) || publicKey is null)
                return LicenseVerificationResult.Invalid($"Installed license uses unknown signing key '{payload.KeyId}'.");

            if (!publicKey.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                return LicenseVerificationResult.Invalid("Installed license signature is invalid or the payload was tampered with.");

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(payload.MachineFingerprint),
                    Encoding.ASCII.GetBytes(expectedMachineFingerprint)))
                return LicenseVerificationResult.Invalid("Installed license belongs to different hardware.");

            if (payload.NotAfterUtc is not null && nowUtc > payload.NotAfterUtc.Value)
                return LicenseVerificationResult.Invalid("Installed license has expired.");

            return LicenseVerificationResult.Valid(payload);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException or CryptographicException)
        {
            return LicenseVerificationResult.Invalid($"Installed license is invalid: {ex.Message}");
        }
    }

    private static void ValidatePayload(EliteScadaLicensePayload payload)
    {
        if (payload.SchemaVersion != CurrentSchemaVersion)
            throw new ArgumentException("License schema is unsupported.", nameof(payload));
        if (string.IsNullOrWhiteSpace(payload.LicenseId))
            throw new ArgumentException("License ID is required.", nameof(payload));
        ValidateFingerprint(payload.MachineFingerprint);
        if (string.IsNullOrWhiteSpace(payload.KeyId))
            throw new ArgumentException("Signing key ID is required.", nameof(payload));
        if (payload.NotAfterUtc is not null && payload.NotAfterUtc.Value <= payload.IssuedAtUtc)
            throw new ArgumentException("License expiry must be later than issue time.", nameof(payload));
        _ = LicensingPolicy.MaximumTags(payload.Tier);
    }

    private static void ValidateFingerprint(string fingerprint)
    {
        if (fingerprint.Length != 64 || fingerprint.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Machine fingerprint must be a 64-character SHA-256 hexadecimal value.", nameof(fingerprint));
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            0 => base64,
            2 => base64 + "==",
            3 => base64 + "=",
            _ => throw new FormatException("Invalid Base64Url length.")
        };
        return Convert.FromBase64String(base64);
    }
}

public sealed class DemoRunSession
{
    private readonly TimeProvider _timeProvider;
    private readonly long _startedTimestamp;

    public DemoRunSession(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _startedTimestamp = _timeProvider.GetTimestamp();
    }

    public TimeSpan Elapsed => _timeProvider.GetElapsedTime(_startedTimestamp);
    public TimeSpan Remaining
    {
        get
        {
            var remaining = LicensingPolicy.DemoMaxContinuousRun - Elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public bool IsExpired => Elapsed >= LicensingPolicy.DemoMaxContinuousRun;
}
