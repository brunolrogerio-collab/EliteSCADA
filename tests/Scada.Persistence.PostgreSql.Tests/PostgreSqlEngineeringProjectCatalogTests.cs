using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlEngineeringProjectCatalogTests
{
    [Fact]
    public async Task Catalog_ListsOneLatestEntryPerPersistedProject()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();
        await using var catalog = new PostgreSqlEngineeringProjectCatalog(connectionString);

        var projectKey = $"catalog-{Guid.NewGuid():N}";
        const string json = """
            {
              "schema": "scada.engineering",
              "schemaVersion": 5,
              "exportedAt": "2026-09-02T00:00:00Z",
              "tags": [],
              "alarms": []
            }
            """;

        var first = await store.SaveAsync(
            projectKey,
            "Catalog Project",
            "scada.engineering",
            5,
            json,
            "catalog-test");
        var second = await store.SaveAsync(
            projectKey,
            "Catalog Project Renamed",
            "scada.engineering",
            5,
            json,
            "catalog-test");

        Assert.True(await catalog.HasAnyAsync());
        var entries = await catalog.ListAsync();
        var entry = Assert.Single(entries, candidate => candidate.ProjectKey == projectKey);
        Assert.Equal(second.Revision, entry.LatestRevision);
        Assert.True(entry.LatestRevision > first.Revision);
        Assert.Equal("Catalog Project Renamed", entry.ProjectName);
        Assert.Equal(second.SavedAtUtc, entry.LastSavedAtUtc);
    }
}
