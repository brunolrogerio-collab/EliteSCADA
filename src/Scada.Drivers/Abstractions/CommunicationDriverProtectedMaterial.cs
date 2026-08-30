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
        Require(ProjectKey, nameof(ProjectKey));
        Require(DataSourceKey, nameof(DataSourceKey));
        Require(DriverType, nameof(DriverType));
        Require(Purpose, nameof(Purpose));
        Require(Reference, nameof(Reference));

        if (!string.Equals(ProjectKey, ProjectKey.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Protected-material project key must not contain leading or trailing whitespace.", nameof(ProjectKey));
        if (!string.Equals(DataSourceKey, DataSourceKey.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Protected-material Data Source key must not contain leading or trailing whitespace.", nameof(DataSourceKey));
        if (!string.Equals(DriverType, DriverType.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Protected-material DriverType must not contain leading or trailing whitespace.", nameof(DriverType));
        if (!string.Equals(Purpose, Purpose.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Protected-material purpose must not contain leading or trailing whitespace.", nameof(Purpose));
        if (!string.Equals(Reference, Reference.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Protected-material reference must not contain leading or trailing whitespace.", nameof(Reference));
    }

    private static void Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} is required for protected-material resolution.", parameterName);
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

    /// <summary>
    /// Optional non-secret format hint, for example "text/plain; charset=utf-8",
    /// "application/pkcs8", or "application/x-pkcs12".
    /// </summary>
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
