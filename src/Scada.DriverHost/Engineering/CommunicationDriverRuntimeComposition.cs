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
        Func<IMqttClientTransport>? mqttTransportFactory = null)
    {
        var registry = new CommunicationDriverRuntimeComponentRegistry();
        registry.Register(new CommunicationDriverRuntimeComponentRegistration(
            new MqttCommunicationRuntimePlanner(),
            new MqttCommunicationRuntimeFactory(mqttTransportFactory)));
        return registry;
    }
}
