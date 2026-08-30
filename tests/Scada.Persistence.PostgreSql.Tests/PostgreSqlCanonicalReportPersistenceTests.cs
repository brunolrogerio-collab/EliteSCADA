using System.Text.Json;
using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlCanonicalReportPersistenceTests
{
    [Fact]
    public async Task RevisionPersistence_PreservesCanonicalReportPayloadAndImmutableHistory()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();

        var projectKey = $"report-v14-{Guid.NewGuid():N}";
        var reportId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var controlId = Guid.NewGuid();
        var firstJson = CanonicalPackage(reportId, sectionId, controlId, "Process History A", "9223372036854775807");
        var secondJson = CanonicalPackage(reportId, sectionId, controlId, "Process History B", "-9223372036854775808");

        var first = await store.SaveAsync(
            projectKey,
            "Report Persistence",
            "scada.engineering",
            14,
            firstJson,
            "wave-09-test");
        var second = await store.SaveAsync(
            projectKey,
            "Report Persistence",
            "scada.engineering",
            14,
            secondJson,
            "wave-09-test");

        var storedFirst = await store.LoadRevisionAsync(projectKey, first.Revision);
        var storedSecond = await store.LoadRevisionAsync(projectKey, second.Revision);
        var latest = await store.LoadLatestAsync(projectKey);

        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.NotNull(latest);
        Assert.Equal(14, storedFirst!.EngineeringSchemaVersion);
        Assert.Equal(14, storedSecond!.EngineeringSchemaVersion);
        Assert.Equal(second.Revision, latest!.Revision);

        AssertReport(storedFirst.EngineeringJson, reportId, sectionId, controlId, "Process History A", "9223372036854775807");
        AssertReport(storedSecond.EngineeringJson, reportId, sectionId, controlId, "Process History B", "-9223372036854775808");
        AssertReport(latest.EngineeringJson, reportId, sectionId, controlId, "Process History B", "-9223372036854775808");
    }

    private static void AssertReport(
        string json,
        Guid reportId,
        Guid sectionId,
        Guid controlId,
        string name,
        string int64Value)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(14, root.GetProperty("schemaVersion").GetInt32());

        var reports = root.GetProperty("reports");
        Assert.Equal(1, reports.GetArrayLength());
        var report = reports[0];
        Assert.Equal(reportId, report.GetProperty("id").GetGuid());
        Assert.Equal("process.history", report.GetProperty("key").GetString());
        Assert.Equal(name, report.GetProperty("name").GetString());

        var parameter = report.GetProperty("parameters")[0];
        Assert.Equal("counter", parameter.GetProperty("key").GetString());
        Assert.Equal("int64", parameter.GetProperty("type").GetString());
        Assert.Equal(int64Value, parameter.GetProperty("defaultValue").GetProperty("value").GetString());

        var query = report.GetProperty("queries")[0].GetProperty("query");
        Assert.Equal("historian.samples", query.GetProperty("dataset").GetString());
        Assert.False(query.GetProperty("page").TryGetProperty("cursor", out var cursor) && cursor.ValueKind != JsonValueKind.Null);

        var section = report.GetProperty("sections")[0];
        Assert.Equal(sectionId, section.GetProperty("id").GetGuid());
        Assert.Equal(controlId, section.GetProperty("controls")[0].GetProperty("id").GetGuid());
    }

    private static string CanonicalPackage(
        Guid reportId,
        Guid sectionId,
        Guid controlId,
        string name,
        string int64Value) => $$"""
        {
          "schema": "scada.engineering",
          "schemaVersion": 14,
          "exportedAt": "2026-08-29T00:00:00Z",
          "tags": [],
          "alarms": [],
          "dataSources": [],
          "templates": [],
          "equipment": [],
          "dynamos": [],
          "screens": [],
          "popups": [],
          "securityRoles": [],
          "commands": [],
          "gateways": [],
          "scripts": [],
          "scriptVisualEventReferences": [],
          "visualAssets": [],
          "reports": [
            {
              "id": "{{reportId:D}}",
              "key": "process.history",
              "name": "{{name}}",
              "page": {
                "paperSizeKey": "A4",
                "orientation": "portrait",
                "marginTopMillimeters": 10,
                "marginRightMillimeters": 10,
                "marginBottomMillimeters": 10,
                "marginLeftMillimeters": 10,
                "showPageNumbers": true
              },
              "parameters": [
                {
                  "key": "counter",
                  "name": "Counter",
                  "type": "int64",
                  "defaultValue": {
                    "type": "int64",
                    "value": "{{int64Value}}"
                  }
                }
              ],
              "queries": [
                {
                  "key": "history",
                  "query": {
                    "dataset": "historian.samples",
                    "range": {
                      "kind": "relative",
                      "durationSeconds": 3600
                    },
                    "filters": [],
                    "orderBy": [
                      {
                        "field": "timestamp",
                        "direction": "descending"
                      }
                    ],
                    "page": {
                      "size": 100,
                      "cursor": null
                    }
                  }
                }
              ],
              "sections": [
                {
                  "id": "{{sectionId:D}}",
                  "key": "detail",
                  "kind": "detail",
                  "heightMillimeters": 8,
                  "queryKey": "history",
                  "repeatOnNewPage": false,
                  "controls": [
                    {
                      "id": "{{controlId:D}}",
                      "key": "value",
                      "kind": "dataField",
                      "xMillimeters": 0,
                      "yMillimeters": 0,
                      "widthMillimeters": 50,
                      "heightMillimeters": 6,
                      "queryKey": "history",
                      "field": "value"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;
}
