using Scada.Api.Licensing;
using Scada.Api.Realtime;
using Scada.Api.Runtime;
using Scada.Core.Abstractions;
using Scada.Core.Product.Licensing;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.Reports;
using Scada.Security.Authentication;

namespace Scada.Api.Security;

public enum InstallationDetachLicenseAction
{
    Keep,
    Remove,
    Replace
}

public sealed record InstallationDetachRequest(
    bool AcknowledgeUnsavedWorking,
    bool AcknowledgeApplicationRemoval,
    bool AcknowledgeAuthorityRemoval,
    bool AcknowledgeHistorianPreserved,
    InstallationDetachLicenseAction LicenseAction = InstallationDetachLicenseAction.Keep,
    string? ReplacementLicenseCode = null);

public sealed record InstallationDetachPreflight(
    bool CanDetach,
    string? ProjectKey,
    string? ProjectName,
    long? WorkingRevision,
    long? PublishedRevision,
    long? ActiveRevision,
    bool UnsavedWorking,
    bool RuntimeActive,
    string AuthorityState,
    long AuthorityEpoch,
    string LicenseState,
    bool EngineeringLocked,
    bool HistorianPreservedByDefault,
    bool ApplicationExportRecommended,
    bool AuthorityExportRecommended,
    IReadOnlyCollection<string> RequiredAcknowledgements,
    IReadOnlyCollection<string> Blockers);

public sealed record InstallationDetachResult(
    bool Detached,
    string Stage,
    string? DetachedProjectKey,
    long? DetachedRevision,
    long AuthorityEpoch,
    string LicenseState,
    string LicenseOutcome,
    bool HistorianPreserved,
    bool SignInRequired,
    IReadOnlyCollection<string> Issues);

