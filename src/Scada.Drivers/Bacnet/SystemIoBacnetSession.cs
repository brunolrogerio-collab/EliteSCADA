using System.Collections.Concurrent;
using System.Globalization;
using System.IO.BACnet;

namespace Scada.Drivers.Bacnet;

public sealed class SystemIoBacnetSession : IBacnetSession
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
    private int _subscriptionId;
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
        if (!string.IsNullOrWhiteSpace(_options.BbmdAddress) && _options.ForeignDeviceTtlSeconds.HasValue)
            _client.RegisterAsForeignDevice(_options.BbmdAddress, checked((short)_options.ForeignDeviceTtlSeconds.Value));
        _started = true;
        return Task.CompletedTask;
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
            return new BacnetPropertyReadResult(binding, fallbackValues.ToArray(), DateTimeOffset.UtcNow);
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
        var route = new CovRoute(binding, device.Address, onNotification);
        var subscriptionId = checked((uint)Interlocked.Increment(ref _subscriptionId));
        try
        {
            await _client.SubscribeCOVAsync(
                device.Address,
                new BacnetObjectId((BacnetObjectTypes)binding.ObjectType, binding.ObjectInstance),
                subscriptionId,
                cancel: false,
                issueConfirmedNotifications: false,
                lifetime: 0,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }

        lock (_covGate) _covRoutes.Add(route);
        return new Subscription(() =>
        {
            lock (_covGate) _covRoutes.Remove(route);
        });
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _client.OnIam -= OnIam;
        _client.OnCOVNotification -= OnCovNotification;
        _client.Dispose();
        lock (_covGate) _covRoutes.Clear();
        return ValueTask.CompletedTask;
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
                .Where(x => x.Address.Equals(address) &&
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

    private sealed record CovRoute(BacnetBinding Binding, BacnetAddress Address, Func<BacnetPropertyReadResult, ValueTask> Handler);

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
