using Scada.Engineering.Contracts;
using Scada.Engineering.Libraries;
using Scada.Engineering.ProjectPackages;

namespace Scada.Engineering.ImportExport;

/// <summary>
/// Documents the permanent Engineering exchange authority split consumed by R2.
/// This is intentionally behavior metadata rather than a second schema/wire contract.
/// </summary>
public static class EngineeringExchangeFormatAuthority
{
    public const string ProjectPackageExtension = ProjectPackageService.PackageExtension;
    public const string FragmentExtension = EngineeringFragmentContract.FileExtension;
    public const string ReusableLibraryExtension = ReusableLibraryPackageService.PackageExtension;

    public static EngineeringExchangeFormatProfile ProjectPackage { get; } = new(
        ProjectPackageExtension,
        EngineeringExchangeFormatPurpose.CompleteApplication,
        Structured: true,
        SupportsNestedResources: true,
        MaintainsReusableSourceRelationship: false);

    public static EngineeringExchangeFormatProfile Fragment { get; } = new(
        FragmentExtension,
        EngineeringExchangeFormatPurpose.SelectedStructuredTransfer,
        Structured: true,
        SupportsNestedResources: true,
        MaintainsReusableSourceRelationship: false);

    public static EngineeringExchangeFormatProfile CsvXlsx { get; } = new(
        "csv/xlsx",
        EngineeringExchangeFormatPurpose.FlatTabularBulkExchange,
        Structured: false,
        SupportsNestedResources: false,
        MaintainsReusableSourceRelationship: false);

    public static EngineeringExchangeFormatProfile ReusableLibrary { get; } = new(
        ReusableLibraryExtension,
        EngineeringExchangeFormatPurpose.CuratedReusableDefinitions,
        Structured: true,
        SupportsNestedResources: true,
        MaintainsReusableSourceRelationship: true);
}

public enum EngineeringExchangeFormatPurpose
{
    CompleteApplication,
    SelectedStructuredTransfer,
    FlatTabularBulkExchange,
    CuratedReusableDefinitions
}

public sealed record EngineeringExchangeFormatProfile(
    string Format,
    EngineeringExchangeFormatPurpose Purpose,
    bool Structured,
    bool SupportsNestedResources,
    bool MaintainsReusableSourceRelationship);
