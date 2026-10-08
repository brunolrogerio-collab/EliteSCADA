using Scada.Core.Tags;
using System.Net;

namespace Scada.Drivers.Mitsubishi;

public enum MitsubishiMelsecFamilyProfile
{
    Fx5U32MtDs,
    IqR04EnCpu
}

public enum MitsubishiMelsecDeviceArea
{
    X,
    Y,
    M,
    L,
    B,
    D,
    W,
    R
}

public enum MitsubishiMelsecPhysicalType
{
    Bit,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Float32
}

public sealed record MitsubishiMelsecRoute(
    byte NetworkNo = 0,
    byte StationNo = 0xFF,
    ushort ModuleIoNo = 0x03FF,
    byte MultidropStationNo = 0);

public sealed record MitsubishiMelsecConnectionOptions(
    string Host,
    int Port,
    MitsubishiMelsecFamilyProfile FamilyProfile,
    MitsubishiMelsecRoute Route,
    TimeSpan ScanInterval,
    TimeSpan ConnectTimeout,
    TimeSpan RequestTimeout,
    ushort MonitoringTimerUnits,
    int MaxWordsPerRequest,
    int MaxBitsPerRequest,
    int MaxRandomPointsPerRequest,
    int MaxBlocksPerRequest,
    int? RMaximumAddress = null)
{
    public string SanitizedEndpoint => Host.Contains(':') ? $"[{Host}]:{Port}" : $"{Host}:{Port}";

    public static bool TryCreate(
        IReadOnlyDictionary<string, string> settings,
        out MitsubishiMelsecConnectionOptions? options,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(settings);
        options = null;
        error = null;

        var host = Get(settings, "host");
        if (string.IsNullOrWhiteSpace(host) || !string.Equals(host, host.Trim(), StringComparison.Ordinal) ||
            host.Length > 253 || host.Any(char.IsWhiteSpace) || host.Contains('@') ||
            host.IndexOfAny(['/', '\\', '\r', '\n', '\0', '[', ']']) >= 0 ||
            (Uri.CheckHostName(host) == UriHostNameType.Unknown && !IPAddress.TryParse(host, out _)))
        {
            error = "A valid Mitsubishi host name or IP address is required.";
            return false;
        }

        if (!TryInt(settings, "port", 5007, 1, 65535, out var port) ||
            !TryInt(settings, "scanIntervalMilliseconds", 1000, 10, 600000, out var scanInterval) ||
            !TryInt(settings, "connectTimeoutMilliseconds", 5000, 100, 60000, out var connectTimeout) ||
            !TryInt(settings, "requestTimeoutMilliseconds", 6000, 250, 60000, out var requestTimeout) ||
            !TryInt(settings, "monitoringTimerUnits", 20, 1, ushort.MaxValue, out var monitoringTimer) ||
            !TryInt(settings, "maxWordsPerRequest", 64, 2, 960, out var maxWords) ||
            !TryInt(settings, "maxBitsPerRequest", 64, 1, 7168, out var maxBits) ||
            !TryInt(settings, "maxRandomPointsPerRequest", 16, 1, 192, out var maxRandomPoints) ||
            !TryInt(settings, "maxBlocksPerRequest", 8, 1, 120, out var maxBlocks))
        {
            error = "One or more Mitsubishi Data Source settings are outside their supported range.";
            return false;
        }

        if (requestTimeout <= monitoringTimer * 250)
        {
            error = "requestTimeoutMilliseconds must exceed the SLMP monitoring timer budget.";
            return false;
        }

        if (!Enum.TryParse<MitsubishiMelsecFamilyProfile>(Get(settings, "familyProfile"), true, out var familyProfile) || !Enum.IsDefined(familyProfile))
        {
            error = "familyProfile must be Fx5U32MtDs or IqR04EnCpu.";
            return false;
        }

        if (!TryRoute(settings, out var route, out error)) return false;

        int? rMaximumAddress = null;
        var rawRMaximum = Get(settings, "rMaximumAddress");
        if (!string.IsNullOrWhiteSpace(rawRMaximum))
        {
            if (!int.TryParse(rawRMaximum, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedRMaximum) ||
                parsedRMaximum is < 0 or > MitsubishiMelsecAddress.MaximumWireAddress)
            {
                error = "rMaximumAddress must be a decimal address within the SLMP device-number field.";
                return false;
            }
            rMaximumAddress = parsedRMaximum;
        }

        options = new MitsubishiMelsecConnectionOptions(
            host.Trim(), port, familyProfile, route!,
            TimeSpan.FromMilliseconds(scanInterval),
            TimeSpan.FromMilliseconds(connectTimeout),
            TimeSpan.FromMilliseconds(requestTimeout),
            checked((ushort)monitoringTimer),
            maxWords, maxBits, maxRandomPoints, maxBlocks, rMaximumAddress);
        return true;
    }

    private static bool TryRoute(
        IReadOnlyDictionary<string, string> settings,
        out MitsubishiMelsecRoute? route,
        out string? error)
    {
        route = null;
        error = null;
        if (!TryInt(settings, "networkNo", 0, 0, 239, out var networkNo) ||
            !TryInt(settings, "stationNo", 255, 1, 255, out var stationNo) ||
            !TryInt(settings, "multidropStationNo", 0, 0, 31, out var multidropStationNo))
        {
            error = "Mitsubishi route settings are outside the supported 3E range.";
            return false;
        }

        var rawModuleIo = Get(settings, "moduleIoNo");
        ushort moduleIoNo = 0x03FF;
        if (!string.IsNullOrWhiteSpace(rawModuleIo) &&
            (rawModuleIo.Length > 4 || !ushort.TryParse(rawModuleIo, System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out moduleIoNo)))
        {
            error = "moduleIoNo must be a four-digit hexadecimal module I/O number without a prefix.";
            return false;
        }

        if (networkNo == 0 && stationNo != 255)
        {
            error = "Direct-station routing requires stationNo 255 (FFH).";
            return false;
        }
        if (networkNo != 0 && stationNo is > 120 or 255)
        {
            error = "Remote routing requires an ordinary stationNo from 1 to 120.";
            return false;
        }

        route = new MitsubishiMelsecRoute(
            checked((byte)networkNo), checked((byte)stationNo), checked((ushort)moduleIoNo), checked((byte)multidropStationNo));
        return true;
    }

    private static bool TryInt(IReadOnlyDictionary<string, string> settings, string key, int fallback,
        int minimum, int maximum, out int value)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = fallback;
            return value >= minimum && value <= maximum;
        }
        return int.TryParse(raw, System.Globalization.NumberStyles.None,
                   System.Globalization.CultureInfo.InvariantCulture, out value) &&
               value >= minimum && value <= maximum;
    }

    internal static string? Get(IReadOnlyDictionary<string, string> settings, string key)
    {
        if (settings.TryGetValue(key, out var exact)) return exact;
        foreach (var item in settings)
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) return item.Value;
        return null;
    }
}

