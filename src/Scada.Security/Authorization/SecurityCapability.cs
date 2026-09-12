namespace Scada.Security.Authorization;

public enum SecurityCapability
{
    View = 0,
    TagRead = 1,
    CommandExecute = 2,
    ProcessValueWrite = 3,
    AlarmAcknowledge = 4,
    AlarmShelve = 5,
    TrendUse = 6,
    TrendSave = 7,
    EngineeringModify = 8,
    UserRoleAdmin = 9,
    SystemAdmin = 10,
    EngineeringView = 11,
    HighAvailabilityObserve = 12,
    HighAvailabilityTransfer = 13,
    HighAvailabilityAdmin = 14
}
