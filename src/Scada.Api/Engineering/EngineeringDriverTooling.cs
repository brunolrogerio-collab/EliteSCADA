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

/// <summary>
/// Creates a short-lived driver Engineering module for one configured Data Source.
/// The API is capability-driven and does not need protocol-specific routing logic.
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
    private IAsyncDisposable? _ownedProvider;

    public EngineeringDriverToolProviderLease(
        CommunicationDriverModuleRegistration registration,
        IAsyncDisposable ownedProvider)
    {
        Registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _ownedProvider = ownedProvider ?? throw new ArgumentNullException(nameof(ownedProvider));
        Registration.Validate();
    }

    public CommunicationDriverModuleRegistration Registration { get; }

    public async ValueTask DisposeAsync()
    {
        var owned = Interlocked.Exchange(ref _ownedProvider, null);
        if (owned is not null)
            await owned.DisposeAsync().ConfigureAwait(false);
    }
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

public sealed class OpcUaEngineeringDriverToolProviderFactory : IEngineeringDriverToolProviderFactory
{
    private readonly ICommunicationDriverProtectedMaterialResolver _protectedMaterialResolver;

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

        return ValueTask.FromResult(
            new EngineeringDriverToolProviderLease(registration, provider));
    }
}
