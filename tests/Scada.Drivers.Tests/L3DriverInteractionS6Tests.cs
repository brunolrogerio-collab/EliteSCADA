using System.Globalization;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Interactions;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.OpenDnp3;
using Scada.Drivers.Mqtt;
using Scada.Drivers.Zigbee2Mqtt;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Interactions;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class L3DriverInteractionS6Tests
{
    private const string DeviceIeee = "0x00124b0001aa55ff";
    private const ushort BoundDnp3Index = 3;

    [Fact]
    [Trait("Category", "L3DriverInteractionS6")]
    public async Task MqttAction_ActiveScriptInvokesBoundDnp3PulseOn_AndHmiUsesSameRuntimeWithoutDeniedEffects()
    {
        var lab = RequiredLab();
        var projectKey = $"l3-s6-z2m-dnp3-{Guid.NewGuid():N}";
        var baseTopic = $"elitescada/l3/s6/{Guid.NewGuid():N}";
        var packageParts = BuildPackage(lab.MqttHost, lab.MqttPort, lab.Dnp3Host, lab.Dnp3Port, baseTopic);
        var package = packageParts.Package;
        var eventBus = new InMemoryScadaEventBus();
        var interactions = new ActiveDriverInteractionRuntimeCatalog();
        var preparedInteractions = interactions.Prepare(package);
        await using var ingress = new TransientEventRuntimeDispatcher(interactions, eventBus);
        var effectAuthority = 1;
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            dnp3SessionFactory: new OpenDnp3MasterSessionFactory());
        await using var coordinator = new EngineeringRuntimeCoordinator(
            eventBus,
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(25),
            new InMemoryServerMemoryRetentionStore(),
            communicationComponents: components,
            industrialEffectAuthority: () => Volatile.Read(ref effectAuthority) == 1,
            transientEventIngress: ingress);

        var configuration = new ConfigurationManager();
        await using var scripts = ServerScriptRuntimeManager.GetShared(coordinator, eventBus, configuration, interactions);
        var policyStore = CreateScriptPolicy(projectKey, packageParts.ScriptId, packageParts.CommandId);
        var serverAuthorization = new ApiAuthorizationService(new NullServiceProvider(), policyStore, configuration);
        var auditStore = new InMemoryAuditSink();
        var audit = new ApiAuditService(auditStore, auditStore, NullLogger<ApiAuditService>.Instance);
        using var commandRuntime = new RichCommandRuntimeService(
            interactions,
            interactions,
            coordinator,
            () => Volatile.Read(ref effectAuthority) == 1);
        var observedCommands = new RecordingRichCommandRuntime(commandRuntime);
        ServerScriptRichCommandBridge.Bind(scripts, interactions, observedCommands, serverAuthorization, audit);

        await using var publisher = new MqttNetClientTransport();
        using var credentials = MqttResolvedCredentials.None;
        await publisher.ConnectAsync(
            new MqttConnectionSettings(
                lab.MqttHost,
                lab.MqttPort,
                UseTls: false,
                ClientId: $"s6-publisher-{Guid.NewGuid():N}",
                ProtocolMode: MqttProtocolMode.Mqtt311,
                CleanSession: true),
            credentials);
        using var http = new HttpClient { BaseAddress = new Uri($"http://{lab.Dnp3Host}:{lab.Dnp3CapturePort}/") };

        try
        {
            using (var reset = await http.PostAsync("commands/reset", content: null))
            {
                reset.EnsureSuccessStatusCode();
            }

            // Seed retained bridge/device values before activation. Readiness
            // requires these initial values, so publishing them afterwards
            // would deadlock the candidate's pre-handover readiness gate.
            await PublishAsync(publisher, $"{baseTopic}/bridge/state", "online", retain: true);
            await PublishAsync(publisher, $"{baseTopic}/bridge/devices", packageParts.InventoryJson, retain: true);
            await PublishAsync(publisher, $"{baseTopic}/s6-button", "{\"state\":\"OFF\"}", retain: true);

            var activated = await scripts.ActivateRuntimeAsync(
                projectKey,
                1,
                package,
                (_, _) =>
                {
                    interactions.Commit(preparedInteractions);
                    return Task.CompletedTask;
                });
            Assert.True(activated.Activated, Describe(activated));

            await WaitForGoodTagAsync(coordinator, packageParts.AnalogTagId,
                value => Convert.ToInt32(value, CultureInfo.InvariantCulture) == 4242,
                TimeSpan.FromSeconds(15));
            await WaitForGoodTagAsync(coordinator, packageParts.OutputTagId,
                value => value is false,
                TimeSpan.FromSeconds(15));

            await EventuallyAsync(() =>
            {
                var z2m = DriverSnapshot(coordinator, Zigbee2MqttContract.DriverType);
                return z2m.ProtocolDetails is not null &&
                       z2m.ProtocolDetails.TryGetValue("deviceCount", out var count) && count == "1";
            }, TimeSpan.FromSeconds(10), "Z2M did not accept the retained simulated bridge inventory.");

            await PublishAsync(
                publisher,
                $"{baseTopic}/s6-button",
                JsonSerializer.Serialize(new { state = "OFF", last_seen = DateTimeOffset.UtcNow.AddSeconds(1).ToString("O", CultureInfo.InvariantCulture) }),
                retain: false);
            await WaitForGoodTagAsync(coordinator, packageParts.Z2mStateTagId,
                value => value is false,
                TimeSpan.FromSeconds(10));

            var before = await ReadCommandsAsync(http);
            Assert.Empty(before);

            // With Active process-effect authority withdrawn, even a live QoS-0
            // device action must not reach the event/script/command path.
            Volatile.Write(ref effectAuthority, 0);
            await PublishActionAsync(publisher, baseTopic, retained: false);
            await EventuallyAsync(
                () => Counter(DriverSnapshot(coordinator, Zigbee2MqttContract.DriverType), "rejectedActionEvents") >= 1,
                TimeSpan.FromSeconds(5),
                "The Z2M driver did not reject the action while process-effect authority was withdrawn.");
            Assert.Empty(await ReadCommandsAsync(http));

            Volatile.Write(ref effectAuthority, 1);
            await PublishActionAsync(publisher, baseTopic, retained: false);
            await EventuallyAsync(
                () => coordinator.TryGetCurrent(packageParts.ScriptResultTagId, out var value) &&
                      value is { Quality: TagQuality.Good } && Convert.ToInt32(value.Value, CultureInfo.InvariantCulture) == 1,
                TimeSpan.FromSeconds(15),
                () => scripts.Snapshot().Scripts.Single().Diagnostics.LastSanitizedError);

            var scriptInvocation = Assert.Single(observedCommands.Snapshot());
            Assert.Equal(InteractionOrigin.ServerScript, scriptInvocation.Causality?.Origin);
            Assert.Equal(InteractionScalarValue.Duration(TimeSpan.FromMilliseconds(250)),
                Assert.Single(scriptInvocation.Parameters).Value);
            var scriptCommands = await WaitForOperateCountAsync(http, expectedCount: 1, TimeSpan.FromSeconds(10));
            AssertCrob(scriptCommands, operationOrdinal: 0, expectedOnTimeMilliseconds: 250);

            var hmiAuthorization = new TestHmiAuthorization(
                new SecurityPrincipal("hmi-operator", "HMI Operator", ["operator"]));
            var hmiService = new HmiRichCommandInvocationService(
                interactions,
                interactions,
                observedCommands,
                hmiAuthorization,
                audit);
            var hmiRuntime = new ScadaRuntimeFacade(coordinator);
            var hmiContext = CreateHmiContext();

            hmiAuthorization.Allowed = false;
            var denied = await InvokeHmiAsync(
                hmiService,
                hmiRuntime,
                hmiContext,
                packageParts.CommandId,
                TimeSpan.FromMilliseconds(300));
            Assert.Equal("Rejected", denied.Outcome);
            Assert.Equal("authorization.denied", denied.Code);
            Assert.Single(observedCommands.Snapshot());
            Assert.Single(await ReadCommandsAsync(http), command => command.Phase == "operate");

            hmiAuthorization.Allowed = true;
            var hmiResult = await InvokeHmiAsync(
                hmiService,
                hmiRuntime,
                hmiContext,
                packageParts.CommandId,
                TimeSpan.FromMilliseconds(350));
            Assert.Equal("Completed", hmiResult.Outcome);

            var invocations = observedCommands.Snapshot();
            Assert.Equal(2, invocations.Count);
            Assert.Equal(InteractionOrigin.Hmi, invocations[1].Causality?.Origin);
            Assert.Equal(InteractionScalarValue.Duration(TimeSpan.FromMilliseconds(350)),
                Assert.Single(invocations[1].Parameters).Value);
            var allCommands = await WaitForOperateCountAsync(http, expectedCount: 2, TimeSpan.FromSeconds(10));
            AssertCrob(allCommands, operationOrdinal: 0, expectedOnTimeMilliseconds: 250);
            AssertCrob(allCommands, operationOrdinal: 1, expectedOnTimeMilliseconds: 350);

            var script = Assert.Single(scripts.Snapshot().Scripts);
            Assert.True(script.Diagnostics.CompletedCount >= 1);
            Assert.Contains(auditStore.Snapshot(), item =>
                item.Action == AuditActions.CommandExecute &&
                item.Source == "server-script" &&
                item.TargetId == packageParts.CommandId.ToString("D") &&
                item.Outcome == AuditOutcome.Succeeded);
            Assert.Contains(auditStore.Snapshot(), item =>
                item.Action == AuditActions.CommandExecute &&
                item.SubjectId == "hmi-operator" &&
                item.TargetId == packageParts.CommandId.ToString("D") &&
                item.Outcome == AuditOutcome.Denied);
            Assert.Contains(auditStore.Snapshot(), item =>
                item.Action == AuditActions.CommandExecute &&
                item.SubjectId == "hmi-operator" &&
                item.TargetId == packageParts.CommandId.ToString("D") &&
                item.Outcome == AuditOutcome.Succeeded);

            var dnp3 = DriverSnapshot(coordinator, Dnp3DriverDescriptorProvider.DriverType);
            Assert.True(dnp3.Counters.Connections >= 1);
            Assert.Equal(2, dnp3.Counters.WriteOperations);
        }
        finally
        {
            // Remove the unique retained lab fixtures; action events are never retained.
            await PublishAsync(publisher, $"{baseTopic}/bridge/state", string.Empty, retain: true);
            await PublishAsync(publisher, $"{baseTopic}/bridge/devices", string.Empty, retain: true);
            await PublishAsync(publisher, $"{baseTopic}/s6-button", string.Empty, retain: true);
        }
    }

    private static (EngineeringPackage Package, Guid ScriptId, Guid CommandId, Guid AnalogTagId, Guid OutputTagId, Guid Z2mStateTagId, Guid ScriptResultTagId, string InventoryJson) BuildPackage(
        string mqttHost,
        int mqttPort,
        string dnp3Host,
        int dnp3Port,
        string baseTopic)
    {
        var z2mId = Guid.NewGuid();
        var dnp3Id = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var scriptId = Guid.NewGuid();
        var analogTagId = Guid.NewGuid();
        var outputTagId = Guid.NewGuid();
        var z2mTagId = Guid.NewGuid();
        var scriptResultTagId = Guid.NewGuid();
        var z2mKey = "l3.s6.z2m";
        var dnp3Key = "l3.s6.dnp3";
        const string friendlyName = "s6-button";
        const string capabilityId = "button.main";
        var z2mSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = mqttHost,
            ["port"] = mqttPort.ToString(CultureInfo.InvariantCulture),
            ["baseTopic"] = baseTopic,
            ["protocolVersion"] = "mqtt311",
            ["reconnectMinimumMilliseconds"] = "100",
            ["reconnectMaximumMilliseconds"] = "500"
        };
        var z2mSource = new DataSourceEngineeringDto(
            z2mId,
            z2mKey,
            "S6 simulated Zigbee2MQTT bridge",
            Zigbee2MqttContract.DriverType,
            Settings: z2mSettings);
        var inventoryJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                ieee_address = DeviceIeee,
                friendly_name = friendlyName,
                supported = true,
                model_id = "S6-Test-Button",
                definition = new { vendor = "EliteSCADA L3 simulator" },
                exposes = new object[]
                {
                    new { type = "binary", property = "state", access = 1, value_on = "ON", value_off = "OFF" },
                    new { type = "action", property = "action", access = 1 }
                }
            }
        });
        var parsedSettings = Zigbee2MqttConnectionSettings.Parse(z2mKey, z2mId, z2mSettings);
        var device = Assert.Single(Zigbee2MqttExposeMapper.ParseInventory(
            Encoding.UTF8.GetBytes(inventoryJson), parsedSettings, "online").Devices);
        var materialized = Zigbee2MqttExposeMapper.BuildMaterialization(z2mId, z2mKey, device);
        var candidate = Assert.Single(materialized.Tags!, item =>
            item.PortableAddress == Zigbee2MqttIdentity.PortableAddress(DeviceIeee, null, "state"));
        var z2mBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            Zigbee2MqttContract.TagBindingSchemaId,
            Zigbee2MqttContract.TagBindingSchemaVersion,
            candidate.PortableAddress!,
            candidate.Metadata);
        var z2mTag = new TagEngineeringDto(
            z2mTagId,
            candidate.Name,
            candidate.Path,
            candidate.DataType,
            Source: z2mKey,
            Address: candidate.PortableAddress,
            ReadOnly: candidate.ReadOnly,
            Metadata: candidate.Metadata?.ToDictionary(pair => pair.Key, pair => pair.Value),
            CommunicationBinding: z2mBinding,
            DataSourceId: z2mId);

        var dnp3Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["transport"] = "tcp",
            ["host"] = dnp3Host,
            ["port"] = dnp3Port.ToString(CultureInfo.InvariantCulture),
            ["masterAddress"] = "1",
            ["outstationAddress"] = "1024",
            ["connectTimeout"] = "00:00:05",
            ["responseTimeout"] = "00:00:03",
            ["reconnectMinDelay"] = "00:00:00.100",
            ["reconnectMaxDelay"] = "00:00:01",
            ["keepAliveTimeout"] = "00:00:05",
            ["integrityPollInterval"] = "00:00:02"
        };
        var dnp3Source = new DataSourceEngineeringDto(
            dnp3Id,
            dnp3Key,
            "Independent S6 DNP3 simulated outstation",
            Dnp3DriverDescriptorProvider.DriverType,
            Settings: dnp3Settings);
        var analogBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            Dnp3DriverDescriptorProvider.ConfigurationSchemaId,
            1,
            "dnp3:analogInput:0",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["staticVariation"] = "G30V1" });
        var outputBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            Dnp3DriverDescriptorProvider.ConfigurationSchemaId,
            1,
            $"dnp3:binaryOutputStatus:{BoundDnp3Index}",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["writable"] = "true",
                ["commandMode"] = "selectBeforeOperate",
                ["binaryTrueOperation"] = "latchOn",
                ["binaryFalseOperation"] = "latchOff"
            });
        var analogTag = new TagEngineeringDto(
            analogTagId, "S6Analog0", "S6.DNP3.Analog0", TagDataType.Int32,
            Source: dnp3Key, Address: analogBinding.PortableAddress, ReadOnly: true,
            CommunicationBinding: analogBinding, DataSourceId: dnp3Id);
        var outputTag = new TagEngineeringDto(
            outputTagId, "S6Output3", "S6.DNP3.BinaryOutput3", TagDataType.Boolean,
            Source: dnp3Key, Address: outputBinding.PortableAddress, ReadOnly: false,
            CommunicationBinding: outputBinding, DataSourceId: dnp3Id);
        var scriptResultTag = new TagEngineeringDto(
            scriptResultTagId,
            "S6ScriptCommandResult",
            "S6.ServerMemory.ScriptCommandResult",
            TagDataType.Int32,
            Source: "memory.server",
            ReadOnly: false,
            InitialValue: new MemoryInitialValueDto(TagDataType.Int32, JsonSerializer.SerializeToElement(0)));

        var eventDefinition = new TransientEventDefinitionEngineeringDto(
            eventId,
            "z2m.action.button",
            [new TransientEventFieldDefinition(
                "action",
                new InteractionScalarSchema(InteractionScalarKind.String, MaximumLength: 32),
                Required: true)],
            equipmentId,
            capabilityId);
        var richCommand = new RichCommandDefinitionEngineeringDto(
            commandId,
            "dnp3.crob.pulseOn",
            [new RichCommandParameterDefinition(
                "onTime",
                new InteractionScalarSchema(
                    InteractionScalarKind.Duration,
                    Minimum: 100,
                    Maximum: 10_000,
                    Unit: "ms"),
                Required: true)]);
        var commandBinding = new DriverCommandBindingEngineeringDto(
            commandId,
            dnp3Id,
            Dnp3CommunicationRuntimeIdentity.StableDeviceIdentity(dnp3Id),
            "dnp3.crob.pulseOn",
            [
                new DriverCommandBindingSetting("pointKind", "binaryOutputStatus"),
                new DriverCommandBindingSetting("pointIndex", BoundDnp3Index.ToString(CultureInfo.InvariantCulture)),
                new DriverCommandBindingSetting("controlMode", "selectBeforeOperate"),
                new DriverCommandBindingSetting("count", "1")
            ]);
        var script = new ScriptEngineeringDefinition(
            scriptId,
            "Scripts.S6.Z2mToDnp3Pulse",
            "S6 Z2M action to DNP3 pulse",
            ScriptEngineeringScope.Server,
            $$"""
def on_event(event):
    action = event["canonicalEvent"]["payload"]["action"]
    if action == "single":
        result = invoke_rich_command("{{commandId:D}}", {"onTime": "00:00:00.2500000"})
        if result["outcome"] == "Completed":
            write_server_memory("{{scriptResultTagId:D}}", 1)
        else:
            write_server_memory("{{scriptResultTagId:D}}", -1)
""",
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.ServerRuntimeEvent,
                    "on_event",
                    TargetReference: eventId.ToString("D"))
            ],
            dependencies:
            [
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.RichCommand,
                    commandId.ToString("D")),
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.ServerMemoryTag,
                    scriptResultTagId.ToString("D"))
            ]);

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [z2mTag, analogTag, outputTag, scriptResultTag],
            Array.Empty<AlarmEngineeringDto>(),
            DataSources:
            [
                new DataSourceEngineeringDto(null, "memory.server", "Server Memory", InternalMemoryRuntimePlanner.ServerMemoryDriverKey),
                z2mSource,
                dnp3Source
            ],
            Equipment:
            [
                new EquipmentEngineeringDto(
                    equipmentId,
                    "S6.Button01",
                    "S6 simulated button",
                    SourceBindings:
                    [new EquipmentSourceBindingEngineeringDto(
                        z2mId,
                        Zigbee2MqttIdentity.StableDeviceIdentity(z2mId, DeviceIeee))],
                    Capabilities:
                    [new EquipmentCapabilityEngineeringDto(capabilityId, EquipmentCapabilityKinds.OnOff)])
            ],
            Scripts: [script],
            TransientEventDefinitions: [eventDefinition],
            CapabilityEventReferences:
            [new CapabilityEventReferenceEngineeringDto(
                equipmentId, capabilityId, "action", eventId, eventDefinition.SemanticKey)],
            RichCommandDefinitions: [richCommand],
            DriverCommandBindings: [commandBinding]);

        return (package, scriptId, commandId, analogTagId, outputTagId, z2mTagId, scriptResultTagId, inventoryJson);
    }

    private static IAuthorityPolicyStore CreateScriptPolicy(string projectKey, Guid scriptId, Guid commandId)
    {
        var scopeId = Guid.NewGuid();
        var roleKey = ServerScriptCommandIdentity.RoleKey(projectKey, scriptId);
        return new InMemoryAuthorityPolicyStore(
            [new SecurityRoleEngineeringDto(
                Guid.NewGuid(),
                roleKey,
                "S6 L3 Server Script",
                Grants:
                [new CapabilityGrantEngineeringDto(
                    SecurityCapability.CommandExecute,
                    new AuthorizationScopeEngineeringDto(ScopeNodeId: scopeId))])],
            [new SecurityScopeEngineeringDto(
                scopeId,
                "s6-dnp3-pulse-command",
                "S6 DNP3 Pulse Command",
                SecurityScopeNodeKind.Command,
                ResourceId: commandId)]);
    }

    private static async Task PublishAsync(IMqttClientTransport publisher, string topic, string payload, bool retain)
    {
        await publisher.PublishAsync(new MqttPublishRequest(
            topic,
            Encoding.UTF8.GetBytes(payload),
            MqttQosLevel.AtLeastOnce,
            retain));
    }

    private static Task PublishActionAsync(IMqttClientTransport publisher, string baseTopic, bool retained) =>
        publisher.PublishAsync(new MqttPublishRequest(
            $"{baseTopic}/s6-button",
            Encoding.UTF8.GetBytes("{\"action\":\"single\"}"),
            MqttQosLevel.AtMostOnce,
            retained)).AsTask();

    private static async Task<HmiRichCommandInvocationResponse> InvokeHmiAsync(
        HmiRichCommandInvocationService service,
        ScadaRuntimeFacade runtime,
        HttpContext context,
        Guid commandId,
        TimeSpan duration)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(duration.ToString("c", CultureInfo.InvariantCulture)));
        return await service.InvokeAsync(
            context,
            runtime,
            commandId,
            new HmiRichCommandInvocationRequest(
                commandId,
                new Dictionary<string, JsonElement>
                {
                    ["onTime"] = document.RootElement.Clone()
                }));
    }

    private static DefaultHttpContext CreateHmiContext()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "hmi-operator")],
                "s6-l3-test"))
        };
        context.Request.Method = HttpMethods.Post;
        return context;
    }

    private static async Task<TagValue> WaitForGoodTagAsync(
        EngineeringRuntimeCoordinator coordinator,
        Guid tagId,
        Func<object?, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        TagValue? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (coordinator.TryGetCurrent(tagId, out var current) && current is not null)
            {
                last = current;
                if (current.Quality == TagQuality.Good && predicate(current.Value)) return current;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException(
            $"Timed out waiting for L3 TAG {tagId}; last value='{last?.Value ?? "<none>"}', quality='{last?.Quality.ToString() ?? "<none>"}'.");
    }

    private static CommunicationDriverDiagnosticSnapshot DriverSnapshot(
        EngineeringRuntimeCoordinator coordinator,
        string driverType) =>
        Assert.Single(coordinator.Describe().CommunicationDrivers, driver =>
            string.Equals(driver.DriverType, driverType, StringComparison.OrdinalIgnoreCase));

    private static long Counter(CommunicationDriverDiagnosticSnapshot snapshot, string name) =>
        snapshot.ProtocolDetails is not null && snapshot.ProtocolDetails.TryGetValue(name, out var value) &&
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    private static async Task<IReadOnlyList<CapturedCrob>> ReadCommandsAsync(HttpClient http)
    {
        using var document = await http.GetAsync("commands");
        document.EnsureSuccessStatusCode();
        using var payload = await document.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(payload);
        return json.RootElement.EnumerateArray().Select(item => new CapturedCrob(
            item.GetProperty("phase").GetString()!,
            item.GetProperty("index").GetInt32(),
            item.GetProperty("operation").GetString()!,
            item.GetProperty("count").GetInt32(),
            item.GetProperty("onTimeMilliseconds").GetInt32(),
            item.GetProperty("offTimeMilliseconds").GetInt32(),
            item.GetProperty("selectSequence").ValueKind == JsonValueKind.Null
                ? null
                : item.GetProperty("selectSequence").GetInt32())).ToArray();
    }

    private static async Task<IReadOnlyList<CapturedCrob>> WaitForOperateCountAsync(
        HttpClient http,
        int expectedCount,
        TimeSpan timeout)
    {
        IReadOnlyList<CapturedCrob> commands = Array.Empty<CapturedCrob>();
        await EventuallyAsync(
            async () =>
            {
                commands = await ReadCommandsAsync(http);
                return commands.Count(command => command.Phase == "operate") == expectedCount;
            },
            timeout,
            $"Expected {expectedCount} DNP3 operate callbacks; observed {commands.Count(command => command.Phase == "operate")}.");
        return commands;
    }

    private static void AssertCrob(
        IReadOnlyList<CapturedCrob> commands,
        int operationOrdinal,
        int expectedOnTimeMilliseconds)
    {
        var operations = commands.Where(command => command.Phase == "operate").ToArray();
        var operation = Assert.IsType<CapturedCrob>(operations[operationOrdinal]);
        Assert.Equal(BoundDnp3Index, operation.Index);
        Assert.Equal("PULSE_ON", operation.Operation);
        Assert.Equal(1, operation.Count);
        Assert.Equal(expectedOnTimeMilliseconds, operation.OnTimeMilliseconds);
        Assert.Equal(0, operation.OffTimeMilliseconds);
        Assert.NotNull(operation.SelectSequence);
        var selections = commands.Where(command => command.Phase == "select").ToArray();
        Assert.Equal(operations.Length, selections.Length);
        Assert.Single(selections, command =>
            command.Index == operation.Index &&
            command.Operation == operation.Operation &&
            command.Count == operation.Count &&
            command.OnTimeMilliseconds == operation.OnTimeMilliseconds &&
            command.OffTimeMilliseconds == operation.OffTimeMilliseconds);
        Assert.Single(commands.Where(command => command.Phase == "operate" && command.Index == BoundDnp3Index && command.Operation == "PULSE_ON"),
            command => command.OnTimeMilliseconds == expectedOnTimeMilliseconds);
    }

    private static async Task EventuallyAsync(Func<bool> condition, TimeSpan timeout, string message)
    {
        await EventuallyAsync(() => Task.FromResult(condition()), timeout, message);
    }

    private static async Task EventuallyAsync(Func<bool> condition, TimeSpan timeout, Func<string?> failureDetails)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(50);
        }
        Assert.True(condition(), failureDetails());
    }

    private static async Task EventuallyAsync(Func<Task<bool>> condition, TimeSpan timeout, string message)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }
        throw new TimeoutException(message);
    }

    private static LabEndpoints RequiredLab()
    {
        var mqttHost = RequiredText("ELITESCADA_L3_MQTT_HOST");
        var dnp3Host = RequiredText("ELITESCADA_L3_DNP3_HOST");
        var helper = RequiredText("ELITESCADA_DNP3_HOST_PATH");
        if (!File.Exists(helper))
            throw new FileNotFoundException("The mandatory S6 L3 OpenDNP3 helper is unavailable.", helper);
        return new LabEndpoints(
            mqttHost,
            RequiredPort("ELITESCADA_L3_MQTT_PORT"),
            dnp3Host,
            RequiredPort("ELITESCADA_L3_DNP3_PORT"),
            RequiredPort("ELITESCADA_L3_DNP3_CAPTURE_PORT"));
    }

    private static string RequiredText(string name) =>
        Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Mandatory S6 L3 endpoint variable '{name}' is missing; this route must not be skipped.");

    private static int RequiredPort(string name) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= 65535
            ? value
            : throw new InvalidOperationException($"Mandatory S6 L3 endpoint variable '{name}' is missing or invalid; this route must not be skipped.");

    private static string Describe(RuntimeActivationResult result) =>
        string.Join(" | ", result.CompilationIssues.Select(issue => $"{issue.Code}: {issue.Message}")
            .Concat(result.RuntimeIssues.Select(issue => $"{issue.Code}: {issue.Message}")));

    private sealed record LabEndpoints(string MqttHost, int MqttPort, string Dnp3Host, int Dnp3Port, int Dnp3CapturePort);
    private sealed record CapturedCrob(string Phase, int Index, string Operation, int Count, int OnTimeMilliseconds, int OffTimeMilliseconds, int? SelectSequence);
    private sealed class RecordingRichCommandRuntime(IRichCommandRuntime inner) : IRichCommandRuntime
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<RichCommandInvocation> _invocations = new();

        public IReadOnlyList<RichCommandInvocation> Snapshot() => _invocations.ToArray();

        public async ValueTask<RichCommandResult> InvokeAsync(
            RichCommandInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            _invocations.Enqueue(invocation);
            return await inner.InvokeAsync(invocation, cancellationToken);
        }
    }

    private sealed class TestHmiAuthorization(SecurityPrincipal principal) : IRichCommandHmiAuthorization
    {
        public bool Allowed { get; set; } = true;

        public SecurityPrincipal GetPrincipal(HttpContext context) => principal;

        public Task<ApiAuthorizationCheck> CheckAsync(
            HttpContext context,
            ScadaRuntimeFacade runtime,
            RichCommandDefinition definition,
            bool requireRuntimeSession,
            CancellationToken cancellationToken)
        {
            var allowed = Allowed;
            return Task.FromResult(new ApiAuthorizationCheck(
                principal,
                new AuthorizationDecision(
                    allowed,
                    SecurityCapability.CommandExecute,
                    allowed ? "test-allowed" : "test-denied",
                    allowed ? ["operator"] : Array.Empty<string>())));
        }
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
