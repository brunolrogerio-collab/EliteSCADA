using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Security.Authentication;

namespace Scada.Api.Security;

/// <summary>
/// Derives the first persisted lifecycle marker from already-initialized Authority evidence.
/// A newly introduced lifecycle table never implies that an Authority is attached.
/// </summary>
public sealed class AuthorityLifecycleBootstrapService(
    IAuthorityLifecycleStore lifecycle,
    ILocalIdentityStore identities,
    IAuthorityPolicyStore authorityPolicies,
    AuthorityPolicyBootstrapService policyBootstrap,
    IEngineeringProjectCatalog? catalog)
{
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await lifecycle.InitializeAsync(cancellationToken);
        var state = await lifecycle.GetAsync(cancellationToken);
        if (state.State is AuthorityLifecycleState.DetachInProgress or AuthorityLifecycleState.AttachInProgress)
            throw new InvalidOperationException("Authority transition is in progress. Startup is fail-closed until recovery completes the durable Authority transition.");
        if (state.State == AuthorityLifecycleState.Invalid)
            throw new InvalidOperationException("Authority lifecycle evidence is invalid. Startup is fail-closed.");
        if (state.State == AuthorityLifecycleState.DeliberatelyDetached)
            return;

        var userCount = await identities.CountAsync(cancellationToken);
        if (state.State == AuthorityLifecycleState.AuthorityPresent && userCount == 0)
        {
            await lifecycle.MarkInvalidAsync(cancellationToken);
            throw new InvalidOperationException("Authority lifecycle says attached but no local Authority identity exists. Startup is fail-closed.");
        }

        if (state.State == AuthorityLifecycleState.InitialInstallation && userCount == 0)
        {
            if (catalog is null || await catalog.HasAnyAsync(cancellationToken))
            {
                await lifecycle.MarkInvalidAsync(cancellationToken);
                throw new InvalidOperationException(
                    "An empty local identity store is not sufficient to prove initial installation. Startup is fail-closed until Authority recovery evidence is available.");
            }

            await policyBootstrap.EnsureInitializedAsync(cancellationToken);
            return;
        }

        await policyBootstrap.EnsureInitializedAsync(cancellationToken);
        if (authorityPolicies.Snapshot().Roles.Count == 0)
        {
            await lifecycle.MarkInvalidAsync(cancellationToken);
            throw new InvalidOperationException("Authority identities exist without canonical Authority policy. Startup is fail-closed.");
        }

        if (state.State == AuthorityLifecycleState.InitialInstallation)
            await lifecycle.MarkAuthorityPresentAsync(cancellationToken);
    }
}
