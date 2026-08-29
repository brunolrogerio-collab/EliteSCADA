using System.Globalization;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Iec60870;

public sealed class Iec104EngineeringConnectionTester : ICommunicationDriverConnectionTester
{
    public const string DriverType = "iec60870.5.104";
    private const int MaximumIssueMessageLength = 512;

    private readonly Func<IIec104ClientAdapter> _adapterFactory;

    public Iec104EngineeringConnectionTester(Func<IIec104ClientAdapter>? adapterFactory = null)
    {
        _adapterFactory = adapterFactory ?? static () => new Iec104TcpClientAdapter();
    }

    public CommunicationDriverTypeDescriptor Descriptor { get; } = new(
        DriverType,
        "IEC 60870-5-104",
        DriverContractVersion: 1,
        DriverCapabilities.Read |
        DriverCapabilities.Write |
        DriverCapabilities.Subscribe |
        DriverCapabilities.Diagnostics |
        DriverCapabilities.SourceTimestamp,
        DriverEngineeringCapabilities.ConnectionTest,
        new[] { DriverAcquisitionMode.EventDriven, DriverAcquisitionMode.Hybrid },
        new DriverConfigurationSchemaDescriptor(
            SchemaId: "elite.iec60870.5.104",
            SchemaVersion: 1,
            DataSourceFields: new DriverConfigurationFieldDescriptor[]
            {
                new("host", DriverConfigurationValueKind.Host, Required: true, DisplayName: "Host"),
                new("port", DriverConfigurationValueKind.Port, DefaultValue: "2404", Minimum: 1, Maximum: 65535),
                new("commonAddresses", DriverConfigurationValueKind.String, Required: true, DisplayName: "Common Addresses", Description: "Comma-separated IEC-104 Common Addresses used by the Data Source."),
                new("stationTimeZone", DriverConfigurationValueKind.String, Required: true, DisplayName: "Station time zone", Description: "TimeZoneInfo identifier used to interpret CP56Time2a values."),
                new("originatorAddress", DriverConfigurationValueKind.Integer, DefaultValue: "0", Minimum: 0, Maximum: 255, Advanced: true),
                new("t0Seconds", DriverConfigurationValueKind.Number, DefaultValue: "30", Minimum: 0.1, Maximum: 3600, Advanced: true),
                new("t1Seconds", DriverConfigurationValueKind.Number, DefaultValue: "15", Minimum: 0.1, Maximum: 3600, Advanced: true),
                new("t2Seconds", DriverConfigurationValueKind.Number, DefaultValue: "10", Minimum: 0.1, Maximum: 3600, Advanced: true),
                new("t3Seconds", DriverConfigurationValueKind.Number, DefaultValue: "20", Minimum: 0.1, Maximum: 86400, Advanced: true),
                new("k", DriverConfigurationValueKind.Integer, DefaultValue: "12", Minimum: 1, Maximum: 32767, Advanced: true),
                new("w", DriverConfigurationValueKind.Integer, DefaultValue: "8", Minimum: 1, Maximum: 32767, Advanced: true)
            },
            TagBindingFields: Array.Empty<DriverConfigurationFieldDescriptor>()),
        Description: "IEC 60870-5-104 client/master. TAG binding schema remains pending coordinated rich CA/IOA/type contract integration.");

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var issues = new List<DriverEngineeringIssue>();
        if (!string.Equals(context.DriverType, DriverType, StringComparison.Ordinal))
        {
            issues.Add(Error("iec104.context.driverType", $"Engineering context driver type must be '{DriverType}'.", "driverType"));
        }

