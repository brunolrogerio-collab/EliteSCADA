using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Modbus;

public sealed class ModbusTcpServerConnectionTester : ICommunicationDriverConnectionTester
{
    public CommunicationDriverTypeDescriptor Descriptor => ModbusTcpServerDriverDescriptorProvider.SharedDescriptor;

    public ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(context.DriverType, ModbusTcpServerDriverDescriptorProvider.DriverTypeId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Connection test expected Driver '{ModbusTcpServerDriverDescriptorProvider.DriverTypeId}'.", nameof(context));

        TcpListener? listener = null;
        try
        {
            var bind = Get(context.Settings, "bindAddress") ?? "0.0.0.0";
            var normalized = ModbusTcpServerDriver.NormalizeBindAddress(bind);
            var address = IPAddress.Parse(normalized);
            var port = ParseInt(context.Settings, "port", 502, 1, 65535);
            listener = new TcpListener(address, port);
            if (address.Equals(IPAddress.IPv6Any))
                listener.Server.DualMode = true;
            listener.Start(1);
            return ValueTask.FromResult(new DriverConnectionTestResult(
                true,
                FormatEndpoint(address, port),
                "tcp-listener-reservable",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["bindAddress"] = normalized,
                    ["port"] = port.ToString(CultureInfo.InvariantCulture),
                    ["releasedAfterTest"] = "true"
                }));
        }
        catch (Exception ex) when (ex is SocketException or UnauthorizedAccessException or ArgumentException)
        {
            return ValueTask.FromResult(new DriverConnectionTestResult(
                false,
                Get(context.Settings, "bindAddress"),
                null,
                Issues: new[]
                {
                    new DriverEngineeringIssue(
                        "MODBUS_TCP_SERVER_BIND_TEST_FAILED",
                        DriverEngineeringIssueSeverity.Error,
                        Sanitize(ex.Message),
                        "bindAddress")
                }));
        }
        finally
        {
            try { listener?.Stop(); } catch { }
        }
    }

    private static string FormatEndpoint(IPAddress address, int port) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]:{port}" : $"{address}:{port}";

    private static int ParseInt(IReadOnlyDictionary<string, string> settings, string key, int fallback, int minimum, int maximum)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum && parsed <= maximum)
            return parsed;
        throw new ArgumentException($"Setting '{key}' must be from {minimum} to {maximum}.");
    }

    private static string? Get(IReadOnlyDictionary<string, string> settings, string key)
    {
        if (settings.TryGetValue(key, out var exact)) return exact?.Trim();
        foreach (var pair in settings)
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                return pair.Value?.Trim();
        return null;
    }

    private static string Sanitize(string value)
    {
        var clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 512 ? clean : clean[..512];
    }
}

public sealed class ModbusRtuServerConnectionTester : ICommunicationDriverConnectionTester
{
    private readonly HostSerialBusCoordinator _coordinator;

    public ModbusRtuServerConnectionTester(HostSerialBusCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public CommunicationDriverTypeDescriptor Descriptor => ModbusRtuServerDriverDescriptorProvider.SharedDescriptor;

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(context.DriverType, ModbusRtuServerDriverDescriptorProvider.DriverTypeId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Connection test expected Driver '{ModbusRtuServerDriverDescriptorProvider.DriverTypeId}'.", nameof(context));

        try
        {
            var line = ModbusRtuPointReadTester.ParseLineSettings(context.Settings);
            await using var lease = await _coordinator.AcquireServerAsync(
                $"engineering:rtu-server-test:{context.DataSourceKey}:{Guid.NewGuid():N}",
                line,
                cancellationToken);
            return new DriverConnectionTestResult(
                true,
                line.PortName,
                line.PhysicalPortKey,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["baudRate"] = line.BaudRate.ToString(CultureInfo.InvariantCulture),
                    ["dataBits"] = line.DataBits.ToString(CultureInfo.InvariantCulture),
                    ["parity"] = line.Parity.ToString(),
                    ["stopBits"] = line.StopBits.ToString(),
                    ["exclusiveReservationReleasedAfterTest"] = "true"
                });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return new DriverConnectionTestResult(
                false,
                context.Settings.TryGetValue("serialPort", out var port) ? port : null,
                null,
                Issues: new[]
                {
                    new DriverEngineeringIssue(
                        "MODBUS_RTU_SERVER_OPEN_TEST_FAILED",
                        DriverEngineeringIssueSeverity.Error,
                        ex.Message.Replace('\r', ' ').Replace('\n', ' ').Trim(),
                        "serialPort")
                });
        }
    }
}
