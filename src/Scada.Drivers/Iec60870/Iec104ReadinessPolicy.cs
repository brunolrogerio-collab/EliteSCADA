namespace Scada.Drivers.Iec60870;

public sealed record Iec104ReadinessEvidence(bool IsReady, string Reason);

/// <summary>
/// Pure IEC 60870-5-104 readiness rule. A source is ready only after data
/// transfer is running and the configured startup General Interrogation policy
/// has completed. Point quality and late-event ordering are intentionally not
/// part of source activation.
/// </summary>
public static class Iec104ReadinessPolicy
{
    public static Iec104ReadinessEvidence Evaluate(
        Iec104SessionState sessionState,
        IReadOnlyDictionary<ushort, Iec104GeneralInterrogationState> generalInterrogations,
        bool generalInterrogationRequired)
    {
        ArgumentNullException.ThrowIfNull(generalInterrogations);

        if (sessionState != Iec104SessionState.Running)
            return new(false, $"IEC-104 data transfer is not running ({sessionState}).");

        if (!generalInterrogationRequired)
            return new(true, "IEC-104 STARTDT is active and startup General Interrogation is disabled by configuration.");

        if (generalInterrogations.Count == 0)
            return new(false, "IEC-104 startup General Interrogation has not been created for any configured Common Address.");

        if (generalInterrogations.Values.Any(state => state == Iec104GeneralInterrogationState.Rejected))
            return new(false, "IEC-104 startup General Interrogation was rejected.");

        if (generalInterrogations.Values.Any(state => state != Iec104GeneralInterrogationState.Completed))
            return new(false, "IEC-104 startup General Interrogation is still in progress.");

        return new(true, "IEC-104 STARTDT is active and startup General Interrogation completed.");
    }
}
