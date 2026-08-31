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
    public const int DefaultCovSubscriptionLifetimeSeconds = 300;

    public TimeSpan EffectiveRequestTimeout => RequestTimeout ?? TimeSpan.FromSeconds(3);
    public TimeSpan EffectiveDiscoveryWindow => DiscoveryWindow ?? TimeSpan.FromMilliseconds(1500);
    public TimeSpan? EffectiveForeignDeviceRenewalInterval => ForeignDeviceTtlSeconds.HasValue
        ? TimeSpan.FromSeconds(ForeignDeviceTtlSeconds.Value * 0.75d)
        : null;
    public TimeSpan? EffectiveForeignDeviceRetryInterval => ForeignDeviceTtlSeconds.HasValue
        ? TimeSpan.FromSeconds(Math.Clamp(ForeignDeviceTtlSeconds.Value * 0.10d, 5d, 30d))
        : null;

    // First-cut COV lifecycle policy is intentionally fixed until simulator and
    // multi-vendor evidence justifies exposing another Engineering tuning knob.
    // A bounded lease lets a normal renewal repair silent remote subscription loss.
    public TimeSpan EffectiveCovSubscriptionLifetime => TimeSpan.FromSeconds(DefaultCovSubscriptionLifetimeSeconds);
    public TimeSpan EffectiveCovRenewalInterval => TimeSpan.FromSeconds(DefaultCovSubscriptionLifetimeSeconds * 0.75d);
    public TimeSpan EffectiveCovRetryInterval => TimeSpan.FromSeconds(30);

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

/// <summary>
/// BACnet/IP network-level Foreign Device Registration lease state. A sent
/// registration request is transport evidence only; it is not represented as a
/// confirmed BBMD acceptance unless the underlying stack exposes that evidence.
/// </summary>
public sealed record BacnetForeignDeviceRegistrationSnapshot(
    bool Configured,
    int? TtlSeconds,
    TimeSpan? RenewalInterval,
    TimeSpan? RetryInterval,
    DateTimeOffset? LastRegistrationRequestAt,
    DateTimeOffset? NextRegistrationAttemptAt,
    long RegistrationRequestsSent,
    long RegistrationFailures,
    string? LastErrorType);

/// <summary>
/// Per-route local COV renewal evidence. PortableAddress is stable Engineering
/// identity; no transient IP address is required to identify the route here.
/// These fields describe local scheduling/request activity, not remote retention.
/// </summary>
public sealed record BacnetCovRouteRenewalSnapshot(
    uint SubscriptionId,
    string PortableAddress,
    DateTimeOffset? LastRenewalRequestAt,
    DateTimeOffset? NextRenewalAttemptAt,
    long RenewalRequests,
    long RenewalFailures,
    DateTimeOffset? LastRenewalFailureAt,
    string? LastRenewalErrorType);

/// <summary>
/// Protocol-local COV subscription lifecycle evidence. The counters deliberately
/// distinguish local route state from remote subscribe/cancel request attempts.
/// They are diagnostics, not proof that a peer retained a subscription after a
/// reboot or network partition.
/// </summary>
public sealed record BacnetCovSubscriptionSnapshot(
    int ActiveSubscriptions,
    long SubscribeRequests,
    long SubscribeFailures,
    long CancelRequests,
    long CancelFailures,
    string? LastErrorType,
    TimeSpan? SubscriptionLifetime = null,
    TimeSpan? RenewalInterval = null,
    TimeSpan? RetryInterval = null,
    long RenewalRequests = 0,
    long RenewalFailures = 0,
    DateTimeOffset? LastRenewalRequestAt = null,
    DateTimeOffset? NextRenewalAttemptAt = null,
    DateTimeOffset? LastRenewalFailureAt = null,
    string? LastRenewalErrorType = null,
    IReadOnlyList<BacnetCovRouteRenewalSnapshot>? Routes = null);

/// <summary>
/// Optional BACnet-specific diagnostic seam. It keeps network-lease state owned
/// by the BACnet adapter while allowing the driver to project it through the
/// existing protocol-details dictionary without changing common contracts.
/// </summary>
public interface IBacnetForeignDeviceRegistrationDiagnostics
{
    BacnetForeignDeviceRegistrationSnapshot GetForeignDeviceRegistrationDiagnostics();
}

/// <summary>
/// Optional BACnet-specific COV diagnostic seam. It intentionally stays outside
/// the common driver contracts because subscriber process identifiers and remote
/// COV cancellation are BACnet protocol concerns.
/// </summary>
public interface IBacnetCovSubscriptionDiagnostics
{
    BacnetCovSubscriptionSnapshot GetCovSubscriptionDiagnostics();
}

/// <summary>
/// A BACnet COV subscription can be disposed synchronously for local route
/// removal, while async disposal additionally permits a bounded remote cancel.
/// BACnetIpDriver always uses async disposal for normal lifecycle operations.
/// </summary>
public interface IBacnetCovSubscription : IDisposable, IAsyncDisposable
{
}

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