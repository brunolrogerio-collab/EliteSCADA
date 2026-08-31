using System.Globalization;
using System.IO.BACnet;

namespace Scada.Drivers.Bacnet;

public sealed partial class SystemIoBacnetSession
{
    private async Task RenewCovSubscriptionAsync(CovRoute route, CancellationToken cancellationToken)
    {
        var lifetimeSeconds = checked((uint)_options.EffectiveCovSubscriptionLifetime.TotalSeconds);
        var renewalInterval = _options.EffectiveCovRenewalInterval;
        var retryInterval = _options.EffectiveCovRetryInterval;
        var delay = renewalInterval;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var requestedAt = DateTimeOffset.UtcNow;
            Interlocked.Increment(ref _covSubscribeRequests);
            Interlocked.Increment(ref _covRenewalRequests);
            route.RecordRenewalRequest(requestedAt);
            lock (_covGate) _covLastRenewalRequestAt = requestedAt;

            try
            {
                await _client.SubscribeCOVAsync(
                    route.Address,
                    route.ObjectId,
                    route.SubscriptionId,
                    cancel: false,
                    issueConfirmedNotifications: false,
                    lifetime: lifetimeSeconds,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                lock (_covGate) _covLastErrorType = null;
                delay = renewalInterval;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                var failedAt = DateTimeOffset.UtcNow;
                Interlocked.Increment(ref _covSubscribeFailures);
                Interlocked.Increment(ref _covRenewalFailures);
                route.RecordRenewalFailure(failedAt, ex.GetType().Name);
                lock (_covGate)
                {
                    _covLastRenewalFailureAt = failedAt;
                    _covLastRenewalErrorType = ex.GetType().Name;
                }
                SetCovLastError(ex);
                // Polling remains active as a safety net. Retry the same subscriber
                // identity promptly so silent peer-side subscription loss can heal.
                delay = retryInterval;
            }

            route.ScheduleRenewal(DateTimeOffset.UtcNow + delay);
        }
    }

