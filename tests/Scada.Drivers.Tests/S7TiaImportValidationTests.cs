using System.Text;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7TiaImportValidationTests
{
    [Theory]
    [InlineData("%M0.0", S7IsoValueType.Boolean, true)]
    [InlineData("%MB4", S7IsoValueType.Byte, true)]
    [InlineData("%MW4", S7IsoValueType.Int16, true)]
    [InlineData("%MD4", S7IsoValueType.Float32, true)]
    [InlineData("%MW4", S7IsoValueType.Float32, false)]
    [InlineData("%MD4", S7IsoValueType.Int16, false)]
    [InlineData("%DB1.DBW10", S7IsoValueType.Int16, true)]
    [InlineData("%DB1.DBD10", S7IsoValueType.Int16, false)]
    public void AddressWidth_RequiresCompatibleClassicTypeNotation(
        string address,
        S7IsoValueType valueType,
        bool expected)
    {
        var valid = S7TiaImportValidation.TryValidateAddressWidth(address, valueType, out var error);

        Assert.Equal(expected, valid);
        if (expected) Assert.Null(error);
        else Assert.Contains("requires", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task XmlImport_WidthMismatchRemainsVisibleButUnsupported()
    {
        const string xml = """
            <Tagtable name="Tags">
              <Tag type="Real" hmiVisible="True" hmiWriteable="True" hmiAccessible="True" addr="%MW4">Speed</Tag>
            </Tagtable>
            """;
        using var content = Utf8(xml);

        var candidate = Assert.Single(await ImportAsync(new DriverImportRequest(null, "tags.xml"), content));

        Assert.False(candidate.IsReadable);
        Assert.False(candidate.IsWritable);
        Assert.Equal("Unsupported", candidate.Metadata!["supportStatus"]);
        Assert.Equal("%MW4", candidate.Metadata["logicalAddress"]);
        Assert.Contains(candidate.Issues!, issue => issue.Code == "S7_TIA_ADDRESS_WIDTH_MISMATCH");
        Assert.StartsWith("tia-xml:unsupported:", candidate.PortableAddress);
    }

    [Fact]
    public async Task SdfImport_WidthMismatchRemainsVisibleButUnsupported()
    {
        const string sdf = "\"Speed\",\"%MW4\",\"Real\",\"True\",\"True\",\"False\",\"Speed\",\"\",\"True\"";
        using var content = Utf8(sdf);

        var candidate = Assert.Single(await ImportAsync(new DriverImportRequest(null, "tags.sdf"), content));

        Assert.False(candidate.IsReadable);
        Assert.False(candidate.IsWritable);
        Assert.Contains(candidate.Issues!, issue => issue.Code == "S7_TIA_ADDRESS_WIDTH_MISMATCH");
        Assert.StartsWith("tia-sdf:unsupported:", candidate.PortableAddress);
    }

    [Fact]
    public async Task StableIdentity_SeparatesDifferentSourceExportsWithoutUsingAddress()
    {
        const string first = """
            <Tagtable name="Tags">
              <Tag type="Real" hmiVisible="True" hmiAccessible="True" addr="%MD4">Speed</Tag>
            </Tagtable>
            """;
        const string second = """
            <Tagtable name="Tags">
              <Tag type="Real" hmiVisible="True" hmiAccessible="True" addr="%MD40">Speed</Tag>
            </Tagtable>
            """;

        using var sourceAOriginal = Utf8(first);
        using var sourceAMoved = Utf8(second);
        using var sourceB = Utf8(first);
        var original = Assert.Single(await ImportAsync(new DriverImportRequest(null, "project-a.xml"), sourceAOriginal));
        var moved = Assert.Single(await ImportAsync(new DriverImportRequest(null, "project-a.xml"), sourceAMoved));
        var otherSource = Assert.Single(await ImportAsync(new DriverImportRequest(null, "project-b.xml"), sourceB));

        Assert.Equal(original.StableIdentity, moved.StableIdentity);
        Assert.Equal(original.CandidateId, moved.CandidateId);
        Assert.NotEqual(original.PortableAddress, moved.PortableAddress);
        Assert.NotEqual(original.StableIdentity, otherSource.StableIdentity);
        Assert.NotEqual(original.CandidateId, otherSource.CandidateId);
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
