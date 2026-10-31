using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class PublicRuntimePackageProjectionTests
{
    [Fact]
    public void Public_projection_exposes_only_allowlisted_resources_and_presentation_fields()
    {
        var publicScreenId = Guid.Parse("89f4a5e7-cada-4a98-950f-5dbb17687e00");
        var privateScreenId = Guid.Parse("ef7412fb-598b-4560-a1f0-aeeb802a6317");
        var publicPopupId = Guid.Parse("e77f5941-2325-467f-9a91-322633e09f14");
        var publicTagId = Guid.Parse("9bb5224f-66c6-4ed2-9570-d5ce8a6a04b2");
        var privateTagId = Guid.Parse("0924af80-e601-4adb-a9e8-e16f34fc3a2c");
        var publicAssetId = Guid.Parse("8caa3208-1c41-4169-8890-00514cf52b1f");
        var privateAssetId = Guid.Parse("4ce0911f-9eb3-4f05-ab63-c861d48b1243");

        using var document = JsonDocument.Parse($$"""
            {
              "schema": "scadivo.engineering",
              "schemaVersion": 30,
              "exportedAt": "2026-10-31T12:00:00Z",
              "startupScreenId": "{{privateScreenId:D}}",
              "screens": [
                { "id": "{{publicScreenId:D}}", "key": "public-main", "name": "Public", "bindings": [
                  { "tagReference": { "tagId": "{{publicTagId:D}}", "tagPath": "Area/Public" }, "tagPath": "Area/Public" },
                  { "tagReference": { "tagId": "{{privateTagId:D}}", "tagPath": "Area/Private" }, "tagPath": "Area/Private" }
                ], "actions": [{ "command": "secret-command" }] },
                { "id": "{{privateScreenId:D}}", "key": "private-admin", "name": "Private Admin", "credentials": "private-screen-secret" }
              ],
              "popups": [{ "id": "{{publicPopupId:D}}", "name": "Allowed popup" }, { "id": "{{privateScreenId:D}}", "name": "Private popup" }],
              "visualAssets": [{ "id": "{{publicAssetId:D}}", "key": "public-logo", "name": "Public logo" }, { "id": "{{privateAssetId:D}}", "name": "Private asset" }],
              "dynamos": [], "equipment": [], "templates": [], "scripts": [{ "source": "secret script" }],
              "scriptVisualEventReferences": [{ "secret": "script reference" }],
              "tags": [{ "id": "{{privateTagId:D}}", "path": "Area/Private", "password": "not-for-runtime" }],
              "alarms": [{ "name": "private alarm" }],
              "dataSources": [{ "connectionString": "private database address" }],
              "securityRoles": [{ "key": "administrator" }],
              "assetProcessIntelligence": { "private": "intelligence config" },
              "unknownFutureSecret": "must never reach the anonymous runtime",
              "branding": { "mode": "image", "text": "Demo", "visualAssetId": "{{privateAssetId:D}}", "secret": "hidden" },
              "runtimePresentation": {
                "historicalPlaybackEnabled": true,
                "mobileOrientation": "landscape",
                "mobileScreens": { "mobile": "public-main", "admin": "private-admin" },
                "stationProfiles": [{ "name": "private station" }],
                "header": {
                  "enabled": true,
                  "historyVisible": true,
                  "alarmsVisible": true,
                  "playbackVisible": true,
                  "titleStyle": { "fontSize": 18, "privateStyleField": "hidden" },
                  "dateTime": { "mode": "time", "privateDateField": "hidden" },
                  "links": [
                    { "label": "Public", "screenKey": "public-main", "visualAssetId": "{{publicAssetId:D}}" },
                    { "label": "Admin", "screenKey": "private-admin", "visualAssetId": "{{privateAssetId:D}}" }
                  ],
                  "unknownPrivateSetting": "hidden"
                },
                "unknownPrivateSetting": "hidden too"
              }
            }
            """);
        var policy = new RuntimePublicAccessPolicy(
            Enabled: true,
            StartScreenId: publicScreenId,
            ScreenIds: [publicScreenId],
            PopupIds: [publicPopupId],
            TagIds: [publicTagId],
            VisualAssetIds: [publicAssetId],
            ProjectKey: "demo",
            Revision: 7);

        var projected = RuntimeEngineeringPackageApi.ProjectPublicHmiPackage(document.RootElement, policy);

        Assert.Equal(
            new[]
            {
                "schema", "schemaVersion", "exportedAt", "startupScreenId", "screens", "popups",
                "dynamos", "equipment", "templates", "scripts", "scriptVisualEventReferences",
                "visualAssets", "branding", "runtimePresentation"
            }.Order(StringComparer.Ordinal),
            projected.Select(property => property.Key).Order(StringComparer.Ordinal));
        Assert.False(projected.ContainsKey("tags"));
        Assert.False(projected.ContainsKey("alarms"));
        Assert.False(projected.ContainsKey("dataSources"));
        Assert.False(projected.ContainsKey("unknownFutureSecret"));
        Assert.Equal(publicScreenId.ToString("D"), projected["startupScreenId"]!.GetValue<string>());
        Assert.Single(projected["popups"]!.AsArray());
        Assert.Single(projected["visualAssets"]!.AsArray());

        var publicScreen = Assert.Single(projected["screens"]!.AsArray())!.AsObject();
        Assert.False(publicScreen.ContainsKey("actions"));
        var bindings = publicScreen["bindings"]!.AsArray();
        Assert.Single(bindings);
        Assert.DoesNotContain("tagPath", bindings[0]!.AsObject());
        Assert.DoesNotContain("tagPath", bindings[0]!["tagReference"]!.AsObject());

        var branding = projected["branding"]!.AsObject();
        Assert.Equal("default", branding["mode"]!.GetValue<string>());
        Assert.False(branding.ContainsKey("visualAssetId"));
        Assert.False(branding.ContainsKey("secret"));

        var presentation = projected["runtimePresentation"]!.AsObject();
        Assert.False(presentation["historicalPlaybackEnabled"]!.GetValue<bool>());
        Assert.False(presentation.ContainsKey("stationProfiles"));
        Assert.False(presentation.ContainsKey("unknownPrivateSetting"));
        Assert.Equal("public-main", presentation["mobileScreens"]!["mobile"]!.GetValue<string>());
        Assert.False(presentation["mobileScreens"]!.AsObject().ContainsKey("admin"));
        var header = presentation["header"]!.AsObject();
        Assert.False(header["historyVisible"]!.GetValue<bool>());
        Assert.False(header["alarmsVisible"]!.GetValue<bool>());
        Assert.False(header["playbackVisible"]!.GetValue<bool>());
        Assert.False(header.ContainsKey("unknownPrivateSetting"));
        Assert.False(header["titleStyle"]!.AsObject().ContainsKey("privateStyleField"));
        Assert.False(header["dateTime"]!.AsObject().ContainsKey("privateDateField"));
        Assert.Single(header["links"]!.AsArray());
    }
}
