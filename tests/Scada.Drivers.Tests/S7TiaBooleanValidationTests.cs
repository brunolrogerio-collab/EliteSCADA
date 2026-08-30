using System.Text;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7TiaBooleanValidationTests
{
    [Fact]
    public async Task Xml_MalformedHmiAccessibleIsExplicitCandidateError()
    {
        const string xml = """
            <Tagtable name="Tags">
              <Tag type="Int" hmiVisible="True" hmiAccessible="sometimes" hmiWriteable="True" addr="%MW4">BadFlag</Tag>
              <Tag type="Int" hmiVisible="True" hmiAccessible="True" hmiWriteable="True" addr="%MW6">Healthy</Tag>
            </Tagtable>
            """;
        using var content = Utf8(xml);

        var candidates = await ImportAsync(new DriverImportRequest(null, "tags.xml"), content);

        var bad = Assert.Single(candidates, candidate => candidate.DisplayName == "BadFlag");
        Assert.False(bad.IsReadable);
        Assert.False(bad.IsWritable);
        Assert.Equal("Unsupported", bad.Metadata!["supportStatus"]);
        Assert.Equal("hmiAccessible", bad.Metadata["invalidBooleanFields"]);
        Assert.Contains(bad.Issues!, issue => issue.Code == "S7_TIA_BOOLEAN_INVALID");

        var healthy = Assert.Single(candidates, candidate => candidate.DisplayName == "Healthy");
        Assert.True(healthy.IsReadable);
        Assert.True(healthy.IsWritable);
        Assert.Equal("Supported", healthy.Metadata!["supportStatus"]);
    }

    [Fact]
    public async Task Sdf_MalformedWritableIsErrorInsteadOfUnknownWriteability()
    {
        const string sdf = "\"BadFlag\",\"%MW4\",\"Int\",\"True\",\"True\",\"False\",\"Bad writable\",\"\",\"perhaps\"";
        using var content = Utf8(sdf);

        var candidate = Assert.Single(await ImportAsync(new DriverImportRequest(null, "tags.sdf"), content));

        Assert.False(candidate.IsReadable);
        Assert.False(candidate.IsWritable);
        Assert.Equal("hmiWriteable", candidate.Metadata!["invalidBooleanFields"]);
        Assert.Contains(candidate.Issues!, issue => issue.Code == "S7_TIA_BOOLEAN_INVALID");
        Assert.DoesNotContain(candidate.Issues!, issue => issue.Code == "S7_TIA_HMI_WRITEABILITY_UNKNOWN");
    }

    [Theory]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void ValidBooleanSpellingsRemainAccepted(string raw, bool expected)
    {
        var invalid = new List<string>();

        var parsed = S7TiaImportCandidateFactory.ParseOptionalBoolean(raw, "flag", invalid);

        Assert.Equal(expected, parsed);
        Assert.Empty(invalid);
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
