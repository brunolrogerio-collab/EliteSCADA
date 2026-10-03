namespace Scada.Drivers.Modbus;

public sealed record ModbusMasterTransportDiagnosticSnapshot(
    string Endpoint,
    TimeSpan RequestTimeout,
    bool IsConnected,
    long ConnectionCount,
    long DisconnectionCount,
    long ReconnectCount,
    long RequestAttempts,
    long SuccessfulRequestAttempts,
    long FailedRequestAttempts,
    long TimeoutCount,
    long CrcErrorCount,
    long ProtocolExceptionCount,
    TimeSpan? LastRequestDuration,
    TimeSpan? AverageRequestDuration,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastDisconnectedAt,
    IReadOnlyDictionary<string, string>? Details = null);

public interface IModbusMasterTransport : IAsyncDisposable
{
    string Endpoint { get; }
    TimeSpan RequestTimeout { get; }
    bool IsConnected { get; }
    IReadOnlyDictionary<string, string> ProtocolDetails { get; }

    ModbusMasterTransportDiagnosticSnapshot GetMasterDiagnostics();

    Task<bool[]> ReadBitsAsync(
        byte unitId,
        ModbusDataArea area,
        ushort address,
        ushort quantity,
        CancellationToken cancellationToken = default);

    Task<ushort[]> ReadRegistersAsync(
        byte unitId,
        ModbusDataArea area,
        ushort address,
        ushort quantity,
        CancellationToken cancellationToken = default);

    Task WriteSingleCoilAsync(
        byte unitId,
        ushort address,
        bool value,
        CancellationToken cancellationToken = default);

    Task WriteSingleRegisterAsync(
        byte unitId,
        ushort address,
        ushort value,
        CancellationToken cancellationToken = default);

    Task WriteMultipleRegistersAsync(
        byte unitId,
        ushort address,
        IReadOnlyList<ushort> values,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}
