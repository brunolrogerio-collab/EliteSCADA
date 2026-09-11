using System.Security.Cryptography;
using System.Text;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;
using Scada.Engineering.Contracts;

namespace Scada.Api.Engineering;

public sealed record DriverEngineeringDiscoveryApiRequest(
    IReadOnlyDictionary<string, string>? Parameters = null,
    int? MaximumResults = null);

public sealed record DriverEngineeringBrowseApiRequest(
    string? ParentNodeId = null,
    string? ContinuationToken = null,
    int? PageSize = null,
    IReadOnlyDictionary<string, string>? Parameters = null);

public sealed record DriverEngineeringDraftDataSourceApiRequest(
    string SourceKey,
    string SourceName,
    string DriverType,
    IReadOnlyDictionary<string, string>? Settings = null,
    IReadOnlyDictionary<string, string>? SecretReferences = null);

public sealed record DriverEngineeringDraftDiscoveryApiRequest(
    DriverEngineeringDraftDataSourceApiRequest DataSource,
    IReadOnlyDictionary<string, string>? Parameters = null,
    int? MaximumResults = null);

/// <summary>
/// Opens a driver Engineering module for a configured or transient Data Source.
/// Stable configured Sources may keep short-lived continuation state between
/// requests. Draft Sources receive an owned transient provider that is disposed
/// at the end of the request and therefore cannot mutate or leak Runtime state.
/// </summary>
public interface IEngineeringDriverToolProviderFactory
{
    string DriverType { get; }

    ValueTask<EngineeringDriverToolProviderLease> CreateAsync(
        string? projectKey,
        DataSourceEngineeringDto dataSource,
        CancellationToken cancellationToken = default);
}

public sealed class EngineeringDriverToolProviderLease : IAsyncDisposable
{
    private readonly IAsyncDisposable? _ownedProvider;

    public EngineeringDriverToolProviderLease(
        CommunicationDriverModuleRegistration registration,
        IAsyncDisposable? ownedProvider = null)
    {
        Registration = registration ?? throw new ArgumentNullException(nameof(registration));
        Registration.Validate();
        _ownedProvider = ownedProvider;
    }

    public CommunicationDriverModuleRegistration Registration { get; }

    public ValueTask DisposeAsync() =>
        _ownedProvider?.DisposeAsync() ?? ValueTask.CompletedTask;
}

public sealed class EngineeringDriverToolProviderFactoryRegistry
{
    private readonly IReadOnlyDictionary<string, IEngineeringDriverToolProviderFactory> _byDriverType;

    public EngineeringDriverToolProviderFactoryRegistry(
        IEnumerable<IEngineeringDriverToolProviderFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(factories);
        var map = new Dictionary<string, IEngineeringDriverToolProviderFactory>(StringComparer.OrdinalIgnoreCase);
        foreach (var factory in factories)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (string.IsNullOrWhiteSpace(factory.DriverType))
                throw new InvalidOperationException("Engineering driver tooling factory must declare a DriverType.");
            if (!map.TryAdd(factory.DriverType.Trim(), factory))
                throw new InvalidOperationException(
                    $"Engineering driver tooling factory for '{factory.DriverType}' is already registered.");
        }
        _byDriverType = map;
    }

    public bool TryGet(string? driverType, out IEngineeringDriverToolProviderFactory? factory)
    {
        if (string.IsNullOrWhiteSpace(driverType))
        {
            factory = null;
            return false;
        }

        return _byDriverType.TryGetValue(driverType.Trim(), out factory);
    }
}

