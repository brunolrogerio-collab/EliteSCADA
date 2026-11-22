using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Mitsubishi;

public sealed class MitsubishiMelsecEngineeringAdapter : ICommunicationDriverConnectionTester
{
    public CommunicationDriverTypeDescriptor Descriptor => MitsubishiMelsecDriverDescriptorProvider.SharedDescriptor;

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(context.DriverType, MitsubishiMelsecDriverDescriptorProvider.DriverTypeId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("MELSEC connection test received a different driver type.", nameof(context));
        if (context.SecretReferences.Count > 0)
            return Failure(null, "MELSEC_PROTECTED_MATERIAL_UNSUPPORTED", "The selected MELSEC 3E profile does not use protected material.");

        if (!MitsubishiMelsecConnectionOptions.TryCreate(context.Settings, out var options, out var error))
            return Failure(null, "MELSEC_CONNECTION_CONFIGURATION_INVALID", error ?? "MELSEC connection settings are invalid.");

        var validOptions = options!;
        await using var session = new MitsubishiMelsecTcpSession(validOptions);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await session.ExecuteAsync(
                MitsubishiMelsecProtocolCodec.ReadTypeNameCommand,
                0,
                ReadOnlyMemory<byte>.Empty,
                writeCommand: false,
                expectedDataLength: null,
                cancellationToken).ConfigureAwait(false);
            return new DriverConnectionTestResult(
                true,
                validOptions.SanitizedEndpoint,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["protocol"] = "SLMP / QnA-compatible MC Protocol",
                    ["frame"] = "3E",
                    ["encoding"] = "binary",
                    ["protocolResponsive"] = "true",
                    ["familyProfile"] = validOptions.FamilyProfile.ToString(),
                    ["route"] = FormatRoute(validOptions.Route),
                    ["responseDataLength"] = response.Data.Length.ToString(CultureInfo.InvariantCulture),
                    ["elapsedMilliseconds"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (MitsubishiMelsecProtocolException ex) when (ex.EndCode.HasValue)
        {
            // An end code proves the controller answered the read-only probe even if that command is unsupported.
            return new DriverConnectionTestResult(
                true,
                validOptions.SanitizedEndpoint,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["protocol"] = "SLMP / QnA-compatible MC Protocol",
                    ["frame"] = "3E",
                    ["encoding"] = "binary",
                    ["protocolResponsive"] = "true",
                    ["familyProfile"] = validOptions.FamilyProfile.ToString(),
                    ["route"] = FormatRoute(validOptions.Route),
                    ["typeNameProbeEndCode"] = ex.EndCode.Value.ToString("X4", CultureInfo.InvariantCulture)
                },
                new[]
                {
                    new DriverEngineeringIssue(
                        "MELSEC_TYPE_NAME_PROBE_REJECTED",
                        DriverEngineeringIssueSeverity.Warning,
                        "The endpoint answered the SLMP probe with an end code; connection is proven, device identity was not established.",
                        "host")
                });
        }
        catch (Exception ex) when (ex is IOException or SocketException or TimeoutException)
        {
            return Failure(validOptions.SanitizedEndpoint, "MELSEC_CONNECTION_FAILED", Sanitize(ex));
        }
    }

    private static DriverConnectionTestResult Failure(string? endpoint, string code, string message) =>
        new(false, endpoint, null, Issues: new[]
        {
            new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, message, "host")
        });

    internal static string FormatRoute(MitsubishiMelsecRoute route) =>
        $"net={route.NetworkNo:X2};station={route.StationNo:X2};moduleIo={route.ModuleIoNo:X4};multidrop={route.MultidropStationNo:X2}";

    internal static string Sanitize(Exception error) => error is MitsubishiMelsecProtocolException protocol
        ? $"{protocol.FailureKind}{(protocol.EndCode.HasValue ? $" (end code 0x{protocol.EndCode.Value:X4})" : string.Empty)}"
        : error is SocketException socket ? $"socket_error ({socket.SocketErrorCode})" : error.GetType().Name;
}

public sealed class MitsubishiMelsecPointReadTester : ICommunicationDriverPointReadTester
{
    public CommunicationDriverTypeDescriptor Descriptor => MitsubishiMelsecDriverDescriptorProvider.SharedDescriptor;

