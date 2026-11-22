using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Scada.Api.Security;

public static class IntegrationProtectedMaterialScopePolicy
{
    public const string ZigbeeNetworkKey = "zigbee.native.network-key";
    public const string KnxUserPassword = "knx.ip-secure.user-password";
    public const string KnxDeviceAuthentication = "knx.ip-secure.device-authentication";
    public const string KnxDataSecureKeyring = "knx.data-secure.keyring";
    public const string ZWaveS0Legacy = "zwave.network-key.s0-legacy";
    public const string ZWaveS2AccessControl = "zwave.network-key.s2-access-control";
    public const string ZWaveS2Authenticated = "zwave.network-key.s2-authenticated";
    public const string ZWaveS2Unauthenticated = "zwave.network-key.s2-unauthenticated";

    public static string? SecretField(string driverType, string purpose)
    {
        if (string.Equals(driverType, "zigbee.native", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(purpose, ZigbeeNetworkKey, StringComparison.Ordinal))
            return "networkKey";
        if (string.Equals(driverType, "zwave.js.controller", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(purpose, ZWaveS0Legacy, StringComparison.Ordinal)) return "s0Legacy";
            if (string.Equals(purpose, ZWaveS2AccessControl, StringComparison.Ordinal)) return "s2AccessControl";
            if (string.Equals(purpose, ZWaveS2Authenticated, StringComparison.Ordinal)) return "s2Authenticated";
            if (string.Equals(purpose, ZWaveS2Unauthenticated, StringComparison.Ordinal)) return "s2Unauthenticated";
        }
        if (string.Equals(driverType, "knx.ip", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(purpose, KnxUserPassword, StringComparison.Ordinal)) return "userPassword";
            if (string.Equals(purpose, KnxDeviceAuthentication, StringComparison.Ordinal)) return "deviceAuthentication";
            if (string.Equals(purpose, KnxDataSecureKeyring, StringComparison.Ordinal)) return "dataSecureKeyring";
        }
        return null;
    }

    public static void Validate(ProtectedMaterialScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        if (!scope.ScopeOwnerKey.StartsWith("project:", StringComparison.Ordinal) ||
            scope.ResourceKind != ProtectedMaterialResourceKinds.Integration)
            throw new UnauthorizedAccessException("Integration protected material requires a project-owned Integration scope.");

        var separator = scope.ResourceId.IndexOf(':');
        if (separator <= 0 || separator == scope.ResourceId.Length - 1 ||
            !Guid.TryParseExact(scope.ResourceId.AsSpan(separator + 1), "D", out var sourceId) || sourceId == Guid.Empty ||
            SecretField(scope.ResourceId[..separator], scope.Purpose) is null)
            throw new UnauthorizedAccessException("Integration protected-material scope is unsupported or lacks stable Data Source identity.");
    }

    public static void ValidateMaterial(ProtectedMaterialScope scope, ReadOnlySpan<byte> material)
    {
        Validate(scope);
        if (material.IsEmpty || material.Length > 1024 * 1024)
            throw new ArgumentException("Protected integration material must contain between 1 byte and 1 MiB.", nameof(material));

        if (scope.Purpose == ZigbeeNetworkKey)
        {
            if (material.Length == 16) return;
            if (material.Length != 32)
                throw new ArgumentException("Zigbee network key must be 16 binary bytes or 32 hexadecimal ASCII characters.", nameof(material));
            foreach (var value in material)
            {
                if (!Uri.IsHexDigit((char)value))
                    throw new ArgumentException("Zigbee network key must be 16 binary bytes or 32 hexadecimal ASCII characters.", nameof(material));
            }
            return;
        }

        if (scope.Purpose is ZWaveS0Legacy or ZWaveS2AccessControl or ZWaveS2Authenticated or ZWaveS2Unauthenticated)
        {
            if (material.Length != 16)
                throw new ArgumentException("Z-Wave S0/S2 network keys must contain exactly 16 binary bytes.", nameof(material));
            return;
        }

        if (scope.Purpose is KnxUserPassword or KnxDeviceAuthentication)
        {
            try
            {
                var text = new UTF8Encoding(false, true).GetString(material);
                if (string.IsNullOrWhiteSpace(text) || text.Length > 4096 || text.Any(char.IsControl))
                    throw new ArgumentException("KNX protected credential is empty or invalid.", nameof(material));
            }
            catch (DecoderFallbackException exception)
            {
                throw new ArgumentException("KNX protected credential must be UTF-8.", nameof(material), exception);
            }
            return;
        }

        if (scope.Purpose == KnxDataSecureKeyring)
            ValidateKnxKeyringBundle(material);
    }

    private static void ValidateKnxKeyringBundle(ReadOnlySpan<byte> material)
    {
        var bundleBytes = material.ToArray();
        try
        {
            using var document = JsonDocument.Parse(bundleBytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.String || format.GetString() != "elitescada.knxkeys-bundle.v1" ||
                !root.TryGetProperty("knxkeysBase64", out var keyring) || keyring.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("keyringPassword", out var password) || password.ValueKind != JsonValueKind.String)
                throw new ArgumentException("KNX Data Secure material must be an XKNX keyring bundle v1.", nameof(material));

            var properties = root.EnumerateObject().Select(property => property.Name).ToArray();
            if (properties.Length != 3 || properties.Distinct(StringComparer.Ordinal).Count() != 3)
                throw new ArgumentException("KNX Data Secure keyring bundle contains unsupported or duplicate fields.", nameof(material));

            var encodedKeyring = keyring.GetString()!;
            var keyringPassword = password.GetString()!;
            if (encodedKeyring.Length is < 4 or > 1_398_104 ||
                keyringPassword.Length is < 1 or > 4096 || keyringPassword.Any(char.IsControl))
                throw new ArgumentException("KNX Data Secure keyring bundle fields are invalid.", nameof(material));

            var keyringBytes = Convert.FromBase64String(encodedKeyring);
            try
            {
                if (keyringBytes.Length is < 1 or > 1024 * 1024)
                    throw new ArgumentException("KNX keyring XML must contain between 1 byte and 1 MiB.", nameof(material));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(keyringBytes);
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("KNX Data Secure material must be a valid keyring bundle.", nameof(material), exception);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("KNX keyring bundle contains invalid base64 data.", nameof(material), exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bundleBytes);
        }
    }
}

public interface IIntegrationProtectedMaterialReferenceStore
{
    ValueTask<string?> FindAsync(ProtectedMaterialScope scope, CancellationToken cancellationToken = default);
    ValueTask SetAsync(ProtectedMaterialScope scope, string reference, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ProtectedMaterialScope scope, CancellationToken cancellationToken = default);
}

/// <summary>Host-only pointer map; Engineering packages contain only HostMaterialSlotReference.Marker.</summary>
public sealed class FileIntegrationProtectedMaterialReferenceStore : IIntegrationProtectedMaterialReferenceStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;

    public FileIntegrationProtectedMaterialReferenceStore(string protectedMaterialStorePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedMaterialStorePath);
        _path = Path.Combine(Path.GetFullPath(protectedMaterialStorePath), "integration-material-references", "references.json");
    }

