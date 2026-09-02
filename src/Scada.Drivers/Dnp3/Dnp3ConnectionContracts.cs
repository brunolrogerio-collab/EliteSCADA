using System.Net;

namespace Scada.Drivers.Dnp3;

/// <summary>
/// Library-neutral TCP/link configuration for one DNP3 Master association.
/// The initial product target is one EliteSCADA Data Source per outstation.
/// </summary>
public sealed record Dnp3TcpConnectionOptions
{
    public const ushort MaxIndividualLinkAddress = 0xFFEF;

    public required string Host { get; init; }
    public int Port { get; init; } = 20000;
    public required ushort MasterAddress { get; init; }
    public required ushort OutstationAddress { get; init; }
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public string SanitizedEndpoint
    {
        get
        {
            var host = Host.Trim();
            return host.Contains(':') && !host.StartsWith('[')
                ? $"[{host}]:{Port}"
                : $"{host}:{Port}";
        }
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new ArgumentException("DNP3 TCP host is required.", nameof(Host));
        if (!Host.Equals(Host.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("DNP3 TCP host must not contain leading or trailing whitespace.", nameof(Host));
        if (Host.Contains('\r') || Host.Contains('\n') || Host.Contains('\0'))
            throw new ArgumentException("DNP3 TCP host contains invalid control characters.", nameof(Host));
        if (!IsPlainHostOrIp(Host))
            throw new ArgumentException("DNP3 TCP host must be a plain DNS hostname or IP address without scheme, credentials, path or embedded port.", nameof(Host));
        if (Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port), "DNP3 TCP port must be between 1 and 65535.");
        if (MasterAddress > MaxIndividualLinkAddress)
            throw new ArgumentOutOfRangeException(nameof(MasterAddress), "DNP3 master link address must be an individual station address (0..65519).");
        if (OutstationAddress > MaxIndividualLinkAddress)
            throw new ArgumentOutOfRangeException(nameof(OutstationAddress), "DNP3 outstation link address must be an individual station address (0..65519).");
        if (MasterAddress == OutstationAddress)
            throw new ArgumentException("DNP3 master and outstation link addresses must be different.", nameof(OutstationAddress));
        if (ConnectTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ConnectTimeout), "DNP3 connect timeout must be positive.");
    }

    private static bool IsPlainHostOrIp(string value)
    {
        if (IPAddress.TryParse(value, out _)) return true;
        return Uri.CheckHostName(value) == UriHostNameType.Dns;
    }
}

/// <summary>
/// EliteSCADA-owned factory seam used by DriverHost/Engineering composition.
/// Concrete protocol-library adapters remain behind this boundary and receive
/// no canonical Engineering objects.
/// </summary>
public interface IDnp3MasterSessionFactory
{
    IDnp3MasterSession Create(Dnp3TcpConnectionOptions connectionOptions);
}