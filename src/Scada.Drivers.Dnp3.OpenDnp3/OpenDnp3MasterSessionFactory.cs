using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Dnp3.OpenDnp3;

public sealed class OpenDnp3MasterSessionFactory : IDnp3MasterSessionFactory
{
    public IDnp3MasterSession Create(Dnp3TcpConnectionOptions connectionOptions)
    {
        ArgumentNullException.ThrowIfNull(connectionOptions);
        connectionOptions.Validate();
        return new OpenDnp3MasterSession(connectionOptions);
    }
}
