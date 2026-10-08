using System.Globalization;
using System.Net;
using Scada.Core.Tags;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Panasonic;

public enum PanasonicMewtocolFamilyProfile
{
    Fp0rF32,
    FpXhCommon,
    Fp7RClassicCom
}

public enum PanasonicMewtocolTransportKind
{
    Tcp,
    Serial
}

public enum PanasonicMewtocolFrameMode
{
    Standard,
    Expanded
}

public enum PanasonicMewtocolArea
{
    X, Y, R, L, T, C,
    WX, WY, WR, WL, DT, LD
}

public enum PanasonicMewtocolPhysicalType
{
    Boolean,
    UInt16,
    Int16
}

public sealed record PanasonicMewtocolAreaCapability(
    int MinimumAddress,
    int MaximumAddress,
    bool IsContact,
    bool IsWritable);

public sealed record PanasonicMewtocolFamilyCapabilities(
    PanasonicMewtocolFamilyProfile Profile,
    IReadOnlyDictionary<PanasonicMewtocolArea, PanasonicMewtocolAreaCapability> Areas,
    int MaximumFrameCharacters,
    int MaximumReadWords,
    int MaximumWriteWords,
    int MaximumSparseContacts,
    bool SupportsExpandedFrame,
    string EvidenceNote)
{
    public bool TryGetArea(PanasonicMewtocolArea area, out PanasonicMewtocolAreaCapability capability) =>
        Areas.TryGetValue(area, out capability!);

    public static PanasonicMewtocolFamilyCapabilities For(PanasonicMewtocolFamilyProfile profile, PanasonicMewtocolFrameMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var areas = profile switch
        {
            PanasonicMewtocolFamilyProfile.Fp0rF32 => Fp0rF32Areas(),
            PanasonicMewtocolFamilyProfile.FpXhCommon => FpXhAreas(),
            PanasonicMewtocolFamilyProfile.Fp7RClassicCom => Fp7RAreas(),
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };

        var expanded = mode == PanasonicMewtocolFrameMode.Expanded;
        if (expanded && profile == PanasonicMewtocolFamilyProfile.Fp0rF32)
            throw new ArgumentException("Expanded MEWTOCOL-COM framing is not enabled by the bounded FP0R F32 profile.", nameof(mode));

        var frameChars = expanded ? 2048 : 118;
        return new PanasonicMewtocolFamilyCapabilities(
            profile,
            areas,
            frameChars,
            expanded ? 509 : 24,
            expanded ? 507 : 24,
            MaximumSparseContacts: 8,
            SupportsExpandedFrame: profile != PanasonicMewtocolFamilyProfile.Fp0rF32,
            EvidenceNote: profile switch
            {
                PanasonicMewtocolFamilyProfile.Fp0rF32 => "FP0R F32 memory limits from WUME-FP0R-03; standard COM frames only.",
                PanasonicMewtocolFamilyProfile.FpXhCommon => "Conservative FP-XH common subset based on C14 minima in WUME-FPXHBASG-061.",
                _ => "Current FP7 R-series classic-COM subset from WUME-FP7CPUH-15; 4-decimal word field caps DT/LD at 9999."
            });
    }

    private static IReadOnlyDictionary<PanasonicMewtocolArea, PanasonicMewtocolAreaCapability> Fp0rF32Areas() =>
        new Dictionary<PanasonicMewtocolArea, PanasonicMewtocolAreaCapability>
        {
            [PanasonicMewtocolArea.X] = Contact(0, ContactNumber(109, 15), writable: false),
            [PanasonicMewtocolArea.Y] = Contact(0, ContactNumber(109, 15), writable: true),
            [PanasonicMewtocolArea.R] = Contact(0, ContactNumber(255, 15), writable: true),
            [PanasonicMewtocolArea.L] = Contact(0, ContactNumber(127, 15), writable: true),
            [PanasonicMewtocolArea.T] = Contact(0, 1007, writable: false),
            [PanasonicMewtocolArea.C] = Contact(1008, 1023, writable: false),
            [PanasonicMewtocolArea.WX] = Word(0, 109, writable: false),
            [PanasonicMewtocolArea.WY] = Word(0, 109, writable: true),
            [PanasonicMewtocolArea.WR] = Word(0, 255, writable: true),
            [PanasonicMewtocolArea.WL] = Word(0, 127, writable: true),
            [PanasonicMewtocolArea.DT] = Word(0, 9999, writable: true),
            [PanasonicMewtocolArea.LD] = Word(0, 255, writable: true)
        };

    private static IReadOnlyDictionary<PanasonicMewtocolArea, PanasonicMewtocolAreaCapability> FpXhAreas() =>
        new Dictionary<PanasonicMewtocolArea, PanasonicMewtocolAreaCapability>
        {
            [PanasonicMewtocolArea.X] = Contact(0, ContactNumber(109, 15), writable: false),
            [PanasonicMewtocolArea.Y] = Contact(0, ContactNumber(109, 15), writable: true),
            [PanasonicMewtocolArea.R] = Contact(0, ContactNumber(255, 15), writable: true),
            [PanasonicMewtocolArea.L] = Contact(0, ContactNumber(127, 15), writable: true),
            [PanasonicMewtocolArea.T] = Contact(0, 1007, writable: false),
            [PanasonicMewtocolArea.C] = Contact(1008, 1023, writable: false),
            [PanasonicMewtocolArea.WX] = Word(0, 109, writable: false),
            [PanasonicMewtocolArea.WY] = Word(0, 109, writable: true),
            [PanasonicMewtocolArea.WR] = Word(0, 255, writable: true),
            [PanasonicMewtocolArea.WL] = Word(0, 127, writable: true),
            [PanasonicMewtocolArea.DT] = Word(0, 9999, writable: true),
            [PanasonicMewtocolArea.LD] = Word(0, 255, writable: true)
        };

    private static IReadOnlyDictionary<PanasonicMewtocolArea, PanasonicMewtocolAreaCapability> Fp7RAreas() =>
        new Dictionary<PanasonicMewtocolArea, PanasonicMewtocolAreaCapability>
        {
            [PanasonicMewtocolArea.X] = Contact(0, ContactNumber(511, 15), writable: false),
            [PanasonicMewtocolArea.Y] = Contact(0, ContactNumber(511, 15), writable: true),
            [PanasonicMewtocolArea.R] = Contact(0, ContactNumber(999, 15), writable: true),
            [PanasonicMewtocolArea.L] = Contact(0, ContactNumber(999, 15), writable: true),
            [PanasonicMewtocolArea.T] = Contact(0, 4095, writable: false),
            [PanasonicMewtocolArea.C] = Contact(0, 1023, writable: false),
            [PanasonicMewtocolArea.WX] = Word(0, 511, writable: false),
            [PanasonicMewtocolArea.WY] = Word(0, 511, writable: true),
            [PanasonicMewtocolArea.WR] = Word(0, 2047, writable: true),
            [PanasonicMewtocolArea.WL] = Word(0, 1023, writable: true),
            [PanasonicMewtocolArea.DT] = Word(0, 9999, writable: true),
            [PanasonicMewtocolArea.LD] = Word(0, 9999, writable: true)
        };

    private static PanasonicMewtocolAreaCapability Contact(int min, int max, bool writable) => new(min, max, true, writable);
    private static PanasonicMewtocolAreaCapability Word(int min, int max, bool writable) => new(min, max, false, writable);
    private static int ContactNumber(int decimalPrefix, int hexNibble) => checked(decimalPrefix * 16 + hexNibble);
}

