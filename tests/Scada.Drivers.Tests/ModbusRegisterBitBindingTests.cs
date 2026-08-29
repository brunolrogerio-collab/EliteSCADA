using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Modbus;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class ModbusRegisterBitBindingTests
{
    [Fact]
    public void Compiler_MapsStructuredRegisterBitSelectorIntoRuntimePoint()
    {
        var dataSource = DataSource();
        var selector = new TagValueSelector(TagValueSelectorKind.Bit, 7);
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(),
            Name: "Pump Fault",
            Path: "Plant.P01.Fault",
            DataType: TagDataType.Boolean,
            Source: dataSource.Key,
            Address: "holding:40",
            ReadOnly: false,
            AddressSelector: selector);

        var result = new EngineeringDriverCompiler().Compile(Package(new[] { tag }, new[] { dataSource }));

        Assert.True(result.CanActivate);
        var point = Assert.Single(Assert.Single(result.ModbusTcpPlans).Points);
        Assert.Equal(ModbusDataArea.HoldingRegister, point.Area);
        Assert.Equal((ushort)40, point.Address);
        Assert.Equal(ModbusValueType.Boolean, point.ValueType);
        Assert.Equal(selector, point.AddressSelector);
        Assert.Equal(selector, point.Tag.AddressSelector);
        Assert.True(point.Writable);
    }

    [Theory]
    [InlineData("holding:1", 16, false)]
    [InlineData("coil:1", 1, false)]
    [InlineData("input:1", 3, true)]
    public void Compiler_RejectsInvalidRegisterBitBinding(string address, int bitIndex, bool writable)
    {
        var dataSource = DataSource();
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(),
            Name: "Invalid Bit",
            Path: "Plant.InvalidBit",
            DataType: TagDataType.Boolean,
            Source: dataSource.Key,
            Address: address,
            ReadOnly: !writable,
            AddressSelector: new TagValueSelector(TagValueSelectorKind.Bit, bitIndex));

        var result = new EngineeringDriverCompiler().Compile(Package(new[] { tag }, new[] { dataSource }));

        Assert.False(result.CanActivate);
        Assert.Empty(result.ModbusTcpPlans);
        Assert.Contains(result.Issues, x => x.Code == "MODBUS_TAG_CONFIGURATION_INVALID" && x.TagPath == tag.Path);
    }

    [Fact]
    public void Codec_DecodesAndMutatesOnlySelectedRegisterBit()
    {
        var point = Point("Bit 3", "Plant.Bit3", ModbusDataArea.HoldingRegister, 10, 3, writable: true);

        Assert.True(Assert.IsType<bool>(ModbusValueCodec.DecodeRegisters(point, new ushort[] { 0b1010 })));
        Assert.Equal((ushort)0b1011, ModbusValueCodec.ApplyRegisterBit(
            point with { Tag = point.Tag with { AddressSelector = new TagValueSelector(TagValueSelectorKind.Bit, 0) } },
            0b1010,
            true));
        Assert.Equal((ushort)0b0010, ModbusValueCodec.ApplyRegisterBit(point, 0b1010, false));
        Assert.Throws<InvalidOperationException>(() => ModbusValueCodec.EncodeRegisters(point, true));
    }

    [Fact]
    public async Task Driver_PollsTwoBitsFromOneRegisterUsingOneSharedPollBlock()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[10] = 0b1010;
        server.Start();

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var bit1 = Point("Bit 1", "Plant.Word.Bit1", ModbusDataArea.HoldingRegister, 10, 1);
        var bit3 = Point("Bit 3", "Plant.Word.Bit3", ModbusDataArea.HoldingRegister, 10, 3);

        await using var driver = new ModbusTcpDriver(
            "modbus.bits",
            "Bit Poll Test",
            "127.0.0.1",
            cache,
            registry,
            new[] { bit1, bit3 },
            server.Port,
            scanRate: TimeSpan.FromMilliseconds(50),
            requestTimeout: TimeSpan.FromMilliseconds(500));

        await driver.StartAsync();
        await WaitForAsync(() => TryGetGood(cache, bit1.Tag.Id) && TryGetGood(cache, bit3.Tag.Id), TimeSpan.FromSeconds(5));

        Assert.True(Assert.IsType<bool>(Get(cache, bit1.Tag.Id).Value));
        Assert.True(Assert.IsType<bool>(Get(cache, bit3.Tag.Id).Value));
        var block = Assert.Single(driver.PollBlocks);
        Assert.Equal(ModbusDataArea.HoldingRegister, block.Area);
        Assert.Equal((ushort)10, block.StartAddress);
        Assert.Equal((ushort)1, block.Quantity);
        Assert.Equal(2, block.PointCount);
        Assert.Contains(server.Requests, x => x.Function == 0x03 && x.Address == 10 && x.Quantity == 1);

        await driver.StopAsync();
    }

    [Fact]
    public async Task Driver_RegisterBitWritesPreserveOtherBitsAndDoNotLoseConcurrentUpdates()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[20] = 0b1000_0000;
        server.ResponseDelay = TimeSpan.FromMilliseconds(20);
        server.Start();

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var bit0 = Point("Bit 0", "Plant.Command.Bit0", ModbusDataArea.HoldingRegister, 20, 0, writable: true);
        var bit1 = Point("Bit 1", "Plant.Command.Bit1", ModbusDataArea.HoldingRegister, 20, 1, writable: true);

        await using var driver = new ModbusTcpDriver(
            "modbus.bitwrites",
            "Bit Write Test",
            "127.0.0.1",
            cache,
            registry,
            new[] { bit0, bit1 },
            server.Port,
            scanRate: TimeSpan.FromSeconds(5),
            requestTimeout: TimeSpan.FromSeconds(1));

        await driver.StartAsync();
        await WaitForAsync(() => TryGetGood(cache, bit0.Tag.Id) && TryGetGood(cache, bit1.Tag.Id), TimeSpan.FromSeconds(5));

        await Task.WhenAll(
            driver.WriteAsync(bit0.Tag.Id, true).AsTask(),
            driver.WriteAsync(bit1.Tag.Id, true).AsTask());

        Assert.Equal((ushort)0b1000_0011, server.HoldingRegisters[20]);
        Assert.True(Assert.IsType<bool>(Get(cache, bit0.Tag.Id).Value));
        Assert.True(Assert.IsType<bool>(Get(cache, bit1.Tag.Id).Value));
        Assert.True(server.Requests.Count(x => x.Function == 0x03 && x.Address == 20 && x.Quantity == 1) >= 3);
        Assert.Equal(2, server.Requests.Count(x => x.Function == 0x06 && x.Address == 20));

        await driver.StopAsync();
    }

    [Fact]
    public async Task Driver_InputRegisterBitRemainsReadOnlyAndCommunicationFailureDoesNotBecomeFalse()
    {
        await using var server = new TestModbusTcpServer();
        server.InputRegisters[30] = 1 << 5;
        server.Start();

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var point = Point("Input Bit", "Plant.Input.Bit5", ModbusDataArea.InputRegister, 30, 5);

        await using var driver = new ModbusTcpDriver(
            "modbus.inputbit",
            "Input Bit Test",
            "127.0.0.1",
            cache,
            registry,
            new[] { point },
            server.Port,
            scanRate: TimeSpan.FromMilliseconds(50),
            requestTimeout: TimeSpan.FromMilliseconds(150));

        await driver.StartAsync();
        await WaitForAsync(() => TryGetGood(cache, point.Tag.Id), TimeSpan.FromSeconds(5));
        Assert.True(Assert.IsType<bool>(Get(cache, point.Tag.Id).Value));
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.WriteAsync(point.Tag.Id, false).AsTask());

        await server.StopAsync();
        await WaitForAsync(
            () => cache.TryGet(point.Tag.Id, out var value) && value?.Quality == TagQuality.BadCommunication,
            TimeSpan.FromSeconds(5));

        var failed = Get(cache, point.Tag.Id);
        Assert.Equal(TagQuality.BadCommunication, failed.Quality);
        Assert.True(Assert.IsType<bool>(failed.Value));

        await driver.StopAsync();
    }

    private static ModbusPoint Point(
        string name,
        string path,
        ModbusDataArea area,
        ushort address,
        int bitIndex,
        bool writable = false)
    {
        var tag = TagDefinition.Create(
            name,
            path,
            TagDataType.Boolean,
            source: "plc.main",
            readOnly: !writable,
            addressSelector: new TagValueSelector(TagValueSelectorKind.Bit, bitIndex));
        return new ModbusPoint(tag, 1, area, address, ModbusValueType.Boolean, Writable: writable);
    }

    private static DataSourceEngineeringDto DataSource() => new(
        Id: null,
        Key: "plc.main",
        Name: "Main PLC",
        Driver: EngineeringDriverCompiler.ModbusTcpDriverKey,
        Settings: new Dictionary<string, string> { ["host"] = "127.0.0.1" });

    private static EngineeringPackage Package(
        IReadOnlyCollection<TagEngineeringDto> tags,
        IReadOnlyCollection<DataSourceEngineeringDto> dataSources) => new(
            Schema: "scada.engineering",
            SchemaVersion: 13,
            ExportedAt: DateTimeOffset.UtcNow,
            Tags: tags,
            Alarms: Array.Empty<AlarmEngineeringDto>(),
            DataSources: dataSources);

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
