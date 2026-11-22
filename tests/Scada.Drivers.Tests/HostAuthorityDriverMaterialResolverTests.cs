using Scada.Api.Security;
using Scada.Drivers.Abstractions;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class HostAuthorityDriverMaterialResolverTests
{
    [Fact]
    public async Task ProtectedReferenceUsesStableProjectIntegrationAndDataSourceScope()
    {
        var sourceId = Guid.NewGuid();
        var authority = new CaptureAuthority();
        var references = new CaptureReferenceStore();
        var legacy = new CaptureLegacyResolver();
        var resolver = new HostAuthorityDriverMaterialResolver(
            authority, new IntegrationProtectedMaterialService(authority, references), legacy);
        var request = new CommunicationDriverProtectedMaterialRequest(
            "home-project", sourceId.ToString("D"), "zigbee.native", "zigbee.native.network-key",
            HostMaterialSlotReference.Marker);

        var scope = HostAuthorityDriverMaterialResolver.Scope(request);
        Assert.Equal("project:home-project", scope.ScopeOwnerKey);
        Assert.Equal(ProtectedMaterialResourceKinds.Integration, scope.ResourceKind);
        Assert.Equal($"zigbee.native:{sourceId:D}", scope.ResourceId);
        Assert.Equal("zigbee.native.network-key", scope.Purpose);
        await references.SetAsync(scope, FileHostProtectedMaterialAuthority.ReferencePrefix + new string('a', 32));

        await using var lease = await resolver.ResolveAsync(request);
        Assert.Equal(scope, authority.ResolvedScope);
        Assert.Equal(new byte[] { 1, 2, 3 }, lease.Material.ToArray());
        Assert.Equal(0, legacy.CallCount);
    }

    [Fact]
    public async Task ProtectedAuthorityFailureDoesNotFallBackToLegacyResolver()
    {
        var authority = new CaptureAuthority { ResolveFailure = new InvalidOperationException("scope denied") };
        var references = new CaptureReferenceStore();
        var legacy = new CaptureLegacyResolver();
        var resolver = new HostAuthorityDriverMaterialResolver(
            authority, new IntegrationProtectedMaterialService(authority, references), legacy);
        var sourceId = Guid.NewGuid();
        var request = new CommunicationDriverProtectedMaterialRequest(
            "home-project", sourceId.ToString("D"), "knx.ip", IntegrationProtectedMaterialScopePolicy.KnxUserPassword,
            HostMaterialSlotReference.Marker);
        await references.SetAsync(HostAuthorityDriverMaterialResolver.Scope(request),
            FileHostProtectedMaterialAuthority.ReferencePrefix + new string('b', 32));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await resolver.ResolveAsync(request));

        Assert.Equal("scope denied", failure.Message);
        Assert.Equal(0, legacy.CallCount);
    }

    [Fact]
    public async Task ProtectedReferenceRequiresStableDataSourceIdentity()
    {
        var authority = new CaptureAuthority();
        var references = new CaptureReferenceStore();
        var legacy = new CaptureLegacyResolver();
        var resolver = new HostAuthorityDriverMaterialResolver(
            authority, new IntegrationProtectedMaterialService(authority, references), legacy);
        var request = new CommunicationDriverProtectedMaterialRequest(
            "home-project", "zigbee.key", "zigbee.native", "zigbee.native.network-key",
            HostMaterialSlotReference.Marker);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await resolver.ResolveAsync(request));
        Assert.Null(authority.ResolvedScope);
        Assert.Equal(0, legacy.CallCount);
    }

    private sealed class CaptureAuthority : IProtectedMaterialAuthority
    {
        public ProtectedMaterialScope? ResolvedScope { get; private set; }
        public Exception? ResolveFailure { get; init; }

        public ValueTask<ProtectedMaterialWriteResult> StoreAsync(ProtectedMaterialScope scope,
            ReadOnlyMemory<byte> material, ProtectedMaterialMutationContext mutation,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<IProtectedMaterialLease> ResolveAsync(ProtectedMaterialScope scope, string reference,
            CancellationToken cancellationToken = default)
        {
            ResolvedScope = scope;
            if (ResolveFailure is not null) throw ResolveFailure;
            return ValueTask.FromResult<IProtectedMaterialLease>(new TestAuthorityLease());
        }

        public ValueTask<ProtectedMaterialReplaceResult> ReplaceAsync(ProtectedMaterialScope scope,
            string existingReference, ReadOnlyMemory<byte> replacement, ProtectedMaterialMutationContext mutation,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask DeleteAsync(ProtectedMaterialScope scope, string reference,
            ProtectedMaterialMutationContext mutation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<ProtectedMaterialAuthorityHealth> GetHealthAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestAuthorityLease : IProtectedMaterialLease
    {
        public ReadOnlyMemory<byte> Material { get; } = new byte[] { 1, 2, 3 };
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CaptureReferenceStore : IIntegrationProtectedMaterialReferenceStore
    {
        private readonly Dictionary<string, string> _references = new(StringComparer.Ordinal);

        public ValueTask<string?> FindAsync(ProtectedMaterialScope scope, CancellationToken cancellationToken = default)
        {
            IntegrationProtectedMaterialScopePolicy.Validate(scope);
            var key = scope.ScopeOwnerKey + "|" + scope.ResourceId + "|" + scope.Purpose;
            return ValueTask.FromResult(_references.GetValueOrDefault(key));
        }

        public ValueTask SetAsync(ProtectedMaterialScope scope, string reference, CancellationToken cancellationToken = default)
        {
            IntegrationProtectedMaterialScopePolicy.Validate(scope);
            var key = scope.ScopeOwnerKey + "|" + scope.ResourceId + "|" + scope.Purpose;
            _references[key] = reference;
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(ProtectedMaterialScope scope, CancellationToken cancellationToken = default)
        {
            IntegrationProtectedMaterialScopePolicy.Validate(scope);
            _references.Remove(scope.ScopeOwnerKey + "|" + scope.ResourceId + "|" + scope.Purpose);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CaptureLegacyResolver : ICommunicationDriverProtectedMaterialResolver
    {
        public int CallCount { get; private set; }

        public ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
            CommunicationDriverProtectedMaterialRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult<ICommunicationDriverProtectedMaterialLease>(new TestDriverLease());
        }
    }

    private sealed class TestDriverLease : ICommunicationDriverProtectedMaterialLease
    {
        public ReadOnlyMemory<byte> Material { get; } = new byte[] { 9 };
        public string? ContentType => null;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
