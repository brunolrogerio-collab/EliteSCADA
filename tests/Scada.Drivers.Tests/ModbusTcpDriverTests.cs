using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Modbus;

namespace Scada.Drivers.Tests;

public sealed class ModbusTcpDriverTests
{
    [Fact]
    public void ValueCodec_RoundTripsScaledFloatWithLowWordFirst()
    {
        var tag = TagDefinition.Create("Pressure", "Plant.Pressure", TagDataType.Double, readOnly: false);
        var point = new ModbusPoint(
            tag,
            UnitId: 1,
            Area: ModbusDataArea.HoldingRegister,
            Address: 100,
            ValueType: ModbusValueType.Float32,
            Writable: true,
            WordOrder: ModbusWordOrder.LowWordFirst,
            Scale: 2d,
            Offset: 10d);

        var registers = ModbusValueCodec.EncodeRegisters(point, 42d);
        var decoded = ModbusValueCodec.DecodeRegisters(point, registers);

        Assert.Equal(2, registers.Length);
        Assert.Equal(42d, Assert.IsType<double>(decoded), 5);
    }

    [Fact]
    public async Task Driver_PollsAllFourAreasGroupsRegistersAndWritesFc05Fc06Fc16()
    {
        await using var server = new TestModbusTcpServer();
        server.Coils[5] = false;
        server.DiscreteInputs[2] = true;
        server.HoldingRegisters[10] = 123;

        var floatBits = unchecked((uint)BitConverter.SingleToInt32Bits(25.5f));
        server.HoldingRegisters[11] = (ushort)(floatBits >> 16);
        server.HoldingRegisters[12] = (ushort)(floatBits & 0xFFFF);
        server.HoldingRegisters[20] = 0;
        server.HoldingRegisters[21] = 0;
        server.HoldingRegisters[24] = 40;
        server.InputRegisters[30] = 321;
        server.Start();

        var eventBus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(eventBus);
        var registry = new InMemoryTagRegistry();

        var coilTag = TagDefinition.Create("Command", "Demo.Modbus.Command", TagDataType.Boolean, readOnly: false);
        var discreteTag = TagDefinition.Create("Ready", "Demo.Modbus.Ready", TagDataType.Boolean);
        var pressureTag = TagDefinition.Create("Pressure", "Demo.Modbus.Pressure", TagDataType.Double, engineeringUnit: "bar");
        var temperatureTag = TagDefinition.Create("Temperature", "Demo.Modbus.Temperature", TagDataType.Double, engineeringUnit: "°C");
        var floatSetpointTag = TagDefinition.Create("Float Setpoint", "Demo.Modbus.FloatSetpoint", TagDataType.Double, readOnly: false);
        var speedTag = TagDefinition.Create("Speed", "Demo.Modbus.Speed", TagDataType.Double, readOnly: false);
        var inputTag = TagDefinition.Create("Input", "Demo.Modbus.Input", TagDataType.Double);

        var points = new[]
        {
            new ModbusPoint(coilTag, 1, ModbusDataArea.Coil, 5, ModbusValueType.Boolean, Writable: true),
            new ModbusPoint(discreteTag, 1, ModbusDataArea.DiscreteInput, 2, ModbusValueType.Boolean),
            new ModbusPoint(pressureTag, 1, ModbusDataArea.HoldingRegister, 10, ModbusValueType.UInt16, Scale: 0.1d),
            new ModbusPoint(temperatureTag, 1, ModbusDataArea.HoldingRegister, 11, ModbusValueType.Float32),
            new ModbusPoint(floatSetpointTag, 1, ModbusDataArea.HoldingRegister, 20, ModbusValueType.Float32, Writable: true),
            new ModbusPoint(speedTag, 1, ModbusDataArea.HoldingRegister, 24, ModbusValueType.UInt16, Writable: true, Scale: 0.5d),
            new ModbusPoint(inputTag, 1, ModbusDataArea.InputRegister, 30, ModbusValueType.UInt16, Scale: 0.01d)
        };

        await using var driver = new ModbusTcpDriver(
            "modbus.test",
            "Modbus Test",
            "127.0.0.1",
            cache,
            registry,
            points,
            server.Port,
            scanRate: TimeSpan.FromMilliseconds(50),
            requestTimeout: TimeSpan.FromMilliseconds(500),
            maxGapElements: 8);

        await driver.StartAsync();
        await WaitForAsync(() => points.All(x => TryGetGood(cache, x.Tag.Id)), TimeSpan.FromSeconds(5));

        Assert.Equal(false, Get(cache, coilTag.Id).Value);
        Assert.Equal(true, Get(cache, discreteTag.Id).Value);
        Assert.Equal(12.3d, Convert.ToDouble(Get(cache, pressureTag.Id).Value), 3);
        Assert.Equal(25.5d, Convert.ToDouble(Get(cache, temperatureTag.Id).Value), 3);
        Assert.Equal(20d, Convert.ToDouble(Get(cache, speedTag.Id).Value), 3);
        Assert.Equal(3.21d, Convert.ToDouble(Get(cache, inputTag.Id).Value), 3);

        var holdingBlock = Assert.Single(driver.PollBlocks, x => x.Area == ModbusDataArea.HoldingRegister);
        Assert.Equal((ushort)10, holdingBlock.StartAddress);
        Assert.Equal((ushort)15, holdingBlock.Quantity);
        Assert.Equal(4, holdingBlock.PointCount);
        Assert.Contains(server.Requests, x => x.Function == 0x03 && x.Address == 10 && x.Quantity == 15);
        Assert.Contains(server.Requests, x => x.Function == 0x01 && x.Address == 5 && x.Quantity == 1);
        Assert.Contains(server.Requests, x => x.Function == 0x02 && x.Address == 2 && x.Quantity == 1);
        Assert.Contains(server.Requests, x => x.Function == 0x04 && x.Address == 30 && x.Quantity == 1);

        await driver.WriteAsync(coilTag.Id, true);
        await driver.WriteAsync(speedTag.Id, 37.5d);
        await driver.WriteAsync(floatSetpointTag.Id, 42.25d);

        Assert.True(server.Coils[5]);
        Assert.Equal((ushort)75, server.HoldingRegisters[24]);
        Assert.Contains(server.Requests, x => x.Function == 0x05 && x.Address == 5);
        Assert.Contains(server.Requests, x => x.Function == 0x06 && x.Address == 24);
        Assert.Contains(server.Requests, x => x.Function == 0x10 && x.Address == 20 && x.Quantity == 2);

        var encodedFloat = new[] { server.HoldingRegisters[20], server.HoldingRegisters[21] };
        Assert.Equal(42.25d, Convert.ToDouble(ModbusValueCodec.DecodeRegisters(points[4], encodedFloat)), 3);
        Assert.Equal(DriverState.Running, driver.Status.State);

        await driver.StopAsync();
    }

