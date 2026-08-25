namespace Scada.Drivers.Abstractions;

[Flags]
public enum DriverCapabilities
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    Subscribe = 1 << 2,
    Browse = 1 << 3,
    Discover = 1 << 4,
    Diagnostics = 1 << 5
}
