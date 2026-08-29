using Scada.Core.Tags;

namespace Scada.Drivers.Bacnet;

/// <summary>
/// Maps BACnet Reliability values into the EliteSCADA-owned quality model.
/// Numeric values follow the standard Reliability enumeration so this layer
/// remains independent from a concrete BACnet library enum.
/// </summary>
public static class BacnetQualityMapper
{
    public static TagQuality FromReliability(uint? reliability, bool communicationSucceeded = true)
    {
        if (!communicationSucceeded) return TagQuality.BadCommunication;
        if (!reliability.HasValue || reliability.Value == 0) return TagQuality.Good;

        return reliability.Value switch
        {
            2 or 3 => TagQuality.Uncertain,                 // over-range / under-range
            10 => TagQuality.BadConfiguration,             // configuration-error
            11 => TagQuality.BadCommunication,             // communication-failure
            1 or 4 or 5 or 6 or 7 or 8 or 9 or
            12 or 13 or 14 or 15 or 16 => TagQuality.BadDevice,
            _ => TagQuality.Uncertain
        };
    }
}
