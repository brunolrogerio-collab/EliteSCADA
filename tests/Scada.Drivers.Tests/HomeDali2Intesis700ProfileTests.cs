using System.Text.Json.Nodes;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Modbus;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class HomeDali2Intesis700ProfileTests
{
    private const string ProfilePath = "docs/examples/home-building/intesis-700-dali-2-engineering-package.json";
    private const string SourceKey = "intesis700.tcp";
    private const string DataSourceId = "70700000-0000-4000-8000-000000000100";

    [Fact]
    public void Package_ImportsExportsAndRoundTripsStableEngineeringBindings()
    {
        using var first = CreateExchange();
        var sourceJson = File.ReadAllText(FindProfileFile());
        var sourcePackage = first.Service.ParseJson(sourceJson);

        var preview = first.Service.Preview(sourcePackage, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);
        var result = first.Service.Apply(sourcePackage, ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);

        var firstExportJson = first.Service.ExportJson();
        var firstExport = first.Service.ParseJson(firstExportJson);
        AssertProfileBindings(firstExport);

        using var second = CreateExchange();
        var secondPreview = second.Service.Preview(firstExport, ImportMode.CreateAndUpdate);
        Assert.True(secondPreview.CanApply);
        var secondResult = second.Service.Apply(firstExport, ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(secondResult.Issues, issue => issue.IsError);

        var secondExportJson = second.Service.ExportJson();
        var secondExport = second.Service.ParseJson(secondExportJson);
        AssertProfileBindings(secondExport);

        using var third = CreateExchange();
        var thirdPreview = third.Service.Preview(secondExport, ImportMode.CreateAndUpdate);
        Assert.True(thirdPreview.CanApply);
        var thirdResult = third.Service.Apply(secondExport, ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(thirdResult.Issues, issue => issue.IsError);
        var thirdExportJson = third.Service.ExportJson();

        // The first export fills canonical historian defaults. After that normalization,
        // subsequent import/export cycles must be stable.
        Assert.Equal(NormalizeExportTime(secondExportJson), NormalizeExportTime(thirdExportJson));
    }

    [Fact]
    public void Package_CompilesOnlyDocumentedBoundedModbusPointsAndWrites()
    {
        using var fixture = CreateExchange();
        var package = fixture.Service.ParseJson(File.ReadAllText(FindProfileFile()));
        var source = Assert.Single(package.DataSources!);

        Assert.Equal(SourceKey, source.Key);
        Assert.Equal("modbus.tcp", source.Driver);
        Assert.Equal("192.0.2.10", source.Settings!["host"]);
        Assert.InRange(int.Parse(source.Settings["port"]), 1, 65535);
        Assert.InRange(int.Parse(source.Settings["unitId"]), 0, 255);
        Assert.InRange(int.Parse(source.Settings["scanIntervalMilliseconds"]), 10, 600000);
        Assert.InRange(int.Parse(source.Settings["requestTimeoutMilliseconds"]), 50, 60000);

        AssertProfileBindings(package);

        var compilation = new EngineeringDriverCompiler().Compile(package);
        Assert.True(compilation.CanActivate, string.Join("\n", compilation.Issues.Select(x => x.Message)));
        var plan = Assert.Single(compilation.ModbusTcpPlans);
        var writes = plan.Points.Where(point => point.Writable).Select(point => point.Tag.Path).Order().ToArray();
        Assert.Equal(
            new[]
            {
                "Home.Dali.Channel0.Ballast00.ArcPowerLevel",
                "Home.Dali.Channel0.Ballast00.ArcPowerOnOff",
                "Home.Dali.Channel0.Group00.ColorTemperature"
            }.Order(),
            writes);

        AssertPoint(plan, "Home.Dali.Channel0.Ballast00.LampPowered", ModbusDataArea.InputRegister, 5, writable: false, bit: 2);
        AssertPoint(plan, "Home.Dali.Channel0.Ballast00.LampFailure", ModbusDataArea.InputRegister, 5, writable: false, bit: 1);
        AssertPoint(plan, "Home.Dali.Channel0.Ballast00.BallastFailure", ModbusDataArea.InputRegister, 5, writable: false, bit: 0);
        AssertPoint(plan, "Home.Dali.Channel0.Ballast00.ActualLevel", ModbusDataArea.InputRegister, 6, writable: false);
        AssertPoint(plan, "Home.Dali.Channel0.Ballast00.ArcPowerLevel", ModbusDataArea.HoldingRegister, 15, writable: true);
        AssertPoint(plan, "Home.Dali.Channel0.Ballast00.ArcPowerOnOff", ModbusDataArea.HoldingRegister, 16, writable: true);
        AssertPoint(plan, "Home.Dali.Channel0.Group00.ColorTemperature", ModbusDataArea.HoldingRegister, 6416, writable: true);
        for (var bit = 0; bit < 6; bit++)
            AssertPoint(
                plan,
                package.Tags.Single(tag => tag.Path.EndsWith(DiagnosticSuffix(bit), StringComparison.Ordinal)).Path,
                ModbusDataArea.InputRegister,
                25065,
                writable: false,
                bit);

        Assert.Equal(0d, Tag(package, "Home.Dali.Channel0.Ballast00.ArcPowerLevel").ScaleMinimum);
        Assert.Equal(100d, Tag(package, "Home.Dali.Channel0.Ballast00.ArcPowerLevel").ScaleMaximum);
        Assert.Equal(0d, Tag(package, "Home.Dali.Channel0.Ballast00.ArcPowerOnOff").ScaleMinimum);
        Assert.Equal(1d, Tag(package, "Home.Dali.Channel0.Ballast00.ArcPowerOnOff").ScaleMaximum);
        Assert.Equal(1000d, Tag(package, "Home.Dali.Channel0.Group00.ColorTemperature").ScaleMinimum);
        Assert.Equal(10000d, Tag(package, "Home.Dali.Channel0.Group00.ColorTemperature").ScaleMaximum);
        Assert.Equal(TagDataType.Int16, Tag(package, "Home.Dali.Channel0.Group00.ColorTemperature").DataType);
        Assert.Equal("Int16", Tag(package, "Home.Dali.Channel0.Group00.ColorTemperature").Metadata!["modbus.valueType"]);

        // IN704 channel 1 is the largest address for these formulas; IN703 only has channel 0.
        var largestBallastPoint = 7000 * 1 + 100 * 63 + 16;
        var largestDiagnosticPoint = 25000 + 6400 * 1 + 100 * 63 + 65;
        var largestColorPoint = 7000 * 1 + 20 * 15 + 6416;
        Assert.InRange(largestBallastPoint, 0, ushort.MaxValue);
        Assert.InRange(largestDiagnosticPoint, 0, ushort.MaxValue);
        Assert.InRange(largestColorPoint, 0, ushort.MaxValue);
    }

    private static void AssertProfileBindings(EngineeringPackage package)
    {
        Assert.Equal("scada.engineering", package.Schema);
        Assert.Equal(23, package.SchemaVersion);
        Assert.Equal(13, package.Tags.Count);
        Assert.Empty(package.Alarms);
        Assert.Empty(package.Commands ?? Array.Empty<CommandEngineeringDto>());

        var dataSource = Assert.Single(package.DataSources!);
        Assert.Equal(Guid.Parse(DataSourceId), dataSource.Id);
        Assert.Equal("modbus.tcp", dataSource.Driver);

        foreach (var tag in package.Tags)
        {
            Assert.Equal(Guid.Parse(DataSourceId), tag.DataSourceId);
            Assert.Equal(SourceKey, tag.Source);
            Assert.NotNull(tag.Address);
            Assert.True(
                ModbusTagAddressCodec.TryParse(tag.Address, tag.Metadata, out _, out _, out var error),
                $"{tag.Path}: {error}");

            var binding = Assert.IsType<CommunicationTagBinding>(tag.CommunicationBinding);
            Assert.Equal("modbus.tcp.engineering", binding.SchemaId);
            Assert.Equal(1, binding.SchemaVersion);
            Assert.Equal(tag.Address, binding.PortableAddress);
            Assert.Equal(tag.Metadata!["modbus.valueType"], binding.EffectiveSettings["modbus.valueType"]);
        }

        var ballast = Assert.Single(package.Equipment!, equipment => equipment.Path == "Home.Dali.Channel0.Ballast00");
        Assert.Equal(Guid.Parse(DataSourceId), Assert.Single(ballast.SourceBindings!).DataSourceId);
        Assert.Contains(ballast.Capabilities!, capability => capability.Kind == EquipmentCapabilityKinds.OnOff);
        Assert.Contains(ballast.Capabilities!, capability => capability.Kind == EquipmentCapabilityKinds.Dimmer);
        Assert.Contains(ballast.Capabilities!, capability => capability.Kind == EquipmentCapabilityKinds.BinaryInput);

        var group = Assert.Single(package.Equipment!, equipment => equipment.Path == "Home.Dali.Channel0.Group00");
        Assert.Contains(group.Capabilities!, capability => capability.Kind == EquipmentCapabilityKinds.ColorLight);
        Assert.Equal("true", group.Metadata!["commissionedExternally"]);
    }

    private static void AssertPoint(
        ModbusTcpRuntimePlan plan,
        string path,
        ModbusDataArea area,
        ushort address,
        bool writable,
        int? bit = null)
    {
        var point = Assert.Single(plan.Points, candidate => candidate.Tag.Path == path);
        Assert.Equal(area, point.Area);
        Assert.Equal(address, point.Address);
        Assert.Equal(writable, point.Writable);
        Assert.Equal(bit, point.AddressSelector?.Index);
    }

    private static TagEngineeringDto Tag(EngineeringPackage package, string path) =>
        Assert.Single(package.Tags, tag => tag.Path == path);

    private static string DiagnosticSuffix(int bit) => bit switch
    {
        0 => "ControlGearOverallFailure",
        1 => "ExternalSupplyUndervoltage",
        2 => "ExternalSupplyOvervoltage",
        3 => "EcgOutputPowerLimit",
        4 => "ThermalDerating",
        5 => "ThermalShutdown",
        _ => throw new ArgumentOutOfRangeException(nameof(bit))
    };

    private static ExchangeFixture CreateExchange()
    {
        var bus = new InMemoryScadaEventBus();
        var alarms = new InMemoryAlarmEngine(bus);
        var exchange = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry());
        return new ExchangeFixture(exchange, alarms);
    }

    private static string FindProfileFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, ProfilePath);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Could not locate profile fixture '{ProfilePath}'.");
    }

    private static string NormalizeExportTime(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        root["exportedAt"] = "2000-01-01T00:00:00+00:00";
        return root.ToJsonString();
    }

    private sealed class ExchangeFixture(EngineeringExchangeService service, InMemoryAlarmEngine alarms) : IDisposable
    {
        public EngineeringExchangeService Service { get; } = service;

        public void Dispose() => alarms.Dispose();
    }
}
