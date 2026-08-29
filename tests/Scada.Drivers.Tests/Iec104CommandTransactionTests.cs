using System.Buffers.Binary;
using Scada.Drivers.Iec60870;
using Xunit;

namespace Scada.Drivers.Tests;

public sealed class Iec104CommandTransactionTests
{
    [Fact]
    public void SingleDirectOperate_EncodesExecuteAndAcceptsPositiveConfirmation()
    {
        var transaction = Iec104CommandTransaction.Single(
            commonAddress: 1,
            informationObjectAddress: 0x1234,
            value: true,
            Iec104CommandMode.DirectOperate,
            qualifier: 0);

        var request = transaction.CreateInitialRequest();

        Assert.Equal(Iec104TypeId.CScNa1, request.Header.TypeId);
        Assert.Equal(Iec104CommandState.AwaitingExecutionConfirmation, transaction.State);
        Assert.Equal(new byte[] { 0x34, 0x12, 0x00, 0x01 }, request.Payload.ToArray());

        Assert.True(transaction.ObserveResponse(CreateResponse(
            request,
            Iec104CommandTransaction.ActivationConfirmationCause,
            negative: false)));
        Assert.Equal(Iec104CommandState.Accepted, transaction.State);
    }

    [Fact]
    public void SingleSelectBeforeOperate_RequiresSelectConfirmationBeforeExecute()
    {
        var transaction = Iec104CommandTransaction.Single(
            1,
            5000,
            value: true,
            Iec104CommandMode.SelectBeforeOperate,
            qualifier: 2);

        var select = transaction.CreateInitialRequest();
        Assert.Equal((byte)0x89, select.Payload.Span[3]);
        Assert.Equal(Iec104CommandState.AwaitingSelectionConfirmation, transaction.State);

        Assert.True(transaction.ObserveResponse(CreateResponse(
            select,
            Iec104CommandTransaction.ActivationConfirmationCause,
            negative: false)));
        Assert.Equal(Iec104CommandState.Selected, transaction.State);

        var execute = transaction.CreateExecuteAfterSelection();
        Assert.Equal((byte)0x09, execute.Payload.Span[3]);
        Assert.Equal(Iec104CommandState.AwaitingExecutionConfirmation, transaction.State);

        Assert.True(transaction.ObserveResponse(CreateResponse(
            execute,
            Iec104CommandTransaction.ActivationConfirmationCause,
            negative: false)));
        Assert.Equal(Iec104CommandState.Accepted, transaction.State);

        Assert.True(transaction.ObserveResponse(CreateResponse(
            execute,
            Iec104CommandTransaction.ActivationTerminationCause,
            negative: false)));
        Assert.Equal(Iec104CommandState.Completed, transaction.State);
    }

    [Fact]
    public void NegativeActivationConfirmation_RejectsCommand()
    {
        var transaction = Iec104CommandTransaction.Double(
            3,
            44,
            Iec104DoublePointState.Off,
            Iec104CommandMode.DirectOperate);
        var request = transaction.CreateInitialRequest();

        Assert.True(transaction.ObserveResponse(CreateResponse(
            request,
            Iec104CommandTransaction.ActivationConfirmationCause,
            negative: true)));
        Assert.Equal(Iec104CommandState.Rejected, transaction.State);
    }

    [Fact]
    public void DoubleCommand_RejectsIndeterminateStatesBeforeTransmission()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Iec104CommandTransaction.Double(
            1,
            1,
            Iec104DoublePointState.Indeterminate0,
            Iec104CommandMode.DirectOperate));
    }

    [Theory]
    [InlineData(-1f, short.MinValue)]
    [InlineData(0f, (short)0)]
    [InlineData(1f, short.MaxValue)]
    public void NormalizedSetpoint_EncodesProtocolRange(float value, short expectedRaw)
    {
        var transaction = Iec104CommandTransaction.NormalizedSetpoint(
            1,
            10,
            value,
            Iec104CommandMode.DirectOperate);

        var request = transaction.CreateInitialRequest();
        var raw = BinaryPrimitives.ReadInt16LittleEndian(request.Payload.Span.Slice(3, 2));

        Assert.Equal(expectedRaw, raw);
        Assert.Equal((byte)0, request.Payload.Span[5]);
    }

    [Fact]
    public void ScaledSetpoint_SelectBitLivesInQosBit7()
    {
        var transaction = Iec104CommandTransaction.ScaledSetpoint(
            1,
            11,
            value: -123,
            Iec104CommandMode.SelectBeforeOperate,
            qualifier: 5);

        var request = transaction.CreateInitialRequest();

        Assert.Equal((short)-123, BinaryPrimitives.ReadInt16LittleEndian(request.Payload.Span.Slice(3, 2)));
        Assert.Equal((byte)0x85, request.Payload.Span[5]);
    }

    [Fact]
    public void ShortFloatSetpoint_RejectsNonFiniteValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Iec104CommandTransaction.ShortFloatSetpoint(
            1,
            12,
            float.NaN,
            Iec104CommandMode.DirectOperate));
    }

    [Fact]
    public void ResponseForDifferentIoa_IsNotConsumed()
    {
        var transaction = Iec104CommandTransaction.Single(
            1,
            20,
            true,
            Iec104CommandMode.DirectOperate);
        var request = transaction.CreateInitialRequest();
        var payload = request.Payload.ToArray();
        new Iec104InformationObjectAddress(21).WriteTo(payload.AsSpan(0, 3));
        var response = Iec104AsduEnvelope.Create(
            request.Header with
            {
                CauseOfTransmission = new Iec104CauseOfTransmission(Iec104CommandTransaction.ActivationConfirmationCause)
            },
            payload);

        Assert.False(transaction.ObserveResponse(response));
        Assert.Equal(Iec104CommandState.AwaitingExecutionConfirmation, transaction.State);
    }

    private static Iec104AsduEnvelope CreateResponse(
        Iec104AsduEnvelope request,
        byte cause,
        bool negative)
    {
        var header = request.Header with
        {
            CauseOfTransmission = new Iec104CauseOfTransmission(
                cause,
                request.Header.CauseOfTransmission.OriginatorAddress,
                isNegativeConfirmation: negative)
        };
        return Iec104AsduEnvelope.Create(header, request.Payload.Span);
    }
}
