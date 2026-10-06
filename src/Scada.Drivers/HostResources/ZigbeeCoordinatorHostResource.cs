namespace Scada.Drivers.HostResources;

public static class ZigbeeCoordinatorHostResource
{
    public const string NetworkIdentityMetadataKey = "zigbee.networkIdentity";

    public static HostResourceDescriptor Create(
        string resourceId,
        HostResourceLocator locator,
        HostResourcePhysicalIdentity physicalIdentity,
        string adapterFamily,
        string model,
        string firmware,
        string networkIdentity,
        HostResourceAvailability availability = HostResourceAvailability.Available,
        IReadOnlyDictionary<string, string>? metadata = null,
        HostResourceReplacementMetadata? replacement = null)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(physicalIdentity);

        locator.Validate();
        if (locator.Kind is not HostResourceLocatorKind.SerialPort and
            not HostResourceLocatorKind.TcpEndpoint)
        {
            throw new ArgumentException(
                "Zigbee coordinator locator must be serial or TCP.",
                nameof(locator));
        }

        if (string.IsNullOrWhiteSpace(adapterFamily))
            throw new ArgumentException("Zigbee adapter family is required.", nameof(adapterFamily));
        if (string.IsNullOrWhiteSpace(networkIdentity))
            throw new ArgumentException("Zigbee network identity evidence is required.", nameof(networkIdentity));

        var details = metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        details[NetworkIdentityMetadataKey] = networkIdentity.Trim();

        var descriptor = new HostResourceDescriptor(
            new HostResourceId(resourceId),
            HostResourceKinds.ZigbeeCoordinator,
            locator,
            physicalIdentity,
            availability,
            adapterFamily,
            model,
            firmware,
            details,
            replacement);
        descriptor.Validate();
        return descriptor;
    }
}
