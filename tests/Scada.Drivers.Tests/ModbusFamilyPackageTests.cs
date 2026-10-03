using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Modbus;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;

namespace Scada.Drivers.Tests;

public sealed class ModbusFamilyPackageTests
{
    [Fact]
    public void EscadaPackage_RoundTripsRtuAndServerCanonicalSettingsWithoutDiscoverySnapshot()
    {
        var sourceTags = new InMemoryTagRegistry();
        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var sourceDataSources = new InMemoryDataSourceEngineeringRegistry();

        sourceDataSources.Upsert(new DataSourceEngineeringDto(
            null,
            "rtu-master",
            "RTU Master",
            ModbusRtuDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["serialPort"] = "/dev/custom-authoring-path",
                ["baudRate"] = "19200",
                ["dataBits"] = "8",
                ["parity"] = "Even",
                ["stopBits"] = "One",
                ["unitId"] = "7"
            }));
        sourceDataSources.Upsert(new DataSourceEngineeringDto(
            null,
            "tcp-server",
            "TCP Server",
            ModbusTcpServerDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["bindAddress"] = "0.0.0.0",
                ["port"] = "1502",
                ["unitId"] = "1",
                ["holdingRanges"] = "v1:0-999;2000-2099"
            }));
        sourceDataSources.Upsert(new DataSourceEngineeringDto(
            null,
            "rtu-server",
            "RTU Server",
            ModbusRtuServerDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["serialPort"] = "COM9",
                ["baudRate"] = "9600",
                ["dataBits"] = "8",
                ["parity"] = "None",
                ["stopBits"] = "One",
                ["unitId"] = "2",
                ["holdingRanges"] = "v1:100-199"
            }));

        sourceTags.Register(TagDefinition.Create(
            "TCP Export",
            "Server.Tcp.Export",
            TagDataType.Int16,
            source: "tcp-server",
            readOnly: false,
            metadata: new Dictionary<string, string>
            {
                ["address"] = "holding:100",
                ["modbus.server.clientAccess"] = "ReadOnly",
                ["modbus.valueType"] = "UInt16"
            }));

        var sourcePackages = new ProjectPackageService(
            new EngineeringExchangeService(sourceTags, sourceAlarms, sourceDataSources));
        var bytes = sourcePackages.Export("modbus-family", "Modbus Family");
        var inspection = sourcePackages.Inspect(bytes);

        Assert.Equal(3, inspection.Engineering.DataSources!.Count);
        var inspectedTcp = Assert.Single(
            inspection.Engineering.DataSources,
            source => source.Key == "tcp-server");
        Assert.Equal("v1:0-999;2000-2099", inspectedTcp.Settings!["holdingRanges"]);
        Assert.DoesNotContain(inspectedTcp.Settings.Keys, key =>
            key.Contains("visiblePorts", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("enumerated", StringComparison.OrdinalIgnoreCase));

        var targetTags = new InMemoryTagRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var targetDataSources = new InMemoryDataSourceEngineeringRegistry();
        var targetPackages = new ProjectPackageService(
            new EngineeringExchangeService(targetTags, targetAlarms, targetDataSources));

        var preview = targetPackages.Preview(bytes, ImportMode.CreateAndUpdate);
        var applied = targetPackages.Apply(bytes, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(applied.Issues);
        Assert.Equal("/dev/custom-authoring-path", targetDataSources.FindByKey("rtu-master")!.Settings!["serialPort"]);
        Assert.Equal("COM9", targetDataSources.FindByKey("rtu-server")!.Settings!["serialPort"]);
        Assert.Equal("v1:100-199", targetDataSources.FindByKey("rtu-server")!.Settings!["holdingRanges"]);
        Assert.True(targetTags.TryGetByPath("Server.Tcp.Export", out var restored));
        Assert.Equal("holding:100", restored!.Metadata["address"]);
        Assert.Equal("ReadOnly", restored!.Metadata["modbus.server.clientAccess"]);
    }
}