/// <summary>
/// Composes the already-frozen Application, Authority and Licensing authorities into one
/// fail-closed installation detach. Historian is intentionally untouched.
/// </summary>
public sealed class InstallationDetachService(
    InitialInstallationGate installationGate,
    IEngineeringInstallationBindingStore binding,
    IEngineeringProjectCatalog catalog,
    IEngineeringProjectPersistenceService persistence,
    EngineeringWorkspace workspace,
    IEngineeringExchangeService exchange,
    IGatewayEngineeringRegistry gateways,
    IReportEngineeringRegistry reports,
    IInstallationRuntimeFence runtimeFence,
    IEngineeringRuntimeCoordinator runtime,
    IScadaEventBus? eventBus,
    IConfiguration configuration,
    AuthorityDetachService authorityDetach,
    IAuthorityLifecycleStore authorityLifecycle,
    IProductLicenseService licensing,
    ProductLicenseLifecycleCoordinator licenseLifecycle)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await binding.InitializeAsync(cancellationToken);
        var current = await binding.GetAsync(cancellationToken);
        if (current.State == EngineeringInstallationBindingState.Legacy)
        {
            var projects = await catalog.ListAsync(cancellationToken);
            var configured = Normalize(configuration["EngineeringRuntime:ProjectKey"]);
            string? adopted = null;
            if (configured is not null &&
                projects.Any(project => project.ProjectKey.Equals(configured, StringComparison.OrdinalIgnoreCase)))
            {
                adopted = configured;
            }
            else if (projects.Count == 1)
            {
                adopted = projects.Single().ProjectKey;
            }
            else if (projects.Count > 1)
            {
                throw new InvalidOperationException(
                    "FND-07 cannot infer the attached Application from multiple persisted projects. " +
                    "Configure EngineeringRuntime:ProjectKey before enabling installation detach.");
            }

            current = await binding.AdoptLegacyAsync(adopted, cancellationToken);
        }

        if (current.State == EngineeringInstallationBindingState.DetachInProgress)
            await RecoverInterruptedDetachAsync(current, cancellationToken);
        else if (current.State == EngineeringInstallationBindingState.AttachInProgress)
            await RecoverInterruptedAttachAsync(current, cancellationToken);

        current = await binding.GetAsync(cancellationToken);
        if (current.State == EngineeringInstallationBindingState.Neutral)
            await runtimeFence.FenceProcessEffectsForInstallationDetachAsync(cancellationToken);
    }

    public async Task<InstallationDetachPreflight> PreflightAsync(
        InstallationDetachRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var installationBinding = await binding.GetAsync(cancellationToken);
        var authority = await authorityLifecycle.GetAsync(cancellationToken);
        var descriptor = workspace.Describe();
        var runtimeDescriptor = runtime.Describe();

        var projectKey = installationBinding.ProjectKey ?? descriptor.ProjectKey ?? runtimeDescriptor.ProjectKey;
        EngineeringProjectLifecycle? lifecycle = null;
        if (!string.IsNullOrWhiteSpace(projectKey))
            lifecycle = await persistence.GetLifecycleAsync(projectKey, cancellationToken);

        var required = new List<string>();
        if (descriptor.IsDirty && !request.AcknowledgeUnsavedWorking)
            required.Add(nameof(request.AcknowledgeUnsavedWorking));
        if (!request.AcknowledgeApplicationRemoval)
            required.Add(nameof(request.AcknowledgeApplicationRemoval));
        if (!request.AcknowledgeAuthorityRemoval)
            required.Add(nameof(request.AcknowledgeAuthorityRemoval));
        if (!request.AcknowledgeHistorianPreserved)
            required.Add(nameof(request.AcknowledgeHistorianPreserved));

        var blockers = new List<string>();
        if (installationBinding.State != EngineeringInstallationBindingState.Attached ||
            string.IsNullOrWhiteSpace(installationBinding.ProjectKey))
        {
            blockers.Add("Installation detach requires one attached Application.");
        }
        if (authority.State != AuthorityLifecycleState.AuthorityPresent)
            blockers.Add("Installation detach requires an attached, valid Security Authority.");
        if (request.LicenseAction == InstallationDetachLicenseAction.Replace &&
            string.IsNullOrWhiteSpace(request.ReplacementLicenseCode))
        {
            blockers.Add("License replacement requires a candidate license code.");
        }
        if (request.LicenseAction != InstallationDetachLicenseAction.Replace &&
            !string.IsNullOrWhiteSpace(request.ReplacementLicenseCode))
        {
            blockers.Add("A replacement license code is accepted only when licenseAction is Replace.");
        }

        return new InstallationDetachPreflight(
            blockers.Count == 0 && required.Count == 0,
            projectKey,
            descriptor.ProjectName,
            lifecycle?.WorkingRevision ?? descriptor.BaseRevision,
            lifecycle?.PublishedRevision,
            lifecycle?.ActiveRevision,
            descriptor.IsDirty,
            runtimeDescriptor.Revision.HasValue,
            authority.State.ToString(),
            authority.Epoch,
            licensing.CurrentVerification.State.ToString(),
            EngineeringLockAccess.IsLocked(exchange),
            HistorianPreservedByDefault: true,
            ApplicationExportRecommended: true,
            AuthorityExportRecommended: true,
            required,
            blockers);
    }

    public async Task<InstallationDetachResult> DetachAsync(
        InstallationDetachRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var installationLease = await installationGate.EnterAsync(cancellationToken);
        var preflight = await PreflightAsync(request, cancellationToken);
        if (!preflight.CanDetach || string.IsNullOrWhiteSpace(preflight.ProjectKey))
        {
            return Failure(
                "preflight",
                preflight.ProjectKey,
                preflight.ActiveRevision,
                preflight.AuthorityEpoch,
                preflight.LicenseState,
                preflight.Blockers.Concat(
                    preflight.RequiredAcknowledgements.Select(x => $"Explicit acknowledgement required: {x}.")).ToArray());
        }

        var projectKey = preflight.ProjectKey;
        await binding.BeginDetachAsync(projectKey, cancellationToken);

        // The process-effect fence is first. Once set, direct Runtime mutations and fallback
        // simulation writes fail closed even before Runtime/Authority teardown completes.
        await runtimeFence.FenceProcessEffectsForInstallationDetachAsync(CancellationToken.None);
        await DeactivateScriptsAsync(CancellationToken.None);
        var stopped = await runtimeFence.StopFencedRuntimeForInstallationDetachAsync(CancellationToken.None);

        await using (var mutation = await workspace.AcquireMutationAsync(cancellationToken: CancellationToken.None))
        {
            await persistence.DeleteProjectAsync(projectKey, CancellationToken.None);
            workspace.ResetToNeutral();
            gateways.Clear();
            reports.Clear();
        }

        await authorityDetach.DetachAsync(CancellationToken.None);
        var completed = await binding.CompleteDetachAsync(projectKey, CancellationToken.None);

        var licenseOutcome = await ApplyLicenseChoiceAsync(request, CancellationToken.None);
        var finalAuthority = await authorityLifecycle.GetAsync(CancellationToken.None);
        var finalLicense = licensing.CurrentVerification.State;

        return new InstallationDetachResult(
            Detached: completed.IsNeutral && finalAuthority.State == AuthorityLifecycleState.DeliberatelyDetached,
            Stage: "complete",
            DetachedProjectKey: projectKey,
            DetachedRevision: stopped.Revision,
            AuthorityEpoch: finalAuthority.Epoch,
            LicenseState: finalLicense.ToString(),
            LicenseOutcome: licenseOutcome,
            HistorianPreserved: true,
            SignInRequired: true,
            Issues: Array.Empty<string>());
    }

    private async Task<string> ApplyLicenseChoiceAsync(
        InstallationDetachRequest request,
        CancellationToken cancellationToken)
    {
        switch (request.LicenseAction)
        {
            case InstallationDetachLicenseAction.Keep:
                return "kept";
            case InstallationDetachLicenseAction.Remove:
            {
                var result = await licenseLifecycle.RemoveAsync(cancellationToken);
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Application/Authority detach completed, but license removal did not complete ({result.ReasonCode}).");
                return result.ReasonCode;
            }
            case InstallationDetachLicenseAction.Replace:
            {
                var result = await licenseLifecycle.InstallOrReplaceAsync(
                    request.ReplacementLicenseCode!,
                    cancellationToken);
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Application/Authority detach completed, but license replacement was rejected ({result.ReasonCode}).");
                return result.ReasonCode;
            }
            default:
                throw new InvalidOperationException("Unsupported installation detach license action.");
        }
    }

    private async Task DeactivateScriptsAsync(CancellationToken cancellationToken)
    {
        if (eventBus is null) return;
        var scripts = ServerScriptRuntimeManager.GetShared(runtime, eventBus, configuration);
        await scripts.DeactivateForInstallationDetachAsync(cancellationToken);
    }

    private async Task RecoverInterruptedDetachAsync(
        EngineeringInstallationBindingSnapshot state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.ProjectKey))
            throw new InvalidOperationException("Interrupted Application detach has no project identity.");

        await runtimeFence.FenceProcessEffectsForInstallationDetachAsync(cancellationToken);
        await DeactivateScriptsAsync(cancellationToken);
        await runtimeFence.StopFencedRuntimeForInstallationDetachAsync(cancellationToken);
        await persistence.DeleteProjectAsync(state.ProjectKey, cancellationToken);
        await using (var mutation = await workspace.AcquireMutationAsync(cancellationToken: cancellationToken))
        {
            workspace.ResetToNeutral();
            gateways.Clear();
            reports.Clear();
        }
        await binding.CompleteDetachAsync(state.ProjectKey, cancellationToken);
    }

    private async Task RecoverInterruptedAttachAsync(
        EngineeringInstallationBindingSnapshot state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.ProjectKey))
            throw new InvalidOperationException("Interrupted Application attach has no project identity.");

        // A durable revision means Application recovery crossed its accepted checkpoint.
        // Finish the binding; later publish/activate recovery remains the existing lifecycle's job.
        var latest = await persistence.LoadLatestAsync(state.ProjectKey, cancellationToken);
        if (latest is not null)
        {
            await binding.CompleteAttachAsync(state.ProjectKey, cancellationToken);
            return;
        }

        await binding.AbortAttachAsync(state.ProjectKey, cancellationToken);
        await runtimeFence.FenceProcessEffectsForInstallationDetachAsync(cancellationToken);
    }

    private static InstallationDetachResult Failure(
        string stage,
        string? projectKey,
        long? revision,
        long authorityEpoch,
        string licenseState,
        IReadOnlyCollection<string> issues) =>
        new(
            false,
            stage,
            projectKey,
            revision,
            authorityEpoch,
            licenseState,
            "unchanged",
            true,
            false,
            issues);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
