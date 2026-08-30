namespace Scada.Drivers.Iec60870;

public enum Iec104GeneralInterrogationState
{
    AwaitingActivationConfirmation,
    Collecting,
    Completed,
    Rejected
}

public sealed class Iec104GeneralInterrogationTransaction
{
    public const byte ActivationCause = 6;
    public const byte ActivationConfirmationCause = 7;
    public const byte ActivationTerminationCause = 10;
    public const byte GlobalQoi = 20;

    public Iec104GeneralInterrogationTransaction(
        ushort commonAddress,
        byte originatorAddress = 0,
        byte qualifierOfInterrogation = GlobalQoi)
    {
        CommonAddress = commonAddress;
        OriginatorAddress = originatorAddress;
        QualifierOfInterrogation = qualifierOfInterrogation;
        State = Iec104GeneralInterrogationState.AwaitingActivationConfirmation;
    }

    public ushort CommonAddress { get; }
    public byte OriginatorAddress { get; }
    public byte QualifierOfInterrogation { get; }
    public Iec104GeneralInterrogationState State { get; private set; }

    public Iec104AsduEnvelope CreateActivation()
    {
        if (State is Iec104GeneralInterrogationState.Completed or Iec104GeneralInterrogationState.Rejected)
            throw new InvalidOperationException("Completed or rejected IEC-104 General Interrogation transactions cannot be reactivated.");

        Span<byte> payload = stackalloc byte[4];
        new Iec104InformationObjectAddress(0).WriteTo(payload.Slice(0, 3));
        payload[3] = QualifierOfInterrogation;

        var header = new Iec104AsduHeader(
            Iec104TypeId.CIcNa1,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(ActivationCause, OriginatorAddress),
            CommonAddress);

        return Iec104AsduEnvelope.Create(header, payload);
    }

    public bool ObserveControlResponse(Iec104AsduEnvelope asdu)
    {
        ArgumentNullException.ThrowIfNull(asdu);

        if (asdu.Header.TypeId != Iec104TypeId.CIcNa1 ||
            asdu.Header.CommonAddress != CommonAddress ||
            asdu.Header.CauseOfTransmission.OriginatorAddress != OriginatorAddress ||
            asdu.Header.CauseOfTransmission.IsTest)
        {
            return false;
        }

        ValidatePayload(asdu);
        var cause = asdu.Header.CauseOfTransmission;

        if (cause.CauseCode == ActivationConfirmationCause)
        {
            if (State != Iec104GeneralInterrogationState.AwaitingActivationConfirmation)
                throw new Iec104ProtocolException($"Unexpected IEC-104 General Interrogation activation confirmation while transaction is {State}.");

            State = cause.IsNegativeConfirmation
                ? Iec104GeneralInterrogationState.Rejected
                : Iec104GeneralInterrogationState.Collecting;
            return true;
        }

        if (cause.CauseCode == ActivationTerminationCause)
        {
            if (cause.IsNegativeConfirmation)
            {
                State = Iec104GeneralInterrogationState.Rejected;
                return true;
            }

            if (State != Iec104GeneralInterrogationState.Collecting)
                throw new Iec104ProtocolException("IEC-104 General Interrogation activation termination arrived before a positive activation confirmation.");

            State = Iec104GeneralInterrogationState.Completed;
            return true;
        }

        return false;
    }

    private void ValidatePayload(Iec104AsduEnvelope asdu)
    {
        if (asdu.Header.ObjectCount != 1 || asdu.Header.IsSequence)
            throw new Iec104ProtocolException("IEC-104 General Interrogation control response must contain exactly one non-sequential information object.");
        if (asdu.Payload.Length != 4)
            throw new Iec104ProtocolException("IEC-104 General Interrogation control response payload must contain IOA 0 and QOI.");

        var payload = asdu.Payload.Span;
        var ioa = Iec104InformationObjectAddress.Parse(payload.Slice(0, 3));
        if (ioa.Value != 0)
            throw new Iec104ProtocolException("IEC-104 General Interrogation control response must use Information Object Address 0.");
        if (payload[3] != QualifierOfInterrogation)
            throw new Iec104ProtocolException($"IEC-104 General Interrogation QOI {payload[3]} does not match requested QOI {QualifierOfInterrogation}.");
    }
}
