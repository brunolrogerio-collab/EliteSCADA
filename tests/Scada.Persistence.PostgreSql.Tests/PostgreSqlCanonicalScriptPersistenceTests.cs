using System.Text.Json;
using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlCanonicalScriptPersistenceTests
{
    [Fact]
    public async Task RevisionPersistence_PreservesCanonicalScriptPayloadAndImmutableHistory()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();

        var projectKey = $"script-v10-{Guid.NewGuid():N}";
        var scriptId = Guid.NewGuid();
        var firstJson = CanonicalPackage(scriptId, "value = 1", enabled: true);
        var secondJson = CanonicalPackage(scriptId, "value = 2", enabled: false);

        var first = await store.SaveAsync(
            projectKey,
            "Script Persistence",
            "scada.engineering",
            10,
            firstJson,
            "wave-05-test");
        var second = await store.SaveAsync(
            projectKey,
            "Script Persistence",
            "scada.engineering",
            10,
            secondJson,
            "wave-05-test");

        var storedFirst = await store.LoadRevisionAsync(projectKey, first.Revision);
        var storedSecond = await store.LoadRevisionAsync(projectKey, second.Revision);
        var latest = await store.LoadLatestAsync(projectKey);

        Assert.NotNull(storedFirst);
        Assert.NotNull(storedSecond);
        Assert.NotNull(latest);
        Assert.Equal(10, storedFirst!.SchemaVersion);
        Assert.Equal(10, storedSecond!.SchemaVersion);
        Assert.Equal(second.Revision, latest!.Revision);

        AssertScript(storedFirst.EngineeringJson, scriptId, "value = 1", enabled: true);
        AssertScript(storedSecond.EngineeringJson, scriptId, "value = 2", enabled: false);
        AssertScript(latest.EngineeringJson, scriptId, "value = 2", enabled: false);
    }

    private static void AssertScript(string json, Guid scriptId, string source, bool enabled)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(10, root.GetProperty("schemaVersion").GetInt32());

        var scripts = root.GetProperty("scripts");
        Assert.Equal(1, scripts.GetArrayLength());
        var script = scripts[0];
        Assert.Equal(scriptId, script.GetProperty("id").GetGuid());
        Assert.Equal("scripts/client/main", script.GetProperty("path").GetString());
        Assert.Equal("Client Main", script.GetProperty("name").GetString());
        Assert.Equal(source, script.GetProperty("source").GetString());
        Assert.Equal(enabled, script.GetProperty("enabled").GetBoolean());
        Assert.Equal("ClientVisual", script.GetProperty("scope").GetString());
        Assert.Equal("python", script.GetProperty("language").GetString());
        Assert.Equal("3", script.GetProperty("languageVersion").GetString());
    }

    private static string CanonicalPackage(Guid scriptId, string source, bool enabled) => $$"""
        {
          "schema": "scada.engineering",
          "schemaVersion": 10,
          "exportedAt": "2026-08-28T00:00:00Z",
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
          "scripts": [
            {
              "id": "{{scriptId:D}}",
              "path": "scripts/client/main",
              "name": "Client Main",
              "scope": "ClientVisual",
              "source": "{{source}}",
              "enabled": {{enabled.ToString().ToLowerInvariant()}},
              "language": "python",
              "languageVersion": "3",
              "entryPoints": [],
              "dependencies": [],
              "metadata": {}
            }
          ],
          "scriptVisualEventReferences": []
        }
        """;
}
