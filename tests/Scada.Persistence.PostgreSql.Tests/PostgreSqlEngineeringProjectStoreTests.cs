using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlEngineeringProjectStoreTests
{
    [Fact]
    public async Task Store_InitializesSavesLoadsAndListsImmutableRevisions()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();
        await store.InitializeAsync();

        var projectKey = $"integration-{Guid.NewGuid():N}";
        const string firstJson = """
            {
              "schema": "scada.engineering",
              "schemaVersion": 5,
              "exportedAt": "2026-08-26T00:00:00Z",
              "tags": [],
              "alarms": []
            }
            """;
        const string secondJson = """
            {
              "schema": "scada.engineering",
              "schemaVersion": 5,
              "exportedAt": "2026-08-26T00:01:00Z",
              "tags": [{ "id": null, "name": "Pressure", "path": "Plant.Pressure", "dataType": "double" }],
              "alarms": []
            }
            """;

        var first = await store.SaveAsync(
            projectKey,
            "Integration Plant",
            "scada.engineering",
            5,
            firstJson,
            "integration-test");
        var second = await store.SaveAsync(
            projectKey,
            "Integration Plant",
            "scada.engineering",
            5,
            secondJson,
            "integration-test");

        var latest = await store.LoadLatestAsync(projectKey);
        var revisions = await store.ListRevisionsAsync(projectKey, 10);

        Assert.NotNull(latest);
        Assert.True(second.Revision > first.Revision);
        Assert.Equal(second.Revision, latest!.Revision);
        Assert.Contains("Plant.Pressure", latest.EngineeringJson);
        Assert.Equal("integration-test", latest.SavedBy);
        Assert.Equal(new[] { second.Revision, first.Revision }, revisions.Select(x => x.Revision).ToArray());
    }

    [Fact]
    public async Task Save_RejectsInvalidEngineeringJsonBeforeDatabaseWrite()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES")
            ?? "Host=127.0.0.1;Database=unused;Username=unused;Password=unused;Timeout=1";
        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(
            "plant-a",
            "Plant A",
            "scada.engineering",
            5,
            "not-json"));
    }
}
