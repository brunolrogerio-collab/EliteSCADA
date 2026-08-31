namespace Scada.Drivers.Abstractions;

/// <summary>
/// Scoped request for protected runtime material referenced by canonical
/// Engineering. Reference identifies protected storage; it is never the secret
/// value itself. Purpose should be a stable namespaced value such as
/// "mqtt.password" or "opcua.client-private-key".
/// </summary>
public sealed record CommunicationDriverProtectedMaterialRequest(
    string ProjectKey,
    string DataSourceKey,
    string DriverType,
    string Purpose,
    string Reference)
{
    public void Validate()
    {
        ValidateToken(ProjectKey, nameof(ProjectKey), "project key");
        ValidateToken(DataSourceKey, nameof(DataSourceKey), "Data Source key");
        ValidateToken(DriverType, nameof(DriverType), "DriverType");
        ValidateToken(Purpose, nameof(Purpose), "purpose");
        ValidateToken(Reference, nameof(Reference), "reference");
    }

    private static void ValidateToken(string value, string parameterName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Protected-material {displayName} is required.", parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException($"Protected-material {displayName} must not contain leading or trailing whitespace.", parameterName);
        if (value.Contains('\r') || value.Contains('\n') || value.Contains('\0'))
            throw new ArgumentException($"Protected-material {displayName} contains invalid control characters.", parameterName);
    }
}

/// <summary>
/// Short-lived lease over protected bytes. The resolver implementation owns the
/// backing storage and must invalidate/zero disposable buffers when the lease is
/// disposed where the backing mechanism permits it. Drivers must not persist,
/// log, cache indefinitely, or copy this material unless strictly necessary;
/// Driver-owned copies must be cleared when practical.
/// </summary>
public interface ICommunicationDriverProtectedMaterialLease : IAsyncDisposable
{
    ReadOnlyMemory<byte> Material { get; }
    string? ContentType { get; }
}

/// <summary>
/// Host-owned protected-material resolver. Drivers can resolve only a concrete
/// reference for an explicit project/Data Source/Driver/purpose scope. This
/// boundary intentionally has no enumeration/list API.
/// </summary>
public interface ICommunicationDriverProtectedMaterialResolver
{
    ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
        CommunicationDriverProtectedMaterialRequest request,
        CancellationToken cancellationToken = default);
}
