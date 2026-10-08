using Scada.Engineering.Contracts;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

internal static class BuiltInSecurityRoleDefaults
{
    private static readonly SecurityCapability[] InitialDeveloperCapabilities =
    [
        SecurityCapability.View,
        SecurityCapability.TagRead,
        SecurityCapability.CommandExecute,
        SecurityCapability.ProcessValueWrite,
        SecurityCapability.AlarmAcknowledge,
        SecurityCapability.AlarmShelve,
        SecurityCapability.TrendUse,
        SecurityCapability.TrendSave,
        SecurityCapability.EngineeringView,
        SecurityCapability.EngineeringModify,
        SecurityCapability.UserRoleAdmin,
        SecurityCapability.SystemAdmin,
        SecurityCapability.HighAvailabilityObserve,
        SecurityCapability.HighAvailabilityTransfer,
        SecurityCapability.HighAvailabilityAdmin
    ];

    public static SecurityRoleEngineeringDto CreateInitialDeveloperRole() => new(
        Id: Guid.Parse("46000000-0000-0000-0000-000000000002"),
        Key: "developer",
        Name: "Developer",
        Description: "Full engineering/development role with application and HA administration capabilities.",
        Grants: InitialDeveloperCapabilities
            .Select(capability => new CapabilityGrantEngineeringDto(capability))
            .ToArray());
}