public sealed record PanasonicMewtocolConnectionOptions(
    PanasonicMewtocolTransportKind Transport,
    PanasonicMewtocolFamilyProfile FamilyProfile,
    PanasonicMewtocolFrameMode FrameMode,
    int Station,
    TimeSpan ScanInterval,
    TimeSpan RequestTimeout,
    TimeSpan ConnectTimeout,
    TimeSpan TurnaroundDelay,
    string? Host,
    int? Port,
    HostSerialLineSettings? SerialSettings)
{
    public PanasonicMewtocolFamilyCapabilities Capabilities => PanasonicMewtocolFamilyCapabilities.For(FamilyProfile, FrameMode);
    public string SanitizedEndpoint => Transport == PanasonicMewtocolTransportKind.Tcp
        ? FormatEndpoint(Host!, Port!.Value)
        : $"serial:{SerialSettings!.PortName}";

    public static bool TryCreate(
        string driverType,
        IReadOnlyDictionary<string, string> settings,
        out PanasonicMewtocolConnectionOptions? options,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(settings);
        options = null;
        error = null;
        var isTcp = string.Equals(driverType, PanasonicMewtocolDriverDescriptorProvider.TcpDriverTypeId, StringComparison.OrdinalIgnoreCase);
        var isSerial = string.Equals(driverType, PanasonicMewtocolDriverDescriptorProvider.SerialDriverTypeId, StringComparison.OrdinalIgnoreCase);
        if (!isTcp && !isSerial)
        {
            error = "Driver type must be a Panasonic MEWTOCOL TCP or Host Serial type.";
            return false;
        }

        if (!TryInteger(settings, "station", 1, 99, out var station) ||
            !TryInteger(settings, "scanIntervalMilliseconds", 1000, 10, 600000, out var scanInterval) ||
            !TryInteger(settings, "requestTimeoutMilliseconds", 3000, 250, 60000, out var requestTimeout) ||
            !TryInteger(settings, "connectTimeoutMilliseconds", 5000, 100, 60000, out var connectTimeout) ||
            !TryInteger(settings, "turnaroundMilliseconds", 0, 0, 1000, out var turnaround))
        {
            error = "One or more Panasonic Data Source numeric settings are outside their supported bounds.";
            return false;
        }

        if (!Enum.TryParse<PanasonicMewtocolFamilyProfile>(Get(settings, "familyProfile"), true, out var familyProfile) ||
            !Enum.IsDefined(familyProfile))
        {
            error = "familyProfile must be Fp0rF32, FpXhCommon, or Fp7RClassicCom.";
            return false;
        }

        if (!Enum.TryParse<PanasonicMewtocolFrameMode>(Get(settings, "frameMode") ?? nameof(PanasonicMewtocolFrameMode.Standard), true, out var frameMode) ||
            !Enum.IsDefined(frameMode))
        {
            error = "frameMode must be Standard or Expanded.";
            return false;
        }
        if (frameMode == PanasonicMewtocolFrameMode.Expanded && familyProfile == PanasonicMewtocolFamilyProfile.Fp0rF32)
        {
            error = "Expanded frame mode is not enabled by the bounded FP0R F32 profile.";
            return false;
        }

        if (isTcp)
        {
            var host = Get(settings, "host");
            if (!IsValidHost(host) || !TryInteger(settings, "port", 1, 65535, out var port))
            {
                error = "A valid TCP host and port are required.";
                return false;
            }
            options = new PanasonicMewtocolConnectionOptions(
                PanasonicMewtocolTransportKind.Tcp, familyProfile, frameMode, station,
                TimeSpan.FromMilliseconds(scanInterval), TimeSpan.FromMilliseconds(requestTimeout),
                TimeSpan.FromMilliseconds(connectTimeout), TimeSpan.FromMilliseconds(turnaround),
                host!.Trim(), port, null);
            return true;
        }

        var portName = Get(settings, "serialPort");
        if (string.IsNullOrWhiteSpace(portName) || !string.Equals(portName, portName.Trim(), StringComparison.Ordinal) ||
            !TryInteger(settings, "baudRate", 9600, 300, 230400, out var baudRate) ||
            !TryInteger(settings, "dataBits", 8, 7, 8, out var dataBits) ||
            !Enum.TryParse<HostSerialParity>(Get(settings, "parity") ?? nameof(HostSerialParity.None), true, out var parity) || !Enum.IsDefined(parity) ||
            !Enum.TryParse<HostSerialStopBits>(Get(settings, "stopBits") ?? nameof(HostSerialStopBits.One), true, out var stopBits) || !Enum.IsDefined(stopBits))
        {
            error = "A server-visible serial port, baud rate, data bits, parity, and stop bits are required.";
            return false;
        }
        if (parity is not (HostSerialParity.None or HostSerialParity.Odd or HostSerialParity.Even) ||
            stopBits is not (HostSerialStopBits.One or HostSerialStopBits.Two))
        {
            error = "MEWTOCOL Host Serial supports 7/8 data bits, None/Odd/Even parity, and one/two stop bits in v1.";
            return false;
        }

        try
        {
            var line = new HostSerialLineSettings(portName!, baudRate, dataBits, parity, stopBits);
            line.Validate();
            options = new PanasonicMewtocolConnectionOptions(
                PanasonicMewtocolTransportKind.Serial, familyProfile, frameMode, station,
                TimeSpan.FromMilliseconds(scanInterval), TimeSpan.FromMilliseconds(requestTimeout),
                TimeSpan.FromMilliseconds(connectTimeout), TimeSpan.FromMilliseconds(turnaround),
                null, null, line);
            return true;
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryInteger(IReadOnlyDictionary<string, string> settings, string key, int min, int max, out int value) =>
        TryInteger(settings, key, (int?)null, min, max, out value);

    private static bool TryInteger(IReadOnlyDictionary<string, string> settings, string key, int fallback, int min, int max, out int value) =>
        TryInteger(settings, key, (int?)fallback, min, max, out value);

    private static bool TryInteger(IReadOnlyDictionary<string, string> settings, string key, int? fallback, int min, int max, out int value)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw) && fallback.HasValue) { value = fallback.Value; return value >= min && value <= max; }
        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
    }

    public static string? Get(IReadOnlyDictionary<string, string> settings, string key)
    {
        if (settings.TryGetValue(key, out var value)) return value;
        foreach (var pair in settings)
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        return null;
    }

    private static bool IsValidHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) && string.Equals(host, host.Trim(), StringComparison.Ordinal) &&
        host.Length <= 253 && !host.Any(char.IsWhiteSpace) && !host.Contains('@') &&
        host.IndexOfAny(['/', '\\', '\r', '\n', '\0', '[', ']']) < 0 &&
        (Uri.CheckHostName(host) != UriHostNameType.Unknown || IPAddress.TryParse(host, out _));

    private static string FormatEndpoint(string host, int port) => host.Contains(':') ? $"[{host}]:{port}" : $"{host}:{port}";
}

