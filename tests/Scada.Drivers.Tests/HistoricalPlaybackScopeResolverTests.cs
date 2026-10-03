using System.Text.Json;
using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class HistoricalPlaybackScopeResolverTests
{
    [Fact]
    public void Resolve_UsesCurrentVisualScope_StableTagIdentity_AndExistingRetrievalPolicy()
    {
        var analogId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var discreteId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var ignoredId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var missingId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var conflictingPathId = Guid.Parse("10000000-0000-0000-0000-000000000005");
        var dynamoId = Guid.Parse("20000000-0000-0000-0000-000000000001");

        using var document = JsonDocument.Parse($$"""
        {
          "runtimePresentation": { "historicalPlaybackEnabled": true, "version": 1 },
          "tags": [
            { "id": "{{analogId}}", "path": "Plant.Analog", "dataType": "double" },
            { "id": "{{discreteId}}", "path": "Plant.Run", "dataType": "boolean" },
            { "id": "{{ignoredId}}", "path": "Plant.Other", "dataType": "double" },
            { "id": "{{conflictingPathId}}", "path": "Plant.NotHistorical", "dataType": "double" }
          ],
          "screens": [
            {
              "key": "overview",
              "elements": [
                {
                  "key": "dyn",
                  "type": "core.dynamo",
                  "dynamoDefinitionId": "{{dynamoId}}",
                  "bindings": [
                    {
                      "key": "pv",
                      "kind": "tag",
                      "target": "Wrong.Legacy.Path",
                      "tagReference": { "tagId": "{{analogId}}" }
                    },
                    {
                      "key": "missing",
                      "kind": "tag",
                      "target": "Plant.NotHistorical",
                      "tagReference": { "tagId": "{{missingId}}" }
                    }
                  ]
                }
              ]
            },
            {
              "key": "other",
              "elements": [
                { "key": "other", "type": "core.text", "bindings": [
                  { "key": "v", "kind": "tag", "target": "Plant.Other" }
                ] }
              ]
            }
          ],
          "popups": [
            { "key": "motor", "elements": [
              { "key": "run", "type": "core.text", "bindings": [
                { "key": "state", "kind": "tag", "target": "Plant.Run" }
              ] }
            ] }
          ],
          "dynamos": [
            { "id": "{{dynamoId}}", "key": "pump", "elements": [
              { "key": "state", "type": "core.text", "bindings": [
                { "key": "run", "kind": "tag", "target": "Plant.Run" }
              ] }
            ] }
          ],
          "equipment": [],
          "templates": []
        }
        """);

        var result = HistoricalPlaybackScopeResolver.Resolve(
            document.RootElement,
            new HistoricalPlaybackScopeRequest("overview", ["motor"]));

        Assert.Equal("overview", result.ScreenKey);
        Assert.Equal(2, result.Tags.Count);
        var analog = Assert.Single(result.Tags, x => x.Id == analogId);
        Assert.Equal("Plant.Analog", analog.Path);
        Assert.Equal("interpolated", analog.RetrievalMode);
        var discrete = Assert.Single(result.Tags, x => x.Id == discreteId);
        Assert.Equal("atOrBefore", discrete.RetrievalMode);
        Assert.DoesNotContain(result.Tags, x => x.Id == ignoredId);
        Assert.DoesNotContain(result.Tags, x => x.Id == conflictingPathId);
        var unresolved = Assert.Single(result.UnresolvedReferences);
        Assert.Equal(missingId, unresolved.Id);
        Assert.Equal("Plant.NotHistorical", unresolved.Path);
    }

    [Fact]
    public void Resolve_ExpandsEquipmentTemplateReferencesWithoutUserConfiguredPlaybackIds()
    {
        var tagId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var templateId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var equipmentId = Guid.Parse("30000000-0000-0000-0000-000000000003");

        using var document = JsonDocument.Parse($"""
        {
          "runtimePresentation": { "historicalPlaybackEnabled": true, "version": 1 },
          "tags": [
            { "id": "{{tagId}}", "path": "Plant.Pump01.Speed", "dataType": "double" }
          ],
          "screens": [
            {
              "key": "overview",
              "elements": [
                {
                  "key": "pump-01",
                  "type": "core.group",
                  "equipmentId": "{{equipmentId}}",
                  "equipmentPath": "Plant.Pump01"
                }
              ]
            }
          ],
          "popups": [],
          "dynamos": [],
          "equipment": [
            {
              "id": "{{equipmentId}}",
              "path": "Plant.Pump01",
              "templateId": "{{templateId}}",
              "templateKey": "pump"
            }
          ],
          "templates": [
            {
              "id": "{{templateId}}",
              "key": "pump",
              "elements": [
                {
                  "key": "speed",
                  "type": "core.valueDisplay",
                  "bindings": [
                    {
                      "key": "text",
                      "kind": "tag",
                      "target": "Plant.Pump01.Speed",
                      "tagReference": { "tagId": "{{tagId}}" }
                    }
                  ]
                }
              ]
            }
          ]
        }
        """);

        var result = HistoricalPlaybackScopeResolver.Resolve(
            document.RootElement,
            new HistoricalPlaybackScopeRequest("overview"));

        var tag = Assert.Single(result.Tags);
        Assert.Equal(tagId, tag.Id);
        Assert.Equal("Plant.Pump01.Speed", tag.Path);
        Assert.Equal("interpolated", tag.RetrievalMode);
        Assert.Empty(result.UnresolvedReferences);
    }

    [Fact]
    public void IsEnabled_DefaultsLegacyPackageToHidden()
    {
        using var legacy = JsonDocument.Parse("{\"screens\":[]}");
        using var enabled = JsonDocument.Parse("{\"runtimePresentation\":{\"historicalPlaybackEnabled\":true}}");
        Assert.False(HistoricalPlaybackScopeResolver.IsEnabled(legacy.RootElement));
        Assert.True(HistoricalPlaybackScopeResolver.IsEnabled(enabled.RootElement));
    }
}
