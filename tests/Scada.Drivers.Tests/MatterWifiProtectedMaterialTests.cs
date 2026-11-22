using System.Security.Cryptography;
using Scada.Api.Security;
using Scada.Drivers.Abstractions;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class MatterWifiProtectedMaterialTests
{
    [Fact]
    public async Task ProvisioningIsIdempotentAndStoresOnlyAHostOwnedRandomKey()
    {
        var authority = new MemoryAuthority();
        var references = new MemoryReferences();
        var service = new MatterWifiProtectedMaterialService(authority, references);
        var scope = MatterWifiProtectedMaterial.Scope("home-project", Guid.NewGuid());
        var mutation = AuthorizedMutation();

        await service.EnsureProvisionedAsync(scope, allowCreate: true, mutation);
        var reference = await references.FindAsync(scope);
        Assert.NotNull(reference);
        Assert.Equal(1, authority.StoreCount);
        Assert.Equal(32, authority.GetMaterial(reference!).Length);
        Assert.Contains(authority.GetMaterial(reference!), value => value != 0);

        await service.EnsureProvisionedAsync(scope, allowCreate: true, mutation);
        Assert.Equal(1, authority.StoreCount);
        Assert.Equal(reference, await references.FindAsync(scope));
    }

    [Fact]
    public async Task ExistingMarkerWithMissingHostReferenceFailsClosedInsteadOfRotatingKey()
    {
        var authority = new MemoryAuthority();
        var service = new MatterWifiProtectedMaterialService(authority, new MemoryReferences());

        var failure = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
            await service.EnsureProvisionedAsync(
                MatterWifiProtectedMaterial.Scope("home-project", Guid.NewGuid()),
                allowCreate: false,
                AuthorizedMutation()));

        Assert.Equal(ProtectedMaterialErrorCodes.CredentialRequired, failure.Code);
        Assert.Equal(0, authority.StoreCount);
    }

    [Fact]
    public async Task ResolverInterceptsOnlyExactMatterSlotAndPreservesFallbackForOtherDrivers()
    {
        var authority = new MemoryAuthority();
        var references = new MemoryReferences();
        var service = new MatterWifiProtectedMaterialService(authority, references);
        var sourceId = Guid.NewGuid();
        var scope = MatterWifiProtectedMaterial.Scope("home-project", sourceId);
        await service.EnsureProvisionedAsync(scope, allowCreate: true, AuthorizedMutation());
        var fallback = new CaptureResolver();
        var request = new CommunicationDriverProtectedMaterialRequest(
            "home-project", sourceId.ToString("D"), MatterWifiProtectedMaterial.DriverType,
            MatterWifiProtectedMaterial.FabricStorageKeyPurpose, HostMaterialSlotReference.Marker);

        var inactiveResolver = new MatterWifiProtectedMaterialResolver(service, fallback);
        var inactiveFailure = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
            await inactiveResolver.ResolveAsync(request));
        Assert.Equal(ProtectedMaterialErrorCodes.Unauthorized, inactiveFailure.Code);
        Assert.Equal(0, fallback.CallCount);

        var resolver = new MatterWifiProtectedMaterialResolver(service, fallback, () => true);

        await using (var lease = await resolver.ResolveAsync(request))
        {
            Assert.Equal(32, lease.Material.Length);
            Assert.Equal(0, fallback.CallCount);
        }

        var invalid = request with { Reference = FileHostProtectedMaterialAuthority.ReferencePrefix + new string('a', 32) };
        await Assert.ThrowsAsync<ProtectedMaterialException>(async () => await resolver.ResolveAsync(invalid));
        Assert.Equal(0, fallback.CallCount);

        var other = request with { DriverType = "knx.ip", Purpose = "knx.ip-secure.user-password" };
        await using var fallbackLease = await resolver.ResolveAsync(other);
        Assert.Equal(new byte[] { 9, 8, 7 }, fallbackLease.Material.ToArray());
        Assert.Equal(1, fallback.CallCount);
    }

    [Fact]
    public async Task FileReferenceMapPersistsOpaqueReferenceOnly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "matter-material-test-" + Guid.NewGuid().ToString("N"));
        var scope = MatterWifiProtectedMaterial.Scope("home-project", Guid.NewGuid());
        var authority = new MemoryAuthority();
        try
        {
            var store = new FileMatterWifiProtectedMaterialReferenceStore(directory);
            var service = new MatterWifiProtectedMaterialService(authority, store);
            await service.EnsureProvisionedAsync(scope, allowCreate: true, AuthorizedMutation());
            var reference = await store.FindAsync(scope);
            Assert.NotNull(reference);
            var material = authority.GetMaterial(reference!);
            var restarted = new FileMatterWifiProtectedMaterialReferenceStore(directory);
            Assert.Equal(reference, await restarted.FindAsync(scope));
            var path = Path.Combine(directory, "matter-wifi", "host-references.json");
            var json = await File.ReadAllTextAsync(path);
            Assert.Contains(reference!, json);
            Assert.DoesNotContain(Convert.ToHexString(material).ToLowerInvariant(), json);
            Assert.DoesNotContain(Convert.ToBase64String(material), json);
            CryptographicOperations.ZeroMemory(material);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static ProtectedMaterialMutationContext AuthorizedMutation() => new(
        new SecurityPrincipal("matter-test-admin", "Matter Test Admin", ["admin"]),
        new AuthorizationDecision(true, SecurityCapability.EngineeringModify, "authorized test", ["admin"]));

    private sealed class MemoryReferences : IMatterWifiProtectedMaterialReferenceStore
    {
        private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);
        public ValueTask<string?> FindAsync(ProtectedMaterialScope scope, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_entries.GetValueOrDefault(Key(scope)));
        public ValueTask SetAsync(ProtectedMaterialScope scope, string reference, CancellationToken cancellationToken = default)
        {
            _entries[Key(scope)] = reference;
            return ValueTask.CompletedTask;
        }
        private static string Key(ProtectedMaterialScope scope) =>
            $"{scope.ScopeOwnerKey}|{scope.ResourceId}|{scope.Purpose}";
    }

    private sealed class MemoryAuthority : IProtectedMaterialAuthority
    {
        private readonly Dictionary<string, (ProtectedMaterialScope Scope, byte[] Material)> _entries = new(StringComparer.Ordinal);
        private int _nextReference;
        public int StoreCount { get; private set; }

        public byte[] GetMaterial(string reference) => _entries[reference].Material;

        public ValueTask<ProtectedMaterialWriteResult> StoreAsync(ProtectedMaterialScope scope,
            ReadOnlyMemory<byte> material, ProtectedMaterialMutationContext mutation, CancellationToken cancellationToken = default)
        {
            mutation.DemandAuthorized();
            var reference = FileHostProtectedMaterialAuthority.ReferencePrefix + (++_nextReference).ToString("x32");
            _entries.Add(reference, (scope, material.ToArray()));
            StoreCount++;
            return ValueTask.FromResult(new ProtectedMaterialWriteResult(reference));
        }

        public ValueTask<IProtectedMaterialLease> ResolveAsync(ProtectedMaterialScope scope, string reference,
            CancellationToken cancellationToken = default)
        {
            if (!_entries.TryGetValue(reference, out var entry) || entry.Scope != scope)
                throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.ReferenceNotFound, "Missing.");
            return ValueTask.FromResult<IProtectedMaterialLease>(new MemoryLease(entry.Material.ToArray()));
        }

        public ValueTask<ProtectedMaterialReplaceResult> ReplaceAsync(ProtectedMaterialScope scope, string existingReference,
            ReadOnlyMemory<byte> replacement, ProtectedMaterialMutationContext mutation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DeleteAsync(ProtectedMaterialScope scope, string reference,
            ProtectedMaterialMutationContext mutation, CancellationToken cancellationToken = default)
        {
            if (_entries.Remove(reference, out var entry)) CryptographicOperations.ZeroMemory(entry.Material);
            return ValueTask.CompletedTask;
        }

        public ValueTask<ProtectedMaterialAuthorityHealth> GetHealthAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ProtectedMaterialAuthorityHealth(ProtectedMaterialAuthorityHealthStatus.Ready));
    }

    private sealed class MemoryLease(byte[] material) : IProtectedMaterialLease
    {
        public ReadOnlyMemory<byte> Material => material;
        public ValueTask DisposeAsync()
        {
            CryptographicOperations.ZeroMemory(material);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CaptureResolver : ICommunicationDriverProtectedMaterialResolver
    {
        public int CallCount { get; private set; }
        public ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
            CommunicationDriverProtectedMaterialRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult<ICommunicationDriverProtectedMaterialLease>(new DriverLease());
        }
    }

    private sealed class DriverLease : ICommunicationDriverProtectedMaterialLease
    {
        public ReadOnlyMemory<byte> Material { get; } = new byte[] { 9, 8, 7 };
        public string? ContentType => null;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
