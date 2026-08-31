using System.Globalization;
using System.Security.Cryptography;
using Scada.Core.Product.Licensing;

return LicenseGeneratorProgram.Run(args);

internal static class LicenseGeneratorProgram
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length == 0 || HasFlag(args, "--help") || HasFlag(args, "-h"))
            {
                PrintUsage();
                return 0;
            }

            var requestCode = Required(args, "--request");
            var tier = ParseTier(Required(args, "--tier"));
            var privateKeyPath = Required(args, "--key");
            var keyId = Optional(args, "--key-id") ?? "preview-1";
            var outputPath = Optional(args, "--out") ?? "EliteSCADA.license";
            var licenseId = Optional(args, "--license-id") ?? Guid.NewGuid().ToString("D");
            var notAfter = ParseOptionalDate(Optional(args, "--expires"));

            if (!EliteScadaLicenseCodec.TryParseMachineRequest(requestCode, out var request, out var requestDiagnostic) || request is null)
                throw new InvalidOperationException(requestDiagnostic ?? "Machine request code is invalid.");

            if (!File.Exists(privateKeyPath))
                throw new FileNotFoundException("Private signing key file was not found.", privateKeyPath);

            using var privateKey = RSA.Create();
            privateKey.ImportFromPem(File.ReadAllText(privateKeyPath));

            var issuedAt = DateTimeOffset.UtcNow;
            var payload = new EliteScadaLicensePayload(
                EliteScadaLicenseCodec.CurrentSchemaVersion,
                licenseId,
                request.MachineFingerprint,
                tier,
                issuedAt,
                notAfter,
                keyId);

            var license = EliteScadaLicenseCodec.CreateSignedLicense(payload, privateKey);
            var fullOutputPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullOutputPath, license + Environment.NewLine);

            Console.WriteLine("EliteSCADA license generated successfully.");
            Console.WriteLine($"License ID : {licenseId}");
            Console.WriteLine($"Machine    : {request.MachineFingerprint}");
            Console.WriteLine($"Tier       : {LicensingPolicy.TierDisplayName(tier)}");
            Console.WriteLine($"Key ID     : {keyId}");
            Console.WriteLine($"Expires    : {(notAfter is null ? "never" : notAfter.Value.ToString("O", CultureInfo.InvariantCulture))}");
            Console.WriteLine($"Output     : {fullOutputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"License generation failed: {ex.Message}");
            return 2;
        }
    }

    private static bool HasFlag(IEnumerable<string> args, string flag) =>
        args.Any(x => string.Equals(x, flag, StringComparison.OrdinalIgnoreCase));

    private static string Required(string[] args, string name) =>
        Optional(args, name) ?? throw new ArgumentException($"Required argument '{name}' is missing.");

    private static string? Optional(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Argument '{name}' requires a value.");
            return args[i + 1];
        }
        return null;
    }

    private static LicenseTier ParseTier(string value) => value.Trim().ToLowerInvariant() switch
    {
        "500" or "tags500" => LicenseTier.Tags500,
        "1000" or "tags1000" => LicenseTier.Tags1000,
        "1500" or "tags1500" => LicenseTier.Tags1500,
        "3000" or "tags3000" => LicenseTier.Tags3000,
        "5000" or "tags5000" => LicenseTier.Tags5000,
        "unlimited" or "ilimitado" => LicenseTier.Unlimited,
        _ => throw new ArgumentException("Unsupported tier. Use 500, 1000, 1500, 3000, 5000 or Unlimited.")
    };

    private static DateTimeOffset? ParseOptionalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            throw new ArgumentException("--expires must be an ISO-8601 date/time.");
        return parsed;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("EliteSCADA Offline License Generator");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  EliteSCADA.LicenseGenerator --request <ESREQ1...> --tier <500|1000|1500|3000|5000|Unlimited> --key <private.pem> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --key-id <id>         Public verification key identifier. Default: preview-1");
        Console.WriteLine("  --out <file>          Output license file. Default: EliteSCADA.license");
        Console.WriteLine("  --license-id <id>     Explicit license ID. Default: generated UUID");
        Console.WriteLine("  --expires <ISO-8601>  Optional license expiration time in UTC");
        Console.WriteLine();
        Console.WriteLine("Security: the private signing key is loaded only from the explicit external --key path.");
    }
}
