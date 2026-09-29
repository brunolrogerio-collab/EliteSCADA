using System.Diagnostics;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Engineering-only canonical OPC UA point read. It reuses the existing
/// protected Foundation session authority and creates no Runtime registration,
/// subscription, write or persistent TAG state.
/// </summary>
public sealed class OpcUaPointReadTester :
    ICommunicationDriverDescriptorProvider,
    ICommunicationDriverPointReadTester
{
    private readonly IOpcUaRuntimeSecurityMaterialProvider _securityMaterialProvider;

    public OpcUaPointReadTester(IOpcUaRuntimeSecurityMaterialProvider securityMaterialProvider)
    {
        _securityMaterialProvider = securityMaterialProvider ??
            throw new ArgumentNullException(nameof(securityMaterialProvider));
    }

    public CommunicationDriverTypeDescriptor Descriptor => OpcUaDriverDescriptorProvider.Definition;

    public async ValueTask<DriverPointReadTestResult> TestPointReadAsync(
        DriverPointReadTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        if (!string.Equals(request.Context.DriverType, Descriptor.DriverType, StringComparison.OrdinalIgnoreCase))
            return ConfigurationFailure(request, "OPCUA_POINT_READ_DRIVER_MISMATCH", $"Point read expected Driver '{Descriptor.DriverType}'.");

        var schemaId = Descriptor.TagBindingSchemaId ?? Descriptor.ConfigurationSchema.SchemaId;
        var schemaVersion = Descriptor.TagBindingSchemaVersion ?? Descriptor.ConfigurationSchema.SchemaVersion;
        if (!string.Equals(request.Binding.SchemaId, schemaId, StringComparison.Ordinal) ||
            request.Binding.SchemaVersion != schemaVersion)
        {
            return ConfigurationFailure(
                request,
                "OPCUA_POINT_READ_BINDING_SCHEMA_INVALID",
                $"OPC UA point read requires binding schema '{schemaId}' v{schemaVersion}.");
        }

        var transform = request.Binding.ValueTransform ?? new TagPhysicalValueTransform();
        try
        {
            transform.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return ConfigurationFailure(request, "OPCUA_POINT_READ_TRANSFORM_INVALID", SafeMessage(ex.Message));
        }

        if (!transform.IsIdentity)
        {
            return ConfigurationFailure(
                request,
                "OPCUA_POINT_READ_PHYSICAL_TRANSFORM_UNSUPPORTED",
                "OPC UA returns canonical typed values; Byte Swap / Word Swap is not a valid OPC UA point-read transform.");
        }

        if (request.AddressSelector is not null)
        {
            return ConfigurationFailure(
                request,
                "OPCUA_POINT_READ_SELECTOR_UNSUPPORTED",
                "OPC UA canonical point read does not support a TAG bit selector.");
        }

        OpcUaNodeIdentity identity;
        OpcUaRuntimeConnectionOptions options;
        OpcUaRuntimeBinding runtimeBinding;
        try
        {
            identity = OpcUaNodeIdentity.ParsePortableAddress(request.Binding.PortableAddress);
            options = OpcUaRuntimeDriverComposer.ParseConnectionOptions(request.Context);

            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OpcUaRuntimeBinding.NodeIdMetadataKey] = identity.NodeId
            };
            if (!string.IsNullOrWhiteSpace(identity.NamespaceUri))
                metadata[OpcUaRuntimeBinding.NamespaceUriMetadataKey] = identity.NamespaceUri;

            var tag = TagDefinition.Create(
                "Point Read Test",
                "__engineering.pointRead.opcua",
                request.DataType,
                source: request.Context.DataSourceKey,
                engineeringUnit: request.EngineeringUnit,
                readOnly: true,
                metadata: metadata,
                communicationBinding: request.Binding);
            runtimeBinding = OpcUaRuntimeBinding.FromTag(tag);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or FormatException)
        {
            return ConfigurationFailure(
                request,
                "OPCUA_POINT_READ_CONFIGURATION_INVALID",
                SafeMessage(ex.Message));
        }

        var endpoint = SanitizeEndpoint(options.EndpointUrl);
        var samples = new List<DriverPointReadSample>(request.SampleCount);

        try
        {
            var factory = new OpcUaFoundationRuntimeSessionFactory(options, _securityMaterialProvider);
            await using var session = await factory
                .ConnectAsync(new[] { runtimeBinding }, cancellationToken)
                .ConfigureAwait(false);

            for (var sampleIndex = 0; sampleIndex < request.SampleCount; sampleIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sampleIndex > 0 && request.SampleIntervalMilliseconds > 0)
                    await Task.Delay(request.SampleIntervalMilliseconds, cancellationToken).ConfigureAwait(false);

                samples.Add(await ReadSampleAsync(
                    session,
                    runtimeBinding,
                    request,
                    transform,
                    cancellationToken).ConfigureAwait(false));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            samples.Add(FailureSample(
                DriverPointReadTestStatus.Bad,
                TagQuality.BadCommunication,
                null,
                transform,
                "OPCUA_POINT_READ_SESSION_FAILED",
                "The protected OPC UA Engineering session failed before a point value could be returned."));
        }

        return BuildResult(request, endpoint, samples);
    }

    private static async Task<DriverPointReadSample> ReadSampleAsync(
        IOpcUaRuntimeSession session,
        OpcUaRuntimeBinding binding,
        DriverPointReadTestRequest request,
        TagPhysicalValueTransform effectiveTransform,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var observed = await session.ReadAsync(binding, cancellationToken).ConfigureAwait(false);
            var latency = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var timestamp = observed.SourceTimestamp ?? observed.ServerTimestamp;

            if (observed.Value is null)
            {
                return FailureSample(
                    DriverPointReadTestStatus.NoData,
                    observed.Quality,
                    latency,
                    effectiveTransform,
                    "OPCUA_POINT_READ_NO_DATA",
                    "The OPC UA node returned no readable value.",
                    timestamp);
            }

            var status = observed.Quality switch
            {
                TagQuality.Good => DriverPointReadTestStatus.Good,
                TagQuality.Uncertain => DriverPointReadTestStatus.IntermittentOrUncertain,
                TagQuality.Unavailable => DriverPointReadTestStatus.NoData,
                _ => DriverPointReadTestStatus.Bad
            };

            var value = NormalizeValue(observed.Value, request.DataType);
            IReadOnlyCollection<DriverEngineeringIssue>? issues = status == DriverPointReadTestStatus.Good
                ? null
                : new[]
                {
                    new DriverEngineeringIssue(
                        "OPCUA_POINT_READ_QUALITY_NOT_GOOD",
                        status == DriverPointReadTestStatus.IntermittentOrUncertain
                            ? DriverEngineeringIssueSeverity.Warning
                            : DriverEngineeringIssueSeverity.Error,
                        $"OPC UA returned quality '{observed.Quality}'.")
                };

            return new DriverPointReadSample(
                status,
                DateTimeOffset.UtcNow,
                timestamp,
                latency,
                observed.Quality,
                Raw: null,
                Decoded: new DriverPointReadValue(request.DataType.ToString(), value),
                Engineering: new DriverPointReadValue(request.DataType.ToString(), value, request.EngineeringUnit),
                EffectiveValueTransform: effectiveTransform,
                Issues: issues);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return FailureSample(
                DriverPointReadTestStatus.Bad,
                TagQuality.BadCommunication,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                effectiveTransform,
                "OPCUA_POINT_READ_FAILED",
                "The OPC UA read failed before a safe point value could be returned.");
        }
    }

    private static object NormalizeValue(object value, TagDataType dataType)
    {
        try
        {
            return dataType switch
            {
                TagDataType.Boolean => Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture),
                TagDataType.Int16 => Convert.ToInt16(value, System.Globalization.CultureInfo.InvariantCulture),
                TagDataType.Int32 => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture),
                TagDataType.Int64 => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture),
                TagDataType.Float => Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture),
                TagDataType.Double => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
                TagDataType.String => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                TagDataType.DateTime => value switch
                {
                    DateTimeOffset dto => dto,
                    DateTime dt => dt,
                    _ => Convert.ToDateTime(value, System.Globalization.CultureInfo.InvariantCulture)
                },
                TagDataType.Enum => value is Enum e ? Convert.ToInt64(e, System.Globalization.CultureInfo.InvariantCulture) : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException($"Unsupported canonical TAG type '{dataType}'.")
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidOperationException(
                $"OPC UA value cannot be represented as canonical TAG type '{dataType}'.",
                ex);
        }
    }

    private static DriverPointReadSample FailureSample(
        DriverPointReadTestStatus status,
        TagQuality quality,
        double? latencyMilliseconds,
        TagPhysicalValueTransform effectiveTransform,
        string code,
        string message,
        DateTimeOffset? sourceTimestamp = null) =>
        new(
            status,
            DateTimeOffset.UtcNow,
            sourceTimestamp,
            latencyMilliseconds,
            quality,
            EffectiveValueTransform: effectiveTransform,
            Issues: new[]
            {
                new DriverEngineeringIssue(
                    code,
                    status == DriverPointReadTestStatus.IntermittentOrUncertain
                        ? DriverEngineeringIssueSeverity.Warning
                        : DriverEngineeringIssueSeverity.Error,
                    message)
            });

    private static DriverPointReadTestResult ConfigurationFailure(
        DriverPointReadTestRequest request,
        string code,
        string message)
    {
        var transform = request.Binding.ValueTransform ?? new TagPhysicalValueTransform();
        var sample = new DriverPointReadSample(
            DriverPointReadTestStatus.Bad,
            DateTimeOffset.UtcNow,
            null,
            null,
            TagQuality.BadConfiguration,
            EffectiveValueTransform: transform,
            Issues: new[]
            {
                new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, SafeMessage(message))
            });
        return BuildResult(request, null, new[] { sample });
    }

    private static DriverPointReadTestResult BuildResult(
        DriverPointReadTestRequest request,
        string? endpoint,
        IReadOnlyCollection<DriverPointReadSample> samples)
    {
        var latencies = samples.Where(x => x.LatencyMilliseconds.HasValue)
            .Select(x => x.LatencyMilliseconds!.Value)
            .ToArray();
        var good = samples.Count(x => x.Status == DriverPointReadTestStatus.Good);
        var uncertain = samples.Count(x => x.Status == DriverPointReadTestStatus.IntermittentOrUncertain);
        var bad = samples.Count(x => x.Status == DriverPointReadTestStatus.Bad);
        var noData = samples.Count(x => x.Status == DriverPointReadTestStatus.NoData);
        var status = samples.Count == 0
            ? DriverPointReadTestStatus.NoData
            : good == samples.Count
                ? DriverPointReadTestStatus.Good
                : noData == samples.Count
                    ? DriverPointReadTestStatus.NoData
                    : good > 0 || uncertain > 0 || noData > 0
                        ? DriverPointReadTestStatus.IntermittentOrUncertain
                        : DriverPointReadTestStatus.Bad;

        var issues = samples.SelectMany(x => x.Issues ?? Array.Empty<DriverEngineeringIssue>())
            .GroupBy(x => (x.Code, x.Message))
            .Select(x => x.First())
            .ToArray();

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
            issues.Length == 0 ? null : issues);
    }

    private static string SanitizeEndpoint(string endpoint)
    {
        var trimmed = endpoint.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
            return trimmed;
        return new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri.ToString();
    }

    private static string SafeMessage(string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }
}