    public async ValueTask<DriverPointReadTestResult> TestPointReadAsync(
        DriverPointReadTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        MitsubishiMelsecPointReadPlan plan;
        try { plan = BuildPlan(request); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        {
            return ConfigurationFailure(request, MitsubishiMelsecEngineeringAdapter.Sanitize(ex));
        }

        await using var session = new MitsubishiMelsecTcpSession(plan.Options);
        var samples = new List<DriverPointReadSample>(request.SampleCount);
        for (var index = 0; index < request.SampleCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index > 0 && request.SampleIntervalMilliseconds > 0)
                await Task.Delay(request.SampleIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
            samples.Add(await ReadSampleAsync(session, plan, request, cancellationToken).ConfigureAwait(false));
        }
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
            status,
            plan.Options.SanitizedEndpoint,
            plan.Address.PortableAddress,
            new DriverPointReadSampleSummary(
                request.SampleCount,
                samples.Count,
                good,
                uncertain,
                bad,
                noData,
                latencies.Length == 0 ? null : latencies.Min(),
                latencies.Length == 0 ? null : latencies.Average(),
                latencies.Length == 0 ? null : latencies.Max()),
            samples);
    }

    private MitsubishiMelsecPointReadPlan BuildPlan(DriverPointReadTestRequest request)
    {
        var descriptor = Descriptor;
        if (!string.Equals(request.Context.DriverType, descriptor.DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Point read expected the Mitsubishi MELSEC driver.");
        if (request.AddressSelector is not null)
            throw new ArgumentException("MELSEC point read embeds the bit identity in its portable device address and does not accept AddressSelector.");
        var binding = request.Binding;
        if (!string.Equals(binding.SchemaId, MitsubishiMelsecDriverDescriptorProvider.BindingSchemaId, StringComparison.Ordinal) ||
            binding.SchemaVersion != MitsubishiMelsecDriverDescriptorProvider.BindingSchemaVersion)
            throw new ArgumentException("MELSEC point read requires the canonical Mitsubishi MELSEC binding schema v1.");
        var settings = CreateBoundedSettings(request.Context.Settings, request.TimeoutMilliseconds);
        if (!MitsubishiMelsecConnectionOptions.TryCreate(settings, out var options, out var error))
            throw new ArgumentException(error ?? "MELSEC source settings are invalid.");
        var validOptions = options!;
        if (!MitsubishiMelsecAddress.TryParse(binding.PortableAddress, validOptions.FamilyProfile, validOptions.RMaximumAddress, out var address, out var addressError))
            throw new ArgumentException(addressError ?? "MELSEC portable address is invalid.");
        var rawType = MitsubishiMelsecConnectionOptions.Get(binding.EffectiveSettings, "physicalDataType");
        if (!Enum.TryParse<MitsubishiMelsecPhysicalType>(rawType, true, out var physicalType) || !Enum.IsDefined(physicalType))
            throw new ArgumentException("MELSEC binding physicalDataType must be a supported physical value type.");
        var transform = binding.ValueTransform ?? new TagPhysicalValueTransform();
        transform.Validate();
        if (address!.IsBitDevice != (physicalType == MitsubishiMelsecPhysicalType.Bit))
            throw new ArgumentException("MELSEC physicalDataType does not match device storage area.");
        if (request.DataType != MitsubishiMelsecValueCodec.CanonicalDataType(physicalType))
            throw new ArgumentException($"MELSEC point read canonical data type must be {MitsubishiMelsecValueCodec.CanonicalDataType(physicalType)}.");
        if (physicalType == MitsubishiMelsecPhysicalType.Bit && !transform.IsIdentity)
            throw new ArgumentException("MELSEC bit points cannot use byte or word transforms.");
        return new MitsubishiMelsecPointReadPlan(validOptions, address, physicalType, transform);
    }

    private async Task<DriverPointReadSample> ReadSampleAsync(
        MitsubishiMelsecTcpSession session,
        MitsubishiMelsecPointReadPlan plan,
        DriverPointReadTestRequest request,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var isBit = plan.PhysicalType == MitsubishiMelsecPhysicalType.Bit;
        var count = checked((ushort)(isBit ? 1 : MitsubishiMelsecValueCodec.WordSpan(plan.PhysicalType)));
        var frame = MitsubishiMelsecProtocolCodec.BuildBatchRead(plan.Options.Route, plan.Options.MonitoringTimerUnits, plan.Address, count, isBit);
        try
        {
            var response = await session.ExecuteAsync(
                MitsubishiMelsecProtocolCodec.BatchReadCommand,
                isBit ? (ushort)1 : (ushort)0,
                frame.AsMemory(MitsubishiMelsecProtocolCodec.HeaderLength + 6),
                writeCommand: false,
                expectedDataLength: MitsubishiMelsecProtocolCodec.ExpectedBatchReadDataLength(count, isBit),
                cancellationToken).ConfigureAwait(false);
            var bit = isBit && MitsubishiMelsecProtocolCodec.DecodePackedBit(response.Data, 0);
            var decoded = MitsubishiMelsecValueCodec.Decode(plan.PhysicalType, response.Data, bit, plan.Transform);
            var latency = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var raw = new DriverPointReadRawRepresentation(
                isBit ? "slmpBit" : "slmpWords",
                Convert.ToHexString(response.Data),
                isBit ? new[] { bit ? "1" : "0" } : Enumerable.Range(0, response.Data.Length / 2)
                    .Select(word => BinaryPrimitives.ReadUInt16LittleEndian(response.Data.AsSpan(word * 2, 2)).ToString("X4", CultureInfo.InvariantCulture)).ToArray(),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["frame"] = "3E",
                    ["encoding"] = "binary",
                    ["command"] = "0401",
                    ["deviceArea"] = plan.Address.Area.ToString()
                });
            return new DriverPointReadSample(
                DriverPointReadTestStatus.Good,
                DateTimeOffset.UtcNow,
                null,
                latency,
                TagQuality.Good,
                raw,
                new DriverPointReadValue(plan.PhysicalType.ToString(), decoded),
                new DriverPointReadValue(request.DataType.ToString(), decoded, request.EngineeringUnit),
                plan.Transform);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or SocketException or TimeoutException or InvalidOperationException)
        {
            var status = ex is MitsubishiMelsecProtocolException { EndCode: 0xC051 or 0xC052 } ? DriverPointReadTestStatus.NoData : DriverPointReadTestStatus.Bad;
            return new DriverPointReadSample(
                status,
                DateTimeOffset.UtcNow,
                null,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                status == DriverPointReadTestStatus.NoData ? TagQuality.BadDevice : TagQuality.BadCommunication,
                Issues: new[] { new DriverEngineeringIssue("MELSEC_POINT_READ_FAILED", DriverEngineeringIssueSeverity.Error, MitsubishiMelsecEngineeringAdapter.Sanitize(ex)) },
                EffectiveValueTransform: plan.Transform);
        }
    }

    private static IReadOnlyDictionary<string, string> CreateBoundedSettings(IReadOnlyDictionary<string, string> source, int timeoutMilliseconds)
    {
        var settings = new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
        var requestedTimer = 20;
        var rawTimer = MitsubishiMelsecConnectionOptions.Get(settings, "monitoringTimerUnits");
        if (!string.IsNullOrWhiteSpace(rawTimer) && ushort.TryParse(rawTimer, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            requestedTimer = parsed;
        var maxTimer = Math.Max(1, (timeoutMilliseconds - 250) / 250);
        settings["monitoringTimerUnits"] = Math.Min(requestedTimer, maxTimer).ToString(CultureInfo.InvariantCulture);
        settings["requestTimeoutMilliseconds"] = timeoutMilliseconds.ToString(CultureInfo.InvariantCulture);
        return settings;
    }

    private static DriverPointReadTestResult ConfigurationFailure(DriverPointReadTestRequest request, string message)
    {
        var sample = new DriverPointReadSample(
            DriverPointReadTestStatus.Bad,
            DateTimeOffset.UtcNow,
            null,
            null,
            TagQuality.BadConfiguration,
            Issues: new[] { new DriverEngineeringIssue("MELSEC_POINT_READ_CONFIGURATION_INVALID", DriverEngineeringIssueSeverity.Error, message) });
        return new DriverPointReadTestResult(
            DriverPointReadTestStatus.Bad,
            null,
            request.Binding.PortableAddress,
            new DriverPointReadSampleSummary(1, 1, 0, 0, 1, 0),
            new[] { sample });
    }

    private sealed record MitsubishiMelsecPointReadPlan(
        MitsubishiMelsecConnectionOptions Options,
        MitsubishiMelsecAddress Address,
        MitsubishiMelsecPhysicalType PhysicalType,
        TagPhysicalValueTransform Transform);
}
