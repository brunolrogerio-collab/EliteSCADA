using Scada.Api.Runtime;
using Scada.Engineering.Persistence;

namespace Scada.Api.Persistence;

public enum EngineeringWorkingBootstrapSource
{
    EmptyCatalog,
    ConfiguredWorking,
    ConfiguredRuntime,
    MostRecentlySaved
}

public sealed record EngineeringWorkingBootstrapResult(
    EngineeringWorkingBootstrapSource Source,
    bool CheckedOut,
    string? ProjectKey,
    long? Revision,
    EngineeringWorkspaceDescriptor Workspace);

public interface IEngineeringWorkingBootstrapService
{
    Task<EngineeringWorkingBootstrapResult> BootstrapAsync(
        string? configuredWorkingProjectKey,
        long? configuredWorkingRevision,
        string? configuredRuntimeProjectKey,
        CancellationToken cancellationToken = default);
}

public sealed class EngineeringWorkingBootstrapService(
    IEngineeringProjectCatalog catalog,
    IEngineeringWorkspaceCheckoutService checkout,
    EngineeringWorkspace workspace) : IEngineeringWorkingBootstrapService
{
    public async Task<EngineeringWorkingBootstrapResult> BootstrapAsync(
        string? configuredWorkingProjectKey,
        long? configuredWorkingRevision,
        string? configuredRuntimeProjectKey,
        CancellationToken cancellationToken = default)
    {
        var workingProjectKey = Normalize(configuredWorkingProjectKey);
        var runtimeProjectKey = Normalize(configuredRuntimeProjectKey);
        if (configuredWorkingRevision is < 1)
            throw new InvalidOperationException("EngineeringWorking:Revision must be greater than zero.");
        if (configuredWorkingRevision.HasValue && workingProjectKey is null)
            throw new InvalidOperationException("EngineeringWorking:Revision requires EngineeringWorking:ProjectKey.");

        var projects = (await catalog.ListAsync(cancellationToken))
            .OrderBy(entry => entry.ProjectKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (projects.Length == 0)
        {
            if (workingProjectKey is not null || runtimeProjectKey is not null)
                throw new InvalidOperationException("A configured Engineering Working/Runtime project was not found because the persisted project catalog is empty.");
            return new EngineeringWorkingBootstrapResult(
                EngineeringWorkingBootstrapSource.EmptyCatalog,
                false,
                null,
                null,
                workspace.Describe());
        }

        EngineeringWorkingBootstrapSource source;
        EngineeringProjectCatalogEntry selected;
        if (workingProjectKey is not null)
        {
            source = EngineeringWorkingBootstrapSource.ConfiguredWorking;
            selected = FindRequired(projects, workingProjectKey, "EngineeringWorking:ProjectKey");
        }
        else if (runtimeProjectKey is not null)
        {
            source = EngineeringWorkingBootstrapSource.ConfiguredRuntime;
            selected = FindRequired(projects, runtimeProjectKey, "EngineeringRuntime:ProjectKey");
        }
        else
        {
            source = EngineeringWorkingBootstrapSource.MostRecentlySaved;
            selected = projects
                .OrderByDescending(entry => entry.LastSavedAtUtc)
                .ThenBy(entry => entry.ProjectKey, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        var revision = configuredWorkingRevision ?? selected.LatestRevision;
        var outcome = await checkout.CheckoutAsync(selected.ProjectKey, revision, cancellationToken);
        if (outcome is null)
            throw new InvalidOperationException($"Persisted Engineering Working project '{selected.ProjectKey}' revision {revision} was not found.");
        if (!outcome.CheckedOut)
            throw new InvalidOperationException($"Persisted Engineering Working project '{selected.ProjectKey}' revision {revision} could not be checked out.");

        return new EngineeringWorkingBootstrapResult(
            source,
            true,
            outcome.Snapshot.ProjectKey,
            outcome.Snapshot.Revision,
            outcome.Workspace);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EngineeringProjectCatalogEntry FindRequired(
        IReadOnlyCollection<EngineeringProjectCatalogEntry> projects,
        string projectKey,
        string settingName) =>
        projects.FirstOrDefault(entry => entry.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Configured {settingName} '{projectKey}' does not exist in the persisted project catalog.");
}
