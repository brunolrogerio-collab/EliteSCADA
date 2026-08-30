using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7TiaSIntImportTests
{
    [Fact]
    public void SharedFactory_MapsXlsxSIntToPortableSignedByteBinding()
    {
        var candidate = S7TiaImportCandidateFactory.Create(new S7TiaImportRecord(
            "TiaXlsx",
            "tags.xlsx",
            "SignedByte",
            "Main/Tags",
            "SInt",
            "%MB4",
            null,
            HmiVisible: true,
            HmiAccessible: true,
            HmiWriteable: true));

        Assert.True(candidate.IsReadable);
        Assert.True(candidate.IsWritable);
        Assert.Equal(TagDataType.Int16, candidate.SuggestedDataType);
        Assert.Equal("Supported", candidate.Metadata!["supportStatus"]);
        Assert.DoesNotContain(candidate.Issues ?? Array.Empty<DriverEngineeringIssue>(), issue =>
            issue.Code == "S7_TIA_DATATYPE_UNSUPPORTED");
        Assert.StartsWith("s7iso:v2;", candidate.PortableAddress);
        Assert.True(S7IsoCommunicationBindingSchemaV2.TryParsePortableAddress(candidate.PortableAddress, out var binding, out var error), error);
        Assert.Equal(S7IsoValueType.SInt, binding!.ValueType);
        Assert.Equal(S7IsoArea.Merker, binding.Area);
        Assert.Equal(4, binding.ByteOffset);
        Assert.True(binding.Writable);
    }

    [Fact]
    public async Task XmlImport_MapsSIntAndRejectsWordNotation()
    {
        const string xml = """
            <Tagtable name="Tags">
              <Tag type="SInt" hmiVisible="True" hmiAccessible="True" hmiWriteable="True" addr="%MB4">SignedByte</Tag>
              <Tag type="SInt" hmiVisible="True" hmiAccessible="True" hmiWriteable="True" addr="%MW6">WrongWidth</Tag>
            </Tagtable>
            """;
        using var content = Utf8(xml);

        var candidates = await ImportAsync(new DriverImportRequest(null, "tags.xml"), content);

        var good = Assert.Single(candidates, candidate => candidate.DisplayName == "SignedByte");
        var wrong = Assert.Single(candidates, candidate => candidate.DisplayName == "WrongWidth");
        Assert.True(good.IsReadable);
        Assert.StartsWith("s7iso:v2;", good.PortableAddress);
        Assert.True(S7IsoCommunicationBindingSchemaV2.TryParsePortableAddress(good.PortableAddress, out var binding, out var error), error);
        Assert.Equal(S7IsoValueType.SInt, binding!.ValueType);
        Assert.False(wrong.IsReadable);
        Assert.Contains(wrong.Issues!, issue => issue.Code == "S7_TIA_ADDRESS_WIDTH_MISMATCH");
    }

    [Fact]
    public async Task SdfImport_MapsSIntToByteBinding()
    {
        const string sdf = "\"SignedByte\",\"%MB4\",\"SInt\",\"True\",\"True\",\"False\",\"Signed byte\",\"\",\"True\"";
        using var content = Utf8(sdf);

        var candidate = Assert.Single(await ImportAsync(new DriverImportRequest(null, "tags.sdf"), content));

        Assert.True(candidate.IsReadable);
        Assert.True(candidate.IsWritable);
        Assert.Equal(TagDataType.Int16, candidate.SuggestedDataType);
        Assert.StartsWith("s7iso:v2;", candidate.PortableAddress);
        Assert.True(S7IsoCommunicationBindingSchemaV2.TryParsePortableAddress(candidate.PortableAddress, out var binding, out var error), error);
        Assert.Equal(S7IsoValueType.SInt, binding!.ValueType);
        Assert.Equal(4, binding.ByteOffset);
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
