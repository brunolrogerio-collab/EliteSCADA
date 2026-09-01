using System.Globalization;
using Scada.Core.Product.Licensing;

namespace EliteSCADA.LicenseGenerator;

internal static class LicenseGeneratorCli
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

            var request = new LicenseGenerationRequest(
                Required(args, "--request"),
                LicenseGenerationService.ParseTier(Required(args, "--tier")),
                Required(args, "--key"),
                Optional(args, "--key-id") ?? "preview-1",
                Optional(args, "--out") ?? "EliteSCADA.license",
                Optional(args, "--license-id") ?? Guid.NewGuid().ToString("D"),
                LicenseGenerationService.ParseOptionalDate(Optional(args, "--expires")));

            var result = LicenseGenerationService.Generate(request);
            Console.WriteLine("EliteSCADA license generated successfully.");
            Console.WriteLine($"License ID : {result.LicenseId}");
            Console.WriteLine($"Machine    : {result.MachineFingerprint}");
            Console.WriteLine($"Tier       : {LicensingPolicy.TierDisplayName(result.Tier)}");
            Console.WriteLine($"Key ID     : {result.KeyId}");
            Console.WriteLine($"Expires    : {(result.NotAfterUtc is null ? "never" : result.NotAfterUtc.Value.ToString("O", CultureInfo.InvariantCulture))}");
            Console.WriteLine($"Output     : {result.OutputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"License generation failed: {ex.Message}");
            return 2;
        }
    }

    private static bool HasFlag(IEnumerable<string> args, string flag) =>
        args.Any(value => string.Equals(value, flag, StringComparison.OrdinalIgnoreCase));

    private static string Required(string[] args, string name) =>
        Optional(args, name) ?? throw new ArgumentException($"Required argument '{name}' is missing.");

    private static string? Optional(string[] args, string name)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) continue;
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Argument '{name}' requires a value.");
            return args[index + 1];
        }
        return null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("EliteSCADA Offline License Generator");
        Console.WriteLine();
        Console.WriteLine("Double-click the executable to open the graphical interface.");
        Console.WriteLine();
        Console.WriteLine("Command-line usage:");
        Console.WriteLine("  EliteSCADA.LicenseGenerator --request <ESREQ1...> --tier <500|1000|1500|3000|5000|Unlimited> --key <private.pem> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --gui                 Open the graphical interface");
        Console.WriteLine("  --key-id <id>         Public verification key identifier. Default: preview-1");
        Console.WriteLine("  --out <file>          Output license file. Default: EliteSCADA.license");
        Console.WriteLine("  --license-id <id>     Explicit license ID. Default: generated UUID");
        Console.WriteLine("  --expires <ISO-8601>  Optional license expiration time in UTC");
        Console.WriteLine();
        Console.WriteLine("Security: the private signing key is loaded only from the explicit external path.");
    }
}
