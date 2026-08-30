using System.Buffers.Binary;

namespace Scada.Drivers.Iec60870;

public enum Iec104CommandMode
{
    DirectOperate,
    SelectBeforeOperate
}

public enum Iec104CommandState
{
    Created,
    AwaitingSelectionConfirmation,
    Selected,
    AwaitingExecutionConfirmation,
    Accepted,
    Completed,
    Rejected
}

public sealed class Iec104CommandTransaction
{
    public const byte ActivationCause = 6;
    public const byte ActivationConfirmationCause = 7;
    public const byte ActivationTerminationCause = 10;

    private readonly byte[] _selectPayload;
    private readonly byte[] _executePayload;

    private Iec104CommandTransaction(
        Iec104TypeId typeId,
        ushort commonAddress,
        Iec104InformationObjectAddress informationObjectAddress,
        Iec104CommandMode mode,
        byte originatorAddress,
        byte[] selectPayload,
        byte[] executePayload)
    {
        TypeId = typeId;
        CommonAddress = commonAddress;
        InformationObjectAddress = informationObjectAddress;
        Mode = mode;
        OriginatorAddress = originatorAddress;
        _selectPayload = selectPayload;
        _executePayload = executePayload;
    }

    public Iec104TypeId TypeId { get; }
    public ushort CommonAddress { get; }
    public Iec104InformationObjectAddress InformationObjectAddress { get; }
    public Iec104CommandMode Mode { get; }
    public byte OriginatorAddress { get; }
    public Iec104CommandState State { get; private set; } = Iec104CommandState.Created;

    public static Iec104CommandTransaction Single(
        ushort commonAddress,
        int informationObjectAddress,
        bool value,
        Iec104CommandMode mode,
        byte qualifier = 0,
        byte originatorAddress = 0)
    {
        ValidateQu(qualifier);
        var ioa = new Iec104InformationObjectAddress(informationObjectAddress);
        var execute = CreatePayload(ioa, 1);
        execute[3] = (byte)((qualifier << 2) | (value ? 0x01 : 0x00));
        var select = (byte[])execute.Clone();
        select[3] |= 0x80;
        return new Iec104CommandTransaction(Iec104TypeId.CScNa1, commonAddress, ioa, mode, originatorAddress, select, execute);
    }

    public static Iec104CommandTransaction Double(
        ushort commonAddress,
        int informationObjectAddress,
        Iec104DoublePointState value,
        Iec104CommandMode mode,
        byte qualifier = 0,
        byte originatorAddress = 0)
    {
        ValidateQu(qualifier);
        if (value is not (Iec104DoublePointState.Off or Iec104DoublePointState.On))
            throw new ArgumentOutOfRangeException(nameof(value), value, "IEC-104 double commands may execute only Off or On states.");

        var ioa = new Iec104InformationObjectAddress(informationObjectAddress);
        var execute = CreatePayload(ioa, 1);
        execute[3] = (byte)((qualifier << 2) | (byte)value);
        var select = (byte[])execute.Clone();
        select[3] |= 0x80;
        return new Iec104CommandTransaction(Iec104TypeId.CDcNa1, commonAddress, ioa, mode, originatorAddress, select, execute);
    }

    public static Iec104CommandTransaction NormalizedSetpoint(
        ushort commonAddress,
        int informationObjectAddress,
        float value,
        Iec104CommandMode mode,
        byte qualifier = 0,
        byte originatorAddress = 0)
    {
        ValidateQl(qualifier);
        if (!float.IsFinite(value) || value is < -1f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(value), value, "IEC-104 normalized setpoint must be finite and in the range -1.0..1.0.");

        var raw = value <= -1f
            ? short.MinValue
            : value >= 1f
                ? short.MaxValue
                : checked((short)((value * 32767.5f) - 0.5f));
        return CreateSetpoint(Iec104TypeId.CSeNa1, commonAddress, informationObjectAddress, mode, qualifier, originatorAddress, raw);
    }

    public static Iec104CommandTransaction ScaledSetpoint(
        ushort commonAddress,
        int informationObjectAddress,
        short value,
        Iec104CommandMode mode,
        byte qualifier = 0,
        byte originatorAddress = 0) =>
        CreateSetpoint(Iec104TypeId.CSeNb1, commonAddress, informationObjectAddress, mode, qualifier, originatorAddress, value);

    public static Iec104CommandTransaction ShortFloatSetpoint(
        ushort commonAddress,
        int informationObjectAddress,
        float value,
        Iec104CommandMode mode,
        byte qualifier = 0,
        byte originatorAddress = 0)
    {
        ValidateQl(qualifier);
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "IEC-104 short-float setpoint must be finite.");

