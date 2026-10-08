using System.Globalization;
using System.Text;

namespace Scada.Drivers.Panasonic;

public sealed record PanasonicMewtocolDecodedResponse(byte Station, string ResponseCode, string Data, string? ErrorCode);

/// <summary>Strict classic ASCII MEWTOCOL-COM framing and v1 memory commands.</summary>
public static class PanasonicMewtocolProtocolCodec
{
    public const byte CarriageReturn = 0x0D;

    public static byte[] BuildReadContacts(int station, IReadOnlyList<PanasonicMewtocolAddress> points, PanasonicMewtocolFrameMode mode)
    {
        ValidateFrameMode(mode);
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(points), "RCP supports one through eight contact points.");
        if (points.Any(point => !point.IsContact)) throw new ArgumentException("RCP accepts contact addresses only.", nameof(points));
        var text = new StringBuilder(points.Count == 1 ? "RCS" : "RCP");
        foreach (var point in points)
            text.Append(point.WireAreaCode).Append(FormatContactNumber(point));
        if (points.Count > 1) text.Append((char)('0' + points.Count));
        return BuildRequest(station, mode, text.ToString());
    }

    public static byte[] BuildReadWords(int station, PanasonicMewtocolAddress start, int count, PanasonicMewtocolFrameMode mode)
    {
        ValidateFrameMode(mode);
        ArgumentNullException.ThrowIfNull(start);
        if (start.IsContact) throw new ArgumentException("RD accepts word data areas only.", nameof(start));
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        if (checked(start.Number + count - 1) > start.MaximumAddress)
            throw new ArgumentOutOfRangeException(nameof(count), "RD range exceeds the selected Panasonic family profile.");
        var maximumWords = mode == PanasonicMewtocolFrameMode.Standard ? 24 : 509;
        if (count > maximumWords)
            throw new ArgumentOutOfRangeException(nameof(count), "RD count exceeds the selected MEWTOCOL-COM frame limit.");
        var text = string.Create(CultureInfo.InvariantCulture,
            $"RD{start.WireAreaCode}{start.Number:D5}{start.Number + count - 1:D5}");
        return BuildRequest(station, mode, text);
    }

    public static byte[] BuildWriteWord(int station, PanasonicMewtocolAddress address, ReadOnlySpan<byte> littleEndianWord, PanasonicMewtocolFrameMode mode)
    {
        ValidateFrameMode(mode);
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsContact || !address.IsWritable) throw new ArgumentException("WD requires a writable word area.", nameof(address));
        if (littleEndianWord.Length != 2) throw new ArgumentException("WD v1 writes exactly one 16-bit word.", nameof(littleEndianWord));
        var payload = Convert.ToHexString(littleEndianWord);
        var text = string.Create(CultureInfo.InvariantCulture,
            $"WD{address.WireAreaCode}{address.Number:D5}{address.Number:D5}{payload}");
        return BuildRequest(station, mode, text);
    }

    public static byte[] BuildWriteContact(int station, PanasonicMewtocolAddress address, bool value, PanasonicMewtocolFrameMode mode)
    {
        ValidateFrameMode(mode);
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsContact || !address.IsWritable) throw new ArgumentException("WCS requires a writable contact area.", nameof(address));
        var text = string.Create(CultureInfo.InvariantCulture,
            $"WCS{address.WireAreaCode}{FormatContactNumber(address)}{(value ? '1' : '0')}");
        return BuildRequest(station, mode, text);
    }

    public static byte[] BuildStatusRead(int station, PanasonicMewtocolFrameMode mode) => BuildRequest(station, mode, "RT");

    public static byte[] BuildRequest(int station, PanasonicMewtocolFrameMode mode, string commandText)
    {
        ValidateFrameMode(mode);
        if (station is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(station));
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        if (commandText.Any(character => character is < ' ' or > '~')) throw new ArgumentException("MEWTOCOL-COM command text must be printable ASCII.", nameof(commandText));
        var header = mode == PanasonicMewtocolFrameMode.Standard ? '%' : '<';
        var prefix = string.Create(CultureInfo.InvariantCulture, $"{header}{station:D2}#{commandText}");
        var bcc = CalculateBcc(Encoding.ASCII.GetBytes(prefix));
        var frame = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{prefix}{bcc:X2}\r"));
        var maximum = mode == PanasonicMewtocolFrameMode.Standard ? 118 : 2048;
        if (frame.Length > maximum) throw new ArgumentOutOfRangeException(nameof(commandText), "MEWTOCOL-COM frame exceeds its selected character limit.");
        return frame;
    }

    public static PanasonicMewtocolDecodedResponse DecodeResponse(
        ReadOnlySpan<byte> frame,
        int expectedStation,
        PanasonicMewtocolFrameMode mode,
        string expectedResponseCode)
    {
        ValidateFrameMode(mode);
        if (expectedStation is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(expectedStation));
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedResponseCode);
        var maximum = mode == PanasonicMewtocolFrameMode.Standard ? 118 : 2048;
        if (frame.Length < 9 || frame.Length > maximum || frame[^1] != CarriageReturn)
            throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM response has an invalid length or terminator.", "invalid_frame");
        var expectedHeader = mode == PanasonicMewtocolFrameMode.Standard ? (byte)'%' : (byte)'<';
        if (frame[0] != expectedHeader || frame[1] is < (byte)'0' or > (byte)'9' || frame[2] is < (byte)'0' or > (byte)'9')
            throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM response header or station field is invalid.", "invalid_header");
        var station = (byte)((frame[1] - '0') * 10 + frame[2] - '0');
        if (station != expectedStation)
            throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM response came from an unexpected station.", "station_mismatch");
        if (!IsAsciiHex(frame[^3]) || !IsAsciiHex(frame[^2]))
            throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM response BCC is not hexadecimal.", "invalid_bcc");
        var suppliedBcc = byte.Parse(Encoding.ASCII.GetString(frame[^3..^1]), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var calculatedBcc = CalculateBcc(frame[..^3]);
        if (suppliedBcc != calculatedBcc)
            throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM response BCC does not match its contents.", "bcc_mismatch");

        if (frame[3] == '!')
        {
            if (frame.Length != 9 || !IsAsciiHex(frame[4]) || !IsAsciiHex(frame[5]))
                throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM error response has an invalid shape.", "invalid_error_response");
            var errorCode = Encoding.ASCII.GetString(frame.Slice(4, 2));
            throw new PanasonicMewtocolProtocolException(
                $"Panasonic MEWTOCOL-COM returned error code 0x{errorCode}.", "plc_error", errorCode,
                dispatchMayHaveOccurred: true);
        }
        if (frame[3] != '$')
            throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM response marker is neither normal nor error.", "invalid_response_marker");

        var dataStart = 4 + expectedResponseCode.Length;
        if (frame.Length < dataStart + 3 || !frame.Slice(4, expectedResponseCode.Length).SequenceEqual(Encoding.ASCII.GetBytes(expectedResponseCode)))
            throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM response command code does not match the request.", "response_command_mismatch");
        var data = Encoding.ASCII.GetString(frame[dataStart..^3]);
        if (expectedResponseCode == "RD" && data.Any(character => !IsAsciiHex((byte)character)))
            throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM word response contains non-hexadecimal data.", "invalid_word_data");
        if (expectedResponseCode == "RC" && data.Any(character => character is not ('0' or '1')))
            throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM contact response contains a value other than 0 or 1.", "invalid_contact_value");
        return new PanasonicMewtocolDecodedResponse(station, expectedResponseCode, data, null);
    }

    public static byte CalculateBcc(ReadOnlySpan<byte> bytes)
    {
        byte value = 0;
        foreach (var item in bytes) value ^= item;
        return value;
    }

    private static bool IsAsciiHex(byte value) => value is >= (byte)'0' and <= (byte)'9' or
        >= (byte)'A' and <= (byte)'F' or >= (byte)'a' and <= (byte)'f';

    private static void ValidateFrameMode(PanasonicMewtocolFrameMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
    }

    public static string FormatContactNumber(PanasonicMewtocolAddress address)
    {
        var text = PanasonicMewtocolAddress.IsHexContact(address.Area)
            ? $"{address.Number / 16:D3}{(address.Number & 0xF).ToString("X", CultureInfo.InvariantCulture)}"
            : address.Number.ToString("D4", CultureInfo.InvariantCulture);
        if (text.Length > 4) throw new ArgumentOutOfRangeException(nameof(address), "Contact address does not fit the four-character MEWTOCOL-COM contact field.");
        return text.PadLeft(4, '0');
    }
}
