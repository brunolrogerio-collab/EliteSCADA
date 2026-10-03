using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

/// <summary>
/// Host-owned server-side authority for protected material referenced by product resources.
/// </summary>
public interface IProtectedMaterialAuthority
{
    ValueTask<ProtectedMaterialWriteResult> StoreAsync(
        ProtectedMaterialScope scope,
        ReadOnlyMemory<byte> material,
        ProtectedMaterialMutationContext mutation,
        CancellationToken cancellationToken = default);

    ValueTask<IProtectedMaterialLease> ResolveAsync(
        ProtectedMaterialScope scope,
        string reference,
        CancellationToken cancellationToken = default);

    ValueTask<ProtectedMaterialReplaceResult> ReplaceAsync(
        ProtectedMaterialScope scope,
        string existingReference,
        ReadOnlyMemory<byte> replacement,
        ProtectedMaterialMutationContext mutation,
        CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(
        ProtectedMaterialScope scope,
        string reference,
        ProtectedMaterialMutationContext mutation,
        CancellationToken cancellationToken = default);

    ValueTask<ProtectedMaterialAuthorityHealth> GetHealthAsync(
        CancellationToken cancellationToken = default);
}

public interface IProtectedMaterialLease : IAsyncDisposable
{
    ReadOnlyMemory<byte> Material { get; }
}

public sealed record ProtectedMaterialScope(
    string ScopeOwnerKey,
    string ResourceKind,
    string ResourceId,
    string Purpose);

public sealed record ProtectedMaterialMutationContext(
    SecurityPrincipal Principal,
    AuthorizationDecision Authorization);

public sealed record ProtectedMaterialWriteResult(
    string Reference,
    bool Configured = true);

public sealed record ProtectedMaterialReplaceResult(
    string Reference,
    string SupersededReference,
    bool Configured = true);

public enum ProtectedMaterialAuthorityHealthStatus
{
    Ready,
    KeyUnavailable,
    StoreUnavailable,
    CorruptConfiguration
}

public sealed record ProtectedMaterialAuthorityHealth(
    ProtectedMaterialAuthorityHealthStatus Status,
    string? Code = null);
