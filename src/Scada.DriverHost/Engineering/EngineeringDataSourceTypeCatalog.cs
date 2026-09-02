using System.Globalization;
using Scada.Core.Sources;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Modbus;
using Scada.Drivers.Simulation;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;

namespace Scada.DriverHost.Engineering;

public sealed record EngineeringDriverCapabilityView(
    bool SupportsConnectionTest,
    bool SupportsDiscovery,
    bool SupportsBrowse,
    bool SupportsFileImport,
    bool SupportsReconcile,
    bool SupportsSharedTransportInfrastructure);

public sealed record EngineeringDriverConfigurationFieldView(
    string Key,
    string ValueKind,
    bool Required,
    string DisplayName,
    string? Description,
    string? DefaultValue,
    IReadOnlyCollection<string> AllowedValues,
    double? Minimum,
    double? Maximum,
    bool Advanced,
    string? DisplayNameResourceKey,
    string? DescriptionResourceKey);

public sealed record EngineeringDriverConfigurationSchemaView(
    string SchemaId,
    int SchemaVersion,
    IReadOnlyCollection<EngineeringDriverConfigurationFieldView> DataSourceFields,
    IReadOnlyCollection<EngineeringDriverConfigurationFieldView> TagBindingFields);

public sealed record EngineeringDataSourceTypeView(
    string TypeKey,
    string DisplayName,
    string Kind,
    string? Description,
    EngineeringDriverCapabilityView Capabilities,
    EngineeringDriverConfigurationSchemaView? ConfigurationSchema);

public sealed record EngineeringDataSourceTypeCatalogView(
    IReadOnlyCollection<EngineeringDataSourceTypeView> DataSourceTypes);

/// <summary>
/// Product-host authority for Data Source types available in this build.
/// Modern communication drivers are projected from the runtime component
/// registry itself. Legacy built-ins are added from their canonical descriptors,
/// and memory providers come from the Core source-provider descriptors.
/// </summary>
public sealed class EngineeringDataSourceTypeCatalog : IDataSourceConfigurationValidator
{
    private const string CommunicationDriverKind = "communicationDriver";
    private const string SourceProviderKind = "sourceProvider";

    private readonly IReadOnlyCollection<EngineeringDataSourceTypeDefinition> _definitions;
    private readonly Dictionary<string, EngineeringDataSourceTypeDefinition> _byType;

    private EngineeringDataSourceTypeCatalog(IEnumerable<EngineeringDataSourceTypeDefinition> definitions)
    {
        _definitions = definitions
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.TypeKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _byType = _definitions.ToDictionary(x => x.TypeKey, StringComparer.OrdinalIgnoreCase);
    }

    public static EngineeringDataSourceTypeCatalog BuildForCurrentSchema(
        CommunicationDriverRuntimeComponentRegistry runtimeRegistry)
    {
        ArgumentNullException.ThrowIfNull(runtimeRegistry);

        var definitions = runtimeRegistry.Registrations
            .Select(registration => EngineeringDataSourceTypeDefinition.ForDriver(registration.Descriptor))
            .Concat(new[]
            {
                EngineeringDataSourceTypeDefinition.ForDriver(ModbusTcpDriverDescriptorProvider.SharedDescriptor),
                EngineeringDataSourceTypeDefinition.ForDriver(SimulationDriverDescriptorProvider.SharedDescriptor),
                EngineeringDataSourceTypeDefinition.ForSource(
                    BuiltInSourceProviderDescriptors.ServerMemory,
                    "Server Memory",
                    "Retentive server-owned memory source."),
                EngineeringDataSourceTypeDefinition.ForSource(
                    BuiltInSourceProviderDescriptors.ClientMemory,
                    "Client Memory",
                    "Runtime-client-owned non-retentive memory source.")
            });

        return new EngineeringDataSourceTypeCatalog(definitions);
    }

    public EngineeringDataSourceTypeCatalogView Describe() => new(
        _definitions.Select(ToView).ToArray());

    public IReadOnlyCollection<ImportIssue> Validate(DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<ImportIssue>();
        var typeKey = dataSource.Driver?.Trim() ?? string.Empty;

        if (!_byType.TryGetValue(typeKey, out var definition))
        {
            issues.Add(Error(
                "DATASOURCE_TYPE_UNAVAILABLE",
                $"Data source '{dataSource.Key}' uses type '{dataSource.Driver}', which is not available in this EliteSCADA build. Select one of the source types returned by the Engineering catalog.",
                dataSource.Key));
            return issues;
        }

        if (definition.DriverDescriptor is null)
            return issues;

        var fields = definition.DriverDescriptor.ConfigurationSchema.DataSourceFields;
        var byKey = fields.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var settings = dataSource.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var secretReferences = dataSource.SecretReferences ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in settings.Keys)
        {
            if (!byKey.TryGetValue(key, out var field) || IsProtectedReference(field.ValueKind))
                issues.Add(Error(
                    "DATASOURCE_SETTING_UNKNOWN",
                    $"Setting '{key}' is not valid for source type '{definition.DisplayName}'. Remove it or select the driver it belongs to.",
                    dataSource.Key));
        }

        foreach (var key in secretReferences.Keys)
        {
            if (!byKey.TryGetValue(key, out var field) || !IsProtectedReference(field.ValueKind))
                issues.Add(Error(
                    "DATASOURCE_SECRET_REFERENCE_UNKNOWN",
                    $"Protected-material reference '{key}' is not valid for source type '{definition.DisplayName}'.",
                    dataSource.Key));
        }

