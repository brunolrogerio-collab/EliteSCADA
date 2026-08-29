using System.Security.Cryptography;
using Scada.Engineering.Persistence;
using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlVisualAssetRevisionTests
{
    [Fact]
    public async Task SameLogicalAsset_CanChangeContentWithoutMutatingOlderRevision()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();

        var projectKey = $"visual-assets-{Guid.NewGuid():N}";
        var assetId = Guid.NewGuid();
        var firstBytes = new byte[] { 1, 2, 3, 4, 5 };
        var secondBytes = new byte[] { 9, 8, 7, 6, 5, 4 };
        var firstHash = Sha256(firstBytes);
        var secondHash = Sha256(secondBytes);
        const string engineeringJson = """
            {
              "schema": "scada.engineering",
              "schemaVersion": 13,
              "exportedAt": "2026-08-28T00:00:00Z",
              "tags": [],
              "alarms": [],
              "visualAssets": []
            }
            """;

        var first = await store.SaveDerivedWithAssetsAsync(
            projectKey,
            "Visual Asset Plant",
            "scada.engineering",
            13,
            engineeringJson,
            null,
            new[]
            {
                new EngineeringRevisionAssetPayload(assetId, firstHash, "image/png", firstBytes)
            },
            "integration-test");

        var second = await store.SaveDerivedWithAssetsAsync(
            projectKey,
            "Visual Asset Plant",
            "scada.engineering",
            13,
            engineeringJson,
            first.Revision,
            new[]
            {
                new EngineeringRevisionAssetPayload(assetId, secondHash, "image/png", secondBytes)
            },
            "integration-test");

        var firstAssets = await store.LoadRevisionAssetsAsync(projectKey, first.Revision);
        var secondAssets = await store.LoadRevisionAssetsAsync(projectKey, second.Revision);

        var firstRestored = Assert.Single(firstAssets);
        var secondRestored = Assert.Single(secondAssets);
        Assert.Equal(assetId, firstRestored.AssetId);
        Assert.Equal(assetId, secondRestored.AssetId);
        Assert.Equal(firstHash, firstRestored.Sha256);
        Assert.Equal(secondHash, secondRestored.Sha256);
        Assert.Equal(firstBytes, firstRestored.Content);
        Assert.Equal(secondBytes, secondRestored.Content);
        Assert.NotEqual(firstRestored.Sha256, secondRestored.Sha256);
    }

    private static string Sha256(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
