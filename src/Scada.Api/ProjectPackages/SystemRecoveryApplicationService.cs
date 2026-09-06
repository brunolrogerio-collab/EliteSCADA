using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Reports;
using Scada.Engineering.VisualAssets;
using Scada.Security.Authentication;

namespace Scada.Api.ProjectPackages;

public sealed record SystemRecoveryApplicationPreview(
    ProjectPackageManifest Manifest,
    ImportPreview ImportPreview,
    string? ConfiguredProjectKey,
    bool ProjectCatalogEmpty,
    bool RuntimeBindingMatches,
    int CompatibleAdministratorCount,
    SystemRecoveryAuthorityAdmission? CurrentUserAdmission,
    bool CanApply,
    IReadOnlyCollection<string> Blockers);

public sealed record SystemRecoveryApplicationApplyResult(
    bool AppliedToWorkspace,
    bool DurableRevisionSaved,
    bool Published,
    bool Activated,
    string Stage,
    ProjectPackageManifest Manifest,
    ImportPreview ReplacementPreview,
    ImportResult? Apply,
    EngineeringProjectSnapshot? Revision,
    EngineeringProjectPublication? Publication,
    EngineeringProjectActivation? Activation,
    EngineeringProjectLifecycle? Lifecycle,
    IReadOnlyCollection<string> Issues)
{
    public bool Recovered =>
        AppliedToWorkspace &&
        DurableRevisionSaved &&
        Published &&
        Activated &&
        Revision is not null &&
        Activation?.ActiveRevision == Revision.Revision &&
        Lifecycle?.ActiveRevision == Revision.Revision;
}

