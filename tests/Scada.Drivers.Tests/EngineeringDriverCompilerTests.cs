using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Modbus;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class EngineeringDriverCompilerTests
{
    [Fact]
    public void Compile_ProducesExecutableModbusPlanFromEngineeringPackage()
    {
        var dataSource = new DataSourceEngineeringDto(
            Id: Guid.NewGuid(),
            Key: "plc.main",
            Name: "Main PLC",
            Driver: EngineeringDriverCompiler.ModbusTcpDriverKey,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "192.0.2.10",
                ["port"] = "1502",
                ["scanIntervalMilliseconds"] = "250",
                ["requestTimeoutMilliseconds"] = "900",
                ["maxGapElements"] = "4",
                ["unitId"] = "7"
            });

        var coil = Tag(
            "Start",
            "Plant.P01.Start",
            TagDataType.Boolean,
            dataSource.Key,
            "coil:5",
            readOnly: false);
        var pressure = Tag(
            "Pressure",
            "Plant.P01.Pressure",
            TagDataType.Double,
            dataSource.Key,
            "holding:10",
            metadata: new Dictionary<string, string>
            {
                ["modbus.valueType"] = "UInt16",
                ["modbus.scale"] = "0.1",
                ["modbus.offset"] = "-1",
                ["modbus.unitId"] = "8"
            });
        var temperature = Tag(
            "Temperature",
            "Plant.P01.Temperature",
            TagDataType.Float,
            dataSource.Key,
            "hr:11",
            metadata: new Dictionary<string, string>
            {
                ["modbus.wordOrder"] = "LowWordFirst"
            });
        var input = Tag(
            "Remote Value",
            "Plant.P01.RemoteValue",
            TagDataType.Int16,
            dataSource.Key,
            "input:30");

        var package = Package(new[] { coil, pressure, temperature, input }, new[] { dataSource });
        var result = new EngineeringDriverCompiler().Compile(package);

        Assert.True(result.CanActivate);
        Assert.DoesNotContain(result.Issues, x => x.IsError);
        var plan = Assert.Single(result.ModbusTcpPlans);
        Assert.Equal("plc.main", plan.DataSourceKey);
        Assert.Equal("192.0.2.10", plan.Host);
        Assert.Equal(1502, plan.Port);
        Assert.Equal(TimeSpan.FromMilliseconds(250), plan.ScanRate);
        Assert.Equal(TimeSpan.FromMilliseconds(900), plan.RequestTimeout);
        Assert.Equal(4, plan.MaxGapElements);
        Assert.Equal(4, plan.Points.Count);

        var coilPoint = Assert.Single(plan.Points, x => x.Tag.Path == coil.Path);
        Assert.Equal(ModbusDataArea.Coil, coilPoint.Area);
        Assert.Equal((ushort)5, coilPoint.Address);
        Assert.Equal(ModbusValueType.Boolean, coilPoint.ValueType);
        Assert.True(coilPoint.Writable);
        Assert.Equal((byte)7, coilPoint.UnitId);

        var pressurePoint = Assert.Single(plan.Points, x => x.Tag.Path == pressure.Path);
        Assert.Equal(ModbusDataArea.HoldingRegister, pressurePoint.Area);
        Assert.Equal(ModbusValueType.UInt16, pressurePoint.ValueType);
        Assert.Equal(0.1d, pressurePoint.Scale, 5);
        Assert.Equal(-1d, pressurePoint.Offset, 5);
        Assert.Equal((byte)8, pressurePoint.UnitId);

        var temperaturePoint = Assert.Single(plan.Points, x => x.Tag.Path == temperature.Path);
        Assert.Equal(ModbusValueType.Float32, temperaturePoint.ValueType);
        Assert.Equal(ModbusWordOrder.LowWordFirst, temperaturePoint.WordOrder);

        var inputPoint = Assert.Single(plan.Points, x => x.Tag.Path == input.Path);
        Assert.Equal(ModbusDataArea.InputRegister, inputPoint.Area);
        Assert.False(inputPoint.Writable);
    }

    [Fact]
    public void Compile_AcceptsNumericAddressOnlyWhenAreaMetadataIsExplicit()
    {
        var dataSource = DataSource();
        var tag = Tag(
            "Pressure",
            "Plant.Pressure",
            TagDataType.Double,
            dataSource.Key,
            "42",
            metadata: new Dictionary<string, string>
            {
                ["modbus.area"] = "holding",
                ["modbus.valueType"] = "UInt16"
            });

        var result = new EngineeringDriverCompiler().Compile(Package(new[] { tag }, new[] { dataSource }));

        Assert.True(result.CanActivate);
        var point = Assert.Single(Assert.Single(result.ModbusTcpPlans).Points);
        Assert.Equal(ModbusDataArea.HoldingRegister, point.Area);
        Assert.Equal((ushort)42, point.Address);
    }

    [Fact]
    public void Compile_RejectsAmbiguousOrUnsafeModbusConfigurationBeforeActivation()
    {
        var dataSource = new DataSourceEngineeringDto(
            Id: null,
            Key: "plc.invalid",
            Name: "Invalid PLC",
            Driver: EngineeringDriverCompiler.ModbusTcpDriverKey,
            Settings: new Dictionary<string, string>
            {
                ["port"] = "70000",
                ["unitId"] = "999"
            });

        var ambiguousAddress = Tag(
            "Ambiguous",
            "Plant.Ambiguous",
            TagDataType.Double,
            dataSource.Key,
            "40001",
            metadata: new Dictionary<string, string> { ["modbus.valueType"] = "UInt16" });
        var readOnlyAreaMarkedWritable = Tag(
            "Input Command",
            "Plant.InputCommand",
            TagDataType.Int16,
            dataSource.Key,
            "input:0",
            readOnly: false);
        var badWordOrder = Tag(
            "Float",
            "Plant.Float",
            TagDataType.Float,
            dataSource.Key,
            "holding:10",
            metadata: new Dictionary<string, string> { ["modbus.wordOrder"] = "mystery" });

        var result = new EngineeringDriverCompiler().Compile(Package(
            new[] { ambiguousAddress, readOnlyAreaMarkedWritable, badWordOrder },
            new[] { dataSource }));

        Assert.False(result.CanActivate);
        Assert.Empty(result.ModbusTcpPlans);
        Assert.Contains(result.Issues, x => x.Code == "MODBUS_HOST_REQUIRED" && x.IsError);
        Assert.Contains(result.Issues, x => x.Code == "MODBUS_SETTING_INVALID" && x.Message.Contains("port", StringComparison.Ordinal));
        Assert.Contains(result.Issues, x => x.Code == "MODBUS_TAG_ADDRESS_INVALID" && x.TagPath == ambiguousAddress.Path);
        Assert.Contains(result.Issues, x => x.Code == "MODBUS_TAG_CONFIGURATION_INVALID" && x.TagPath == readOnlyAreaMarkedWritable.Path);
        Assert.Contains(result.Issues, x => x.Code == "MODBUS_WORD_ORDER_INVALID" && x.TagPath == badWordOrder.Path);
    }

    [Fact]
    public void Compile_IgnoresBuiltInSimulationButRejectsUnknownEnabledDrivers()
    {
        var simulation = new DataSourceEngineeringDto(null, "builtin.simulation", "Simulation", "builtin.simulation");
        var unsupported = new DataSourceEngineeringDto(null, "opc.legacy", "Legacy OPC", "opc.da");

        var result = new EngineeringDriverCompiler().Compile(Package(
            Array.Empty<TagEngineeringDto>(),
            new[] { simulation, unsupported }));

        Assert.False(result.CanActivate);
        Assert.Empty(result.ModbusTcpPlans);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("DRIVER_UNSUPPORTED", issue.Code);
        Assert.Equal("opc.legacy", issue.DataSourceKey);
    }

    private static DataSourceEngineeringDto DataSource() => new(
        Id: null,
        Key: "plc.main",
        Name: "Main PLC",
        Driver: EngineeringDriverCompiler.ModbusTcpDriverKey,
        Settings: new Dictionary<string, string> { ["host"] = "127.0.0.1" });

    private static TagEngineeringDto Tag(
        string name,
        string path,
        TagDataType type,
        string source,
        string address,
        bool readOnly = true,
        Dictionary<string, string>? metadata = null) => new(
            Id: Guid.NewGuid(),
            Name: name,
            Path: path,
            DataType: type,
            Source: source,
            Address: address,
            ReadOnly: readOnly,
            Metadata: metadata);

    private static EngineeringPackage Package(
        IReadOnlyCollection<TagEngineeringDto> tags,
        IReadOnlyCollection<DataSourceEngineeringDto> dataSources) => new(
            Schema: "scada.engineering",
            SchemaVersion: 5,
            ExportedAt: DateTimeOffset.UtcNow,
            Tags: tags,
            Alarms: Array.Empty<AlarmEngineeringDto>(),
            DataSources: dataSources);
}
