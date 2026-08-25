using Scada.Drivers.Simulation;

namespace Scada.Api.HostedServices;

public sealed class SimulationDriverHostedService(SimulationDriver driver) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => driver.StartAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => driver.StopAsync(cancellationToken);
}
