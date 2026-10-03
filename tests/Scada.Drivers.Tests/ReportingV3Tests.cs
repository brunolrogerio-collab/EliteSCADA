using System.IO.Compression;
using System.Text;
using Scada.Api.Reports;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Reports;

namespace Scada.Drivers.Tests;

public sealed class ReportingV3Tests
{
    [Fact]
    public void Validation_RejectsWideRawAndNonUtcCalendarAlignment()
    {
        var report = Report() with
        {
            TableLayout = ReportTableLayout.Wide,
            Resolution = new ReportDataResolutionEngineeringDto(
                ReportDataResolutionMode.Raw,
                BucketAlignment: "localCalendar")
        };

        var problems = ReportEngineeringValidation.Validate(report);

        Assert.Contains(problems, p => p.Code == "REPORT_WIDE_RAW_UNSUPPORTED");
        Assert.Contains(problems, p => p.Code == "REPORT_BUCKET_ALIGNMENT_UNSUPPORTED");
    }

    [Fact]
    public void UnitLabelOverride_IsPresentationOnlyAndStableTagIdentityPersists()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var report = Report() with
        {
            Variables =
            [
                new ReportVariableEngineeringDto(
                    id,
                    "Area.Pressure",
                    "Pressure",
                    "Double",
                    "kPa",
                    "driver.modbus",
                    "Process pressure",
                    UnitMode: ReportUnitMode.LabelOverride,
                    UnitLabel: "Pressão")
            ]
        };

        var variable = Assert.Single(report.Variables!);
        Assert.Equal(id, variable.TagId);
        Assert.Equal("kPa", variable.EngineeringUnit);
        Assert.Equal("Pressão", variable.UnitLabel);
        Assert.Equal(ReportUnitMode.LabelOverride, variable.UnitMode);
        Assert.Empty(ReportEngineeringValidation.Validate(report));
    }

    [Fact]
    public void ArtifactRenderer_ProducesPdfTypedXlsxAndUtf8CsvFromOneSnapshot()
    {
        var at = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        var columns = new[]
        {
            Column("number", HistoricalFieldType.Number),
            Column("when", HistoricalFieldType.DateTime),
            Column("enabled", HistoricalFieldType.Boolean),
            Column("message", HistoricalFieldType.String)
        };
        var row = new HistoricalQueryRow(new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            ["number"] = HistoricalQueryValue.FromDouble(12.5),
            ["when"] = HistoricalQueryValue.FromDateTime(at),
            ["enabled"] = HistoricalQueryValue.FromBoolean(true),
            ["message"] = HistoricalQueryValue.FromString("quoted, \"value\"")
        });
        var result = new ReportExecutionResult(
            Guid.NewGuid(),
            "v3",
            new Dictionary<string, ReportParameterValue>(),
            [new ReportQueryExecutionResult("main", HistoricalDatasets.HistorianSamples, columns, [row], at.AddHours(-1), at)],
            at);
        var snapshot = new ReportGeneratedSnapshot(Guid.NewGuid(), null, Report(), null, result, at);
        var renderer = new ReportArtifactRenderer();

        var pdf = renderer.Render(snapshot, "pdf")!;
        var xlsx = renderer.Render(snapshot, "xlsx")!;
        var csv = renderer.Render(snapshot, "csv")!;

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf.Content, 0, 5));
        Assert.Equal("application/pdf", pdf.ContentType);

        using var archive = new ZipArchive(new MemoryStream(xlsx.Content), ZipArchiveMode.Read);
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);
        using var reader = new StreamReader(sheet!.Open(), Encoding.UTF8);
        var xml = reader.ReadToEnd();
        Assert.Contains("<v>12.5</v>", xml);
        Assert.Contains("t=\"b\"><v>1</v>", xml);
        Assert.Contains("s=\"1\"><v>", xml);
        Assert.Contains("quoted, &quot;value&quot;", xml);

        var csvText = Encoding.UTF8.GetString(csv.Content);
        Assert.StartsWith("\uFEFF", csvText);
        Assert.Contains("12.5", csvText);
        Assert.Contains("\"quoted, \"\"value\"\"\"", csvText);
    }

    [Fact]
    public void GeneratedStore_IsOwnerBound()
    {
        var at = DateTimeOffset.UtcNow;
        var result = new ReportExecutionResult(
            null, "v3", new Dictionary<string, ReportParameterValue>(),
            Array.Empty<ReportQueryExecutionResult>(), at);
        var store = new ReportGeneratedExecutionStore();
        var snapshot = store.Add("operator-a", Report(), null, result);

        Assert.True(store.TryGet(snapshot.ExecutionId, "operator-a", out _));
        Assert.False(store.TryGet(snapshot.ExecutionId, "operator-b", out _));
    }

    private static ReportEngineeringDto Report() =>
        new(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            "process.summary",
            "Process Summary",
            Queries:
            [
                new ReportQueryEngineeringDto(
                    "main",
                    new HistoricalQueryRequest(
                        HistoricalDatasets.HistorianSamples,
                        HistoricalTimeRange.Relative(3600),
                        Page: new HistoricalPageRequest(100)))
            ],
            Sections:
            [
                new ReportSectionEngineeringDto(
                    Guid.NewGuid(),
                    "detail",
                    ReportSectionKind.Detail,
                    8,
                    "main",
                    Controls:
                    [
                        new ReportControlEngineeringDto(
                            Guid.NewGuid(), "value", ReportControlKind.DataField,
                            0, 0, 40, 6, QueryKey: "main", Field: "value")
                    ])
            ]);

    private static HistoricalColumn Column(string field, HistoricalFieldType type) =>
        new(field, type, Array.Empty<HistoricalFilterOperator>(), false, false, false);
}
