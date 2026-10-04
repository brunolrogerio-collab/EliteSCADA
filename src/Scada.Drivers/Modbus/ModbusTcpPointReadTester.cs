using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Modbus;

public abstract class ModbusPointReadTesterBase :
    ICommunicationDriverDescriptorProvider,
    ICommunicationDriverPointReadTester
{
    public abstract CommunicationDriverTypeDescriptor Descriptor { get; }
    protected abstract string DriverType { get; }

    protected abstract IModbusMasterTransport CreateTransport(
        DriverPointReadTestRequest request,
        byte unitId,
        int requestTimeoutMilliseconds);

    protected abstract string BuildSanitizedEndpoint(DriverPointReadTestRequest request);

    public async ValueTask<DriverPointReadTestResult> TestPointReadAsync(
        DriverPointReadTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        PointReadPlan plan;
        try
        {
            plan = BuildPlan(request);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        {
            return ConfigurationFailure(request, "MODBUS_POINT_READ_CONFIGURATION_INVALID", SanitizeError(ex));
        }

        var resultIssues = new List<DriverEngineeringIssue>();
        if (plan.LegacyWordOrderNormalized)
        {
            resultIssues.Add(new DriverEngineeringIssue(
                "MODBUS_WORD_ORDER_NORMALIZED",
                DriverEngineeringIssueSeverity.Information,
                "Legacy Modbus word order and the shared physical transform were normalized to one effective Word Swap operation."));
        }

        IModbusMasterTransport transport;
        string endpoint;
        try
        {
            transport = CreateTransport(
                request,
                plan.Point.UnitId,
                Math.Min(plan.RequestTimeoutMilliseconds, request.TimeoutMilliseconds));
            endpoint = BuildSanitizedEndpoint(request);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        {
            return ConfigurationFailure(request, "MODBUS_POINT_READ_TRANSPORT_INVALID", SanitizeError(ex));
        }

        await using (transport)
        {
            var samples = new List<DriverPointReadSample>(request.SampleCount);
            for (var sampleIndex = 0; sampleIndex < request.SampleCount; sampleIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sampleIndex > 0 && request.SampleIntervalMilliseconds > 0)
                    await Task.Delay(request.SampleIntervalMilliseconds, cancellationToken);

                samples.Add(await ReadSampleAsync(transport, plan, request, cancellationToken));
            }

            return BuildResult(
                request,
                endpoint,
                samples,
                resultIssues.Count == 0 ? null : resultIssues);
        }
    }

    private PointReadPlan BuildPlan(DriverPointReadTestRequest request)
    {
        if (!string.Equals(request.Context.DriverType, DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Point read expected Driver '{DriverType}'.");

        var bindingSchemaId = Descriptor.TagBindingSchemaId ?? Descriptor.ConfigurationSchema.SchemaId;
        var bindingSchemaVersion = Descriptor.TagBindingSchemaVersion ?? Descriptor.ConfigurationSchema.SchemaVersion;
        if (!string.Equals(request.Binding.SchemaId, bindingSchemaId, StringComparison.Ordinal) ||
            request.Binding.SchemaVersion != bindingSchemaVersion)
        {
            throw new ArgumentException(
                $"Modbus point read requires binding schema '{bindingSchemaId}' v{bindingSchemaVersion}.");
        }

        var requestTimeoutMilliseconds = ParseInt(
            request.Context.Settings,
            "requestTimeoutMilliseconds",
            3000,
            50,
            60000);
        var unitId = ParseInt(
            request.Binding.EffectiveSettings,
            "modbus.unitId",
            ParseInt(request.Context.Settings, "unitId", 1, 0, 255),
            0,
            255);

        if (!ModbusTagAddressCodec.TryParse(
                request.Binding.PortableAddress,
                request.Binding.EffectiveSettings,
                out var area,
                out var address,
                out var addressError))
        {
            throw new ArgumentException(addressError ?? "Modbus portable address is invalid.");
        }

        var valueType = ParseValueType(request);
        var scale = ParseDouble(request.Binding.EffectiveSettings, "modbus.scale", 1d, nonZero: true);
        var offset = ParseDouble(request.Binding.EffectiveSettings, "modbus.offset", 0d, nonZero: false);

        var transform = request.Binding.ValueTransform ?? new TagPhysicalValueTransform();
        transform.Validate();
        var nativeWordOrder = ParseWordOrder(request.Binding.EffectiveSettings);
        var nativeWordSwap = nativeWordOrder == ModbusWordOrder.LowWordFirst;
        var effectiveTransform = new TagPhysicalValueTransform(
            TagPhysicalValueTransform.CurrentContractVersion,
            transform.ByteSwap,
            transform.WordSwap || nativeWordSwap);

        if (area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput && !effectiveTransform.IsIdentity)
            throw new ArgumentException("Byte Swap / Word Swap is not applicable to Modbus coil or discrete-input reads.");

        var tag = TagDefinition.Create(
            "Point Read Test",
            "__engineering.pointRead.modbus",
            request.DataType,
            source: request.Context.DataSourceKey,
            engineeringUnit: request.EngineeringUnit,
            readOnly: true,
            addressSelector: request.AddressSelector,
            communicationBinding: request.Binding);
        var point = new ModbusPoint(
            tag,
            checked((byte)unitId),
            area,
            address,
            valueType,
            Writable: false,
            WordOrder: ModbusWordOrder.HighWordFirst,
            Scale: 1d,
            Offset: 0d);
        point.Validate();

        if (DriverType == ModbusRtuDriverDescriptorProvider.DriverTypeId && point.UnitId is < 1 or > 247)
            throw new ArgumentException("Modbus RTU point read Unit ID must be from 1 to 247.");

        return new PointReadPlan(
            requestTimeoutMilliseconds,
            point,
            scale,
            offset,
            effectiveTransform,
            nativeWordSwap);
    }

    private static async Task<DriverPointReadSample> ReadSampleAsync(
        IModbusMasterTransport transport,
        PointReadPlan plan,
        DriverPointReadTestRequest request,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            DriverPointReadRawRepresentation raw;
            DriverPointReadValue decoded;
            DriverPointReadValue engineering;

            if (plan.Point.Area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput)
            {
                var values = await transport.ReadBitsAsync(plan.Point.UnitId, plan.Point.Area, plan.Point.Address, 1, cancellationToken);
                var value = values[0];
                raw = new DriverPointReadRawRepresentation(
                    "bits",
                    value ? "01" : "00",
                    new[] { value ? "1" : "0" },
                    BuildMetadata(plan, transport));
                decoded = new DriverPointReadValue(nameof(Boolean), value);
                engineering = new DriverPointReadValue(request.DataType.ToString(), value, request.EngineeringUnit);
            }
            else
            {
                var registers = await transport.ReadRegistersAsync(
                    plan.Point.UnitId,
                    plan.Point.Area,
                    plan.Point.Address,
                    checked((ushort)plan.Point.RegisterCount),
                    cancellationToken);
                raw = new DriverPointReadRawRepresentation(
                    "registers",
                    Convert.ToHexString(ToWireBytes(registers)),
                    registers.Select(value => $"0x{value:X4}").ToArray(),
                    BuildMetadata(plan, transport));

                var transformedBytes = ApplyPhysicalTransform(ToWireBytes(registers), plan.EffectiveTransform);
                var decodedValue = DecodeCanonical(plan.Point.ValueType, transformedBytes);
                if (plan.Point.AddressSelector is not null)
                {
                    var transformedWord = BinaryPrimitives.ReadUInt16BigEndian(transformedBytes);
                    decoded = new DriverPointReadValue(nameof(UInt16), transformedWord);
                    var selected = (transformedWord & (1 << plan.Point.AddressSelector.Index)) != 0;
                    engineering = new DriverPointReadValue(request.DataType.ToString(), selected, request.EngineeringUnit);
                }
                else
                {
                    decoded = new DriverPointReadValue(plan.Point.ValueType.ToString(), decodedValue);
                    engineering = new DriverPointReadValue(
                        request.DataType.ToString(),
                        ApplyEngineering(decodedValue, plan.Point.ValueType, request.DataType, plan.Scale, plan.Offset),
                        request.EngineeringUnit);
                }
            }

            return new DriverPointReadSample(
                DriverPointReadTestStatus.Good,
                DateTimeOffset.UtcNow,
                null,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                TagQuality.Good,
                raw,
                decoded,
                engineering,
                plan.EffectiveTransform);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ModbusRtuCrcException ex)
        {
            return FailureSample(
                DriverPointReadTestStatus.Bad,
                TagQuality.BadCommunication,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                plan.EffectiveTransform,
                "MODBUS_RTU_CRC_FAILED",
                SanitizeError(ex));
        }
        catch (ModbusProtocolException ex)
        {
            var noData = ex.ExceptionCode is 0x02 or 0x03;
            return FailureSample(
                noData ? DriverPointReadTestStatus.NoData : DriverPointReadTestStatus.Bad,
                TagQuality.BadDevice,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                plan.EffectiveTransform,
                "MODBUS_DEVICE_REJECTED_POINT",
                SanitizeError(ex));
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            return FailureSample(
                DriverPointReadTestStatus.Bad,
                TagQuality.BadCommunication,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                plan.EffectiveTransform,
                "MODBUS_POINT_READ_COMMUNICATION_FAILED",
                SanitizeError(ex));
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or FormatException)
        {
            return FailureSample(
                DriverPointReadTestStatus.Bad,
                TagQuality.BadConfiguration,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                plan.EffectiveTransform,
                "MODBUS_POINT_READ_DECODE_FAILED",
                SanitizeError(ex));
        }
    }

    protected static int ParseInt(IReadOnlyDictionary<string, string> settings, string key, int fallback, int minimum, int maximum)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum && parsed <= maximum)
            return parsed;
        throw new ArgumentException($"Modbus setting '{key}' must be from {minimum} to {maximum}.");
    }

    protected static string Required(IReadOnlyDictionary<string, string> settings, string key) =>
        Get(settings, key)?.Trim() is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Required Modbus setting '{key}' is missing.");

    protected static string? Get(IReadOnlyDictionary<string, string> settings, string key)
    {
        if (settings.TryGetValue(key, out var exact)) return exact;
        foreach (var pair in settings)
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        return null;
    }

    protected static string SanitizeText(string value)
    {
        var candidate = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\0', ' ').Trim();
        return candidate.Length <= 256 ? candidate : candidate[..256];
    }

    private static DriverPointReadSample FailureSample(
        DriverPointReadTestStatus status,
        TagQuality quality,
        double? latencyMilliseconds,
        TagPhysicalValueTransform effectiveTransform,
        string code,
        string message) =>
        new(status, DateTimeOffset.UtcNow, null, latencyMilliseconds, quality,
            EffectiveValueTransform: effectiveTransform,
            Issues: new[] { new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, message) });

    private static IReadOnlyDictionary<string, string> BuildMetadata(PointReadPlan plan, IModbusMasterTransport transport)
    {
        var metadata = new Dictionary<string, string>(transport.ProtocolDetails, StringComparer.Ordinal)
        {
            ["area"] = plan.Point.Area.ToString(),
            ["address"] = plan.Point.Address.ToString(CultureInfo.InvariantCulture),
            ["unitId"] = plan.Point.UnitId.ToString(CultureInfo.InvariantCulture),
            ["valueType"] = plan.Point.ValueType.ToString(),
            ["scale"] = plan.Scale.ToString("R", CultureInfo.InvariantCulture),
            ["offset"] = plan.Offset.ToString("R", CultureInfo.InvariantCulture),
            ["byteSwap"] = plan.EffectiveTransform.ByteSwap ? "true" : "false",
            ["wordSwap"] = plan.EffectiveTransform.WordSwap ? "true" : "false"
        };
        if (plan.Point.AddressSelector is not null)
            metadata["bitSelector"] = plan.Point.AddressSelector.Index.ToString(CultureInfo.InvariantCulture);
        return metadata;
    }

    private static byte[] ToWireBytes(IReadOnlyList<ushort> registers)
    {
        var bytes = new byte[registers.Count * 2];
        for (var index = 0; index < registers.Count; index++)
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(index * 2, 2), registers[index]);
        return bytes;
    }

    private static byte[] ApplyPhysicalTransform(ReadOnlySpan<byte> wireBytes, TagPhysicalValueTransform transform)
    {
        var bytes = wireBytes.ToArray();
        if (transform.ByteSwap)
            for (var index = 0; index + 1 < bytes.Length; index += 2)
                (bytes[index], bytes[index + 1]) = (bytes[index + 1], bytes[index]);

        if (transform.WordSwap && bytes.Length >= 4)
        {
            var copy = bytes.ToArray();
            var wordCount = bytes.Length / 2;
            for (var word = 0; word < wordCount; word++)
            {
                var sourceWord = wordCount - 1 - word;
                bytes[word * 2] = copy[sourceWord * 2];
                bytes[word * 2 + 1] = copy[sourceWord * 2 + 1];
            }
        }
        return bytes;
    }

    private static object DecodeCanonical(ModbusValueType valueType, ReadOnlySpan<byte> bytes) =>
        valueType switch
        {
            ModbusValueType.Boolean => BinaryPrimitives.ReadUInt16BigEndian(bytes) != 0,
            ModbusValueType.Int16 => BinaryPrimitives.ReadInt16BigEndian(bytes),
            ModbusValueType.UInt16 => BinaryPrimitives.ReadUInt16BigEndian(bytes),
            ModbusValueType.Int32 => BinaryPrimitives.ReadInt32BigEndian(bytes),
            ModbusValueType.UInt32 => BinaryPrimitives.ReadUInt32BigEndian(bytes),
            ModbusValueType.Float32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(bytes)),
            ModbusValueType.Int64 => BinaryPrimitives.ReadInt64BigEndian(bytes),
            ModbusValueType.UInt64 => BinaryPrimitives.ReadUInt64BigEndian(bytes),
            ModbusValueType.Float64 => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(bytes)),
            _ => throw new ArgumentOutOfRangeException(nameof(valueType))
        };

    private static object ApplyEngineering(object decoded, ModbusValueType valueType, TagDataType targetType, double scale, double offset)
    {
        if (valueType == ModbusValueType.Boolean)
            return Convert.ToBoolean(decoded, CultureInfo.InvariantCulture);
        var engineering = Convert.ToDouble(decoded, CultureInfo.InvariantCulture) * scale + offset;
        if (!double.IsFinite(engineering))
            throw new OverflowException("Modbus scale/offset produced a non-finite Engineering value.");
        return targetType switch
        {
            TagDataType.Int16 => checked((short)Math.Round(engineering)),
            TagDataType.Int32 => checked((int)Math.Round(engineering)),
            TagDataType.Int64 => checked((long)Math.Round(engineering)),
            TagDataType.Float => checked((float)engineering),
            TagDataType.Double => engineering,
            _ => throw new InvalidOperationException($"Modbus numeric point cannot produce canonical TAG type '{targetType}'.")
        };
    }

    private static ModbusValueType ParseValueType(DriverPointReadTestRequest request)
    {
        var raw = Get(request.Binding.EffectiveSettings, "modbus.valueType");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var normalized = raw.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
            if (Enum.TryParse<ModbusValueType>(normalized, true, out var parsed) && Enum.IsDefined(parsed))
                return parsed;
            throw new ArgumentException($"Unsupported Modbus value type '{raw}'.");
        }
        return request.DataType switch
        {
            TagDataType.Boolean => ModbusValueType.Boolean,
            TagDataType.Int16 => ModbusValueType.Int16,
            TagDataType.Int32 => ModbusValueType.Int32,
            TagDataType.Int64 => ModbusValueType.Int64,
            TagDataType.Float => ModbusValueType.Float32,
            TagDataType.Double => ModbusValueType.Float64,
            _ => throw new ArgumentException($"Canonical TAG type '{request.DataType}' requires an explicit supported Modbus value type.")
        };
    }

    private static ModbusWordOrder ParseWordOrder(IReadOnlyDictionary<string, string> settings)
    {
        var raw = Get(settings, "modbus.wordOrder");
        if (string.IsNullOrWhiteSpace(raw)) return ModbusWordOrder.HighWordFirst;
        var normalized = raw.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<ModbusWordOrder>(normalized, true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;
        throw new ArgumentException($"Unsupported Modbus word order '{raw}'. Use HighWordFirst or LowWordFirst.");
    }

    private static double ParseDouble(IReadOnlyDictionary<string, string> settings, string key, double fallback, bool nonZero)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            double.IsFinite(parsed) && (!nonZero || parsed != 0d))
            return parsed;
        throw new ArgumentException($"Modbus setting '{key}' must be a finite{(nonZero ? " non-zero" : string.Empty)} number.");
    }

    private static string SanitizeError(Exception error)
    {
        var message = error.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return message.Length <= 512 ? message : message[..512];
    }

    private static DriverPointReadTestResult ConfigurationFailure(DriverPointReadTestRequest request, string code, string message)
    {
        var sample = new DriverPointReadSample(
            DriverPointReadTestStatus.Bad, DateTimeOffset.UtcNow, null, null, TagQuality.BadConfiguration,
            Issues: new[] { new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, message) });
        return BuildResult(request, null, new[] { sample }, sample.Issues);
    }

    private static DriverPointReadTestResult BuildResult(
        DriverPointReadTestRequest request,
        string? endpoint,
        IReadOnlyCollection<DriverPointReadSample> samples,
        IReadOnlyCollection<DriverEngineeringIssue>? issues)
    {
        var latencies = samples.Where(sample => sample.LatencyMilliseconds.HasValue).Select(sample => sample.LatencyMilliseconds!.Value).ToArray();
        var good = samples.Count(sample => sample.Status == DriverPointReadTestStatus.Good);
        var uncertain = samples.Count(sample => sample.Status == DriverPointReadTestStatus.IntermittentOrUncertain);
        var bad = samples.Count(sample => sample.Status == DriverPointReadTestStatus.Bad);
        var noData = samples.Count(sample => sample.Status == DriverPointReadTestStatus.NoData);
        var status = samples.Count == 0 ? DriverPointReadTestStatus.NoData
            : good == samples.Count ? DriverPointReadTestStatus.Good
            : noData == samples.Count ? DriverPointReadTestStatus.NoData
            : good > 0 || uncertain > 0 || noData > 0 ? DriverPointReadTestStatus.IntermittentOrUncertain
            : DriverPointReadTestStatus.Bad;

        return new DriverPointReadTestResult(
            status,
            endpoint,
            request.Binding.PortableAddress,
            new DriverPointReadSampleSummary(
                request.SampleCount, samples.Count, good, uncertain, bad, noData,
                latencies.Length == 0 ? null : latencies.Min(),
                latencies.Length == 0 ? null : latencies.Average(),
                latencies.Length == 0 ? null : latencies.Max()),
            samples.ToArray(),
            issues);
    }

    private sealed record PointReadPlan(
        int RequestTimeoutMilliseconds,
        ModbusPoint Point,
        double Scale,
        double Offset,
        TagPhysicalValueTransform EffectiveTransform,
        bool LegacyWordOrderNormalized);
}

