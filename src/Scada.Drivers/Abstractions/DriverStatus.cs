namespace Scada.Drivers.Abstractions;

public sealed record DriverStatus(
    string DriverId,
    string Name,
    DriverState State,
    DateTimeOffset Timestamp,
    string? Message = null,
    long UpdatesPublished = 0);
