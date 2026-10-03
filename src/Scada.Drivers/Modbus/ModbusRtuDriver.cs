using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Modbus;

public sealed class ModbusRtuDriver : ModbusMasterDriver, ICommunicationDriverResourceClaimSource
{
    private readonly HostSerialLineSettings _serialSettings;
    public ModbusRtuDriver(
        string driverId,
        string name,
        HostSerialBusCoordinator coordinator,
        HostSerialLineSettings settings,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<ModbusPoint> points,
        TimeSpan? scanRate = null,
        TimeSpan? requestTimeout = null,
        int maxGapElements = 8)
        : this(
            driverId,
            name,
            coordinator,
            settings,
            cache,
            registry,
            points?.ToArray() ?? throw new ArgumentNullException(nameof(points)),
            scanRate,
            requestTimeout,
            maxGapElements)
    {
    }

    private ModbusRtuDriver(
        string driverId,
        string name,
        HostSerialBusCoordinator coordinator,
        HostSerialLineSettings settings,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IReadOnlyCollection<ModbusPoint> points,
        TimeSpan? scanRate,
        TimeSpan? requestTimeout,
        int maxGapElements)
        : base(
            driverId,
            name,
            ModbusRtuDriverDescriptorProvider.DriverTypeId,
            new ModbusRtuTransport(
                coordinator,
                settings,
                driverId,
                points.Select(point => point.UnitId).Distinct().ToArray(),
                requestTimeout),
            cache,
            registry,
            points,
            scanRate,
            maxGapElements)
    {
        _serialSettings = settings;
    }

    public IReadOnlyCollection<CommunicationDriverResourceClaim> ResourceClaims => new[]
    {
        new CommunicationDriverResourceClaim(
            "serial-port",
            string.Empty,
            _serialSettings.PhysicalPortKey,
            DriverId,
            $"{_serialSettings.BaudRate}|{_serialSettings.DataBits}|{_serialSettings.Parity}|{_serialSettings.StopBits}",
            Exclusive: false)
    };
}