    private async ValueTask CancelCovSubscriptionAsync(CovRoute route)
    {
        RemoveCovRoute(route);
        await route.StopRenewalAsync().ConfigureAwait(false);
        if (!route.TryBeginRemoteCancel()) return;

        Interlocked.Increment(ref _covCancelRequests);
        using var timeoutCts = new CancellationTokenSource(_options.EffectiveRequestTimeout + TimeSpan.FromSeconds(1));
        try
        {
            await _client.SubscribeCOVAsync(
                route.Address,
                route.ObjectId,
                route.SubscriptionId,
                cancel: true,
                issueConfirmedNotifications: false,
                lifetime: 0,
                cancellationToken: timeoutCts.Token).ConfigureAwait(false);
            lock (_covGate) _covLastErrorType = null;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _covCancelFailures);
            SetCovLastError(ex);
            // Cancellation is lifecycle cleanup. A remote peer that is offline or
            // rebooting must not prevent the local driver/session from stopping.
        }
    }

    private async Task CancelAllCovRoutesAsync()
    {
        CovRoute[] routes;
        lock (_covGate) routes = _covRoutes.ToArray();
        foreach (var route in routes)
            await CancelCovSubscriptionAsync(route).ConfigureAwait(false);
        lock (_covGate) _covRoutes.Clear();
    }

    private void RemoveCovRoute(CovRoute route)
    {
        lock (_covGate) _covRoutes.Remove(route);
    }

    private void SetCovLastError(Exception ex)
    {
        lock (_covGate) _covLastErrorType = ex.GetType().Name;
    }

    private static IList<BacnetPropertyReference> BuildReadPropertyReferences(BacnetBinding binding)
    {
        var references = new List<BacnetPropertyReference>
        {
            new(binding.PropertyIdentifier, binding.ArrayIndex ?? uint.MaxValue)
        };
        foreach (var companion in CompanionPropertyIds)
        {
            if ((uint)companion == binding.PropertyIdentifier && !binding.ArrayIndex.HasValue) continue;
            references.Add(new BacnetPropertyReference(companion));
        }
        return references;
    }

    private async Task<BacnetObjectState?> ReadCompanionObjectStateFallbackAsync(
        BacnetAddress address,
        BacnetObjectId objectId,
        BacnetBinding binding,
        IReadOnlyList<BacnetValue> engineeredValues,
        CancellationToken cancellationToken)
    {
        var properties = new List<BacnetPropertyValue>();
        if (!binding.ArrayIndex.HasValue && CompanionPropertyIds.Any(x => (uint)x == binding.PropertyIdentifier))
        {
            properties.Add(new BacnetPropertyValue
            {
                property = new BacnetPropertyReference(binding.PropertyIdentifier, uint.MaxValue),
                value = engineeredValues.ToList()
            });
        }

        foreach (var companion in CompanionPropertyIds)
        {
            if ((uint)companion == binding.PropertyIdentifier && !binding.ArrayIndex.HasValue) continue;
            try
            {
                var values = await _client.ReadPropertyAsync(
                    address,
                    objectId,
                    companion,
                    arrayIndex: uint.MaxValue,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (values.Count == 0) continue;
                properties.Add(new BacnetPropertyValue
                {
                    property = new BacnetPropertyReference(companion),
                    value = values.ToList()
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                // The engineered value already succeeded. Stop optional state
                // enrichment on timeout so fallback cannot multiply scan latency.
                break;
            }
            catch
            {
                // Optional companion properties vary widely by object/device.
                // Preserve any state already acquired and continue with the rest.
            }
        }

        return ParseObjectState(properties);
    }

    private static IReadOnlyList<BacnetValue> ExtractPropertyValues(
        IEnumerable<BacnetPropertyValue> properties,
        uint propertyIdentifier,
        uint? arrayIndex)
    {
        return properties
            .Where(x => x.property.propertyIdentifier == propertyIdentifier &&
                        (!arrayIndex.HasValue || x.property.propertyArrayIndex == arrayIndex.Value))
            .SelectMany(x => x.value ?? Array.Empty<BacnetValue>())
            .Where(x => x.Value is not BacnetError)
            .ToArray();
    }

    private static BacnetObjectState? ParseObjectState(IEnumerable<BacnetPropertyValue> properties)
    {
        var snapshot = properties.ToArray();
        var reliability = TryConvertUInt32(FirstPropertyRaw(snapshot, BacnetPropertyIds.PROP_RELIABILITY));
        var explicitOutOfService = TryConvertBoolean(FirstPropertyRaw(snapshot, BacnetPropertyIds.PROP_OUT_OF_SERVICE));
        var unitsRaw = FirstPropertyRaw(snapshot, BacnetPropertyIds.PROP_UNITS);
        var units = unitsRaw?.ToString();

        bool? inAlarm = null;
        bool? fault = null;
        bool? overridden = null;
        bool? statusOutOfService = null;
        if (FirstPropertyRaw(snapshot, BacnetPropertyIds.PROP_STATUS_FLAGS) is BacnetBitString flags)
        {
            inAlarm = TryGetBit(flags, 0);
            fault = TryGetBit(flags, 1);
            overridden = TryGetBit(flags, 2);
            statusOutOfService = TryGetBit(flags, 3);
        }

        var outOfService = explicitOutOfService == true || statusOutOfService == true
            ? true
            : explicitOutOfService ?? statusOutOfService;

        if (!reliability.HasValue && !inAlarm.HasValue && !fault.HasValue && !overridden.HasValue &&
            !outOfService.HasValue && string.IsNullOrWhiteSpace(units))
            return null;

        return new BacnetObjectState(reliability, inAlarm, fault, overridden, outOfService, units);
    }

    private static object? FirstPropertyRaw(IEnumerable<BacnetPropertyValue> properties, BacnetPropertyIds propertyId)
    {
        foreach (var property in properties)
        {
            if (property.property.propertyIdentifier != (uint)propertyId || property.value is null) continue;
            foreach (var value in property.value)
            {
                if (value.Value is null or BacnetError) continue;
                return value.Value;
            }
        }
        return null;
    }

    private static uint? TryConvertUInt32(object? raw)
    {
        if (raw is null) return null;
        try { return Convert.ToUInt32(raw, CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) { return null; }
    }

    private static bool? TryConvertBoolean(object? raw)
    {
        if (raw is bool value) return value;
        return null;
    }

    private static bool? TryGetBit(BacnetBitString flags, byte bit)
    {
        if (flags.Length <= bit) return null;
        try { return flags[bit]; }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private void InvalidateDevice(uint deviceInstance)
        => _devices.TryRemove(deviceInstance, out _);

    private void SendWhoIs(uint? lowLimit, uint? highLimit)
    {
        var low = lowLimit.HasValue ? checked((int)lowLimit.Value) : -1;
        var high = highLimit.HasValue ? checked((int)highLimit.Value) : -1;
        if (!string.IsNullOrWhiteSpace(_options.TargetAddress))
        {
            var receiver = new BacnetAddress(BacnetAddressTypes.IP, _options.TargetAddress.Trim());
            _client.WhoIs(low, high, receiver);
        }
        else
        {
            _client.WhoIs(low, high);
        }
        if (!string.IsNullOrWhiteSpace(_options.BbmdAddress))
            _client.RemoteWhoIs(_options.BbmdAddress, lowLimit: low, highLimit: high);
    }

    private void OnIam(BacnetClient sender, BacnetAddress address, uint deviceId, uint maxApdu, BacnetSegmentations segmentation, ushort vendorId)
    {
        if (deviceId > BacnetBinding.MaximumDeviceInstance) return;
        var observation = new BacnetDeviceObservation(deviceId, address, maxApdu, segmentation, vendorId);
        _devices[deviceId] = observation;
        if (_deviceWaiters.TryGetValue(deviceId, out var waiter)) waiter.TrySetResult(observation);
    }

    private void OnCovNotification(
        BacnetClient sender,
        BacnetAddress address,
        byte invokeId,
        uint subscriberProcessIdentifier,
        BacnetObjectId initiatingDeviceIdentifier,
        BacnetObjectId monitoredObjectIdentifier,
        uint timeRemaining,
        bool needConfirm,
        ICollection<BacnetPropertyValue> values,
        BacnetMaxSegments maxSegments)
    {
        CovRoute[] routes;
        lock (_covGate)
        {
            routes = _covRoutes
                .Where(x => x.SubscriptionId == subscriberProcessIdentifier &&
                            x.Address.Equals(address) &&
                            x.Binding.ObjectType == (uint)monitoredObjectIdentifier.Type &&
                            x.Binding.ObjectInstance == monitoredObjectIdentifier.Instance)
                .ToArray();
        }

        var objectState = ParseObjectState(values);
        foreach (var route in routes)
        {
            var matching = ExtractPropertyValues(values, route.Binding.PropertyIdentifier, route.Binding.ArrayIndex);
            if (matching.Count == 0) continue;
            _ = InvokeCovHandlerAsync(route, matching, objectState);
        }
    }

    private static async Task InvokeCovHandlerAsync(
        CovRoute route,
        IReadOnlyList<BacnetValue> values,
        BacnetObjectState? objectState)
    {
        try
        {
            await route.Handler(new BacnetPropertyReadResult(
                route.Binding,
                values,
                DateTimeOffset.UtcNow,
                objectState)).ConfigureAwait(false);
        }
        catch
        {
            // User callback failures must not escape the BACnet library receive thread.
        }
    }

    private void EnsureStarted()
    {
        ThrowIfDisposed();
        if (!_started) throw new InvalidOperationException("BACnet session has not been started.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class CovRoute(
        BacnetBinding binding,
        BacnetAddress address,
        BacnetObjectId objectId,
        uint subscriptionId,
        Func<BacnetPropertyReadResult, ValueTask> handler)
    {
        private readonly CancellationTokenSource _renewalCts = new();
        private readonly object _renewalDiagnosticsGate = new();
        private Task? _renewalTask;
        private DateTimeOffset? _lastRenewalRequestAt;
        private DateTimeOffset? _nextRenewalAttemptAt;
        private DateTimeOffset? _lastRenewalFailureAt;
        private long _renewalRequests;
        private long _renewalFailures;
        private string? _lastRenewalErrorType;
        private int _renewalStopped;
        private int _remoteCancelStarted;

        public BacnetBinding Binding { get; } = binding;
        public BacnetAddress Address { get; } = address;
        public BacnetObjectId ObjectId { get; } = objectId;
        public uint SubscriptionId { get; } = subscriptionId;
        public Func<BacnetPropertyReadResult, ValueTask> Handler { get; } = handler;
        public CancellationToken RenewalToken => _renewalCts.Token;

        public void StartRenewal(Task renewalTask) => _renewalTask = renewalTask;

        public void ScheduleRenewal(DateTimeOffset nextAttemptAt)
        {
            lock (_renewalDiagnosticsGate) _nextRenewalAttemptAt = nextAttemptAt;
        }

        public void RecordRenewalRequest(DateTimeOffset requestedAt)
        {
            lock (_renewalDiagnosticsGate)
            {
                _lastRenewalRequestAt = requestedAt;
                _renewalRequests++;
            }
        }

        public void RecordRenewalFailure(DateTimeOffset failedAt, string errorType)
        {
            lock (_renewalDiagnosticsGate)
            {
                _lastRenewalFailureAt = failedAt;
                _lastRenewalErrorType = errorType;
                _renewalFailures++;
            }
        }

        public BacnetCovRouteRenewalSnapshot GetRenewalDiagnostics()
        {
            lock (_renewalDiagnosticsGate)
            {
                return new BacnetCovRouteRenewalSnapshot(
                    SubscriptionId,
                    Binding.PortableAddress,
                    _lastRenewalRequestAt,
                    _nextRenewalAttemptAt,
                    _renewalRequests,
                    _renewalFailures,
                    _lastRenewalFailureAt,
                    _lastRenewalErrorType);
            }
        }

        public void CancelRenewal()
        {
            if (Volatile.Read(ref _renewalStopped) != 0) return;
            try { _renewalCts.Cancel(); } catch (ObjectDisposedException) { }
        }

        public async ValueTask StopRenewalAsync()
        {
            if (Interlocked.Exchange(ref _renewalStopped, 1) != 0) return;
            await _renewalCts.CancelAsync().ConfigureAwait(false);
            if (_renewalTask is not null)
            {
                try { await _renewalTask.ConfigureAwait(false); }
                catch (OperationCanceledException) when (_renewalCts.IsCancellationRequested) { }
            }
            _renewalCts.Dispose();
        }

        public bool TryBeginRemoteCancel() => Interlocked.Exchange(ref _remoteCancelStarted, 1) == 0;
    }

    private sealed class Subscription(SystemIoBacnetSession owner, CovRoute route) : IBacnetCovSubscription
    {
        private int _localDisposed;
        private int _asyncDisposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _localDisposed, 1) == 0)
            {
                route.CancelRenewal();
                owner.RemoveCovRoute(route);
            }
        }

        public async ValueTask DisposeAsync()
        {
            Dispose();
            if (Interlocked.Exchange(ref _asyncDisposed, 1) != 0) return;
            await owner.CancelCovSubscriptionAsync(route).ConfigureAwait(false);
        }
    }
}