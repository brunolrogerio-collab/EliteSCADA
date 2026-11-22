using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Panasonic;

public sealed class PanasonicMewtocolEngineeringAdapter : ICommunicationDriverConnectionTester
{
    private readonly HostSerialBusCoordinator _serialCoordinator;

    public PanasonicMewtocolEngineeringAdapter(string driverType, HostSerialBusCoordinator serialCoordinator)
    {
        _ = PanasonicMewtocolDriverDescriptorProvider.For(driverType);
        DriverType = driverType;
        _serialCoordinator = serialCoordinator ?? throw new ArgumentNullException(nameof(serialCoordinator));
    }

    public string DriverType { get; }
    public CommunicationDriverTypeDescriptor Descriptor => PanasonicMewtocolDriverDescriptorProvider.For(DriverType);

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(context.DriverType, DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Panasonic connection test received a different driver type.", nameof(context));
        if (context.SecretReferences.Count > 0)
            return Failure(null, "PANASONIC_PROTECTED_MATERIAL_UNSUPPORTED", "MEWTOCOL-COM v1 does not use protected material.");
        if (!PanasonicMewtocolConnectionOptions.TryCreate(DriverType, context.Settings, out var options, out var error))
            return Failure(null, "PANASONIC_CONNECTION_CONFIGURATION_INVALID", error ?? "Panasonic Data Source settings are invalid.");

        var validOptions = options!;
        IPanasonicMewtocolSession session = CreateSession(validOptions, context.DataSourceKey, engineeringReadOnly: true);
        await using (session.ConfigureAwait(false))
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                var response = await session.ExecuteAsync(
                    PanasonicMewtocolProtocolCodec.BuildStatusRead(validOptions.Station, validOptions.FrameMode),
                    "RT", expectedDataLength: null, writeCommand: false, cancellationToken).ConfigureAwait(false);
                return new DriverConnectionTestResult(
                    true, validOptions.SanitizedEndpoint, null,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["protocol"] = "MEWTOCOL-COM",
                        ["protocolResponsive"] = "true",
                        ["transport"] = validOptions.Transport.ToString(),
                        ["familyProfile"] = validOptions.FamilyProfile.ToString(),
                        ["station"] = validOptions.Station.ToString(CultureInfo.InvariantCulture),
                        ["responseDataLength"] = response.Data.Length.ToString(CultureInfo.InvariantCulture),
                        ["elapsedMilliseconds"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)
                    });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (PanasonicMewtocolProtocolException ex) when (ex.FailureKind == "plc_error")
            {
                return new DriverConnectionTestResult(
                    true, validOptions.SanitizedEndpoint, null,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["protocol"] = "MEWTOCOL-COM",
                        ["protocolResponsive"] = "true",
                        ["transport"] = validOptions.Transport.ToString(),
                        ["familyProfile"] = validOptions.FamilyProfile.ToString(),
                        ["plcErrorCode"] = ex.ErrorCode ?? string.Empty
                    },
                    new[] { new DriverEngineeringIssue("PANASONIC_STATUS_PROBE_REJECTED", DriverEngineeringIssueSeverity.Warning,
                        "The endpoint returned a valid MEWTOCOL-COM error response; transport and protocol responsiveness are proven, PLC status was not read.", "host") });
            }
            catch (Exception ex) when (ex is IOException or SocketException or TimeoutException or InvalidOperationException or ArgumentException)
            {
                return Failure(validOptions.SanitizedEndpoint, "PANASONIC_CONNECTION_FAILED", Sanitize(ex));
            }
        }
    }

    private IPanasonicMewtocolSession CreateSession(PanasonicMewtocolConnectionOptions options, string owner, bool engineeringReadOnly) =>
        options.Transport == PanasonicMewtocolTransportKind.Tcp
            ? new PanasonicMewtocolTcpSession(options)
            : new PanasonicMewtocolSerialSession(options, _serialCoordinator, $"engineering:{owner}:{Guid.NewGuid():N}", engineeringReadOnly);

    private static DriverConnectionTestResult Failure(string? endpoint, string code, string message) =>
        new(false, endpoint, null, Issues: new[] { new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, message, "host") });

    internal static string Sanitize(Exception error) => error switch
    {
        PanasonicMewtocolProtocolException protocol => $"{protocol.FailureKind}{(protocol.ErrorCode is null ? string.Empty : $" (0x{protocol.ErrorCode})")}",
        SocketException socket => $"socket_error ({socket.SocketErrorCode})",
        TimeoutException => "timeout",
        _ => error.GetType().Name
    };
}

public sealed class PanasonicMewtocolPointReadTester : ICommunicationDriverPointReadTester
{
    private readonly string _driverType;
    private readonly HostSerialBusCoordinator _serialCoordinator;

