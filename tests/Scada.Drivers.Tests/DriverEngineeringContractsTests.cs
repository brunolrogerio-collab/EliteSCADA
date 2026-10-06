using System.Text.Json;
using Scada.Api.Engineering;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Tests;

public sealed class DriverEngineeringContractsTests
{
    [Fact]
    public void Descriptor_SeparatesRuntimeEngineeringAndAcquisitionCapabilities()
    {
        var descriptor = new CommunicationDriverTypeDescriptor(
            "mqtt.raw",
            "MQTT",
            DriverContractVersion: 1,
            RuntimeCapabilities: DriverCapabilities.Read |
                                 DriverCapabilities.Write |
                                 DriverCapabilities.Subscribe |
                                 DriverCapabilities.Diagnostics |
                                 DriverCapabilities.SourceTimestamp,
            EngineeringCapabilities: DriverEngineeringCapabilities.ConnectionTest |
                                     DriverEngineeringCapabilities.Discover,
            AcquisitionModes: [DriverAcquisitionMode.EventDriven],
            ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
                "elitescada.driver.mqtt.raw",
                1,
                [new DriverConfigurationFieldDescriptor("host", DriverConfigurationValueKind.Host, Required: true)],
                [new DriverConfigurationFieldDescriptor("topic", DriverConfigurationValueKind.String, Required: true)]));

        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Subscribe));
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Discover));
        Assert.False(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Browse));
        Assert.Equal(DriverAcquisitionMode.EventDriven, Assert.Single(descriptor.AcquisitionModes));
    }

    [Fact]
    public void EngineeringInterfaces_AreCapabilitySpecific()
    {
        Assert.True(typeof(ICommunicationDriverDiscoverySource)
            .IsAssignableTo(typeof(ICommunicationDriverDescriptorProvider)));
        Assert.True(typeof(ICommunicationDriverBrowser)
            .IsAssignableTo(typeof(ICommunicationDriverDescriptorProvider)));
        Assert.False(typeof(ICommunicationDriverDiscoverySource)
            .IsAssignableTo(typeof(ICommunicationDriverBrowser)));
        Assert.False(typeof(ICommunicationDriverFileImporter)
            .IsAssignableTo(typeof(ICommunicationDriverBrowser)));
        Assert.True(typeof(ICommunicationDriverPointReadTester)
            .IsAssignableTo(typeof(ICommunicationDriverDescriptorProvider)));
        Assert.False(typeof(ICommunicationDriverPointReadTester)
            .IsAssignableTo(typeof(ICommunicationDriverConnectionTester)));
    }

    [Fact]
    public void PointReadRequest_ValidatesCanonicalBindingAndBoundedSampling()
    {
        var request = new DriverPointReadTestRequest(
            new DriverEngineeringDataSourceContext(
                "source-1",
                "Source 1",
                "modbus.tcp",
                new Dictionary<string, string>(),
                new Dictionary<string, string>()),
            new CommunicationTagBinding(
                CommunicationTagBinding.CurrentContractVersion,
                "elitescada.driver.modbus.tcp.tag",
                1,
                "holding:10"),
            TagDataType.Double,
            EngineeringUnit: "bar",
            SampleCount: 3,
            SampleIntervalMilliseconds: 250,
            TimeoutMilliseconds: 5000);

        request.Validate();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (request with { SampleCount = 0 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (request with { SampleCount = DriverPointReadTestRequest.MaximumSampleCount + 1 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (request with { TimeoutMilliseconds = DriverPointReadTestRequest.MaximumTimeoutMilliseconds + 1 }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (request with { AddressSelector = new TagValueSelector(TagValueSelectorKind.Bit, -1) }).Validate());
    }

    [Fact]
    public void DraftPointReadApi_AcceptsTagEditorsCanonicalStringDataType()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        const string payload = """
            {
              "dataSource": { "sourceKey": "modbus", "sourceName": "Modbus", "driverType": "modbus.tcp", "settings": {}, "secretReferences": {} },
              "binding": { "contractVersion": 1, "schemaId": "elitescada.driver.modbus.tcp.tag", "schemaVersion": 1, "portableAddress": "holding:10", "settings": {} },
              "dataType": "int16",
              "addressSelector": { "kind": "bit", "index": 2 },
              "engineeringUnit": null,
              "sampleCount": 1,
              "sampleIntervalMilliseconds": 0,
              "timeoutMilliseconds": 5000
            }
            """;

        var request = JsonSerializer.Deserialize<DriverEngineeringDraftPointReadTestApiRequest>(payload, options);

        Assert.NotNull(request);
        Assert.Equal(TagDataType.Int16, request.DataType);
        Assert.Equal(new TagValueSelector(TagValueSelectorKind.Bit, 2), request.AddressSelector);
        Assert.Equal("holding:10", request.Binding.PortableAddress);
        Assert.Equal("modbus.tcp", request.DataSource.DriverType);
    }
    [Fact]
    public void DiscoveryCandidate_CanCarryReadOnlyCompositeMaterializationProposal()
    {
        var proposal = new DriverMaterializationCandidate(
            new DriverMaterializationEquipmentCandidate(
                "equipment-1",
                "Home.Relay",
                "Relay",
                "opaque-device-01",
                SourceRole: "primary",
                LocationPlaceholder: "Kitchen",
                Capabilities:
                [
                    new DriverMaterializationCapabilityCandidate(
                        "switch",
                        "OnOff",
                        [new DriverMaterializationRoleBinding("state", TagCandidateId: "tag-state")])
                ],
                Metadata: new Dictionary<string, string>
                {
                    ["manufacturer"] = "Example",
                    ["model"] = "Relay"
                }),
            Tags:
            [
                new DriverMaterializationTagCandidate(
                    "tag-state",
                    "State",
                    "Home.Relay.State",
                    TagDataType.Boolean,
                    PortableAddress: "channel:0",
                    ReadOnly: false)
            ]);

        var candidate = new DriverDiscoveryCandidate(
            "candidate-1",
            "opaque-device-01",
            "Relay",
            Materialization: proposal);

        Assert.Equal("opaque-device-01", candidate.Materialization!.Equipment.StableDeviceIdentity);
        Assert.Equal("OnOff", Assert.Single(candidate.Materialization.Equipment.Capabilities!).Kind);
        Assert.Equal("tag-state", Assert.Single(candidate.Materialization.Tags!).CandidateId);
    }

}