    [Fact]
    public async Task Driver_ReconnectsAfterTcpConnectionIsDropped()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[0] = 100;
        server.Start();

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("Value", "Demo.Modbus.Value", TagDataType.Double);
        var point = new ModbusPoint(tag, 1, ModbusDataArea.HoldingRegister, 0, ModbusValueType.UInt16);

        await using var driver = new ModbusTcpDriver(
            "modbus.reconnect",
            "Reconnect Test",
            "127.0.0.1",
            cache,
            registry,
            new[] { point },
            server.Port,
            scanRate: TimeSpan.FromMilliseconds(50),
            requestTimeout: TimeSpan.FromMilliseconds(300));

        await driver.StartAsync();
        await WaitForAsync(() => TryGetGood(cache, tag.Id), TimeSpan.FromSeconds(5));
        var requestsBeforeDrop = server.Requests.Count;

        server.DropConnections();
        server.HoldingRegisters[0] = 222;

        await WaitForAsync(
            () => TryGetGood(cache, tag.Id) && Convert.ToDouble(Get(cache, tag.Id).Value) == 222d && server.Requests.Count > requestsBeforeDrop,
            TimeSpan.FromSeconds(5));

        Assert.Equal(DriverState.Running, driver.Status.State);
        Assert.Equal(222d, Convert.ToDouble(Get(cache, tag.Id).Value));

        await driver.StopAsync();
    }

    [Fact]
    public async Task Driver_PublishesBadCommunicationWhenEndpointBecomesUnavailable()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[0] = 55;
        server.Start();

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("Value", "Demo.Modbus.Unavailable", TagDataType.Double);
        var point = new ModbusPoint(tag, 1, ModbusDataArea.HoldingRegister, 0, ModbusValueType.UInt16);

        await using var driver = new ModbusTcpDriver(
            "modbus.unavailable",
            "Unavailable Test",
            "127.0.0.1",
            cache,
            registry,
            new[] { point },
            server.Port,
            scanRate: TimeSpan.FromMilliseconds(50),
            requestTimeout: TimeSpan.FromMilliseconds(150));

        await driver.StartAsync();
        await WaitForAsync(() => TryGetGood(cache, tag.Id), TimeSpan.FromSeconds(5));
        await server.StopAsync();

        await WaitForAsync(
            () => cache.TryGet(tag.Id, out var value) && value?.Quality == TagQuality.BadCommunication,
            TimeSpan.FromSeconds(5));

        var failed = Get(cache, tag.Id);
        Assert.Equal(TagQuality.BadCommunication, failed.Quality);
        Assert.Equal(55d, Convert.ToDouble(failed.Value));
        Assert.Equal(DriverState.Running, driver.Status.State);
        Assert.NotNull(driver.Status.Message);

        await driver.StopAsync();
    }

    private static bool TryGetGood(ICurrentTagCache cache, Guid id) =>
        cache.TryGet(id, out var value) && value?.Quality == TagQuality.Good;

    private static TagValue Get(ICurrentTagCache cache, Guid id)
    {
        Assert.True(cache.TryGet(id, out var value));
        return Assert.IsType<TagValue>(value);
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(20);
        }
        Assert.True(predicate(), $"Condition was not met within {timeout}.");
    }
}
