namespace Scada.Drivers.Iec60870;

/// <summary>
/// Library-neutral client seam for the IEC-104 runtime. Concrete protocol-library types remain behind adapters.
/// </summary>
public interface IIec104ClientAdapter : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(
        string host,
        int port,
        Iec104SessionOptions options,
        CancellationToken cancellationToken = default);

    Task StartDataTransferAsync(CancellationToken cancellationToken = default);

    Task StopDataTransferAsync(CancellationToken cancellationToken = default);

    ValueTask SendAsync(
        Iec104AsduEnvelope asdu,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<Iec104AsduEnvelope> ReadAsync(
        CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