public sealed class OpcUaEngineeringDriverToolProviderFactory :
    IEngineeringDriverToolProviderFactory,
    IAsyncDisposable
{
    private readonly ICommunicationDriverProtectedMaterialResolver _protectedMaterialResolver;
    private readonly object _sync = new();
    private readonly Dictionary<Guid, CachedProvider> _active = new();
    private readonly List<OpcUaFoundationEngineeringProvider> _retired = new();
    private bool _disposed;

    public OpcUaEngineeringDriverToolProviderFactory(
        ICommunicationDriverProtectedMaterialResolver protectedMaterialResolver)
    {
        _protectedMaterialResolver = protectedMaterialResolver ??
            throw new ArgumentNullException(nameof(protectedMaterialResolver));
    }

    public string DriverType => OpcUaDriverDescriptorProvider.DriverTypeId;

    public ValueTask<EngineeringDriverToolProviderLease> CreateAsync(
        string? projectKey,
        DataSourceEngineeringDto dataSource,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(dataSource);
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"OPC UA Engineering tooling cannot open Data Source driver '{dataSource.Driver}'.",
                nameof(dataSource));
        }

        if (!dataSource.Id.HasValue || dataSource.Id.Value == Guid.Empty)
        {
            lock (_sync) ThrowIfDisposed();
            var transient = CreateProvider(projectKey, dataSource);
            return ValueTask.FromResult(
                new EngineeringDriverToolProviderLease(transient.Registration, transient.Provider));
        }

        string fingerprint = CreateFingerprint(projectKey, dataSource);
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_active.TryGetValue(dataSource.Id.Value, out var cached) &&
                string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(new EngineeringDriverToolProviderLease(cached.Registration));
            }

            var created = CreateProvider(projectKey, dataSource);
            if (cached is not null)
                _retired.Add(cached.Provider);

            _active[dataSource.Id.Value] = new CachedProvider(
                fingerprint,
                created.Provider,
                created.Registration);
            return ValueTask.FromResult(new EngineeringDriverToolProviderLease(created.Registration));
        }
    }

    public async ValueTask DisposeAsync()
    {
        OpcUaFoundationEngineeringProvider[] providers;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            providers = _active.Values.Select(x => x.Provider)
                .Concat(_retired)
                .Distinct<OpcUaFoundationEngineeringProvider>(ReferenceEqualityComparer.Instance)
                .ToArray();
            _active.Clear();
            _retired.Clear();
        }

        foreach (var provider in providers)
            await provider.DisposeAsync().ConfigureAwait(false);
    }

    private CreatedProvider CreateProvider(string? projectKey, DataSourceEngineeringDto dataSource)
    {
        var securityMaterialProvider = new OpcUaEngineeringSecurityMaterialProvider(
            projectKey,
            dataSource.Key,
            dataSource.SecretReferences,
            _protectedMaterialResolver);
        var provider = new OpcUaFoundationEngineeringProvider(securityMaterialProvider);
        var registration = new CommunicationDriverModuleRegistration(
            provider,
            ConnectionTester: provider,
            DiscoverySource: provider,
            Browser: provider,
            FileImporter: null,
            Reconciler: provider);
        registration.Validate();
        return new CreatedProvider(provider, registration);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OpcUaEngineeringDriverToolProviderFactory));
    }

    private static string CreateFingerprint(string? projectKey, DataSourceEngineeringDto dataSource)
    {
        var canonical = new StringBuilder();
        Append(canonical, projectKey ?? string.Empty);
        Append(canonical, dataSource.Id?.ToString("D") ?? string.Empty);
        Append(canonical, dataSource.Key);
        Append(canonical, dataSource.Driver);
        AppendDictionary(canonical, dataSource.Settings);
        AppendDictionary(canonical, dataSource.SecretReferences);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void AppendDictionary(
        StringBuilder builder,
        IReadOnlyDictionary<string, string>? values)
    {
        if (values is null)
        {
            Append(builder, string.Empty);
            return;
        }

        foreach (var pair in values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            Append(builder, pair.Key.ToUpperInvariant());
            Append(builder, pair.Value);
        }
    }

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(value.Length).Append(':').Append(value).Append('|');

    private sealed record CreatedProvider(
        OpcUaFoundationEngineeringProvider Provider,
        CommunicationDriverModuleRegistration Registration);

    private sealed record CachedProvider(
        string Fingerprint,
        OpcUaFoundationEngineeringProvider Provider,
        CommunicationDriverModuleRegistration Registration);
}
