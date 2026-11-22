using System.Text;
using System.Text.Json;
using Scada.Api.Security;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class IntegrationProtectedMaterialServiceTests
{
    [Fact]
    public async Task ConfigureAndResolve_PersistOpaqueReferenceOutsidePackageAndReplaceSafely()
    {
        var root = Path.Combine(Path.GetTempPath(), "elitescada-integration-material-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var authority = new MemoryAuthority();
            var references = new FileIntegrationProtectedMaterialReferenceStore(root);
            var service = new IntegrationProtectedMaterialService(authority, references);
            var scope = HostAuthorityDriverMaterialResolver.Scope(
                "home-project", Guid.NewGuid(), "zigbee.native", IntegrationProtectedMaterialScopePolicy.ZigbeeNetworkKey);
            var first = Encoding.UTF8.GetBytes("00112233445566778899aabbccddeeff");

            await service.ConfigureAsync(scope, first, Admin());
            Assert.True((await service.GetPublicStateAsync(scope)).Configured);
            await using (var lease = await service.ResolveAsync(scope))
                Assert.Equal(first, lease.Material.ToArray());

            var referenceState = await File.ReadAllTextAsync(Path.Combine(root, "integration-material-references", "references.json"));
            Assert.DoesNotContain(Encoding.UTF8.GetString(first), referenceState, StringComparison.Ordinal);
            Assert.Contains(FileHostProtectedMaterialAuthority.ReferencePrefix, referenceState, StringComparison.Ordinal);

            var second = Encoding.UTF8.GetBytes("ffeeddccbbaa99887766554433221100");
            await service.ConfigureAsync(scope, second, Admin());
            await using (var lease = await service.ResolveAsync(scope))
                Assert.Equal(second, lease.Material.ToArray());
            Assert.Single(authority.StoredMaterials);

            var otherProject = scope with { ScopeOwnerKey = "project:other-project" };
            var missing = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
                await service.ResolveAsync(otherProject));
            Assert.Equal(ProtectedMaterialErrorCodes.CredentialRequired, missing.Code);

            await service.DeleteAsync(scope, Admin());
            Assert.False((await service.GetPublicStateAsync(scope)).Configured);
            Assert.Empty(authority.StoredMaterials);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ScopePolicyRejectsUnknownDriverAndPurposePairs()
    {
        var unsupported = new Scada.Api.Security.ProtectedMaterialScope(
            "project:home", ProtectedMaterialResourceKinds.Integration, "knx.ip:00000000-0000-0000-0000-000000000001", "knx.ip-secure.unknown");

        Assert.Throws<UnauthorizedAccessException>(() => IntegrationProtectedMaterialScopePolicy.Validate(unsupported));
        Assert.Null(IntegrationProtectedMaterialScopePolicy.SecretField("zigbee.native", IntegrationProtectedMaterialScopePolicy.KnxUserPassword));
    }

    [Fact]
    public void KnxProtectedPurposesRequirePortableSlotsAndValidateTheKeyringBundleEnvelope()
    {
        var sourceId = Guid.NewGuid();
        var userPasswordScope = HostAuthorityDriverMaterialResolver.Scope(
            "home-project", sourceId, "knx.ip", IntegrationProtectedMaterialScopePolicy.KnxUserPassword);
        var keyringScope = HostAuthorityDriverMaterialResolver.Scope(
            "home-project", sourceId, "knx.ip", IntegrationProtectedMaterialScopePolicy.KnxDataSecureKeyring);
        var bundle = JsonSerializer.SerializeToUtf8Bytes(new
        {
            format = "elitescada.knxkeys-bundle.v1",
            knxkeysBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("<Keyring />")),
            keyringPassword = "synthetic-only"
        });

        IntegrationProtectedMaterialScopePolicy.ValidateMaterial(userPasswordScope, Encoding.UTF8.GetBytes("protected-user-password"));
        IntegrationProtectedMaterialScopePolicy.ValidateMaterial(keyringScope, bundle);

        Assert.Throws<ArgumentException>(() =>
            IntegrationProtectedMaterialScopePolicy.ValidateMaterial(keyringScope, Encoding.UTF8.GetBytes("not-a-bundle")));
        Assert.Throws<ArgumentException>(() =>
            IntegrationProtectedMaterialScopePolicy.ValidateMaterial(userPasswordScope, [0xff, 0xfe]));
    }

    private static ProtectedMaterialMutationContext Admin() => new(
        new SecurityPrincipal("admin", "Administrator", ["admin"]),
        new AuthorizationDecision(true, SecurityCapability.EngineeringModify, "Authorized for tests.", ["admin"]));

    private sealed class MemoryAuthority : IProtectedMaterialAuthority
    {
        private int _next;
        public Dictionary<string, (ProtectedMaterialScope Scope, byte[] Material)> StoredMaterials { get; } = new(StringComparer.Ordinal);

        public ValueTask<ProtectedMaterialWriteResult> StoreAsync(ProtectedMaterialScope scope,
            ReadOnlyMemory<byte> material, ProtectedMaterialMutationContext mutation,
            CancellationToken cancellationToken = default)
        {
            mutation.DemandAuthorized();
            var reference = FileHostProtectedMaterialAuthority.ReferencePrefix + Interlocked.Increment(ref _next).ToString("x32");
            StoredMaterials[reference] = (scope, material.ToArray());
            return ValueTask.FromResult(new ProtectedMaterialWriteResult(reference));
        }

        public ValueTask<IProtectedMaterialLease> ResolveAsync(ProtectedMaterialScope scope, string reference,
            CancellationToken cancellationToken = default)
        {
            if (!StoredMaterials.TryGetValue(reference, out var stored) || stored.Scope != scope)
                throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.ScopeMismatch, "Scope mismatch.");
            return ValueTask.FromResult<IProtectedMaterialLease>(new MemoryLease(stored.Material.ToArray()));
        }

        public async ValueTask<ProtectedMaterialReplaceResult> ReplaceAsync(ProtectedMaterialScope scope,
            string existingReference, ReadOnlyMemory<byte> replacement, ProtectedMaterialMutationContext mutation,
            CancellationToken cancellationToken = default)
        {
            mutation.DemandAuthorized();
            if (!StoredMaterials.TryGetValue(existingReference, out var stored) || stored.Scope != scope)
                throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.ScopeMismatch, "Scope mismatch.");
            var result = await StoreAsync(scope, replacement, mutation, cancellationToken);
            return new ProtectedMaterialReplaceResult(result.Reference, existingReference);
        }

        public ValueTask DeleteAsync(ProtectedMaterialScope scope, string reference,
            ProtectedMaterialMutationContext mutation, CancellationToken cancellationToken = default)
        {
            mutation.DemandAuthorized();
            if (StoredMaterials.TryGetValue(reference, out var stored) && stored.Scope == scope)
                StoredMaterials.Remove(reference);
            return ValueTask.CompletedTask;
        }

        public ValueTask<ProtectedMaterialAuthorityHealth> GetHealthAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ProtectedMaterialAuthorityHealth(ProtectedMaterialAuthorityHealthStatus.Ready));
    }

    private sealed class MemoryLease(byte[] material) : IProtectedMaterialLease
    {
        public ReadOnlyMemory<byte> Material { get; } = material;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
