using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Dnp3;

public sealed class Dnp3Driver : ICommunicationDriver, ICommunicationDiagnosticsSource
{
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyList<Dnp3Point> _points;
    private readonly IReadOnlyDictionary<Guid, Dnp3Point> _pointsByTagId;
    private readonly IReadOnlyDictionary<(Dnp3PointKind Kind, ushort Index), Dnp3Point> _pointsByPhysicalIdentity;
    private readonly IDnp3MasterSession _session;
    private readonly Dnp3AssociationOptions _associationOptions;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private CancellationTokenSource? _cts;
    private long _updatesPublished;
    private long _rejectedMeasurements;
    private long _lastMeasurementUtcTicks;
    private int _pendingUserRequests;

    public Dnp3Driver(
        string driverId,
        string name,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<Dnp3Point> points,
        IDnp3MasterSession session,
        Dnp3AssociationOptions? associationOptions = null)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(session);

        DriverId = driverId.Trim();
        Name = name.Trim();
        _cache = cache;
        _registry = registry;
        _session = session;
        _associationOptions = associationOptions ?? new Dnp3AssociationOptions();
        _associationOptions.Validate();

        _points = points.ToArray();
        if (_points.Count == 0)
            throw new ArgumentException("At least one DNP3 point is required.", nameof(points));

        foreach (var point in _points) point.Validate();

        if (_points.Select(point => point.Tag.Id).Distinct().Count() != _points.Count)
            throw new ArgumentException("Each DNP3 point must reference a unique TAG ID.", nameof(points));

        if (_points.Select(point => (point.Binding.PointKind, point.Binding.Index)).Distinct().Count() != _points.Count)
            throw new ArgumentException("Each configured DNP3 physical point identity must be unique within one Data Source.", nameof(points));

        _pointsByTagId = _points.ToDictionary(point => point.Tag.Id);
        _pointsByPhysicalIdentity = _points.ToDictionary(point => (point.Binding.PointKind, point.Binding.Index));
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId { get; }
    public string Name { get; }

