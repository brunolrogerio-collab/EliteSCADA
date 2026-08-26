using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlEngineeringLineageTests
{
    [Fact]
    public async Task SaveDerived_PersistsLineageAndRejectsCrossProjectParent()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();

        var projectA = $"lineage-a-{Guid.NewGuid():N}";
        var projectB = $"lineage-b-{Guid.NewGuid():N}";
        const string json = """
            {
              "schema": "scada.engineering",
              "schemaVersion": 5,
              "exportedAt": "2026-08-26T00:00:00Z",
              "tags": [],
              "alarms": []
            }
            """;

        var root = await store.SaveAsync(
            projectA,
            "Lineage Plant A",
            "scada.engineering",
            5,
            json,
            "engineer-a");

        var derived = await store.SaveDerivedAsync(
            projectA,
            "Lineage Plant A",
            "scada.engineering",
            5,
            json,
            root.Revision,
            "engineer-b");

        var loaded = await store.LoadRevisionAsync(projectA, derived.Revision);

        Assert.Null(root.BasedOnRevision);
        Assert.NotNull(loaded);
        Assert.Equal(root.Revision, derived.BasedOnRevision);
        Assert.Equal(root.Revision, loaded!.BasedOnRevision);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveDerivedAsync(
                projectB,
                "Lineage Plant B",
                "scada.engineering",
                5,
                json,
                root.Revision,
                "engineer-b"));

        Assert.Contains("does not belong", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await store.LoadLatestAsync(projectB));
    }
}
