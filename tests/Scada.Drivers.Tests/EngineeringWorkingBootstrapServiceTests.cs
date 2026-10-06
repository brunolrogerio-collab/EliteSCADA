using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Persistence;
using Scada.Engineering.VisualAssets;

namespace Scada.Drivers.Tests;

public sealed class EngineeringWorkingBootstrapServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RuntimeConfiguredProjectBecomesWorkingWithoutImplicitDemoContent()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var checkout = new RecordingCheckout(workspace);
        var service = new EngineeringWorkingBootstrapService(
            new Catalog(Entry("eee-demo", 2, T0)),
            checkout,
            workspace);

        var result = await service.BootstrapAsync(null, null, "eee-demo");

        Assert.True(result.CheckedOut);
        Assert.Equal(EngineeringWorkingBootstrapSource.ConfiguredRuntime, result.Source);
        Assert.Equal("eee-demo", result.ProjectKey);
        Assert.Equal(2, result.Revision);
        Assert.Equal(("eee-demo", 2L), checkout.LastRequest);
        Assert.Equal("eee-demo", workspace.Describe().ProjectKey);
        Assert.Equal(2, workspace.Describe().BaseRevision);
        Assert.Equal(0, workspace.Describe().TagCount);
        Assert.Equal(0, workspace.Describe().DynamoCount);
    }

    [Fact]
    public async Task ExistingLegacyBuiltinLibraryIsPurgedAndReplacedWithoutDeletingProjectDynamos()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var original = LegacyPlatformMarker();
        var originalId = Guid.NewGuid();
        workspace.Assets.UpsertDynamo(original with { Id = originalId });
        var importedOriginal = ImportedE3DynamoLibrary.Create().Single(dynamo => dynamo.Key == "e3.process.motor-1");
        var importedOriginalId = Guid.NewGuid();
        workspace.Assets.UpsertDynamo(importedOriginal with
        {
            Id = importedOriginalId,
            Elements = importedOriginal.Elements!.Where(element => element.Type != "core.arc").ToArray()
        });
        var customMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["builtinLibrary"] = "false"
        };
        var customDynamo = BuiltinDynamoCatalogV1.Create().First() with
        {
            Id = Guid.NewGuid(),
            Key = "user.custom.dynamo",
            Metadata = customMetadata,
            Name = "Custom definition with a platform key"
        };
        workspace.Assets.UpsertDynamo(customDynamo);
        var checkout = new RecordingCheckout(workspace);

        var result = await new EngineeringWorkingBootstrapService(
                new Catalog(Entry("project-with-library", 4, T0)),
                checkout,
                workspace)
            .BootstrapAsync(null, null, "project-with-library");

        Assert.Null(workspace.Assets.FindDynamoByKey(original.Key));
        Assert.DoesNotContain(workspace.Assets.SnapshotDynamos(), dynamo => dynamo.Id == originalId);
        Assert.Null(workspace.Assets.FindDynamoByKey(importedOriginal.Key));
        Assert.DoesNotContain(workspace.Assets.SnapshotDynamos(), dynamo =>
            dynamo.Metadata?.GetValueOrDefault("assetOrigin") == "elipse-e3-import");
        Assert.Equal("Custom definition with a platform key", workspace.Assets.FindDynamoByKey(customDynamo.Key)!.Name);
        Assert.Equal(28, workspace.Assets.SnapshotDynamos().Count);
        Assert.DoesNotContain(workspace.Assets.SnapshotDynamos(), dynamo =>
            dynamo.Metadata?.GetValueOrDefault("catalogStatus") == "legacy");
        var replacementCatalog = BuiltinDynamoCatalogV1.Create();
        Assert.All(replacementCatalog, definition =>
            Assert.Contains(workspace.Assets.SnapshotDynamos(), item => item.Key == definition.Key));
        var artwork = BuiltinDynamoCatalogV1.CreateArtworkAssets();
        Assert.All(artwork, item =>
        {
            var persistedAsset = workspace.VisualAssets.FindAsset(item.Asset.Id!.Value);
            Assert.NotNull(persistedAsset);
            Assert.Equal(item.Asset.Key, persistedAsset.Key);
            Assert.True(workspace.VisualAssets.HasPayload(persistedAsset.Sha256));
        });
        Assert.True(result.Workspace.IsDirty);
        Assert.Equal(28, result.Workspace.DynamoCount);
        Assert.Equal(4, result.Workspace.BaseRevision);
    }

    [Fact]
    public void ImportedLegacyWorkspaceCanUpgradeBuiltinDynamoCatalogWithoutRestartingApi()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        workspace.Assets.UpsertDynamo(LegacyPlatformMarker());

        // Import/apply happens after startup bootstrap in real deployments. The API
        // must run this migration immediately after the successful package apply.
        EngineeringWorkingBootstrapService.UpgradeBuiltinDynamos(workspace);

        var upgradedDefinitions = BuiltinDynamoCatalogV1.Create();
        Assert.All(upgradedDefinitions, definition =>
        {
            var saved = workspace.Assets.FindDynamoByKey(definition.Key);
            Assert.NotNull(saved);
            Assert.Equal(BuiltinDynamoCatalogV1.Version, saved!.Properties!["libraryVersion"]);
        });

        Assert.All(BuiltinDynamoCatalogV1.CreateArtworkAssets(), item =>
        {
            var asset = workspace.VisualAssets.FindAssetByKey(item.Asset.Key);
            Assert.NotNull(asset);
            Assert.True(workspace.VisualAssets.HasPayload(asset!.Sha256));
        });
        Assert.DoesNotContain(workspace.Assets.SnapshotDynamos(), definition =>
            definition.Metadata?.GetValueOrDefault("catalogStatus") == "legacy");
    }

    [Fact]
    public void ExistingCatalogUpgradeReplacesOnlyBuiltinArtworkAndAddsSubmersiblePumpDynamo()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var previousCatalog = BuiltinDynamoCatalogV1.Create()
            .Where(definition => definition.Key != "pump.submersible");
        foreach (var definition in previousCatalog)
        {
            var previousProperties = new Dictionary<string, string>(definition.Properties!, StringComparer.Ordinal)
            {
                ["libraryVersion"] = "1.0.1"
            };
            workspace.Assets.UpsertDynamo(definition with { Properties = previousProperties });
        }

        var latestArtwork = BuiltinDynamoCatalogV1.CreateArtworkAssets();
        var motorArtwork = latestArtwork.Single(item => item.Asset.Key == "builtin.dynamo.v1.motor-tefc");
        var previousPayload = VisualAssetPayload.Create("image/svg+xml",
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1 1\"><rect width=\"1\" height=\"1\" fill=\"#000000\"/></svg>"u8.ToArray());
        workspace.VisualAssets.UpsertAsset(motorArtwork.Asset with
        {
            ByteLength = previousPayload.ByteLength,
            Sha256 = previousPayload.Sha256
        });
        workspace.VisualAssets.PutPayload(previousPayload);
        workspace.Assets.UpsertDynamo(LegacyPlatformMarker());

        EngineeringWorkingBootstrapService.UpgradeBuiltinDynamos(workspace);

        Assert.Equal(BuiltinDynamoCatalogV1.Version,
            workspace.Assets.FindDynamoByKey("motor.tefc")!.Properties!["libraryVersion"]);
        Assert.NotNull(workspace.Assets.FindDynamoByKey("pump.submersible"));
        Assert.Equal(motorArtwork.Asset.Sha256,
            workspace.VisualAssets.FindAssetByKey(motorArtwork.Asset.Key)!.Sha256);
        Assert.True(workspace.VisualAssets.HasPayload(motorArtwork.Asset.Sha256));
    }

    [Fact]
    public async Task ExplicitAlternateWorkingProjectAndRevisionOverrideRuntimeProject()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var checkout = new RecordingCheckout(workspace);
        var service = new EngineeringWorkingBootstrapService(
            new Catalog(
                Entry("active-a", 2, T0),
                Entry("working-b", 7, T0.AddMinutes(-1))),
            checkout,
            workspace);

        var result = await service.BootstrapAsync(" working-b ", 5, "active-a");

        Assert.Equal(EngineeringWorkingBootstrapSource.ConfiguredWorking, result.Source);
        Assert.Equal(("working-b", 5L), checkout.LastRequest);
        Assert.Equal("working-b", result.Workspace.ProjectKey);
        Assert.Equal(5, result.Workspace.BaseRevision);
    }

    [Fact]
    public async Task NoConfigurationSelectsMostRecentlySavedWithStableProjectKeyTieBreak()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var checkout = new RecordingCheckout(workspace);
        var service = new EngineeringWorkingBootstrapService(
            new Catalog(
                Entry("z-project", 4, T0),
                Entry("b-project", 3, T0.AddMinutes(1)),
                Entry("a-project", 6, T0.AddMinutes(1))),
            checkout,
            workspace);

        var result = await service.BootstrapAsync(null, null, null);

        Assert.Equal(EngineeringWorkingBootstrapSource.MostRecentlySaved, result.Source);
        Assert.Equal(("a-project", 6L), checkout.LastRequest);
    }

    [Fact]
    public async Task CaseCollidingFallbackKeysSelectTheSameOrdinalKeyRegardlessOfCatalogOrder()
    {
        static async Task<string?> SelectAsync(params EngineeringProjectCatalogEntry[] entries)
        {
            using var workspace = new EngineeringWorkspace(seedDemo: false);
            var checkout = new RecordingCheckout(workspace);
            var result = await new EngineeringWorkingBootstrapService(
                    new Catalog(entries),
                    checkout,
                    workspace)
                .BootstrapAsync(null, null, null);
            return result.ProjectKey;
        }

        var first = await SelectAsync(
            Entry("plant", 2, T0),
            Entry("Plant", 1, T0));
        var reversed = await SelectAsync(
            Entry("Plant", 1, T0),
            Entry("plant", 2, T0));

        Assert.Equal("Plant", first);
        Assert.Equal(first, reversed);
    }

    [Fact]
    public async Task EmptyPersistedCatalogLeavesTruthfulNeutralWorkingWorkspace()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var checkout = new RecordingCheckout(workspace);
        var service = new EngineeringWorkingBootstrapService(new Catalog(), checkout, workspace);

        var result = await service.BootstrapAsync(null, null, null);

        Assert.False(result.CheckedOut);
        Assert.Equal(EngineeringWorkingBootstrapSource.EmptyCatalog, result.Source);
        Assert.Null(result.ProjectKey);
        Assert.Null(result.Revision);
        Assert.Null(result.Workspace.ProjectKey);
        Assert.Null(checkout.LastRequest);
    }

    [Fact]
    public async Task MissingExplicitWorkingProjectFailsClosedWithoutFallback()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var checkout = new RecordingCheckout(workspace);
        var service = new EngineeringWorkingBootstrapService(
            new Catalog(Entry("persisted", 1, T0)),
            checkout,
            workspace);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BootstrapAsync("missing", null, null));

        Assert.Contains("EngineeringWorking:ProjectKey", error.Message);
        Assert.Null(checkout.LastRequest);
        Assert.Null(workspace.Describe().ProjectKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveExplicitWorkingRevisionFailsClosed(long revision)
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var checkout = new RecordingCheckout(workspace);
        var service = new EngineeringWorkingBootstrapService(
            new Catalog(Entry("persisted", 2, T0)),
            checkout,
            workspace);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BootstrapAsync("persisted", revision, null));

        Assert.Contains("greater than zero", error.Message);
        Assert.Null(checkout.LastRequest);
    }

    [Fact]
    public async Task ExplicitWorkingRevisionWithoutProjectFailsClosed()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var checkout = new RecordingCheckout(workspace);
        var service = new EngineeringWorkingBootstrapService(
            new Catalog(Entry("persisted", 2, T0)),
            checkout,
            workspace);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BootstrapAsync(null, 2, null));

        Assert.Contains("requires EngineeringWorking:ProjectKey", error.Message);
        Assert.Null(checkout.LastRequest);
    }

    [Fact]
    public async Task MissingExplicitWorkingRevisionFailsClosedWithoutLatestFallback()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var checkout = new MissingCheckout();
        var service = new EngineeringWorkingBootstrapService(
            new Catalog(Entry("persisted", 5, T0)),
            checkout,
            workspace);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BootstrapAsync("persisted", 3, null));

        Assert.Contains("revision 3 was not found", error.Message);
        Assert.Equal(("persisted", 3L), checkout.LastRequest);
        Assert.Null(workspace.Describe().ProjectKey);
    }

    [Fact]
    public async Task RestartUsesTheSameDeterministicSelection()
    {
        var catalog = new Catalog(
            Entry("older", 8, T0),
            Entry("newer", 3, T0.AddHours(1)));

        using var firstWorkspace = new EngineeringWorkspace(seedDemo: false);
        var firstCheckout = new RecordingCheckout(firstWorkspace);
        var first = await new EngineeringWorkingBootstrapService(catalog, firstCheckout, firstWorkspace)
            .BootstrapAsync(null, null, null);

        using var restartedWorkspace = new EngineeringWorkspace(seedDemo: false);
        var restartedCheckout = new RecordingCheckout(restartedWorkspace);
        var restarted = await new EngineeringWorkingBootstrapService(catalog, restartedCheckout, restartedWorkspace)
            .BootstrapAsync(null, null, null);

        Assert.Equal(first.Source, restarted.Source);
        Assert.Equal(first.ProjectKey, restarted.ProjectKey);
        Assert.Equal(first.Revision, restarted.Revision);
        Assert.Equal(firstCheckout.LastRequest, restartedCheckout.LastRequest);
    }

    [Fact]
    public void ExplicitNoPersistenceConstructionStillSupportsDemoBootstrap()
    {
        using var workspace = new EngineeringWorkspace();

        var descriptor = workspace.Describe();
        Assert.Equal("demo", descriptor.ProjectKey);
        Assert.True(descriptor.TagCount > 0);
        Assert.True(descriptor.DynamoCount > 0);
    }

    private static EngineeringProjectCatalogEntry Entry(string key, long revision, DateTimeOffset savedAt) =>
        new(key, key, revision, savedAt);

    // Minimal migration marker. The discarded 72 drawings are intentionally not
    // retained in production or test factories merely to exercise retirement.
    private static DynamoEngineeringDto LegacyPlatformMarker() => new(
        Key: "retired.platform.test-marker", Name: "Retired catalog marker", Id: Guid.NewGuid(),
        Metadata: new Dictionary<string, string> { ["builtinLibrary"] = "true", ["catalogStatus"] = "legacy" });

    private sealed class Catalog(params EngineeringProjectCatalogEntry[] entries) : IEngineeringProjectCatalog
    {
        public Task<IReadOnlyCollection<EngineeringProjectCatalogEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectCatalogEntry>>(entries);
    }

    private sealed class RecordingCheckout(EngineeringWorkspace workspace) : IEngineeringWorkspaceCheckoutService
    {
        public (string ProjectKey, long Revision)? LastRequest { get; private set; }

        public Task<EngineeringWorkspaceCheckoutOutcome?> CheckoutAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default)
        {
            LastRequest = (projectKey, revision);
            var savedAt = T0.AddMinutes(revision);
            var snapshot = new EngineeringProjectSnapshot(
                revision,
                projectKey,
                projectKey,
                "elitescada.engineering",
                1,
                savedAt,
                "{}");
            workspace.SetCheckout(projectKey, projectKey, revision, savedAt);
            var preview = new ImportPreview(
                ImportMode.CreateAndUpdate,
                0,
                0,
                0,
                0,
                Array.Empty<ImportPreviewItem>());
            var apply = new ImportResult(
                ImportMode.CreateAndUpdate,
                0,
                0,
                0,
                Array.Empty<ImportIssue>());
            return Task.FromResult<EngineeringWorkspaceCheckoutOutcome?>(
                new EngineeringWorkspaceCheckoutOutcome(snapshot, preview, apply, workspace.Describe()));
        }
    }

    private sealed class MissingCheckout : IEngineeringWorkspaceCheckoutService
    {
        public (string ProjectKey, long Revision)? LastRequest { get; private set; }

        public Task<EngineeringWorkspaceCheckoutOutcome?> CheckoutAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default)
        {
            LastRequest = (projectKey, revision);
            return Task.FromResult<EngineeringWorkspaceCheckoutOutcome?>(null);
        }
    }
}
