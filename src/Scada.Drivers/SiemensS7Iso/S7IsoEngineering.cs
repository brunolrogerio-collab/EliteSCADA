using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.SiemensS7Iso;

public sealed class S7IsoEngineeringAdapter :
    ICommunicationDriverConnectionTester,
    ICommunicationDriverFileImporter
{
    public CommunicationDriverTypeDescriptor Descriptor { get; } = CreateDescriptor();

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!TryCreateOptions(context.Settings, out var options, out var issues))
            return new DriverConnectionTestResult(false, null, null, Issues: issues);

        await using var transport = new S7IsoTransport(options!);
        try
        {
            await transport.ConnectAsync(cancellationToken);
            var diagnostics = transport.GetDiagnostics();
            var observed = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["connectionMode"] = options!.ConnectionMode.ToString(),
                ["sourceTsap"] = S7IsoConnectionOptions.FormatTsap(options.EffectiveSourceTsap),
                ["destinationTsap"] = S7IsoConnectionOptions.FormatTsap(options.EffectiveDestinationTsap),
                ["requestedPduSize"] = options.RequestedPduSize.ToString(CultureInfo.InvariantCulture),
                ["negotiatedPduSize"] = diagnostics.NegotiatedPduSize?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                ["writeEnabled"] = options.WriteEnabled ? "true" : "false"
            };

            return new DriverConnectionTestResult(
                true,
                options.SanitizedEndpoint,
                null,
                observed,
                Array.Empty<DriverEngineeringIssue>());
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or SocketException or InvalidOperationException)
        {
            return new DriverConnectionTestResult(
                false,
                options!.SanitizedEndpoint,
                null,
                Issues: new[]
                {
                    new DriverEngineeringIssue(
                        "S7_CONNECTION_FAILED",
                        DriverEngineeringIssueSeverity.Error,
                        SanitizeError(ex))
                });
        }
    }

    public async IAsyncEnumerable<DriverImportCandidate> ImportAsync(
        DriverImportRequest request,
        Stream content,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);

        var format = ResolveTiaImportFormat(request.SourceName, request.ContentType);
        if (format == S7TiaImportFormat.Unsupported)
        {
            yield return UnsupportedImportFormat(request.SourceName, request.ContentType);
            yield break;
        }

        IReadOnlyList<DriverImportCandidate> candidates = Array.Empty<DriverImportCandidate>();
        DriverImportCandidate? parseFailure = null;
        try
        {
            candidates = format switch
            {
                S7TiaImportFormat.Xlsx => S7TiaXlsxImporter.Parse(request.SourceName, content, cancellationToken),
                S7TiaImportFormat.Xml => S7TiaXmlImporter.Parse(request.SourceName, content, cancellationToken),
                S7TiaImportFormat.Sdf => S7TiaSdfImporter.Parse(request.SourceName, content, cancellationToken),
                _ => throw new InvalidOperationException("Unsupported TIA import format routing state.")
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or FormatException or System.Xml.XmlException or System.Text.DecoderFallbackException)
        {
            parseFailure = InvalidImportCandidate(format, request.SourceName, ex);
        }

        if (parseFailure is not null)
        {
            yield return parseFailure;
            yield break;
        }

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return candidate;
            await Task.Yield();
        }
    }

    internal static bool TryCreateOptions(
        IReadOnlyDictionary<string, string> settings,
        out S7IsoConnectionOptions? options,
        out IReadOnlyCollection<DriverEngineeringIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<DriverEngineeringIssue>();
        options = null;

        var host = Required(settings, "host", errors);
        var family = ParseRequiredEnum<S7CpuFamily>(settings, "cpuFamily", errors);
        var mode = ParseRequiredEnum<S7IsoConnectionMode>(settings, "connectionMode", errors);
        var port = ParseInt(settings, "port", 102, 1, 65535, errors);

        byte? rack = null;
        byte? slot = null;
        var role = S7IsoConnectionRole.OperatorPanel;
        if (mode == S7IsoConnectionMode.RackSlot)
        {
            rack = checked((byte)ParseRequiredInt(settings, "rack", 0, 7, errors));
            slot = checked((byte)ParseRequiredInt(settings, "slot", 0, 31, errors));
            role = ParseRequiredEnum<S7IsoConnectionRole>(settings, "connectionRole", errors);
        }

        var sourceTsap = ParseTsap(settings, "sourceTsap", 0x0100, required: false, errors);
        ushort? destinationTsap = mode == S7IsoConnectionMode.ExplicitTsap
            ? ParseTsap(settings, "destinationTsap", 0, required: true, errors)
            : null;

        var connectTimeoutMs = ParseInt(settings, "connectTimeoutMs", 5000, 1, 300_000, errors);
        var requestTimeoutMs = ParseInt(settings, "requestTimeoutMs", 3000, 1, 300_000, errors);
        var reconnectDelayMs = ParseInt(settings, "reconnectDelayMs", 1000, 0, 300_000, errors);
        var requestedPduSize = ParseInt(settings, "requestedPduSize", 480, 240, 960, errors);
        var writeEnabled = ParseBool(settings, "writeEnabled", false, errors);

        if (errors.Count == 0)
        {
            try
            {
                options = new S7IsoConnectionOptions(
                    host!,
                    family,
                    mode,
                    rack,
                    slot,
                    role,
                    sourceTsap,
                    destinationTsap,
                    port,
                    TimeSpan.FromMilliseconds(connectTimeoutMs),
                    TimeSpan.FromMilliseconds(requestTimeoutMs),
                    TimeSpan.FromMilliseconds(reconnectDelayMs),
                    checked((ushort)requestedPduSize),
                    writeEnabled);
            }
            catch (Exception ex) when (ex is ArgumentException or OverflowException)
            {
                errors.Add(new DriverEngineeringIssue(
                    "S7_CONFIGURATION_INVALID",
                    DriverEngineeringIssueSeverity.Error,
                    ex.Message));
            }
        }

        issues = errors;
        return errors.Count == 0 && options is not null;
    }

    private static CommunicationDriverTypeDescriptor CreateDescriptor()
    {
        var dataSourceFields = new[]
        {
            Field("host", DriverConfigurationValueKind.Host, required: true, display: "Host / IP"),
            Field("port", DriverConfigurationValueKind.Port, defaultValue: "102", minimum: 1, maximum: 65535),
            Field("cpuFamily", DriverConfigurationValueKind.Enum, required: true, allowed: Enum.GetNames<S7CpuFamily>()),
            Field("connectionMode", DriverConfigurationValueKind.Enum, required: true, allowed: Enum.GetNames<S7IsoConnectionMode>()),
            Field("rack", DriverConfigurationValueKind.Integer, minimum: 0, maximum: 7),
            Field("slot", DriverConfigurationValueKind.Integer, minimum: 0, maximum: 31),
            Field("connectionRole", DriverConfigurationValueKind.Enum, allowed: Enum.GetNames<S7IsoConnectionRole>()),
            Field("writeEnabled", DriverConfigurationValueKind.Boolean, defaultValue: "false", display: "Enable writes"),
            Field("sourceTsap", DriverConfigurationValueKind.Identifier, defaultValue: "0x0100", advanced: true),
            Field("destinationTsap", DriverConfigurationValueKind.Identifier, advanced: true),
            Field("connectTimeoutMs", DriverConfigurationValueKind.Integer, defaultValue: "5000", minimum: 1, maximum: 300000, advanced: true),
            Field("requestTimeoutMs", DriverConfigurationValueKind.Integer, defaultValue: "3000", minimum: 1, maximum: 300000, advanced: true),
            Field("reconnectDelayMs", DriverConfigurationValueKind.Integer, defaultValue: "1000", minimum: 0, maximum: 300000, advanced: true),
            Field("requestedPduSize", DriverConfigurationValueKind.Integer, defaultValue: "480", minimum: 240, maximum: 960, advanced: true)
        };

        var tagFields = new[]
        {
            Field("area", DriverConfigurationValueKind.Enum, required: true, allowed: Enum.GetNames<S7IsoArea>()),
            Field("dbNumber", DriverConfigurationValueKind.Integer, minimum: 0, maximum: ushort.MaxValue),
            Field("byteOffset", DriverConfigurationValueKind.Integer, required: true, minimum: 0, maximum: 2097151),
            Field("bitOffset", DriverConfigurationValueKind.Integer, minimum: 0, maximum: 7),
            Field("valueType", DriverConfigurationValueKind.Enum, required: true, allowed: Enum.GetNames<S7IsoValueType>()),
            Field("stringLength", DriverConfigurationValueKind.Integer, minimum: 0, maximum: 254),
            Field("writable", DriverConfigurationValueKind.Boolean, defaultValue: "false"),
            Field("valueOrder", DriverConfigurationValueKind.Enum, defaultValue: nameof(S7IsoValueOrder.Normal), allowed: Enum.GetNames<S7IsoValueOrder>())
        };

        return new CommunicationDriverTypeDescriptor(
            "siemens.s7.iso",
            "Siemens S7 ISO-on-TCP",
            1,
            DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics,
            DriverEngineeringCapabilities.ConnectionTest | DriverEngineeringCapabilities.FileImport,
            new[] { DriverAcquisitionMode.Polling },
            new DriverConfigurationSchemaDescriptor(
                "siemens.s7.iso",
                1,
                dataSourceFields,
                tagFields),
            Description: "Classic Siemens S7 communication over ISO-on-TCP / RFC1006 with Engineering-side TIA XLSX, XML and SDF PLC-tag import.");
    }

    private static DriverImportCandidate InvalidImportCandidate(
        S7TiaImportFormat format,
        string sourceName,
        Exception error)
    {
        var token = format switch
        {
            S7TiaImportFormat.Xlsx => "xlsx",
            S7TiaImportFormat.Xml => "xml",
            S7TiaImportFormat.Sdf => "sdf",
            _ => "export"
        };
        var sourceKind = format switch
        {
            S7TiaImportFormat.Xlsx => "TiaXlsx",
            S7TiaImportFormat.Xml => "TiaXml",
            S7TiaImportFormat.Sdf => "TiaSdf",
            _ => "TiaExport"
        };

        return new DriverImportCandidate(
            $"tia-{token}-parse-error",
            $"{sourceKind}|{sourceName}",
            sourceName,
            $"tia-{token}:parse-error",
            false,
            false,
            Issues: new[]
            {
                new DriverEngineeringIssue(
                    $"S7_TIA_{token.ToUpperInvariant()}_INVALID",
                    DriverEngineeringIssueSeverity.Error,
                    SanitizeError(error))
            });
    }

    private static DriverImportCandidate UnsupportedImportFormat(string sourceName, string? contentType) =>
        new(
            "tia-format-unsupported",
            $"TiaExport|{sourceName}",
            sourceName,
            "tia-export:unsupported-format",
            false,
            false,
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceName"] = sourceName,
                ["contentType"] = contentType ?? string.Empty,
                ["supportedFormats"] = "xlsx,xml,sdf"
            },
            Issues: new[]
            {
                new DriverEngineeringIssue(
                    "S7_TIA_FORMAT_NOT_IMPLEMENTED",
                    DriverEngineeringIssueSeverity.Error,
                    "This S7 Engineering slice supports TIA PLC-tag XLSX, XML and SDF imports. TIA Openness remains explicit follow-up work.")
            });

    private static S7TiaImportFormat ResolveTiaImportFormat(string sourceName, string? contentType)
    {
        if (sourceName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return S7TiaImportFormat.Xlsx;
        if (sourceName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return S7TiaImportFormat.Xml;
        if (sourceName.EndsWith(".sdf", StringComparison.OrdinalIgnoreCase)) return S7TiaImportFormat.Sdf;

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase)) return S7TiaImportFormat.Xlsx;
            if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase)) return S7TiaImportFormat.Xml;
        }

        return S7TiaImportFormat.Unsupported;
    }

    private static DriverConfigurationFieldDescriptor Field(
        string key,
        DriverConfigurationValueKind kind,
        bool required = false,
        string? display = null,
        string? defaultValue = null,
        IReadOnlyCollection<string>? allowed = null,
        double? minimum = null,
        double? maximum = null,
        bool advanced = false) =>
        new(
            key,
            kind,
            required,
            display,
            DefaultValue: defaultValue,
            AllowedValues: allowed,
            Minimum: minimum,
            Maximum: maximum,
            Advanced: advanced);

    private static string? Required(
        IReadOnlyDictionary<string, string> settings,
        string key,
        List<DriverEngineeringIssue> errors)
    {
        if (settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value.Trim();

        errors.Add(Issue(key, $"S7 setting '{key}' is required."));
        return null;
    }

    private static int ParseRequiredInt(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int minimum,
        int maximum,
        List<DriverEngineeringIssue> errors)
    {
        if (!settings.TryGetValue(key, out var text))
        {
            errors.Add(Issue(key, $"S7 setting '{key}' is required for the selected connection mode."));
            return minimum;
        }

        return ParseIntValue(text, key, minimum, maximum, errors);
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int defaultValue,
        int minimum,
        int maximum,
        List<DriverEngineeringIssue> errors)
    {
        if (!settings.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
            return defaultValue;
        return ParseIntValue(text, key, minimum, maximum, errors);
    }

    private static int ParseIntValue(
        string text,
        string key,
        int minimum,
        int maximum,
        List<DriverEngineeringIssue> errors)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
            value >= minimum &&
            value <= maximum)
            return value;

        errors.Add(Issue(key, $"S7 setting '{key}' must be an integer from {minimum} to {maximum}."));
        return minimum;
    }

    private static bool ParseBool(
        IReadOnlyDictionary<string, string> settings,
        string key,
        bool defaultValue,
        List<DriverEngineeringIssue> errors)
    {
        if (!settings.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
            return defaultValue;
        if (bool.TryParse(text, out var value))
            return value;

        errors.Add(Issue(key, $"S7 setting '{key}' must be true or false."));
        return defaultValue;
    }

    private static T ParseRequiredEnum<T>(
        IReadOnlyDictionary<string, string> settings,
        string key,
        List<DriverEngineeringIssue> errors)
        where T : struct, Enum
    {
        if (settings.TryGetValue(key, out var text) &&
            Enum.TryParse<T>(text, true, out var value) &&
            Enum.IsDefined(value))
            return value;

        errors.Add(Issue(key, $"S7 setting '{key}' is required and must use a supported value."));
        return default;
    }

    private static ushort ParseTsap(
        IReadOnlyDictionary<string, string> settings,
        string key,
        ushort defaultValue,
        bool required,
        List<DriverEngineeringIssue> errors)
    {
        if (!settings.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
        {
            if (required) errors.Add(Issue(key, $"S7 setting '{key}' is required."));
            return defaultValue;
        }

        if (S7IsoConnectionOptions.TryParseTsap(text, out var value))
            return value;

        errors.Add(Issue(key, $"S7 setting '{key}' must use a TSAP such as 0x0301 or 03.01."));
        return defaultValue;
    }

    private static DriverEngineeringIssue Issue(string key, string message) =>
        new(
            $"S7_{key.ToUpperInvariant()}_INVALID",
            DriverEngineeringIssueSeverity.Error,
            message,
            key);

    private static string SanitizeError(Exception error)
    {
        var message = error.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return message.Length <= 512 ? message : message[..512];
    }

    private enum S7TiaImportFormat
    {
        Unsupported,
        Xlsx,
        Xml,
        Sdf
    }
}