    public PanasonicMewtocolPointReadTester(string driverType, HostSerialBusCoordinator serialCoordinator)
    {
        _ = PanasonicMewtocolDriverDescriptorProvider.For(driverType);
        _driverType = driverType;
        _serialCoordinator = serialCoordinator ?? throw new ArgumentNullException(nameof(serialCoordinator));
    }

    public CommunicationDriverTypeDescriptor Descriptor => PanasonicMewtocolDriverDescriptorProvider.For(_driverType);

    public async ValueTask<DriverPointReadTestResult> TestPointReadAsync(
        DriverPointReadTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        PointReadPlan plan;
        try { plan = BuildPlan(request); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        {
            return ConfigurationFailure(request, PanasonicMewtocolEngineeringAdapter.Sanitize(ex));
        }

        IPanasonicMewtocolSession session = plan.Options.Transport == PanasonicMewtocolTransportKind.Tcp
            ? new PanasonicMewtocolTcpSession(plan.Options)
            : new PanasonicMewtocolSerialSession(plan.Options, _serialCoordinator, $"engineering:pointread:{Guid.NewGuid():N}", engineeringReadOnly: true);
        await using (session.ConfigureAwait(false))
        {
            var samples = new List<DriverPointReadSample>(request.SampleCount);
            for (var index = 0; index < request.SampleCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index > 0 && request.SampleIntervalMilliseconds > 0)
                    await Task.Delay(request.SampleIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
                samples.Add(await ReadSampleAsync(session, plan, request, cancellationToken).ConfigureAwait(false));
            }
            return BuildResult(plan, request, samples);
        }
    }

    private PointReadPlan BuildPlan(DriverPointReadTestRequest request)
    {
        if (!string.Equals(request.Context.DriverType, _driverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Point Read expected the matching Panasonic MEWTOCOL driver type.");
        if (request.AddressSelector is not null)
            throw new ArgumentException("Panasonic MEWTOCOL Point Read does not accept generic AddressSelector.");
        if (!string.Equals(request.Binding.SchemaId, PanasonicMewtocolDriverDescriptorProvider.BindingSchemaId, StringComparison.Ordinal) ||
            request.Binding.SchemaVersion != PanasonicMewtocolDriverDescriptorProvider.BindingSchemaVersion)
            throw new ArgumentException($"Panasonic Point Read requires binding schema '{PanasonicMewtocolDriverDescriptorProvider.BindingSchemaId}' v{PanasonicMewtocolDriverDescriptorProvider.BindingSchemaVersion}.");
        if (!PanasonicMewtocolConnectionOptions.TryCreate(_driverType, WithRequestTimeout(request.Context.Settings, request.TimeoutMilliseconds), out var options, out var optionError))
            throw new ArgumentException(optionError ?? "Panasonic Data Source settings are invalid.");
        if (!PanasonicMewtocolAddress.TryParse(request.Binding.PortableAddress, options!.FamilyProfile, out var address, out var addressError))
            throw new ArgumentException(addressError ?? "Panasonic portable address is invalid.");
        var rawType = PanasonicMewtocolConnectionOptions.Get(request.Binding.EffectiveSettings, "physicalDataType");
        if (!Enum.TryParse<PanasonicMewtocolPhysicalType>(rawType, true, out var physicalType) || !Enum.IsDefined(physicalType))
            throw new ArgumentException("Panasonic physicalDataType must be Boolean, UInt16 or Int16.");
        var transform = request.Binding.ValueTransform ?? new TagPhysicalValueTransform();
        transform.Validate();
        if (address!.IsContact != (physicalType == PanasonicMewtocolPhysicalType.Boolean))
            throw new ArgumentException("Panasonic physicalDataType does not match the contact/word address.");
        if (transform.WordSwap || (physicalType == PanasonicMewtocolPhysicalType.Boolean && !transform.IsIdentity))
            throw new ArgumentException("Panasonic supports byte swap on one-word values only; Boolean contacts have no transform.");
        if (request.DataType != PanasonicMewtocolValueCodec.CanonicalDataType(physicalType))
            throw new ArgumentException($"Panasonic canonical data type must be {PanasonicMewtocolValueCodec.CanonicalDataType(physicalType)}.");
        var tag = TagDefinition.Create(
            "Panasonic Point Read", "__engineering.pointRead.panasonic", request.DataType,
            source: request.Context.DataSourceKey, engineeringUnit: request.EngineeringUnit,
            readOnly: true, communicationBinding: request.Binding);
        var point = new PanasonicMewtocolPoint(tag, address, physicalType, Writable: false, transform);
        return new PointReadPlan(options, point);
    }

    private static async Task<DriverPointReadSample> ReadSampleAsync(
        IPanasonicMewtocolSession session,
        PointReadPlan plan,
        DriverPointReadTestRequest request,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var point = plan.Point;
        var isContact = point.Address.IsContact;
        var requestFrame = isContact
            ? PanasonicMewtocolProtocolCodec.BuildReadContacts(plan.Options.Station, new[] { point.Address }, plan.Options.FrameMode)
            : PanasonicMewtocolProtocolCodec.BuildReadWords(plan.Options.Station, point.Address, 1, plan.Options.FrameMode);
        try
        {
            var response = await session.ExecuteAsync(
                requestFrame, isContact ? "RC" : "RD", isContact ? 1 : 4, writeCommand: false, cancellationToken).ConfigureAwait(false);
            object value;
            DriverPointReadRawRepresentation raw;
            if (isContact)
            {
                var status = response.Data[0];
                if (status is not ('0' or '1')) throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM contact response contains a value other than 0 or 1.", "invalid_contact_value");
                value = status == '1';
                raw = new DriverPointReadRawRepresentation("contact", status.ToString(), new[] { status.ToString() },
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = "RCS", ["area"] = point.Address.Area.ToString() });
            }
            else
            {
                var wireBytes = Convert.FromHexString(response.Data);
                value = PanasonicMewtocolValueCodec.DecodeWord(wireBytes, point.PhysicalType, point.Transform);
                raw = new DriverPointReadRawRepresentation("wordBytes", Convert.ToHexString(wireBytes), new[] { response.Data },
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = "RD", ["area"] = point.Address.Area.ToString(), ["encoding"] = "MEWTOCOL-COM low-byte-first hex" });
            }
            return new DriverPointReadSample(
                DriverPointReadTestStatus.Good, DateTimeOffset.UtcNow, null,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, TagQuality.Good,
                raw,
                new DriverPointReadValue(point.PhysicalType.ToString(), value),
                new DriverPointReadValue(request.DataType.ToString(), value, request.EngineeringUnit),
                point.Transform);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or TimeoutException or SocketException or InvalidOperationException or ArgumentException)
        {
            var noData = ex is PanasonicMewtocolProtocolException protocol &&
                protocol.FailureKind is "request_timeout" or "connect_timeout" or "transport_error" or "connect_error";
            var issue = new DriverEngineeringIssue(
                "PANASONIC_POINT_READ_FAILED", DriverEngineeringIssueSeverity.Error,
                PanasonicMewtocolEngineeringAdapter.Sanitize(ex), "address");
            return new DriverPointReadSample(
                noData ? DriverPointReadTestStatus.NoData : DriverPointReadTestStatus.Bad,
                DateTimeOffset.UtcNow, null, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                noData ? TagQuality.BadCommunication : TagQuality.Bad,
                Issues: new[] { issue }, EffectiveValueTransform: point.Transform);
        }
    }

    private static DriverPointReadTestResult BuildResult(
        PointReadPlan plan,
        DriverPointReadTestRequest request,
        IReadOnlyCollection<DriverPointReadSample> samples)
    {
        var good = samples.Count(sample => sample.Status == DriverPointReadTestStatus.Good);
        var bad = samples.Count(sample => sample.Status == DriverPointReadTestStatus.Bad);
        var noData = samples.Count(sample => sample.Status == DriverPointReadTestStatus.NoData);
        var uncertain = samples.Count - good - bad - noData;
        var status = good == samples.Count ? DriverPointReadTestStatus.Good
            : good > 0 ? DriverPointReadTestStatus.IntermittentOrUncertain
            : noData == samples.Count ? DriverPointReadTestStatus.NoData
            : DriverPointReadTestStatus.Bad;
        var latencies = samples.Where(sample => sample.LatencyMilliseconds.HasValue).Select(sample => sample.LatencyMilliseconds!.Value).ToArray();
        return new DriverPointReadTestResult(
            status, plan.Options.SanitizedEndpoint, plan.Point.Address.PortableAddress,
            new DriverPointReadSampleSummary(
                request.SampleCount, samples.Count, good, uncertain, bad, noData,
                latencies.Length == 0 ? null : latencies.Min(),
                latencies.Length == 0 ? null : latencies.Average(),
                latencies.Length == 0 ? null : latencies.Max()), samples);
    }

    private static IReadOnlyDictionary<string, string> WithRequestTimeout(IReadOnlyDictionary<string, string> settings, int timeoutMilliseconds)
    {
        var copy = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase)
        {
            ["requestTimeoutMilliseconds"] = Math.Clamp(timeoutMilliseconds, 250, 60000).ToString(CultureInfo.InvariantCulture)
        };
        return copy;
    }

    private DriverPointReadTestResult ConfigurationFailure(DriverPointReadTestRequest request, string message) =>
        new(DriverPointReadTestStatus.Bad, null, request.Binding.PortableAddress,
            new DriverPointReadSampleSummary(request.SampleCount, 0, 0, 0, request.SampleCount, 0), Array.Empty<DriverPointReadSample>(),
            new[] { new DriverEngineeringIssue("PANASONIC_POINT_READ_CONFIGURATION_INVALID", DriverEngineeringIssueSeverity.Error, message, "address") });

    private sealed record PointReadPlan(PanasonicMewtocolConnectionOptions Options, PanasonicMewtocolPoint Point);
}