        var host = GetRequired(context.Settings, "host", issues);
        var port = GetInteger(context.Settings, "port", 2404, 1, 65535, issues);
        _ = ParseCommonAddresses(context.Settings, issues);
        _ = ValidateTimeZone(context.Settings, issues);
        _ = GetInteger(context.Settings, "originatorAddress", 0, 0, 255, issues);
        var t0 = GetSeconds(context.Settings, "t0Seconds", 30, issues);
        var t1 = GetSeconds(context.Settings, "t1Seconds", 15, issues);
        var t2 = GetSeconds(context.Settings, "t2Seconds", 10, issues);
        var t3 = GetSeconds(context.Settings, "t3Seconds", 20, issues);
        var k = GetInteger(context.Settings, "k", 12, 1, 32767, issues);
        var w = GetInteger(context.Settings, "w", 8, 1, 32767, issues);

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
            issues.Add(Error("iec104.config.apci", Sanitize(ex.Message)));
        }

        if (issues.Any(static issue => issue.Severity == DriverEngineeringIssueSeverity.Error))
        {
            return new DriverConnectionTestResult(
                Succeeded: false,
                SanitizedEndpoint: string.IsNullOrWhiteSpace(host) ? null : FormatEndpoint(host, port),
                ObservedIdentity: null,
                Issues: issues);
        }

        await using var adapter = _adapterFactory()
            ?? throw new InvalidOperationException("IEC-104 Engineering adapter factory returned null.");

        try
        {
            await adapter.ConnectAsync(host!, port, options, cancellationToken).ConfigureAwait(false);
            await adapter.StartDataTransferAsync(cancellationToken).ConfigureAwait(false);

            var properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["tcpConnected"] = "true",
                ["startDtConfirmed"] = "true"
            };

            if (adapter is IIec104TransportDiagnosticsSource diagnosticsSource)
            {
                var transport = diagnosticsSource.GetTransportDiagnostics();
                properties["apciNextSendSequence"] = transport.NextSendSequence.ToString(CultureInfo.InvariantCulture);
                properties["apciExpectedReceiveSequence"] = transport.ExpectedReceiveSequence.ToString(CultureInfo.InvariantCulture);
                properties["startDtConfirmationsReceived"] = transport.StartDtConfirmationsReceived.ToString(CultureInfo.InvariantCulture);
            }

            await adapter.StopDataTransferAsync(cancellationToken).ConfigureAwait(false);
            properties["stopDtConfirmed"] = "true";
            await adapter.DisconnectAsync(cancellationToken).ConfigureAwait(false);

            return new DriverConnectionTestResult(
                Succeeded: true,
                SanitizedEndpoint: FormatEndpoint(host!, port),
                ObservedIdentity: $"IEC-104 {host}:{port}",
                ObservedProperties: properties,
                Issues: issues);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (adapter.IsConnected)
            {
                try
                {
                    using var cleanupCts = new CancellationTokenSource(options.T0);
                    await adapter.DisconnectAsync(cleanupCts.Token).ConfigureAwait(false);
                }
                catch
                {
                    // The primary connection-test failure remains authoritative.
                }
            }

            issues.Add(Error("iec104.connection.failed", Sanitize(ex.Message)));
            return new DriverConnectionTestResult(
                Succeeded: false,
                SanitizedEndpoint: FormatEndpoint(host!, port),
                ObservedIdentity: null,
                Issues: issues);
        }
    }

    private static string? GetRequired(
        IReadOnlyDictionary<string, string> settings,
        string key,
        ICollection<DriverEngineeringIssue> issues)
    {
        if (settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value.Trim();

        issues.Add(Error("iec104.config.required", $"IEC-104 setting '{key}' is required.", key));
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

        issues.Add(Error("iec104.config.integer", $"IEC-104 setting '{key}' must be an integer in the range {minimum}..{maximum}.", key));
        return defaultValue;
    }

    private static double GetSeconds(
        IReadOnlyDictionary<string, string> settings,
        string key,
        double defaultValue,
        ICollection<DriverEngineeringIssue> issues)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0.1 && value <= 86400)
            return value;

        issues.Add(Error("iec104.config.duration", $"IEC-104 setting '{key}' must be seconds in the range 0.1..86400.", key));
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
                issues.Add(Error("iec104.config.commonAddress", $"IEC-104 Common Address '{item}' is not a valid 16-bit unsigned integer.", "commonAddresses"));
                continue;
            }
            addresses.Add(address);
        }

        if (addresses.Count == 0)
            issues.Add(Error("iec104.config.commonAddress.empty", "At least one IEC-104 Common Address is required.", "commonAddresses"));

        return addresses.Distinct().OrderBy(static value => value).ToArray();
    }

    private static TimeZoneInfo? ValidateTimeZone(
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
            issues.Add(Error("iec104.config.timeZone", $"IEC-104 station time zone '{id}' is not available on this runtime.", "stationTimeZone"));
        }
        catch (InvalidTimeZoneException)
        {
            issues.Add(Error("iec104.config.timeZone.invalid", $"IEC-104 station time zone '{id}' is invalid on this runtime.", "stationTimeZone"));
        }

        return null;
    }

    private static DriverEngineeringIssue Error(string code, string message, string? fieldKey = null) =>
        new(code, DriverEngineeringIssueSeverity.Error, Sanitize(message), fieldKey);

    private static string FormatEndpoint(string host, int port) => $"{host}:{port}";

    private static string Sanitize(string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= MaximumIssueMessageLength
            ? sanitized
            : sanitized[..MaximumIssueMessageLength];
    }
}
