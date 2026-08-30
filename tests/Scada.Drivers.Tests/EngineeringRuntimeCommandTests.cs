using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class EngineeringRuntimeCommandTests
{
    [Fact]
    public async Task ActiveCommand_ExecutesConfiguredValueThroughOwningDriver()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[10] = 12;
        server.Start();

        var tagId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var package = CreatePackage(server.Port, tagId, commandId);
        await using var runtime = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));

        var activation = await runtime.ActivateAsync("plant-a", 1, package);

        Assert.True(activation.Activated);
        Assert.True(runtime.TryGetCommand(commandId, out var command));
        Assert.Equal("plant.p01.start", command!.Key);
        Assert.Equal(tagId, command.TargetTagId);
        Assert.Equal((short)77, command.Value);

        await runtime.ExecuteCommandAsync(commandId);

        Assert.Equal((ushort)77, server.HoldingRegisters[10]);
    }

    private static EngineeringPackage CreatePackage(int port, Guid tagId, Guid commandId)
    {
        var tag = new TagEngineeringDto(
            tagId,
            "Command Word",
            "Plant.P01.CommandWord",
            TagDataType.Int16,
            Source: "plc-a",
            Address: "holding:10",
            ReadOnly: false);
        var dataSource = new DataSourceEngineeringDto(
            null,
            "plc-a",
            "PLC A",
            EngineeringDriverCompiler.ModbusTcpDriverKey,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["scanIntervalMilliseconds"] = "25",
                ["requestTimeoutMilliseconds"] = "2000",
                ["unitId"] = "1"
            });
        var command = new CommandEngineeringDto(
            commandId,
            "plant.p01.start",
            "Start P01",
            CommandKind.WriteTagValue,
            "77",
            tagId,
            tag.Path,
            Area: "Plant",
            EquipmentPath: "Plant.P01");

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { tag },
            Array.Empty<AlarmEngineeringDto>(),
            new[] { dataSource },
            Commands: new[] { command });
    }
}