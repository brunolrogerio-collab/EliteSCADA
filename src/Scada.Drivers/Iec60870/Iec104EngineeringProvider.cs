using System.Globalization;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Cohesive Engineering surface for IEC-104. Browse is deliberately evidence-based:
/// IEC-104 does not standardize a complete point namespace browse, so a bounded GI/observation
/// window returns only points actually seen and never mutates canonical Engineering state.
/// </summary>
public sealed class Iec104EngineeringProvider : ICommunicationDriverConnectionTester, ICommunicationDriverBrowser
{
    private const int DefaultMaximumCandidates = 10_000;
    private const int MaximumCandidateLimit = 1_000_000;
    private const int MaximumIssueMessageLength = 512;
    private static readonly TimeSpan DefaultObservationWindow = TimeSpan.FromSeconds(5);

    private readonly Func<IIec104ClientAdapter> _adapterFactory;
    private readonly Iec104EngineeringConnectionTester _connectionTester;

    public Iec104EngineeringProvider(Func<IIec104ClientAdapter>? adapterFactory = null)
    {
        _adapterFactory = adapterFactory ?? static () => new Iec104TcpClientAdapter();
        _connectionTester = new Iec104EngineeringConnectionTester(_adapterFactory);
        Descriptor = _connectionTester.Descriptor with
        {
            EngineeringCapabilities = _connectionTester.Descriptor.EngineeringCapabilities | DriverEngineeringCapabilities.Browse,
            Description = "IEC 60870-5-104 client/master with connection test and bounded GI-based Engineering browse. Browse results are partial evidence; rich TAG binding remains a coordinated Engineering contract."
        };
    }

    public CommunicationDriverTypeDescriptor Descriptor { get; }

