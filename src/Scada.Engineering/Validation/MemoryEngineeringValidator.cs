using Scada.Core.Sources;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Engineering.Validation;

internal static class MemoryEngineeringValidator
{
    public static IReadOnlyCollection<ImportIssue> ValidateDataSource(DataSourceEngineeringDto dataSource)
    {
        var issues = new List<ImportIssue>();
        if (!IsMemoryDriver(dataSource.Driver))
            return issues;

        if (dataSource.Settings is { Count: > 0 })
        {
            issues.Add(Error(
                "MEMORY_DATASOURCE_SETTINGS_NOT_ALLOWED",
                $"Internal Memory data source '{dataSource.Key}' does not use transport/network settings.",
                ImportEntityKind.DataSource,
                dataSource.Key));
        }

        if (dataSource.SecretReferences is { Count: > 0 })
        {
            issues.Add(Error(
                "MEMORY_DATASOURCE_SECRETS_NOT_ALLOWED",
                $"Internal Memory data source '{dataSource.Key}' does not use network credentials or secret references.",
                ImportEntityKind.DataSource,
                dataSource.Key));
        }

        return issues;
    }

    public static IReadOnlyCollection<ImportIssue> ValidateTag(
        TagEngineeringDto tag,
        DataSourceEngineeringDto? dataSource)
    {
        var issues = new List<ImportIssue>();
        var isMemory = dataSource is not null && IsMemoryDriver(dataSource.Driver);
        var isClientMemory = dataSource is not null && IsClientMemoryDriver(dataSource.Driver);

        if (tag.Metadata?.Keys.Any(key => key.StartsWith(
                MemoryEngineeringValueCodec.ReservedMetadataPrefix,
                StringComparison.OrdinalIgnoreCase)) == true)
        {
            issues.Add(Error(
                "MEMORY_RESERVED_METADATA_NOT_ALLOWED",
                $"TAG '{tag.Path}' uses reserved Internal Memory metadata keys. Use initialValue instead.",
                ImportEntityKind.Tag,
                tag.Path));
        }

        if (!isMemory)
        {
            if (tag.InitialValue is not null)
            {
                issues.Add(Error(
                    "MEMORY_INITIAL_VALUE_SOURCE_REQUIRED",
                    $"TAG '{tag.Path}' defines initialValue but is not linked to an Internal Memory data source.",
                    ImportEntityKind.Tag,
                    tag.Path));
            }
            return issues;
        }

        if (!string.IsNullOrWhiteSpace(tag.Address))
        {
            issues.Add(Error(
                "MEMORY_TAG_ADDRESS_NOT_ALLOWED",
                $"Internal Memory TAG '{tag.Path}' does not use a network/device address.",
                ImportEntityKind.Tag,
                tag.Path));
        }

        if (isClientMemory && tag.Historian?.Enabled == true)
        {
            issues.Add(Error(
                "CLIENT_MEMORY_HISTORIAN_NOT_ALLOWED",
                $"Client Memory TAG '{tag.Path}' cannot be configured as a global server historian source.",
                ImportEntityKind.Tag,
                tag.Path));
        }

        if (tag.InitialValue is null)
            return issues;

        if (tag.InitialValue.DataType != tag.DataType)
        {
            issues.Add(Error(
                "MEMORY_INITIAL_VALUE_TYPE_MISMATCH",
                $"Internal Memory TAG '{tag.Path}' initial value declares {tag.InitialValue.DataType} but TAG type is {tag.DataType}.",
                ImportEntityKind.Tag,
                tag.Path));
            return issues;
        }

        try
        {
            _ = MemoryEngineeringValueCodec.ToTypedValue(tag.InitialValue);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            issues.Add(Error(
                "MEMORY_INITIAL_VALUE_INVALID",
                $"Internal Memory TAG '{tag.Path}' initial value is invalid for {tag.DataType}.",
                ImportEntityKind.Tag,
                tag.Path));
        }

        return issues;
    }

    public static bool IsClientMemoryDriver(string? driver) =>
        string.Equals(driver, BuiltInSourceProviderDescriptors.ClientMemory.TypeKey, StringComparison.OrdinalIgnoreCase);

    public static bool IsMemoryDriver(string? driver) =>
        IsClientMemoryDriver(driver) ||
        string.Equals(driver, BuiltInSourceProviderDescriptors.ServerMemory.TypeKey, StringComparison.OrdinalIgnoreCase);

    private static ImportIssue Error(string code, string message, ImportEntityKind kind, string key) =>
        new(code, message, kind, key, true);
}
