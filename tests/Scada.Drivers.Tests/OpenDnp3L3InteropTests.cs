using System.ComponentModel;
using System.Diagnostics;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.OpenDnp3;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class OpenDnp3L3InteropTests
{
    [Fact]
    [Trait("Category", "L3OpenDnp3Integration")]
    public async Task OpenDnp3Runtime_AcquiresAndCommandsIndependentDnp3PyOutstation()
    {
        var host = Environment.GetEnvironmentVariable("ELITESCADA_L3_DNP3_HOST");
        var portText = Environment.GetEnvironmentVariable("ELITESCADA_L3_DNP3_PORT");
        var helperPath = Environment.GetEnvironmentVariable("ELITESCADA_DNP3_HOST_PATH");
        if (string.IsNullOrWhiteSpace(host) ||
            !int.TryParse(portText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var port) ||
            string.IsNullOrWhiteSpace(helperPath))
        {
            return;
        }

        Assert.True(File.Exists(helperPath), $"OpenDNP3 helper was not found at '{helperPath}'.");

        var analogId = Guid.NewGuid();
        var binaryOutputId = Guid.NewGuid();
        var dataSource = new DataSourceEngineeringDto(
            Guid.NewGuid(),
            "l3.opendnp3",
            "L3 OpenDNP3",
            Dnp3DriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["transport"] = "tcp",
                ["host"] = host,
                ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["masterAddress"] = "1",
                ["outstationAddress"] = "1024",
                ["connectTimeout"] = "00:00:05",
                ["responseTimeout"] = "00:00:03",
                ["reconnectMinDelay"] = "00:00:00.100",
                ["reconnectMaxDelay"] = "00:00:01",
                ["keepAliveTimeout"] = "00:00:05",
                ["integrityPollInterval"] = "00:00:02"
            });

        var analogBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            Dnp3DriverDescriptorProvider.ConfigurationSchemaId,
            1,
            "dnp3:analogInput:0",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["staticVariation"] = "G30V1"
            });
        var binaryOutputBinding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            Dnp3DriverDescriptorProvider.ConfigurationSchemaId,
            1,
            "dnp3:binaryOutputStatus:3",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["writable"] = "true",
                ["commandMode"] = "selectBeforeOperate",
                ["binaryTrueOperation"] = "latchOn",
                ["binaryFalseOperation"] = "latchOff"
            });

        var package = new EngineeringPackage(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [
                new TagEngineeringDto(
                    analogId,
                    "Analog0",
                    "L3.OpenDNP3.Analog0",
                    TagDataType.Int32,
                    Source: dataSource.Key,
                    Address: analogBinding.PortableAddress,
                    ReadOnly: true,
                    CommunicationBinding: analogBinding),
                new TagEngineeringDto(
                    binaryOutputId,
                    "BinaryOutput3",
                    "L3.OpenDNP3.BinaryOutput3",
                    TagDataType.Boolean,
                    Source: dataSource.Key,
                    Address: binaryOutputBinding.PortableAddress,
                    ReadOnly: false,
                    CommunicationBinding: binaryOutputBinding)
            ],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

        var bus = new InMemoryScadaEventBus();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            dnp3SessionFactory: new OpenDnp3MasterSessionFactory());
        await using var coordinator = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(20),
            communicationComponents: components);

        var activation = await coordinator.ActivateAsync("l3-opendnp3-dnp3py", 1, package);
        Assert.True(
            activation.Activated,
            string.Join(" | ", activation.CompilationIssues.Select(static issue => issue.Message)
                .Concat(activation.RuntimeIssues.Select(static issue => issue.Message))));

        var analog = await WaitForGoodValueAsync(
            coordinator,
            analogId,
            static value => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) == 4242,
            TimeSpan.FromSeconds(10));
        Assert.Equal(4242, Convert.ToInt32(analog.Value, System.Globalization.CultureInfo.InvariantCulture));

        var initialOutput = await WaitForGoodValueAsync(
            coordinator,
            binaryOutputId,
            static value => value is bool boolean && !boolean,
            TimeSpan.FromSeconds(10));
        Assert.False(Assert.IsType<bool>(initialOutput.Value));

        await coordinator.WriteAsync(binaryOutputId, true);

        var commandedOutput = await WaitForGoodValueAsync(
            coordinator,
            binaryOutputId,
            static value => value is true,
            TimeSpan.FromSeconds(10));
        Assert.True(Assert.IsType<bool>(commandedOutput.Value));

        var diagnostics = Assert.Single(coordinator.Describe().CommunicationDrivers);
        Assert.Equal(Dnp3DriverDescriptorProvider.DriverType, diagnostics.DriverType);
        Assert.True(diagnostics.Counters.Connections >= 1);
        Assert.True(diagnostics.Counters.WriteOperations >= 1);
    }

    [Fact]
    [Trait("Category", "L3OpenDnp3Integration")]
    public async Task OpenDnp3Session_PropagatesNegativeStatus_ExecutesG41V1ToV4_AndStopsCleanly()
    {
        var host = Environment.GetEnvironmentVariable("ELITESCADA_L3_DNP3_HOST");
        var portText = Environment.GetEnvironmentVariable("ELITESCADA_L3_DNP3_PORT");
        var helperPath = Environment.GetEnvironmentVariable("ELITESCADA_DNP3_HOST_PATH");
        if (string.IsNullOrWhiteSpace(host) ||
            !int.TryParse(portText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var port) ||
            string.IsNullOrWhiteSpace(helperPath))
        {
            return;
        }

        helperPath = Path.GetFullPath(helperPath);
        Assert.True(File.Exists(helperPath), $"OpenDNP3 helper was not found at '{helperPath}'.");
        var baselineHelperCount = CountHelperProcesses(helperPath);

        var factory = new OpenDnp3MasterSessionFactory();
        var session = factory.Create(new Dnp3TcpConnectionOptions
        {
            Host = host,
            Port = port,
            MasterAddress = 1,
            OutstationAddress = 1024,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        });
        var online = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            await session.StartAsync(
                new Dnp3AssociationOptions
                {
                    ResponseTimeout = TimeSpan.FromSeconds(3),
                    ReconnectMinDelay = TimeSpan.FromMilliseconds(100),
                    ReconnectMaxDelay = TimeSpan.FromSeconds(1),
                    KeepAliveTimeout = TimeSpan.FromSeconds(5),
                    IntegrityPollInterval = TimeSpan.FromSeconds(2)
                },
                static (_, _) => ValueTask.CompletedTask,
                (state, _) =>
                {
                    if (state == Dnp3SessionState.Online)
                        online.TrySetResult(true);
                    return ValueTask.CompletedTask;
                });

            await online.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(Dnp3SessionState.Online, session.State);
            await WaitForHelperCountAsync(helperPath, count => count >= baselineHelperCount + 1, TimeSpan.FromSeconds(5));

            var rejected = await session.ExecuteBinaryAsync(
                99,
                Dnp3BinaryOperation.LatchOn,
                new Dnp3BinaryCommandProfile { Mode = Dnp3CommandMode.SelectBeforeOperate });
            Assert.False(rejected.Succeeded);
            Assert.Contains("NOT_SUPPORTED", rejected.Status, StringComparison.OrdinalIgnoreCase);

            var analogCases = new (object Value, Dnp3AnalogOutputVariation Variation)[]
            {
                (123456, Dnp3AnalogOutputVariation.Int32),
                ((short)1234, Dnp3AnalogOutputVariation.Int16),
                (12.5f, Dnp3AnalogOutputVariation.Float32),
                (12.5d, Dnp3AnalogOutputVariation.Float64)
            };

            foreach (var analogCase in analogCases)
            {
                var result = await session.ExecuteAnalogAsync(
                    5,
                    analogCase.Value,
                    new Dnp3AnalogCommandProfile(Dnp3CommandMode.DirectOperate, analogCase.Variation));
                Assert.True(
                    result.Succeeded,
                    $"G41V{(byte)analogCase.Variation} failed with status '{result.Status}': {result.Message}");
            }

            var diagnostics = session.GetDiagnostics();
            Assert.True(diagnostics.FailedOperations >= 1);
            Assert.True(diagnostics.WriteOperations >= 5);

            await session.StopAsync();
            Assert.Equal(Dnp3SessionState.Stopped, session.State);
            await WaitForHelperCountAsync(helperPath, count => count <= baselineHelperCount, TimeSpan.FromSeconds(5));
        }
        finally
        {
            await session.DisposeAsync();
            await WaitForHelperCountAsync(helperPath, count => count <= baselineHelperCount, TimeSpan.FromSeconds(5));
        }
    }

    private static async Task<TagValue> WaitForGoodValueAsync(
        EngineeringRuntimeCoordinator coordinator,
        Guid tagId,
        Func<object?, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        TagValue? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (coordinator.TryGetCurrent(tagId, out var current) && current is not null)
            {
                last = current;
                if (current.Quality == TagQuality.Good && predicate(current.Value))
                    return current;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Timed out waiting for DNP3 TAG {tagId}. Last value='{last?.Value ?? "<none>"}', quality='{last?.Quality.ToString() ?? "<none>"}'.");
    }

    private static async Task WaitForHelperCountAsync(
        string helperPath,
        Func<int, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        var lastCount = CountHelperProcesses(helperPath);
        while (DateTimeOffset.UtcNow < deadline)
        {
            lastCount = CountHelperProcesses(helperPath);
            if (predicate(lastCount))
                return;
            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Timed out waiting for OpenDNP3 helper process count condition. Last count={lastCount}, helper='{helperPath}'.");
    }

    private static int CountHelperProcesses(string helperPath)
    {
        var expected = Path.GetFullPath(helperPath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var count = 0;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.HasExited)
                        continue;
                    var processPath = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(processPath) &&
                        string.Equals(Path.GetFullPath(processPath), expected, comparison))
                    {
                        count++;
                    }
                }
                catch (Win32Exception)
                {
                    // Some unrelated system processes may not expose module metadata to the test account.
                }
                catch (InvalidOperationException)
                {
                    // Process exited while being inspected.
                }
                catch (NotSupportedException)
                {
                    // Platform process metadata unavailable for an unrelated process.
                }
            }
        }

        return count;
    }
}