    public ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default) =>
        _connectionTester.TestConnectionAsync(context, cancellationToken);

    public async ValueTask<DriverBrowsePage> BrowseAsync(
        DriverBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Context);
        cancellationToken.ThrowIfCancellationRequested();

        var issues = new List<DriverEngineeringIssue>();
        if (!string.Equals(request.Context.DriverType, Iec104EngineeringConnectionTester.DriverType, StringComparison.Ordinal))
        {
            issues.Add(Error(
                "iec104.browse.driverType",
                $"Engineering context driver type must be '{Iec104EngineeringConnectionTester.DriverType}'.",
                "driverType"));
        }

        if (!string.IsNullOrWhiteSpace(request.ContinuationToken))
        {
            issues.Add(Error(
                "iec104.browse.continuation.unsupported",
                "IEC-104 observation browse does not support continuation tokens because each browse is a fresh bounded protocol observation."));
        }

        if (!string.IsNullOrWhiteSpace(request.ParentNodeId))
        {
            issues.Add(Warning(
                "iec104.browse.flat",
                "IEC-104 observation browse is flat; ParentNodeId is ignored and the bounded observation result is returned from the Data Source root."));
        }

        var settings = ParseSettings(request.Context.Settings, issues);
        var observationWindow = ParseObservationWindow(request.Parameters, issues);
        var maximumCandidates = ParseMaximumCandidates(request, issues);

        if (issues.Any(static issue => issue.Severity == DriverEngineeringIssueSeverity.Error) || settings is null)
            return new DriverBrowsePage(Array.Empty<DriverBrowseNode>(), IsPartial: true, Issues: issues);

        var collector = new Iec104ObservationCollector(
            _adapterFactory,
            settings.Host,
            settings.Port,
            settings.SessionOptions,
            settings.StationTimeZone,
            settings.CommonAddresses,
            settings.OriginatorAddress);

        Iec104ObservationResult observation;
        try
        {
            observation = await collector.ObserveAsync(
                observationWindow,
                maximumCandidates,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            issues.Add(Error("iec104.browse.failed", Sanitize(ex.Message)));
            return new DriverBrowsePage(Array.Empty<DriverBrowseNode>(), IsPartial: true, Issues: issues);
        }

        issues.Add(Information(
            "iec104.browse.partial",
            "IEC-104 browse is observation-based and therefore partial. Only points observed during General Interrogation/event traffic are returned."));

        if (!observation.AllRequestedGeneralInterrogationsCompleted)
        {
            issues.Add(Warning(
                "iec104.browse.gi.incomplete",
                "One or more requested IEC-104 General Interrogations did not reach positive activation termination during the observation window."));
        }

        if (observation.CandidateLimitReached)
        {
            issues.Add(Warning(
                "iec104.browse.limit",
                $"IEC-104 observation stopped after reaching the configured candidate limit of {maximumCandidates}."));
        }

        foreach (var pair in observation.GeneralInterrogationStates.OrderBy(static pair => pair.Key))
        {
            if (pair.Value == Iec104GeneralInterrogationState.Rejected)
            {
                issues.Add(Warning(
                    "iec104.browse.gi.rejected",
                    $"IEC-104 General Interrogation for Common Address {pair.Key} was rejected by the remote station."));
            }
        }

        var nodes = observation.Candidates
            .OrderBy(static candidate => candidate.CommonAddress)
            .ThenBy(static candidate => candidate.InformationObjectAddress)
            .Select(CreateBrowseNode)
            .ToArray();

        return new DriverBrowsePage(nodes, ContinuationToken: null, IsPartial: true, Issues: issues);
    }

    private static DriverBrowseNode CreateBrowseNode(Iec104ObservedPointCandidate candidate)
    {
        var portableAddress = FormatPortableAddress(candidate.CommonAddress, candidate.InformationObjectAddress);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["commonAddress"] = candidate.CommonAddress.ToString(CultureInfo.InvariantCulture),
            ["informationObjectAddress"] = candidate.InformationObjectAddress.ToString(CultureInfo.InvariantCulture),
            ["observedTypeIds"] = string.Join(",", candidate.ObservedTypeIds.OrderBy(static typeId => (byte)typeId).Select(static typeId => ((byte)typeId).ToString(CultureInfo.InvariantCulture))),
            ["observedTypeNames"] = string.Join(",", candidate.ObservedTypeIds.OrderBy(static typeId => (byte)typeId)),
            ["lastQuality"] = candidate.LastQuality.ToString(),
            ["lastCauseOfTransmission"] = candidate.LastCauseOfTransmission.ToString(CultureInfo.InvariantCulture),
            ["observationCount"] = candidate.ObservationCount.ToString(CultureInfo.InvariantCulture)
        };

        if (candidate.LastSourceTimestamp.HasValue)
            metadata["lastSourceTimestamp"] = candidate.LastSourceTimestamp.Value.ToString("O", CultureInfo.InvariantCulture);

        IReadOnlyCollection<DriverEngineeringIssue>? nodeIssues = candidate.HasTypeConflict
            ? new[]
            {
                Warning(
                    "iec104.browse.typeConflict",
                    $"IEC-104 point CA {candidate.CommonAddress} IOA {candidate.InformationObjectAddress} was observed with multiple Type IDs; data type/binding requires explicit Engineering review.")
            }
            : null;

        return new DriverBrowseNode(
            NodeId: portableAddress,
            StableIdentity: portableAddress,
            DisplayName: $"CA {candidate.CommonAddress} / IOA {candidate.InformationObjectAddress}",
            IsContainer: false,
            IsReadable: true,
            IsWritable: false,
            PortableAddress: portableAddress,
            SuggestedDataType: candidate.SuggestedDataType,
            Metadata: metadata,
            Issues: nodeIssues);
    }

    private static string FormatPortableAddress(ushort commonAddress, int informationObjectAddress) =>
        $"ca={commonAddress.ToString(CultureInfo.InvariantCulture)};ioa={informationObjectAddress.ToString(CultureInfo.InvariantCulture)}";

    private static ParsedSettings? ParseSettings(
        IReadOnlyDictionary<string, string> settings,
        ICollection<DriverEngineeringIssue> issues)
    {
        var host = GetRequired(settings, "host", issues);
        var port = GetInteger(settings, "port", 2404, 1, 65535, issues);
        var commonAddresses = ParseCommonAddresses(settings, issues);
        var timeZone = ParseTimeZone(settings, issues);
        var originatorAddress = GetInteger(settings, "originatorAddress", 0, 0, 255, issues);
        var t0 = GetSeconds(settings, "t0Seconds", 30, 0.1, 3600, issues);
        var t1 = GetSeconds(settings, "t1Seconds", 15, 0.1, 3600, issues);
        var t2 = GetSeconds(settings, "t2Seconds", 10, 0.1, 3600, issues);
        var t3 = GetSeconds(settings, "t3Seconds", 20, 0.1, 86400, issues);
        var k = GetInteger(settings, "k", 12, 1, 32767, issues);
        var w = GetInteger(settings, "w", 8, 1, 32767, issues);

        var options = new Iec104SessionOptions
        {
            T0 = TimeSpan.FromSeconds(t0),
            T1 = TimeSpan.FromSeconds(t1),
            T2 = TimeSpan.FromSeconds(t2),
            T3 = TimeSpan.FromSeconds(t3),
            K = k,
            W = w
        };

        try
        {
            options.Validate();
        }
        catch (ArgumentException ex)
        {
            issues.Add(Error("iec104.browse.apci", Sanitize(ex.Message)));
        }

        if (host is null || timeZone is null || commonAddresses.Length == 0 || issues.Any(static issue => issue.Severity == DriverEngineeringIssueSeverity.Error))
            return null;

        return new ParsedSettings(
            host,
            port,
            commonAddresses,
            timeZone,
            checked((byte)originatorAddress),
            options);
    }

    private static TimeSpan ParseObservationWindow(
        IReadOnlyDictionary<string, string>? parameters,
        ICollection<DriverEngineeringIssue> issues)
    {
        if (parameters is null || !parameters.TryGetValue("observationWindowSeconds", out var raw) || string.IsNullOrWhiteSpace(raw))
            return DefaultObservationWindow;

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0.1 && seconds <= 600)
            return TimeSpan.FromSeconds(seconds);

        issues.Add(Error(
            "iec104.browse.observationWindow",
            "IEC-104 browse parameter 'observationWindowSeconds' must be in the range 0.1..600 seconds.",
            "observationWindowSeconds"));
        return DefaultObservationWindow;
    }

    private static int ParseMaximumCandidates(
        DriverBrowseRequest request,
        ICollection<DriverEngineeringIssue> issues)
    {
        if (request.PageSize.HasValue)
        {
            if (request.PageSize.Value is >= 1 and <= MaximumCandidateLimit)
                return request.PageSize.Value;

            issues.Add(Error(
                "iec104.browse.pageSize",
                $"IEC-104 browse PageSize must be in the range 1..{MaximumCandidateLimit}."));
            return DefaultMaximumCandidates;
        }

        if (request.Parameters is null || !request.Parameters.TryGetValue("maximumCandidates", out var raw) || string.IsNullOrWhiteSpace(raw))
            return DefaultMaximumCandidates;

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= MaximumCandidateLimit)
            return value;

        issues.Add(Error(
            "iec104.browse.maximumCandidates",
            $"IEC-104 browse parameter 'maximumCandidates' must be an integer in the range 1..{MaximumCandidateLimit}.",
            "maximumCandidates"));
        return DefaultMaximumCandidates;
    }

    private static string? GetRequired(
        IReadOnlyDictionary<string, string> settings,
        string key,
        ICollection<DriverEngineeringIssue> issues)
    {
        if (settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value.Trim();

        issues.Add(Error("iec104.browse.required", $"IEC-104 setting '{key}' is required.", key));
        return null;
    }

    private static int GetInteger(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int defaultValue,
        int minimum,
        int maximum,
        ICollection<DriverEngineeringIssue> issues)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= minimum && value <= maximum)
            return value;

        issues.Add(Error("iec104.browse.integer", $"IEC-104 setting '{key}' must be an integer in the range {minimum}..{maximum}.", key));
        return defaultValue;
    }

    private static double GetSeconds(
        IReadOnlyDictionary<string, string> settings,
        string key,
        double defaultValue,
        double minimum,
        double maximum,
        ICollection<DriverEngineeringIssue> issues)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= minimum && value <= maximum)
            return value;

        issues.Add(Error("iec104.browse.duration", $"IEC-104 setting '{key}' must be seconds in the range {minimum}..{maximum}.", key));
        return defaultValue;
    }

    private static ushort[] ParseCommonAddresses(
        IReadOnlyDictionary<string, string> settings,
        ICollection<DriverEngineeringIssue> issues)
    {
        var raw = GetRequired(settings, "commonAddresses", issues);
        if (raw is null)
            return Array.Empty<ushort>();

        var addresses = new List<ushort>();
        foreach (var item in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ushort.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var address))
            {
                issues.Add(Error("iec104.browse.commonAddress", $"IEC-104 Common Address '{item}' is not a valid 16-bit unsigned integer.", "commonAddresses"));
                continue;
            }

            addresses.Add(address);
        }

        if (addresses.Count == 0)
            issues.Add(Error("iec104.browse.commonAddress.empty", "At least one IEC-104 Common Address is required.", "commonAddresses"));

        return addresses.Distinct().OrderBy(static value => value).ToArray();
    }

    private static TimeZoneInfo? ParseTimeZone(
        IReadOnlyDictionary<string, string> settings,
        ICollection<DriverEngineeringIssue> issues)
    {
        var id = GetRequired(settings, "stationTimeZone", issues);
        if (id is null)
            return null;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            issues.Add(Error("iec104.browse.timeZone", $"IEC-104 station time zone '{id}' is not available on this runtime.", "stationTimeZone"));
        }
        catch (InvalidTimeZoneException)
        {
            issues.Add(Error("iec104.browse.timeZone.invalid", $"IEC-104 station time zone '{id}' is invalid on this runtime.", "stationTimeZone"));
        }

        return null;
    }

    private static DriverEngineeringIssue Information(string code, string message) =>
        new(code, DriverEngineeringIssueSeverity.Information, Sanitize(message));

    private static DriverEngineeringIssue Warning(string code, string message) =>
        new(code, DriverEngineeringIssueSeverity.Warning, Sanitize(message));

    private static DriverEngineeringIssue Error(string code, string message, string? fieldKey = null) =>
        new(code, DriverEngineeringIssueSeverity.Error, Sanitize(message), fieldKey);

    private static string Sanitize(string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= MaximumIssueMessageLength
            ? sanitized
            : sanitized[..MaximumIssueMessageLength];
    }

    private sealed record ParsedSettings(
        string Host,
        int Port,
        ushort[] CommonAddresses,
        TimeZoneInfo StationTimeZone,
        byte OriginatorAddress,
        Iec104SessionOptions SessionOptions);
}