public sealed record MitsubishiMelsecPoint(
    TagDefinition Tag,
    MitsubishiMelsecAddress Address,
    MitsubishiMelsecPhysicalType PhysicalType,
    bool Writable,
    TagPhysicalValueTransform Transform)
{
    public int WordSpan => PhysicalType switch
    {
        MitsubishiMelsecPhysicalType.Int16 or MitsubishiMelsecPhysicalType.UInt16 => 1,
        MitsubishiMelsecPhysicalType.Int32 or MitsubishiMelsecPhysicalType.UInt32 or MitsubishiMelsecPhysicalType.Float32 => 2,
        MitsubishiMelsecPhysicalType.Bit => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(PhysicalType))
    };

    public int BitSpan => PhysicalType == MitsubishiMelsecPhysicalType.Bit ? 1 : 0;
}

public sealed class MitsubishiMelsecProtocolException : IOException
{
    public MitsubishiMelsecProtocolException(
        string message,
        string failureKind,
        ushort? endCode = null,
        bool dispatchMayHaveOccurred = false,
        Exception? innerException = null) : base(message, innerException)
    {
        FailureKind = failureKind;
        EndCode = endCode;
        DispatchMayHaveOccurred = dispatchMayHaveOccurred;
    }

    public string FailureKind { get; }
    public ushort? EndCode { get; }
    public bool DispatchMayHaveOccurred { get; }
}

public sealed class MitsubishiMelsecWriteOutcomeUnknownException : IOException
{
    public MitsubishiMelsecWriteOutcomeUnknownException(string message, ushort? endCode = null, Exception? innerException = null)
        : base(message, innerException) => EndCode = endCode;

    public ushort? EndCode { get; }
}
