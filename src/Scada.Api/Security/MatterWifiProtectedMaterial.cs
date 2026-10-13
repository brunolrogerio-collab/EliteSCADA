using System.Security.Cryptography;
using System.Text.Json;
using Scada.Drivers.Abstractions;

namespace Scada.Api.Security;

public static class MatterWifiProtectedMaterial
{
    public const string DriverType = "matter.wifi";
    public const string FabricStorageKeyPurpose = "matter.wifi.fabric-storage-key";
    public const string SecretField = "fabricStorageKey";

    public static ProtectedMaterialScope Scope(string projectKey, Guid dataSourceId)
    {
        if (string.IsNullOrWhiteSpace(projectKey) || dataSourceId == Guid.Empty)
            throw new UnauthorizedAccessException("Matter fabric storage key requires a stable project and Data Source identity.");
        var scope = new ProtectedMaterialScope(
            "project:" + projectKey,
            ProtectedMaterialResourceKinds.Integration,
            $"{DriverType}:{dataSourceId:D}",
            FabricStorageKeyPurpose);
        scope.Validate();
        return scope;
    }

    public static ProtectedMaterialScope Scope(CommunicationDriverProtectedMaterialRequest request)
    {
        request.Validate();
        if (!Guid.TryParseExact(request.DataSourceKey, "D", out var dataSourceId) || dataSourceId == Guid.Empty ||
            !string.Equals(request.DriverType, DriverType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Purpose, FabricStorageKeyPurpose, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Matter protected material requires the exact supported purpose and a stable Data Source ID.");
        return Scope(request.ProjectKey, dataSourceId);
    }

    public static bool IsOpaqueReference(string? reference) =>
        !string.IsNullOrEmpty(reference) &&
        reference.Length == FileHostProtectedMaterialAuthority.ReferencePrefix.Length + 32 &&
        reference.StartsWith(FileHostProtectedMaterialAuthority.ReferencePrefix, StringComparison.Ordinal) &&
        reference.AsSpan(FileHostProtectedMaterialAuthority.ReferencePrefix.Length)
            .ToArray().All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public interface IMatterWifiProtectedMaterialReferenceStore
{
    ValueTask<string?> FindAsync(ProtectedMaterialScope scope, CancellationToken cancellationToken = default);
    ValueTask SetAsync(ProtectedMaterialScope scope, string reference, CancellationToken cancellationToken = default);
}

/// <summary>Host-only Matter reference map; it stores opaque authority references, never fabric key bytes.</summary>
public sealed class FileMatterWifiProtectedMaterialReferenceStore : IMatterWifiProtectedMaterialReferenceStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;

    public FileMatterWifiProtectedMaterialReferenceStore(string protectedMaterialStorePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedMaterialStorePath);
        _path = Path.Combine(Path.GetFullPath(protectedMaterialStorePath), "matter-wifi", "host-references.json");
    }

    public async ValueTask<string?> FindAsync(ProtectedMaterialScope scope, CancellationToken cancellationToken = default)
    {
        var key = ScopeKey(scope);
        await _gate.WaitAsync(cancellationToken);
        try { return (await ReadAsync(cancellationToken)).GetValueOrDefault(key); }
        finally { _gate.Release(); }
    }

    public async ValueTask SetAsync(ProtectedMaterialScope scope, string reference, CancellationToken cancellationToken = default)
    {
        var key = ScopeKey(scope);
        if (!MatterWifiProtectedMaterial.IsOpaqueReference(reference))
            throw new ArgumentException("Matter protected-material reference is invalid.", nameof(reference));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadAsync(cancellationToken);
            entries[key] = reference;
            await WriteAsync(entries, cancellationToken);
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
                          ?? throw new InvalidDataException("Matter protected-material reference state is empty.");
            if (entries.Any(entry => !IsScopeKey(entry.Key) || !MatterWifiProtectedMaterial.IsOpaqueReference(entry.Value)))
                throw new InvalidDataException("Matter protected-material reference state is invalid.");
            return new(entries, StringComparer.Ordinal);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Matter protected-material reference state is malformed.", exception);
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
            HardenFilePermissions(temporary);
            File.Move(temporary, _path, overwrite: true);
            HardenFilePermissions(_path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string ScopeKey(ProtectedMaterialScope scope)
    {
        ValidateScope(scope);
        return Convert.ToHexString(scope.ComputeScopeHash()).ToLowerInvariant();
    }

    private static void ValidateScope(ProtectedMaterialScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        var separator = scope.ResourceId.IndexOf(':');
        if (!scope.ScopeOwnerKey.StartsWith("project:", StringComparison.Ordinal) ||
            scope.ResourceKind != ProtectedMaterialResourceKinds.Integration ||
            separator <= 0 || separator == scope.ResourceId.Length - 1 ||
            !string.Equals(scope.ResourceId[..separator], MatterWifiProtectedMaterial.DriverType, StringComparison.Ordinal) ||
            !Guid.TryParseExact(scope.ResourceId.AsSpan(separator + 1), "D", out var id) || id == Guid.Empty ||
            !string.Equals(scope.Purpose, MatterWifiProtectedMaterial.FabricStorageKeyPurpose, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Matter protected-material scope is unsupported.");
    }

    private static bool IsScopeKey(string value) => value.Length == 64 &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void HardenFilePermissions(string path)
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

public sealed class MatterWifiProtectedMaterialService(
    IProtectedMaterialAuthority authority,
    IMatterWifiProtectedMaterialReferenceStore references)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask EnsureProvisionedAsync(
        ProtectedMaterialScope scope,
        bool allowCreate,
        ProtectedMaterialMutationContext? mutation,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(scope);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await references.FindAsync(scope, cancellationToken);
            if (existing is not null)
            {
                await using var current = await authority.ResolveAsync(scope, existing, cancellationToken);
                if (current.Material.Length != 32)
                    throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.ProtectedMaterialUnreadable,
                        "Matter fabric storage key has an invalid size.");
                return;
            }

            if (!allowCreate)
                throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.CredentialRequired,
                    "The Matter key marker exists but its host-owned key reference is missing; refusing to replace fabric identity.");
            ArgumentNullException.ThrowIfNull(mutation);
            mutation.DemandAuthorized();

            var key = RandomNumberGenerator.GetBytes(32);
            string? reference = null;
            try
            {
                reference = (await authority.StoreAsync(scope, key, mutation, cancellationToken)).Reference;
                await references.SetAsync(scope, reference, cancellationToken);
            }
            catch
            {
                if (reference is not null)
                {
                    try { await authority.DeleteAsync(scope, reference, mutation, CancellationToken.None); }
                    catch { /* the failed pointer commit leaves only inert encrypted material */ }
                }
                throw;
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        finally { _gate.Release(); }
    }

    public async ValueTask<IProtectedMaterialLease> ResolveAsync(
        ProtectedMaterialScope scope,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(scope);
        var reference = await references.FindAsync(scope, cancellationToken);
        if (reference is null)
            throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.CredentialRequired,
                "Matter fabric storage key has not been provisioned on this host.");
        var lease = await authority.ResolveAsync(scope, reference, cancellationToken);
        if (lease.Material.Length == 32) return lease;
        await lease.DisposeAsync();
        throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.ProtectedMaterialUnreadable,
            "Matter fabric storage key has an invalid size.");
    }

    private static void ValidateScope(ProtectedMaterialScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        var separator = scope.ResourceId.IndexOf(':');
        if (!scope.ScopeOwnerKey.StartsWith("project:", StringComparison.Ordinal) ||
            scope.ResourceKind != ProtectedMaterialResourceKinds.Integration ||
            separator <= 0 || separator == scope.ResourceId.Length - 1 ||
            !string.Equals(scope.ResourceId[..separator], MatterWifiProtectedMaterial.DriverType, StringComparison.Ordinal) ||
            !Guid.TryParseExact(scope.ResourceId.AsSpan(separator + 1), "D", out var id) || id == Guid.Empty ||
            !string.Equals(scope.Purpose, MatterWifiProtectedMaterial.FabricStorageKeyPurpose, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Matter protected-material scope is unsupported.");
    }
}

/// <summary>Intercepts only Matter's exact host-owned fabric key slot while the isolated preview composition is active.</summary>
public sealed class MatterWifiProtectedMaterialResolver : ICommunicationDriverProtectedMaterialResolver
{
    private readonly MatterWifiProtectedMaterialService _matter;
    private readonly ICommunicationDriverProtectedMaterialResolver _fallback;
    private readonly Func<bool> _isPreviewCompositionActive;

    public MatterWifiProtectedMaterialResolver(
        MatterWifiProtectedMaterialService matter,
        ICommunicationDriverProtectedMaterialResolver fallback,
        Func<bool>? isPreviewCompositionActive = null)
    {
        _matter = matter ?? throw new ArgumentNullException(nameof(matter));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _isPreviewCompositionActive = isPreviewCompositionActive ?? (() => false);
    }

    public async ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
        CommunicationDriverProtectedMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Validate();
        if (!string.Equals(request.DriverType, MatterWifiProtectedMaterial.DriverType, StringComparison.OrdinalIgnoreCase))
            return await _fallback.ResolveAsync(request, cancellationToken);

        if (!_isPreviewCompositionActive())
            throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.Unauthorized,
                "Matter protected material is available only while the isolated developer preview composition is active.");

        if (!string.Equals(request.Purpose, MatterWifiProtectedMaterial.FabricStorageKeyPurpose, StringComparison.Ordinal) ||
            !string.Equals(request.Reference, HostMaterialSlotReference.Marker, StringComparison.Ordinal))
            throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.InvalidReference,
                "Matter fabric key requires its host-owned marker and exact purpose.");

        var lease = await _matter.ResolveAsync(MatterWifiProtectedMaterial.Scope(request), cancellationToken);
        return new DriverLease(lease);
    }

    private sealed class DriverLease(IProtectedMaterialLease inner) : ICommunicationDriverProtectedMaterialLease
    {
        public ReadOnlyMemory<byte> Material => inner.Material;
        public string? ContentType => null;
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
