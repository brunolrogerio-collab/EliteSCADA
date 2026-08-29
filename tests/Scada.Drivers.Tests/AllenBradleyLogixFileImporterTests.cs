using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class AllenBradleyLogixFileImporterTests
{
    [Fact]
    public async Task ImportAsync_DispatchesL5kByExtensionThroughCommonCapability()
    {
        const string l5k = """
            CONTROLLER Demo
            TAG
            TankLevel : DINT (RADIX := Decimal, ExternalAccess := Read/Write, Constant := No) := 123;
            END_TAG
            END_CONTROLLER
            """;

        var importer = new AllenBradleyLogixFileImporter();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(l5k));
        var candidates = new List<DriverImportCandidate>();
        await foreach (var candidate in importer.ImportAsync(
                           new DriverImportRequest(null, "demo.L5K", "text/plain"),
                           stream))
        {
            candidates.Add(candidate);
        }

        var level = Assert.Single(candidates);
        Assert.Equal("controller:TankLevel", level.StableIdentity);
        Assert.Equal(TagDataType.Int32, level.SuggestedDataType);
        Assert.True(level.IsReadable);
        Assert.True(level.IsWritable);
        Assert.Equal(AllenBradleyLogixEngineeringAdapter.DriverType, importer.Descriptor.DriverType);
    }

    [Fact]
    public async Task ImportAsync_PreservesExistingL5xPath()
    {
        const string l5x = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RSLogix5000Content>
              <Controller Name="Demo">
                <Tags>
                  <Tag Name="Pressure" TagType="Base" DataType="REAL" ExternalAccess="Read Only" Constant="false" />
                </Tags>
              </Controller>
            </RSLogix5000Content>
            """;

        var importer = new AllenBradleyLogixFileImporter();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(l5x));
        var candidates = new List<DriverImportCandidate>();
        await foreach (var candidate in importer.ImportAsync(
                           new DriverImportRequest(null, "demo.L5X", "application/xml"),
                           stream))
        {
            candidates.Add(candidate);
        }

        var pressure = Assert.Single(candidates);
        Assert.Equal("controller:Pressure", pressure.StableIdentity);
        Assert.Equal(TagDataType.Float, pressure.SuggestedDataType);
        Assert.True(pressure.IsReadable);
        Assert.False(pressure.IsWritable);
    }

    [Fact]
    public async Task ImportAsync_DispatchesL5kByExplicitContentTypeWithoutTreatingPlainTextAsL5k()
    {
        const string l5k = """
            CONTROLLER Demo
            TAG
            Count : DINT (ExternalAccess := Read Only) := 9;
            END_TAG
            END_CONTROLLER
            """;

        var importer = new AllenBradleyLogixFileImporter();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(l5k));
        var candidates = new List<DriverImportCandidate>();
        await foreach (var candidate in importer.ImportAsync(
                           new DriverImportRequest(null, "project.export", "application/x-logix-l5k"),
                           stream))
        {
            candidates.Add(candidate);
        }

        var count = Assert.Single(candidates);
        Assert.Equal("controller:Count", count.StableIdentity);
        Assert.True(count.IsReadable);
        Assert.False(count.IsWritable);
    }
}
