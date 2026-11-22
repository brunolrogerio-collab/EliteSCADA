using Microsoft.Extensions.Configuration;
using Scada.Api.Persistence;
using Scada.Engineering.Persistence;

namespace Scada.Drivers.Tests;

public sealed class EngineeringInstallationApplicationAuthorityResolverTests
{
    [Fact]
    public async Task AttachedBindingSupersedesStaleLegacyConfiguration()
    {
        var authority = await EngineeringInstallationApplicationAuthorityResolver.ResolveAsync(
            new FixedBindingStore(EngineeringInstallationBindingState.Attached, "plant-b"),
            Configuration("plant-a"));

        Assert.Equal("plant-b", authority.ProjectKey);
        Assert.Equal(EngineeringInstallationBindingState.Attached, authority.BindingState);
        Assert.False(authority.UsesLegacyConfiguration);
        Assert.True(authority.Matches("PLANT-B"));
        Assert.False(authority.Matches("plant-a"));
    }

    [Theory]
    [InlineData(EngineeringInstallationBindingState.Neutral)]
    [InlineData(EngineeringInstallationBindingState.AttachInProgress)]
    [InlineData(EngineeringInstallationBindingState.DetachInProgress)]
    public async Task PostLegacyNonAttachedStatesDoNotFallBackToStaticConfiguration(
        EngineeringInstallationBindingState state)
    {
        var authority = await EngineeringInstallationApplicationAuthorityResolver.ResolveAsync(
            new FixedBindingStore(state, "plant-b"),
            Configuration("plant-a"));

        Assert.Null(authority.ProjectKey);
        Assert.Equal(state, authority.BindingState);
        Assert.False(authority.UsesLegacyConfiguration);
        Assert.False(authority.Matches("plant-a"));
        Assert.False(authority.Matches("plant-b"));
    }

    [Fact]
    public async Task LegacyBindingKeepsStaticConfigurationCompatibility()
    {
        var authority = await EngineeringInstallationApplicationAuthorityResolver.ResolveAsync(
            new FixedBindingStore(EngineeringInstallationBindingState.Legacy, null),
            Configuration(" legacy-project "));

        Assert.Equal("legacy-project", authority.ProjectKey);
        Assert.Equal(EngineeringInstallationBindingState.Legacy, authority.BindingState);
        Assert.True(authority.UsesLegacyConfiguration);
    }

    [Fact]
    public async Task MissingBindingStoreKeepsFocusedHostCompatibility()
    {
        var authority = await EngineeringInstallationApplicationAuthorityResolver.ResolveAsync(
            bindingStore: null,
            Configuration("focused-host"));

        Assert.Equal("focused-host", authority.ProjectKey);
        Assert.Null(authority.BindingState);
        Assert.True(authority.UsesLegacyConfiguration);
    }

    private static IConfiguration Configuration(string? projectKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EngineeringRuntime:ProjectKey"] = projectKey
            })
            .Build();

    private sealed class FixedBindingStore(
        EngineeringInstallationBindingState state,
        string? projectKey) : IEngineeringInstallationBindingStore
    {
        private readonly EngineeringInstallationBindingSnapshot _snapshot =
            new(state, projectKey, 1, DateTimeOffset.UtcNow);

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EngineeringInstallationBindingSnapshot> GetAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_snapshot);

        public Task<EngineeringInstallationBindingSnapshot> AdoptLegacyAsync(
            string? projectKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringInstallationBindingSnapshot> BeginAttachAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringInstallationBindingSnapshot> CompleteAttachAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringInstallationBindingSnapshot> AbortAttachAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringInstallationBindingSnapshot> BeginDetachAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringInstallationBindingSnapshot> CompleteDetachAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
