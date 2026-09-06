using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Libraries;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class ReusableLibraryCatalogStoreTests
{
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

        var associated = catalog.Associate(bytes, inspection);
        var entry = catalog.Find(libraryId);
        var removed = catalog.Disassociate(libraryId);

        Assert.True(associated.Added);
        Assert.NotNull(entry);
        Assert.Equal(libraryId, entry!.LibraryId);
        Assert.Single(entry.Inspection.Manifest.Resources);
        Assert.True(removed);
        Assert.Empty(catalog.Snapshot());
        Assert.Equal(baselineChanges, changes);
        Assert.NotNull(assets.FindTemplate(templateId));
        Assert.Empty(visualAssets.SnapshotAssets());
    }

    [Fact]
    public void AssociateSameExactLibrary_IsIdempotent()
    {
        var (service, bytes, inspection) = CreateTemplateLibrary(Guid.NewGuid(), "1.0.0", "same.template");
        _ = service;
        var catalog = new ReusableLibraryCatalogStore();

        var first = catalog.Associate(bytes, inspection);
        var second = catalog.Associate(bytes, inspection);

        Assert.True(first.Added);
        Assert.False(second.Added);
        Assert.Equal(first.Library.ContentSha256, second.Library.ContentSha256);
        Assert.Single(catalog.Snapshot());
    }

    [Fact]
    public void AssociateSameLibraryIdWithDifferentContent_FailsWithoutReplacingExistingCatalog()
    {
        var libraryId = Guid.NewGuid();
        var (_, firstBytes, firstInspection) = CreateTemplateLibrary(libraryId, "1.0.0", "first.template");
        var (_, secondBytes, secondInspection) = CreateTemplateLibrary(libraryId, "2.0.0", "second.template");
        var catalog = new ReusableLibraryCatalogStore();
        var first = catalog.Associate(firstBytes, firstInspection);

        var exception = Assert.Throws<ReusableLibraryAssociationConflictException>(() =>
            catalog.Associate(secondBytes, secondInspection));

        Assert.Equal(libraryId, exception.LibraryId);
        var retained = Assert.Single(catalog.Snapshot());
        Assert.Equal("1.0.0", retained.Version);
        Assert.Equal(first.Library.ContentSha256, retained.ContentSha256);
    }

    [Fact]
    public void CatalogDescriptor_ContainsNoSourcePathOrRuntimeAuthority()
    {
        var libraryId = Guid.NewGuid();
        var (_, bytes, inspection) = CreateTemplateLibrary(libraryId, "1.0.0", "portable.template");
        var catalog = new ReusableLibraryCatalogStore();

        catalog.Associate(bytes, inspection);
        var descriptor = Assert.Single(catalog.Snapshot());

        Assert.Equal(libraryId, descriptor.LibraryId);
        Assert.Equal(64, descriptor.ContentSha256.Length);
        Assert.DoesNotContain("path", string.Join(',', descriptor.GetType().GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime", string.Join(',', descriptor.GetType().GetProperties().Select(x => x.Name)), StringComparison.OrdinalIgnoreCase);
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
