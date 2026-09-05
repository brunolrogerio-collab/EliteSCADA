using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringBrowserVisualValidationTests
{
    [Theory]
    [InlineData(BuiltinVisualObjectSchemas.AlarmBrowserType, "alarm")]
    [InlineData(BuiltinVisualObjectSchemas.EventBrowserType, "event")]
    public void BuiltinSchema_RegistersBrowserObjectsWithCanonicalGeometry(string objectType, string expectedKind)
    {
        var schema = BuiltinVisualObjectSchemas.GetRequired(objectType);

        Assert.Equal(objectType, schema.ObjectTypeKey);
        Assert.True(schema.Declares("x"));
        Assert.True(schema.Declares("y"));
        Assert.True(schema.Declares("width"));
        Assert.True(schema.Declares("height"));
        Assert.False(schema.Declares(BuiltinVisualObjectSchemas.BrowserConfigProperty));
        Assert.Contains(expectedKind, objectType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AlarmBrowser_AcceptsNativeConfigurationAndPreservesItThroughMigration()
    {
        var element = Browser(
            "alarm-browser",
            BuiltinVisualObjectSchemas.AlarmBrowserType,
            """
            {
              "version": 1,
              "mode": "history",
              "lifecycle": "active",
              "acknowledgement": "unacknowledged",
              "minimumPriority": 3,
              "area": "Process",
              "tagPath": "Plant.P01",
              "text": "pressure",
              "lookbackSeconds": 7200,
              "columns": ["timestamp", "state", "priority", "name", "area", "tag.path", "message"],
              "sortField": "priority",
              "sortDirection": "descending",
              "pageSize": 25,
              "acknowledgeEnabled": true
            }
            """);

        var issues = Validate(element, ImportEntityKind.Screen);
        Assert.DoesNotContain(issues, issue => issue.IsError);

        var normalized = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        Assert.True(normalized.Properties!.TryGetValue(BuiltinVisualObjectSchemas.BrowserConfigProperty, out var config));
        Assert.Equal(JsonValueKind.Object, config.ValueKind);
        Assert.Equal("history", config.GetProperty("mode").GetString());
        Assert.Equal("Plant.P01", config.GetProperty("tagPath").GetString());
        Assert.Equal(25, config.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public void EventBrowser_AcceptsCanonicalOperationalEventFiltersWithoutAlarmSemantics()
    {
        var element = Browser(
            "event-browser",
            BuiltinVisualObjectSchemas.EventBrowserType,
            """
            {
              "version": 1,
              "type": "OperatorAction",
              "category": "operation",
              "source": "runtime.hmi",
              "area": "Process",
              "equipmentPath": "Plant.P01",
              "tagPath": "Plant.P01.Running",
              "operator": "operator-a",
              "operation": "start",
              "commandKey": "pump.start",
              "text": "started",
              "lookbackSeconds": 86400,
              "columns": ["timestamp", "type", "category", "source", "area", "equipment.path", "tag.path", "operator", "operation", "command.key", "message"],
              "sortField": "timestamp",
              "sortDirection": "descending",
              "pageSize": 50
            }
            """);

        var issues = Validate(element, ImportEntityKind.Popup);
        Assert.DoesNotContain(issues, issue => issue.IsError);
    }

    [Theory]
    [InlineData(BuiltinVisualObjectSchemas.AlarmBrowserType, "{\"version\":1,\"columns\":[\"timestamp\",\"type\"]}", "VISUAL_BROWSER_COLUMNS_INVALID")]
    [InlineData(BuiltinVisualObjectSchemas.EventBrowserType, "{\"version\":1,\"columns\":[\"timestamp\",\"state\"]}", "VISUAL_BROWSER_COLUMNS_INVALID")]
    [InlineData(BuiltinVisualObjectSchemas.AlarmBrowserType, "{\"version\":2,\"columns\":[\"timestamp\"]}", "VISUAL_BROWSER_CONFIG_VERSION_UNSUPPORTED")]
    [InlineData(BuiltinVisualObjectSchemas.AlarmBrowserType, "{\"version\":1,\"columns\":[\"timestamp\"],\"pageSize\":9}", "VISUAL_BROWSER_PAGE_SIZE_INVALID")]
    [InlineData(BuiltinVisualObjectSchemas.EventBrowserType, "{\"version\":1,\"columns\":[\"timestamp\"],\"lookbackSeconds\":59}", "VISUAL_BROWSER_LOOKBACK_INVALID")]
    [InlineData(BuiltinVisualObjectSchemas.EventBrowserType, "{\"version\":1,\"columns\":[\"timestamp\"],\"sortField\":\"priority\"}", "VISUAL_BROWSER_SORT_INVALID")]
    public void BrowserValidation_FailsClosedForCrossKindOrInvalidConfiguration(
        string objectType,
        string configuration,
        string expectedCode)
    {
        var element = Browser("invalid-browser", objectType, configuration);

        var issues = Validate(element, ImportEntityKind.Screen);

        Assert.Contains(issues, issue => issue.Code == expectedCode && issue.IsError);
    }

    [Fact]
    public void BrowserValidation_RejectsNonObjectConfiguration()
    {
        var element = new VisualElementEngineeringDto(
            "invalid-browser",
            BuiltinVisualObjectSchemas.AlarmBrowserType,
            Properties: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["x"] = Json("10"),
                ["y"] = Json("20"),
                ["width"] = Json("720"),
                ["height"] = Json("320"),
                [BuiltinVisualObjectSchemas.BrowserConfigProperty] = Json("\"manual-json-is-not-a-browser-config\"")
            });

        var issues = Validate(element, ImportEntityKind.Screen);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_BROWSER_CONFIG_INVALID" && issue.IsError);
    }

    private static VisualElementEngineeringDto Browser(string key, string objectType, string configuration) =>
        new(
            key,
            objectType,
            Properties: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["x"] = Json("10"),
                ["y"] = Json("20"),
                ["width"] = Json("720"),
                ["height"] = Json("320"),
                [BuiltinVisualObjectSchemas.BrowserConfigProperty] = Json(configuration)
            });

    private static IReadOnlyCollection<ImportIssue> Validate(VisualElementEngineeringDto element, ImportEntityKind kind) =>
        BuiltinVisualEngineeringValidation.Validate(element, kind, "c18.browser", schemaVersion: int.MaxValue);

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
