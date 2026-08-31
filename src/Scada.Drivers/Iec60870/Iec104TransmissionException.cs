namespace Scada.Drivers.Iec60870;

/// <summary>
/// Raised when an IEC-104 APDU write fails after the transport has committed a sequence number.
/// At that point some or all bytes may already have reached the controlled station, so callers
/// must not infer that an operational command did not execute.
/// </summary>
public sealed class Iec104AmbiguousTransmissionException : IOException
{
    public Iec104AmbiguousTransmissionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
