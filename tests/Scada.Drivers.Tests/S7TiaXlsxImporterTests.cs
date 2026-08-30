using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7TiaXlsxImporterTests
{
    [Fact]
    public async Task ImportXlsx_PreservesSupportedAndUnsupportedCandidates()
    {
        using var workbook = CreateWorkbook();
        var request = new DriverImportRequest(
            null,
            "plc-tags.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var candidates = await ImportAsync(new S7IsoEngineeringAdapter(), request, workbook);

        Assert.Equal(5, candidates.Count);

        var run = Assert.Single(candidates, candidate => candidate.DisplayName == "Run");
        Assert.True(run.IsReadable);
        Assert.True(run.IsWritable);
        Assert.Equal(TagDataType.Boolean, run.SuggestedDataType);
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(run.PortableAddress, out var runBinding, out var runError), runError);
        Assert.Equal(S7IsoArea.Merker, runBinding!.Area);
        Assert.Equal(0, runBinding.ByteOffset);
        Assert.Equal((byte)0, runBinding.BitOffset);
        Assert.True(runBinding.Writable);
        Assert.Equal("TiaXlsx", run.Metadata!["sourceKind"]);
        Assert.Equal("Supported", run.Metadata["supportStatus"]);

        var speed = Assert.Single(candidates, candidate => candidate.DisplayName == "Speed");
        Assert.True(speed.IsReadable);
        Assert.False(speed.IsWritable);
        Assert.Equal(TagDataType.Float, speed.SuggestedDataType);
        Assert.Contains(speed.Issues!, issue => issue.Code == "S7_TIA_HMI_WRITEABILITY_UNKNOWN");
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(speed.PortableAddress, out var speedBinding, out var speedError), speedError);
        Assert.Equal(S7IsoArea.Merker, speedBinding!.Area);
        Assert.Equal(4, speedBinding.ByteOffset);
        Assert.Equal(S7IsoValueType.Float32, speedBinding.ValueType);
        Assert.False(speedBinding.Writable);

        var dbValue = Assert.Single(candidates, candidate => candidate.DisplayName == "DbValue");
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(dbValue.PortableAddress, out var dbBinding, out var dbError), dbError);
        Assert.Equal(S7IsoArea.DataBlock, dbBinding!.Area);
        Assert.Equal((ushort)1, dbBinding.DbNumber);
        Assert.Equal(10, dbBinding.ByteOffset);
        Assert.Equal(S7IsoValueType.Int16, dbBinding.ValueType);
        Assert.True(dbBinding.Writable);

        var germanInput = Assert.Single(candidates, candidate => candidate.DisplayName == "GermanInput");
        Assert.True(germanInput.IsReadable);
        Assert.False(germanInput.IsWritable);
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(germanInput.PortableAddress, out var inputBinding, out var inputError), inputError);
        Assert.Equal(S7IsoArea.Input, inputBinding!.Area);
        Assert.Equal(0, inputBinding.ByteOffset);
        Assert.Equal((byte)1, inputBinding.BitOffset);
        Assert.False(inputBinding.Writable);

        var structured = Assert.Single(candidates, candidate => candidate.DisplayName == "Structured");
        Assert.False(structured.IsReadable);
        Assert.False(structured.IsWritable);
        Assert.Null(structured.SuggestedDataType);
        Assert.Contains(structured.Issues!, issue => issue.Code == "S7_TIA_DATATYPE_UNSUPPORTED");
        Assert.Equal("Unsupported", structured.Metadata!["supportStatus"]);
        Assert.StartsWith("tia-xlsx:unsupported:", structured.PortableAddress);
    }

    [Fact]
    public async Task ImportXlsx_MalformedBooleanIsCandidateErrorWithoutPoisoningOtherRows()
    {
        using var workbook = CreateWorkbook(runWritable: "MAYBE");
        var request = new DriverImportRequest(null, "plc-tags.xlsx");

        var candidates = await ImportAsync(new S7IsoEngineeringAdapter(), request, workbook);

        var run = Assert.Single(candidates, candidate => candidate.DisplayName == "Run");
        Assert.False(run.IsReadable);
        Assert.False(run.IsWritable);
        Assert.Equal("Unsupported", run.Metadata!["supportStatus"]);
        Assert.Equal("hmiWriteable", run.Metadata["invalidBooleanFields"]);
        Assert.Contains(run.Issues!, issue => issue.Code == "S7_TIA_BOOLEAN_INVALID");
        Assert.DoesNotContain(run.Issues!, issue => issue.Code == "S7_TIA_HMI_WRITEABILITY_UNKNOWN");

        var dbValue = Assert.Single(candidates, candidate => candidate.DisplayName == "DbValue");
        Assert.True(dbValue.IsReadable);
        Assert.True(dbValue.IsWritable);
        Assert.DoesNotContain(dbValue.Issues ?? Array.Empty<DriverEngineeringIssue>(), issue =>
            issue.Code == "S7_TIA_BOOLEAN_INVALID");
    }

    [Fact]
    public async Task ImportXlsx_AddressMoveChangesBindingButNotStableSymbolIdentity()
    {
        using var originalWorkbook = CreateWorkbook("%MD4");
        using var movedWorkbook = CreateWorkbook("%MD40");
        var request = new DriverImportRequest(null, "plc-tags.xlsx");

        var original = Assert.Single(
            await ImportAsync(new S7IsoEngineeringAdapter(), request, originalWorkbook),
            candidate => candidate.DisplayName == "Speed");
        var moved = Assert.Single(
            await ImportAsync(new S7IsoEngineeringAdapter(), request, movedWorkbook),
            candidate => candidate.DisplayName == "Speed");

        Assert.Equal(original.StableIdentity, moved.StableIdentity);
        Assert.Equal(original.CandidateId, moved.CandidateId);
        Assert.NotEqual(original.PortableAddress, moved.PortableAddress);
        Assert.Equal("%MD4", original.Metadata!["logicalAddress"]);
        Assert.Equal("%MD40", moved.Metadata!["logicalAddress"]);
    }

    [Fact]
    public async Task ImportUnknownFormat_ReturnsExplicitUnsupportedFormatCandidate()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("name,address"));
        var request = new DriverImportRequest(null, "plc-tags.csv", "text/csv");

        var candidates = await ImportAsync(new S7IsoEngineeringAdapter(), request, content);

        var candidate = Assert.Single(candidates);
        Assert.False(candidate.IsReadable);
        Assert.Contains(candidate.Issues!, issue => issue.Code == "S7_TIA_FORMAT_NOT_IMPLEMENTED");
        Assert.Equal("xlsx,xml,sdf", candidate.Metadata!["supportedFormats"]);
    }

    [Fact]
    public async Task ImportInvalidXlsx_ReturnsSanitizedParseIssue()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("not-an-xlsx"));
        var request = new DriverImportRequest(null, "broken.xlsx");

        var candidates = await ImportAsync(new S7IsoEngineeringAdapter(), request, content);

        var candidate = Assert.Single(candidates);
        Assert.False(candidate.IsReadable);
        var issue = Assert.Single(candidate.Issues!);
        Assert.Equal("S7_TIA_XLSX_INVALID", issue.Code);
        Assert.DoesNotContain('\n', issue.Message);
        Assert.DoesNotContain('\r', issue.Message);
    }

    private static async Task<IReadOnlyList<DriverImportCandidate>> ImportAsync(
        S7IsoEngineeringAdapter adapter,
        DriverImportRequest request,
        Stream content)
    {
        var result = new List<DriverImportCandidate>();
        await foreach (var candidate in adapter.ImportAsync(request, content))
            result.Add(candidate);
        return result;
    }

    private static MemoryStream CreateWorkbook(string speedAddress = "%MD4", string runWritable = "TRUE")
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(
                archive,
                "xl/workbook.xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="PLCTags" sheetId="1" r:id="rId1" /></sheets>
                </workbook>
                """);
            WriteEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1"
                                Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"
                                Target="worksheets/sheet1.xml" />
                </Relationships>
                """);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", CreateSheet(speedAddress, runWritable).ToString(SaveOptions.DisableFormatting));
        }
        stream.Position = 0;
        return stream;
    }

    private static XDocument CreateSheet(string speedAddress, string runWritable)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new[]
        {
            new[] { "Name", "Path", "Data Type", "Logical Address", "Comment", "Hmi Visible", "Hmi Accessible", "Hmi Writeable" },
            new[] { "Run", "Main/Tags", "Bool", "%M0.0", "Run flag", "TRUE", "TRUE", runWritable },
            new[] { "Speed", "Main/Tags", "Real", speedAddress, "Speed", "TRUE", "TRUE", "<no value>" },
            new[] { "DbValue", "Main/Tags", "Int", "%DB1.DBW10", "DB value", "TRUE", "TRUE", "TRUE" },
            new[] { "GermanInput", "Main/Tags", "Bool", "%E0.1", "Input using German mnemonic", "TRUE", "TRUE", "TRUE" },
            new[] { "Structured", "Main/Tags", "MyUdt", "%MD8", "Unsupported UDT", "TRUE", "TRUE", "TRUE" }
        };

        var sheetData = new XElement(ns + "sheetData");
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = new XElement(ns + "row", new XAttribute("r", rowIndex + 1));
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                var reference = $"{ColumnName(columnIndex)}{rowIndex + 1}";
                row.Add(new XElement(
                    ns + "c",
                    new XAttribute("r", reference),
                    new XAttribute("t", "inlineStr"),
                    new XElement(ns + "is", new XElement(ns + "t", rows[rowIndex][columnIndex]))));
            }
            sheetData.Add(row);
        }

        return new XDocument(new XElement(ns + "worksheet", sheetData));
    }

    private static string ColumnName(int zeroBased)
    {
        var value = zeroBased + 1;
        var builder = new StringBuilder();
        while (value > 0)
        {
            value--;
            builder.Insert(0, (char)('A' + value % 26));
            value /= 26;
        }
        return builder.ToString();
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
