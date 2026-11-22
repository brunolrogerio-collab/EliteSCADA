using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Scada.Api.Runtime;

/// <summary>
/// Deployment-owned protected material boundary for HA secrets. References are opaque and may be
/// persisted in host configuration; cleartext material never crosses the public HA status contract.
/// </summary>
public interface IRuntimeHaDeploymentSecretStore
{
    string Store(string purpose, string secret);
    string Resolve(string purpose, string reference);
}

public sealed class RuntimeHaDeploymentSecretStoreException : InvalidOperationException
{
    public RuntimeHaDeploymentSecretStoreException(string message)
        : base(message)
    {
    }

    public RuntimeHaDeploymentSecretStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Host/deployment secret store backed by AES-256-GCM encrypted envelopes. The encryption key is
/// supplied by deployment through an environment variable and is never persisted by EliteSCADA.
/// The store intentionally exposes no enumeration or delete API so an admin replacement cannot
/// delete material still referenced by the running configuration before restart/rebind.
/// </summary>
public sealed class RuntimeHaEncryptedFileDeploymentSecretStore : IRuntimeHaDeploymentSecretStore
{
    public const string DefaultProtectionKeyEnvironmentVariable = "ELITESCADA_HA_SECRET_STORE_KEY";
    public const string ReferencePrefix = "ha-secret-v1:";

    private const string EnvelopeSchema = "elitescada.runtime-ha-protected-secret";
    private const int EnvelopeVersion = 1;
    private const int RequiredKeyBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _rootPath;
    private readonly byte[]? _protectionKey;

    public RuntimeHaEncryptedFileDeploymentSecretStore(
        string rootPath,
        byte[]? protectionKey)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Secret-store path is required.", nameof(rootPath));

        _rootPath = Path.GetFullPath(rootPath.Trim());
        if (protectionKey is not null)
        {
            if (protectionKey.Length != RequiredKeyBytes)
            {
                throw new ArgumentException(
                    "HA deployment secret-store protection key must be exactly 32 bytes.",
                    nameof(protectionKey));
            }

            _protectionKey = protectionKey.ToArray();
        }
    }

