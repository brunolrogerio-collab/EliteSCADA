namespace Scada.Drivers.Tests;

/// <summary>
/// The machine license is host-owned state. Project packages and Authority/System
/// Recovery may consume entitlement state, but may never acquire mutation authority.
/// This source guard makes a new non-lifecycle InstallLicense/RemoveLicense call fail
/// deterministically in the test suite before it can silently reach a machine file.
/// </summary>
public sealed class ProductLicenseMutationBoundaryTests
{
    [Fact]
    public void ProductAndAuthorityOperations_CannotCallMachineLicenseMutation()
    {
        var root = FindRepositoryRoot();
        var sourceRoot = Path.Combine(root, "src");
        var mutationCalls = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => new
            {
                Path = Path.GetRelativePath(root, path).Replace('\\', '/'),
                Text = File.ReadAllText(path)
            })
            .SelectMany(file => FindMutationCalls(file.Text)
                .Select(call => $"{file.Path}:{call}"))
            .OrderBy(call => call, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["src/Scada.Api/Licensing/ProductLicenseLifecycleCoordinator.cs:InstallLicense"],
            mutationCalls);

        AssertNoMutationCall(root, "src/Scada.Engineering/ProjectPackages/ProjectPackageService.cs");
        AssertNoMutationCall(root, "src/Scada.Api/ProjectPackages/SystemRecoveryApplicationService.cs");
        AssertNoMutationCall(root, "src/Scada.Api/Security/AuthorityDetachService.cs");
        AssertNoMutationCall(root, "src/Scada.Api/Persistence/PersistedRuntimeRecoveryService.cs");
    }

    private static IEnumerable<string> FindMutationCalls(string source)
    {
        foreach (var operation in new[] { "InstallLicense", "RemoveLicense" })
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    source,
                    $@"\.\s*{System.Text.RegularExpressions.Regex.Escape(operation)}\s*\("))
                yield return operation;
        }
    }

    private static void AssertNoMutationCall(string root, string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(root, relativePath));
        Assert.Empty(FindMutationCalls(source));
    }

    private static string FindRepositoryRoot()
    {
        foreach (var candidate in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(candidate); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ScadaPlatform.sln")))
                    return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the EliteSCADA repository root.");
    }
}
