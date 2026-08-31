using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.AllenBradley;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Iec60870;
using Scada.Drivers.Mqtt;
using Scada.Drivers.OpcUa;

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
        ICommunicationDriverProtectedMaterialResolver? hostProtectedMaterialResolver = null,
        Func<IIec104ClientAdapter>? iec104AdapterFactory = null,
        ILogixProtocolClientFactory? logixClientFactory = null,
        Func<OpcUaRuntimeConnectionOptions, IOpcUaRuntimeSecurityMaterialProvider, IOpcUaRuntimeSessionFactory>? opcUaSessionFactoryBuilder = null,
        IDnp3MasterSessionFactory? dnp3SessionFactory = null)
    {
        var protectedMaterialResolver = hostProtectedMaterialResolver
            ?? EnvironmentCommunicationDriverProtectedMaterialResolver.CreateDeterministicScopedEnvironment();
        var registry = new CommunicationDriverRuntimeComponentRegistry();
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new MqttCommunicationRuntimePlanner(),
            new HostProtectedMaterialRuntimeFactory(
                new MqttCommunicationRuntimeFactory(mqttTransportFactory),
                protectedMaterialResolver)));
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new Iec104CommunicationRuntimePlanner(),
            new Iec104CommunicationRuntimeFactory(iec104AdapterFactory)));
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new AllenBradleyLogixCommunicationRuntimePlanner(),
            new AllenBradleyLogixCommunicationRuntimeFactory(logixClientFactory)));
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new OpcUaCommunicationRuntimePlanner(),
            new HostProtectedMaterialRuntimeFactory(
                new OpcUaCommunicationRuntimeFactory(opcUaSessionFactoryBuilder),
                protectedMaterialResolver)));
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new Dnp3CommunicationRuntimePlanner(),
            new Dnp3CommunicationRuntimeFactory(dnp3SessionFactory)));
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