public sealed class ModbusTcpPointReadTester : ModbusPointReadTesterBase
{
    public override CommunicationDriverTypeDescriptor Descriptor => ModbusTcpDriverDescriptorProvider.SharedDescriptor;
    protected override string DriverType => ModbusTcpDriverDescriptorProvider.DriverTypeId;

    protected override IModbusMasterTransport CreateTransport(
        DriverPointReadTestRequest request,
        byte unitId,
        int requestTimeoutMilliseconds)
    {
        var host = Required(request.Context.Settings, "host");
        var port = ParseInt(request.Context.Settings, "port", 502, 1, 65535);
        return new ModbusTcpTransport(host, port, TimeSpan.FromMilliseconds(requestTimeoutMilliseconds));
    }

    protected override string BuildSanitizedEndpoint(DriverPointReadTestRequest request)
    {
        var host = Required(request.Context.Settings, "host").Trim();
        var port = ParseInt(request.Context.Settings, "port", 502, 1, 65535);
        if (Uri.TryCreate($"tcp://{host}", UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            var safeHost = uri.Host.Contains(':', StringComparison.Ordinal) ? $"[{uri.Host}]" : uri.Host;
            return $"{safeHost}:{port}";
        }
        var candidate = SanitizeText(host);
        var at = candidate.LastIndexOf('@');
        if (at >= 0 && at < candidate.Length - 1) candidate = candidate[(at + 1)..];
        return $"{candidate}:{port}";
    }
}

public sealed class ModbusRtuPointReadTester :
    ModbusPointReadTesterBase,
    ICommunicationDriverConnectionTester
{
    private readonly HostSerialBusCoordinator _coordinator;

    public ModbusRtuPointReadTester(HostSerialBusCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public override CommunicationDriverTypeDescriptor Descriptor => ModbusRtuDriverDescriptorProvider.SharedDescriptor;
    protected override string DriverType => ModbusRtuDriverDescriptorProvider.DriverTypeId;

    protected override IModbusMasterTransport CreateTransport(
        DriverPointReadTestRequest request,
        byte unitId,
        int requestTimeoutMilliseconds)
    {
        var line = ParseLineSettings(request.Context.Settings);
        return new ModbusRtuTransport(
            _coordinator,
            line,
            $"engineering:point-read:{request.Context.DataSourceKey}:{Guid.NewGuid():N}",
            new[] { unitId },
            TimeSpan.FromMilliseconds(requestTimeoutMilliseconds),
            engineeringLease: true);
    }

    protected override string BuildSanitizedEndpoint(DriverPointReadTestRequest request) =>
        SanitizeText(Required(request.Context.Settings, "serialPort"));

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(context.DriverType, DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Connection test expected Driver '{DriverType}'.", nameof(context));

        HostSerialLineSettings line;
        byte unitId;
        int timeoutMilliseconds;
        try
        {
            line = ParseLineSettings(context.Settings);
            unitId = checked((byte)ParseInt(context.Settings, "unitId", 1, 0, 255));
            timeoutMilliseconds = ParseInt(context.Settings, "requestTimeoutMilliseconds", 3000, 50, 60000);
        }
        catch (ArgumentException ex)
        {
            return new DriverConnectionTestResult(false, null, null, Issues: new[]
            {
                new DriverEngineeringIssue("MODBUS_RTU_CONNECTION_CONFIGURATION_INVALID", DriverEngineeringIssueSeverity.Error,
                    SanitizeText(ex.Message), ex.ParamName)
            });
        }

        await using var transport = new ModbusRtuTransport(
            _coordinator,
            line,
            $"engineering:connection-test:{context.DataSourceKey}:{Guid.NewGuid():N}",
            new[] { unitId },
            TimeSpan.FromMilliseconds(Math.Min(timeoutMilliseconds, 5000)),
            engineeringLease: true);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await transport.ReadDeviceIdentificationAsync(unitId, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return new DriverConnectionTestResult(
                true,
                SanitizeText(line.PortName),
                line.PhysicalPortKey,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["transport"] = "Modbus RTU",
                    ["protocolResponsive"] = "true",
                    ["deviceIdentificationSupported"] = "true",
                    ["deviceIdentificationObjectCount"] = response.ObjectCount.ToString(CultureInfo.InvariantCulture),
                    ["elapsedMilliseconds"] = stopwatch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture),
                    ["baudRate"] = line.BaudRate.ToString(CultureInfo.InvariantCulture),
                    ["dataBits"] = line.DataBits.ToString(CultureInfo.InvariantCulture),
                    ["parity"] = line.Parity.ToString(),
                    ["stopBits"] = line.StopBits.ToString()
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ModbusProtocolException ex)
        {
            stopwatch.Stop();
            return new DriverConnectionTestResult(true, SanitizeText(line.PortName), line.PhysicalPortKey,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["transport"] = "Modbus RTU",
                    ["protocolResponsive"] = "true",
                    ["deviceIdentificationSupported"] = "false",
                    ["protocolExceptionCode"] = ex.ExceptionCode.ToString(CultureInfo.InvariantCulture),
                    ["elapsedMilliseconds"] = stopwatch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)
                },
                new[] { new DriverEngineeringIssue("MODBUS_RTU_DEVICE_IDENTIFICATION_UNSUPPORTED", DriverEngineeringIssueSeverity.Warning,
                    "The endpoint responded to Modbus, but does not support the read-only device-identification request.", "unitId") });
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            return ConnectionFailure(line, "MODBUS_RTU_CONNECTION_TIMEOUT", "Modbus RTU protocol request timed out.", "unitId");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            stopwatch.Stop();
            return ConnectionFailure(line, "MODBUS_RTU_CONNECTION_TEST_FAILED", SanitizeText(ex.Message), "serialPort");
        }
    }

    private static DriverConnectionTestResult ConnectionFailure(
        HostSerialLineSettings line, string code, string message, string fieldKey) =>
        new(false, SanitizeText(line.PortName), null, Issues: new[]
        {
            new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, message, fieldKey)
        });

    internal static HostSerialLineSettings ParseLineSettings(IReadOnlyDictionary<string, string> settings)
    {
        var port = Required(settings, "serialPort");
        var baud = ParseInt(settings, "baudRate", 9600, 300, 4_000_000);
        var dataBits = ParseInt(settings, "dataBits", 8, 5, 8);
        var parity = ParseEnum(settings, "parity", HostSerialParity.None);
        var stopBits = ParseEnum(settings, "stopBits", HostSerialStopBits.One);
        var line = new HostSerialLineSettings(port.Trim(), baud, dataBits, parity, stopBits);
        line.Validate();
        return line;
    }

    private static T ParseEnum<T>(IReadOnlyDictionary<string, string> settings, string key, T fallback) where T : struct, Enum
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        var normalized = raw.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<T>(normalized, true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        throw new ArgumentException($"Unsupported serial setting '{key}' value '{raw}'.");
    }
}
