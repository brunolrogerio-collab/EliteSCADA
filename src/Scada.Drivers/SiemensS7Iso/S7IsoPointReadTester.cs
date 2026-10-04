using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.SiemensS7Iso;

/// <summary>
/// Engineering-only bounded S7 ISO point read over the existing RFC1006/S7
/// transport. The canonical shared physical transform is materialized into the
/// existing S7 value-order codec exactly once.
/// </summary>
public sealed class S7IsoPointReadTester :
    ICommunicationDriverDescriptorProvider,
    ICommunicationDriverPointReadTester
{
    private readonly S7IsoEngineeringAdapter _descriptorProvider = new();

    public CommunicationDriverTypeDescriptor Descriptor => _descriptorProvider.Descriptor;

    public async ValueTask<DriverPointReadTestResult> TestPointReadAsync(
        DriverPointReadTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        if (!string.Equals(
                request.Context.DriverType,
                Descriptor.DriverType,
                StringComparison.OrdinalIgnoreCase))
        {
            return ConfigurationFailure(
                request,
                "S7_POINT_READ_DRIVER_MISMATCH",
                $"Point read expected Driver '{Descriptor.DriverType}'.");
        }

        if (request.AddressSelector is not null)
        {
            return ConfigurationFailure(
                request,
                "S7_POINT_READ_SELECTOR_UNSUPPORTED",
                "S7 ISO absolute bit addressing belongs to the canonical S7 binding; an additional TAG AddressSelector is not supported by this point-read path.");
        }

        if (!string.Equals(
                request.Binding.SchemaId,
                S7IsoCommunicationBindingProjection.SchemaId,
                StringComparison.Ordinal) ||
            request.Binding.SchemaVersion != S7IsoCommunicationBindingProjection.SchemaVersion)
        {
            return ConfigurationFailure(
                request,
                "S7_POINT_READ_BINDING_SCHEMA_INVALID",
                $"S7 point read requires binding schema '{S7IsoCommunicationBindingProjection.SchemaId}' v{S7IsoCommunicationBindingProjection.SchemaVersion}.");
        }

        if (!S7IsoEngineeringAdapter.TryCreateOptions(
                request.Context.Settings,
                out var options,
                out var optionIssues))
        {
            return ConfigurationFailure(
                request,
                "S7_POINT_READ_CONFIGURATION_INVALID",
                string.Join(" ", optionIssues.Select(issue => issue.Message)));
        }

        var transform = request.Binding.ValueTransform ?? new TagPhysicalValueTransform();
        try
        {
            transform.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return ConfigurationFailure(
                request,
                "S7_POINT_READ_TRANSFORM_INVALID",
                SanitizeError(ex));
        }

        if (!S7IsoCommunicationBindingProjection.TryMaterializeCanonical(
                request.Binding.PortableAddress,
                request.Binding.EffectiveSettings,
                transform.ByteSwap,
                transform.WordSwap,
                out var materializedBinding,
                out var bindingError))
        {
            return ConfigurationFailure(
                request,
                "S7_POINT_READ_BINDING_INVALID",
                bindingError ?? "S7 canonical binding could not be materialized.");
        }

        S7IsoPoint point;
        try
        {
            var tag = TagDefinition.Create(
                "Point Read Test",
                "__engineering.pointRead.s7",
                request.DataType,
                source: request.Context.DataSourceKey,
                engineeringUnit: request.EngineeringUnit,
                readOnly: true,
                communicationBinding: request.Binding);
            point = materializedBinding!.ToPoint(tag);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return ConfigurationFailure(
                request,
                "S7_POINT_READ_BINDING_INVALID",
                SanitizeError(ex));
        }

        var configuredOptions = options!;
        var pointReadTimeout = TimeSpan.FromMilliseconds(request.TimeoutMilliseconds);
        var validOptions = new S7IsoConnectionOptions(
            configuredOptions.Host,
            configuredOptions.CpuFamily,
            configuredOptions.ConnectionMode,
            configuredOptions.Rack,
            configuredOptions.Slot,
            configuredOptions.ConnectionRole,
            configuredOptions.SourceTsap,
            configuredOptions.DestinationTsap,
            configuredOptions.Port,
            configuredOptions.ConnectTimeout <= pointReadTimeout ? configuredOptions.ConnectTimeout : pointReadTimeout,
            configuredOptions.RequestTimeout <= pointReadTimeout ? configuredOptions.RequestTimeout : pointReadTimeout,
            configuredOptions.ReconnectDelay,
            configuredOptions.RequestedPduSize,
            configuredOptions.WriteEnabled);
        await using var transport = new S7IsoTransport(validOptions);
        var samples = new List<DriverPointReadSample>(request.SampleCount);

        for (var sampleIndex = 0; sampleIndex < request.SampleCount; sampleIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sampleIndex > 0 && request.SampleIntervalMilliseconds > 0)
                await Task.Delay(request.SampleIntervalMilliseconds, cancellationToken);

            samples.Add(await ReadSampleAsync(
                transport,
                point,
                request,
                transform,
                cancellationToken));
        }

        return BuildResult(
            request,
            validOptions.SanitizedEndpoint,
            samples,
            null);
    }

    private static async Task<DriverPointReadSample> ReadSampleAsync(
        S7IsoTransport transport,
        S7IsoPoint point,
        DriverPointReadTestRequest request,
        TagPhysicalValueTransform effectiveTransform,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await transport.ReadDetailedAsync(new[] { point }, cancellationToken);
            if (result.CommunicationFailures.TryGetValue(point, out var communicationFailure))
            {
                return FailureSample(
                    DriverPointReadTestStatus.Bad,
                    TagQuality.BadCommunication,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    effectiveTransform,
                    "S7_POINT_READ_COMMUNICATION_FAILED",
                    SanitizeMessage(communicationFailure));
            }

            if (result.ConfigurationFailures.TryGetValue(point, out var configurationFailure))
            {
                return FailureSample(
                    DriverPointReadTestStatus.Bad,
                    TagQuality.BadConfiguration,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    effectiveTransform,
                    "S7_POINT_READ_CONFIGURATION_INVALID",
                    SanitizeMessage(configurationFailure));
            }

            var item = result.Items.SingleOrDefault();
            if (item is null)
            {
                return FailureSample(
                    DriverPointReadTestStatus.NoData,
                    TagQuality.Unavailable,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    effectiveTransform,
                    "S7_POINT_READ_NO_DATA",
                    "The S7 peer returned no item for the requested point.");
            }

            if (!item.Succeeded)
            {
                var noData = item.ReturnCode is 0x05 or 0x0A;
                return FailureSample(
                    noData ? DriverPointReadTestStatus.NoData : DriverPointReadTestStatus.Bad,
                    TagQuality.BadDevice,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    effectiveTransform,
                    $"S7_POINT_READ_RETURN_{item.ReturnCode:X2}",
                    $"S7 point read failed with return code 0x{item.ReturnCode:X2} ({S7IsoProtocol.DescribeReturnCode(item.ReturnCode)}).");
            }

            var bytes = item.Data ?? Array.Empty<byte>();
            if (bytes.Length == 0)
            {
                return FailureSample(
                    DriverPointReadTestStatus.NoData,
                    TagQuality.Unavailable,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    effectiveTransform,
                    "S7_POINT_READ_EMPTY_PAYLOAD",
                    "The S7 peer returned an empty point payload.");
            }

            var decoded = S7IsoValueCodec.Decode(point, bytes);
            var raw = new DriverPointReadRawRepresentation(
                "bytes",
                Convert.ToHexString(bytes),
                bytes.Select(value => $"0x{value:X2}").ToArray(),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["area"] = point.Area.ToString(),
                    ["dbNumber"] = point.DbNumber.ToString(CultureInfo.InvariantCulture),
                    ["byteOffset"] = point.ByteOffset.ToString(CultureInfo.InvariantCulture),
                    ["bitOffset"] = point.BitOffset.ToString(CultureInfo.InvariantCulture),
                    ["valueType"] = point.ValueType.ToString(),
                    ["byteSwap"] = effectiveTransform.ByteSwap ? "true" : "false",
                    ["wordSwap"] = effectiveTransform.WordSwap ? "true" : "false"
                });

            return new DriverPointReadSample(
                DriverPointReadTestStatus.Good,
                DateTimeOffset.UtcNow,
                null,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                TagQuality.Good,
                raw,
                new DriverPointReadValue(point.ValueType.ToString(), decoded),
                new DriverPointReadValue(
                    request.DataType.ToString(),
                    decoded,
                    request.EngineeringUnit),
                effectiveTransform);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or SocketException or ObjectDisposedException)
        {
            return FailureSample(
                DriverPointReadTestStatus.Bad,
                TagQuality.BadCommunication,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                effectiveTransform,
                "S7_POINT_READ_COMMUNICATION_FAILED",
                SanitizeError(ex));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            return FailureSample(
                DriverPointReadTestStatus.Bad,
                TagQuality.BadConfiguration,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                effectiveTransform,
                "S7_POINT_READ_DECODE_FAILED",
                SanitizeError(ex));
        }
    }

    private static DriverPointReadSample FailureSample(
        DriverPointReadTestStatus status,
        TagQuality quality,
        double? latencyMilliseconds,
        TagPhysicalValueTransform effectiveTransform,
        string code,
        string message) =>
        new(
            status,
            DateTimeOffset.UtcNow,
            null,
            latencyMilliseconds,
            quality,
            EffectiveValueTransform: effectiveTransform,
            Issues: new[]
            {
                new DriverEngineeringIssue(
                    code,
                    DriverEngineeringIssueSeverity.Error,
                    message)
            });

    private static DriverPointReadTestResult ConfigurationFailure(
        DriverPointReadTestRequest request,
        string code,
        string message)
    {
        var sample = new DriverPointReadSample(
            DriverPointReadTestStatus.Bad,
            DateTimeOffset.UtcNow,
            null,
            null,
            TagQuality.BadConfiguration,
            EffectiveValueTransform: request.Binding.ValueTransform,
            Issues: new[]
            {
                new DriverEngineeringIssue(
                    code,
                    DriverEngineeringIssueSeverity.Error,
                    SanitizeMessage(message))
            });
        return BuildResult(request, null, new[] { sample }, sample.Issues);
    }

    private static DriverPointReadTestResult BuildResult(
        DriverPointReadTestRequest request,
        string? endpoint,
        IReadOnlyCollection<DriverPointReadSample> samples,
        IReadOnlyCollection<DriverEngineeringIssue>? issues)
    {
        var latencies = samples
            .Where(sample => sample.LatencyMilliseconds.HasValue)
            .Select(sample => sample.LatencyMilliseconds!.Value)
            .ToArray();
        var good = samples.Count(sample => sample.Status == DriverPointReadTestStatus.Good);
        var uncertain = samples.Count(sample => sample.Status == DriverPointReadTestStatus.IntermittentOrUncertain);
        var bad = samples.Count(sample => sample.Status == DriverPointReadTestStatus.Bad);
        var noData = samples.Count(sample => sample.Status == DriverPointReadTestStatus.NoData);
        var status = samples.Count == 0
            ? DriverPointReadTestStatus.NoData
            : good == samples.Count
                ? DriverPointReadTestStatus.Good
                : noData == samples.Count
                    ? DriverPointReadTestStatus.NoData
                    : good > 0 || uncertain > 0 || noData > 0
                        ? DriverPointReadTestStatus.IntermittentOrUncertain
                        : DriverPointReadTestStatus.Bad;

        return new DriverPointReadTestResult(
            status,
            endpoint,
            request.Binding.PortableAddress,
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
            samples.ToArray(),
            issues);
    }

    private static string SanitizeError(Exception error) => SanitizeMessage(error.Message);

    private static string SanitizeMessage(string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }
}
