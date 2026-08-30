using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7TiaDateImportTests
{
    [Fact]
    public void SharedFactory_MapsXlsxDateToPortableWordBinding()
    {
        var candidate = S7TiaImportCandidateFactory.Create(new S7TiaImportRecord(
            "TiaXlsx",
            "tags.xlsx",
            "ProductionDate",
            "Main/Tags",
            "Date",
            "%MW20",
            null,
            HmiVisible: true,
            HmiAccessible: true,
            HmiWriteable: true));

        Assert.True(candidate.IsReadable);
        Assert.True(candidate.IsWritable);
        Assert.Equal(TagDataType.DateTime, candidate.SuggestedDataType);
        Assert.Equal("Supported", candidate.Metadata!["supportStatus"]);
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(candidate.PortableAddress, out var binding, out var error), error);
        Assert.Equal(S7IsoValueType.Date, binding!.ValueType);
        Assert.Equal(20, binding.ByteOffset);
    }

    [Fact]
    public async Task XmlImport_DateUsesWordNotationAndRejectsDoubleWord()
    {
        const string xml = """
            <Tagtable name="Tags">
              <Tag type="Date" hmiVisible="True" hmiAccessible="True" hmiWriteable="True" addr="%MW20">ProductionDate</Tag>
              <Tag type="Date" hmiVisible="True" hmiAccessible="True" hmiWriteable="True" addr="%MD24">WrongWidth</Tag>
            </Tagtable>
            """;
        using var content = Utf8(xml);

        var candidates = await ImportAsync(new DriverImportRequest(null, "tags.xml"), content);

        var good = Assert.Single(candidates, candidate => candidate.DisplayName == "ProductionDate");
        var wrong = Assert.Single(candidates, candidate => candidate.DisplayName == "WrongWidth");
        Assert.True(good.IsReadable);
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(good.PortableAddress, out var binding, out var error), error);
        Assert.Equal(S7IsoValueType.Date, binding!.ValueType);
        Assert.False(wrong.IsReadable);
        Assert.Contains(wrong.Issues!, issue => issue.Code == "S7_TIA_ADDRESS_WIDTH_MISMATCH");
    }

    [Fact]
    public async Task SdfImport_DateMapsToCanonicalDateTime()
    {
        const string sdf = "\"ProductionDate\",\"%MW20\",\"Date\",\"True\",\"True\",\"False\",\"Production date\",\"\",\"True\"";
        using var content = Utf8(sdf);

        var candidate = Assert.Single(await ImportAsync(new DriverImportRequest(null, "tags.sdf"), content));

        Assert.True(candidate.IsReadable);
        Assert.True(candidate.IsWritable);
        Assert.Equal(TagDataType.DateTime, candidate.SuggestedDataType);
        Assert.True(S7IsoTagBinding.TryParsePortableAddress(candidate.PortableAddress, out var binding, out var error), error);
        Assert.Equal(S7IsoValueType.Date, binding!.ValueType);
    }

    [Theory]
    [InlineData("%MW20", true)]
    [InlineData("%DB4.DBW20", true)]
    [InlineData("%MD20", false)]
    [InlineData("%DB4.DBD20", false)]
    public void DateWidth_RequiresWordNotation(string address, bool expected)
    {
        var valid = S7TiaImportValidation.TryValidateAddressWidth(address, S7IsoValueType.Date, out var error);

        Assert.Equal(expected, valid);
        if (expected) Assert.Null(error);
        else Assert.NotNull(error);
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
