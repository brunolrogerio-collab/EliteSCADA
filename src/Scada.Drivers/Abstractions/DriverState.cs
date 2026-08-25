namespace Scada.Drivers.Abstractions;

public enum DriverState
{
    Stopped,
    Starting,
    Running,
    Faulted,
    Stopping
}
