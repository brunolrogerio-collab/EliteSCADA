using System.Security.Cryptography;
using System.Text.Json;

namespace Scada.Api.Security;

/// <summary>
/// Host-only mapping from a stable media resource to an opaque protected-material reference.
/// The mapping is deliberately not part of Engineering DTOs or project packages.
/// </summary>
public interface IMediaSourceCredentialReferenceStore
{
    ValueTask<string?> FindAsync(string ownerKey, string sourceId, CancellationToken cancellationToken = default);
    ValueTask SetAsync(string ownerKey, string sourceId, string reference, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(string ownerKey, string sourceId, CancellationToken cancellationToken = default);
}

public sealed class FileMediaSourceCredentialReferenceStore : IMediaSourceCredentialReferenceStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;

    public FileMediaSourceCredentialReferenceStore(string protectedMaterialStorePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedMaterialStorePath);
        _path = Path.Combine(Path.GetFullPath(protectedMaterialStorePath), "media-source-references", "credentials.json");
    }

    public async ValueTask<string?> FindAsync(string ownerKey, string sourceId, CancellationToken cancellationToken = default)
    {
        ValidateKey(ownerKey, sourceId);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadAsync(cancellationToken);
            return entries.TryGetValue(Key(ownerKey, sourceId), out var reference) ? reference : null;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask SetAsync(string ownerKey, string sourceId, string reference, CancellationToken cancellationToken = default)
    {
        ValidateKey(ownerKey, sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (!IsOpaqueReference(reference))
            throw new ArgumentException("Protected-material reference is invalid.", nameof(reference));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadAsync(cancellationToken);
            entries[Key(ownerKey, sourceId)] = reference;
            await WriteAsync(entries, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async ValueTask RemoveAsync(string ownerKey, string sourceId, CancellationToken cancellationToken = default)
    {
        ValidateKey(ownerKey, sourceId);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadAsync(cancellationToken);
            if (entries.Remove(Key(ownerKey, sourceId))) await WriteAsync(entries, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task<Dictionary<string, string>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return new(StringComparer.Ordinal);
        try
        {
            await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read,
                16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var entries = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, Json, cancellationToken)
                          ?? throw new InvalidDataException("Media credential reference state is empty.");
            if (entries.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || !IsOpaqueReference(entry.Value)))
                throw new InvalidDataException("Media credential reference state is invalid.");
            return entries;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Media credential reference state is malformed.", exception);
        }
    }

    private async Task WriteAsync(Dictionary<string, string> entries, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        HardenDirectoryPermissions(directory);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, entries, Json, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            HardenPermissions(temporary);
            File.Move(temporary, _path, overwrite: true);
            HardenPermissions(_path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string Key(string ownerKey, string sourceId) => ownerKey + "|" + sourceId;

    private static bool IsOpaqueReference(string reference) =>
        !string.IsNullOrEmpty(reference) &&
        reference.Length == FileHostProtectedMaterialAuthority.ReferencePrefix.Length + 32 &&
        reference.StartsWith(FileHostProtectedMaterialAuthority.ReferencePrefix, StringComparison.Ordinal) &&
        reference.AsSpan(FileHostProtectedMaterialAuthority.ReferencePrefix.Length)
            .ToArray().All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void ValidateKey(string ownerKey, string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (ownerKey.Length > 256 || sourceId.Length > 256 || ownerKey.Any(char.IsControl) || sourceId.Any(char.IsControl))
            throw new ArgumentException("Media credential owner or source identity is invalid.");
    }

    private static void HardenPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            catch (PlatformNotSupportedException) { }
        }
    }

    private static void HardenDirectoryPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
            catch (PlatformNotSupportedException) { }
        }
    }
}

