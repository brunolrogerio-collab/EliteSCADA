using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Security.Audit;

namespace Scada.Api.Security;

/// <summary>
/// Canonical deployment-owned protected-material authority for server resources.
/// Ciphertext is persisted per opaque reference; the protection key is supplied externally.
/// </summary>
public sealed class FileHostProtectedMaterialAuthority :
    IProtectedMaterialAuthority,
    IDisposable
{
    public const string ReferencePrefix = "protected-material-v1:";
    public const string EnvelopeSchema = "elitescada.protected-material";
    public const int EnvelopeVersion = 1;
    public const string Algorithm = "AES-256-GCM";
    public const int RequiredKeyBytes = 32;

    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int MaximumMaterialBytes = 1_048_576;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _rootPath;
    private readonly byte[]? _protectionKey;
    private readonly IAuditSink _audit;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _referenceGates =
        new(StringComparer.Ordinal);
    private bool _disposed;

    public FileHostProtectedMaterialAuthority(
        ProtectedMaterialAuthorityOptions options,
        byte[]? protectionKey,
        IAuditSink audit,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(audit);
        options.ValidateKeySeparation();

        _rootPath = Path.GetFullPath(options.StorePath);
        _audit = audit;
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (protectionKey is not null)
        {
            if (protectionKey.Length != RequiredKeyBytes)
                throw new ArgumentException(
                    "Protected-material protection key must be exactly 32 bytes.",
                    nameof(protectionKey));
            _protectionKey = protectionKey.ToArray();
        }
    }

    public async ValueTask<ProtectedMaterialWriteResult> StoreAsync(
        ProtectedMaterialScope scope,
        ReadOnlyMemory<byte> material,
        ProtectedMaterialMutationContext mutation,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateMutation(scope, material, mutation);
        _ = RequireKey();

        var reference = await StoreCoreAsync(scope, material, cancellationToken);
        try
        {
            await WriteAuditAsync(
                ProtectedMaterialAuditActions.Store,
                scope,
                mutation,
                cancellationToken);
        }
        catch
        {
            TryDeleteUncommitted(reference);
            throw;
        }

        return new(reference);
    }

    public async ValueTask<IProtectedMaterialLease> ResolveAsync(
        ProtectedMaterialScope scope,
        string reference,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        _ = RequireKey();

        var normalizedReference = NormalizeReference(reference);
        var gate = GateFor(normalizedReference);
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await ResolveCoreAsync(
                scope,
                normalizedReference,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<ProtectedMaterialReplaceResult> ReplaceAsync(
        ProtectedMaterialScope scope,
        string existingReference,
        ReadOnlyMemory<byte> replacement,
        ProtectedMaterialMutationContext mutation,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateMutation(scope, replacement, mutation);
        _ = RequireKey();

        var normalizedExisting = NormalizeReference(existingReference);
        var gate = GateFor(normalizedExisting);
        await gate.WaitAsync(cancellationToken);
        string replacementReference;
        try
        {
            await using (var previous = await ResolveCoreAsync(
                             scope,
                             normalizedExisting,
                             cancellationToken))
            {
                // Scoped/decryptable old material must exist before rotation is accepted.
            }

            replacementReference = await StoreCoreAsync(
                scope,
                replacement,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }

        try
        {
            await WriteAuditAsync(
                ProtectedMaterialAuditActions.Replace,
                scope,
                mutation,
                cancellationToken);
        }
        catch
        {
            TryDeleteUncommitted(replacementReference);
            throw;
        }

        // Caller must atomically persist Reference, then delete SupersededReference when lifecycle
        // ownership permits. This avoids breaking the active consumer before configuration commits.
        return new(replacementReference, normalizedExisting);
    }

    public async ValueTask DeleteAsync(
        ProtectedMaterialScope scope,
        string reference,
        ProtectedMaterialMutationContext mutation,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(mutation);
        scope.Validate();
        mutation.DemandAuthorized();
        _ = RequireKey();

        var normalizedReference = NormalizeReference(reference);
        var gate = GateFor(normalizedReference);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using (var existing = await ResolveCoreAsync(
                             scope,
                             normalizedReference,
                             cancellationToken))
            {
                // Wrong key/scope must never be able to destroy material.
            }

            try
            {
                File.Delete(PathForReference(normalizedReference));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                throw StoreUnavailable();
            }
        }
        finally
        {
            gate.Release();
        }

        await WriteAuditAsync(
            ProtectedMaterialAuditActions.Delete,
            scope,
            mutation,
            cancellationToken);
    }

    public async ValueTask<ProtectedMaterialAuthorityHealth> GetHealthAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_protectionKey is not { Length: RequiredKeyBytes })
        {
            return new(
                ProtectedMaterialAuthorityHealthStatus.KeyUnavailable,
                ProtectedMaterialErrorCodes.KeyUnavailable);
        }

        try
        {
            if (!Directory.Exists(_rootPath))
                return new(ProtectedMaterialAuthorityHealthStatus.Ready);

            foreach (var file in Directory.EnumerateFiles(_rootPath, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                ProtectedMaterialEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<ProtectedMaterialEnvelope>(
                        bytes,
                        Json);
                }
                catch (JsonException)
                {
                    return new(
                        ProtectedMaterialAuthorityHealthStatus.CorruptConfiguration,
                        ProtectedMaterialErrorCodes.ProtectedMaterialUnreadable);
                }

                if (envelope is null || !EnvelopeShapeIsValid(envelope))
                {
                    return new(
                        ProtectedMaterialAuthorityHealthStatus.CorruptConfiguration,
                        ProtectedMaterialErrorCodes.ProtectedMaterialUnreadable);
                }
            }

            return new(ProtectedMaterialAuthorityHealthStatus.Ready);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new(
                ProtectedMaterialAuthorityHealthStatus.StoreUnavailable,
                ProtectedMaterialErrorCodes.StoreUnavailable);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_protectionKey is not null)
            CryptographicOperations.ZeroMemory(_protectionKey);

        foreach (var gate in _referenceGates.Values)
            gate.Dispose();
        _referenceGates.Clear();
    }

    private async ValueTask<string> StoreCoreAsync(
        ProtectedMaterialScope scope,
        ReadOnlyMemory<byte> material,
        CancellationToken cancellationToken)
    {
        var key = RequireKey();
        var reference = ReferencePrefix + Guid.NewGuid().ToString("N");
        var path = PathForReference(reference);
        var scopeHash = scope.ComputeScopeHash();
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[material.Length];
        var tag = new byte[TagBytes];
        var aad = BuildAssociatedData(reference, scopeHash);
        string? temporary = null;

        try
        {
            using (var aes = new AesGcm(key, TagBytes))
                aes.Encrypt(nonce, material.Span, ciphertext, tag, aad);

            var envelope = new ProtectedMaterialEnvelope(
                EnvelopeSchema,
                EnvelopeVersion,
                Algorithm,
                reference,
                Convert.ToHexString(scopeHash),
                _timeProvider.GetUtcNow(),
                nonce,
                ciphertext,
                tag);

            Directory.CreateDirectory(_rootPath);
            TightenDirectoryPermissions(_rootPath);

            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var serialized = JsonSerializer.SerializeToUtf8Bytes(envelope, Json);
            await File.WriteAllBytesAsync(
                temporary,
                serialized,
                cancellationToken);
            TightenFilePermissions(temporary);
            File.Move(temporary, path, overwrite: false);
            temporary = null;
            TightenFilePermissions(path);
            return reference;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ProtectedMaterialException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            CryptographicException or
            JsonException)
        {
            throw StoreUnavailable();
        }
        finally
        {
            if (temporary is not null)
            {
                try
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch
                {
                    // Best-effort cleanup; the temporary file contains ciphertext only.
                }
            }

            CryptographicOperations.ZeroMemory(scopeHash);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    private async ValueTask<IProtectedMaterialLease> ResolveCoreAsync(
        ProtectedMaterialScope scope,
        string normalizedReference,
        CancellationToken cancellationToken)
    {
        var key = RequireKey();
        var path = PathForReference(normalizedReference);
        byte[] persisted;

        try
        {
            if (!File.Exists(path))
                throw ReferenceNotFound();
            persisted = await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ProtectedMaterialException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw StoreUnavailable();
        }

        ProtectedMaterialEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ProtectedMaterialEnvelope>(
                           persisted,
                           Json)
                       ?? throw MaterialUnreadable();
        }
        catch (JsonException)
        {
            throw MaterialUnreadable();
        }

        if (!EnvelopeShapeIsValid(envelope) ||
            !string.Equals(
                envelope.Reference,
                normalizedReference,
                StringComparison.Ordinal))
        {
            throw MaterialUnreadable();
        }

        var expectedScopeHash = scope.ComputeScopeHash();
        byte[] actualScopeHash;
        try
        {
            actualScopeHash = Convert.FromHexString(envelope.ScopeHash);
        }
        catch (FormatException)
        {
            CryptographicOperations.ZeroMemory(expectedScopeHash);
            throw MaterialUnreadable();
        }

        if (!CryptographicOperations.FixedTimeEquals(
                expectedScopeHash,
                actualScopeHash))
        {
            CryptographicOperations.ZeroMemory(expectedScopeHash);
            CryptographicOperations.ZeroMemory(actualScopeHash);
            throw new ProtectedMaterialException(
                ProtectedMaterialErrorCodes.ScopeMismatch,
                "Protected-material reference is not authorized for this resource scope.");
        }

        var aad = BuildAssociatedData(
            normalizedReference,
            expectedScopeHash);
        CryptographicOperations.ZeroMemory(expectedScopeHash);
        CryptographicOperations.ZeroMemory(actualScopeHash);

        var plaintext = new byte[envelope.Ciphertext.Length];
        try
        {
            using (var aes = new AesGcm(key, TagBytes))
            {
                aes.Decrypt(
                    envelope.Nonce,
                    envelope.Ciphertext,
                    envelope.Tag,
                    plaintext,
                    aad);
            }

            return new ZeroingProtectedMaterialLease(plaintext);
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw MaterialUnreadable();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    private async ValueTask WriteAuditAsync(
        string action,
        ProtectedMaterialScope scope,
        ProtectedMaterialMutationContext mutation,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["resourceKind"] = scope.ResourceKind,
            ["purpose"] = scope.Purpose,
            ["capability"] = mutation.Authorization.Capability.ToString()
        };

        await _audit.WriteAsync(
            AuditEvent.Create(
                mutation.Principal.SubjectId,
                mutation.Principal.DisplayName,
                action,
                AuditOutcome.Succeeded,
                "protected-material",
                scope.ResourceId,
                details,
                projectKey: ProjectKeyFromScopeOwner(scope.ScopeOwnerKey),
                roles: mutation.Principal.Roles,
                source: "protected-material-authority"),
            cancellationToken);
    }

    private static string? ProjectKeyFromScopeOwner(string scopeOwnerKey)
    {
        const string prefix = "project:";
        return scopeOwnerKey.StartsWith(prefix, StringComparison.Ordinal)
            ? scopeOwnerKey[prefix.Length..]
            : null;
    }

    private static byte[] BuildAssociatedData(
        string reference,
        ReadOnlySpan<byte> scopeHash)
    {
        var scopeHashHex = Convert.ToHexString(scopeHash);
        return Encoding.UTF8.GetBytes(
            $"{EnvelopeSchema}|{EnvelopeVersion}|{Algorithm}|{reference}|{scopeHashHex}");
    }

    private static bool EnvelopeShapeIsValid(
        ProtectedMaterialEnvelope envelope) =>
        string.Equals(envelope.Schema, EnvelopeSchema, StringComparison.Ordinal) &&
        envelope.SchemaVersion == EnvelopeVersion &&
        string.Equals(envelope.Algorithm, Algorithm, StringComparison.Ordinal) &&
        IsValidReference(envelope.Reference) &&
        envelope.ScopeHash.Length == 64 &&
        envelope.ScopeHash.All(Uri.IsHexDigit) &&
        envelope.Nonce.Length == NonceBytes &&
        envelope.Ciphertext.Length > 0 &&
        envelope.Ciphertext.Length <= MaximumMaterialBytes &&
        envelope.Tag.Length == TagBytes;

    private static void ValidateMutation(
        ProtectedMaterialScope scope,
        ReadOnlyMemory<byte> material,
        ProtectedMaterialMutationContext mutation)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(mutation);
        scope.Validate();
        mutation.DemandAuthorized();

        if (material.IsEmpty)
            throw new ArgumentException(
                "Protected material cannot be empty.",
                nameof(material));
        if (material.Length > MaximumMaterialBytes)
            throw new ArgumentException(
                "Protected material exceeds the host safety limit.",
                nameof(material));
    }

    private byte[] RequireKey() =>
        _protectionKey
        ?? throw new ProtectedMaterialException(
            ProtectedMaterialErrorCodes.KeyUnavailable,
            "Protected-material authority is unavailable because its deployment key is not configured.");

    private SemaphoreSlim GateFor(string reference) =>
        _referenceGates.GetOrAdd(
            reference,
            static _ => new SemaphoreSlim(1, 1));

    private string PathForReference(string reference) =>
        Path.Combine(
            _rootPath,
            NormalizeReference(reference)[ReferencePrefix.Length..] + ".json");

    internal static string NormalizeReference(string reference)
    {
        if (!IsValidReference(reference))
        {
            throw new ProtectedMaterialException(
                ProtectedMaterialErrorCodes.InvalidReference,
                "Protected-material reference is invalid.");
        }

        return reference;
    }

    private static bool IsValidReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) ||
            !reference.StartsWith(ReferencePrefix, StringComparison.Ordinal))
            return false;

        var identifier = reference[ReferencePrefix.Length..];
        return identifier.Length == 32 &&
               identifier.All(character =>
                   char.IsAsciiDigit(character) ||
                   character is >= 'a' and <= 'f');
    }

    private void TryDeleteUncommitted(string reference)
    {
        try
        {
            var path = PathForReference(reference);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Mutation remains failed; maintenance may later remove ciphertext-only debris.
        }
    }

    private static ProtectedMaterialException ReferenceNotFound() =>
        new(
            ProtectedMaterialErrorCodes.ReferenceNotFound,
            "Protected-material reference was not found.");

    private static ProtectedMaterialException MaterialUnreadable() =>
        new(
            ProtectedMaterialErrorCodes.ProtectedMaterialUnreadable,
            "Protected material is unreadable.");

    private static ProtectedMaterialException StoreUnavailable() =>
        new(
            ProtectedMaterialErrorCodes.StoreUnavailable,
            "Protected-material store is unavailable.");

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

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class ZeroingProtectedMaterialLease :
        IProtectedMaterialLease
    {
        private byte[]? _material;

        public ZeroingProtectedMaterialLease(byte[] material) =>
            _material = material;

        public ReadOnlyMemory<byte> Material =>
            _material ?? ReadOnlyMemory<byte>.Empty;

        public ValueTask DisposeAsync()
        {
            var material = Interlocked.Exchange(ref _material, null);
            if (material is not null)
                CryptographicOperations.ZeroMemory(material);
            return ValueTask.CompletedTask;
        }
    }

    private sealed record ProtectedMaterialEnvelope(
        string Schema,
        int SchemaVersion,
        string Algorithm,
        string Reference,
        string ScopeHash,
        DateTimeOffset CreatedAtUtc,
        byte[] Nonce,
        byte[] Ciphertext,
        byte[] Tag);
}
