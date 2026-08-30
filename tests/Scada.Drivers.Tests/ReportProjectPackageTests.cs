using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Reports;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Drivers.Tests;

public sealed class ReportProjectPackageTests
{
    [Fact]
    public void EscadaPackage_PreservesAndReappliesCanonicalReport()
    {
        var eventBus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(eventBus);
        var reports = new InMemoryReportEngineeringRegistry();
        var exchange = CreateExchange(new InMemoryTagRegistry(), alarms, reports);
        var packages = new ProjectPackageService(exchange);
        var report = Report();
        reports.Upsert(report);

        var bytes = packages.Export("report-package", "Report Package");
        var inspected = packages.Inspect(bytes);

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, inspected.Manifest.EngineeringSchemaVersion);
        var packaged = Assert.Single(inspected.Engineering.Reports!);
        Assert.Equal(report.Id, packaged.Id);
        Assert.Equal(report.Key, packaged.Key);
        Assert.Equal(Assert.Single(report.Queries!).Query, Assert.Single(packaged.Queries!).Query);
        Assert.Equal("9223372036854775807", Assert.Single(packaged.Parameters!).DefaultValue.Value);

        reports.Clear();
        var preview = packages.Preview(bytes, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);
        Assert.Contains(preview.Items, x =>
            x.EntityKind == ImportEntityKind.Report &&
            x.EntityKey == report.Key &&
            x.Operation == ImportOperation.Create);

        var applied = packages.Apply(bytes, ImportMode.CreateAndUpdate);
        Assert.Empty(applied.Issues);
        var restored = Assert.Single(reports.SnapshotReports());
        Assert.Equal(report.Id, restored.Id);

        var reportSection = Assert.Single(report.Sections!);
        var restoredSection = Assert.Single(restored.Sections!);
        Assert.Equal(reportSection.Id, restoredSection.Id);
        Assert.Equal(Assert.Single(reportSection.Controls!).Id, Assert.Single(restoredSection.Controls!).Id);
        Assert.Equal(Assert.Single(report.Queries!).Query, Assert.Single(restored.Queries!).Query);
    }

    private static EngineeringExchangeService CreateExchange(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IReportEngineeringRegistry reports) =>
        new(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry(),
            new InMemoryGatewayEngineeringRegistry(),
            new InMemoryScriptEngineeringRegistry(),
            new InMemoryVisualAssetEngineeringRegistry(),
            reports);

    private static ReportEngineeringDto Report() =>
        new(
            Guid.NewGuid(),
            "process.history.package",
            "Process History Package",
            Parameters:
            [
                new(
                    "counter",
                    "Counter",
                    ReportParameterType.Int64,
                    ReportParameterValue.FromInt64(long.MaxValue))
            ],
            Queries:
            [
                new(
                    "history",
                    new HistoricalQueryRequest(
                        HistoricalDatasets.HistorianSamples,
                        HistoricalTimeRange.Relative(3600),
                        Page: new HistoricalPageRequest(100)))
            ],
            Sections:
            [
                new(
                    Guid.NewGuid(),
                    "detail",
                    ReportSectionKind.Detail,
                    8,
                    "history",
                    Controls:
                    [
                        new(
                            Guid.NewGuid(),
                            "value",
                            ReportControlKind.DataField,
                            0,
                            0,
                            50,
                            6,
                            QueryKey: "history",
                            Field: "value")
                    ])
            ]);
}
