using System.Net.Sockets;

namespace Scada.Drivers.SiemensS7Iso;

internal enum S7IsoFailureKind
{
    TransportUnavailable,
    IsoConnectionRejected,
    S7SessionRejected,
    ProtectionDenied,
    AddressInvalid,
    TypeUnsupported,
    WriteRejected,
    Timeout,
    ProtocolFault
}

internal enum S7IsoFailurePhase
{
    ConnectTransport,
    CotpConnect,
    SetupCommunication,
    Read,
    Write
}

internal static class S7IsoFailureClassifier
{
    public static S7IsoFailureKind Classify(Exception error, S7IsoFailurePhase phase)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error is TimeoutException)
            return S7IsoFailureKind.Timeout;

        if (error is S7IsoProtocolException protocol && protocol.ReturnCode.HasValue)
            return ClassifyReturnCode(protocol.ReturnCode.Value, phase == S7IsoFailurePhase.Write);

        if (phase == S7IsoFailurePhase.CotpConnect && error is S7IsoProtocolException)
            return S7IsoFailureKind.IsoConnectionRejected;

        if (phase == S7IsoFailurePhase.SetupCommunication && error is S7IsoProtocolException)
            return S7IsoFailureKind.S7SessionRejected;

        if (error is SocketException or EndOfStreamException)
            return S7IsoFailureKind.TransportUnavailable;

        if (error is IOException && error is not S7IsoProtocolException)
            return S7IsoFailureKind.TransportUnavailable;

        return S7IsoFailureKind.ProtocolFault;
    }

    public static S7IsoFailureKind ClassifyReturnCode(byte returnCode, bool writeOperation) => returnCode switch
    {
        0x03 => S7IsoFailureKind.ProtectionDenied,
        0x05 or 0x0A => S7IsoFailureKind.AddressInvalid,
        0x06 or 0x07 => S7IsoFailureKind.TypeUnsupported,
        0x01 when writeOperation => S7IsoFailureKind.WriteRejected,
        _ when writeOperation => S7IsoFailureKind.WriteRejected,
        _ => S7IsoFailureKind.ProtocolFault
    };
}