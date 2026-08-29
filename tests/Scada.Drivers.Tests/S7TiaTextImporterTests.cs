using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7TiaTextImporterTests
{
    [Fact]
    public async Task ImportXml_ParsesOfficialTagAttributesAndPreservesConstants()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Tagtable name="Default tag table">
              <Tag type="Bool" hmiVisible="True" hmiWriteable="True" hmiAccessible="True" retain="False" remark="Run flag" addr="%M0.0">Run</Tag>
              <Tag type="Real" hmiVisible="True" hmiAccessible="True" retain="False" remark="Speed" addr="%MD4">Speed</Tag>
              <Tag type="MyUdt" hmiVisible="True" hmiWriteable="True" hmiAccessible="True" retain="False" remark="Structured" addr="%MD8">Structured</Tag>
              <Constant type="Int" remark="Constant example" value="222">Const_1</Constant>
            </Tagtable>
            """;
        using var content = Utf8(xml);
        var request = new DriverImportRequest(null, "plc-tags.xml", "application/xml");

        var candidates = await ImportAsync(request, content);

        Assert.Equal(4, candidates.Count);

        var run = Assert.Single(candidates, candidate => candidate.DisplayName == "Run");
        Assert.True(run.IsReadable);
        Assert.True(run.IsWritable);
        Assert.Equal(TagDataType.Boolean, run.SuggestedDataType);
        Assert.Equal("TiaXml", run.Metadata!["sourceKind"]);
        Assert.Equal("Default tag table", run.Metadata["tiaPath"]);
        Assert.Equal("false", run.Metadata["retain"]);
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(run.PortableAddress, out var runBinding, out var runError), runError);
        Assert.Equal(S7IsoArea.Merker, runBinding!.Area);
        Assert.True(runBinding.Writable);

        var speed = Assert.Single(candidates, candidate => candidate.DisplayName == "Speed");
        Assert.True(speed.IsReadable);
        Assert.False(speed.IsWritable);
        Assert.Equal(TagDataType.Float, speed.SuggestedDataType);
        Assert.Contains(speed.Issues!, issue => issue.Code == "S7_TIA_HMI_WRITEABILITY_UNKNOWN");

        var structured = Assert.Single(candidates, candidate => candidate.DisplayName == "Structured");
        Assert.False(structured.IsReadable);
        Assert.Contains(structured.Issues!, issue => issue.Code == "S7_TIA_DATATYPE_UNSUPPORTED");
        Assert.StartsWith("tia-xml:unsupported:", structured.PortableAddress);

        var constant = Assert.Single(candidates, candidate => candidate.DisplayName == "Const_1");
        Assert.False(constant.IsReadable);
        Assert.False(constant.IsWritable);
        Assert.Equal("Constant", constant.Metadata!["entityKind"]);
        Assert.Equal("222", constant.Metadata["constantValue"]);
        Assert.Contains(constant.Issues!, issue => issue.Code == "S7_TIA_CONSTANT_UNSUPPORTED");
        Assert.StartsWith("tia-xml:unsupported:", constant.PortableAddress);
    }

    [Fact]
    public async Task ImportSdf_ParsesOfficialPositionalColumnsQuotedFieldsAndConstants()
    {
        const string sdf = """
            "Run","%M0.0","Bool","True","True","False","Run flag","","True"
            "Speed","%MD4","Real","True","True","False","Speed, rpm","",""
            "Input","%I0.1","Bool","True","True","False","Physical input","","True"
            "Const_1","CONSTANT","Int","","","","Constant example","222",""
            """;
        using var content = Utf8(sdf);
        var request = new DriverImportRequest(null, "plc-tags.sdf", "text/plain");

        var candidates = await ImportAsync(request, content);

        Assert.Equal(4, candidates.Count);
        var run = Assert.Single(candidates, candidate => candidate.DisplayName == "Run");
        Assert.True(run.IsReadable);
        Assert.True(run.IsWritable);
        Assert.Equal("TiaSdf", run.Metadata!["sourceKind"]);
        Assert.Equal("Run flag", run.Metadata["comment"]);

        var speed = Assert.Single(candidates, candidate => candidate.DisplayName == "Speed");
        Assert.True(speed.IsReadable);
        Assert.False(speed.IsWritable);
        Assert.Equal("Speed, rpm", speed.Metadata!["comment"]);
        Assert.Contains(speed.Issues!, issue => issue.Code == "S7_TIA_HMI_WRITEABILITY_UNKNOWN");

        var input = Assert.Single(candidates, candidate => candidate.DisplayName == "Input");
        Assert.True(input.IsReadable);
        Assert.False(input.IsWritable);
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(input.PortableAddress, out var inputBinding, out var inputError), inputError);
        Assert.Equal(S7IsoArea.Input, inputBinding!.Area);
        Assert.False(inputBinding.Writable);

        var constant = Assert.Single(candidates, candidate => candidate.DisplayName == "Const_1");
        Assert.False(constant.IsReadable);
        Assert.Equal("222", constant.Metadata!["constantValue"]);
        Assert.Contains(constant.Issues!, issue => issue.Code == "S7_TIA_CONSTANT_UNSUPPORTED");
        Assert.StartsWith("tia-sdf:unsupported:", constant.PortableAddress);
    }

    [Fact]
    public async Task ImportSdf_OptionalOfficialHeaderRowIsAccepted()
    {
        const string sdf = """
            "Name","Address","Data type","Accessible from HMI/OPC UA","Visible in HMI Engineering","Retain","Comment","Value (in case of constant)","Writable from HMI/OPC UA"
            "DbValue","%DB1.DBW10","Int","True","True","False","DB value","","True"
            """;
        using var content = Utf8(sdf);
        var request = new DriverImportRequest(null, "plc-tags.sdf");

        var candidate = Assert.Single(await ImportAsync(request, content));

        Assert.True(candidate.IsReadable);
        Assert.True(candidate.IsWritable);
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(candidate.PortableAddress, out var binding, out var error), error);
        Assert.Equal(S7IsoArea.DataBlock, binding!.Area);
        Assert.Equal((ushort)1, binding.DbNumber);
        Assert.Equal(10, binding.ByteOffset);
        Assert.Equal(S7IsoValueType.Int16, binding.ValueType);
    }

    [Fact]
    public async Task ImportInvalidXml_ReturnsFormatSpecificSanitizedIssue()
    {
        using var content = Utf8("<Tags><Tag></Tags>");
        var request = new DriverImportRequest(null, "broken.xml", "application/xml");

        var candidate = Assert.Single(await ImportAsync(request, content));

        Assert.False(candidate.IsReadable);
        var issue = Assert.Single(candidate.Issues!);
        Assert.Equal("S7_TIA_XML_INVALID", issue.Code);
        Assert.DoesNotContain('\n', issue.Message);
        Assert.DoesNotContain('\r', issue.Message);
    }

    [Fact]
    public async Task ImportInvalidSdf_ReturnsFormatSpecificSanitizedIssue()
    {
        using var content = Utf8("\"Run\",\"%M0.0\",\"Bool");
        var request = new DriverImportRequest(null, "broken.sdf");

        var candidate = Assert.Single(await ImportAsync(request, content));

        Assert.False(candidate.IsReadable);
        var issue = Assert.Single(candidate.Issues!);
        Assert.Equal("S7_TIA_SDF_INVALID", issue.Code);
        Assert.DoesNotContain('\n', issue.Message);
        Assert.DoesNotContain('\r', issue.Message);
    }

    private static async Task<IReadOnlyList<DriverImportCandidate>> ImportAsync(
        DriverImportRequest request,
        Stream content)
    {
        var result = new List<DriverImportCandidate>();
        await foreach (var candidate in new S7IsoEngineeringAdapter().ImportAsync(request, content))
            result.Add(candidate);
        return result;
    }

    private static MemoryStream Utf8(string value) => new(Encoding.UTF8.GetBytes(value));
}
