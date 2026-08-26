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
        var explicitFirst = await store.LoadRevisionAsync(projectKey, first.Revision);
        var revisions = await store.ListRevisionsAsync(projectKey, 10);

        Assert.NotNull(latest);
        Assert.NotNull(explicitFirst);
        Assert.True(second.Revision > first.Revision);
        Assert.Equal(second.Revision, latest!.Revision);
        Assert.Contains("Plant.Pressure", latest.EngineeringJson);
        Assert.DoesNotContain("Plant.Pressure", explicitFirst!.EngineeringJson);
        Assert.Equal(first.Revision, explicitFirst.Revision);
        Assert.Equal("integration-test", latest.SavedBy);
        Assert.Equal(new[] { second.Revision, first.Revision }, revisions.Select(x => x.Revision).ToArray());
    }

    [Fact]
    public async Task PublishRevision_PersistsProjectPublicationAndCanMovePointer()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();

        var projectKey = $"publication-{Guid.NewGuid():N}";
        const string json = """
            {
              "schema": "scada.engineering",
              "schemaVersion": 5,
              "exportedAt": "2026-08-26T00:00:00Z",
              "tags": [],
              "alarms": []
            }
            """;

        var first = await store.SaveAsync(projectKey, "Publication Plant", "scada.engineering", 5, json);
        var second = await store.SaveAsync(projectKey, "Publication Plant", "scada.engineering", 5, json);

        Assert.Null(await store.GetPublicationAsync(projectKey));

        var publishedFirst = await store.PublishRevisionAsync(projectKey, first.Revision, "supervisor-a");
        var storedFirst = await store.GetPublicationAsync(projectKey);

        Assert.NotNull(publishedFirst);
        Assert.Equal(first.Revision, storedFirst!.PublishedRevision);
        Assert.Equal("supervisor-a", storedFirst.PublishedBy);

        var publishedSecond = await store.PublishRevisionAsync(projectKey, second.Revision, "supervisor-b");
        var storedSecond = await store.GetPublicationAsync(projectKey);

        Assert.NotNull(publishedSecond);
        Assert.Equal(second.Revision, storedSecond!.PublishedRevision);
        Assert.Equal("supervisor-b", storedSecond.PublishedBy);
    }

    [Fact]
    public async Task Activation_OnlyAcceptsCurrentPublicationAndDoesNotMoveWhenPublicationChanges()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();

        var projectKey = $"activation-{Guid.NewGuid():N}";
        const string json = """
            {
              "schema": "scada.engineering",
              "schemaVersion": 5,
              "exportedAt": "2026-08-26T00:00:00Z",
              "tags": [],
              "alarms": []
            }
            """;

        var first = await store.SaveAsync(projectKey, "Activation Plant", "scada.engineering", 5, json);
        var second = await store.SaveAsync(projectKey, "Activation Plant", "scada.engineering", 5, json);

        Assert.Null(await store.GetActivationAsync(projectKey));
        Assert.Null(await store.RecordActivationAsync(projectKey, first.Revision, "operator-before-publish"));

        await store.PublishRevisionAsync(projectKey, first.Revision, "supervisor-a");
        var activatedFirst = await store.RecordActivationAsync(projectKey, first.Revision, "operator-a");

        Assert.NotNull(activatedFirst);
        Assert.Equal(first.Revision, activatedFirst!.ActiveRevision);
        Assert.Equal("operator-a", activatedFirst.ActivatedBy);

        await store.PublishRevisionAsync(projectKey, second.Revision, "supervisor-b");
        var stillActiveFirst = await store.GetActivationAsync(projectKey);

        Assert.NotNull(stillActiveFirst);
        Assert.Equal(first.Revision, stillActiveFirst!.ActiveRevision);
        Assert.Null(await store.RecordActivationAsync(projectKey, first.Revision, "operator-stale"));

        var activatedSecond = await store.RecordActivationAsync(projectKey, second.Revision, "operator-b");
        var storedSecond = await store.GetActivationAsync(projectKey);

        Assert.NotNull(activatedSecond);
        Assert.Equal(second.Revision, storedSecond!.ActiveRevision);
        Assert.Equal("operator-b", storedSecond.ActivatedBy);
    }

    [Fact]
    public async Task PublishRevision_RejectsRevisionOwnedByAnotherProject()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();

        var projectA = $"project-a-{Guid.NewGuid():N}";
        var projectB = $"project-b-{Guid.NewGuid():N}";
        const string json = """
            {
              "schema": "scada.engineering",
              "schemaVersion": 5,
              "exportedAt": "2026-08-26T00:00:00Z",
              "tags": [],
              "alarms": []
            }
            """;

        var revisionA = await store.SaveAsync(projectA, "Project A", "scada.engineering", 5, json);
        var result = await store.PublishRevisionAsync(projectB, revisionA.Revision, "intruder");

        Assert.Null(result);
        Assert.Null(await store.GetPublicationAsync(projectB));
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
