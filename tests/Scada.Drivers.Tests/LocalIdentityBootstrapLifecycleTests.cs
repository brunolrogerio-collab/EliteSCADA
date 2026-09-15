using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Security;
using Scada.Engineering.Persistence;
using Scada.Security.Authentication;

namespace Scada.Drivers.Tests;

public sealed class LocalIdentityBootstrapLifecycleTests
{
    [Fact]
    public async Task Bootstrap_IsAvailableOnlyForExplicitInitialInstallation()
    {
        var initial = new InMemoryAuthorityLifecycleStore();
        var initialStatus = await ResolveAsync(initial);
        Assert.True(initialStatus.Required);
        Assert.True(initialStatus.Available);

        await initial.MarkAuthorityPresentAsync();
        await initial.BeginDetachAsync();
        await initial.CompleteDetachAsync();
        var detachedStatus = await ResolveAsync(initial);
        Assert.True(detachedStatus.Required);
        Assert.False(detachedStatus.Available);
        Assert.Equal("authority-lifecycle-deliberatelydetached", detachedStatus.BlockedReason);
    }

    [Fact]
    public async Task Bootstrap_IsFailClosed_WhenLifecycleStateIsUnavailable()
    {
        var status = await ResolveAsync(new UnavailableLifecycleStore());

        Assert.True(status.Required);
        Assert.False(status.Available);
        Assert.Equal("authority-lifecycle-invalid", status.BlockedReason);
    }

    private static async Task<LocalIdentityApi.InitialAdministratorBootstrapStatus> ResolveAsync(
        IAuthorityLifecycleStore lifecycle)
    {
        var identities = new InMemoryLocalIdentityStore();
        var services = new ServiceCollection()
            .AddSingleton(lifecycle)
            .AddSingleton<IEngineeringProjectCatalog>(new EmptyCatalog())
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        return await LocalIdentityApi.ResolveBootstrapStatusAsync(
            context,
            new LocalIdentityRuntimeOptions(true, true, true, "test", DurableStore: true),
            new LocalIdentityBootstrapService(identities),
            CancellationToken.None);
    }

    private sealed class EmptyCatalog : IEngineeringProjectCatalog
    {
        public Task<IReadOnlyCollection<EngineeringProjectCatalogEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectCatalogEntry>>(Array.Empty<EngineeringProjectCatalogEntry>());
    }

    private sealed class UnavailableLifecycleStore : IAuthorityLifecycleStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AuthorityLifecycleSnapshot> GetAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("corrupt lifecycle state");
        public Task<AuthorityLifecycleSnapshot> MarkAuthorityPresentAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<AuthorityLifecycleSnapshot> MarkInvalidAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<AuthorityLifecycleSnapshot> BeginDetachAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<AuthorityLifecycleSnapshot> CompleteDetachAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<AuthorityLifecycleSnapshot> BeginAttachAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<AuthorityLifecycleSnapshot> CompleteAttachAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<AuthorityLifecycleSnapshot> AbortAttachAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
