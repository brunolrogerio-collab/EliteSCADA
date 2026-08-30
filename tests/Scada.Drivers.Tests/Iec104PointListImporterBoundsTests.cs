using System.Text;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104PointListImporterBoundsTests
{
    [Fact]
    public async Task ImportAsync_RejectsDataRowsBeyondConfiguredMaximum()
    {
        const string csv =
            "commonAddress,informationObjectAddress,typeId,displayName\n" +
            "1,1,M_SP_NA_1,One\n" +
            "1,2,M_SP_NA_1,Two\n" +
            "1,3,M_SP_NA_1,Three\n";
        var request = Request(new Dictionary<string, string>
        {
            ["maximumRows"] = "2"
        });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CollectAsync(csv, request));

        Assert.Contains("maximumRows=2", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_RejectsLineBeyondConfiguredMaximumLength()
    {
        const string csv =
            "commonAddress,informationObjectAddress,typeId,displayName\n" +
            "1,1,M_SP_NA_1,This display name is intentionally much too long\n";
        var request = Request(new Dictionary<string, string>
        {
            ["maximumLineLength"] = "56"
        });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CollectAsync(csv, request));

        Assert.Contains("maximumLineLength=56", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_RejectsSeekableFileBeyondConfiguredByteLimitBeforeParsing()
    {
        const string csv =
            "commonAddress,informationObjectAddress,typeId\n" +
            "1,1,M_SP_NA_1\n";
        var request = Request(new Dictionary<string, string>
        {
            ["maximumFileBytes"] = "32"
        });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CollectAsync(csv, request));

        Assert.Contains("maximumFileBytes=32", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_RejectsNulCharacter()
    {
        const string csv =
            "commonAddress,informationObjectAddress,typeId,displayName\n" +
            "1,1,M_SP_NA_1,Bad\0Name\n";

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CollectAsync(csv, Request()));

        Assert.Contains("NUL", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_BoundsSourceLineMetadataForManyDuplicateRows()
    {
        var csv = new StringBuilder("commonAddress,informationObjectAddress,typeId,displayName\n");
        for (var index = 0; index < 70; index++)
            csv.AppendLine("1,77,M_SP_NA_1,Repeated");

        var candidates = await CollectAsync(csv.ToString(), Request());

        var candidate = Assert.Single(candidates);
        Assert.Equal("70", candidate.Metadata!["sourceRowCount"]);
        Assert.Equal("true", candidate.Metadata["sourceLinesTruncated"]);
        Assert.Equal(64, candidate.Metadata["sourceLines"].Split(',').Length);
        Assert.Contains(candidate.Issues!, static issue => issue.Code == "iec104.import.duplicate");
        Assert.Contains(candidate.Issues!, static issue => issue.Code == "iec104.import.sourceLinesTruncated");
    }

    [Fact]
    public async Task ImportAsync_RejectsLimitParameterOutsideHardCap()
    {
        var request = Request(new Dictionary<string, string>
        {
            ["maximumRows"] = "1000001"
        });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CollectAsync("commonAddress,informationObjectAddress,typeId\n", request));

        Assert.Contains("maximumRows", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_HonorsPreCancelledTokenBeforeReadingContent()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CollectAsync(
                "commonAddress,informationObjectAddress,typeId\n1,1,M_SP_NA_1\n",
                Request(),
                cts.Token));
    }

    private static DriverImportRequest Request(IReadOnlyDictionary<string, string>? parameters = null) =>
        new(
            Context: null,
            SourceName: "points.csv",
            ContentType: "text/csv",
            Parameters: parameters);

    private static async Task<List<DriverImportCandidate>> CollectAsync(
        string csv,
        DriverImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var importer = new Iec104PointListImporter();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = new List<DriverImportCandidate>();
        await foreach (var candidate in importer.ImportAsync(request, content, cancellationToken))
            result.Add(candidate);
        return result;
    }
}