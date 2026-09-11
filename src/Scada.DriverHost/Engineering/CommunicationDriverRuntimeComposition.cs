using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.AllenBradley;
using Scada.Drivers.Bacnet;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Iec60870;
using Scada.Drivers.Mqtt;
using Scada.Drivers.OpcUa;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.DriverHost.Engineering;

/// <summary>
/// Canonical composition point for communication drivers that have completed
/// coordinator convergence. Keeping this registry explicit prevents protocol
/// workers from introducing private activation seams into the runtime host.
/// The same registration also carries the Engineering descriptor so runtime
/// availability and the product catalog cannot drift into separate lists.
/// </summary>
public static class CommunicationDriverRuntimeComposition
{
    public static CommunicationDriverRuntimeComponentRegistry BuildForCurrentSchema(
        Func<IMqttClientTransport>? mqttTransportFactory = null,
        ICommunicationDriverProtectedMaterialResolver? hostProtectedMaterialResolver = null,
        Func<IIec104ClientAdapter>? iec104AdapterFactory = null,
        ILogixProtocolClientFactory? logixClientFactory = null,
        Func<OpcUaRuntimeConnectionOptions, IOpcUaRuntimeSecurityMaterialProvider, IOpcUaRuntimeSessionFactory>? opcUaSessionFactoryBuilder = null,
        IDnp3MasterSessionFactory? dnp3SessionFactory = null,
        IBacnetSessionFactory? bacnetSessionFactory = null)
    {
        var protectedMaterialResolver = hostProtectedMaterialResolver
            ?? EnvironmentCommunicationDriverProtectedMaterialResolver.CreateDeterministicScopedEnvironment();
        var registry = new CommunicationDriverRuntimeComponentRegistry();
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new MqttCommunicationRuntimePlanner(),
            new HostProtectedMaterialRuntimeFactory(
                new MqttCommunicationRuntimeFactory(mqttTransportFactory),
                protectedMaterialResolver),
            new MqttDriverDescriptorProvider().Descriptor));
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new Iec104CommunicationRuntimePlanner(),
            new Iec104CommunicationRuntimeFactory(iec104AdapterFactory),
            Iec104DriverDescriptorProvider.Enrich(new Iec104EngineeringProvider(iec104AdapterFactory).Descriptor)));
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new AllenBradleyLogixCommunicationRuntimePlanner(),
            new AllenBradleyLogixCommunicationRuntimeFactory(logixClientFactory),
            new AllenBradleyLogixEngineeringAdapter(logixClientFactory).Descriptor));
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new OpcUaCommunicationRuntimePlanner(),
            new HostProtectedMaterialRuntimeFactory(
                new OpcUaCommunicationRuntimeFactory(opcUaSessionFactoryBuilder),
                protectedMaterialResolver),
            OpcUaDriverDescriptorProvider.Definition));
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new Dnp3CommunicationRuntimePlanner(),
            new Dnp3CommunicationRuntimeFactory(dnp3SessionFactory),
            Dnp3DriverDescriptorProvider.SharedDescriptor));
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new S7IsoCommunicationRuntimePlanner(),
            new S7IsoCommunicationRuntimeFactory(),
            new S7IsoEngineeringAdapter().Descriptor));
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new BacnetCommunicationRuntimePlanner(),
            new BacnetCommunicationRuntimeFactory(bacnetSessionFactory),
            BacnetDriverDescriptor.Instance));
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
