using System.Globalization;
using System.Security.Cryptography;
using Scada.Core.Product.Licensing;

namespace EliteSCADA.LicenseGenerator;

internal sealed record LicenseGenerationRequest(
    string MachineRequestCode,
    LicenseTier Tier,
    string PrivateKeyPath,
    string KeyId,
    string OutputPath,
    string LicenseId,
    DateTimeOffset? NotAfterUtc);

internal sealed record LicenseGenerationResult(
    string LicenseId,
    string MachineFingerprint,
    LicenseTier Tier,
    string KeyId,
    DateTimeOffset? NotAfterUtc,
    string OutputPath);

internal static class LicenseGenerationService
{
    public static LicenseGenerationResult Generate(LicenseGenerationRequest input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!EliteScadaLicenseCodec.TryParseMachineRequest(
                input.MachineRequestCode.Trim(),
                out var machineRequest,
                out var requestDiagnostic) || machineRequest is null)
        {
            throw new InvalidOperationException(requestDiagnostic ?? "Machine request code is invalid.");
        }

        var privateKeyPath = Path.GetFullPath(input.PrivateKeyPath.Trim());
        if (!File.Exists(privateKeyPath))
            throw new FileNotFoundException("Private signing key file was not found.", privateKeyPath);

        var keyId = RequiredText(input.KeyId, "Key ID");
        var licenseId = RequiredText(input.LicenseId, "License ID");
        var fullOutputPath = Path.GetFullPath(RequiredText(input.OutputPath, "Output path"));
        var directory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        using var privateKey = RSA.Create();
        privateKey.ImportFromPem(File.ReadAllText(privateKeyPath));

        var payload = new EliteScadaLicensePayload(
            EliteScadaLicenseCodec.CurrentSchemaVersion,
            licenseId,
            machineRequest.MachineFingerprint,
            input.Tier,
            DateTimeOffset.UtcNow,
            input.NotAfterUtc,
            keyId);
        var signedLicense = EliteScadaLicenseCodec.CreateSignedLicense(payload, privateKey);
        File.WriteAllText(fullOutputPath, signedLicense + Environment.NewLine);

        return new LicenseGenerationResult(
            licenseId,
            machineRequest.MachineFingerprint,
            input.Tier,
            keyId,
            input.NotAfterUtc,
            fullOutputPath);
    }

    public static LicenseTier ParseTier(string value) => value.Trim().ToLowerInvariant() switch
    {
        "500" or "tags500" => LicenseTier.Tags500,
        "1000" or "tags1000" => LicenseTier.Tags1000,
        "1500" or "tags1500" => LicenseTier.Tags1500,
        "3000" or "tags3000" => LicenseTier.Tags3000,
        "5000" or "tags5000" => LicenseTier.Tags5000,
        "unlimited" or "ilimitado" => LicenseTier.Unlimited,
        _ => throw new ArgumentException("Unsupported tier. Use 500, 1000, 1500, 3000, 5000 or Unlimited.")
    };

    public static DateTimeOffset? ParseOptionalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw new ArgumentException("--expires must be an ISO-8601 date/time.");
        }
        return parsed;
    }

    private static string RequiredText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{label} is required.");
        return value.Trim();
    }
}