/// <summary>Credential lifecycle for a media source; only trusted server consumers may resolve.</summary>
public sealed class MediaSourceProtectedCredentialService(
    IProtectedMaterialAuthority authority,
    IMediaSourceCredentialReferenceStore references)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    public async ValueTask<ProtectedMaterialPublicState> GetPublicStateAsync(
        ProtectedMaterialScope scope, CancellationToken cancellationToken = default)
    {
        ValidateScope(scope);
        var reference = await references.FindAsync(scope.ScopeOwnerKey, scope.ResourceId, cancellationToken);
        return reference is null
            ? new(false, ProtectedMaterialErrorCodes.NotConfigured)
            : new(true);
    }

    public async ValueTask ConfigureAsync(
        ProtectedMaterialScope scope,
        MediaSourceCredentialRequest request,
        ProtectedMaterialMutationContext mutation,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(scope);
        ArgumentNullException.ThrowIfNull(request);
        var validation = request.Validate();
        if (validation is not null) throw new ArgumentException(validation, nameof(request));

        var material = JsonSerializer.SerializeToUtf8Bytes(request, Json);
        var gateEntered = false;
        try
        {
            await _lifecycleGate.WaitAsync(cancellationToken);
            gateEntered = true;
            var existing = await references.FindAsync(scope.ScopeOwnerKey, scope.ResourceId, cancellationToken);
            var next = existing is null
                ? (await authority.StoreAsync(scope, material, mutation, cancellationToken)).Reference
                : (await authority.ReplaceAsync(scope, existing, material, mutation, cancellationToken)).Reference;

            try
            {
                await references.SetAsync(scope.ScopeOwnerKey, scope.ResourceId, next, cancellationToken);
            }
            catch
            {
                // The previously committed reference remains usable if reference persistence fails.
                try { await authority.DeleteAsync(scope, next, mutation, CancellationToken.None); }
                catch { /* preserve the original persistence failure; an unreferenced envelope is inert */ }
                throw;
            }

            if (existing is not null && !string.Equals(existing, next, StringComparison.Ordinal))
            {
                try { await authority.DeleteAsync(scope, existing, mutation, CancellationToken.None); }
                catch (Exception exception) when (exception is ProtectedMaterialException or IOException or UnauthorizedAccessException)
                {
                    // The active reference is already switched. A stale envelope is not addressable
                    // through this resource and can be removed by host maintenance later.
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
            if (gateEntered) _lifecycleGate.Release();
        }
    }

    public async ValueTask<IProtectedMaterialLease> ResolveAsync(
        ProtectedMaterialScope scope, CancellationToken cancellationToken = default)
    {
        ValidateScope(scope);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            var reference = await references.FindAsync(scope.ScopeOwnerKey, scope.ResourceId, cancellationToken);
            if (reference is null)
                throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.CredentialRequired,
                    "Media source credentials must be provisioned on this host.");
            return await authority.ResolveAsync(scope, reference, cancellationToken);
        }
        finally { _lifecycleGate.Release(); }
    }

    public async ValueTask DeleteAsync(
        ProtectedMaterialScope scope,
        ProtectedMaterialMutationContext mutation,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(scope);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            var reference = await references.FindAsync(scope.ScopeOwnerKey, scope.ResourceId, cancellationToken);
            if (reference is null) return;
            await references.RemoveAsync(scope.ScopeOwnerKey, scope.ResourceId, cancellationToken);
            await authority.DeleteAsync(scope, reference, mutation, CancellationToken.None);
        }
        finally { _lifecycleGate.Release(); }
    }

    private static void ValidateScope(ProtectedMaterialScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        if (scope.ResourceKind != ProtectedMaterialResourceKinds.MediaSource ||
            scope.Purpose != ProtectedMaterialPurposes.ConnectionCredential)
            throw new ArgumentException("Media credential scope must identify a MediaSource connection credential.", nameof(scope));
    }
}

public sealed record MediaSourceCredentialRequest(string? Username, string? Password, string? BearerToken)
{
    public string? Validate()
    {
        var bearer = !string.IsNullOrEmpty(BearerToken);
        var basic = !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
        if (bearer == basic ||
            (bearer && (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(Password))) ||
            (!bearer && (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))))
            return "Provide either a bearer token or a username and password.";
        if ((Username?.Length ?? 0) > 256 || (Password?.Length ?? 0) > 8192 || (BearerToken?.Length ?? 0) > 8192)
            return "Media source credential exceeds the supported size.";
        if ((Username?.Any(char.IsControl) ?? false) ||
            (Password?.Any(char.IsControl) ?? false) ||
            (BearerToken?.Any(char.IsControl) ?? false))
            return "Media source credentials cannot contain control characters.";
        return null;
    }
}
