using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scada.Api.Persistence;

public interface IDatabaseTopologyStore
{
    Task<DatabaseTopologyDocument> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(DatabaseTopologyDocument document, CancellationToken cancellationToken = default);
}

public interface IDeploymentDatabaseSecretStore
{
    bool IsAvailable { get; }

    Task<string> StoreAsync(
        string? reference,
        ReadOnlyMemory<byte> material,
        CancellationToken cancellationToken = default);

    ValueTask<DeploymentDatabaseSecretLease> ResolveAsync(
        string reference,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string reference, CancellationToken cancellationToken = default);
}

public sealed class DeploymentDatabaseSecretLease : IAsyncDisposable
{
    private byte[]? _material;

    internal DeploymentDatabaseSecretLease(byte[] material) =>
        _material = material ?? throw new ArgumentNullException(nameof(material));

    public ReadOnlyMemory<byte> Material => _material ?? ReadOnlyMemory<byte>.Empty;

    public ValueTask DisposeAsync()
    {
        var material = Interlocked.Exchange(ref _material, null);
        if (material is not null)
            CryptographicOperations.ZeroMemory(material);
        return ValueTask.CompletedTask;
    }
}

public sealed class FileDatabaseTopologyStore : IDatabaseTopologyStore
{
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private readonly TimeProvider _timeProvider;

