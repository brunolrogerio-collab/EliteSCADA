using Scada.Core.Tags;

namespace Scada.Drivers.Modbus;

/// <summary>
/// Compatibility wrapper preserving the established public ModbusTcpDriver surface
/// while delegating polling/write/lifecycle behavior to the common master core.
/// </summary>
public sealed class ModbusTcpDriver : ModbusMasterDriver
{
    public ModbusTcpDriver(
        string driverId,
        string name,
        string host,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<ModbusPoint> points,
        int port = 502,
        TimeSpan? scanRate = null,
        TimeSpan? requestTimeout = null,
        int maxGapElements = 8)
        : base(
            driverId,
            name,
            ModbusTcpDriverDescriptorProvider.DriverTypeId,
            new ModbusTcpTransport(host, port, requestTimeout),
            cache,
            registry,
            points,
            scanRate,
            maxGapElements)
    {
    }
}
