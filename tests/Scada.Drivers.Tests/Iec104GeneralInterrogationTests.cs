using Scada.Drivers.Iec60870;
using Xunit;

namespace Scada.Drivers.Tests;

public sealed class Iec104GeneralInterrogationTests
{
    [Fact]
    public void CreateActivation_BuildsGlobalGeneralInterrogationRequest()
    {
        var transaction = new Iec104GeneralInterrogationTransaction(
            commonAddress: 7,
            originatorAddress: 3);

        var asdu = transaction.CreateActivation();

        Assert.Equal(Iec104TypeId.CIcNa1, asdu.Header.TypeId);
        Assert.Equal((byte)1, asdu.Header.ObjectCount);
        Assert.False(asdu.Header.IsSequence);
        Assert.Equal((byte)Iec104GeneralInterrogationTransaction.ActivationCause, asdu.Header.CauseOfTransmission.CauseCode);
        Assert.Equal((byte)3, asdu.Header.CauseOfTransmission.OriginatorAddress);
        Assert.Equal((ushort)7, asdu.Header.CommonAddress);
        Assert.Equal(new byte[] { 0, 0, 0, Iec104GeneralInterrogationTransaction.GlobalQoi }, asdu.Payload.ToArray());
    }

    [Fact]
    public void ObserveControlResponse_PositiveConfirmationThenTermination_CompletesTransaction()
    {
        var transaction = new Iec104GeneralInterrogationTransaction(1, originatorAddress: 2);

        Assert.True(transaction.ObserveControlResponse(CreateResponse(
            commonAddress: 1,
            originatorAddress: 2,
            cause: Iec104GeneralInterrogationTransaction.ActivationConfirmationCause,
            negative: false)));
        Assert.Equal(Iec104GeneralInterrogationState.Collecting, transaction.State);

        Assert.True(transaction.ObserveControlResponse(CreateResponse(
            commonAddress: 1,
            originatorAddress: 2,
            cause: Iec104GeneralInterrogationTransaction.ActivationTerminationCause,
            negative: false)));
        Assert.Equal(Iec104GeneralInterrogationState.Completed, transaction.State);
    }

    [Fact]
    public void ObserveControlResponse_NegativeActivationConfirmation_RejectsTransaction()
    {
        var transaction = new Iec104GeneralInterrogationTransaction(1);

        Assert.True(transaction.ObserveControlResponse(CreateResponse(
            commonAddress: 1,
            originatorAddress: 0,
            cause: Iec104GeneralInterrogationTransaction.ActivationConfirmationCause,
            negative: true)));

        Assert.Equal(Iec104GeneralInterrogationState.Rejected, transaction.State);
    }

    [Fact]
    public void ObserveControlResponse_OtherCommonAddress_IsIgnored()
    {
        var transaction = new Iec104GeneralInterrogationTransaction(1);

        Assert.False(transaction.ObserveControlResponse(CreateResponse(
            commonAddress: 2,
            originatorAddress: 0,
            cause: Iec104GeneralInterrogationTransaction.ActivationConfirmationCause,
            negative: false)));
        Assert.Equal(Iec104GeneralInterrogationState.AwaitingActivationConfirmation, transaction.State);
    }

    [Fact]
    public void ObserveControlResponse_TerminationBeforeConfirmation_IsProtocolError()
    {
        var transaction = new Iec104GeneralInterrogationTransaction(1);

        Assert.Throws<Iec104ProtocolException>(() => transaction.ObserveControlResponse(CreateResponse(
            commonAddress: 1,
            originatorAddress: 0,
            cause: Iec104GeneralInterrogationTransaction.ActivationTerminationCause,
            negative: false)));
    }

    [Fact]
    public void ObserveControlResponse_WrongQoi_IsProtocolError()
    {
        var transaction = new Iec104GeneralInterrogationTransaction(1);
        var response = CreateResponse(
            commonAddress: 1,
            originatorAddress: 0,
            cause: Iec104GeneralInterrogationTransaction.ActivationConfirmationCause,
            negative: false,
            qoi: 21);

        Assert.Throws<Iec104ProtocolException>(() => transaction.ObserveControlResponse(response));
    }

    private static Iec104AsduEnvelope CreateResponse(
        ushort commonAddress,
        byte originatorAddress,
        byte cause,
        bool negative,
        byte qoi = Iec104GeneralInterrogationTransaction.GlobalQoi)
    {
        var header = new Iec104AsduHeader(
            Iec104TypeId.CIcNa1,
            ObjectCount: 1,
            IsSequence: false,
            new Iec104CauseOfTransmission(cause, originatorAddress, isNegativeConfirmation: negative),
            commonAddress);

        return Iec104AsduEnvelope.Create(header, new byte[] { 0, 0, 0, qoi });
    }
}
