using System.Globalization;
using System.IO.BACnet;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Bacnet;

public sealed class BacnetEngineeringProvider :
    ICommunicationDriverConnectionTester,
    ICommunicationDriverDiscoverySource,
    ICommunicationDriverBrowser,
    ICommunicationDriverReconciler
{
    private readonly IBacnetSessionFactory _sessions;

    public BacnetEngineeringProvider(IBacnetSessionFactory? sessions = null)
        => _sessions = sessions ?? new SystemIoBacnetSessionFactory();

    public CommunicationDriverTypeDescriptor Descriptor => BacnetDriverDescriptor.Instance;

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deviceInstance = RequireDeviceInstance(context.Settings);
            await using var session = _sessions.Create(BuildOptions(context.Settings));
            await session.StartAsync(cancellationToken).ConfigureAwait(false);
            var device = await session.ResolveDeviceAsync(deviceInstance, cancellationToken).ConfigureAwait(false);
            var name = await TryReadTextAsync(session, new BacnetBinding(
                deviceInstance,
                (uint)BacnetObjectTypes.OBJECT_DEVICE,
                deviceInstance,
                (uint)BacnetPropertyIds.PROP_OBJECT_NAME), cancellationToken).ConfigureAwait(false);

            return new DriverConnectionTestResult(
                true,
                device.SanitizedEndpoint,
                $"device:{device.DeviceInstance}",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["deviceInstance"] = device.DeviceInstance.ToString(CultureInfo.InvariantCulture),
                    ["vendorId"] = device.VendorId.ToString(CultureInfo.InvariantCulture),
                    ["maximumApdu"] = device.MaximumApdu.ToString(CultureInfo.InvariantCulture),
                    ["segmentation"] = device.Segmentation.ToString(),
                    ["objectName"] = name ?? string.Empty
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DriverConnectionTestResult(
                false,
                null,
                null,
                Issues: new[] { Issue("BACNET_CONNECTION_TEST_FAILED", DriverEngineeringIssueSeverity.Error, Sanitize(ex.Message)) });
        }
    }

    public async IAsyncEnumerable<DriverDiscoveryCandidate> DiscoverAsync(
        DriverDiscoveryRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = request.Context?.Settings ?? new Dictionary<string, string>();
        IBacnetSession? session = null;
        try
        {
            session = _sessions.Create(BuildOptions(settings, deviceRequired: false));
            await session.StartAsync(cancellationToken).ConfigureAwait(false);
            await foreach (var device in session.DiscoverAsync(request.MaximumResults, cancellationToken).ConfigureAwait(false))
            {
                var name = await TryReadTextAsync(session, new BacnetBinding(
                    device.DeviceInstance,
                    (uint)BacnetObjectTypes.OBJECT_DEVICE,
                    device.DeviceInstance,
                    (uint)BacnetPropertyIds.PROP_OBJECT_NAME), cancellationToken).ConfigureAwait(false);
                yield return new DriverDiscoveryCandidate(
                    $"bacnet-device-{device.DeviceInstance}",
                    $"device={device.DeviceInstance}",
                    string.IsNullOrWhiteSpace(name) ? $"BACnet Device {device.DeviceInstance}" : name,
                    device.SanitizedEndpoint,
                    SuggestedSettings: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["deviceInstance"] = device.DeviceInstance.ToString(CultureInfo.InvariantCulture)
                    },
                    Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["vendorId"] = device.VendorId.ToString(CultureInfo.InvariantCulture),
                        ["maximumApdu"] = device.MaximumApdu.ToString(CultureInfo.InvariantCulture),
                        ["segmentation"] = device.Segmentation.ToString()
                    });
            }
        }
        finally
        {
            if (session is not null) await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask<DriverBrowsePage> BrowseAsync(
        DriverBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deviceInstance = RequireDeviceInstance(request.Context.Settings);
            await using var session = _sessions.Create(BuildOptions(request.Context.Settings));
            await session.StartAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(request.ParentNodeId))
            {
                var device = await session.ResolveDeviceAsync(deviceInstance, cancellationToken).ConfigureAwait(false);
                var name = await TryReadTextAsync(session, new BacnetBinding(
                    deviceInstance,
                    (uint)BacnetObjectTypes.OBJECT_DEVICE,
                    deviceInstance,
                    (uint)BacnetPropertyIds.PROP_OBJECT_NAME), cancellationToken).ConfigureAwait(false);
                return new DriverBrowsePage(new[]
                {
                    new DriverBrowseNode(
                        $"device:{deviceInstance}",
                        $"device={deviceInstance}",
                        string.IsNullOrWhiteSpace(name) ? $"BACnet Device {deviceInstance}" : name,
                        IsContainer: true,
                        IsReadable: true,
                        IsWritable: false,
                        Metadata: new Dictionary<string, string> { ["endpoint"] = device.SanitizedEndpoint })
                });
            }

            if (TryParseDeviceNode(request.ParentNodeId, out var parentDevice))
            {
                if (parentDevice != deviceInstance)
                    return ErrorPage("BACNET_BROWSE_DEVICE_MISMATCH", "Browse node belongs to a different BACnet Device Instance.");
                return await BrowseObjectsAsync(session, deviceInstance, request, cancellationToken).ConfigureAwait(false);
            }

            if (TryParseObjectNode(request.ParentNodeId, out var objectType, out var objectInstance))
                return await BrowseObjectPropertiesAsync(session, deviceInstance, objectType, objectInstance, cancellationToken).ConfigureAwait(false);

            return ErrorPage("BACNET_BROWSE_NODE_INVALID", $"Unknown BACnet browse node '{request.ParentNodeId}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorPage("BACNET_BROWSE_FAILED", Sanitize(ex.Message));
        }
    }

    public async IAsyncEnumerable<DriverReconcileResult> ReconcileAsync(
        DriverReconcileRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var session = _sessions.Create(BuildOptions(request.Context.Settings));
        await session.StartAsync(cancellationToken).ConfigureAwait(false);
        foreach (var address in request.PortableAddresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!BacnetBinding.TryParse(address, out var binding, out var error) || binding is null)
            {
                yield return new DriverReconcileResult(
                    address,
                    DriverReconcileStatus.Error,
                    Issues: new[] { Issue("BACNET_BINDING_INVALID", DriverEngineeringIssueSeverity.Error, error ?? "Invalid BACnet binding.") });
                continue;
            }
            try
            {
                var result = await session.ReadAsync(binding, cancellationToken).ConfigureAwait(false);
                var observedType = result.Values.Count == 0 ? null : GuessTagDataType(result.Values[0], binding.ObjectType);
                yield return new DriverReconcileResult(
                    address,
                    DriverReconcileStatus.Unchanged,
                    binding.StableIdentity,
                    binding.PortableAddress,
                    observedType,
                    IsReadable: true,
                    IsWritable: null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                yield return new DriverReconcileResult(
                    address,
                    DriverReconcileStatus.Missing,
                    Issues: new[] { Issue("BACNET_POINT_UNAVAILABLE", DriverEngineeringIssueSeverity.Warning, Sanitize(ex.Message)) });
            }
        }
    }

    private static async Task<DriverBrowsePage> BrowseObjectsAsync(
        IBacnetSession session,
        uint deviceInstance,
        DriverBrowseRequest request,
        CancellationToken cancellationToken)
    {
        var objectList = await session.ReadAsync(new BacnetBinding(
            deviceInstance,
            (uint)BacnetObjectTypes.OBJECT_DEVICE,
            deviceInstance,
            (uint)BacnetPropertyIds.PROP_OBJECT_LIST), cancellationToken).ConfigureAwait(false);
        var objects = objectList.Values
            .Select(x => x.Value)
            .OfType<BacnetObjectId>()
            .Distinct()
            .OrderBy(x => (uint)x.Type)
            .ThenBy(x => x.Instance)
            .ToArray();

        var offset = 0;
        if (!string.IsNullOrWhiteSpace(request.ContinuationToken) &&
            !int.TryParse(request.ContinuationToken, NumberStyles.None, CultureInfo.InvariantCulture, out offset))
            return ErrorPage("BACNET_CONTINUATION_INVALID", "BACnet continuation token is invalid.");
        var pageSize = Math.Clamp(request.PageSize ?? 100, 1, 500);
        var selected = objects.Skip(offset).Take(pageSize).ToArray();
        var nodes = new List<DriverBrowseNode>(selected.Length);
        foreach (var objectId in selected)
        {
            var name = await TryReadTextAsync(session, new BacnetBinding(
                deviceInstance,
                (uint)objectId.Type,
                objectId.Instance,
                (uint)BacnetPropertyIds.PROP_OBJECT_NAME), cancellationToken).ConfigureAwait(false);
            nodes.Add(new DriverBrowseNode(
                $"object:{(uint)objectId.Type}:{objectId.Instance}",
                $"device={deviceInstance};object={(uint)objectId.Type}:{objectId.Instance}",
                string.IsNullOrWhiteSpace(name) ? objectId.ToString() : name,
                IsContainer: true,
                IsReadable: true,
                IsWritable: false,
                Metadata: new Dictionary<string, string>
                {
                    ["objectType"] = ((uint)objectId.Type).ToString(CultureInfo.InvariantCulture),
                    ["objectInstance"] = objectId.Instance.ToString(CultureInfo.InvariantCulture)
                }));
        }
        var next = offset + selected.Length < objects.Length ? (offset + selected.Length).ToString(CultureInfo.InvariantCulture) : null;
        return new DriverBrowsePage(nodes, next, IsPartial: next is not null);
    }

    private static async Task<DriverBrowsePage> BrowseObjectPropertiesAsync(
        IBacnetSession session,
        uint deviceInstance,
        uint objectType,
        uint objectInstance,
        CancellationToken cancellationToken)
    {
        var binding = new BacnetBinding(
            deviceInstance,
            objectType,
            objectInstance,
            (uint)BacnetPropertyIds.PROP_PRESENT_VALUE);
        try
        {
            var sample = await session.ReadAsync(binding, cancellationToken).ConfigureAwait(false);
            if (sample.Values.Count == 0)
                return ErrorPage("BACNET_PRESENT_VALUE_EMPTY", "BACnet Present_Value returned no value.");
            var dataType = GuessTagDataType(sample.Values[0], objectType);
            return new DriverBrowsePage(new[]
            {
                new DriverBrowseNode(
                    $"property:{objectType}:{objectInstance}:{binding.PropertyIdentifier}",
                    binding.StableIdentity,
                    "Present Value",
                    IsContainer: false,
                    IsReadable: true,
                    IsWritable: IsCommonWritableObject(objectType),
                    PortableAddress: binding.PortableAddress,
                    SuggestedDataType: dataType,
                    Metadata: new Dictionary<string, string>
                    {
                        ["deviceInstance"] = deviceInstance.ToString(CultureInfo.InvariantCulture),
                        ["objectType"] = objectType.ToString(CultureInfo.InvariantCulture),
                        ["objectInstance"] = objectInstance.ToString(CultureInfo.InvariantCulture),
                        ["propertyIdentifier"] = binding.PropertyIdentifier.ToString(CultureInfo.InvariantCulture)
                    })
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ErrorPage("BACNET_PRESENT_VALUE_UNAVAILABLE", Sanitize(ex.Message), DriverEngineeringIssueSeverity.Warning);
        }
    }

    private static TagDataType? GuessTagDataType(BacnetValue value, uint objectType)
    {
        if (objectType is 3 or 4 or 5) return TagDataType.Boolean;
        if (objectType is 13 or 14 or 19) return TagDataType.Enum;
        return value.Tag switch
        {
            BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN => TagDataType.Boolean,
            BacnetApplicationTags.BACNET_APPLICATION_TAG_SIGNED_INT => TagDataType.Int64,
            BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT => TagDataType.Int64,
            BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL => TagDataType.Float,
            BacnetApplicationTags.BACNET_APPLICATION_TAG_DOUBLE => TagDataType.Double,
            BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING => TagDataType.String,
            BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED => TagDataType.Enum,
            BacnetApplicationTags.BACNET_APPLICATION_TAG_DATE or
            BacnetApplicationTags.BACNET_APPLICATION_TAG_TIME or
            BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME => TagDataType.DateTime,
            _ => null
        };
    }

    private static bool IsCommonWritableObject(uint objectType)
        => objectType is 1 or 2 or 4 or 5 or 14 or 19;

    private static async Task<string?> TryReadTextAsync(IBacnetSession session, BacnetBinding binding, CancellationToken cancellationToken)
    {
        try
        {
            var result = await session.ReadAsync(binding, cancellationToken).ConfigureAwait(false);
            return result.Values.FirstOrDefault().Value?.ToString();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static BacnetSessionOptions BuildOptions(IReadOnlyDictionary<string, string> settings, bool deviceRequired = true)
    {
        if (deviceRequired) _ = RequireDeviceInstance(settings);
        var localPort = ParseInt(settings, "localPort", BacnetClient.DEFAULT_UDP_PORT, 1, 65535);
        var timeoutMs = ParseInt(settings, "requestTimeoutMilliseconds", 3000, 100, 60000);
        var discoveryMs = ParseInt(settings, "discoveryWindowMilliseconds", 1500, 100, 30000);
        var bbmd = Get(settings, "bbmdAddress");
        var ttl = TryGetInt(settings, "foreignDeviceTtlSeconds", 30, 32767);
        return new BacnetSessionOptions(
            localPort,
            TimeSpan.FromMilliseconds(timeoutMs),
            Retries: 2,
            TimeSpan.FromMilliseconds(discoveryMs),
            bbmd,
            ttl);
    }

    private static uint RequireDeviceInstance(IReadOnlyDictionary<string, string> settings)
    {
        var raw = Get(settings, "deviceInstance");
        if (!uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value > BacnetBinding.MaximumDeviceInstance)
            throw new ArgumentException($"BACnet setting 'deviceInstance' is required from 0 to {BacnetBinding.MaximumDeviceInstance}.");
        return value;
    }

    private static int ParseInt(IReadOnlyDictionary<string, string> settings, string key, int defaultValue, int min, int max)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
            throw new ArgumentException($"BACnet setting '{key}' must be from {min} to {max}.");
        return value;
    }

    private static int? TryGetInt(IReadOnlyDictionary<string, string> settings, string key, int min, int max)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
            throw new ArgumentException($"BACnet setting '{key}' must be from {min} to {max}.");
        return value;
    }

    private static string? Get(IReadOnlyDictionary<string, string> settings, string key)
        => settings.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

    private static bool TryParseDeviceNode(string nodeId, out uint deviceInstance)
        => uint.TryParse(nodeId.StartsWith("device:", StringComparison.OrdinalIgnoreCase) ? nodeId[7..] : null,
            NumberStyles.None, CultureInfo.InvariantCulture, out deviceInstance);

    private static bool TryParseObjectNode(string nodeId, out uint objectType, out uint objectInstance)
    {
        objectType = default;
        objectInstance = default;
        if (!nodeId.StartsWith("object:", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = nodeId[7..].Split(':');
        return parts.Length == 2 &&
               uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out objectType) &&
               uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out objectInstance);
    }

    private static DriverBrowsePage ErrorPage(string code, string message, DriverEngineeringIssueSeverity severity = DriverEngineeringIssueSeverity.Error)
        => new(Array.Empty<DriverBrowseNode>(), Issues: new[] { Issue(code, severity, message) });

    private static DriverEngineeringIssue Issue(string code, DriverEngineeringIssueSeverity severity, string message)
        => new(code, severity, message);

    private static string Sanitize(string? message)
    {
        var value = string.IsNullOrWhiteSpace(message) ? "BACnet operation failed." : message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 512 ? value : value[..512];
    }
}
