using Scada.Core.Product;
using Scada.Security.Licensing;

return await LicenseGeneratorCli.RunAsync(args);

internal static class LicenseGeneratorCli
{
    public static Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintUsage();
                return Task.FromResult(args.Length == 0 ? 1 : 0);
            }

            return Task.FromResult(args[0].ToLowerInvariant() switch
            {
                "keygen" => Keygen(args[1..]),
                "issue" => Issue(args[1..]),
                _ => Fail($"Unknown command '{args[0]}'.")
            });
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return Task.FromResult(2);
        }
    }

    private static int Keygen(string[] args)
    {
        var options = ParseOptions(args);
        var privatePath = Required(options, "private");
        var publicPath = Required(options, "public");
        var force = options.ContainsKey("force");

        EnsureWritable(privatePath, force);
        EnsureWritable(publicPath, force);

        var keys = ProductLicenseCryptography.GenerateSigningKeyPair();
        WriteSensitive(privatePath, keys.PrivateKeyPem);
        WriteText(publicPath, keys.PublicKeyPem);

        Console.WriteLine($"Private signing key created: {Path.GetFullPath(privatePath)}");
        Console.WriteLine($"Public verification key created: {Path.GetFullPath(publicPath)}");
        Console.WriteLine("Keep the private key offline and outside the EliteSCADA repository/distribution.");
        return 0;
    }

    private static int Issue(string[] args)
    {
        var options = ParseOptions(args);
        var privateKeyPath = Required(options, "private-key");
        var requestCode = ResolveRequestCode(options);
        var licenseId = Required(options, "license-id");
        var capacity = Required(options, "tags");
        var customer = Optional(options, "customer");
        var outputPath = Optional(options, "out");

        var unlimited = capacity.Equals("unlimited", StringComparison.OrdinalIgnoreCase);
        int? maxTags = null;
        if (!unlimited)
        {
            if (!int.TryParse(capacity, out var parsed) || !ProductLicensePolicy.IsSupportedLicensedTagTier(parsed))
            {
                throw new ArgumentException(
                    $"--tags must be one of {string.Join(", ", ProductLicensePolicy.LicensedTagTiers)} or 'unlimited'.");
            }
            maxTags = parsed;
        }

        var privateKeyPem = File.ReadAllText(privateKeyPath);
        var licenseCode = ProductLicenseCryptography.IssueLicense(
            privateKeyPem,
            requestCode,
            licenseId,
            maxTags,
            unlimited,
            DateTimeOffset.UtcNow,
            customer);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.WriteLine(licenseCode);
        }
        else
        {
            WriteText(outputPath, licenseCode + Environment.NewLine);
            Console.WriteLine($"License created: {Path.GetFullPath(outputPath)}");
        }

        var request = ProductLicenseCryptography.ParseHardwareRequestCode(requestCode);
        Console.Error.WriteLine($"License ID: {licenseId.Trim()}");
        Console.Error.WriteLine($"Capacity: {(unlimited ? "unlimited" : maxTags)} TAGs");
        Console.Error.WriteLine($"Hardware fingerprint: {request.HardwareFingerprint}");
        if (!string.IsNullOrWhiteSpace(customer))
            Console.Error.WriteLine($"Customer: {customer.Trim()}");
        return 0;
    }

    private static string ResolveRequestCode(IReadOnlyDictionary<string, string?> options)
    {
        var direct = Optional(options, "request");
        var requestFile = Optional(options, "request-file");
        if (!string.IsNullOrWhiteSpace(direct) && !string.IsNullOrWhiteSpace(requestFile))
            throw new ArgumentException("Use either --request or --request-file, not both.");
        if (!string.IsNullOrWhiteSpace(direct))
            return direct.Trim();
        if (!string.IsNullOrWhiteSpace(requestFile))
            return File.ReadAllText(requestFile).Trim();
        throw new ArgumentException("Hardware request is required via --request or --request-file.");
    }

    private static Dictionary<string, string?> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length == 2)
                throw new ArgumentException($"Unexpected argument '{token}'.");

            var name = token[2..];
            if (name.Equals("force", StringComparison.OrdinalIgnoreCase))
            {
                result[name] = null;
                continue;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Option '--{name}' requires a value.");
            result[name] = args[++index];
        }
        return result;
    }

    private static string Required(IReadOnlyDictionary<string, string?> options, string name)
    {
        var value = Optional(options, name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"Option '--{name}' is required.")
            : value;
    }

    private static string? Optional(IReadOnlyDictionary<string, string?> options, string name) =>
        options.TryGetValue(name, out var value) ? value : null;

    private static void EnsureWritable(string path, bool force)
    {
        if (File.Exists(path) && !force)
            throw new IOException($"File '{path}' already exists. Use --force only when replacement is intentional.");
    }

    private static void WriteSensitive(string path, string content)
    {
        WriteText(path, content);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static void WriteText(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, content);
    }

    private static bool IsHelp(string value) =>
        value.Equals("help", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("-h", StringComparison.OrdinalIgnoreCase);

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("EliteSCADA offline license generator");
        Console.WriteLine();
        Console.WriteLine("Generate signing keys:");
        Console.WriteLine("  keygen --private <private.pem> --public <public.pem> [--force]");
        Console.WriteLine();
        Console.WriteLine("Issue a hardware-bound license:");
        Console.WriteLine("  issue --private-key <private.pem> --request <ESREQ1...> --license-id <id> --tags <500|1000|1500|3000|5000|unlimited> [--customer <name>] [--out <license.escadalicense>]");
        Console.WriteLine("  issue --private-key <private.pem> --request-file <request.txt> --license-id <id> --tags <tier> [--customer <name>] [--out <file>]");
    }
}
