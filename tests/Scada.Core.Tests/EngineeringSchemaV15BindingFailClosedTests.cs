using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class EngineeringSchemaV15BindingFailClosedTests
{
    [Fact]
    public void Preview_RejectsMalformedCommunicationBindingContract()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry());
        var binding = new CommunicationTagBinding(
            ContractVersion: 99,
            SchemaId: "modbus.tcp.tag",
            SchemaVersion: 1,
            PortableAddress: "40001");
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    null,
                    "Pressure",
                    "Plant.P01.Pressure",
                    TagDataType.Double,
                    Source: "plant.modbus01",
                    Address: binding.PortableAddress,
                    CommunicationBinding: binding)
            },
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                new DataSourceEngineeringDto(
                    null,
                    "plant.modbus01",
                    "PLC principal",
                    "modbus.tcp",
                    Settings: new Dictionary<string, string> { ["host"] = "10.10.0.10" })
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(x => x.Issues),
            issue => issue.Code == "TAG_COMMUNICATION_BINDING_INVALID");
    }
}