    public FileDatabaseTopologyStore(string path, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Database topology state path is required.", nameof(path));

        _path = Path.GetFullPath(path);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string StatePath => _path;

    public async Task<DatabaseTopologyDocument> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_path))
                return DatabaseTopologyDocument.CreateDefault(_timeProvider.GetUtcNow());

            await using var stream = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var document = await JsonSerializer.DeserializeAsync<DatabaseTopologyDocument>(
                stream,
                Json,
                cancellationToken);
            if (document is null)
                throw new InvalidDataException("Database topology state is empty.");

            document.Validate();
            return document;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Database topology state is malformed.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        DatabaseTopologyDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    16 * 1024,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(stream, document, Json, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                HardenFilePermissions(temporary);
                File.Move(temporary, _path, overwrite: true);
                HardenFilePermissions(_path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    internal static void HardenFilePermissions(string path)
    {
        if (OperatingSystem.IsWindows()) return;

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (PlatformNotSupportedException)
        {
        }
    }
}

public sealed class EncryptedFileDeploymentDatabaseSecretStore :
    IDeploymentDatabaseSecretStore,
    IDisposable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private byte[]? _masterKey;

    public EncryptedFileDeploymentDatabaseSecretStore(
        string path,
        ReadOnlySpan<byte> masterKey)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Deployment database secret-store path is required.", nameof(path));

        _path = Path.GetFullPath(path);
        if (!masterKey.IsEmpty)
        {
            if (masterKey.Length != 32)
                throw new ArgumentException(
                    "Deployment database secret-store master key must be exactly 32 bytes.",
                    nameof(masterKey));
            _masterKey = masterKey.ToArray();
        }
    }

    public bool IsAvailable => _masterKey is { Length: 32 };

    public async Task<string> StoreAsync(
        string? reference,
        ReadOnlyMemory<byte> material,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        if (material.IsEmpty)
            throw new ArgumentException("Database credential material cannot be empty.", nameof(material));

        var normalizedReference = string.IsNullOrWhiteSpace(reference)
            ? "db-" + Guid.NewGuid().ToString("N")
            : NormalizeReference(reference);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var ciphertext = new byte[material.Length];
            var tag = new byte[TagSize];
            var aad = Encoding.UTF8.GetBytes(normalizedReference);
            try
            {
                using var aes = new AesGcm(_masterKey!, TagSize);
                aes.Encrypt(nonce, material.Span, ciphertext, tag, aad);
                document.Secrets[normalizedReference] = new EncryptedSecretEnvelope(
                    1,
                    Convert.ToBase64String(nonce),
                    Convert.ToBase64String(ciphertext),
                    Convert.ToBase64String(tag));
                await WriteDocumentAsync(document, cancellationToken);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(aad);
                CryptographicOperations.ZeroMemory(ciphertext);
                CryptographicOperations.ZeroMemory(tag);
                CryptographicOperations.ZeroMemory(nonce);
            }

            return normalizedReference;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<DeploymentDatabaseSecretLease> ResolveAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var normalizedReference = NormalizeReference(reference);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken);
            if (!document.Secrets.TryGetValue(normalizedReference, out var envelope))
                throw new KeyNotFoundException("Database credential reference was not found.");
            if (envelope.Version != 1)
                throw new InvalidDataException("Database credential envelope version is unsupported.");

            byte[] nonce;
            byte[] ciphertext;
            byte[] tag;
            try
            {
                nonce = Convert.FromBase64String(envelope.NonceBase64);
                ciphertext = Convert.FromBase64String(envelope.CiphertextBase64);
                tag = Convert.FromBase64String(envelope.TagBase64);
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("Database credential envelope is malformed.", ex);
            }

            if (nonce.Length != NonceSize || tag.Length != TagSize || ciphertext.Length == 0)
                throw new InvalidDataException("Database credential envelope is malformed.");

            var plaintext = new byte[ciphertext.Length];
            var aad = Encoding.UTF8.GetBytes(normalizedReference);
            try
            {
                using var aes = new AesGcm(_masterKey!, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
                return new DeploymentDatabaseSecretLease(plaintext);
            }
            catch (CryptographicException ex)
            {
                CryptographicOperations.ZeroMemory(plaintext);
                throw new InvalidDataException("Database credential could not be decrypted.", ex);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(aad);
                CryptographicOperations.ZeroMemory(nonce);
                CryptographicOperations.ZeroMemory(ciphertext);
                CryptographicOperations.ZeroMemory(tag);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var normalizedReference = NormalizeReference(reference);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_path)) return;
            var document = await ReadDocumentAsync(cancellationToken);
            if (document.Secrets.Remove(normalizedReference))
                await WriteDocumentAsync(document, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        var key = Interlocked.Exchange(ref _masterKey, null);
        if (key is not null)
            CryptographicOperations.ZeroMemory(key);
        _gate.Dispose();
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable)
            throw new InvalidOperationException(
                "Deployment database secret store is unavailable. Configure DatabaseTopology:Secrets:MasterKeyBase64 with a host-protected 32-byte key.");
    }

    private async Task<SecretStoreDocument> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
            return new SecretStoreDocument(1, new Dictionary<string, EncryptedSecretEnvelope>(StringComparer.Ordinal));

        try
        {
            await using var stream = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var document = await JsonSerializer.DeserializeAsync<SecretStoreDocument>(
                stream,
                Json,
                cancellationToken);
            if (document is null || document.Version != 1 || document.Secrets is null)
                throw new InvalidDataException("Deployment database secret store is malformed.");
            return document with
            {
                Secrets = new Dictionary<string, EncryptedSecretEnvelope>(
                    document.Secrets,
                    StringComparer.Ordinal)
            };
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Deployment database secret store is malformed.", ex);
        }
    }

    private async Task WriteDocumentAsync(
        SecretStoreDocument document,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, document, Json, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            FileDatabaseTopologyStore.HardenFilePermissions(temporary);
            File.Move(temporary, _path, overwrite: true);
            FileDatabaseTopologyStore.HardenFilePermissions(_path);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static string NormalizeReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Database credential reference is required.", nameof(reference));

        var normalized = reference.Trim();
        if (normalized.Length > 120 ||
            normalized.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '-' or '_' or '.')))
        {
            throw new ArgumentException("Database credential reference contains invalid characters.", nameof(reference));
        }

        return normalized;
    }

    private sealed record SecretStoreDocument(
        int Version,
        Dictionary<string, EncryptedSecretEnvelope> Secrets);

    private sealed record EncryptedSecretEnvelope(
        int Version,
        string NonceBase64,
        string CiphertextBase64,
        string TagBase64);
}
