using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;

namespace Scada.DriverHost.Engineering;

/// <summary>Common TAG/cache boundary for the isolated Matter Wi-Fi preview.</summary>
public sealed class MatterWifiCommunicationDriver : ICommunicationDriver, ICommunicationDiagnosticsSource, ICommunicationDriverReadinessSource
{
    private readonly MatterWifiCommunicationRuntimePlan _plan;
    private readonly CommunicationDriverRuntimeServices _services;
    private readonly IMatterWifiSidecarSession _session;
    private readonly IReadOnlyDictionary<Guid, MatterWifiPoint> _pointsById;
    private readonly ConcurrentDictionary<Guid, MatterWifiObservation> _startupObservations = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastCommunicationAt;
    private string? _lastError;
    private long _requests, _successes, _failures, _reads, _writes, _updates;
    private int _running, _starting, _disposed;

    public MatterWifiCommunicationDriver(MatterWifiCommunicationRuntimePlan plan, CommunicationDriverRuntimeServices services, IMatterWifiSidecarSession session)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _services.Validate();
        _pointsById = plan.Points.ToDictionary(point => point.Tag.Id);
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, _stateChangedAt);
        _session.ObservationReceived += OnObservation;
    }

    public string DriverId => _plan.DataSourceKey;
    public string Name => _plan.Name;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _plan.Tags;
    public DriverCapabilities Capabilities => DriverCapabilities.Read | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics |
        (_plan.Points.Any(point => point.Writable && !point.Tag.ReadOnly) ? DriverCapabilities.Write : DriverCapabilities.None);

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _running) != 0) throw new InvalidOperationException($"Matter Wi-Fi Data Source '{DriverId}' is already active.");
            if (!_services.CanAcquireInputs) throw new InvalidOperationException("Matter Wi-Fi Standby Runtime cannot open the sidecar or acquire Matter inputs.");
            foreach (var point in _plan.Points) _services.Registry.Upsert(point.Tag);
            SetStatus(DriverState.Starting, null);
            Volatile.Write(ref _starting, 1);
            var sessionStarted = false;
            try
            {
                await _session.StartAsync(cancellationToken).ConfigureAwait(false);
                sessionStarted = true;
                Volatile.Write(ref _running, 1);
                Volatile.Write(ref _starting, 0);
                foreach (var observation in _startupObservations.Values.OrderBy(item => item.ObservedAt))
                {
                    if (_pointsById.TryGetValue(observation.TagId, out var point))
                        await PublishStateAsync(point, ConvertValue(observation.Value, point.Tag.DataType), "matter.attribute-report", cancellationToken).ConfigureAwait(false);
                }
                _startupObservations.Clear();
                SetStatus(DriverState.Running, null);
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _running, 0);
                Volatile.Write(ref _starting, 0);
                _startupObservations.Clear();
                if (sessionStarted)
                {
                    try { await _session.StopAsync(CancellationToken.None).ConfigureAwait(false); }
                    catch { /* Keep the initial startup failure as the cause. */ }
                }
                RecordFailure(exception);
                SetStatus(DriverState.Faulted, _lastError);
                throw;
            }
        }
        finally { _lifecycleGate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _running) == 0) return;
            SetStatus(DriverState.Stopping, null);
            try
            {
                await _session.StopAsync(cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _running, 0);
                SetStatus(DriverState.Stopped, null);
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _running, 0);
                RecordFailure(exception);
                SetStatus(DriverState.Faulted, _lastError);
                throw;
            }
        }
        finally { _lifecycleGate.Release(); }
    }

    public async ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        var point = GetPoint(tagId);
        if (Volatile.Read(ref _running) == 0 || !_services.CanAcquireInputs)
            return _services.Cache.TryGet(tagId, out var cached) ? cached : null;
        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _reads);
        try
        {
            var json = await _session.ReadAsync(point, cancellationToken).ConfigureAwait(false);
            return await PublishStateAsync(point, ConvertValue(json, point.Tag.DataType), "matter.attribute-readback", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RecordFailure(exception);
            await PublishBadCommunicationAsync(point, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        var point = GetPoint(tagId);
        if (point.Tag.ReadOnly || !point.Writable) throw new InvalidOperationException($"Matter TAG '{point.Tag.Path}' is read-only.");
        var requested = ValidateValue(point.Tag.DataType, value);
        if (Volatile.Read(ref _running) == 0 || !_services.CanOwnExternalEffects)
            throw new InvalidOperationException("Matter Wi-Fi Runtime does not currently own external effects.");
        Interlocked.Increment(ref _requests);
        Interlocked.Increment(ref _writes);
        try
        {
            var json = await _session.WriteAndReadBackAsync(point, requested, cancellationToken).ConfigureAwait(false);
            var observed = ConvertValue(json, point.Tag.DataType);
            if (!Equals(requested, observed)) throw new MatterWifiWriteUnconfirmedException(point.Tag.Id, "READBACK_MISMATCH");
            await PublishStateAsync(point, observed, "matter.confirmed-write", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Once the remote write/readback path has been entered, any failure leaves the process value uncertain.
            RecordFailure(exception);
            await PublishUncertainAsync(point).ConfigureAwait(false);
            throw;
        }
    }

    public CommunicationDriverReadinessSnapshot GetCommunicationReadiness() => new(
        DriverId,
        MatterWifiDriverDescriptorProvider.DriverType,
        Volatile.Read(ref _running) != 0 ? CommunicationDriverReadinessState.Ready :
            Status.State == DriverState.Faulted ? CommunicationDriverReadinessState.Faulted : CommunicationDriverReadinessState.Stopped,
        DateTimeOffset.UtcNow,
        _lastError,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["stateAuthority"] = "Matter attribute report or explicit read",
            ["scope"] = "already-commissioned-persistent-values-only",
            ["haSupport"] = "not-supported",
            ["productionSupport"] = "not-supported"
        });

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var source = _session.GetCommunicationDiagnostics();
        return source with
        {
            DataSourceKey = DriverId,
            DataSourceName = Name,
            DriverType = MatterWifiDriverDescriptorProvider.DriverType,
            RuntimeInstanceId = _runtimeInstanceId,
            State = Status.State switch
            {
                DriverState.Starting => CommunicationDriverOperationalState.Starting,
                DriverState.Running => CommunicationDriverOperationalState.Healthy,
                DriverState.Stopping => CommunicationDriverOperationalState.Stopping,
                DriverState.Faulted => CommunicationDriverOperationalState.Faulted,
                _ => CommunicationDriverOperationalState.Stopped
            },
            StateChangedAt = _stateChangedAt,
            LastSuccessfulCommunicationAt = _lastCommunicationAt ?? source.LastSuccessfulCommunicationAt,
            LastError = _lastError ?? source.LastError,
            AssociatedTagCount = _plan.Points.Count,
            TagQuality = BuildQualitySummary(),
            Counters = source.Counters with
            {
                Requests = Interlocked.Read(ref _requests),
                SuccessfulOperations = Interlocked.Read(ref _successes),
                FailedOperations = Interlocked.Read(ref _failures),
                ReadOperations = Interlocked.Read(ref _reads),
                WriteOperations = Interlocked.Read(ref _writes),
                UpdatesPublished = Interlocked.Read(ref _updates)
            },
            ProtocolDetails = new Dictionary<string, string>(source.ProtocolDetails ?? new Dictionary<string, string>(), StringComparer.Ordinal)
            {
                ["protocol"] = "Matter Wi-Fi",
                ["stateAuthority"] = "Matter attribute report or explicit read",
                ["scope"] = "already-commissioned-persistent-values-only",
                ["physicalCompatibilityClaim"] = "deferred",
                ["haSupport"] = "not-supported"
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _session.ObservationReceived -= OnObservation;
        try { await StopAsync().ConfigureAwait(false); }
        finally
        {
            try { await _session.DisposeAsync().ConfigureAwait(false); }
            finally { _lifecycleGate.Dispose(); }
        }
    }

    private MatterWifiPoint GetPoint(Guid tagId) => _pointsById.TryGetValue(tagId, out var point)
        ? point : throw new KeyNotFoundException($"Matter TAG '{tagId}' is not part of Data Source '{DriverId}'.");

    private void OnObservation(MatterWifiObservation observation)
    {
        if (!_services.CanAcquireInputs || !_pointsById.ContainsKey(observation.TagId)) return;
        if (Volatile.Read(ref _running) == 0)
        {
            if (Volatile.Read(ref _starting) != 0) _startupObservations[observation.TagId] = observation;
            return;
        }
        if (_pointsById.TryGetValue(observation.TagId, out var point)) _ = PublishObservationAsync(point, observation);
    }

    private async Task PublishObservationAsync(MatterWifiPoint point, MatterWifiObservation observation)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _running) == 0 || !_services.CanAcquireInputs) return;
            await PublishStateAsync(point, ConvertValue(observation.Value, point.Tag.DataType), "matter.attribute-report", CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) { RecordFailure(exception); }
        finally { _lifecycleGate.Release(); }
    }

    private async ValueTask<TagValue?> PublishStateAsync(MatterWifiPoint point, object value, string source, CancellationToken cancellationToken)
    {
        var updated = await _services.Cache.UpdateAsync(point.Tag, TagValue.Good(point.Tag.Id, value, source), cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _successes);
        if (updated is not null) Interlocked.Increment(ref _updates);
        _lastCommunicationAt = DateTimeOffset.UtcNow;
        _lastError = null;
        return updated;
    }

    private async ValueTask PublishBadCommunicationAsync(MatterWifiPoint point, CancellationToken cancellationToken) =>
        _ = await _services.Cache.UpdateAsync(point.Tag,
            new TagValue(point.Tag.Id, null, DateTimeOffset.UtcNow, TagQuality.BadCommunication, "matter.wifi"), cancellationToken).ConfigureAwait(false);

    private async ValueTask PublishUncertainAsync(MatterWifiPoint point)
    {
        _services.Cache.TryGet(point.Tag.Id, out var current);
        await _services.Cache.UpdateAsync(point.Tag,
            new TagValue(point.Tag.Id, current?.Value, DateTimeOffset.UtcNow, TagQuality.Uncertain, "matter.wifi.write-outcome-unknown"),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static object ValidateValue(TagDataType type, object? value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        var valid = type switch
        {
            TagDataType.Boolean => value is bool,
            TagDataType.Int16 => value is short,
            TagDataType.Int32 => value is int,
            TagDataType.Int64 => value is long,
            TagDataType.Float => value is float number && float.IsFinite(number),
            TagDataType.Double => value is double number && double.IsFinite(number),
            TagDataType.String => value is string text && text.Length <= 4096,
            _ => false
        };
        return valid ? value : throw new ArgumentException($"Matter write value does not match the canonical {type} TAG type.", nameof(value));
    }

    private static object ConvertValue(JsonElement value, TagDataType type) => type switch
    {
        TagDataType.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
        TagDataType.Int16 when value.TryGetInt16(out var number) => number,
        TagDataType.Int32 when value.TryGetInt32(out var number) => number,
        TagDataType.Int64 when value.TryGetInt64(out var number) => number,
        TagDataType.Int64 when value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) => number,
        TagDataType.Float when value.TryGetSingle(out var number) && float.IsFinite(number) => number,
        TagDataType.Double when value.TryGetDouble(out var number) && double.IsFinite(number) => number,
        TagDataType.String when value.ValueKind == JsonValueKind.String && value.GetString() is { Length: <= 4096 } text => text,
        _ => throw new InvalidDataException("Matter returned a process value with a type or range mismatch.")
    };

    private void RecordFailure(Exception exception)
    {
        Interlocked.Increment(ref _failures);
        _lastError = ManagedSidecarLogSanitizer.Sanitize(exception.Message, maximumLength: 256) ?? "Matter operation failed.";
    }

    private void SetStatus(DriverState state, string? message)
    {
        _stateChangedAt = DateTimeOffset.UtcNow;
        Status = new DriverStatus(DriverId, Name, state, _stateChangedAt, message, Interlocked.Read(ref _updates));
    }

    private CommunicationTagQualitySummary BuildQualitySummary()
    {
        var counts = new int[9];
        foreach (var point in _plan.Points)
        {
            if (!_services.Cache.TryGet(point.Tag.Id, out var current) || current is null) { counts[8]++; continue; }
            counts[current.Quality switch
            {
                TagQuality.Good => 0, TagQuality.BadCommunication => 1, TagQuality.Uncertain => 2,
                TagQuality.Bad => 3, TagQuality.BadConfiguration => 4, TagQuality.BadDevice => 5,
                TagQuality.Stale => 6, TagQuality.Disabled => 7, _ => 1
            }]++;
        }
        return new(counts[0], counts[1], counts[2], counts[3], counts[4], counts[5], counts[6], counts[7], counts[8]);
    }
}
