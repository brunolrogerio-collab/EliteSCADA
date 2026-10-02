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
                .ThenBy(entry => entry.ProjectKey, StringComparer.Ordinal)
                .First();
        }

        var revision = configuredWorkingRevision ?? selected.LatestRevision;
        var outcome = await checkout.CheckoutAsync(selected.ProjectKey, revision, cancellationToken);
        if (outcome is null)
            throw new InvalidOperationException($"Persisted Engineering Working project '{selected.ProjectKey}' revision {revision} was not found.");
        if (!outcome.CheckedOut)
        {
            var issues = outcome.Preview.Items
                .SelectMany(item => item.Issues)
                .Concat(outcome.ApplyResult?.Issues ?? Array.Empty<Scada.Engineering.Contracts.ImportIssue>())
                .Where(issue => issue.IsError)
                .Take(8)
                .Select(issue => $"{issue.Code} ({issue.EntityKind}:{issue.EntityKey}): {issue.Message}")
                .ToArray();
            var diagnostic = issues.Length == 0
                ? "No validation details were returned."
                : string.Join(" | ", issues);
            throw new InvalidOperationException(
                $"Persisted Engineering Working project '{selected.ProjectKey}' revision {revision} could not be checked out. {diagnostic}");
        }

        UpgradeBuiltinDynamos();
        RemoveImportedE3Dynamos();

        return new EngineeringWorkingBootstrapResult(
            source,
            true,
            outcome.Snapshot.ProjectKey,
            outcome.Snapshot.Revision,
            workspace.Describe());
    }

    private void UpgradeBuiltinDynamos()
    {
        var existingDynamos = workspace.Assets.SnapshotDynamos()
            .ToDictionary(dynamo => dynamo.Key, StringComparer.OrdinalIgnoreCase);
        var existingBuiltins = existingDynamos
            .Where(pair =>
                pair.Value.Metadata is not null &&
                pair.Value.Metadata.TryGetValue("builtinLibrary", out var isBuiltin) &&
                string.Equals(isBuiltin, "true", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        // Do not seed the platform library into projects that never opted into it.
        if (existingBuiltins.Count == 0) return;

        foreach (var latest in BuiltinDynamoLibrary.Create())
        {
            if (existingDynamos.TryGetValue(latest.Key, out var sameKeyDynamo) &&
                !existingBuiltins.ContainsKey(latest.Key))
                continue;

            if (existingBuiltins.TryGetValue(latest.Key, out var current))
            {
                var currentVersionText = current.Properties is not null &&
                    current.Properties.TryGetValue("libraryVersion", out var versionText)
                    ? versionText
                    : null;
                if (Version.TryParse(currentVersionText, out var currentVersion) &&
                    Version.TryParse(BuiltinDynamoLibrary.Version, out var latestVersion) &&
                    currentVersion >= latestVersion)
                    continue;

                workspace.Assets.UpsertDynamo(latest with { Id = current.Id });
                continue;
            }

            // A project that already uses the built-in library receives newly added
            // definitions too, while user-created dynamos remain untouched.
            workspace.Assets.UpsertDynamo(latest);
        }
    }

    private void RemoveImportedE3Dynamos()
    {
        // Legacy project snapshots can still contain the trial imports. Remove
        // only library-owned Elipse E3 definitions; leave native built-ins and
        // user-authored dynamos untouched. The source drawings and converter stay
        // available offline for a future explicit import.
        var importedKeys = workspace.Assets.SnapshotDynamos()
            .Where(dynamo => dynamo.Metadata is not null &&
                (dynamo.Metadata.TryGetValue("importedDynamoLibrary", out var imported) &&
                    string.Equals(imported, "true", StringComparison.OrdinalIgnoreCase) ||
                 dynamo.Metadata.TryGetValue("assetOrigin", out var origin) &&
                    string.Equals(origin, "elipse-e3-import", StringComparison.OrdinalIgnoreCase)))
            .Select(dynamo => dynamo.Key)
            .ToArray();

        foreach (var key in importedKeys)
            workspace.Assets.RemoveDynamo(key);
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
