using System.IO.BACnet;

namespace Scada.Drivers.Bacnet;

public sealed record BacnetSessionOptions(
    int LocalPort = BacnetClient.DEFAULT_UDP_PORT,
    TimeSpan? RequestTimeout = null,
    int Retries = 2,
    TimeSpan? DiscoveryWindow = null,
    string? BbmdAddress = null,
    int? ForeignDeviceTtlSeconds = null)
{
    public TimeSpan EffectiveRequestTimeout => RequestTimeout ?? TimeSpan.FromSeconds(3);
    public TimeSpan EffectiveDiscoveryWindow => DiscoveryWindow ?? TimeSpan.FromMilliseconds(1500);

    public void Validate()
    {
        if (LocalPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(LocalPort));
        if (EffectiveRequestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        if (Retries is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(Retries));
        if (EffectiveDiscoveryWindow <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(DiscoveryWindow));
        if (ForeignDeviceTtlSeconds is < 30 or > 65535) throw new ArgumentOutOfRangeException(nameof(ForeignDeviceTtlSeconds));
        if (ForeignDeviceTtlSeconds.HasValue && string.IsNullOrWhiteSpace(BbmdAddress))
            throw new ArgumentException("BACnet Foreign Device Registration requires a BBMD address.");
    }
}

public sealed record BacnetDeviceObservation(
    uint DeviceInstance,
    BacnetAddress Address,
    uint MaximumApdu,
    BacnetSegmentations Segmentation,
    ushort VendorId)
{
    public string SanitizedEndpoint => Address.ToString();
}

public sealed record BacnetPropertyReadResult(
    BacnetBinding Binding,
    IReadOnlyList<BacnetValue> Values,
    DateTimeOffset ObservedAt);

public interface IBacnetSession : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task<BacnetDeviceObservation> ResolveDeviceAsync(uint deviceInstance, CancellationToken cancellationToken = default);
    IAsyncEnumerable<BacnetDeviceObservation> DiscoverAsync(int? maximumResults = null, CancellationToken cancellationToken = default);
    Task<BacnetPropertyReadResult> ReadAsync(BacnetBinding binding, CancellationToken cancellationToken = default);
    Task WriteAsync(BacnetBinding binding, IReadOnlyCollection<BacnetValue> values, CancellationToken cancellationToken = default);
    Task<IDisposable?> TrySubscribeCovAsync(
        BacnetBinding binding,
        Func<BacnetPropertyReadResult, ValueTask> onNotification,
        CancellationToken cancellationToken = default);
}

public interface IBacnetSessionFactory
{
    IBacnetSession Create(BacnetSessionOptions options);
}

public sealed class SystemIoBacnetSessionFactory : IBacnetSessionFactory
{
    public IBacnetSession Create(BacnetSessionOptions options) => new SystemIoBacnetSession(options);
}
