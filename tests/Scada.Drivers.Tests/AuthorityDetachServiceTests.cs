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
    public async Task Recovery_AbortsInterruptedAttach_ToAClosedNeutralAuthority()
    {
        var identities = new InMemoryLocalIdentityStore();
        var lifecycle = new InMemoryAuthorityLifecycleStore();
        var policy = Policy("authority-admin", SecurityCapability.SystemAdmin);
        await lifecycle.MarkAuthorityPresentAsync();
        await lifecycle.BeginDetachAsync();
        var detached = await lifecycle.CompleteDetachAsync();
        await lifecycle.BeginAttachAsync();
        await identities.CreateAsync(Account("authority-admin"));

        var service = new AuthorityDetachService(lifecycle, identities, policy);

        Assert.True(await service.RecoverIfInProgressAsync());
        var recovered = await lifecycle.GetAsync();
        Assert.Equal(AuthorityLifecycleState.DeliberatelyDetached, recovered.State);
        Assert.Equal(detached.Epoch + 1, recovered.Epoch);
        Assert.False(AuthorityLifecycleSessionFence.IsCurrent(recovered, detached.Epoch));
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

    [Fact]
    public async Task Attach_ValidatesTargetBeforeItLeavesTheClosedNeutralState()
    {
        var identities = new InMemoryLocalIdentityStore();
        var lifecycle = new InMemoryAuthorityLifecycleStore();
        var policies = new InMemoryAuthorityPolicyStore();
        await lifecycle.MarkAuthorityPresentAsync();
        await lifecycle.BeginDetachAsync();
        var detached = await lifecycle.CompleteDetachAsync();

        var invalidTarget = new AuthorityAttachTarget(
            [Account("non-admin")],
            [Role("non-admin", SecurityCapability.View)],
            Array.Empty<SecurityScopeEngineeringDto>());

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new AuthorityAttachService(lifecycle, identities, policies).AttachAsync(invalidTarget));

        Assert.Equal(detached, await lifecycle.GetAsync());
        Assert.Equal(0, await identities.CountAsync());
        Assert.Empty(policies.Snapshot().Roles);
    }

    [Fact]
    public async Task Attach_AppliesAValidatedTarget_AndAdvancesTheEpoch()
    {
        var identities = new InMemoryLocalIdentityStore();
        var lifecycle = new InMemoryAuthorityLifecycleStore();
        var policies = new InMemoryAuthorityPolicyStore();
        await lifecycle.MarkAuthorityPresentAsync();
        await lifecycle.BeginDetachAsync();
        var detached = await lifecycle.CompleteDetachAsync();
        var account = Account("authority-admin");
        var roles = new[] { Role("authority-admin", SecurityCapability.SystemAdmin) };

        var result = await new AuthorityAttachService(lifecycle, identities, policies).AttachAsync(
            new AuthorityAttachTarget([account], roles, Array.Empty<SecurityScopeEngineeringDto>()));

        Assert.Equal(AuthorityLifecycleState.AuthorityPresent, result.After.State);
        Assert.Equal(detached.Epoch + 1, result.After.Epoch);
        Assert.True(AuthorityLifecycleSessionFence.IsCurrent(result.After, result.After.Epoch));
        Assert.Equal(1, await identities.CountAsync());
        Assert.Single(policies.Snapshot().Roles);
    }

    [Fact]
    public async Task Switch_RejectsAnInvalidBackupBeforeTheCurrentAuthorityIsDetached()
    {
        var identities = new InMemoryLocalIdentityStore();
        var lifecycle = new InMemoryAuthorityLifecycleStore();
        var policies = Policy("current-admin", SecurityCapability.SystemAdmin);
        var current = Account("current-admin");
        await identities.CreateAsync(current);
        await lifecycle.MarkAuthorityPresentAsync();
        var issued = await lifecycle.GetAsync();
        var backup = new AuthorityBackupService().Export(
            [Account("replacement")],
            new AuthorityBackupPolicyPayload(1, "[]", "[]"),
            "authority-switch-test-password");
        var service = new AuthoritySwitchService(
            lifecycle,
            new AuthorityDetachService(lifecycle, identities, policies),
            new AuthorityAttachService(lifecycle, identities, policies),
            identities,
            new AuthorityBackupService());

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.SwitchAsync(backup, "authority-switch-test-password"));

        Assert.Equal(issued, await lifecycle.GetAsync());
        Assert.True(AuthorityLifecycleSessionFence.IsCurrent(await lifecycle.GetAsync(), issued.Epoch));
        Assert.Equal(current.Id, Assert.Single(await identities.ListAsync()).Id);
        Assert.Single(policies.Snapshot().Roles);
    }

    [Fact]
    public async Task Switch_ReplacesAuthority_AndFencesBothPriorAndIntermediateEpochs()
    {
        var identities = new InMemoryLocalIdentityStore();
        var lifecycle = new InMemoryAuthorityLifecycleStore();
        var policies = Policy("current-admin", SecurityCapability.SystemAdmin);
        var current = Account("current-admin");
        await identities.CreateAsync(current);
        await lifecycle.MarkAuthorityPresentAsync();
        var issued = await lifecycle.GetAsync();
        var replacement = Account("replacement-admin");
        var replacementRoles = new[] { Role("replacement-admin", SecurityCapability.SystemAdmin) };
        var backup = new AuthorityBackupService().Export(
            [replacement],
            new AuthorityBackupPolicyPayload(
                1,
                System.Text.Json.JsonSerializer.Serialize(replacementRoles),
                System.Text.Json.JsonSerializer.Serialize(Array.Empty<SecurityScopeEngineeringDto>())),
            "authority-switch-test-password");
        var service = new AuthoritySwitchService(
            lifecycle,
            new AuthorityDetachService(lifecycle, identities, policies),
            new AuthorityAttachService(lifecycle, identities, policies),
            identities,
            new AuthorityBackupService());

        var result = await service.SwitchAsync(backup, "authority-switch-test-password");

        Assert.Equal(AuthorityLifecycleState.DeliberatelyDetached, result.Detach.After.State);
        Assert.Equal(AuthorityLifecycleState.AuthorityPresent, result.Attach.After.State);
        Assert.Equal(issued.Epoch + 2, result.Attach.After.Epoch);
        Assert.False(AuthorityLifecycleSessionFence.IsCurrent(result.Attach.After, issued.Epoch));
        Assert.False(AuthorityLifecycleSessionFence.IsCurrent(result.Attach.After, result.Detach.After.Epoch));
        Assert.True(AuthorityLifecycleSessionFence.IsCurrent(result.Attach.After, result.Attach.After.Epoch));
        Assert.Equal(replacement.Id, Assert.Single(await identities.ListAsync()).Id);
        Assert.Equal("replacement-admin", Assert.Single(policies.Snapshot().Roles).Key);
    }

    [Fact]
    public async Task ConcurrentSwitches_SerializePreparationAndReplacement_AsDistinctEpochTransitions()
    {
        var innerIdentities = new InMemoryLocalIdentityStore();
        var identities = new FirstClearBarrierLocalIdentityStore(innerIdentities);
        var lifecycle = new InMemoryAuthorityLifecycleStore();
        var policies = Policy("current-admin", SecurityCapability.SystemAdmin);
        var current = Account("current-admin");
        await innerIdentities.CreateAsync(current);
        await lifecycle.MarkAuthorityPresentAsync();
        var issued = await lifecycle.GetAsync();
        var backupA = Backup(Account("replacement-a"), "replacement-a");
        var backupB = Backup(Account("replacement-b"), "replacement-b");
        var service = new AuthoritySwitchService(
            lifecycle,
            new AuthorityDetachService(lifecycle, identities, policies),
            new AuthorityAttachService(lifecycle, identities, policies),
            identities,
            new AuthorityBackupService());

        var first = service.SwitchAsync(backupA, "authority-switch-test-password");
        await identities.FirstClearEntered.WaitAsync(TimeSpan.FromSeconds(5));
        var second = service.SwitchAsync(backupB, "authority-switch-test-password");

        Assert.False(second.IsCompleted);
        Assert.Equal(AuthorityLifecycleState.DetachInProgress, (await lifecycle.GetAsync()).State);
        Assert.False(AuthorityLifecycleSessionFence.IsCurrent(await lifecycle.GetAsync(), issued.Epoch));

        identities.ReleaseFirstClear();
        var firstResult = await first;
        var secondResult = await second;

        Assert.Equal(issued.Epoch + 2, firstResult.Attach.After.Epoch);
        Assert.Equal(issued.Epoch + 4, secondResult.Attach.After.Epoch);
        Assert.Equal(AuthorityLifecycleState.AuthorityPresent, (await lifecycle.GetAsync()).State);
        Assert.False(AuthorityLifecycleSessionFence.IsCurrent(await lifecycle.GetAsync(), issued.Epoch));
        Assert.Equal("replacement-b", Assert.Single(policies.Snapshot().Roles).Key);
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

    private static SecurityRoleEngineeringDto Role(string key, SecurityCapability capability) =>
        new(Guid.NewGuid(), key, key, Grants: [new CapabilityGrantEngineeringDto(capability)]);

    private static string Backup(LocalUserAccount account, string role) =>
        new AuthorityBackupService().Export(
            [account],
            new AuthorityBackupPolicyPayload(
                1,
                System.Text.Json.JsonSerializer.Serialize(new[] { Role(role, SecurityCapability.SystemAdmin) }),
                System.Text.Json.JsonSerializer.Serialize(Array.Empty<SecurityScopeEngineeringDto>())),
            "authority-switch-test-password");

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
