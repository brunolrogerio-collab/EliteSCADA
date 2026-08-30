using Scada.Core.Tags;

namespace Scada.Drivers.Iec60870;

public sealed record Iec104ObservedPointCandidate(
    ushort CommonAddress,
    int InformationObjectAddress,
    IReadOnlyCollection<Iec104TypeId> ObservedTypeIds,
    TagDataType? SuggestedDataType,
    object LastValue,
    TagQuality LastQuality,
    DateTimeOffset? LastSourceTimestamp,
    byte LastCauseOfTransmission,
    int ObservationCount,
    bool HasTypeConflict);

public sealed record Iec104ObservationResult(
    IReadOnlyCollection<Iec104ObservedPointCandidate> Candidates,
    IReadOnlyDictionary<ushort, Iec104GeneralInterrogationState> GeneralInterrogationStates,
    bool AllRequestedGeneralInterrogationsCompleted,
    bool CandidateLimitReached,
    bool IsPartial,
    DateTimeOffset CapturedAt);

/// <summary>
/// Bounded Engineering evidence collector. IEC-104 has no standardized full address-space browse, so
/// this component performs STARTDT + General Interrogation and records only points actually observed.
/// Results are always partial evidence and are never persisted or applied to TAG Engineering directly.
/// </summary>
public sealed class Iec104ObservationCollector
{
    private readonly Func<IIec104ClientAdapter> _adapterFactory;
    private readonly string _host;
    private readonly int _port;
    private readonly Iec104SessionOptions _options;
    private readonly TimeZoneInfo _stationTimeZone;
    private readonly ushort[] _commonAddresses;
    private readonly byte _originatorAddress;

