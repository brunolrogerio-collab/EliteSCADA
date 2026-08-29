using System.Globalization;

namespace Scada.Drivers.SiemensS7Iso;

public enum S7CpuFamily
{
    Unknown,
    S7300,
    S7400,
    S71200,
    S71500
}

public enum S7IsoConnectionMode
{
    RackSlot,
    ExplicitTsap
}

public enum S7IsoConnectionRole : byte
{
    ProgrammingDevice = 0x01,
    OperatorPanel = 0x02,
    Basic = 0x03
}

public sealed record S7IsoConnectionOptions
{
    public S7IsoConnectionOptions(
        string host,
        S7CpuFamily cpuFamily,
        S7IsoConnectionMode connectionMode,
        byte? rack = null,
        byte? slot = null,
        S7IsoConnectionRole connectionRole = S7IsoConnectionRole.OperatorPanel,
        ushort sourceTsap = 0x0100,
        ushort? destinationTsap = null,
        int port = 102,
        TimeSpan? connectTimeout = null,
        TimeSpan? requestTimeout = null,
        TimeSpan? reconnectDelay = null,
        ushort requestedPduSize = 480,
        bool writeEnabled = false)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("S7 ISO host is required.", nameof(host));
        if (!Enum.IsDefined(cpuFamily))
            throw new ArgumentOutOfRangeException(nameof(cpuFamily));
        if (!Enum.IsDefined(connectionMode))
            throw new ArgumentOutOfRangeException(nameof(connectionMode));
        if (!Enum.IsDefined(connectionRole))
            throw new ArgumentOutOfRangeException(nameof(connectionRole));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        if (rack is > 7)
            throw new ArgumentOutOfRangeException(nameof(rack), "S7 rack must be from 0 to 7.");
        if (slot is > 31)
            throw new ArgumentOutOfRangeException(nameof(slot), "S7 slot must be from 0 to 31.");
        if (connectionMode == S7IsoConnectionMode.RackSlot && rack is null)
            throw new ArgumentException("Rack/Slot mode requires an explicit rack.", nameof(rack));
        if (connectionMode == S7IsoConnectionMode.RackSlot && slot is null)
            throw new ArgumentException("Rack/Slot mode requires an explicit slot.", nameof(slot));
        if (connectionMode == S7IsoConnectionMode.ExplicitTsap && destinationTsap is null)
            throw new ArgumentException("Explicit TSAP mode requires a destination TSAP.", nameof(destinationTsap));

        Host = host.Trim();
        CpuFamily = cpuFamily;
        ConnectionMode = connectionMode;
        Rack = rack;
        Slot = slot;
        ConnectionRole = connectionRole;
        SourceTsap = sourceTsap;
        DestinationTsap = destinationTsap;
        Port = port;
        ConnectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5);
        RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(3);
        ReconnectDelay = reconnectDelay ?? TimeSpan.FromSeconds(1);
        RequestedPduSize = requestedPduSize;
        WriteEnabled = writeEnabled;

        if (ConnectTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(connectTimeout), "S7 connect timeout must be positive.");
        if (RequestTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(requestTimeout), "S7 request timeout must be positive.");
        if (ReconnectDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(reconnectDelay), "S7 reconnect delay cannot be negative.");
        if (RequestedPduSize is < 240 or > 960)
            throw new ArgumentOutOfRangeException(nameof(requestedPduSize), "Requested S7 PDU size must be from 240 to 960 bytes.");
    }

    public string Host { get; }
    public S7CpuFamily CpuFamily { get; }
    public S7IsoConnectionMode ConnectionMode { get; }
    public byte? Rack { get; }
    public byte? Slot { get; }
    public S7IsoConnectionRole ConnectionRole { get; }
    public ushort SourceTsap { get; }
    public ushort? DestinationTsap { get; }
    public int Port { get; }
    public TimeSpan ConnectTimeout { get; }
    public TimeSpan RequestTimeout { get; }
    public TimeSpan ReconnectDelay { get; }
    public ushort RequestedPduSize { get; }
    public bool WriteEnabled { get; }

    public ushort EffectiveSourceTsap => SourceTsap;

    public ushort EffectiveDestinationTsap => ConnectionMode switch
    {
        S7IsoConnectionMode.RackSlot => checked((ushort)(((byte)ConnectionRole << 8) | (Rack!.Value * 32 + Slot!.Value))),
        S7IsoConnectionMode.ExplicitTsap => DestinationTsap!.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(ConnectionMode))
    };

    public string SanitizedEndpoint => $"{Host}:{Port}";

    public static string FormatTsap(ushort tsap) => $"0x{tsap:X4}";

    public static bool TryParseTsap(string? text, out ushort tsap)
    {
        tsap = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var value = text.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ushort.TryParse(value[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out tsap);

        var dot = value.IndexOf('.');
        if (dot > 0 && dot < value.Length - 1)
        {
            if (!byte.TryParse(value[..dot], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var high) ||
                !byte.TryParse(value[(dot + 1)..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var low))
                return false;
            tsap = (ushort)((high << 8) | low);
            return true;
        }

        if (value.Length == 4 &&
            ushort.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out tsap))
            return true;

        return ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out tsap);
    }
}
