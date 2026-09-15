using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class AuthorityDetachServiceTests
{
    [Fact]
    public async Task Detach_ClearsCanonicalAuthority_AndFencesThePreviousEpoch()
    {
        var identities = new InMemoryLocalIdentityStore();
        var lifecycle = new InMemoryAuthorityLifecycleStore();
        var policy = Policy("authority-admin", SecurityCapability.SystemAdmin);
        await lifecycle.MarkAuthorityPresentAsync();
        var account = Account("authority-admin");
        await identities.CreateAsync(account);
        var before = await lifecycle.GetAsync();

        var result = await new AuthorityDetachService(lifecycle, identities, policy).DetachAsync();

        Assert.False(result.AlreadyDetached);
        Assert.Equal([account.Id.ToString()], result.RevokedSubjects);
        Assert.Equal(AuthorityLifecycleState.DeliberatelyDetached, result.After.State);
        Assert.Equal(before.Epoch + 1, result.After.Epoch);
        Assert.False(AuthorityLifecycleSessionFence.IsCurrent(result.After, before.Epoch));
        Assert.Equal(0, await identities.CountAsync());
        Assert.Empty(policy.Snapshot().Roles);
        Assert.Empty(policy.Snapshot().Scopes);
    }

    [Fact]
    public async Task Recovery_CompletesInterruptedJournal_Idempotently()
    {
        var identities = new InMemoryLocalIdentityStore();
        var lifecycle = new InMemoryAuthorityLifecycleStore();
        var policy = Policy("authority-admin", SecurityCapability.SystemAdmin);
        await lifecycle.MarkAuthorityPresentAsync();
        await identities.CreateAsync(Account("authority-admin"));
        await lifecycle.BeginDetachAsync();

        var service = new AuthorityDetachService(lifecycle, identities, policy);

        Assert.True(await service.RecoverIfInProgressAsync());
        Assert.False(await service.RecoverIfInProgressAsync());
        Assert.Equal(AuthorityLifecycleState.DeliberatelyDetached, (await lifecycle.GetAsync()).State);
        Assert.Equal(0, await identities.CountAsync());
        Assert.Empty(policy.Snapshot().Roles);
        Assert.Empty(policy.Snapshot().Scopes);
    }

    [Fact]
    public async Task ConcurrentDetach_ConvergesWithOneEpochAdvance_AndFencesBeforeDataClears()
    {
        var innerIdentities = new InMemoryLocalIdentityStore();
        var identities = new FirstClearBarrierLocalIdentityStore(innerIdentities);
        var lifecycle = new InMemoryAuthorityLifecycleStore();
        var policy = Policy("authority-admin", SecurityCapability.SystemAdmin);
        await lifecycle.MarkAuthorityPresentAsync();
        await innerIdentities.CreateAsync(Account("authority-admin"));
        var issued = await lifecycle.GetAsync();
        var firstService = new AuthorityDetachService(lifecycle, identities, policy);
        var secondService = new AuthorityDetachService(lifecycle, identities, policy);

        var first = firstService.DetachAsync();
        await identities.FirstClearEntered.WaitAsync(TimeSpan.FromSeconds(5));

        var fenced = await lifecycle.GetAsync();
        Assert.Equal(AuthorityLifecycleState.DetachInProgress, fenced.State);
        Assert.Equal(1, await innerIdentities.CountAsync());
        Assert.False(AuthorityLifecycleSessionFence.IsCurrent(fenced, issued.Epoch));

        var second = secondService.DetachAsync();
        var secondResult = await second;
        identities.ReleaseFirstClear();
        var firstResult = await first;

        Assert.Equal(AuthorityLifecycleState.DeliberatelyDetached, firstResult.After.State);
        Assert.Equal(AuthorityLifecycleState.DeliberatelyDetached, secondResult.After.State);
        Assert.Equal(issued.Epoch + 1, (await lifecycle.GetAsync()).Epoch);
        Assert.Equal(0, await innerIdentities.CountAsync());
        Assert.Empty(policy.Snapshot().Roles);
        Assert.Empty(policy.Snapshot().Scopes);
    }

    [Fact]
    public void SystemAdminCheck_AllowsSystemAdmin_ButNotUserRoleAdminAlone()
    {
        var policies = Policy(
            ("role-admin", SecurityCapability.UserRoleAdmin),
            ("system-admin", SecurityCapability.SystemAdmin));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var service = new ApiAuthorizationService(new EmptyServiceProvider(), policies, configuration);

        var roleAdmin = Check(service, "role-admin");
        var systemAdmin = Check(service, "system-admin");

        Assert.False(roleAdmin.Allowed);
        Assert.True(systemAdmin.Allowed);
    }

    private static ApiAuthorizationCheck Check(ApiAuthorizationService service, string role)
    {
        var context = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    [
                        new System.Security.Claims.Claim("sub", Guid.NewGuid().ToString()),
                        new System.Security.Claims.Claim("role", role)
                    ],
                    "test"))
        };
        return service.CheckWorkspace(context, SecurityCapability.SystemAdmin);
    }

    private static InMemoryAuthorityPolicyStore Policy(string role, SecurityCapability capability) =>
        Policy((role, capability));

    private static InMemoryAuthorityPolicyStore Policy(params (string Role, SecurityCapability Capability)[] grants) =>
        new(
            grants.Select((grant, index) => new SecurityRoleEngineeringDto(
                Guid.Parse($"71000000-0000-0000-0000-{index + 1:000000000000}"),
                grant.Role,
                grant.Role,
                Grants: [new CapabilityGrantEngineeringDto(grant.Capability)])));

    private static LocalUserAccount Account(string role)
    {
        var now = DateTimeOffset.UtcNow;
        return new LocalUserAccount(
            Guid.NewGuid(),
            "authority-admin",
            LocalIdentityNormalization.NormalizeUsername("authority-admin"),
            "Authority Administrator",
            true,
            [role],
            LocalPasswordHasher.Hash("authority-detach-test-password"),
            now,
            now);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class FirstClearBarrierLocalIdentityStore(ILocalIdentityStore inner) : ILocalIdentityStore
    {
        private int _firstClear;
        private readonly TaskCompletionSource _firstClearEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstClear = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstClearEntered => _firstClearEntered.Task;
        public void ReleaseFirstClear() => _releaseFirstClear.TrySetResult();
        public Task InitializeAsync(CancellationToken cancellationToken = default) => inner.InitializeAsync(cancellationToken);
        public ValueTask<IAsyncDisposable> AcquireMutationLeaseAsync(CancellationToken cancellationToken = default) => inner.AcquireMutationLeaseAsync(cancellationToken);
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => inner.CountAsync(cancellationToken);
        public Task<LocalUserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) => inner.FindByUsernameAsync(username, cancellationToken);
        public Task<LocalUserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) => inner.FindByIdAsync(id, cancellationToken);
        public Task<IReadOnlyCollection<LocalUserAccount>> ListAsync(CancellationToken cancellationToken = default) => inner.ListAsync(cancellationToken);
        public Task CreateAsync(LocalUserAccount account, CancellationToken cancellationToken = default) => inner.CreateAsync(account, cancellationToken);
        public Task UpdateAsync(LocalUserAccount account, CancellationToken cancellationToken = default) => inner.UpdateAsync(account, cancellationToken);
        public Task ReplaceAllAsync(IReadOnlyCollection<LocalUserAccount> accounts, CancellationToken cancellationToken = default) => inner.ReplaceAllAsync(accounts, cancellationToken);
        public Task<bool> TryReplaceAllIfEmptyAsync(IReadOnlyCollection<LocalUserAccount> accounts, CancellationToken cancellationToken = default) => inner.TryReplaceAllIfEmptyAsync(accounts, cancellationToken);

        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _firstClear, 1, 0) == 0)
            {
                _firstClearEntered.TrySetResult();
                await _releaseFirstClear.Task.WaitAsync(cancellationToken);
            }

            await inner.ClearAllAsync(cancellationToken);
        }
    }
}
