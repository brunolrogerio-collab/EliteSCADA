using Scada.Api.Persistence;
using Scada.Engineering.Persistence;

namespace Scada.Api.Security;

/// <summary>
/// One production startup sequence for the installation-level authorities that must be
/// ordered around persisted Engineering. The sequence is intentionally testable as one
/// unit so attach/detach journal recovery cannot drift behind Working checkout again.
/// </summary>
public static class InstallationStartupOrchestration
{
    public static Task InitializeInstallationFoundationAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            app.InitializeEngineeringPersistenceStorageAsync,
            async ct =>
            {
                await app.InitializeAuditAsync(ct);
                var localIdentityRuntime = app.Services.GetRequiredService<LocalIdentityRuntimeOptions>();
                if (localIdentityRuntime.Enabled)
                {
                    await app.Services.GetRequiredService<AuthorityDetachService>()
                        .RecoverIfInProgressAsync(ct);
                    await app.Services.GetRequiredService<AuthorityLifecycleBootstrapService>()
                        .EnsureInitializedAsync(ct);
                }
                else
                {
                    // Policy remains canonical Authority state even when local authentication
                    // is disabled. Only the local-identity lifecycle is unavailable.
                    await app.Services.GetRequiredService<AuthorityPolicyBootstrapService>()
                        .EnsureInitializedAsync(ct);
                }
            },
            async ct =>
            {
                var localIdentityRuntime = app.Services.GetRequiredService<LocalIdentityRuntimeOptions>();
                if (localIdentityRuntime.Enabled &&
                    app.Services.GetService<IEngineeringInstallationBindingStore>() is not null)
                    await app.Services.GetRequiredService<InstallationDetachService>()
                        .InitializeAsync(ct);
            },
            app.InitializeEngineeringPersistenceAsync,
            cancellationToken);

    internal static async Task RunAsync(
        Func<CancellationToken, Task> initializeEngineeringStorage,
        Func<CancellationToken, Task> initializeAuthority,
        Func<CancellationToken, Task> recoverInstallationJournal,
        Func<CancellationToken, Task> initializePersistedWorking,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initializeEngineeringStorage);
        ArgumentNullException.ThrowIfNull(initializeAuthority);
        ArgumentNullException.ThrowIfNull(recoverInstallationJournal);
        ArgumentNullException.ThrowIfNull(initializePersistedWorking);

        await initializeEngineeringStorage(cancellationToken);
        await initializeAuthority(cancellationToken);
        await recoverInstallationJournal(cancellationToken);
        await initializePersistedWorking(cancellationToken);
    }
}
