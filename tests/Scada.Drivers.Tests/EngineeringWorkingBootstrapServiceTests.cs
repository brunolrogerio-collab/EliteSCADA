using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Persistence;

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