/// <summary>
/// Coordinates first-project application recovery without introducing a disposable
/// product identity or project. The caller must already be authenticated as a real
/// restored/local Authority identity. Authorization is evaluated prospectively
/// against the SecurityRoles carried by the package being recovered.
/// </summary>
public sealed class SystemRecoveryApplicationService(
    IProjectPackageService packages,
    IEngineeringProjectPersistenceService persistence,
    IEngineeringProjectCatalog catalog,
    IPublishedRuntimeActivationService activation,
    ILocalIdentityStore identities,
    EngineeringWorkspace workspace,
    IEngineeringExchangeService exchange,
    IGatewayEngineeringRegistry gateways,
    IReportEngineeringRegistry reports,
    InitialInstallationGate installationGate,
    IConfiguration configuration)
{
    public async Task<SystemRecoveryApplicationPreview> PreviewAsync(
        ReadOnlyMemory<byte> packageBytes,
        LocalUserAccount? currentUser,
        bool requireCurrentUserAdmission,
        CancellationToken cancellationToken = default)
    {
        var inspection = packages.Inspect(packageBytes);
        var preview = packages.Preview(packageBytes, ImportMode.CreateAndUpdate);
        var configuredProjectKey = configuration["EngineeringRuntime:ProjectKey"]?.Trim();
        var catalogEmpty = !await catalog.HasAnyAsync(cancellationToken);
        var bindingMatches =
            !string.IsNullOrWhiteSpace(configuredProjectKey) &&
            configuredProjectKey.Equals(inspection.Manifest.ProjectKey, StringComparison.OrdinalIgnoreCase);

        var users = await identities.ListAsync(cancellationToken);
        var admissions = users
            .Select(user => SystemRecoveryAuthorityAdmissionEvaluator.Evaluate(inspection.Engineering, user))
            .ToArray();
        var compatibleAdministratorCount = admissions.Count(x => x.Allowed);
        var currentAdmission = currentUser is null
            ? null
            : SystemRecoveryAuthorityAdmissionEvaluator.Evaluate(inspection.Engineering, currentUser);

        var blockers = new List<string>();
        if (!catalogEmpty)
            blockers.Add("A persisted Engineering project already exists. First-project System Recovery is closed.");
        if (string.IsNullOrWhiteSpace(configuredProjectKey))
            blockers.Add("EngineeringRuntime:ProjectKey must be configured before application recovery can activate Runtime.");
        else if (!bindingMatches)
            blockers.Add($"This runtime instance is bound to project '{configuredProjectKey}', not package project '{inspection.Manifest.ProjectKey}'.");
        if (!preview.CanApply)
            blockers.Add("The application package has blocking Engineering preview errors.");
        if (requireCurrentUserAdmission && currentAdmission?.Allowed != true)
            blockers.Add(currentAdmission?.Reason ?? "The current restored Authority identity is not admitted by the recovered application policy.");
        if (requireCurrentUserAdmission && compatibleAdministratorCount == 0)
            blockers.Add("The restored Authority has no enabled identity that the recovered application grants EngineeringModify plus UserRoleAdmin or SystemAdmin.");

        return new SystemRecoveryApplicationPreview(
            inspection.Manifest,
            preview,
            configuredProjectKey,
            catalogEmpty,
            bindingMatches,
            compatibleAdministratorCount,
            currentAdmission,
            blockers.Count == 0,
            blockers);
    }

    public async Task<SystemRecoveryApplicationApplyResult> ApplyAsync(
        ReadOnlyMemory<byte> packageBytes,
        LocalUserAccount currentUser,
        string? savedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        await using var installationLease = await installationGate.EnterAsync(cancellationToken);
        var preflight = await PreviewAsync(
            packageBytes,
            currentUser,
            requireCurrentUserAdmission: true,
            cancellationToken);
        if (!preflight.CanApply)
        {
            return FailedBeforeMutation(
                preflight,
                "preflight",
                preflight.Blockers);
        }

        await using var mutation = await workspace.AcquireMutationAsync(
            cancellationToken: cancellationToken);

        var backupJson = exchange.ExportJson(indented: false);
        var backupPackage = exchange.ParseJson(backupJson);
        var backupHashes = (backupPackage.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
            .Select(x => x.Sha256)
            .ToArray();
        var backupContext = new EngineeringImportContext(
            workspace.VisualAssets.SnapshotPayloads(backupHashes));
        var backupDescriptor = workspace.Describe();

        ClearWorkspace();

        ImportPreview replacementPreview;
        ImportResult applyResult;
        try
        {
            // This is the definitive replacement preview. It runs after the old Working
            // model has been cleared, so package references cannot accidentally resolve
            // through seeded/demo content that is not present in the recovered package.
            replacementPreview = packages.Preview(packageBytes, ImportMode.CreateAndUpdate);
            if (!replacementPreview.CanApply)
            {
                RestoreBackup(backupPackage, backupContext, backupDescriptor);
                return new SystemRecoveryApplicationApplyResult(
                    false,
                    false,
                    false,
                    false,
                    "replacement-preview",
                    preflight.Manifest,
                    replacementPreview,
                    null,
                    null,
                    null,
                    null,
                    null,
                    replacementPreview.Items
                        .SelectMany(x => x.Issues)
                        .Where(x => x.IsError)
                        .Select(x => $"{x.Code}: {x.Message}")
                        .ToArray());
            }

            applyResult = packages.Apply(packageBytes, ImportMode.CreateAndUpdate);
            if (applyResult.Issues.Any(x => x.IsError))
            {
                RestoreBackup(backupPackage, backupContext, backupDescriptor);
                return new SystemRecoveryApplicationApplyResult(
                    false,
                    false,
                    false,
                    false,
                    "workspace-apply",
                    preflight.Manifest,
                    replacementPreview,
                    applyResult,
                    null,
                    null,
                    null,
                    null,
                    applyResult.Issues
                        .Where(x => x.IsError)
                        .Select(x => $"{x.Code}: {x.Message}")
                        .ToArray());
            }
        }
        catch
        {
            RestoreBackup(backupPackage, backupContext, backupDescriptor);
            throw;
        }

        EngineeringProjectSnapshot snapshot;
        try
        {
            var saveVersion = workspace.CaptureChangeVersion();
            snapshot = await persistence.SaveCurrentDerivedAsync(
                preflight.Manifest.ProjectKey,
                preflight.Manifest.ProjectName,
                basedOnRevision: null,
                savedBy,
                cancellationToken);
            workspace.AcceptSave(
                snapshot.ProjectKey,
                snapshot.ProjectName,
                snapshot.Revision,
                snapshot.SavedAtUtc,
                saveVersion);
        }
        catch
        {
            // Store implementations own the atomic revision/blob transaction. Until a
            // successful snapshot is returned there is no accepted durable recovery
            // checkpoint, so restoring the previous in-memory Working state is honest.
            RestoreBackup(backupPackage, backupContext, backupDescriptor);
            throw;
        }

        // From this point onward a durable root revision exists. Do not fake rollback to
        // an empty installation: any later failure is returned as explicit partial state.
        var publication = await persistence.PublishRevisionAsync(
            snapshot.ProjectKey,
            snapshot.Revision,
            savedBy,
            cancellationToken);
        if (publication is null || !publication.Published || publication.Publication is null)
        {
            var lifecycle = await persistence.GetLifecycleAsync(snapshot.ProjectKey, cancellationToken);
            return new SystemRecoveryApplicationApplyResult(
                true,
                true,
                false,
                false,
                "publish",
                preflight.Manifest,
                replacementPreview,
                applyResult,
                snapshot,
                null,
                null,
                lifecycle,
                publication?.Preview.Items
                    .SelectMany(x => x.Issues)
                    .Where(x => x.IsError)
                    .Select(x => $"{x.Code}: {x.Message}")
                    .DefaultIfEmpty("Recovered application was saved as a durable root revision but could not be published.")
                    .ToArray()
                ?? ["Recovered application was saved as a durable root revision but publication did not complete."]);
        }

        var activationOutcome = await activation.ActivateAsync(
            snapshot.ProjectKey,
            savedBy,
            cancellationToken);
        var lifecycleAfterActivation = activationOutcome.Lifecycle
            ?? await persistence.GetLifecycleAsync(snapshot.ProjectKey, cancellationToken);
        var activated =
            activationOutcome.Activated &&
            activationOutcome.Activation?.ActiveRevision == snapshot.Revision &&
            lifecycleAfterActivation.ActiveRevision == snapshot.Revision;

        if (!activated)
        {
            return new SystemRecoveryApplicationApplyResult(
                true,
                true,
                true,
                false,
                "activate",
                preflight.Manifest,
                replacementPreview,
                applyResult,
                snapshot,
                publication.Publication,
                activationOutcome.Activation,
                lifecycleAfterActivation,
                ["Recovered application was saved and published, but the accepted Active Runtime revision was not established."]);
        }

        return new SystemRecoveryApplicationApplyResult(
            true,
            true,
            true,
            true,
            "complete",
            preflight.Manifest,
            replacementPreview,
            applyResult,
            snapshot,
            publication.Publication,
            activationOutcome.Activation,
            lifecycleAfterActivation,
            Array.Empty<string>());
    }

    private static SystemRecoveryApplicationApplyResult FailedBeforeMutation(
        SystemRecoveryApplicationPreview preview,
        string stage,
        IReadOnlyCollection<string> issues) =>
        new(
            false,
            false,
            false,
            false,
            stage,
            preview.Manifest,
            preview.ImportPreview,
            null,
            null,
            null,
            null,
            null,
            issues);

    private void RestoreBackup(
        EngineeringPackage backupPackage,
        EngineeringImportContext backupContext,
        EngineeringWorkspaceDescriptor backupDescriptor)
    {
        ImportResult? restored = null;
        try
        {
            ClearWorkspace();
            restored = exchange.Apply(backupPackage, ImportMode.CreateAndUpdate, backupContext);
        }
        finally
        {
            workspace.RestoreDescriptor(backupDescriptor);
        }

        if (restored is null || restored.Issues.Any(x => x.IsError))
            throw new InvalidOperationException(
                "System Recovery failed before a durable revision was accepted and the previous Engineering Workspace could not be restored cleanly.");
    }

    private void ClearWorkspace()
    {
        workspace.Clear();
        gateways.Clear();
        reports.Clear();
    }
}