    public static RuntimeHaEncryptedFileDeploymentSecretStore FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredPath =
            configuration["HighAvailability:Administration:SecretStore:Path"];
        var rootPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "data", "ha", "secrets")
            : Path.GetFullPath(configuredPath.Trim());

        var configuredEnvironmentVariable =
            configuration[
                "HighAvailability:Administration:SecretStore:ProtectionKeyEnvironmentVariable"];
        var environmentVariable = string.IsNullOrWhiteSpace(configuredEnvironmentVariable)
            ? DefaultProtectionKeyEnvironmentVariable
            : configuredEnvironmentVariable.Trim();

        var encodedKey = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(encodedKey))
            return new RuntimeHaEncryptedFileDeploymentSecretStore(rootPath, null);

        try
        {
            var key = Convert.FromBase64String(encodedKey);
            try
            {
                if (key.Length != RequiredKeyBytes)
                {
                    throw new RuntimeHaDeploymentSecretStoreException(
                        "HA deployment secret-store protection key is invalid.");
                }

                return new RuntimeHaEncryptedFileDeploymentSecretStore(rootPath, key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
        catch (FormatException ex)
        {
            throw new RuntimeHaDeploymentSecretStoreException(
                "HA deployment secret-store protection key is invalid.",
                ex);
        }
    }

    public string Store(string purpose, string secret)
    {
        var key = RequireKey();
        var normalizedPurpose = NormalizePurpose(purpose);
        if (string.IsNullOrEmpty(secret))
        {
            throw new RuntimeHaDeploymentSecretStoreException(
                "HA protected secret material is empty.");
        }

        var reference = ReferencePrefix + Guid.NewGuid().ToString("N");
        var path = PathForReference(reference);
        var plaintext = Encoding.UTF8.GetBytes(secret);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];
        var associatedData = Encoding.UTF8.GetBytes(
            $"{EnvelopeSchema}|{EnvelopeVersion}|{normalizedPurpose}|{reference}");

        try
        {
            using (var aes = new AesGcm(key, TagBytes))
                aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

            var envelope = new RuntimeHaProtectedSecretEnvelope(
                EnvelopeSchema,
                EnvelopeVersion,
                reference,
                normalizedPurpose,
                nonce,
                ciphertext,
                tag);

            Directory.CreateDirectory(_rootPath);
            TightenDirectoryPermissions(_rootPath);

            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(
                    temporary,
                    JsonSerializer.SerializeToUtf8Bytes(envelope, Json));
                TightenFilePermissions(temporary);
                File.Move(temporary, path, overwrite: false);
                TightenFilePermissions(path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }

            return reference;
        }
        catch (RuntimeHaDeploymentSecretStoreException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            CryptographicException or
            JsonException)
        {
            throw new RuntimeHaDeploymentSecretStoreException(
                "HA protected secret could not be persisted.",
                ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(associatedData);
        }
    }

    public string Resolve(string purpose, string reference)
    {
        var key = RequireKey();
        var normalizedPurpose = NormalizePurpose(purpose);
        var path = PathForReference(reference);

        try
        {
            if (!File.Exists(path))
            {
                throw new RuntimeHaDeploymentSecretStoreException(
                    "HA protected secret reference is unavailable.");
            }

            var envelope = JsonSerializer.Deserialize<RuntimeHaProtectedSecretEnvelope>(
                File.ReadAllBytes(path),
                Json)
                ?? throw new RuntimeHaDeploymentSecretStoreException(
                    "HA protected secret envelope is empty.");

            if (!string.Equals(envelope.Schema, EnvelopeSchema, StringComparison.Ordinal) ||
                envelope.SchemaVersion != EnvelopeVersion ||
                !string.Equals(envelope.Reference, reference, StringComparison.Ordinal) ||
                !string.Equals(envelope.Purpose, normalizedPurpose, StringComparison.Ordinal) ||
                envelope.Nonce.Length != NonceBytes ||
                envelope.Tag.Length != TagBytes)
            {
                throw new RuntimeHaDeploymentSecretStoreException(
                    "HA protected secret envelope is incompatible.");
            }

            var plaintext = new byte[envelope.Ciphertext.Length];
            var associatedData = Encoding.UTF8.GetBytes(
                $"{EnvelopeSchema}|{EnvelopeVersion}|{normalizedPurpose}|{reference}");
            try
            {
                using (var aes = new AesGcm(key, TagBytes))
                {
                    aes.Decrypt(
                        envelope.Nonce,
                        envelope.Ciphertext,
                        envelope.Tag,
                        plaintext,
                        associatedData);
                }

                return Encoding.UTF8.GetString(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
                CryptographicOperations.ZeroMemory(associatedData);
            }
        }
        catch (RuntimeHaDeploymentSecretStoreException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            CryptographicException or
            JsonException)
        {
            throw new RuntimeHaDeploymentSecretStoreException(
                "HA protected secret could not be resolved.",
                ex);
        }
    }

    private byte[] RequireKey() =>
        _protectionKey
        ?? throw new RuntimeHaDeploymentSecretStoreException(
            "HA deployment secret store is unavailable because its protection key is not configured.");

    private string PathForReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) ||
            !reference.StartsWith(ReferencePrefix, StringComparison.Ordinal))
        {
            throw new RuntimeHaDeploymentSecretStoreException(
                "HA protected secret reference is invalid.");
        }

        var identifier = reference[ReferencePrefix.Length..];
        if (identifier.Length != 32 ||
            identifier.Any(ch => !Uri.IsHexDigit(ch)))
        {
            throw new RuntimeHaDeploymentSecretStoreException(
                "HA protected secret reference is invalid.");
        }

        return Path.Combine(_rootPath, identifier + ".json");
    }

    private static string NormalizePurpose(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
            throw new ArgumentException("Secret purpose is required.", nameof(purpose));

        var normalized = purpose.Trim();
        if (normalized.Length > 128)
            throw new ArgumentException("Secret purpose is too long.", nameof(purpose));
        return normalized;
    }

    private static void TightenDirectoryPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute);
    }

    private static void TightenFilePermissions(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite);
    }

    private sealed record RuntimeHaProtectedSecretEnvelope(
        string Schema,
        int SchemaVersion,
        string Reference,
        string Purpose,
        byte[] Nonce,
        byte[] Ciphertext,
        byte[] Tag);
}