        foreach (var field in fields)
        {
            var source = IsProtectedReference(field.ValueKind) ? secretReferences : settings;
            source.TryGetValue(field.Key, out var rawValue);
            var effectiveValue = string.IsNullOrWhiteSpace(rawValue) ? field.DefaultValue : rawValue;

            if (string.IsNullOrWhiteSpace(effectiveValue))
            {
                if (field.Required)
                    issues.Add(Error(
                        "DATASOURCE_SETTING_REQUIRED",
                        $"{field.DisplayName ?? field.Key} is required for source type '{definition.DisplayName}'.",
                        dataSource.Key));
                continue;
            }

            ValidateField(definition, dataSource.Key, field, effectiveValue, issues);
        }

        return issues;
    }

    private static void ValidateField(
        EngineeringDataSourceTypeDefinition definition,
        string dataSourceKey,
        DriverConfigurationFieldDescriptor field,
        string value,
        List<ImportIssue> issues)
    {
        switch (field.ValueKind)
        {
            case DriverConfigurationValueKind.Boolean:
                if (!bool.TryParse(value, out _))
                    AddInvalid("must be true or false");
                break;
            case DriverConfigurationValueKind.Integer:
            case DriverConfigurationValueKind.Port:
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    AddInvalid("must be an integer");
                    break;
                }
                ValidateRange(integer);
                break;
            case DriverConfigurationValueKind.Number:
            case DriverConfigurationValueKind.Duration:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                {
                    AddInvalid("must be a finite number");
                    break;
                }
                ValidateRange(number);
                break;
            case DriverConfigurationValueKind.Enum:
                if (field.AllowedValues is { Count: > 0 } &&
                    !field.AllowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
                    AddInvalid($"must be one of: {string.Join(", ", field.AllowedValues)}");
                break;
        }

        void ValidateRange(double number)
        {
            if (field.Minimum.HasValue && number < field.Minimum.Value)
                AddInvalid($"must be greater than or equal to {field.Minimum.Value.ToString(CultureInfo.InvariantCulture)}");
            else if (field.Maximum.HasValue && number > field.Maximum.Value)
                AddInvalid($"must be less than or equal to {field.Maximum.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        void AddInvalid(string expectation) => issues.Add(Error(
            "DATASOURCE_SETTING_INVALID",
            $"{field.DisplayName ?? field.Key} for source type '{definition.DisplayName}' {expectation}; received '{value}'.",
            dataSourceKey));
    }

    private static EngineeringDataSourceTypeView ToView(EngineeringDataSourceTypeDefinition definition)
    {
        var descriptor = definition.DriverDescriptor;
        var engineeringCapabilities = descriptor?.EngineeringCapabilities ?? DriverEngineeringCapabilities.None;
        return new EngineeringDataSourceTypeView(
            definition.TypeKey,
            definition.DisplayName,
            descriptor is null ? SourceProviderKind : CommunicationDriverKind,
            definition.Description,
            new EngineeringDriverCapabilityView(
                engineeringCapabilities.HasFlag(DriverEngineeringCapabilities.ConnectionTest),
                engineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Discover),
                engineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Browse),
                engineeringCapabilities.HasFlag(DriverEngineeringCapabilities.FileImport),
                engineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Reconcile),
                descriptor?.SupportsSharedTransportInfrastructure ?? false),
            descriptor is null ? null : ToView(descriptor.ConfigurationSchema));
    }

    private static EngineeringDriverConfigurationSchemaView ToView(DriverConfigurationSchemaDescriptor schema) => new(
        schema.SchemaId,
        schema.SchemaVersion,
        schema.DataSourceFields.Select(ToView).ToArray(),
        schema.TagBindingFields.Select(ToView).ToArray());

    private static EngineeringDriverConfigurationFieldView ToView(DriverConfigurationFieldDescriptor field) => new(
        field.Key,
        ToCamelCase(field.ValueKind.ToString()),
        field.Required,
        field.DisplayName ?? field.Key,
        field.Description,
        field.DefaultValue,
        field.AllowedValues?.ToArray() ?? Array.Empty<string>(),
        field.Minimum,
        field.Maximum,
        field.Advanced,
        field.DisplayNameResourceKey,
        field.DescriptionResourceKey);

    private static bool IsProtectedReference(DriverConfigurationValueKind valueKind) =>
        valueKind is DriverConfigurationValueKind.SecretReference or DriverConfigurationValueKind.CertificateReference;

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private static ImportIssue Error(string code, string message, string entityKey) =>
        new(code, message, ImportEntityKind.DataSource, entityKey, true);

    private sealed record EngineeringDataSourceTypeDefinition(
        string TypeKey,
        string DisplayName,
        string? Description,
        CommunicationDriverTypeDescriptor? DriverDescriptor,
        SourceProviderDescriptor? SourceProviderDescriptor)
    {
        public static EngineeringDataSourceTypeDefinition ForDriver(CommunicationDriverTypeDescriptor descriptor) =>
            new(descriptor.DriverType, descriptor.DisplayName, descriptor.Description, descriptor, null);

        public static EngineeringDataSourceTypeDefinition ForSource(
            SourceProviderDescriptor descriptor,
            string displayName,
            string description) =>
            new(descriptor.TypeKey, displayName, description, null, descriptor);
    }
}
