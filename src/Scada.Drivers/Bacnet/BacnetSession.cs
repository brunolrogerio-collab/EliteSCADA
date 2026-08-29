using System.IO.BACnet;

namespace Scada.Drivers.Bacnet;

public sealed record BacnetSessionOptions(
    int LocalPort = BacnetClient.DEFAULT_UDP_PORT,
    TimeSpan? RequestTimeout = null,
    int Retries = 2,
    TimeSpan? DiscoveryWindow = null,
    string? BbmdAddress = null,
    int? ForeignDeviceTtlSeconds = null,
    string? TargetAddress = null)
{
    public TimeSpan EffectiveRequestTimeout => RequestTimeout ?? TimeSpan.FromSeconds(3);
    public TimeSpan EffectiveDiscoveryWindow => DiscoveryWindow ?? TimeSpan.FromMilliseconds(1500);

    public void Validate()
    {
        if (LocalPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(LocalPort));
        if (EffectiveRequestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        if (Retries is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(Retries));
        if (EffectiveDiscoveryWindow <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(DiscoveryWindow));
        if (ForeignDeviceTtlSeconds is < 30 or > short.MaxValue) throw new ArgumentOutOfRangeException(nameof(ForeignDeviceTtlSeconds));
        if (ForeignDeviceTtlSeconds.HasValue && string.IsNullOrWhiteSpace(BbmdAddress))
            throw new ArgumentException("BACnet Foreign Device Registration requires a BBMD address.");
        if (!string.IsNullOrWhiteSpace(TargetAddress))
        {
            try { _ = new BacnetAddress(BacnetAddressTypes.IP, TargetAddress.Trim()); }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                throw new ArgumentException("BACnet targetAddress must be an IPv4 address with optional UDP port, for example '192.168.1.20:47808'.", nameof(TargetAddress), ex);
            }
        }
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

/// <summary>
/// Protocol-neutral projection of BACnet object health/capability evidence that
/// accompanies a sampled property. Null members mean the peer did not provide
/// or support that companion property; they are not silently treated as faults.
/// </summary>
public sealed record BacnetObjectState(
    uint? Reliability = null,
    bool? InAlarm = null,
    bool? Fault = null,
    bool? Overridden = null,
    bool? OutOfService = null,
    string? Units = null);

public sealed record BacnetPropertyReadResult(
    BacnetBinding Binding,
    IReadOnlyList<BacnetValue> Values,
    DateTimeOffset ObservedAt,
    BacnetObjectState? ObjectState = null,
    bool UsedReadPropertyMultiple = false);

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
