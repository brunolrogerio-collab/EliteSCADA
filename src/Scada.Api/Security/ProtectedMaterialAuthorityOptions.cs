using System.Security.Cryptography;

namespace Scada.Api.Security;

public sealed record ProtectedMaterialAuthorityOptions(
    string StorePath,
    string ProtectionKeyEnvironmentVariable,
    string? ProtectionKeyFile)
{
    public const string DefaultProtectionKeyEnvironmentVariable =
        "ELITESCADA_PROTECTED_MATERIAL_KEY";

    public static ProtectedMaterialAuthorityOptions FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredPath = configuration["ProtectedMaterial:Store:Path"];
        var storePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "data", "protected-material")
            : Path.GetFullPath(configuredPath.Trim());

        var configuredEnvironment =
            configuration["ProtectedMaterial:Store:ProtectionKeyEnvironmentVariable"];
        var environmentVariable = string.IsNullOrWhiteSpace(configuredEnvironment)
            ? DefaultProtectionKeyEnvironmentVariable
            : configuredEnvironment.Trim();

        if (environmentVariable.Contains('=') ||
            environmentVariable.Contains('\0') ||
            environmentVariable.Any(char.IsWhiteSpace))
        {
            throw new ProtectedMaterialException(
                ProtectedMaterialErrorCodes.KeyUnavailable,
                "Protected-material protection key configuration is invalid.");
        }

        var configuredKeyFile =
            configuration["ProtectedMaterial:Store:ProtectionKeyFile"];
        var keyFile = string.IsNullOrWhiteSpace(configuredKeyFile)
            ? null
            : Path.GetFullPath(configuredKeyFile.Trim());

        var options = new ProtectedMaterialAuthorityOptions(
            Path.GetFullPath(storePath),
            environmentVariable,
            keyFile);
        options.ValidateKeySeparation();
        return options;
    }

    internal void ValidateKeySeparation()
    {
        if (string.IsNullOrWhiteSpace(StorePath))
        {
            throw new ProtectedMaterialException(
                ProtectedMaterialErrorCodes.StoreUnavailable,
                "Protected-material store configuration is invalid.");
        }

        if (string.IsNullOrWhiteSpace(ProtectionKeyFile))
            return;

        var root = Path.GetFullPath(StorePath);
        var key = Path.GetFullPath(ProtectionKeyFile);
        var relative = Path.GetRelativePath(root, key);
        if (relative.Equals(".", StringComparison.Ordinal) ||
            (!relative.Equals("..", StringComparison.Ordinal) &&
             !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
             !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)))
        {
            throw new ProtectedMaterialException(
                ProtectedMaterialErrorCodes.KeyUnavailable,
                "Protected-material protection key must not be stored with protected ciphertext.");
        }
    }

    internal byte[]? LoadProtectionKey()
    {
        ValidateKeySeparation();

        string? encoded = Environment.GetEnvironmentVariable(
            ProtectionKeyEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(encoded) &&
            !string.IsNullOrWhiteSpace(ProtectionKeyFile))
        {
            try
            {
                encoded = File.ReadAllText(ProtectionKeyFile).Trim();
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                throw new ProtectedMaterialException(
                    ProtectedMaterialErrorCodes.KeyUnavailable,
                    "Protected-material protection key is unavailable.");
            }
        }

        if (string.IsNullOrWhiteSpace(encoded))
            return null;

        byte[] key;
        try
        {
            key = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            throw new ProtectedMaterialException(
                ProtectedMaterialErrorCodes.KeyUnavailable,
                "Protected-material protection key configuration is invalid.");
        }

        if (key.Length == FileHostProtectedMaterialAuthority.RequiredKeyBytes)
            return key;

        CryptographicOperations.ZeroMemory(key);
        throw new ProtectedMaterialException(
            ProtectedMaterialErrorCodes.KeyUnavailable,
            "Protected-material protection key configuration is invalid.");
    }
}
