using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Zigbee2Mqtt;

public sealed class Zigbee2MqttEngineeringProvider :
    ICommunicationDriverConnectionTester,
    ICommunicationDriverDiscoverySource,
    ICommunicationDriverPointReadTester
{
    public const string SelectedIeeeAddressesParameter = "selectedIeeeAddresses";
    private readonly string _projectKey;
    private readonly string _dataSourceKey;
    private readonly Guid _dataSourceId;
    private readonly ICommunicationDriverProtectedMaterialResolver _protectedMaterialResolver;
    private readonly Func<IMqttClientTransport> _transportFactory;
    private readonly Zigbee2MqttDriverDescriptorProvider _descriptorProvider = new();

    public Zigbee2MqttEngineeringProvider(
        string? projectKey,
        string dataSourceKey,
        Guid dataSourceId,
        ICommunicationDriverProtectedMaterialResolver protectedMaterialResolver,
        Func<IMqttClientTransport>? transportFactory = null)
    {
        if (string.IsNullOrWhiteSpace(dataSourceKey)) throw new ArgumentException("Data Source key is required.", nameof(dataSourceKey));
        if (dataSourceId == Guid.Empty) throw new ArgumentException("DataSourceId is required.", nameof(dataSourceId));
        _projectKey = string.IsNullOrWhiteSpace(projectKey) ? "engineering-draft" : projectKey.Trim();
        _dataSourceKey = dataSourceKey.Trim();
        _dataSourceId = dataSourceId;
        _protectedMaterialResolver = protectedMaterialResolver ?? throw new ArgumentNullException(nameof(protectedMaterialResolver));
        _transportFactory = transportFactory ?? (() => new MqttNetClientTransport());
    }

    public CommunicationDriverTypeDescriptor Descriptor => _descriptorProvider.Descriptor;

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            EnsureContext(context);
            var settings = Zigbee2MqttConnectionSettings.Parse(context.DataSourceKey, _dataSourceId, context.Settings);
            await using var transport = CreateTransport();
            using var credentials = await ResolveCredentialsAsync(context, cancellationToken).ConfigureAwait(false);
            await transport.ConnectAsync(settings.Mqtt, credentials, cancellationToken).ConfigureAwait(false);
            var inventory = await ReadInventoryAsync(transport, settings, cancellationToken).ConfigureAwait(false);
            return new DriverConnectionTestResult(
                inventory.BridgeState == "online",
                SanitizedEndpoint(settings),
                "zigbee2mqtt.bridge",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["bridgeState"] = inventory.BridgeState ?? "unknown",
                    ["deviceCount"] = inventory.Inventory.Devices.Count.ToString(CultureInfo.InvariantCulture),
                    ["supportedDeviceCount"] = inventory.Inventory.Devices.Count(device => device.Supported).ToString(CultureInfo.InvariantCulture),
                    ["zigbee2mqttVersion"] = inventory.Inventory.Version ?? string.Empty,
                    ["inventoryRetained"] = inventory.InventoryRetained.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()
                });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return FailureConnection("Z2M_CONNECTION_TEST_FAILED", ex);
        }
    }

    public async IAsyncEnumerable<DriverDiscoveryCandidate> DiscoverAsync(
        DriverDiscoveryRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Context is null)
        {
            yield return FailureCandidate("Z2M_DISCOVERY_CONTEXT_REQUIRED", "Zigbee2MQTT discovery requires a Data Source context.");
            yield break;
        }

        Zigbee2MqttConnectionSettings settings;
        try
        {
            EnsureContext(request.Context);
            settings = Zigbee2MqttConnectionSettings.Parse(request.Context.DataSourceKey, _dataSourceId, request.Context.Settings);
        }
        catch (Exception ex)
        {
            yield return FailureCandidate("Z2M_DISCOVERY_CONFIGURATION", SafeFailure(ex));
            yield break;
        }

        InventoryProbe probe;
        try
        {
            await using var transport = CreateTransport();
            using var credentials = await ResolveCredentialsAsync(request.Context, cancellationToken).ConfigureAwait(false);
            await transport.ConnectAsync(settings.Mqtt, credentials, cancellationToken).ConfigureAwait(false);
            probe = await ReadInventoryAsync(transport, settings, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            yield return FailureCandidate("Z2M_DISCOVERY_FAILED", SafeFailure(ex), SanitizedEndpoint(settings));
            yield break;
        }

        HashSet<string> selected;
        try { selected = ParseSelection(request.Parameters); }
        catch (Exception ex)
        {
            yield return FailureCandidate("Z2M_SELECTION_INVALID", SafeFailure(ex), SanitizedEndpoint(settings));
            yield break;
        }
        var maximum = request.MaximumResults is > 0
            ? Math.Min(request.MaximumResults.Value, settings.MaximumDevices)
            : settings.MaximumDevices;
        if (selected.Count > maximum)
        {
            yield return FailureCandidate(
                "Z2M_SELECTION_LIMIT",
                $"The selection contains {selected.Count} devices, above the configured discovery result limit of {maximum}.",
                SanitizedEndpoint(settings));
            yield break;
        }
        var devices = selected.Count == 0
            ? probe.Inventory.Devices.Take(maximum).ToArray()
            : probe.Inventory.Devices.ToArray();
        var issues = new List<DriverEngineeringIssue>();
        if (probe.Inventory.Devices.Count > devices.Length)
        {
            issues.Add(new DriverEngineeringIssue(
                "Z2M_INVENTORY_PARTIAL",
                DriverEngineeringIssueSeverity.Warning,
                $"Discovery is capped at {devices.Length} of {probe.Inventory.Devices.Count} devices."));
        }

        if (selected.Count == 0)
        {
            foreach (var device in devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return CreateInventoryCandidate(device, settings, probe, materialization: null, issues);
            }
            yield break;
        }

        var byIeee = devices.ToDictionary(device => device.IeeeAddress, StringComparer.Ordinal);
        foreach (var unknown in selected.OrderBy(ieee => ieee, StringComparer.Ordinal).Where(ieee => !byIeee.ContainsKey(ieee)))
        {
            yield return FailureCandidate(
                "Z2M_SELECTED_DEVICE_NOT_FOUND",
                $"Selected IEEE address '{unknown}' is not present in the current bridge inventory.",
                SanitizedEndpoint(settings));
        }

        foreach (var ieee in selected.OrderBy(ieee => ieee, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!byIeee.TryGetValue(ieee, out var device)) continue;
            var materialization = device.Supported
                ? Zigbee2MqttExposeMapper.BuildMaterialization(_dataSourceId, request.Context.DataSourceKey, device)
                : null;
            yield return CreateInventoryCandidate(device, settings, probe, materialization, issues);
        }
    }

    public async ValueTask<DriverPointReadTestResult> TestPointReadAsync(
        DriverPointReadTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var binding = request.Binding;
        var address = binding.PortableAddress;
        Zigbee2MqttConnectionSettings? failureSettings = null;
        try
        {
            EnsureContext(request.Context);
            var settings = Zigbee2MqttConnectionSettings.Parse(request.Context.DataSourceKey, _dataSourceId, request.Context.Settings);
            failureSettings = settings;
            var point = CreatePoint(binding, request.DataType, request.EngineeringUnit, expectedDataSourceId: _dataSourceId);
            if (!point.Gettable)
                return PointReadNoData(address, settings, "Z2M_POINT_READ_ACCESS_DENIED", "The selected expose does not declare get/read access.");

            await using var transport = CreateTransport();
            using var credentials = await ResolveCredentialsAsync(request.Context, cancellationToken).ConfigureAwait(false);
            await transport.ConnectAsync(settings.Mqtt, credentials, cancellationToken).ConfigureAwait(false);
            var probe = await ReadInventoryAsync(transport, settings, cancellationToken).ConfigureAwait(false);
            var device = RequireCurrentDevice(probe.Inventory, point);
            var expose = RequireCurrentExpose(device, point);
            ValidateExposeAgainstPoint(expose, point, request.DataType, request.EngineeringUnit);

            var stateTopic = StateTopic(settings, device.FriendlyName);
            var availabilityTopic = AvailabilityTopic(settings, device.FriendlyName);
            await transport.SubscribeAsync(
                [new MqttSubscription(stateTopic, MqttQosLevel.AtLeastOnce), new MqttSubscription(availabilityTopic, MqttQosLevel.AtLeastOnce)],
                cancellationToken).ConfigureAwait(false);

            var samples = new List<DriverPointReadSample>();
            var latencies = new List<double>();
            for (var index = 0; index < request.SampleCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var started = Stopwatch.GetTimestamp();
                var requestIssuedAt = DateTimeOffset.UtcNow;
                await transport.PublishAsync(
                    new MqttPublishRequest(
                        GetTopic(settings, device.FriendlyName),
                        BuildGetPayload(point.Property),
                        MqttQosLevel.AtLeastOnce,
                        Retain: false),
                    cancellationToken).ConfigureAwait(false);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(Math.Min(request.TimeoutMilliseconds, (int)settings.RequestTimeout.TotalMilliseconds));
                var received = false;
                while (!received)
                {
                    MqttTransportMessage message;
                    try { message = await transport.ReceiveAsync(timeout.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    if (message.Topic != stateTopic || message.Retained || message.ReceivedAtUtc < requestIssuedAt) continue;
                    using var document = JsonDocument.Parse(message.Payload);
                    if (!Zigbee2MqttExposeMapper.TryDecodeValue(point, document.RootElement, out var value, out var error))
                    {
                        samples.Add(BadPointSample(message.ReceivedAtUtc, Stopwatch.GetElapsedTime(started).TotalMilliseconds, error));
                        received = true;
                        continue;
                    }
                    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    latencies.Add(elapsed);
                    samples.Add(new DriverPointReadSample(
                        DriverPointReadTestStatus.Good,
                        message.ReceivedAtUtc,
                        SourceTimestampUtc: null,
                        elapsed,
                        TagQuality.Good,
                        Raw: new DriverPointReadRawRepresentation("mqtt-z2m-json", Metadata: new Dictionary<string, string> { ["retained"] = "false" }),
                        Decoded: new DriverPointReadValue(request.DataType.ToString(), value, request.EngineeringUnit),
                        Engineering: new DriverPointReadValue(request.DataType.ToString(), value, request.EngineeringUnit)));
                    received = true;
                }

                if (!received)
                    samples.Add(new DriverPointReadSample(
                        DriverPointReadTestStatus.NoData,
                        DateTimeOffset.UtcNow,
                        null,
                        Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                        TagQuality.Uncertain,
                        Issues: [new DriverEngineeringIssue("Z2M_POINT_READ_TIMEOUT", DriverEngineeringIssueSeverity.Warning, "No non-retained state report confirmed the requested PointRead before timeout.")]));
                if (index + 1 < request.SampleCount && request.SampleIntervalMilliseconds > 0)
                    await Task.Delay(request.SampleIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            var good = samples.Count(sample => sample.Status == DriverPointReadTestStatus.Good);
            var noData = samples.Count(sample => sample.Status == DriverPointReadTestStatus.NoData);
            var bad = samples.Count - good - noData;
            var status = good == samples.Count ? DriverPointReadTestStatus.Good
                : good == 0 && bad == 0 ? DriverPointReadTestStatus.NoData
                : good == 0 ? DriverPointReadTestStatus.Bad
                : DriverPointReadTestStatus.IntermittentOrUncertain;
            return new DriverPointReadTestResult(
                status,
                SanitizedEndpoint(settings),
                address,
                new DriverPointReadSampleSummary(
                    request.SampleCount,
                    samples.Count,
                    good,
                    samples.Count(sample => sample.Quality == TagQuality.Uncertain),
                    bad,
                    noData,
                    latencies.Count == 0 ? null : latencies.Min(),
                    latencies.Count == 0 ? null : latencies.Average(),
                    latencies.Count == 0 ? null : latencies.Max()),
                samples);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return PointReadNoData(address, failureSettings, "Z2M_POINT_READ_FAILED", SafeFailure(ex), request.SampleCount);
        }
    }

    internal async ValueTask<MqttResolvedCredentials> ResolveCredentialsAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken)
    {
        var username = Get(context.Settings, "username");
        if (!TryGet(context.SecretReferences, Zigbee2MqttContract.PasswordSecretReferenceKey, out var reference) || string.IsNullOrWhiteSpace(reference))
        {
            if (username is not null) return new MqttResolvedCredentials(username);
            return MqttResolvedCredentials.None;
        }

        var protectedRequest = new CommunicationDriverProtectedMaterialRequest(
            _projectKey,
            context.DataSourceKey,
            Zigbee2MqttContract.DriverType,
            Zigbee2MqttContract.PasswordPurpose,
            reference);
        protectedRequest.Validate();
        await using var lease = await _protectedMaterialResolver.ResolveAsync(protectedRequest, cancellationToken).ConfigureAwait(false);
        if (lease.Material.IsEmpty) throw new InvalidOperationException("Protected MQTT password reference resolved to empty material.");
        if (string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException("MQTT password reference requires a username.");
        return new MqttResolvedCredentials(username, lease.Material);
    }

    internal static async ValueTask<InventoryProbe> ReadInventoryAsync(
        IMqttClientTransport transport,
        Zigbee2MqttConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        await transport.SubscribeAsync(
            [
                new MqttSubscription(settings.BridgeStateTopic, MqttQosLevel.AtLeastOnce),
                new MqttSubscription(settings.BridgeDevicesTopic, MqttQosLevel.AtLeastOnce),
                new MqttSubscription(settings.BridgeInfoTopic, MqttQosLevel.AtLeastOnce)
            ],
            cancellationToken).ConfigureAwait(false);

        byte[]? devices = null;
        string? bridgeState = null;
        string? bridgeInfo = null;
        var inventoryRetained = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.RequestTimeout);
        while (devices is null || bridgeState is null)
        {
            MqttTransportMessage message;
            try { message = await transport.ReceiveAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Zigbee2MQTT did not provide bridge state and device inventory before timeout.");
            }
            if (message.Payload.Length > settings.Mqtt.MaximumInboundPayloadBytes)
                throw new FormatException("Zigbee2MQTT bridge payload exceeds the configured byte limit.");
            if (message.Topic == settings.BridgeStateTopic)
                bridgeState = DecodeUtf8(message.Payload);
            else if (message.Topic == settings.BridgeDevicesTopic)
            {
                devices = message.Payload.ToArray();
                inventoryRetained = message.Retained;
            }
            else if (message.Topic == settings.BridgeInfoTopic)
                bridgeInfo = DecodeUtf8(message.Payload);
        }

        var inventory = Zigbee2MqttExposeMapper.ParseInventory(devices, settings, bridgeState, bridgeInfo);
        if (!string.Equals(inventory.BridgeState, "online", StringComparison.Ordinal))
            throw new IOException("Zigbee2MQTT bridge is not online.");
        return new InventoryProbe(inventory, inventoryRetained);
    }

    public static Zigbee2MqttPoint CreatePoint(
        CommunicationTagBinding binding,
        TagDataType dataType,
        string? engineeringUnit,
        bool readOnly = true,
        Guid? expectedDataSourceId = null)
    {
        binding.Validate();
        if (!string.Equals(binding.SchemaId, Zigbee2MqttContract.TagBindingSchemaId, StringComparison.Ordinal) ||
            binding.SchemaVersion != Zigbee2MqttContract.TagBindingSchemaVersion || binding.ValueTransform is not null)
            throw new ArgumentException("Zigbee2MQTT TAG binding schema/version/transform is unsupported.", nameof(binding));
        if (!Zigbee2MqttIdentity.TryParsePortableAddress(binding.PortableAddress, out var ieee, out var endpoint, out var property))
            throw new ArgumentException("Zigbee2MQTT TAG address must be a canonical IEEE/endpoint/property binding.", nameof(binding));
        var values = binding.EffectiveSettings;
        if (!TryGet(values, "z2m.dataSourceId", out var dataSourceIdText) ||
            !Guid.TryParse(dataSourceIdText, out var bindingDataSourceId) || bindingDataSourceId == Guid.Empty)
            throw new ArgumentException("Zigbee2MQTT binding must include its canonical DataSourceId identity.", nameof(binding));
        if (expectedDataSourceId.HasValue && bindingDataSourceId != expectedDataSourceId.Value)
            throw new ArgumentException("Zigbee2MQTT binding DataSourceId does not match the canonical Data Source.", nameof(binding));
        if (!TryGetInt(values, "z2m.access", out var access) || (access & 1) == 0 || (access & ~7) != 0)
            throw new ArgumentException("Zigbee2MQTT TAG binding must declare valid readable expose access.", nameof(binding));
        if (!TryGet(values, "z2m.exposeType", out var exposeType))
            throw new ArgumentException("Zigbee2MQTT TAG binding must declare its exposed type.", nameof(binding));
        var valueKind = exposeType.Equals("binary", StringComparison.OrdinalIgnoreCase)
            ? Zigbee2MqttValueKind.Boolean
            : exposeType.Equals("numeric", StringComparison.OrdinalIgnoreCase)
                ? Zigbee2MqttValueKind.Double
                : throw new ArgumentException("Zigbee2MQTT TAG expose type is not supported.", nameof(binding));
        if (valueKind == Zigbee2MqttValueKind.Boolean && dataType != TagDataType.Boolean ||
            valueKind == Zigbee2MqttValueKind.Double && dataType != TagDataType.Double)
            throw new ArgumentException("Zigbee2MQTT exposed type does not match the canonical Boolean/Double TAG type.", nameof(dataType));
        values.TryGetValue("z2m.unit", out var unit);
        if (!string.Equals(unit ?? string.Empty, engineeringUnit ?? string.Empty, StringComparison.Ordinal))
            throw new ArgumentException("Zigbee2MQTT TAG engineering unit must exactly match its declared expose unit.", nameof(engineeringUnit));
        double? minimum = ParseOptionalDouble(values, "z2m.minimum");
        double? maximum = ParseOptionalDouble(values, "z2m.maximum");
        double? step = ParseOptionalDouble(values, "z2m.step");
        values.TryGetValue("z2m.valueOn", out var valueOn);
        values.TryGetValue("z2m.valueOff", out var valueOff);
        values.TryGetValue("z2m.capability", out var capability);
        var writableContract = (access & 2) != 0 &&
                               string.Equals(Get(values, "z2m.reportConfirmation"), Zigbee2MqttContract.ReadbackConfirmation, StringComparison.Ordinal);
        if (!readOnly && !writableContract)
            throw new ArgumentException("Writable Zigbee2MQTT TAG requires set access and an explicit non-retained report confirmation policy.", nameof(binding));
        var tag = TagDefinition.Create(
            property,
            binding.PortableAddress,
            dataType,
            source: null,
            engineeringUnit: engineeringUnit,
            description: null,
            readOnly: readOnly);
        var point = new Zigbee2MqttPoint(tag, ieee, endpoint, property, valueKind, access, unit, minimum, maximum, step, valueOn, valueOff, capability ?? string.Empty);
        point.Validate();
        return point;
    }

    internal static Zigbee2MqttDevice RequireCurrentDevice(Zigbee2MqttInventory inventory, Zigbee2MqttPoint point)
    {
        var device = inventory.Devices.SingleOrDefault(item => item.IeeeAddress == point.IeeeAddress)
            ?? throw new InvalidOperationException($"IEEE device '{point.IeeeAddress}' is no longer in the Zigbee2MQTT inventory.");
        if (!device.Supported)
            throw new InvalidOperationException($"IEEE device '{point.IeeeAddress}' is no longer declared supported by Zigbee2MQTT.");
        return device;
    }

    internal static Zigbee2MqttExpose RequireCurrentExpose(Zigbee2MqttDevice device, Zigbee2MqttPoint point) =>
        device.Exposes.SingleOrDefault(expose => expose.Property == point.Property && expose.Endpoint == point.Endpoint)
        ?? throw new InvalidOperationException($"Expose '{point.Property}' is no longer present on IEEE device '{point.IeeeAddress}'.");

    internal static void ValidateExposeAgainstPoint(
        Zigbee2MqttExpose expose,
        Zigbee2MqttPoint point,
        TagDataType dataType,
        string? engineeringUnit)
    {
        if (expose.ValueKind != point.ValueKind || expose.Access != point.Access || expose.Unit != point.Unit ||
            expose.Minimum != point.Minimum || expose.Maximum != point.Maximum || expose.Step != point.Step ||
            expose.ValueOnJson != point.ValueOnJson || expose.ValueOffJson != point.ValueOffJson ||
            !string.Equals(expose.CapabilityKind, point.CapabilityKind, StringComparison.Ordinal) ||
            (point.ValueKind == Zigbee2MqttValueKind.Boolean && dataType != TagDataType.Boolean) ||
            (point.ValueKind == Zigbee2MqttValueKind.Double && dataType != TagDataType.Double) ||
            !string.Equals(point.Unit ?? string.Empty, engineeringUnit ?? string.Empty, StringComparison.Ordinal))
            throw new InvalidOperationException("Current Zigbee2MQTT expose metadata no longer matches the canonical TAG binding.");
    }

    internal static string StateTopic(Zigbee2MqttConnectionSettings settings, string friendlyName) => $"{settings.BaseTopic}/{friendlyName}";
    internal static string AvailabilityTopic(Zigbee2MqttConnectionSettings settings, string friendlyName) => $"{settings.BaseTopic}/{friendlyName}/availability";
    internal static string SetTopic(Zigbee2MqttConnectionSettings settings, string friendlyName) => $"{settings.BaseTopic}/{friendlyName}/set";
    internal static string GetTopic(Zigbee2MqttConnectionSettings settings, string friendlyName) => $"{settings.BaseTopic}/{friendlyName}/get";

    internal static ReadOnlyMemory<byte> BuildGetPayload(string property) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, string> { [property] = string.Empty }));

    internal static ReadOnlyMemory<byte> BuildSetPayload(string property, string rawJsonValue)
    {
        using var value = JsonDocument.Parse(rawJsonValue);
        return Encoding.UTF8.GetBytes($"{{{JsonSerializer.Serialize(property)}:{value.RootElement.GetRawText()}}}");
    }

    internal static string SanitizedEndpoint(Zigbee2MqttConnectionSettings settings) =>
        $"{(settings.Mqtt.UseTls ? "mqtts" : "mqtt")}://{settings.Mqtt.Host}:{settings.Mqtt.Port}/{settings.BaseTopic}";

    private DriverDiscoveryCandidate CreateInventoryCandidate(
        Zigbee2MqttDevice device,
        Zigbee2MqttConnectionSettings settings,
        InventoryProbe probe,
        DriverMaterializationCandidate? materialization,
        IReadOnlyCollection<DriverEngineeringIssue> aggregateIssues)
    {
        var candidateIssues = (device.Issues ?? Array.Empty<DriverEngineeringIssue>()).Concat(aggregateIssues).ToArray();
        return new DriverDiscoveryCandidate(
            $"z2m-{device.IeeeAddress}",
            Zigbee2MqttIdentity.StableDeviceIdentity(_dataSourceId, device.IeeeAddress),
            device.FriendlyName,
            SanitizedEndpoint(settings),
            SuggestedSettings: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["baseTopic"] = settings.BaseTopic,
                ["host"] = settings.Mqtt.Host,
                ["port"] = settings.Mqtt.Port.ToString(CultureInfo.InvariantCulture),
                ["tls"] = settings.Mqtt.UseTls.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()
            },
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ieeeAddress"] = device.IeeeAddress,
                ["friendlyName"] = device.FriendlyName,
                ["supported"] = device.Supported.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
                ["exposeCount"] = device.Exposes.Count.ToString(CultureInfo.InvariantCulture),
                ["inventoryRetained"] = probe.InventoryRetained.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
                ["bridgeState"] = probe.Inventory.BridgeState ?? "unknown",
                ["zigbee2mqttVersion"] = probe.Inventory.Version ?? string.Empty,
                ["selected"] = (materialization is not null).ToString(CultureInfo.InvariantCulture).ToLowerInvariant()
            },
            Issues: candidateIssues,
            Materialization: materialization);
    }

    private static HashSet<string> ParseSelection(IReadOnlyDictionary<string, string>? parameters)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (parameters is null) return result;
        var raw = Get(parameters, SelectedIeeeAddressesParameter) ?? Get(parameters, "selectedDeviceIds");
        if (string.IsNullOrWhiteSpace(raw)) return result;
        if (raw.Length > 64 * 1024)
            throw new ArgumentException("Selected Zigbee2MQTT device list exceeds the configured discovery input limit.");
        foreach (var value in raw.Split([',', ';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var ieeeInput = value.StartsWith("z2m-", StringComparison.Ordinal) ? value[4..] : value;
            if (!Zigbee2MqttIdentity.TryNormalizeIeee(ieeeInput, out var ieee))
                throw new ArgumentException("Selected Zigbee2MQTT device identifiers must be IEEE addresses.");
            result.Add(ieee);
            if (result.Count > 4096)
                throw new ArgumentException("Selected Zigbee2MQTT device list exceeds the maximum inventory device count.");
        }
        return result;
    }

    private static string DecodeUtf8(ReadOnlyMemory<byte> payload)
    {
        if (payload.IsEmpty) return string.Empty;
        return Encoding.UTF8.GetString(payload.Span);
    }

    private static DriverConnectionTestResult FailureConnection(string code, Exception exception) =>
        new(false, null, null, Issues: [new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, SafeFailure(exception))]);

    private static DriverDiscoveryCandidate FailureCandidate(string code, string message, string? endpoint = null) =>
        new($"z2m-failure-{code.ToLowerInvariant()}", "unresolved", "Zigbee2MQTT bridge", endpoint,
            Issues: [new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, message)]);

    private static DriverPointReadTestResult PointReadNoData(
        string address,
        Zigbee2MqttConnectionSettings? settings,
        string code,
        string message,
        int requestedSamples = 1) =>
        new(
            DriverPointReadTestStatus.NoData,
            settings is null ? null : SanitizedEndpoint(settings),
            address,
            new DriverPointReadSampleSummary(requestedSamples, 0, 0, requestedSamples, 0, requestedSamples),
            [],
            [new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, message)]);

    private static DriverPointReadSample BadPointSample(DateTimeOffset observedAt, double latency, string? error) =>
        new(DriverPointReadTestStatus.Bad, observedAt, null, latency, TagQuality.BadDevice,
            Issues: [new DriverEngineeringIssue("Z2M_POINT_READ_VALUE_INVALID", DriverEngineeringIssueSeverity.Error, error ?? "Reported value is outside the expose contract.")]);

    private static string SafeFailure(Exception ex) =>
        ex is TimeoutException ? "The MQTT broker or Zigbee2MQTT bridge did not respond before the configured timeout." :
        ex is JsonException or FormatException ? "Zigbee2MQTT returned a malformed or unsupported inventory/state payload." :
        ex is ArgumentException or InvalidOperationException ? "Zigbee2MQTT settings, credentials, or selected expose do not satisfy the declared contract." :
        "The MQTT broker or Zigbee2MQTT bridge operation failed; verify the sanitized endpoint and broker/bridge diagnostics.";

    private void EnsureContext(DriverEngineeringDataSourceContext context)
    {
        if (!string.Equals(context.DriverType, Zigbee2MqttContract.DriverType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(context.DataSourceKey, _dataSourceKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Zigbee2MQTT Engineering context does not match this Data Source registration.");
        if (context.Settings.Keys.Any(key => key.Equals("password", StringComparison.OrdinalIgnoreCase) ||
                                             key.Equals("passwordValue", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("MQTT passwords must be supplied as protected SecretReferences, not plaintext Data Source settings.");
    }

    private IMqttClientTransport CreateTransport() =>
        _transportFactory() ?? throw new InvalidOperationException("MQTT transport factory returned null.");

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : values.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

    private static bool TryGet(IReadOnlyDictionary<string, string> values, string key, out string value)
    {
        if (values.TryGetValue(key, out var direct))
        {
            value = direct;
            return true;
        }
        var pair = values.FirstOrDefault(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (pair.Key is not null)
        {
            value = pair.Value;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static bool TryGetInt(IReadOnlyDictionary<string, string> values, string key, out int result)
    {
        if (TryGet(values, key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            return true;
        result = 0;
        return false;
    }

    private static double? ParseOptionalDouble(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!TryGet(values, key, out var raw)) return null;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new ArgumentException($"Zigbee2MQTT TAG setting '{key}' must be finite numeric text.");
        return value;
    }

}

internal sealed record InventoryProbe(Zigbee2MqttInventory Inventory, bool InventoryRetained);
