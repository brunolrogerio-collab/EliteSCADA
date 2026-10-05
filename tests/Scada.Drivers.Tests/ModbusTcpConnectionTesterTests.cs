using Scada.Drivers.Abstractions;
using Scada.Drivers.Modbus;
using Scada.Api.Engineering;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class ModbusTcpConnectionTesterTests
{
    [Fact]
    public async Task ConnectionTest_UsesReadOnlyModbusDeviceIdentification()
    {
        await using var server = new TestModbusTcpServer();
        server.Start();
        var tester = new ModbusTcpConnectionTester();

        var result = await tester.TestConnectionAsync(Context(server.Port));

        Assert.True(result.Succeeded);
        Assert.Equal($"127.0.0.1:{server.Port}", result.SanitizedEndpoint);
        Assert.Equal("true", result.ObservedProperties!["protocolResponsive"]);
        Assert.Equal("true", result.ObservedProperties["deviceIdentificationSupported"]);
        Assert.Equal((byte)0x2B, Assert.Single(server.Requests).Function);
    }

    [Fact]
    public async Task ConnectionTest_ClassifiesProtocolResponseWhenIdentificationIsUnsupported()
    {
        await using var server = new TestModbusTcpServer { RejectDeviceIdentification = true };
        server.Start();

        var result = await new ModbusTcpConnectionTester().TestConnectionAsync(Context(server.Port));

        Assert.True(result.Succeeded);
        Assert.Equal("false", result.ObservedProperties!["deviceIdentificationSupported"]);
        Assert.Equal("MODBUS_TCP_DEVICE_IDENTIFICATION_UNSUPPORTED", Assert.Single(result.Issues!).Code);
    }

    [Fact]
    public async Task ConnectionTest_BoundsSlowProtocolResponse()
    {
        await using var server = new TestModbusTcpServer { ResponseDelay = TimeSpan.FromSeconds(6) };
        server.Start();

        var result = await new ModbusTcpConnectionTester().TestConnectionAsync(Context(server.Port));

        Assert.False(result.Succeeded);
        Assert.Equal("MODBUS_TCP_CONNECTION_TIMEOUT", Assert.Single(result.Issues!).Code);
    }

    [Fact]
    public async Task ConnectionTest_PropagatesCallerCancellation()
    {
        await using var server = new TestModbusTcpServer { ResponseDelay = TimeSpan.FromSeconds(1) };
        server.Start();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new ModbusTcpConnectionTester().TestConnectionAsync(Context(server.Port), cancellation.Token));
    }

    [Fact]
    public async Task ConnectionTest_ClassifiesRefusedPortSeparatelyFromPointRead()
    {
        await using var server = new TestModbusTcpServer();
        server.Start();
        var port = server.Port;
        await server.StopAsync();

        var result = await new ModbusTcpConnectionTester().TestConnectionAsync(Context(port));

        Assert.False(result.Succeeded);
        Assert.Equal("MODBUS_TCP_CONNECTION_REFUSED", Assert.Single(result.Issues!).Code);
        Assert.Equal($"127.0.0.1:{port}", result.SanitizedEndpoint);
    }

    [Fact]
    public void DriverAdvertisesConnectionTestSeparatelyFromPointRead()
    {
        var capabilities = new ModbusTcpConnectionTester().Descriptor.EngineeringCapabilities;

        Assert.True(capabilities.HasFlag(DriverEngineeringCapabilities.ConnectionTest));
        Assert.True(capabilities.HasFlag(DriverEngineeringCapabilities.PointReadTest));
    }

    [Fact]
    public async Task DraftToolingFactoryRegistersConnectionAndPointReadAsDistinctCapabilities()
    {
        var source = new DataSourceEngineeringDto(
            null,
            "modbus-check",
            "Modbus check",
            ModbusTcpDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string> { ["host"] = "127.0.0.1" });
        await using var lease = await new ModbusEngineeringDriverToolProviderFactory()
            .CreateAsync("test-project", source);

        Assert.NotNull(lease.Registration.ConnectionTester);
        Assert.NotNull(lease.Registration.PointReadTester);
        Assert.NotSame(lease.Registration.ConnectionTester, lease.Registration.PointReadTester);
        Assert.True(lease.Registration.ConnectionTester.Descriptor.EngineeringCapabilities
            .HasFlag(DriverEngineeringCapabilities.ConnectionTest));
        Assert.True(lease.Registration.PointReadTester.Descriptor.EngineeringCapabilities
            .HasFlag(DriverEngineeringCapabilities.PointReadTest));
    }

    private static DriverEngineeringDataSourceContext Context(int port) => new(
        "modbus-check",
        "Modbus check",
        ModbusTcpDriverDescriptorProvider.DriverTypeId,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = "127.0.0.1",
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture)
        },
        new Dictionary<string, string>(StringComparer.Ordinal));
}
