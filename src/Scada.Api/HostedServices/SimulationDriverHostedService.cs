using Scada.Api.Runtime;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Simulation;

namespace Scada.Api.HostedServices;

public sealed class SimulationDriverHostedService(
    SimulationDriver driver,
    DemoRuntimeServices demoRuntime,
    IEngineeringRuntimeCoordinator engineeringRuntime,
    RuntimeHighAvailabilityService? highAvailability = null) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (engineeringRuntime.Describe().Revision.HasValue)
            return;
        if (highAvailability?.Enabled == true &&
            !highAvailability.CanOwnIndustrialEffects())
        {
            return;
        }

        await driver.StartAsync(cancellationToken);

        foreach (var alarm in DemoProcessModel.CreateAlarmDefinitions())
            demoRuntime.Alarms.Register(alarm);
    }

    public Task StopAsync(CancellationToken cancellationToken) => driver.StopAsync(cancellationToken);
}
