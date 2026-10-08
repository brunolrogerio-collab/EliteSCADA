using Scada.Drivers.Abstractions;
using Scada.Drivers.Zigbee2Mqtt;
using Scada.Engineering.Contracts;

namespace Scada.Api.Engineering;

/// <summary>
/// Creates short-lived Engineering MQTT tooling for one configured external
/// Zigbee2MQTT Data Source. The provider receives protected-material references
/// only and resolves credentials for the requested operation.
/// </summary>
public sealed class Zigbee2MqttEngineeringDriverToolProviderFactory : IEngineeringDriverToolProviderFactory
{
    private readonly ICommunicationDriverProtectedMaterialResolver _protectedMaterialResolver;

    public Zigbee2MqttEngineeringDriverToolProviderFactory(
        ICommunicationDriverProtectedMaterialResolver protectedMaterialResolver)
    {
        _protectedMaterialResolver = protectedMaterialResolver
            ?? throw new ArgumentNullException(nameof(protectedMaterialResolver));
    }

    public string DriverType => Zigbee2MqttContract.DriverType;

    public ValueTask<EngineeringDriverToolProviderLease> CreateAsync(
        string? projectKey,
        DataSourceEngineeringDto dataSource,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(dataSource);
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Zigbee2MQTT Engineering tooling cannot open Data Source driver '{dataSource.Driver}'.", nameof(dataSource));
        if (dataSource.Id is null || dataSource.Id == Guid.Empty)
            throw new ArgumentException("Zigbee2MQTT Engineering tooling requires a canonical DataSourceId.", nameof(dataSource));

        var provider = new Zigbee2MqttEngineeringProvider(
            projectKey,
            dataSource.Key,
            dataSource.Id.Value,
            _protectedMaterialResolver);
        var registration = new CommunicationDriverModuleRegistration(
            provider,
            ConnectionTester: provider,
            DiscoverySource: provider,
            PointReadTester: provider);
        registration.Validate();
        return ValueTask.FromResult(new EngineeringDriverToolProviderLease(registration));
    }
}
