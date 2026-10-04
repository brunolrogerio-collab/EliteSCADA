using System.Security.Cryptography;
using System.Text;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public static class ProtectedMaterialResourceKinds
{
    public const string MediaSource = "MediaSource";
    public const string RemoteDatabase = "RemoteDatabase";
    public const string Integration = "Integration";
    public const string CloudConnector = "CloudConnector";
}

public static class ProtectedMaterialPurposes
{
    public const string Username = "Username";
    public const string Password = "Password";
    public const string BearerToken = "BearerToken";
    public const string ClientSecret = "ClientSecret";
    public const string SharedSecret = "SharedSecret";
    public const string ConnectionCredential = "ConnectionCredential";
}

public static class ProtectedMaterialErrorCodes
{
    public const string NotConfigured = "NOT_CONFIGURED";
    public const string InvalidReference = "INVALID_REFERENCE";
    public const string ReferenceNotFound = "REFERENCE_NOT_FOUND";
    public const string ScopeMismatch = "SCOPE_MISMATCH";
    public const string KeyUnavailable = "KEY_UNAVAILABLE";
    public const string ProtectedMaterialUnreadable = "PROTECTED_MATERIAL_UNREADABLE";
    public const string StoreUnavailable = "STORE_UNAVAILABLE";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string CredentialRequired = "CREDENTIAL_REQUIRED";
}

public static class ProtectedMaterialAuditActions
{
    public const string Store = "protected-material.store";
    public const string Replace = "protected-material.replace";
    public const string Delete = "protected-material.delete";
}

public sealed class ProtectedMaterialException : InvalidOperationException
{
    public ProtectedMaterialException(string code, string message)
        : base(message) => Code = code;

    public string Code { get; }
}

/// <summary>
/// Complete server-side ownership scope for one protected-material field. A reference alone is
/// never sufficient authority: trusted consumers must supply the exact owner/resource/purpose.
/// </summary>
public sealed record ProtectedMaterialScope(
    string ScopeOwnerKey,
    string ResourceKind,
    string ResourceId,
    string Purpose)
{
    public void Validate()
    {
        ValidateValue(ScopeOwnerKey, nameof(ScopeOwnerKey), "scope owner", 256);
        ValidateToken(ResourceKind, nameof(ResourceKind), "resource kind", 96);
        ValidateValue(ResourceId, nameof(ResourceId), "resource ID", 256);
        ValidateToken(Purpose, nameof(Purpose), "purpose", 128);
    }

    internal byte[] ComputeScopeHash()
    {
        Validate();
        var canonical = new StringBuilder();
        Append(canonical, ScopeOwnerKey);
        Append(canonical, ResourceKind);
        Append(canonical, ResourceId);
        Append(canonical, Purpose);
        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
    }

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(value.Length).Append(':').Append(value).Append('|');

    private static void ValidateValue(
        string value,
        string parameterName,
        string displayName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Protected-material {displayName} is required.", parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException(
                $"Protected-material {displayName} must not contain leading or trailing whitespace.",
                parameterName);
        if (value.Length > maximumLength)
            throw new ArgumentException(
                $"Protected-material {displayName} exceeds the supported length.",
                parameterName);
        if (value.Any(char.IsControl))
            throw new ArgumentException(
                $"Protected-material {displayName} contains invalid control characters.",
                parameterName);
    }

    private static void ValidateToken(
        string value,
        string parameterName,
        string displayName,
        int maximumLength)
    {
        ValidateValue(value, parameterName, displayName, maximumLength);
        if (value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '-' or '_' or '.')))
        {
            throw new ArgumentException(
                $"Protected-material {displayName} contains unsupported characters.",
                parameterName);
        }
    }
}

public sealed record ProtectedMaterialMutationContext(
    SecurityPrincipal Principal,
    AuthorizationDecision Authorization)
{
    public void DemandAuthorized()
    {
        ArgumentNullException.ThrowIfNull(Principal);
        ArgumentNullException.ThrowIfNull(Authorization);

        if (!Principal.IsAuthenticated || !Authorization.Allowed)
        {
            throw new ProtectedMaterialException(
                ProtectedMaterialErrorCodes.Unauthorized,
                "Protected-material administration is not authorized.");
        }
    }
}

public sealed record ProtectedMaterialWriteResult(
    string Reference,
    bool Configured = true);

public sealed record ProtectedMaterialReplaceResult(
    string Reference,
    string SupersededReference,
    bool Configured = true);

public sealed record ProtectedMaterialPublicState(
    bool Configured,
    string? StatusCode = null);

public sealed record ProtectedMaterialDependencyDescriptor(
    string ResourceKind,
    string ResourceId,
    string Purpose,
    bool CredentialRequired);

/// <summary>
/// Project/package export carries a dependency descriptor, never secret bytes and never a
/// host-specific protected-material reference. A different host therefore requires reprovisioning.
/// </summary>
public static class ProtectedMaterialPortability
{
    public static ProtectedMaterialDependencyDescriptor ForProjectExport(
        ProtectedMaterialScope scope,
        bool configured)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Validate();
        return new(
            scope.ResourceKind,
            scope.ResourceId,
            scope.Purpose,
            CredentialRequired: configured);
    }

    public static ProtectedMaterialPublicState ForImportedHost(
        ProtectedMaterialDependencyDescriptor dependency) =>
        dependency.CredentialRequired
            ? new(false, ProtectedMaterialErrorCodes.CredentialRequired)
            : new(false, ProtectedMaterialErrorCodes.NotConfigured);
}

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

public interface IProtectedMaterialLease : IAsyncDisposable
{
    ReadOnlyMemory<byte> Material { get; }
}

/// <summary>
/// Host-owned server-side authority. It intentionally has no enumeration or plaintext read-back
/// contract. Feature-owned administrative APIs authorize writes; trusted server consumers resolve.
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
