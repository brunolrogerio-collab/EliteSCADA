using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Modbus;

/// <summary>
/// Verifies TCP reachability for a Modbus TCP Data Source without issuing a
/// Modbus function request. PointRead remains the separate protocol/address test.
/// </summary>
public sealed class ModbusTcpConnectionTester : ICommunicationDriverConnectionTester
{
    public const int MaximumTimeoutMilliseconds = 5000;

    public CommunicationDriverTypeDescriptor Descriptor => ModbusTcpDriverDescriptorProvider.SharedDescriptor;

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(context.DriverType, ModbusTcpDriverDescriptorProvider.DriverTypeId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Modbus TCP connection test received a different driver type.", nameof(context));

        string host;
        int port;
        byte unitId;
        try
        {
            host = Required(context.Settings, "host");
            port = ParsePort(context.Settings);
            unitId = ParseUnitId(context.Settings);
        }
        catch (ArgumentException ex)
        {
            return Failure(null, "MODBUS_TCP_CONNECTION_CONFIGURATION_INVALID", ex.Message, ex.ParamName);
        }

        var endpoint = FormatEndpoint(host, port);
        var stopwatch = Stopwatch.StartNew();
        await using var transport = new ModbusTcpTransport(host, port, TimeSpan.FromMilliseconds(MaximumTimeoutMilliseconds));
        try
        {
            var response = await transport.ReadDeviceIdentificationAsync(unitId, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return new DriverConnectionTestResult(
                true,
                endpoint,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["transport"] = "Modbus TCP",
                    ["protocolResponsive"] = "true",
                    ["deviceIdentificationSupported"] = "true",
                    ["deviceIdentificationObjectCount"] = response.ObjectCount.ToString(CultureInfo.InvariantCulture),
                    ["elapsedMilliseconds"] = stopwatch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ModbusProtocolException ex)
        {
            stopwatch.Stop();
            return new DriverConnectionTestResult(
                true,
                endpoint,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["transport"] = "Modbus TCP",
                    ["protocolResponsive"] = "true",
                    ["deviceIdentificationSupported"] = "false",
                    ["protocolExceptionCode"] = ex.ExceptionCode.ToString(CultureInfo.InvariantCulture),
                    ["elapsedMilliseconds"] = stopwatch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)
                },
                new[]
                {
                    new DriverEngineeringIssue(
                        "MODBUS_TCP_DEVICE_IDENTIFICATION_UNSUPPORTED",
                        DriverEngineeringIssueSeverity.Warning,
                        "The endpoint responded to Modbus, but does not support the read-only device-identification request.",
                        "host")
                });
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return Failure(endpoint, "MODBUS_TCP_CONNECTION_TIMEOUT", "Modbus TCP protocol request timed out.", "host");
        }
        catch (SocketException ex)
        {
            stopwatch.Stop();
            var code = ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => "MODBUS_TCP_CONNECTION_REFUSED",
                SocketError.HostNotFound or SocketError.NoData => "MODBUS_TCP_DNS_FAILURE",
                SocketError.NetworkUnreachable or SocketError.HostUnreachable => "MODBUS_TCP_UNREACHABLE",
                _ => "MODBUS_TCP_CONNECTION_FAILED"
            };
            var message = ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => "The remote TCP port refused the connection.",
                SocketError.HostNotFound or SocketError.NoData => "The configured host name could not be resolved.",
                SocketError.NetworkUnreachable or SocketError.HostUnreachable => "The configured host is not reachable from the server.",
                _ => "The server could not establish the TCP connection."
            };
            return Failure(endpoint, code, message, "host");
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            return Failure(endpoint, "MODBUS_TCP_CONNECTION_TIMEOUT", "Modbus TCP protocol request timed out.", "host");
        }
        catch (IOException)
        {
            stopwatch.Stop();
            return Failure(endpoint, "MODBUS_TCP_PROTOCOL_RESPONSE_INVALID", "The endpoint did not return a valid Modbus TCP response.", "host");
        }
    }

    private static DriverConnectionTestResult Failure(string? endpoint, string code, string message, string? fieldKey) =>
        new(false, endpoint, null, Issues: new[]
        {
            new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, message, fieldKey)
        });

    private static string Required(IReadOnlyDictionary<string, string> settings, string key)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException($"Required Modbus setting '{key}' is missing.", key);
        var host = raw.Trim();
        if (host.Length > 253 || host.Any(char.IsControl) || host.Any(char.IsWhiteSpace) || host.Contains('@') || host.Contains('/'))
            throw new ArgumentException("Host must be one DNS name or IP address.", key);
        return host;
    }

    private static int ParsePort(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue("port", out var raw) || string.IsNullOrWhiteSpace(raw)) return 502;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) && port is >= 1 and <= 65535)
            return port;
        throw new ArgumentException("Modbus TCP port must be from 1 to 65535.", "port");
    }

    private static byte ParseUnitId(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue("unitId", out var raw) || string.IsNullOrWhiteSpace(raw)) return 1;
        if (byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unitId)) return unitId;
        throw new ArgumentException("Modbus TCP Unit ID must be from 0 to 255.", "unitId");
    }

    private static string FormatEndpoint(string host, int port)
    {
        var safeHost = host.Trim('[', ']');
        if (safeHost.Contains(':', StringComparison.Ordinal)) safeHost = $"[{safeHost}]";
        return $"{safeHost}:{port}";
    }
}
