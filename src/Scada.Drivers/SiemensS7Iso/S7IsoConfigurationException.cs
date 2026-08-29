namespace Scada.Drivers.SiemensS7Iso;

/// <summary>
/// Represents a local S7 binding/request constraint that is known before a
/// protocol request is sent. It is deliberately not an IOException so callers
/// do not misclassify an Engineering incompatibility as a transport failure.
/// </summary>
internal sealed class S7IsoConfigurationException : InvalidOperationException
{
    public S7IsoConfigurationException(string message)
        : base(message)
    {
    }
}
