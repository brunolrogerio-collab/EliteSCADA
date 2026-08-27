namespace Scada.Drivers.Abstractions;

[Flags]
public enum DriverCapabilities
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,

    /// <summary>
    /// The active runtime can acquire values through protocol subscription or
    /// event delivery. This does not imply that subscription objects become
    /// public Engineering entities or that the host calls protocol SDK types.
    /// </summary>
    Subscribe = 1 << 2,

    /// <summary>
    /// Retained for compatibility with early Driver SDK callers. New code
    /// should advertise Engineering browse through DriverEngineeringCapabilities
    /// on ICommunicationDriverEngineeringAdapter instead of putting browse on an
    /// active ICommunicationDriver instance.
    /// </summary>
    Browse = 1 << 3,

    /// <summary>
    /// Retained for compatibility with early Driver SDK callers. New code
    /// should advertise Engineering discovery through DriverEngineeringCapabilities.
    /// </summary>
    Discover = 1 << 4,

    Diagnostics = 1 << 5,

    /// <summary>
    /// Runtime values can preserve a protocol/device-origin timestamp in
    /// addition to the local EliteSCADA observation timestamp.
    /// </summary>
    SourceTimestamp = 1 << 6,

    /// <summary>
    /// Runtime values can preserve a protocol/server timestamp distinct from
    /// both source/device time and local observation time when the protocol
    /// provides one (for example OPC UA).
    /// </summary>
    ServerTimestamp = 1 << 7
}