public sealed record PanasonicMewtocolPoint(
    TagDefinition Tag,
    PanasonicMewtocolAddress Address,
    PanasonicMewtocolPhysicalType PhysicalType,
    bool Writable,
    TagPhysicalValueTransform Transform);

public sealed class PanasonicMewtocolProtocolException : IOException
{
    public PanasonicMewtocolProtocolException(
        string message,
        string failureKind,
        string? errorCode = null,
        bool dispatchMayHaveOccurred = false,
        Exception? innerException = null) : base(message, innerException)
    {
        FailureKind = failureKind;
        ErrorCode = errorCode;
        DispatchMayHaveOccurred = dispatchMayHaveOccurred;
    }

    public string FailureKind { get; }
    public string? ErrorCode { get; }
    public bool DispatchMayHaveOccurred { get; }
}

public sealed class PanasonicMewtocolWriteOutcomeUnknownException : IOException
{
    public PanasonicMewtocolWriteOutcomeUnknownException(string message, string? errorCode = null, Exception? innerException = null)
        : base(message, innerException) => ErrorCode = errorCode;

    public string? ErrorCode { get; }
}

public static class PanasonicMewtocolValueCodec
{
    public static TagDataType CanonicalDataType(PanasonicMewtocolPhysicalType type) => type switch
    {
        PanasonicMewtocolPhysicalType.Boolean => TagDataType.Boolean,
        PanasonicMewtocolPhysicalType.UInt16 or PanasonicMewtocolPhysicalType.Int16 => TagDataType.Int32,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static object DecodeWord(ReadOnlySpan<byte> wireBytes, PanasonicMewtocolPhysicalType type, TagPhysicalValueTransform? transform = null)
    {
        if (wireBytes.Length != 2) throw new ArgumentException("MEWTOCOL v1 values occupy exactly one 16-bit word.", nameof(wireBytes));
        transform ??= new TagPhysicalValueTransform();
        transform.Validate();
        if (transform.WordSwap) throw new NotSupportedException("Word Swap is not defined for a single MEWTOCOL 16-bit word.");
        var low = transform.ByteSwap ? wireBytes[1] : wireBytes[0];
        var high = transform.ByteSwap ? wireBytes[0] : wireBytes[1];
        var raw = (ushort)(low | (high << 8));
        return type switch
        {
            PanasonicMewtocolPhysicalType.UInt16 => (int)raw,
            PanasonicMewtocolPhysicalType.Int16 => (int)unchecked((short)raw),
            _ => throw new ArgumentOutOfRangeException(nameof(type), "Boolean values use contact commands.")
        };
    }

    public static byte[] EncodeWord(object? value, PanasonicMewtocolPhysicalType type, TagPhysicalValueTransform? transform = null)
    {
        transform ??= new TagPhysicalValueTransform();
        transform.Validate();
        if (transform.WordSwap) throw new NotSupportedException("Word Swap is not defined for a single MEWTOCOL 16-bit word.");
        var number = type switch
        {
            PanasonicMewtocolPhysicalType.UInt16 => ToUInt16(value),
            PanasonicMewtocolPhysicalType.Int16 => unchecked((ushort)ToInt16(value)),
            _ => throw new ArgumentOutOfRangeException(nameof(type), "Boolean values use contact commands.")
        };
        return transform.ByteSwap
            ? new[] { (byte)(number >> 8), (byte)number }
            : new[] { (byte)number, (byte)(number >> 8) };
    }

    public static bool ToBoolean(object? value) => value switch
    {
        bool bit => bit,
        byte n when n <= 1 => n == 1,
        sbyte n when n is 0 or 1 => n == 1,
        short n when n is 0 or 1 => n == 1,
        ushort n when n <= 1 => n == 1,
        int n when n is 0 or 1 => n == 1,
        uint n when n <= 1 => n == 1,
        long n when n is 0 or 1 => n == 1,
        _ => throw new ArgumentException("A MEWTOCOL Boolean write requires Boolean or numeric 0/1.", nameof(value))
    };

    private static ushort ToUInt16(object? value)
    {
        var number = value switch
        {
            byte n => n, ushort n => n, uint n when n <= ushort.MaxValue => n,
            int n when n >= 0 && n <= ushort.MaxValue => (uint)n,
            long n when n >= 0 && n <= ushort.MaxValue => (uint)n,
            _ => throw new ArgumentException("UInt16 requires a non-negative integer canonical value in range.", nameof(value))
        };
        return checked((ushort)number);
    }

    private static short ToInt16(object? value)
    {
        var number = value switch
        {
            sbyte n => n, byte n => n, short n => n,
            ushort n when n <= short.MaxValue => (int)n,
            int n when n >= short.MinValue && n <= short.MaxValue => n,
            long n when n >= short.MinValue && n <= short.MaxValue => (int)n,
            _ => throw new ArgumentException("Int16 requires an integer canonical value in range.", nameof(value))
        };
        return checked((short)number);
    }
}
