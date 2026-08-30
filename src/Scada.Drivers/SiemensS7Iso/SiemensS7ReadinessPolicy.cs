namespace Scada.Drivers.SiemensS7Iso;

public sealed record SiemensS7ReadinessEvidence(bool IsReady, string Reason);

/// <summary>
/// Pure S7 source-activation rule. Individual point decode/device failures are
/// deliberately excluded: they affect point quality, not whether the negotiated
/// ISO/S7 session is operational.
/// </summary>
public static class SiemensS7ReadinessPolicy
{
    public static SiemensS7ReadinessEvidence Evaluate(
        bool sessionEstablished,
        int negotiatedPduSize,
        bool initialAcquisitionAttempted)
    {
        if (!sessionEstablished)
            return new(false, "ISO-on-TCP/S7 session is not established.");

        if (negotiatedPduSize <= 0)
            return new(false, "S7 PDU negotiation has not completed with a valid size.");

        if (!initialAcquisitionAttempted)
            return new(false, "No bounded S7 acquisition attempt has executed yet.");

        return new(true, "ISO/S7 session and PDU are negotiated and acquisition has executed.");
    }
}
