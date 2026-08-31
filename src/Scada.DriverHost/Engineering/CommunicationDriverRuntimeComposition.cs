using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.DriverHost.Engineering;

/// <summary>
/// Canonical composition point for communication drivers that have completed
/// coordinator convergence. Keeping this registry explicit prevents protocol
/// workers from introducing private activation seams into the runtime host.
/// </summary>
public static class CommunicationDriverRuntimeComposition
{
    public static CommunicationDriverRuntimeComponentRegistry BuildForCurrentSchema(
        Func<IMqttClientTransport>? mqttTransportFactory = null,
        ICommunicationDriverProtectedMaterialResolver? hostProtectedMaterialResolver = null)
    {
        var protectedMaterialResolver = hostProtectedMaterialResolver
            ?? EnvironmentCommunicationDriverProtectedMaterialResolver.CreateDeterministicScopedEnvironment();
        var registry = new CommunicationDriverRuntimeComponentRegistry();
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new MqttCommunicationRuntimePlanner(),
            new HostProtectedMaterialRuntimeFactory(
                new MqttCommunicationRuntimeFactory(mqttTransportFactory),
                protectedMaterialResolver)));
        return registry;
    }

    private sealed class HostProtectedMaterialRuntimeFactory : ICommunicationDriverRuntimeFactory
    {
        private readonly ICommunicationDriverRuntimeFactory _inner;
        private readonly ICommunicationDriverProtectedMaterialResolver _resolver;

        public HostProtectedMaterialRuntimeFactory(
            ICommunicationDriverRuntimeFactory inner,
            ICommunicationDriverProtectedMaterialResolver resolver)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public string DriverType => _inner.DriverType;

        public ICommunicationDriver Create(
            ICommunicationDriverRuntimePlan plan,
            CommunicationDriverRuntimeServices services)
        {
            ArgumentNullException.ThrowIfNull(services);
            return _inner.Create(
                plan,
                services.ProtectedMaterialResolver is null
                    ? services with { ProtectedMaterialResolver = _resolver }
                    : services);
        }
    }
}
