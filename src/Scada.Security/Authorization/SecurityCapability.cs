namespace Scada.Security.Authorization;

public enum SecurityCapability
{
    View,
    TagRead,
    CommandExecute,
    ProcessValueWrite,
    AlarmAcknowledge,
    AlarmShelve,
    TrendUse,
    TrendSave,
    EngineeringModify,
    UserRoleAdmin,
    SystemAdmin
}
