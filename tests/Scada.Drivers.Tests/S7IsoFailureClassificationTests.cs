using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoFailureClassificationTests
{
    [Theory]
    [InlineData(0x03, false, S7IsoFailureKind.ProtectionDenied)]
    [InlineData(0x03, true, S7IsoFailureKind.ProtectionDenied)]
    [InlineData(0x05, false, S7IsoFailureKind.AddressInvalid)]
    [InlineData(0x0A, true, S7IsoFailureKind.AddressInvalid)]
    [InlineData(0x06, false, S7IsoFailureKind.TypeUnsupported)]
    [InlineData(0x07, true, S7IsoFailureKind.TypeUnsupported)]
    [InlineData(0x01, true, S7IsoFailureKind.WriteRejected)]
    [InlineData(0x01, false, S7IsoFailureKind.ProtocolFault)]
    public void ReturnCodes_MapToActionableFailureKinds(int code, bool write, S7IsoFailureKind expected)
    {
        Assert.Equal(expected, S7IsoFailureClassifier.ClassifyReturnCode((byte)code, write));
    }

    [Fact]
    public void ConnectionPhases_DistinguishCotpAndS7SessionRejection()
    {
        var protocol = new S7IsoProtocolException("rejected");

        Assert.Equal(
            S7IsoFailureKind.IsoConnectionRejected,
            S7IsoFailureClassifier.Classify(protocol, S7IsoFailurePhase.CotpConnect));
        Assert.Equal(
            S7IsoFailureKind.S7SessionRejected,
            S7IsoFailureClassifier.Classify(protocol, S7IsoFailurePhase.SetupCommunication));
        Assert.Equal(
            S7IsoFailureKind.ProtocolFault,
            S7IsoFailureClassifier.Classify(protocol, S7IsoFailurePhase.Read));
    }

    [Fact]
    public void TransportAndTimeoutFailures_RemainDistinct()
    {
        Assert.Equal(
            S7IsoFailureKind.TransportUnavailable,
            S7IsoFailureClassifier.Classify(new EndOfStreamException("closed"), S7IsoFailurePhase.Read));
        Assert.Equal(
            S7IsoFailureKind.Timeout,
            S7IsoFailureClassifier.Classify(new TimeoutException("late"), S7IsoFailurePhase.Read));
    }
}