    public DriverCapabilities Capabilities =>
        DriverCapabilities.Read |
        DriverCapabilities.Subscribe |
        DriverCapabilities.Diagnostics |
        DriverCapabilities.SourceTimestamp |
        (_points.Any(point => point.Binding.Writable) ? DriverCapabilities.Write : DriverCapabilities.None);

    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(point => point.Tag).ToArray();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_cts is { IsCancellationRequested: false }) return;
            cancellationToken.ThrowIfCancellationRequested();

            Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
            foreach (var point in _points)
            {
                if (_registry.TryGet(point.Tag.Id, out var existing) && existing is not null)
                {
                    if (!existing.Path.Equals(point.Tag.Path, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"DNP3 TAG '{point.Tag.Id}' is already registered with path '{existing.Path}', expected '{point.Tag.Path}'.");
                    continue;
                }

                _registry.Register(point.Tag);
            }

            _cts = new CancellationTokenSource();
            try
            {
                await _session.StartAsync(
                    _associationOptions,
                    HandleMeasurementAsync,
                    HandleSessionStateAsync,
                    _cts.Token);
                Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
            }
            catch (Exception ex)
            {
                Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, SanitizeError(ex), Interlocked.Read(ref _updatesPublished));
                try
                {
                    await _session.StopAsync(CancellationToken.None);
                }
                catch
                {
                    // Best-effort cleanup only. The original start failure remains authoritative.
                }
                _cts.Dispose();
                _cts = null;
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            var cts = _cts;
            if (cts is null) return;

            Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
            await cts.CancelAsync();
            try
            {
                await _session.StopAsync(cancellationToken);
                Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
            }
            catch (Exception ex)
            {
                Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, SanitizeError(ex), Interlocked.Read(ref _updatesPublished));
                throw;
            }
            finally
            {
                cts.Dispose();
                _cts = null;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsByTagId.ContainsKey(tagId))
            throw new KeyNotFoundException($"DNP3 TAG '{tagId}' was not found in driver '{DriverId}'.");

        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        if (!_pointsByTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"DNP3 TAG '{tagId}' was not found in driver '{DriverId}'.");
        if (!point.Binding.Writable || point.Tag.ReadOnly)
            throw new InvalidOperationException($"DNP3 TAG '{point.Tag.Path}' is not writable.");
        if (Status.State != DriverState.Running || _session.State != Dnp3SessionState.Online)
            throw new InvalidOperationException("DNP3 association is not online; command was not queued or retained for replay.");

        var result = point.Binding.PointKind switch
        {
            Dnp3PointKind.BinaryOutputStatus => await ExecuteBinaryWriteAsync(point, value, cancellationToken),
            Dnp3PointKind.AnalogOutputStatus => await ExecuteAnalogWriteAsync(point, value, cancellationToken),
            _ => throw new InvalidOperationException($"DNP3 point kind '{point.Binding.PointKind}' does not support writes.")
        };

        if (!result.Succeeded)
            throw new Dnp3CommandException(result.Status, SanitizeText(result.Message));
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var session = _session.GetDiagnostics();
        var capturedAt = DateTimeOffset.UtcNow;
        var protocolDetails = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["acquisitionMode"] = "Hybrid",
            ["sessionState"] = session.State.ToString(),
            ["startupIntegrityScans"] = session.StartupIntegrityScans.ToString(CultureInfo.InvariantCulture),
            ["class0Scans"] = session.Class0Scans.ToString(CultureInfo.InvariantCulture),
            ["class1Scans"] = session.Class1Scans.ToString(CultureInfo.InvariantCulture),
            ["class2Scans"] = session.Class2Scans.ToString(CultureInfo.InvariantCulture),
            ["class3Scans"] = session.Class3Scans.ToString(CultureInfo.InvariantCulture),
            ["unsolicitedResponses"] = session.UnsolicitedResponses.ToString(CultureInfo.InvariantCulture),
            ["restartDetections"] = session.RestartDetections.ToString(CultureInfo.InvariantCulture),
            ["eventBufferOverflowDetections"] = session.EventBufferOverflowDetections.ToString(CultureInfo.InvariantCulture),
            ["rejectedMeasurements"] = Interlocked.Read(ref _rejectedMeasurements).ToString(CultureInfo.InvariantCulture),
            ["pendingUserRequests"] = Volatile.Read(ref _pendingUserRequests).ToString(CultureInfo.InvariantCulture),
            ["maxQueuedUserRequests"] = _associationOptions.MaxQueuedUserRequests.ToString(CultureInfo.InvariantCulture)
        };

        return new CommunicationDriverDiagnosticSnapshot(
            DriverId,
            Name,
            "dnp3.master",
            _runtimeInstanceId,
            session.Endpoint,
            MapCommunicationState(session.State),
            session.StateChangedAt,
            capturedAt,
            session.LastSuccessfulCommunicationAt,
            session.LastFailedCommunicationAt,
            SanitizeText(session.LastError),
            GetDataAge(capturedAt),
            ConfiguredScanInterval: null,
            LastOperationDuration: null,
            AverageOperationDuration: null,
            LastScanDuration: null,
            session.RecentFailureRate,
            _points.Count,
            BuildQualitySummary(),
            new CommunicationDriverCounters(
                Cycles: session.StartupIntegrityScans + session.Class0Scans + session.Class1Scans + session.Class2Scans + session.Class3Scans,
                Requests: session.Requests,
                SuccessfulOperations: session.SuccessfulOperations,
                FailedOperations: session.FailedOperations,
                ConsecutiveFailures: session.ConsecutiveFailures,
                Timeouts: session.Timeouts,
                Connections: session.Connections,
                Disconnections: session.Disconnections,
                Reconnects: session.Reconnects,
                ReadOperations: session.ReadOperations,
                WriteOperations: session.WriteOperations,
                UpdatesPublished: Interlocked.Read(ref _updatesPublished)),
            protocolDetails);
    }

    private async ValueTask<Dnp3CommandResult> ExecuteBinaryWriteAsync(
        Dnp3Point point,
        object? value,
        CancellationToken cancellationToken)
    {
        if (value is not bool boolean)
            throw new ArgumentException("DNP3 binary output write requires a Boolean value.", nameof(value));

        var profile = point.BinaryCommandProfile ?? throw new InvalidOperationException("DNP3 binary command profile is missing.");
        profile.Validate();
        var operation = profile.ResolveOperation(boolean);
        return await ExecuteBoundedUserRequestAsync(
            token => _session.ExecuteBinaryAsync(point.Binding.Index, operation, profile, token),
            cancellationToken);
    }

    private async ValueTask<Dnp3CommandResult> ExecuteAnalogWriteAsync(
        Dnp3Point point,
        object? value,
        CancellationToken cancellationToken)
    {
        var profile = point.AnalogCommandProfile ?? throw new InvalidOperationException("DNP3 analog command profile is missing.");
        profile.Validate(point.Tag.DataType);
        var normalized = Dnp3RuntimeValueMapper.NormalizeAnalogCommand(point, value);
        return await ExecuteBoundedUserRequestAsync(
            token => _session.ExecuteAnalogAsync(point.Binding.Index, normalized, profile, token),
            cancellationToken);
    }

    private async ValueTask<Dnp3CommandResult> ExecuteBoundedUserRequestAsync(
        Func<CancellationToken, ValueTask<Dnp3CommandResult>> operation,
        CancellationToken cancellationToken)
    {
        var pending = Interlocked.Increment(ref _pendingUserRequests);
        if (pending > _associationOptions.MaxQueuedUserRequests)
        {
            Interlocked.Decrement(ref _pendingUserRequests);
            throw new InvalidOperationException("DNP3 user request queue is full; command was not queued.");
        }

        try
        {
            await _commandGate.WaitAsync(cancellationToken);
            try
            {
                if (_session.State != Dnp3SessionState.Online)
                    throw new InvalidOperationException("DNP3 association left the Online state before command execution; command was not replayed.");
                return await operation(cancellationToken);
            }
            finally
            {
                _commandGate.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _pendingUserRequests);
        }
    }

    private async ValueTask HandleMeasurementAsync(Dnp3Measurement measurement, CancellationToken cancellationToken)
    {
        if (!_pointsByPhysicalIdentity.TryGetValue((measurement.PointKind, measurement.Index), out var point))
        {
            Interlocked.Increment(ref _rejectedMeasurements);
            return;
        }

        var variationValid = measurement.IsEvent
            ? Dnp3VariationRules.IsEventVariation(point.Binding.PointKind, measurement.Variation)
            : Dnp3VariationRules.IsStaticVariation(point.Binding.PointKind, measurement.Variation);

        var inferredType = Dnp3VariationRules.TryGetCanonicalDataType(point.Binding.PointKind, measurement.Variation);
        if (!variationValid || (inferredType is not null && inferredType != point.Tag.DataType))
        {
            Interlocked.Increment(ref _rejectedMeasurements);
            await PublishBadConfigurationAsync(point, measurement.SourceTimestamp, cancellationToken);
            return;
        }

        object normalized;
        try
        {
            normalized = Dnp3RuntimeValueMapper.Normalize(point, measurement.Value);
        }
        catch (ArgumentException)
        {
            Interlocked.Increment(ref _rejectedMeasurements);
            await PublishBadConfigurationAsync(point, measurement.SourceTimestamp, cancellationToken);
            return;
        }

        var observedAt = DateTimeOffset.UtcNow;
        var sample = Dnp3MeasurementMapper.CreateTagValue(
            point.Tag.Id,
            normalized,
            observedAt,
            measurement.Flags,
            measurement.SourceTimestamp,
            measurement.SourceTimestampSynchronized,
            DriverId);

        await _cache.UpdateAsync(point.Tag, sample, cancellationToken);
        Interlocked.Exchange(ref _lastMeasurementUtcTicks, observedAt.UtcDateTime.Ticks);
        var updates = Interlocked.Increment(ref _updatesPublished);
        if (_session.State == Dnp3SessionState.Online)
            Status = new DriverStatus(DriverId, Name, DriverState.Running, observedAt, UpdatesPublished: updates);
    }

    private async ValueTask HandleSessionStateAsync(Dnp3SessionState state, CancellationToken cancellationToken)
    {
        switch (state)
        {
            case Dnp3SessionState.Reconnecting:
                foreach (var point in _points)
                    await PublishCommunicationFailureAsync(point, cancellationToken);
                Status = new DriverStatus(
                    DriverId,
                    Name,
                    DriverState.Running,
                    DateTimeOffset.UtcNow,
                    "DNP3 association is reconnecting.",
                    Interlocked.Read(ref _updatesPublished));
                break;

            case Dnp3SessionState.Faulted:
                foreach (var point in _points)
                    await PublishCommunicationFailureAsync(point, cancellationToken);
                Status = new DriverStatus(
                    DriverId,
                    Name,
                    DriverState.Faulted,
                    DateTimeOffset.UtcNow,
                    "DNP3 association faulted.",
                    Interlocked.Read(ref _updatesPublished));
                break;

            case Dnp3SessionState.Degraded:
                Status = new DriverStatus(
                    DriverId,
                    Name,
                    DriverState.Running,
                    DateTimeOffset.UtcNow,
                    "DNP3 association is degraded.",
                    Interlocked.Read(ref _updatesPublished));
                break;

            case Dnp3SessionState.Online:
                Status = new DriverStatus(
                    DriverId,
                    Name,
                    DriverState.Running,
                    DateTimeOffset.UtcNow,
                    UpdatesPublished: Interlocked.Read(ref _updatesPublished));
                break;
        }
    }

    private async ValueTask PublishCommunicationFailureAsync(Dnp3Point point, CancellationToken cancellationToken)
    {
        _cache.TryGet(point.Tag.Id, out var previous);
        if (previous?.Quality == TagQuality.BadCommunication)
            return;

        var sample = new TagValue(
            point.Tag.Id,
            previous?.Value,
            DateTimeOffset.UtcNow,
            TagQuality.BadCommunication,
            DriverId)
        {
            SourceTimestamp = previous?.SourceTimestamp
        };

        await _cache.UpdateAsync(point.Tag, sample, cancellationToken);
        Interlocked.Increment(ref _updatesPublished);
    }

    private async ValueTask PublishBadConfigurationAsync(
        Dnp3Point point,
        DateTimeOffset? sourceTimestamp,
        CancellationToken cancellationToken)
    {
        _cache.TryGet(point.Tag.Id, out var previous);
        var sample = new TagValue(
            point.Tag.Id,
            previous?.Value,
            DateTimeOffset.UtcNow,
            TagQuality.BadConfiguration,
            DriverId)
        {
            SourceTimestamp = sourceTimestamp ?? previous?.SourceTimestamp
        };

        await _cache.UpdateAsync(point.Tag, sample, cancellationToken);
        Interlocked.Increment(ref _updatesPublished);
    }

    private TimeSpan? GetDataAge(DateTimeOffset capturedAt)
    {
        var ticks = Interlocked.Read(ref _lastMeasurementUtcTicks);
        if (ticks == 0) return null;
        var lastMeasurement = new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
        return capturedAt - lastMeasurement;
    }

    private CommunicationTagQualitySummary BuildQualitySummary()
    {
        var good = 0;
        var badCommunication = 0;
        var uncertain = 0;
        var bad = 0;
        var badConfiguration = 0;
        var badDevice = 0;
        var stale = 0;
        var disabled = 0;
        var noSample = 0;

        foreach (var point in _points)
        {
            if (!_cache.TryGet(point.Tag.Id, out var sample) || sample is null)
            {
                noSample++;
                continue;
            }

            switch (sample.Quality)
            {
                case TagQuality.Good: good++; break;
                case TagQuality.BadCommunication: badCommunication++; break;
                case TagQuality.Uncertain: uncertain++; break;
                case TagQuality.Bad: bad++; break;
                case TagQuality.BadConfiguration: badConfiguration++; break;
                case TagQuality.BadDevice: badDevice++; break;
                case TagQuality.Stale: stale++; break;
                case TagQuality.Disabled: disabled++; break;
                default: bad++; break;
            }
        }

        return new CommunicationTagQualitySummary(
            good,
            badCommunication,
            uncertain,
            bad,
            badConfiguration,
            badDevice,
            stale,
            disabled,
            noSample);
    }

    private static CommunicationDriverOperationalState MapCommunicationState(Dnp3SessionState state) => state switch
    {
        Dnp3SessionState.Stopped => CommunicationDriverOperationalState.Stopped,
        Dnp3SessionState.Connecting or Dnp3SessionState.StartupIntegrity => CommunicationDriverOperationalState.Starting,
        Dnp3SessionState.Online => CommunicationDriverOperationalState.Healthy,
        Dnp3SessionState.Degraded => CommunicationDriverOperationalState.Degraded,
        Dnp3SessionState.Reconnecting => CommunicationDriverOperationalState.Reconnecting,
        Dnp3SessionState.Faulted => CommunicationDriverOperationalState.Faulted,
        Dnp3SessionState.Stopping => CommunicationDriverOperationalState.Stopping,
        _ => CommunicationDriverOperationalState.Faulted
    };

    private static string? SanitizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var sanitized = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }

    private static string SanitizeError(Exception error) => SanitizeText(error.Message) ?? error.GetType().Name;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        finally
        {
            await _session.DisposeAsync();
            _lifecycleGate.Dispose();
            _commandGate.Dispose();
        }
    }
}
