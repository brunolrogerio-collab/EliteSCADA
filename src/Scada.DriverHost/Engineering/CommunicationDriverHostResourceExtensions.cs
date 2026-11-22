using Scada.Drivers.HostResources;

namespace Scada.DriverHost.Engineering;

public static class CommunicationDriverHostResourceExtensions
{
    public static HostResourceLeaseCoordinator.HostResourceLease AcquireHostResource(
        this CommunicationDriverRuntimeServices services,
        HostResourceLeaseCoordinator coordinator,
        HostResourceId resourceId,
        string ownerId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(coordinator);
        services.Validate();
        return coordinator.Acquire(resourceId, ownerId, () => services.CanOwnExternalEffects);
    }
}
