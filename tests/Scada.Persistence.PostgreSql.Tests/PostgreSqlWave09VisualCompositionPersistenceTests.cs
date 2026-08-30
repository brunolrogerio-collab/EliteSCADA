using System.Text.Json;
using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlWave09VisualCompositionPersistenceTests
{
    [Fact]
    public async Task RevisionPersistence_PreservesDynamoPopupNavigationAndLineage()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();

        var projectKey = $"wave09-visual-{Guid.NewGuid():N}";
        var firstJson = CanonicalPackage("Pump A");
        var secondJson = CanonicalPackage("Pump B");

        var first = await store.SaveAsync(
            projectKey,
            "Wave 09 Visual Composition",
            "scada.engineering",
            13,
            firstJson,
            "wave09-dev2");
        var second = await store.SaveDerivedAsync(
            projectKey,
            "Wave 09 Visual Composition",
            "scada.engineering",
            13,
            secondJson,
            first.Revision,
            "wave09-dev2");

        var storedFirst = await store.LoadRevisionAsync(projectKey, first.Revision);
        var storedSecond = await store.LoadRevisionAsync(projectKey, second.Revision);

        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.Null(storedFirst!.BasedOnRevision);
        Assert.Equal(first.Revision, storedSecond!.BasedOnRevision);
        AssertWave09Payload(storedFirst.EngineeringJson, "Pump A");
        AssertWave09Payload(storedSecond.EngineeringJson, "Pump B");
    }

    private static void AssertWave09Payload(string json, string caption)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(13, root.GetProperty("schemaVersion").GetInt32());

        var dynamo = root.GetProperty("dynamos")[0];
        Assert.Equal("dynamo.pump", dynamo.GetProperty("key").GetString());
        Assert.Equal("caption", dynamo.GetProperty("parameters")[0].GetProperty("key").GetString());
        Assert.Equal("body", dynamo.GetProperty("elements")[0].GetProperty("key").GetString());

        var instance = root.GetProperty("screens")[0].GetProperty("elements")[0];
        Assert.Equal(caption, instance.GetProperty("dynamoParameters")[0].GetProperty("value").GetString());
        var action = instance.GetProperty("actions")[0];
        Assert.Equal("openPopup", action.GetProperty("kind").GetString());
        Assert.Equal("popup.pump", action.GetProperty("targetKey").GetString());
    }

    private static string CanonicalPackage(string caption) => $$"""
        {
          "schema": "scada.engineering",
          "schemaVersion": 13,
          "exportedAt": "2026-08-29T00:00:00Z",
          "tags": [],
          "alarms": [],
          "dynamos": [
            {
              "key": "dynamo.pump",
              "name": "Pump",
              "parameters": [
                { "key": "caption", "kind": "string", "required": true, "version": 1 }
              ],
              "elements": [
                { "key": "body", "type": "core.rectangle" }
              ]
            }
          ],
          "screens": [
            {
              "key": "plant.overview",
              "name": "Overview",
              "route": "/overview",
              "elements": [
                {
                  "key": "pump01",
                  "type": "dynamo",
                  "dynamoKey": "dynamo.pump",
                  "dynamoParameters": [
                    { "key": "caption", "kind": "string", "value": "{{caption}}", "version": 1 }
                  ],
                  "actions": [
                    { "eventKey": "click", "kind": "openPopup", "targetKey": "popup.pump", "version": 1 }
                  ]
                }
              ]
            }
          ],
          "popups": [
            {
              "key": "popup.pump",
              "name": "Pump Details",
              "elements": []
            }
          ]
        }
        """;
}
