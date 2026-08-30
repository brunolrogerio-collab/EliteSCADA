using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;

namespace Scada.Drivers.Tests;

public sealed class Dnp3Dnp3PyL2IntegrationTests
{
    [Fact]
    [Trait("Category", "Dnp3Dnp3PyIntegration")]
    public async Task Driver_StartupIntegrity_PopulatesBinaryAnalogAndCounterFromIndependentPeer()
    {
        if (!TryGetEndpoint(out var host, out var port)) return;

        var eventBus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(eventBus);
        var registry = new InMemoryTagRegistry();

        var binaryTag = TagDefinition.Create(
            "BI0",
            "DNP3.Dnp3Py.BI0",
            TagDataType.Boolean,
            source: "dnp3",
            readOnly: true);
        var analogTag = TagDefinition.Create(
            "AI0",
            "DNP3.Dnp3Py.AI0",
            TagDataType.Int32,
            source: "dnp3",
            readOnly: true);
        var counterTag = TagDefinition.Create(
            "CTR0",
            "DNP3.Dnp3Py.CTR0",
            TagDataType.Int64,
            source: "dnp3",
            readOnly: true);

        var points = new Dnp3Point[]
        {
            new(
                binaryTag,
                new Dnp3PointBinding(
                    Dnp3PointKind.BinaryInput,
                    0,
                    TagDataType.Boolean,
                    new Dnp3ObjectVariation(1, 2))),
            new(
                analogTag,
                new Dnp3PointBinding(
                    Dnp3PointKind.AnalogInput,
                    0,
                    TagDataType.Int32,
                    new Dnp3ObjectVariation(30, 1))),
            new(
                counterTag,
                new Dnp3PointBinding(
                    Dnp3PointKind.Counter,
                    0,
                    TagDataType.Int64,
                    new Dnp3ObjectVariation(20, 1)))
        };

        var session = new StepFunctionDnp3MasterSession(new Dnp3TcpConnectionOptions
        {
            Host = host,
            Port = port,
            MasterAddress = 1,
            OutstationAddress = 1024,
            ConnectTimeout = TimeSpan.FromSeconds(4)
        });

        await using var driver = new Dnp3Driver(
            "dnp3-dnp3py-l2",
            "DNP3 dnp3py L2",
            cache,
            registry,
            points,
            session,
            new Dnp3AssociationOptions
            {
                ResponseTimeout = TimeSpan.FromSeconds(3),
                ReconnectMinDelay = TimeSpan.FromMilliseconds(200),
                ReconnectMaxDelay = TimeSpan.FromSeconds(1),
                KeepAliveTimeout = TimeSpan.FromSeconds(5),
                IntegrityPollInterval = null,
                Class1PollInterval = null,
                Class2PollInterval = null,
                Class3PollInterval = null,
                EnableUnsolicitedClassesAfterIntegrity = Dnp3ClassSet.None
            });

        try
        {
            await driver.StartAsync();

            await WaitUntilAsync(
                () => session.State == Dnp3SessionState.Online,
                TimeSpan.FromSeconds(12),
                "DNP3 Step Function master did not reach Online against dnp3py.");

            await WaitUntilAsync(
                () => HasGoodValue(cache, binaryTag.Id, true) &&
                      HasGoodValue(cache, analogTag.Id, 4242) &&
                      HasGoodValue(cache, counterTag.Id, 123456L),
                TimeSpan.FromSeconds(12),
                "Startup integrity did not populate the independent dnp3py point set.");

            var diagnostics = driver.GetCommunicationDiagnostics();
            Assert.Equal("dnp3.master", diagnostics.DriverType);
            Assert.Equal($"{host}:{port}", diagnostics.Endpoint);
            Assert.True(diagnostics.Counters.Connections >= 1);
            Assert.True(diagnostics.Counters.ReadOperations >= 1);
            Assert.True(diagnostics.Counters.UpdatesPublished >= 3);
            Assert.Equal(0, diagnostics.Counters.FailedOperations);
            Assert.Equal(0, diagnostics.RejectedValues);
        }
        finally
        {
            try { await driver.StopAsync(); } catch { }
        }
    }

    private static bool HasGoodValue(CurrentTagCache cache, Guid tagId, object expected)
    {
        if (!cache.TryGet(tagId, out var value) || value is null || value.Quality != TagQuality.Good)
            return false;
        return Equals(value.Value, expected);
    }

    private static bool TryGetEndpoint(out string host, out int port)
    {
        host = Environment.GetEnvironmentVariable("ELITESCADA_DNP3PY_L2_HOST")?.Trim() ?? string.Empty;
        var rawPort = Environment.GetEnvironmentVariable("ELITESCADA_DNP3PY_L2_PORT")?.Trim();
        if (string.IsNullOrWhiteSpace(host) || !int.TryParse(rawPort, out port) || port is < 1 or > 65535)
        {
            port = 0;
            return false;
        }
        return true;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string message)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(50);
        }
        throw new TimeoutException(message);
    }
}
