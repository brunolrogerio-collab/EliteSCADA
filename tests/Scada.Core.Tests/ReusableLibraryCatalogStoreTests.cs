using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Libraries;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class ReusableLibraryCatalogStoreTests
{
    private const string ProjectA = "project:a";
    private const string ProjectB = "project:b";

    [Fact]
    public void AssociateBrowseAndDisassociate_DoNotMutateCanonicalResourceRegistries()
    {
        var changes = 0;
        var assets = new InMemoryEngineeringAssetRegistry(() => changes++);
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry(() => changes++);
        var templateId = Guid.NewGuid();
        assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            templateId,
            "catalog.template",
            "Catalog Template"));
        var baselineChanges = changes;
        var service = new ReusableLibraryPackageService(assets, visualAssets);
        var libraryId = Guid.NewGuid();
        var bytes = service.Export(new ReusableLibraryExportRequest(
            libraryId,
            "Catalog Library",
            "1.0.0",
            [new(ReusableLibraryResourceKinds.EquipmentTemplate, templateId)]));
        var inspection = service.Inspect(bytes);
        var catalog = new ReusableLibraryCatalogStore();

        var associated = catalog.Associate(ProjectA, bytes, inspection);
        var entry = catalog.Find(ProjectA, libraryId);
        var removed = catalog.Disassociate(ProjectA, libraryId);

        Assert.True(associated.Added);
        Assert.NotNull(entry);
        Assert.Equal(libraryId, entry!.LibraryId);
        Assert.Single(entry.Inspection.Manifest.Resources);
        Assert.True(removed);
        Assert.Empty(catalog.Snapshot(ProjectA));
        Assert.Equal(baselineChanges, changes);
        Assert.NotNull(assets.FindTemplate(templateId));
        Assert.Empty(visualAssets.SnapshotAssets());
    }

    [Fact]
    public void AssociateSameExactLibrary_IsIdempotentWithinProjectScope()
    {
        var (service, bytes, inspection) = CreateTemplateLibrary(Guid.NewGuid(), "1.0.0", "same.template");
        _ = service;
        var catalog = new ReusableLibraryCatalogStore();

        var first = catalog.Associate(ProjectA, bytes, inspection);
        var second = catalog.Associate(ProjectA, bytes, inspection);

        Assert.True(first.Added);
        Assert.False(second.Added);
        Assert.Equal(first.Library.ContentSha256, second.Library.ContentSha256);
        Assert.Single(catalog.Snapshot(ProjectA));
    }

    [Fact]
    public void SameLibraryCanBeAssociatedIndependentlyAcrossProjectScopes()
    {
        var libraryId = Guid.NewGuid();
        var (_, bytes, inspection) = CreateTemplateLibrary(libraryId, "1.0.0", "isolated.template");
        var catalog = new ReusableLibraryCatalogStore();

        var first = catalog.Associate(ProjectA, bytes, inspection);

        Assert.True(first.Added);
        Assert.NotNull(catalog.Find(ProjectA, libraryId));
        Assert.Null(catalog.Find(ProjectB, libraryId));
        Assert.Empty(catalog.Snapshot(ProjectB));

        var second = catalog.Associate(ProjectB, bytes, inspection);
        Assert.True(second.Added);
        Assert.Single(catalog.Snapshot(ProjectA));
        Assert.Single(catalog.Snapshot(ProjectB));

        Assert.True(catalog.Disassociate(ProjectA, libraryId));
        Assert.Null(catalog.Find(ProjectA, libraryId));
        Assert.NotNull(catalog.Find(ProjectB, libraryId));
    }

    [Fact]
    public void AssociateSameLibraryIdWithDifferentContent_FailsWithoutReplacingExistingProjectCatalog()
    {
        var libraryId = Guid.NewGuid();
        var (_, firstBytes, firstInspection) = CreateTemplateLibrary(libraryId, "1.0.0", "first.template");
        var (_, secondBytes, secondInspection) = CreateTemplateLibrary(libraryId, "2.0.0", "second.template");
        var catalog = new ReusableLibraryCatalogStore();
        var first = catalog.Associate(ProjectA, firstBytes, firstInspection);

        var exception = Assert.Throws<ReusableLibraryAssociationConflictException>(() =>
            catalog.Associate(ProjectA, secondBytes, secondInspection));

        Assert.Equal(libraryId, exception.LibraryId);
        var retained = Assert.Single(catalog.Snapshot(ProjectA));
        Assert.Equal("1.0.0", retained.Version);
        Assert.Equal(first.Library.ContentSha256, retained.ContentSha256);
        Assert.Empty(catalog.Snapshot(ProjectB));
    }

    [Fact]
    public void CatalogDescriptor_ContainsNoSourcePathOrRuntimeAuthority()
    {
        var libraryId = Guid.NewGuid();
        var (_, bytes, inspection) = CreateTemplateLibrary(libraryId, "1.0.0", "portable.template");
        var catalog = new ReusableLibraryCatalogStore();

        catalog.Associate(ProjectA, bytes, inspection);
        var descriptor = Assert.Single(catalog.Snapshot(ProjectA));

        Assert.Equal(libraryId, descriptor.LibraryId);
        Assert.Equal(64, descriptor.ContentSha256.Length);
        Assert.DoesNotContain("path", string.Join(',', descriptor.GetType().GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime", string.Join(',', descriptor.GetType().GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScopeIsRequiredAndCannotBeBlank()
    {
        var libraryId = Guid.NewGuid();
        var (_, bytes, inspection) = CreateTemplateLibrary(libraryId, "1.0.0", "scope.template");
        var catalog = new ReusableLibraryCatalogStore();

        Assert.Throws<ArgumentException>(() => catalog.Associate(" ", bytes, inspection));
        Assert.Throws<ArgumentException>(() => catalog.Snapshot(""));
        Assert.Throws<ArgumentException>(() => catalog.Find(" ", libraryId));
        Assert.Throws<ArgumentException>(() => catalog.Disassociate("", libraryId));
    }

    private static (ReusableLibraryPackageService Service, byte[] Bytes, ReusableLibraryInspection Inspection) CreateTemplateLibrary(
        Guid libraryId,
        string version,
        string key)
    {
        var assets = new InMemoryEngineeringAssetRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var templateId = Guid.NewGuid();
        assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(
            templateId,
            key,
            key));
        var service = new ReusableLibraryPackageService(assets, visualAssets);
        var bytes = service.Export(new ReusableLibraryExportRequest(
            libraryId,
            "Catalog Library",
            version,
            [new(ReusableLibraryResourceKinds.EquipmentTemplate, templateId)]));
        return (service, bytes, service.Inspect(bytes));
    }
}
