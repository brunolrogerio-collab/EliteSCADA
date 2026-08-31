using System.Net.Http.Json;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.AllenBradley;
using Scada.Drivers.Bacnet;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Iec60870;
using Scada.Drivers.Mqtt;
using Scada.Drivers.OpcUa;
using Scada.Drivers.SiemensS7Iso;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class L3SevenDriverWriteTests
{
    private const string OpcUaSecurityPolicyNone = "http://opcfoundation.org/UA/SecurityPolicy#None";

    [Fact]
    [Trait("Category", "L3SevenDriverRuntime")]
    public async Task OneRuntime_ExecutesEverySupportedFirstVersionWrite_WhileMqttRemainsAcquisitionOnly()
    {
        if (!TryGetLab(out var lab)) return;

        var mqttId = Guid.NewGuid();
        var iecMonitorId = Guid.NewGuid();
        var iecCommandId = Guid.NewGuid();
        var iecFeedbackId = Guid.NewGuid();
        var cipId = Guid.NewGuid();
        var opcUaId = Guid.NewGuid();
        var dnp3AnalogId = Guid.NewGuid();
        var dnp3OutputId = Guid.NewGuid();
        var s7Id = Guid.NewGuid();
        var bacnetId = Guid.NewGuid();

        var mqttBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            MqttDriverDescriptorProvider.SchemaId,
            1,
            "elitescada/lab/l3/write-value",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mqtt.payloadFormat"] = "utf8Scalar",
                ["mqtt.qos"] = "1"
            });

        var iecMonitorBinding = IecBinding("ca=1;ioa=100", "MMeNb1");
        var iecCommandBinding = IecBinding(
            "ca=1;ioa=5000",
            "MSpNa1",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["iec104.typeId"] = "MSpNa1",
                ["iec104.commandTypeId"] = "CScNa1",
                ["iec104.commandMode"] = "direct"
            });
        var iecFeedbackBinding = IecBinding("ca=1;ioa=5001", "MSpNa1");

        var cipReference = new LogixSymbolReference(LogixTagScope.Controller, "MyInt", LogixNativeType.Int);
        var cipBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            AllenBradleyLogixContractIdentity.BindingSchemaId,
            AllenBradleyLogixContractIdentity.BindingSchemaVersion,
            LogixPortableAddress.Format(cipReference, LogixExternalAccess.ReadWrite));

        var opcIdentity = new OpcUaNodeIdentity(
            "ns=2;s=Lab.Temperature",
            "urn:elitescada:interop:opcua");
        var opcBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            OpcUaDriverDescriptorProvider.ConfigurationSchemaId,
            OpcUaDriverDescriptorProvider.ConfigurationSchemaVersion,
            opcIdentity.PortableAddress);

        var dnpAnalogBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            Dnp3DriverDescriptorProvider.ConfigurationSchemaId,
            1,
            "dnp3:analogInput:0",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["staticVariation"] = "G30V1"
            });
        var dnpOutputBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            Dnp3DriverDescriptorProvider.ConfigurationSchemaId,
            1,
            "dnp3:binaryOutputStatus:3",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["writable"] = "true",
                ["commandMode"] = "selectBeforeOperate",
                ["binaryTrueOperation"] = "latchOn",
                ["binaryFalseOperation"] = "latchOff"
            });

        var s7Binding = S7Binding(new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.DataBlock,
            0,
            S7IsoValueType.Int16,
            DbNumber: 1,
            Writable: true));

        var bacnetNativeBinding = new BacnetBinding(
            599001,
            ObjectType: 2,
            ObjectInstance: 1,
            PropertyIdentifier: 85,
            UseCov: true,
            WritePriority: 8);
        var bacnetBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            BacnetCommunicationBindingProjection.SchemaId,
            BacnetCommunicationBindingProjection.SchemaVersion,
            BacnetCommunicationBindingProjection.ToCanonicalPortableAddress(bacnetNativeBinding),
            BacnetCommunicationBindingProjection.ToCanonicalSettings(bacnetNativeBinding));

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [
                Tag(mqttId, "MQTT", "L3.Write.MQTT", TagDataType.Double, "l3.write.mqtt", mqttBinding, true),
                Tag(iecMonitorId, "IEC104Monitor", "L3.Write.IEC104.Monitor", TagDataType.Int16, "l3.write.iec104", iecMonitorBinding, true),
                Tag(iecCommandId, "IEC104Command", "L3.Write.IEC104.Command", TagDataType.Boolean, "l3.write.iec104", iecCommandBinding, false),
                Tag(iecFeedbackId, "IEC104Feedback", "L3.Write.IEC104.Feedback", TagDataType.Boolean, "l3.write.iec104", iecFeedbackBinding, true),
                Tag(cipId, "CIP", "L3.Write.CIP", TagDataType.Int16, "l3.write.cip", cipBinding, false),
                Tag(opcUaId, "OPCUA", "L3.Write.OPCUA", TagDataType.Double, "l3.write.opcua", opcBinding, false),
                Tag(dnp3AnalogId, "DNP3Analog", "L3.Write.DNP3.Analog", TagDataType.Int32, "l3.write.dnp3", dnpAnalogBinding, true),
                Tag(dnp3OutputId, "DNP3Output", "L3.Write.DNP3.Output", TagDataType.Boolean, "l3.write.dnp3", dnpOutputBinding, false),
                Tag(s7Id, "S7", "L3.Write.S7", TagDataType.Int16, "l3.write.s7", s7Binding, false),
                Tag(bacnetId, "BACnet", "L3.Write.BACnet", TagDataType.Double, "l3.write.bacnet", bacnetBinding, false)
            ],
            Array.Empty<AlarmEngineeringDto>(),
            [
                MqttDataSource("l3.write.mqtt", lab.MqttHost, lab.MqttPort),
                Iec104DataSource("l3.write.iec104", lab.Iec104Host, lab.Iec104Port),
                CipDataSource("l3.write.cip", lab.CipHost, lab.CipPort),
                OpcUaDataSource("l3.write.opcua", lab.OpcUaEndpoint),
                Dnp3DataSource("l3.write.dnp3", lab.Dnp3Host, lab.Dnp3Port),
                S7DataSource("l3.write.s7", lab.S7Host, lab.S7Port),
                BacnetDataSource("l3.write.bacnet")
            ]);

        var bus = new InMemoryScadaEventBus();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema();
        var inner = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(30),
            communicationComponents: components);
        await using var runtime = new GatewayEngineeringRuntimeCoordinator(inner, bus);

        var activation = await runtime.ActivateAsync("l3-seven-driver-writes", 1, package);
        Assert.True(activation.Activated, Describe(activation));
        Assert.Equal(7, runtime.Describe().CommunicationDrivers.Count);

        await WaitForGoodValueAsync(runtime, iecMonitorId, value => Convert.ToInt16(value) == 23, TimeSpan.FromSeconds(10));
        await WaitForGoodValueAsync(runtime, cipId, value => Convert.ToInt16(value) == 1234, TimeSpan.FromSeconds(10));
        await WaitForGoodValueAsync(runtime, opcUaId, value => Math.Abs(Convert.ToDouble(value) - 21.5d) < 0.001d, TimeSpan.FromSeconds(10));
        await WaitForGoodValueAsync(runtime, dnp3AnalogId, value => Convert.ToInt32(value) == 4242, TimeSpan.FromSeconds(10));
        await WaitForGoodValueAsync(runtime, s7Id, value => Convert.ToInt16(value) == 1234, TimeSpan.FromSeconds(10));
        await WaitForGoodValueAsync(runtime, bacnetId, value => Math.Abs(Convert.ToDouble(value) - 21.5d) < 0.001d, TimeSpan.FromSeconds(10));

        await PublishMqttAsync(lab.NodeRedUrl, "elitescada/lab/l3/write-value", 18.5d);
        await WaitForGoodValueAsync(runtime, mqttId, value => Math.Abs(Convert.ToDouble(value) - 18.5d) < 0.001d, TimeSpan.FromSeconds(10));

        // MQTT v1 is acquisition-only. Every other Driver below has a supported
        // first-version command/write path and is exercised against the same
        // concurrently active seven-Driver runtime.
        await runtime.WriteAsync(iecCommandId, true);
        await WaitForGoodValueAsync(runtime, iecFeedbackId, value => Convert.ToBoolean(value), TimeSpan.FromSeconds(10));

        await runtime.WriteAsync(cipId, (short)3101);
        await WaitForCipValueAsync(lab.CipHost, lab.CipPort, cipReference, (short)3101, TimeSpan.FromSeconds(10));
        await WaitForGoodValueAsync(runtime, cipId, value => Convert.ToInt16(value) == 3101, TimeSpan.FromSeconds(10));

        await runtime.WriteAsync(opcUaId, 31.25d);
        await WaitForGoodValueAsync(runtime, opcUaId, value => Math.Abs(Convert.ToDouble(value) - 31.25d) < 0.001d, TimeSpan.FromSeconds(10));

        await runtime.WriteAsync(dnp3OutputId, true);

        await runtime.WriteAsync(s7Id, (short)3202);
        await WaitForGoodValueAsync(runtime, s7Id, value => Convert.ToInt16(value) == 3202, TimeSpan.FromSeconds(10));

        await runtime.WriteAsync(bacnetId, 32.5d);
        await WaitForGoodValueAsync(runtime, bacnetId, value => Math.Abs(Convert.ToDouble(value) - 32.5d) < 0.001d, TimeSpan.FromSeconds(10));
    }

    private static CommunicationTagBinding IecBinding(
        string address,
        string typeId,
        IReadOnlyDictionary<string, string>? settings = null)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["iec104.typeId"] = typeId
        };
        if (settings is not null)
            foreach (var item in settings) merged[item.Key] = item.Value;

        return new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            Iec104CommunicationRuntimePlanner.BindingSchemaId,
            Iec104CommunicationRuntimePlanner.BindingSchemaVersion,
            address,
            merged);
    }

    private static TagEngineeringDto Tag(
        Guid id,
        string name,
        string path,
        TagDataType dataType,
        string source,
        CommunicationTagBinding binding,
        bool readOnly) => new(
            id,
            name,
            path,
            dataType,
            Source: source,
            Address: binding.PortableAddress,
            ReadOnly: readOnly,
            CommunicationBinding: binding);

    private static DataSourceEngineeringDto MqttDataSource(string key, string host, int port) => new(
        Guid.NewGuid(), key, "L3 Write MQTT", MqttDriverDescriptorProvider.DriverType,
        Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = host,
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["tls"] = "false",
            ["clientId"] = $"elite-l3-write-{Guid.NewGuid():N}",
            ["protocolVersion"] = "mqtt5"
        });

    private static DataSourceEngineeringDto Iec104DataSource(string key, string host, int port) => new(
        Guid.NewGuid(), key, "L3 Write IEC-104", Iec104EngineeringConnectionTester.DriverType,
        Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = host,
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["commonAddresses"] = "1",
            ["stationTimeZone"] = "UTC"
        });

    private static DataSourceEngineeringDto CipDataSource(string key, string host, int port) => new(
        Guid.NewGuid(), key, "L3 Write CIP", AllenBradleyLogixContractIdentity.DriverType,
        Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = host,
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["profile"] = nameof(LogixControllerProfile.ControlLogix),
            ["scanIntervalMs"] = "100",
            ["requestTimeoutMs"] = "3000",
            ["reconnectMinimumMs"] = "100",
            ["reconnectMaximumMs"] = "1000",
            ["maxBatchSize"] = "8",
            ["securityMode"] = "Unsecured"
        });

    private static DataSourceEngineeringDto OpcUaDataSource(string key, string endpoint) => new(
        Guid.NewGuid(), key, "L3 Write OPC UA", OpcUaDriverDescriptorProvider.DriverTypeId,
        Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["endpointUrl"] = endpoint,
            ["securityMode"] = "None",
            ["securityPolicyUri"] = OpcUaSecurityPolicyNone,
            ["authenticationMode"] = "Anonymous",
            ["sessionTimeout"] = "00:00:30",
            ["publishingInterval"] = "00:00:00.100"
        });

    private static DataSourceEngineeringDto Dnp3DataSource(string key, string host, int port) => new(
        Guid.NewGuid(), key, "L3 Write DNP3", Dnp3DriverDescriptorProvider.DriverType,
        Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["transport"] = "tcp",
            ["host"] = host,
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["masterAddress"] = "1",
            ["outstationAddress"] = "1024",
            ["connectTimeout"] = "00:00:02",
            ["responseTimeout"] = "00:00:02",
            ["reconnectMinDelay"] = "00:00:00.100",
            ["reconnectMaxDelay"] = "00:00:01",
            ["keepAliveTimeout"] = "00:00:05",
            ["integrityPollInterval"] = "00:15:00"
        });

    private static DataSourceEngineeringDto S7DataSource(string key, string host, int port) => new(
        Guid.NewGuid(), key, "L3 Write S7", S7IsoCommunicationRuntimePlan.DriverTypeKey,
        Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = host,
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["cpuFamily"] = nameof(S7CpuFamily.S71500),
            ["connectionMode"] = nameof(S7IsoConnectionMode.RackSlot),
            ["rack"] = "0",
            ["slot"] = "1",
            ["connectionRole"] = nameof(S7IsoConnectionRole.Basic),
            ["writeEnabled"] = "true",
            ["sourceTsap"] = "0x0100",
            ["connectTimeoutMs"] = "2000",
            ["requestTimeoutMs"] = "2000",
            ["reconnectDelayMs"] = "100",
            ["requestedPduSize"] = "480"
        });

    private static DataSourceEngineeringDto BacnetDataSource(string key) => new(
        Guid.NewGuid(), key, "L3 Write BACnet/IP", BacnetDriverDescriptor.DriverType,
        Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["deviceInstance"] = "599001",
            ["localPort"] = "47809",
            ["scanIntervalMilliseconds"] = "100",
            ["requestTimeoutMilliseconds"] = "2000",
            ["discoveryWindowMilliseconds"] = "3000",
            ["targetAddress"] = "127.0.0.1:47808"
        });

    private static CommunicationTagBinding S7Binding(S7IsoTagBinding binding)
    {
        var transform = S7IsoCommunicationBindingProjection.GetPhysicalValueTransform(binding);
        return new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            S7IsoCommunicationBindingProjection.SchemaId,
            S7IsoCommunicationBindingProjection.SchemaVersion,
            S7IsoCommunicationBindingProjection.ToCanonicalPortableAddress(binding),
            S7IsoCommunicationBindingProjection.ToCanonicalSettings(binding),
            new TagPhysicalValueTransform(ByteSwap: transform.ByteSwap, WordSwap: transform.WordSwap));
    }

    private static async Task PublishMqttAsync(string nodeRedUrl, string topic, double value)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var response = await client.PostAsJsonAsync(
            $"{nodeRedUrl.TrimEnd('/')}/lab/mqtt/publish",
            new
            {
                topic,
                qos = 1,
                retain = false,
                payload = value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        Assert.True(response.IsSuccessStatusCode, $"Node-RED MQTT stimulus returned {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    private static async Task WaitForGoodValueAsync(
        GatewayEngineeringRuntimeCoordinator runtime,
        Guid tagId,
        Func<object, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (runtime.TryGetCurrent(tagId, out var current) &&
                current?.Quality == TagQuality.Good &&
                current.Value is not null &&
                predicate(current.Value))
                return;
            await Task.Delay(50);
        }

        runtime.TryGetCurrent(tagId, out var last);
        throw new TimeoutException($"TAG '{tagId}' did not reach expected Good value within {timeout}. Last={last?.Value}; Quality={last?.Quality}.");
    }

    private static async Task WaitForCipValueAsync(
        string host,
        int port,
        LogixSymbolReference reference,
        short expected,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        Exception? lastError = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var verifier = new LogixEtherNetIpClient();
                await verifier.ConnectAsync(new AllenBradleyLogixOptions(
                    host,
                    port,
                    LogixControllerProfile.ControlLogix,
                    ScanInterval: TimeSpan.FromMilliseconds(150),
                    RequestTimeout: TimeSpan.FromSeconds(3),
                    ReconnectMinimum: TimeSpan.FromMilliseconds(100),
                    ReconnectMaximum: TimeSpan.FromSeconds(1),
                    MaxBatchSize: 1));
                var result = Assert.Single(await verifier.ReadManyAsync([reference]));
                await verifier.DisconnectAsync();
                if (result.Succeeded && result.NativeValue is short current && current == expected) return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException($"CIP peer did not expose {expected} for '{reference.StableIdentity}'. Last error: {lastError?.Message}");
    }

    private static bool TryGetLab(out LabEndpoints lab)
    {
        lab = default!;
        if (!TryEndpoint("ELITESCADA_L3_MQTT_HOST", "ELITESCADA_L3_MQTT_PORT", out var mqttHost, out var mqttPort) ||
            !TryEndpoint("ELITESCADA_L3_IEC104_HOST", "ELITESCADA_L3_IEC104_PORT", out var iecHost, out var iecPort) ||
            !TryEndpoint("ELITESCADA_L3_CIP_HOST", "ELITESCADA_L3_CIP_PORT", out var cipHost, out var cipPort) ||
            !TryEndpoint("ELITESCADA_L3_DNP3_HOST", "ELITESCADA_L3_DNP3_PORT", out var dnpHost, out var dnpPort) ||
            !TryEndpoint("ELITESCADA_L3_S7_HOST", "ELITESCADA_L3_S7_PORT", out var s7Host, out var s7Port))
            return false;

        var opcEndpoint = Environment.GetEnvironmentVariable("ELITESCADA_L3_OPCUA_ENDPOINT")?.Trim();
        var nodeRedUrl = Environment.GetEnvironmentVariable("ELITESCADA_L3_NODE_RED_URL")?.Trim();
        if (string.IsNullOrWhiteSpace(opcEndpoint) || string.IsNullOrWhiteSpace(nodeRedUrl)) return false;

        lab = new LabEndpoints(
            mqttHost, mqttPort, iecHost, iecPort, cipHost, cipPort,
            opcEndpoint, dnpHost, dnpPort, s7Host, s7Port, nodeRedUrl);
        return true;
    }

    private static bool TryEndpoint(string hostVariable, string portVariable, out string host, out int port)
    {
        host = Environment.GetEnvironmentVariable(hostVariable)?.Trim() ?? string.Empty;
        var rawPort = Environment.GetEnvironmentVariable(portVariable)?.Trim();
        if (string.IsNullOrWhiteSpace(host) || !int.TryParse(rawPort, out port) || port is < 1 or > 65535)
        {
            port = 0;
            return false;
        }
        return true;
    }

    private static string Describe(RuntimeActivationResult result) =>
        string.Join(" | ", result.CompilationIssues.Select(issue => $"{issue.Code}: {issue.Message}")
            .Concat(result.RuntimeIssues.Select(issue => $"{issue.Code}: {issue.Message}")));

    private sealed record LabEndpoints(
        string MqttHost,
        int MqttPort,
        string Iec104Host,
        int Iec104Port,
        string CipHost,
        int CipPort,
        string OpcUaEndpoint,
        string Dnp3Host,
        int Dnp3Port,
        string S7Host,
        int S7Port,
        string NodeRedUrl);
}