        var ioa = new Iec104InformationObjectAddress(informationObjectAddress);
        var execute = CreatePayload(ioa, 5);
        BinaryPrimitives.WriteInt32LittleEndian(execute.AsSpan(3, 4), BitConverter.SingleToInt32Bits(value));
        execute[7] = qualifier;
        var select = (byte[])execute.Clone();
        select[7] |= 0x80;
        return new Iec104CommandTransaction(Iec104TypeId.CSeNc1, commonAddress, ioa, mode, originatorAddress, select, execute);
    }

    public Iec104AsduEnvelope CreateInitialRequest()
    {
        if (State != Iec104CommandState.Created)
            throw new InvalidOperationException($"IEC-104 command initial request cannot be created while transaction is {State}.");

        if (Mode == Iec104CommandMode.SelectBeforeOperate)
        {
            State = Iec104CommandState.AwaitingSelectionConfirmation;
            return CreateActivation(_selectPayload);
        }

        State = Iec104CommandState.AwaitingExecutionConfirmation;
        return CreateActivation(_executePayload);
    }

    public Iec104AsduEnvelope CreateExecuteAfterSelection()
    {
        if (Mode != Iec104CommandMode.SelectBeforeOperate)
            throw new InvalidOperationException("Direct-operate IEC-104 commands do not have a separate execute-after-selection step.");
        if (State != Iec104CommandState.Selected)
            throw new InvalidOperationException($"IEC-104 execute request requires Selected state; current state is {State}.");

        State = Iec104CommandState.AwaitingExecutionConfirmation;
        return CreateActivation(_executePayload);
    }

    public bool ObserveResponse(Iec104AsduEnvelope asdu)
    {
        ArgumentNullException.ThrowIfNull(asdu);
        if (asdu.Header.TypeId != TypeId ||
            asdu.Header.CommonAddress != CommonAddress ||
            asdu.Header.CauseOfTransmission.OriginatorAddress != OriginatorAddress ||
            asdu.Header.CauseOfTransmission.IsTest)
        {
            return false;
        }

        if (asdu.Header.ObjectCount != 1 || asdu.Header.IsSequence)
            throw new Iec104ProtocolException("IEC-104 command response must contain exactly one non-sequential information object.");
        if (asdu.Payload.Length < 3)
            throw new Iec104ProtocolException("IEC-104 command response is missing its Information Object Address.");

        var responseIoa = Iec104InformationObjectAddress.Parse(asdu.Payload.Span.Slice(0, 3));
        if (responseIoa != InformationObjectAddress)
            return false;

        var cause = asdu.Header.CauseOfTransmission;
        if (cause.CauseCode == ActivationConfirmationCause)
        {
            if (cause.IsNegativeConfirmation)
            {
                State = Iec104CommandState.Rejected;
                return true;
            }

            if (State == Iec104CommandState.AwaitingSelectionConfirmation)
            {
                ValidateEcho(asdu, _selectPayload);
                State = Iec104CommandState.Selected;
                return true;
            }

            if (State == Iec104CommandState.AwaitingExecutionConfirmation)
            {
                ValidateEcho(asdu, _executePayload);
                State = Iec104CommandState.Accepted;
                return true;
            }

            throw new Iec104ProtocolException($"Unexpected IEC-104 command activation confirmation while transaction is {State}.");
        }

        if (cause.CauseCode == ActivationTerminationCause)
        {
            if (cause.IsNegativeConfirmation)
            {
                State = Iec104CommandState.Rejected;
                return true;
            }
            if (State != Iec104CommandState.Accepted)
                throw new Iec104ProtocolException("IEC-104 command activation termination arrived before a positive execute confirmation.");

            ValidateEcho(asdu, _executePayload);
            State = Iec104CommandState.Completed;
            return true;
        }

        return false;
    }

    private Iec104AsduEnvelope CreateActivation(byte[] payload)
    {
        var header = new Iec104AsduHeader(
            TypeId,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(ActivationCause, OriginatorAddress),
            CommonAddress);
        return Iec104AsduEnvelope.Create(header, payload);
    }

    private static Iec104CommandTransaction CreateSetpoint(
        Iec104TypeId typeId,
        ushort commonAddress,
        int informationObjectAddress,
        Iec104CommandMode mode,
        byte qualifier,
        byte originatorAddress,
        short value)
    {
        ValidateQl(qualifier);
        var ioa = new Iec104InformationObjectAddress(informationObjectAddress);
        var execute = CreatePayload(ioa, 3);
        BinaryPrimitives.WriteInt16LittleEndian(execute.AsSpan(3, 2), value);
        execute[5] = qualifier;
        var select = (byte[])execute.Clone();
        select[5] |= 0x80;
        return new Iec104CommandTransaction(typeId, commonAddress, ioa, mode, originatorAddress, select, execute);
    }

    private static byte[] CreatePayload(Iec104InformationObjectAddress ioa, int dataLength)
    {
        var payload = new byte[3 + dataLength];
        ioa.WriteTo(payload.AsSpan(0, 3));
        return payload;
    }

    private static void ValidateEcho(Iec104AsduEnvelope asdu, byte[] expectedPayload)
    {
        if (!asdu.Payload.Span.SequenceEqual(expectedPayload))
            throw new Iec104ProtocolException("IEC-104 command confirmation payload does not echo the pending command identity/value/qualifier.");
    }

    private static void ValidateQu(byte qualifier)
    {
        if (qualifier > 31)
            throw new ArgumentOutOfRangeException(nameof(qualifier), qualifier, "IEC-104 single/double command qualifier QU must be in the range 0..31.");
    }

    private static void ValidateQl(byte qualifier)
    {
        if (qualifier > 127)
            throw new ArgumentOutOfRangeException(nameof(qualifier), qualifier, "IEC-104 setpoint qualifier QL must be in the range 0..127.");
    }
}
