using System.Collections.Concurrent;
using System.Globalization;
using System.IO.BACnet;

namespace Scada.Drivers.Bacnet;

public sealed partial class SystemIoBacnetSession :
    IBacnetSession,
    IBacnetForeignDeviceRegistrationDiagnostics,
    IBacnetCovSubscriptionDiagnostics
{
    private static readonly BacnetPropertyIds[] CompanionPropertyIds =
    {
        BacnetPropertyIds.PROP_STATUS_FLAGS,
        BacnetPropertyIds.PROP_RELIABILITY,
        BacnetPropertyIds.PROP_OUT_OF_SERVICE,
        BacnetPropertyIds.PROP_UNITS
    };

    private readonly BacnetSessionOptions _options;
    private readonly BacnetClient _client;
    private readonly ConcurrentDictionary<uint, BacnetDeviceObservation> _devices = new();
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<BacnetDeviceObservation>> _deviceWaiters = new();
    private readonly object _covGate = new();
    private readonly List<CovRoute> _covRoutes = new();
    private readonly object _foreignDeviceGate = new();
    private CancellationTokenSource? _foreignDeviceRenewalCts;
    private Task? _foreignDeviceRenewalTask;
    private DateTimeOffset? _lastForeignDeviceRegistrationRequestAt;
    private DateTimeOffset? _nextForeignDeviceRegistrationAttemptAt;
    private long _foreignDeviceRegistrationRequestsSent;
    private long _foreignDeviceRegistrationFailures;
    private string? _foreignDeviceRegistrationLastErrorType;
    private long _covSubscribeRequests;
    private long _covSubscribeFailures;
    private long _covRenewalRequests;
    private long _covRenewalFailures;
    private DateTimeOffset? _covLastRenewalRequestAt;
    private DateTimeOffset? _covLastRenewalFailureAt;
    private string? _covLastRenewalErrorType;
    private long _covCancelRequests;
    private long _covCancelFailures;
    private string? _covLastErrorType;
    private int _subscriptionId;
    private int _disposeStarted;
    private bool _started;
    private bool _disposed;

    public SystemIoBacnetSession(BacnetSessionOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _client = new BacnetClient(
            _options.LocalPort,
            checked((int)_options.EffectiveRequestTimeout.TotalMilliseconds),
            _options.Retries);
        _client.OnIam += OnIam;
        _client.OnCOVNotification += OnCovNotification;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (_started) return Task.CompletedTask;
        _client.Start();
        if (IsForeignDeviceRegistrationConfigured())
        {
            var nextAttempt = BacnetForeignDeviceRegistrationPolicy.ExecuteAndScheduleNext(
                _options,
                SendForeignDeviceRegistration);
            _foreignDeviceRenewalCts = new CancellationTokenSource();
            SetNextForeignDeviceRegistrationAttempt(DateTimeOffset.UtcNow + nextAttempt.Delay);
            _foreignDeviceRenewalTask = RenewForeignDeviceRegistrationAsync(
                nextAttempt,
                _foreignDeviceRenewalCts.Token);
        }
        _started = true;
        return Task.CompletedTask;
    }

    public BacnetForeignDeviceRegistrationSnapshot GetForeignDeviceRegistrationDiagnostics()
    {
        lock (_foreignDeviceGate)
        {
            return new BacnetForeignDeviceRegistrationSnapshot(
                Configured: IsForeignDeviceRegistrationConfigured(),
                TtlSeconds: _options.ForeignDeviceTtlSeconds,
                RenewalInterval: _options.EffectiveForeignDeviceRenewalInterval,
                RetryInterval: _options.EffectiveForeignDeviceRetryInterval,
                LastRegistrationRequestAt: _lastForeignDeviceRegistrationRequestAt,
                NextRegistrationAttemptAt: _nextForeignDeviceRegistrationAttemptAt,
                RegistrationRequestsSent: _foreignDeviceRegistrationRequestsSent,
                RegistrationFailures: _foreignDeviceRegistrationFailures,
                LastErrorType: _foreignDeviceRegistrationLastErrorType);
        }
    }

    public BacnetCovSubscriptionSnapshot GetCovSubscriptionDiagnostics()
    {
        lock (_covGate)
        {
            var routes = _covRoutes
                .Select(x => x.GetRenewalDiagnostics())
                .OrderBy(x => x.SubscriptionId)
                .ToArray();
            var scheduled = routes
                .Where(x => x.NextRenewalAttemptAt.HasValue)
                .Select(x => x.NextRenewalAttemptAt!.Value)
                .OrderBy(x => x)
                .ToArray();
            var nextRenewalAttemptAt = scheduled.Length == 0
                ? (DateTimeOffset?)null
                : scheduled[0];

            return new BacnetCovSubscriptionSnapshot(
                ActiveSubscriptions: _covRoutes.Count,
                SubscribeRequests: Interlocked.Read(ref _covSubscribeRequests),
                SubscribeFailures: Interlocked.Read(ref _covSubscribeFailures),
                CancelRequests: Interlocked.Read(ref _covCancelRequests),
                CancelFailures: Interlocked.Read(ref _covCancelFailures),
                LastErrorType: _covLastErrorType,
                SubscriptionLifetime: _options.EffectiveCovSubscriptionLifetime,
                RenewalInterval: _options.EffectiveCovRenewalInterval,
                RetryInterval: _options.EffectiveCovRetryInterval,
                RenewalRequests: Interlocked.Read(ref _covRenewalRequests),
                RenewalFailures: Interlocked.Read(ref _covRenewalFailures),
                LastRenewalRequestAt: _covLastRenewalRequestAt,
                NextRenewalAttemptAt: nextRenewalAttemptAt,
                LastRenewalFailureAt: _covLastRenewalFailureAt,
                LastRenewalErrorType: _covLastRenewalErrorType,
                Routes: routes);
        }
    }

    public async Task<BacnetDeviceObservation> ResolveDeviceAsync(uint deviceInstance, CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        if (deviceInstance > BacnetBinding.MaximumDeviceInstance)
            throw new ArgumentOutOfRangeException(nameof(deviceInstance));
        if (_devices.TryGetValue(deviceInstance, out var cached)) return cached;

        var waiter = new TaskCompletionSource<BacnetDeviceObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var selected = _deviceWaiters.GetOrAdd(deviceInstance, waiter);
        SendWhoIs(deviceInstance, deviceInstance);
        try
        {
            return await selected.Task.WaitAsync(_options.EffectiveDiscoveryWindow, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"BACnet device instance {deviceInstance} did not answer Who-Is within {_options.EffectiveDiscoveryWindow.TotalMilliseconds:0} ms.");
        }
        finally
        {
            _deviceWaiters.TryRemove(deviceInstance, out _);
        }
    }

    public async IAsyncEnumerable<BacnetDeviceObservation> DiscoverAsync(
        int? maximumResults = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        if (maximumResults is <= 0) yield break;

        SendWhoIs(null, null);
        await Task.Delay(_options.EffectiveDiscoveryWindow, cancellationToken).ConfigureAwait(false);

        var observations = _devices.Values
            .OrderBy(x => x.DeviceInstance)
            .Take(maximumResults ?? int.MaxValue)
            .ToArray();
        foreach (var observation in observations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return observation;
        }
    }

    public async Task<BacnetPropertyReadResult> ReadAsync(BacnetBinding binding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        var device = await ResolveDeviceAsync(binding.DeviceInstance, cancellationToken).ConfigureAwait(false);
        var objectId = new BacnetObjectId((BacnetObjectTypes)binding.ObjectType, binding.ObjectInstance);

        try
        {
            var propertyReferences = BuildReadPropertyReferences(binding);
            var accessResults = await _client.ReadPropertyMultipleAsync(
                device.Address,
                objectId,
                propertyReferences,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var properties = accessResults
                .SelectMany(x => x.values ?? Array.Empty<BacnetPropertyValue>())
                .ToArray();
            var values = ExtractPropertyValues(properties, binding.PropertyIdentifier, binding.ArrayIndex);
            if (values.Count > 0)
            {
                return new BacnetPropertyReadResult(
                    binding,
                    values,
                    DateTimeOffset.UtcNow,
                    ParseObjectState(properties),
                    UsedReadPropertyMultiple: true);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Some BACnet devices reject RPM entirely or reject a request that
            // contains an unsupported optional companion property. RP remains
            // the deterministic compatibility fallback for the engineered value.
        }

        try
        {
            var fallbackValues = await _client.ReadPropertyAsync(
                device.Address,
                objectId,
                (BacnetPropertyIds)binding.PropertyIdentifier,
                arrayIndex: binding.ArrayIndex ?? uint.MaxValue,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var values = fallbackValues.ToArray();
            var objectState = await ReadCompanionObjectStateFallbackAsync(
                device.Address,
                objectId,
                binding,
                values,
                cancellationToken).ConfigureAwait(false);
            return new BacnetPropertyReadResult(
                binding,
                values,
                DateTimeOffset.UtcNow,
                objectState,
                UsedReadPropertyMultiple: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            InvalidateDevice(binding.DeviceInstance);
            throw;
        }
    }

    public async Task WriteAsync(BacnetBinding binding, IReadOnlyCollection<BacnetValue> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(values);
        binding.Validate();
        if (values.Count == 0) throw new ArgumentException("At least one BACnet value is required.", nameof(values));
        var device = await ResolveDeviceAsync(binding.DeviceInstance, cancellationToken).ConfigureAwait(false);
        try
        {
            await _client.WritePropertyAsync(
                device.Address,
                new BacnetObjectId((BacnetObjectTypes)binding.ObjectType, binding.ObjectInstance),
                (BacnetPropertyIds)binding.PropertyIdentifier,
                values,
                priority: binding.WritePriority,
                arrayIndex: binding.ArrayIndex ?? uint.MaxValue,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            InvalidateDevice(binding.DeviceInstance);
            throw;
        }
    }

    public async Task<IDisposable?> TrySubscribeCovAsync(
        BacnetBinding binding,
        Func<BacnetPropertyReadResult, ValueTask> onNotification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(onNotification);
        if (!binding.UseCov) return null;
        binding.Validate();

        var device = await ResolveDeviceAsync(binding.DeviceInstance, cancellationToken).ConfigureAwait(false);
        var subscriptionId = checked((uint)Interlocked.Increment(ref _subscriptionId));
        var objectId = new BacnetObjectId((BacnetObjectTypes)binding.ObjectType, binding.ObjectInstance);
        var lifetimeSeconds = checked((uint)_options.EffectiveCovSubscriptionLifetime.TotalSeconds);
        Interlocked.Increment(ref _covSubscribeRequests);
        try
        {
            await _client.SubscribeCOVAsync(
                device.Address,
                objectId,
                subscriptionId,
                cancel: false,
                issueConfirmedNotifications: false,
                lifetime: lifetimeSeconds,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _covSubscribeFailures);
            SetCovLastError(ex);
            return null;
        }

        var route = new CovRoute(binding, device.Address, objectId, subscriptionId, onNotification);
        route.ScheduleRenewal(DateTimeOffset.UtcNow + _options.EffectiveCovRenewalInterval);
        lock (_covGate)
        {
            _covRoutes.Add(route);
            _covLastErrorType = null;
        }
        route.StartRenewal(RenewCovSubscriptionAsync(route, route.RenewalToken));
        return new Subscription(this, route);
    }
}