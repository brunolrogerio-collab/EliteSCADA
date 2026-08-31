namespace Scada.Drivers.Abstractions;

/// <summary>
/// One stable Driver-type registration. Feature providers are optional only when
/// the public descriptor does not advertise that capability. This keeps module
/// composition explicit and prevents central switch statements from becoming a
/// second source of Driver capability truth.
/// </summary>
public sealed record CommunicationDriverModuleRegistration(
    ICommunicationDriverDescriptorProvider DescriptorProvider,
    ICommunicationDriverConnectionTester? ConnectionTester = null,
    ICommunicationDriverDiscoverySource? DiscoverySource = null,
    ICommunicationDriverBrowser? Browser = null,
    ICommunicationDriverFileImporter? FileImporter = null,
    ICommunicationDriverReconciler? Reconciler = null)
{
    public CommunicationDriverTypeDescriptor Descriptor => DescriptorProvider.Descriptor;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(DescriptorProvider);
        var descriptor = DescriptorProvider.Descriptor ?? throw new InvalidOperationException("Driver descriptor provider returned null.");

        if (string.IsNullOrWhiteSpace(descriptor.DriverType))
            throw new InvalidOperationException("DriverType is required for module registration.");
        if (!string.Equals(descriptor.DriverType, descriptor.DriverType.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException($"DriverType '{descriptor.DriverType}' must not contain leading or trailing whitespace.");
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            throw new InvalidOperationException($"Driver '{descriptor.DriverType}' must declare a display name.");
        if (descriptor.DriverContractVersion <= 0)
            throw new InvalidOperationException($"Driver '{descriptor.DriverType}' must declare a positive contract version.");
        if (descriptor.AcquisitionModes is null || descriptor.AcquisitionModes.Count == 0)
            throw new InvalidOperationException($"Driver '{descriptor.DriverType}' must declare at least one acquisition mode.");

        var schema = descriptor.ConfigurationSchema
            ?? throw new InvalidOperationException($"Driver '{descriptor.DriverType}' must declare a configuration schema.");
        if (schema.SchemaVersion <= 0)
            throw new InvalidOperationException($"Driver '{descriptor.DriverType}' must declare a positive configuration schema version.");
        if (string.IsNullOrWhiteSpace(schema.SchemaId))
            throw new InvalidOperationException($"Driver '{descriptor.DriverType}' must declare a configuration schema ID.");
        if (!string.Equals(schema.SchemaId, schema.SchemaId.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException($"Driver '{descriptor.DriverType}' configuration schema ID must not contain leading or trailing whitespace.");
        if (schema.DataSourceFields is null || schema.TagBindingFields is null)
            throw new InvalidOperationException($"Driver '{descriptor.DriverType}' configuration schema field collections must not be null.");

        ValidateProvider(ConnectionTester, descriptor, DriverEngineeringCapabilities.ConnectionTest, nameof(ConnectionTester));
        ValidateProvider(DiscoverySource, descriptor, DriverEngineeringCapabilities.Discover, nameof(DiscoverySource));
        ValidateProvider(Browser, descriptor, DriverEngineeringCapabilities.Browse, nameof(Browser));
        ValidateProvider(FileImporter, descriptor, DriverEngineeringCapabilities.FileImport, nameof(FileImporter));
        ValidateProvider(Reconciler, descriptor, DriverEngineeringCapabilities.Reconcile, nameof(Reconciler));
    }

    private static void ValidateProvider(
        ICommunicationDriverDescriptorProvider? provider,
        CommunicationDriverTypeDescriptor descriptor,
        DriverEngineeringCapabilities capability,
        string providerName)
    {
        var advertised = descriptor.EngineeringCapabilities.HasFlag(capability);
        if (advertised && provider is null)
            throw new InvalidOperationException($"Driver '{descriptor.DriverType}' advertises {capability} but no {providerName} was registered.");
        if (!advertised && provider is not null)
            throw new InvalidOperationException($"Driver '{descriptor.DriverType}' registered {providerName} without advertising {capability}.");
        if (provider is null)
            return;

        var providerDescriptor = provider.Descriptor
            ?? throw new InvalidOperationException($"Driver {providerName} returned a null descriptor.");
        var providerType = providerDescriptor.DriverType;
        if (!string.Equals(providerType, descriptor.DriverType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Driver provider type '{providerType}' does not match registration type '{descriptor.DriverType}'.");
    }
}

/// <summary>
/// In-memory fail-closed registry for stable Driver-type module registrations.
/// DriverType identity is case-insensitive and duplicate registrations are an
/// error; there is deliberately no last-registration-wins behavior.
/// </summary>
public sealed class CommunicationDriverModuleRegistry
{
    private readonly Dictionary<string, CommunicationDriverModuleRegistration> _registrations =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CommunicationDriverModuleRegistration> Registrations =>
        _registrations.Values.OrderBy(x => x.Descriptor.DriverType, StringComparer.OrdinalIgnoreCase).ToArray();

    public void Register(CommunicationDriverModuleRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        registration.Validate();

        var driverType = registration.Descriptor.DriverType;
        if (!_registrations.TryAdd(driverType, registration))
            throw new InvalidOperationException($"Driver type '{driverType}' is already registered.");
    }

    public bool TryGet(string driverType, out CommunicationDriverModuleRegistration? registration)
    {
        if (string.IsNullOrWhiteSpace(driverType))
        {
            registration = null;
            return false;
        }

        return _registrations.TryGetValue(driverType.Trim(), out registration);
    }

    public CommunicationDriverModuleRegistration GetRequired(string driverType)
    {
        if (TryGet(driverType, out var registration) && registration is not null)
            return registration;

        throw new KeyNotFoundException($"Driver type '{driverType}' is not registered.");
    }
}
