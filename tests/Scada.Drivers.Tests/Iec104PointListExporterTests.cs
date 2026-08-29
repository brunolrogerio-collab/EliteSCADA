using System.Text;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104PointListExporterTests
{
    [Fact]
    public async Task ExportAsync_PreservesMultipleTypeIdsAndCsvEscapingAcrossRoundTrip()
    {
        var candidate = new DriverImportCandidate(
            CandidateId: "ca=1;ioa=77",
            StableIdentity: "ca=1;ioa=77",
            DisplayName: "Breaker, \"A\"",
            PortableAddress: "ca=1;ioa=77",
            IsReadable: true,
            IsWritable: false,
            SuggestedDataType: null,
            Metadata: new Dictionary<string, string>
            {
                ["declaredTypeIds"] = "1,30"
            });

        await using var content = new MemoryStream();
        await Iec104PointListExporter.ExportAsync(new[] { candidate }, content);

        var csv = Encoding.UTF8.GetString(content.ToArray());
        Assert.Contains("M_SP_NA_1", csv);
        Assert.Contains("M_SP_TB_1", csv);
        Assert.Contains("\"Breaker, \"\"A\"\"\"", csv);

        content.Position = 0;
        var importer = new Iec104PointListImporter();
        var request = new DriverImportRequest(
            Context: null,
            SourceName: "roundtrip.csv",
            ContentType: "text/csv");
        var imported = new List<DriverImportCandidate>();
        await foreach (var item in importer.ImportAsync(request, content))
            imported.Add(item);

        var roundTripped = Assert.Single(imported);
        Assert.Equal("ca=1;ioa=77", roundTripped.PortableAddress);
        Assert.Equal("Breaker, \"A\"", roundTripped.DisplayName);
        Assert.Equal("1,30", roundTripped.Metadata!["declaredTypeIds"]);
        Assert.Contains(roundTripped.Issues!, static issue => issue.Code == "iec104.import.typeConflict");
    }

    [Fact]
    public async Task ExportAsync_RejectsCommandTypeMetadata()
    {
        var candidate = new DriverImportCandidate(
            CandidateId: "ca=1;ioa=5",
            StableIdentity: "ca=1;ioa=5",
            DisplayName: "Unsafe command-shaped candidate",
            PortableAddress: "ca=1;ioa=5",
            IsReadable: true,
            IsWritable: false,
            Metadata: new Dictionary<string, string>
            {
                ["declaredTypeIds"] = "45"
            });
        await using var destination = new MemoryStream();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            Iec104PointListExporter.ExportAsync(new[] { candidate }, destination));

        Assert.Contains("unsupported monitored Type ID 45", exception.Message);
    }

    [Fact]
    public async Task ExportAsync_RejectsMultilineDisplayNameUntilImporterSupportsMultilineRecords()
    {
        var candidate = new DriverImportCandidate(
            CandidateId: "ca=1;ioa=6",
            StableIdentity: "ca=1;ioa=6",
            DisplayName: "Line one\nLine two",
            PortableAddress: "ca=1;ioa=6",
            IsReadable: true,
            IsWritable: false,
            Metadata: new Dictionary<string, string>
            {
                ["declaredTypeIds"] = "1"
            });
        await using var destination = new MemoryStream();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            Iec104PointListExporter.ExportAsync(new[] { candidate }, destination));

        Assert.Contains("cannot contain CR/LF", exception.Message);
    }
}
