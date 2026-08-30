using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoFailureClassificationTests
{
    [Theory]
    [InlineData(0x03, false, nameof(S7IsoFailureKind.ProtectionDenied))]
    [InlineData(0x03, true, nameof(S7IsoFailureKind.ProtectionDenied))]
    [InlineData(0x05, false, nameof(S7IsoFailureKind.AddressInvalid))]
    [InlineData(0x0A, true, nameof(S7IsoFailureKind.AddressInvalid))]
    [InlineData(0x06, false, nameof(S7IsoFailureKind.TypeUnsupported))]
    [InlineData(0x07, true, nameof(S7IsoFailureKind.TypeUnsupported))]
    [InlineData(0x01, true, nameof(S7IsoFailureKind.WriteRejected))]
    [InlineData(0x01, false, nameof(S7IsoFailureKind.ProtocolFault))]
    public void ReturnCodes_MapToActionableFailureKinds(int code, bool write, string expected)
    {
        Assert.Equal(expected, S7IsoFailureClassifier.ClassifyReturnCode((byte)code, write).ToString());
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
