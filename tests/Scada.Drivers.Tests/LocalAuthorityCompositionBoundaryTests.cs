using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Api.Realtime;
using Scada.DriverHost.Engineering;
using Scada.Engineering.Contracts;
using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class LocalAuthorityCompositionBoundaryTests
{
    [Fact]
    public async Task DisabledLocalAuthorityStartsWithPersistenceBindingWithoutResolvingDetachServices()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(new LocalIdentityRuntimeOptions(
            AuthenticationEnabled: false,
            Enabled: false,
            SecureCookie: true,
            CookieName: "test"));
        builder.Services.AddSingleton<IAuditStore>(new InMemoryAuditSink(new AuditQueryPolicy()));
        builder.Services.AddSingleton(new EngineeringWorkspace(seedDemo: false));
        builder.Services.AddSingleton<IAuthorityPolicyStore, InMemoryAuthorityPolicyStore>();
        builder.Services.AddSingleton(new AuthorityPolicyBootstrapOptions(null));
        builder.Services.AddSingleton(sp => new AuthorityPolicyBootstrapService(
            sp.GetRequiredService<IAuthorityPolicyStore>(),
            sp.GetService<Scada.Security.Authentication.ILocalIdentityStore>(),
            null,
            null,
            sp.GetRequiredService<AuthorityPolicyBootstrapOptions>(),
            sp.GetService<EngineeringWorkspace>()));
        builder.Services.AddSingleton<IEngineeringInstallationBindingStore, NeutralBindingStore>();

        await using var app = builder.Build();

        await app.InitializeInstallationFoundationAsync();

        Assert.Null(app.Services.GetService<InstallationDetachService>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task InstallationDetachEndpointsFollowLocalAuthorityAvailability(
        bool localAuthorityEnabled,
        bool expectedToBeMapped)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(new LocalIdentityRuntimeOptions(
            AuthenticationEnabled: localAuthorityEnabled,
            Enabled: localAuthorityEnabled,
            SecureCookie: true,
            CookieName: "test"));
        if (localAuthorityEnabled)
        {
            builder.Services.AddSingleton<ScadaRuntimeFacade>(_ => throw new NotSupportedException());
            builder.Services.AddSingleton<ApiAuthorizationService>(_ => throw new NotSupportedException());
            builder.Services.AddSingleton<ApiAuditService>(_ => throw new NotSupportedException());
            builder.Services.AddSingleton<InstallationDetachService>(_ => throw new NotSupportedException());
            builder.Services.AddSingleton<TagRealtimeHub>(_ => throw new NotSupportedException());
        }
        await using var app = builder.Build();

        app.MapInstallationDetachEndpoints();

        var patterns = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.Equal(expectedToBeMapped, patterns.Contains("/api/installation/detach/preflight"));
        Assert.Equal(expectedToBeMapped, patterns.Contains("/api/installation/detach"));
    }

    private sealed class NeutralBindingStore : IEngineeringInstallationBindingStore
    {
        private static readonly EngineeringInstallationBindingSnapshot Snapshot = new(
            EngineeringInstallationBindingState.Neutral,
            null,
            1,
            DateTimeOffset.UtcNow);

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<EngineeringInstallationBindingSnapshot> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshot);
        public Task<EngineeringInstallationBindingSnapshot> AdoptLegacyAsync(string? projectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshot);
        public Task<EngineeringInstallationBindingSnapshot> BeginAttachAsync(string projectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<EngineeringInstallationBindingSnapshot> CompleteAttachAsync(string projectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<EngineeringInstallationBindingSnapshot> AbortAttachAsync(string projectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<EngineeringInstallationBindingSnapshot> BeginDetachAsync(string projectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<EngineeringInstallationBindingSnapshot> CompleteDetachAsync(string projectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
