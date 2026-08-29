using System.Runtime.CompilerServices;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104EngineeringConnectionTesterTests
{
    [Fact]
    public async Task ValidConfiguration_PerformsFullHandshakeAndReturnsSanitizedEvidence()
    {
        var adapter = new FakeAdapter();
        var tester = new Iec104EngineeringConnectionTester(() => adapter);
        var context = CreateContext();

        var result = await tester.TestConnectionAsync(context);

        Assert.True(result.Succeeded);
        Assert.Equal("192.0.2.10:2404", result.SanitizedEndpoint);
        Assert.Equal("IEC-104 192.0.2.10:2404", result.ObservedIdentity);
        Assert.Equal("true", result.ObservedProperties?["tcpConnected"]);
        Assert.Equal("true", result.ObservedProperties?["startDtConfirmed"]);
        Assert.Equal("true", result.ObservedProperties?["stopDtConfirmed"]);
        Assert.Equal(1, adapter.ConnectCount);
        Assert.Equal(1, adapter.StartCount);
        Assert.Equal(1, adapter.StopCount);
        Assert.Equal(1, adapter.DisconnectCount);
        Assert.Equal(DriverEngineeringCapabilities.ConnectionTest, tester.Descriptor.EngineeringCapabilities);
        Assert.Empty(tester.Descriptor.ConfigurationSchema.TagBindingFields);
        Assert.Equal(Iec104EngineeringConnectionTester.DriverType, tester.Descriptor.DriverType);
    }

    [Fact]
    public async Task InvalidApciConfiguration_IsRejectedBeforeAdapterCreation()
    {
        var factoryCalls = 0;
        var tester = new Iec104EngineeringConnectionTester(() =>
        {
            factoryCalls++;
            return new FakeAdapter();
        });
        var settings = CreateContext().Settings.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);
        settings["t1Seconds"] = "5";
        settings["t2Seconds"] = "5";
        var context = CreateContext(settings);

        var result = await tester.TestConnectionAsync(context);

        Assert.False(result.Succeeded);
        Assert.Equal(0, factoryCalls);
        Assert.Contains(result.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue =>
            issue.Code == "iec104.config.apci" && issue.Severity == DriverEngineeringIssueSeverity.Error);
    }

    [Fact]
    public async Task MissingIdentityInputs_AreActionableAndDoNotOpenSocket()
    {
        var factoryCalls = 0;
        var tester = new Iec104EngineeringConnectionTester(() =>
        {
            factoryCalls++;
            return new FakeAdapter();
        });
        var context = new DriverEngineeringDataSourceContext(
            "ds-1",
            "Station",
            Iec104EngineeringConnectionTester.DriverType,
            new Dictionary<string, string>
            {
                ["host"] = " ",
                ["commonAddresses"] = "not-a-number",
                ["stationTimeZone"] = "definitely/not/a/time-zone"
            },
            new Dictionary<string, string>());

        var result = await tester.TestConnectionAsync(context);

        Assert.False(result.Succeeded);
        Assert.Equal(0, factoryCalls);
        Assert.Contains(result.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue => issue.FieldKey == "host");
        Assert.Contains(result.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue => issue.FieldKey == "commonAddresses");
        Assert.Contains(result.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue => issue.FieldKey == "stationTimeZone");
    }

    [Fact]
    public async Task TransportFailure_IsReturnedWithoutMultilineDiagnosticLeak()
    {
        var adapter = new FakeAdapter
        {
            ConnectFailure = new IOException("first line\nsecond line\r\nthird line")
        };
        var tester = new Iec104EngineeringConnectionTester(() => adapter);

        var result = await tester.TestConnectionAsync(CreateContext());

        Assert.False(result.Succeeded);
        var issue = Assert.Single(result.Issues ?? Array.Empty<DriverEngineeringIssue>());
        Assert.Equal("iec104.connection.failed", issue.Code);
        Assert.Equal("first line second line  third line", issue.Message);
        Assert.DoesNotContain("\n", issue.Message);
        Assert.DoesNotContain("\r", issue.Message);
    }

    private static DriverEngineeringDataSourceContext CreateContext(
        IReadOnlyDictionary<string, string>? settings = null) =>
        new(
            "ds-iec104",
            "Remote station",
            Iec104EngineeringConnectionTester.DriverType,
            settings ?? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["host"] = "192.0.2.10",
                ["port"] = "2404",
                ["commonAddresses"] = "2,1,2",
                ["stationTimeZone"] = "UTC",
                ["originatorAddress"] = "0",
                ["t0Seconds"] = "30",
                ["t1Seconds"] = "15",
                ["t2Seconds"] = "10",
                ["t3Seconds"] = "20",
                ["k"] = "12",
                ["w"] = "8"
            },
            new Dictionary<string, string>());

    private sealed class FakeAdapter : IIec104ClientAdapter
    {
        public bool IsConnected { get; private set; }
        public int ConnectCount { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisconnectCount { get; private set; }
        public Exception? ConnectFailure { get; init; }

        public Task ConnectAsync(string host, int port, Iec104SessionOptions options, CancellationToken cancellationToken = default)
        {
            ConnectCount++;
            if (ConnectFailure is not null)
                return Task.FromException(ConnectFailure);
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task StartDataTransferAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopDataTransferAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public ValueTask SendAsync(Iec104AsduEnvelope asdu, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public async IAsyncEnumerable<Iec104AsduEnvelope> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            DisconnectCount++;
            IsConnected = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