    public async ValueTask<string?> FindAsync(ProtectedMaterialScope scope, CancellationToken cancellationToken = default)
    {
        var key = ScopeKey(scope);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadAsync(cancellationToken);
            return entries.GetValueOrDefault(key);
        }
        finally { _gate.Release(); }
    }

    public async ValueTask SetAsync(ProtectedMaterialScope scope, string reference, CancellationToken cancellationToken = default)
    {
        var key = ScopeKey(scope);
        if (!IsOpaqueReference(reference))
            throw new ArgumentException("Protected-material reference is invalid.", nameof(reference));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadAsync(cancellationToken);
            entries[key] = reference;
            await WriteAsync(entries, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async ValueTask RemoveAsync(ProtectedMaterialScope scope, CancellationToken cancellationToken = default)
    {
        var key = ScopeKey(scope);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await ReadAsync(cancellationToken);
            if (entries.Remove(key)) await WriteAsync(entries, cancellationToken);
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
                          ?? throw new InvalidDataException("Integration protected-material reference state is empty.");
            if (entries.Any(entry => !IsScopeKey(entry.Key) || !IsOpaqueReference(entry.Value)))
                throw new InvalidDataException("Integration protected-material reference state is invalid.");
            return new(entries, StringComparer.Ordinal);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Integration protected-material reference state is malformed.", exception);
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

    private static string ScopeKey(ProtectedMaterialScope scope)
    {
        IntegrationProtectedMaterialScopePolicy.Validate(scope);
        return Convert.ToHexString(scope.ComputeScopeHash()).ToLowerInvariant();
    }

    private static bool IsScopeKey(string value) => value.Length == 64 &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsOpaqueReference(string reference) =>
        !string.IsNullOrEmpty(reference) &&
        reference.Length == FileHostProtectedMaterialAuthority.ReferencePrefix.Length + 32 &&
        reference.StartsWith(FileHostProtectedMaterialAuthority.ReferencePrefix, StringComparison.Ordinal) &&
        reference.AsSpan(FileHostProtectedMaterialAuthority.ReferencePrefix.Length)
            .ToArray().All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

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

public sealed class IntegrationProtectedMaterialService(
    IProtectedMaterialAuthority authority,
    IIntegrationProtectedMaterialReferenceStore references)
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    public async ValueTask ConfigureAsync(
        ProtectedMaterialScope scope,
        ReadOnlyMemory<byte> material,
        ProtectedMaterialMutationContext mutation,
        CancellationToken cancellationToken = default)
    {
        IntegrationProtectedMaterialScopePolicy.ValidateMaterial(scope, material.Span);
        ArgumentNullException.ThrowIfNull(mutation);
        mutation.DemandAuthorized();
        if (material.IsEmpty || material.Length > 1024 * 1024)
            throw new ArgumentException("Protected integration material must contain between 1 byte and 1 MiB.", nameof(material));

        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            var existing = await references.FindAsync(scope, cancellationToken);
            var next = existing is null
                ? (await authority.StoreAsync(scope, material, mutation, cancellationToken)).Reference
                : (await authority.ReplaceAsync(scope, existing, material, mutation, cancellationToken)).Reference;
            try
            {
                await references.SetAsync(scope, next, cancellationToken);
            }
            catch
            {
                try { await authority.DeleteAsync(scope, next, mutation, CancellationToken.None); }
                catch { /* the previous mapped reference remains usable */ }
                throw;
            }

            if (existing is not null && !string.Equals(existing, next, StringComparison.Ordinal))
            {
                try { await authority.DeleteAsync(scope, existing, mutation, CancellationToken.None); }
                catch (Exception exception) when (exception is ProtectedMaterialException or IOException or UnauthorizedAccessException)
                {
                    // The new reference is already active; a stale encrypted envelope is inert.
                }
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask<IProtectedMaterialLease> ResolveAsync(
        ProtectedMaterialScope scope,
        CancellationToken cancellationToken = default)
    {
        IntegrationProtectedMaterialScopePolicy.Validate(scope);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            var reference = await references.FindAsync(scope, cancellationToken);
            if (reference is null)
                throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.CredentialRequired,
                    "Integration protected material must be provisioned on this host.");
            return await authority.ResolveAsync(scope, reference, cancellationToken);
        }
        finally { _lifecycleGate.Release(); }
    }

    public async ValueTask DeleteAsync(
        ProtectedMaterialScope scope,
        ProtectedMaterialMutationContext mutation,
        CancellationToken cancellationToken = default)
    {
        IntegrationProtectedMaterialScopePolicy.Validate(scope);
        mutation.DemandAuthorized();
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            var reference = await references.FindAsync(scope, cancellationToken);
            if (reference is null) return;
            await references.RemoveAsync(scope, cancellationToken);
            await authority.DeleteAsync(scope, reference, mutation, CancellationToken.None);
        }
        finally { _lifecycleGate.Release(); }
    }

    public async ValueTask<ProtectedMaterialPublicState> GetPublicStateAsync(
        ProtectedMaterialScope scope,
        CancellationToken cancellationToken = default)
    {
        IntegrationProtectedMaterialScopePolicy.Validate(scope);
        var reference = await references.FindAsync(scope, cancellationToken);
        return reference is null
            ? new ProtectedMaterialPublicState(false, ProtectedMaterialErrorCodes.CredentialRequired)
            : new ProtectedMaterialPublicState(true);
    }
}