    public Iec104ObservationCollector(
        Func<IIec104ClientAdapter> adapterFactory,
        string host,
        int port,
        Iec104SessionOptions options,
        TimeZoneInfo stationTimeZone,
        IEnumerable<ushort> commonAddresses,
        byte originatorAddress = 0)
    {
        ArgumentNullException.ThrowIfNull(adapterFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stationTimeZone);
        ArgumentNullException.ThrowIfNull(commonAddresses);
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("IEC-104 host is required.", nameof(host));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));

        options.Validate();
        var addresses = commonAddresses.Distinct().OrderBy(static value => value).ToArray();
        if (addresses.Length == 0)
            throw new ArgumentException("IEC-104 observation requires at least one Common Address.", nameof(commonAddresses));

        _adapterFactory = adapterFactory;
        _host = host.Trim();
        _port = port;
        _options = options;
        _stationTimeZone = stationTimeZone;
        _commonAddresses = addresses;
        _originatorAddress = originatorAddress;
    }

    public async Task<Iec104ObservationResult> ObserveAsync(
        TimeSpan observationWindow,
        int maximumCandidates = 10_000,
        CancellationToken cancellationToken = default)
    {
        if (observationWindow <= TimeSpan.Zero || observationWindow > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(observationWindow), observationWindow, "IEC-104 observation window must be greater than zero and no more than 10 minutes.");
        if (maximumCandidates is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(maximumCandidates), maximumCandidates, "IEC-104 observation candidate limit must be in the range 1..1000000.");

        await using var adapter = _adapterFactory()
            ?? throw new InvalidOperationException("IEC-104 observation adapter factory returned null.");

        var gi = _commonAddresses.ToDictionary(
            static address => address,
            address => new Iec104GeneralInterrogationTransaction(address, _originatorAddress));
        var candidates = new Dictionary<Iec104ObservedPointKey, MutableCandidate>();
        var candidateLimitReached = false;
        var connected = false;
        var started = false;

        using var observationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        observationCts.CancelAfter(observationWindow);

        try
        {
            await adapter.ConnectAsync(_host, _port, _options, cancellationToken).ConfigureAwait(false);
            connected = true;
            await adapter.StartDataTransferAsync(cancellationToken).ConfigureAwait(false);
            started = true;

            foreach (var transaction in gi.Values)
                await adapter.SendAsync(transaction.CreateActivation(), cancellationToken).ConfigureAwait(false);

            try
            {
                await foreach (var asdu in adapter.ReadAsync(observationCts.Token).WithCancellation(observationCts.Token).ConfigureAwait(false))
                {
                    if (TryObserveGeneralInterrogation(asdu, gi))
                    {
                        if (AllGeneralInterrogationsTerminal(gi))
                            break;
                        continue;
                    }

                    if (asdu.Header.CauseOfTransmission.IsTest)
                        continue;
                    if (!Iec104InformationObjectDecoder.IsSupported(asdu.Header.TypeId))
                        continue;

                    var points = Iec104InformationObjectDecoder.Decode(asdu, _stationTimeZone);
                    foreach (var point in points)
                    {
                        var key = new Iec104ObservedPointKey(point.CommonAddress, point.InformationObjectAddress.Value);
                        if (!candidates.TryGetValue(key, out var candidate))
                        {
                            if (candidates.Count >= maximumCandidates)
                            {
                                candidateLimitReached = true;
                                break;
                            }

                            candidate = new MutableCandidate(point.CommonAddress, point.InformationObjectAddress.Value);
                            candidates.Add(key, candidate);
                        }

                        candidate.Observe(point, asdu.Header.CauseOfTransmission.CauseCode);
                    }

                    if (candidateLimitReached)
                        break;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && observationCts.IsCancellationRequested)
            {
                // Bounded observation window elapsed. Partial evidence is the intended result.
            }
        }
        finally
        {
            using var cleanupCts = new CancellationTokenSource(_options.T0);
            if (started && adapter.IsConnected)
            {
                try
                {
                    await adapter.StopDataTransferAsync(cleanupCts.Token).ConfigureAwait(false);
                }
                catch
                {
                    // Observation evidence remains useful even when STOPDT cleanup fails.
                }
            }

            if (connected && adapter.IsConnected)
            {
                try
                {
                    await adapter.DisconnectAsync(cleanupCts.Token).ConfigureAwait(false);
                }
                catch
                {
                    // Adapter disposal is the final cleanup boundary.
                }
            }
        }

        var giStates = gi.ToDictionary(static pair => pair.Key, static pair => pair.Value.State);
        var resultCandidates = candidates.Values
            .OrderBy(static candidate => candidate.CommonAddress)
            .ThenBy(static candidate => candidate.InformationObjectAddress)
            .Select(static candidate => candidate.ToResult())
            .ToArray();

        return new Iec104ObservationResult(
            resultCandidates,
            giStates,
            giStates.Count > 0 && giStates.Values.All(static state => state == Iec104GeneralInterrogationState.Completed),
            candidateLimitReached,
            IsPartial: true,
            DateTimeOffset.UtcNow);
    }

    private static bool TryObserveGeneralInterrogation(
        Iec104AsduEnvelope asdu,
        IReadOnlyDictionary<ushort, Iec104GeneralInterrogationTransaction> transactions)
    {
        if (asdu.Header.TypeId != Iec104TypeId.CIcNa1)
            return false;
        if (!transactions.TryGetValue(asdu.Header.CommonAddress, out var transaction))
            return false;
        return transaction.ObserveControlResponse(asdu);
    }

    private static bool AllGeneralInterrogationsTerminal(
        IReadOnlyDictionary<ushort, Iec104GeneralInterrogationTransaction> transactions) =>
        transactions.Values.All(static transaction => transaction.State is
            Iec104GeneralInterrogationState.Completed or Iec104GeneralInterrogationState.Rejected);

    private static TagDataType? MapSuggestedDataType(IEnumerable<Iec104TypeId> typeIds)
    {
        TagDataType? result = null;
        foreach (var typeId in typeIds)
        {
            var mapped = typeId switch
            {
                Iec104TypeId.MSpNa1 or Iec104TypeId.MSpTb1 => TagDataType.Boolean,
                Iec104TypeId.MDpNa1 or Iec104TypeId.MDpTb1 => TagDataType.Enum,
                Iec104TypeId.MBoNa1 or Iec104TypeId.MBoTb1 => TagDataType.Int32,
                Iec104TypeId.MMeNa1 or Iec104TypeId.MMeTd1 => TagDataType.Float,
                Iec104TypeId.MMeNb1 or Iec104TypeId.MMeTe1 => TagDataType.Int16,
                Iec104TypeId.MMeNc1 or Iec104TypeId.MMeTf1 => TagDataType.Float,
                _ => (TagDataType?)null
            };

            if (mapped is null)
                return null;
            if (result is null)
                result = mapped;
            else if (result != mapped)
                return null;
        }

        return result;
    }

    private readonly record struct Iec104ObservedPointKey(ushort CommonAddress, int InformationObjectAddress);

    private sealed class MutableCandidate
    {
        private readonly HashSet<Iec104TypeId> _typeIds = new();

        public MutableCandidate(ushort commonAddress, int informationObjectAddress)
        {
            CommonAddress = commonAddress;
            InformationObjectAddress = informationObjectAddress;
        }

        public ushort CommonAddress { get; }
        public int InformationObjectAddress { get; }
        public object? LastValue { get; private set; }
        public TagQuality LastQuality { get; private set; }
        public DateTimeOffset? LastSourceTimestamp { get; private set; }
        public byte LastCauseOfTransmission { get; private set; }
        public int ObservationCount { get; private set; }

        public void Observe(Iec104DecodedPoint point, byte causeOfTransmission)
        {
            _typeIds.Add(point.TypeId);
            LastValue = point.Value;
            LastQuality = point.Quality;
            LastSourceTimestamp = point.SourceTimestamp;
            LastCauseOfTransmission = causeOfTransmission;
            ObservationCount++;
        }

        public Iec104ObservedPointCandidate ToResult()
        {
            var types = _typeIds.OrderBy(static typeId => (byte)typeId).ToArray();
            return new Iec104ObservedPointCandidate(
                CommonAddress,
                InformationObjectAddress,
                types,
                MapSuggestedDataType(types),
                LastValue ?? throw new InvalidOperationException("IEC-104 observation candidate has no sample value."),
                LastQuality,
                LastSourceTimestamp,
                LastCauseOfTransmission,
                ObservationCount,
                HasTypeConflict: types.Length > 1);
        }
    }
}
