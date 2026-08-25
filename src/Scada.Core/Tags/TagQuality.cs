namespace Scada.Core.Tags;

public enum TagQuality
{
    Good,
    Uncertain,
    Bad,
    BadCommunication,
    BadConfiguration,
    BadDevice,
    Stale,
    Disabled
}
