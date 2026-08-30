namespace Scada.Drivers.Dnp3;

public sealed record Dnp3ReadinessEvidence(bool IsReady, string Reason);

public static class Dnp3ReadinessPolicy
{
    public static Dnp3ReadinessEvidence Evaluate(Dnp3SessionDiagnosticSnapshot session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Evaluate(
            session.State == Dnp3SessionState.Online,
            session.StartupIntegrityScans > 0);
    }

    public static Dnp3ReadinessEvidence Evaluate(
        bool associationOnline,
        bool startupIntegrityCompleted)
    {
        if (!associationOnline)
            return new(false, "DNP3 association is not online.");

        if (!startupIntegrityCompleted)
            return new(false, "DNP3 startup integrity has not completed.");

        return new(true, "DNP3 association is online and startup integrity completed.");
    }
}
