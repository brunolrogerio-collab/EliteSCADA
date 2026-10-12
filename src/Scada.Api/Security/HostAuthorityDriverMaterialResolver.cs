using Scada.Drivers.Abstractions;

namespace Scada.Api.Security;

/// <summary>Adapts the existing encrypted host authority; never enumerates or falls back for its references.</summary>
public sealed class HostAuthorityDriverMaterialResolver(
    IProtectedMaterialAuthority authority,
    IntegrationProtectedMaterialService integrationMaterial,
    ICommunicationDriverProtectedMaterialResolver legacy) : ICommunicationDriverProtectedMaterialResolver
{
    public static ProtectedMaterialScope Scope(CommunicationDriverProtectedMaterialRequest request)
    {
        request.Validate();
        if (!Guid.TryParseExact(request.DataSourceKey, "D", out var sourceId) || sourceId == Guid.Empty)
            throw new UnauthorizedAccessException("Protected sidecar material requires a stable Data Source ID.");
        return Scope(request.ProjectKey, sourceId, request.DriverType, request.Purpose);
    }

    public static ProtectedMaterialScope Scope(string projectKey, Guid sourceId, string driverType, string purpose)
    {
        var validation = new CommunicationDriverProtectedMaterialRequest(
            projectKey, sourceId.ToString("D"), driverType, purpose, "scope-validation");
        validation.Validate();
        if (sourceId == Guid.Empty)
            throw new UnauthorizedAccessException("Protected sidecar material requires a stable Data Source ID.");
        return new("project:" + projectKey, ProtectedMaterialResourceKinds.Integration,
            $"{driverType}:{sourceId:D}", purpose);
    }

    public async ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
        CommunicationDriverProtectedMaterialRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();
        var scope = Scope(request);
        if (IntegrationProtectedMaterialScopePolicy.SecretField(request.DriverType, request.Purpose) is not null &&
            !string.Equals(request.Reference, HostMaterialSlotReference.Marker, StringComparison.Ordinal))
            throw new ProtectedMaterialException(ProtectedMaterialErrorCodes.InvalidReference,
                "Home/building integration material must use the portable host-owned slot marker.");
        if (string.Equals(request.Reference, HostMaterialSlotReference.Marker, StringComparison.Ordinal))
            return new DriverLease(await integrationMaterial.ResolveAsync(scope, cancellationToken));
        if (!request.Reference.StartsWith(FileHostProtectedMaterialAuthority.ReferencePrefix, StringComparison.Ordinal))
            return await legacy.ResolveAsync(request, cancellationToken);
        var lease = await authority.ResolveAsync(scope, request.Reference, cancellationToken);
        return new DriverLease(lease);
    }

    private sealed class DriverLease(IProtectedMaterialLease inner) : ICommunicationDriverProtectedMaterialLease
    {
        public ReadOnlyMemory<byte> Material => inner.Material;
        public string? ContentType => null;
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
