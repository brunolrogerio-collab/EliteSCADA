using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Drivers.Simulation;

namespace Scada.Api.HostedServices;

public sealed class SimulationDriverHostedService(
    SimulationDriver driver,
    ITagRegistry registry,
    IAlarmEngine alarmEngine) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await driver.StartAsync(cancellationToken);
        RegisterDemoAlarms();
    }

    public Task StopAsync(CancellationToken cancellationToken) => driver.StopAsync(cancellationToken);

    private void RegisterDemoAlarms()
    {
        if (registry.TryGetByPath("Demo.Discharge.Pressure", out var pressureTag) && pressureTag is not null)
        {
            alarmEngine.Register(AlarmDefinition.Create(
                "High discharge pressure", pressureTag.Id, AlarmType.High, AlarmPriority.High,
                setpoint: 9.0, area: "Demo", message: "Discharge pressure above 9.0 bar"));
        }

        if (registry.TryGetByPath("Demo.P01.Fault", out var faultTag) && faultTag is not null)
        {
            alarmEngine.Register(AlarmDefinition.Create(
                "Pump P01 fault", faultTag.Id, AlarmType.Digital, AlarmPriority.Critical,
                digitalActiveValue: true, area: "Demo", message: "Pump P01 fault active"));
        }
    }
}
