using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104PointListImporterTests
{
    [Fact]
    public void Descriptor_AnnouncesFileImportWithoutTagBindingSchema()
    {
        var importer = new Iec104PointListImporter();

        Assert.True(importer.Descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.FileImport));
        Assert.True(importer.Descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Browse));
        Assert.Empty(importer.Descriptor.ConfigurationSchema.TagBindingFields);
    }

    [Fact]
    public async Task ImportAsync_MapsNumericAndIecTypeNamesToReadOnlyCandidates()
    {
        const string csv = "commonAddress,informationObjectAddress,typeId,displayName\n" +
                           "1,77,1,Running\n" +
                           "2,100,M_ME_NC_1,\"Pressure, inlet\"\n";
        var importer = new Iec104PointListImporter();

        var candidates = await CollectAsync(importer.ImportAsync(
            CreateRequest(),
            CreateStream(csv)));

        Assert.Equal(2, candidates.Count);

        var discrete = candidates[0];
        Assert.Equal("ca=1;ioa=77", discrete.PortableAddress);
        Assert.Equal(discrete.PortableAddress, discrete.StableIdentity);
        Assert.Equal("Running", discrete.DisplayName);
        Assert.True(discrete.IsReadable);
        Assert.False(discrete.IsWritable);
        Assert.Equal(TagDataType.Boolean, discrete.SuggestedDataType);
        Assert.Equal("1", discrete.Metadata!["declaredTypeIds"]);
        Assert.Null(discrete.Issues);

        var analog = candidates[1];
        Assert.Equal("ca=2;ioa=100", analog.PortableAddress);
        Assert.Equal("Pressure, inlet", analog.DisplayName);
        Assert.Equal(TagDataType.Float, analog.SuggestedDataType);
        Assert.Equal(((byte)Iec104TypeId.MMeNc1).ToString(), analog.Metadata!["declaredTypeIds"]);
        Assert.Equal("MMeNc1", analog.Metadata["declaredTypeNames"]);
    }

    [Fact]
    public async Task ImportAsync_CollapsesDuplicateAddressAndFlagsTypeConflict()
    {
        const string csv = "commonAddress,informationObjectAddress,typeId,displayName\n" +
                           "1,10,M_SP_NA_1,Point 10\n" +
                           "1,10,M_DP_NA_1,Point 10 duplicate\n";
        var importer = new Iec104PointListImporter();

        var candidates = await CollectAsync(importer.ImportAsync(
            CreateRequest(),
            CreateStream(csv)));

        var candidate = Assert.Single(candidates);
        Assert.Equal("ca=1;ioa=10", candidate.PortableAddress);
        Assert.Null(candidate.SuggestedDataType);
        Assert.Equal("1,3", candidate.Metadata!["declaredTypeIds"]);
        Assert.Contains(candidate.Issues!, static issue => issue.Code == "iec104.import.duplicate");
        Assert.Contains(candidate.Issues!, static issue => issue.Code == "iec104.import.typeConflict");
        Assert.False(candidate.IsWritable);
    }

    [Fact]
    public async Task ImportAsync_RejectsCommandTypeAsMonitoredPoint()
    {
        const string csv = "commonAddress,informationObjectAddress,typeId\n" +
                           "1,20,C_SC_NA_1\n";
        var importer = new Iec104PointListImporter();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await CollectAsync(importer.ImportAsync(CreateRequest(), CreateStream(csv))));

        Assert.Contains("unsupported monitored Type ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_RejectsMissingRequiredHeader()
    {
        const string csv = "commonAddress,typeId\n1,M_SP_NA_1\n";
        var importer = new Iec104PointListImporter();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await CollectAsync(importer.ImportAsync(CreateRequest(), CreateStream(csv))));

        Assert.Contains("informationObjectAddress", exception.Message, StringComparison.Ordinal);
    }

    private static DriverImportRequest CreateRequest() =>
        new(
            Context: new DriverEngineeringDataSourceContext(
                DataSourceKey: "iec-test",
                DataSourceName: "IEC test",
                DriverType: Iec104EngineeringConnectionTester.DriverType,
                Settings: new Dictionary<string, string>(),
                SecretReferences: new Dictionary<string, string>()),
            SourceName: "points.csv",
            ContentType: "text/csv");

    private static MemoryStream CreateStream(string text) =>
        new(Encoding.UTF8.GetBytes(text), writable: false);

    private static async Task<List<DriverImportCandidate>> CollectAsync(
        IAsyncEnumerable<DriverImportCandidate> source)
    {
        var result = new List<DriverImportCandidate>();
        await foreach (var candidate in source)
            result.Add(candidate);
        return result;
    }
}
