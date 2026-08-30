using System.Diagnostics;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class AllenBradleyCipL2IntegrationTests
{
    [Fact]
    [Trait("Category", "CipL2Integration")]
    public async Task ProtocolClient_ReadsAndWritesAgainstIndependentControlLogixSimulator()
    {
        if (!TryGetEndpoint(out var host, out var port)) return;

        var options = CreateOptions(host, port);
        var myDint = new LogixSymbolReference(LogixTagScope.Controller, "MyDint", LogixNativeType.Dint);
        var myReal = new LogixSymbolReference(LogixTagScope.Controller, "MyReal", LogixNativeType.Real);
        var setPoint = new LogixSymbolReference(LogixTagScope.Controller, "SetPoint", LogixNativeType.Real);

        await using var client = new LogixEtherNetIpClient();
        await client.ConnectAsync(options);

        var initial = await client.ReadManyAsync([myDint, myReal, setPoint]);
        Assert.Equal(3, initial.Count);
        Assert.All(initial, static result => Assert.True(result.Succeeded, result.Message));
        Assert.Equal(42, Assert.IsType<int>(initial[0].NativeValue));
        Assert.InRange(Assert.IsType<float>(initial[1].NativeValue), 3.139f, 3.141f);
        Assert.InRange(Assert.IsType<float>(initial[2].NativeValue), 74.99f, 75.01f);

        await client.WriteAsync(setPoint, 81.25f);
        var readback = Assert.Single(await client.ReadManyAsync([setPoint]));
        Assert.True(readback.Succeeded, readback.Message);
        Assert.InRange(Assert.IsType<float>(readback.NativeValue), 81.24f, 81.26f);

        var diagnostics = client.GetDiagnostics();
        Assert.True(diagnostics.Connected);
        Assert.True(diagnostics.SuccessfulRequests > 0);
        Assert.Equal(0, diagnostics.FailedRequests);

        await client.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "CipL2Integration")]
    public async Task Driver_PollsAndWritesThroughCanonicalBoundaryAgainstIndependentSimulator()
    {
        if (!TryGetEndpoint(out var host, out var port)) return;

        var tag = TagDefinition.Create(
            "MyInt",
            "CIP.MyInt",
            TagDataType.Int16,
            source: "AB-L2",
            readOnly: false);
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "MyInt", LogixNativeType.Int);
        var binding = new LogixTagBinding(
            tag,
            reference,
            Writable: true,
            ExternalAccess: LogixExternalAccess.ReadWrite);

        var cache = new TestCurrentTagCache();
        var registry = new TestTagRegistry();
        await using var driver = new AllenBradleyLogixDriver(
            "AB-L2",
            "Independent CIP L2",
            CreateOptions(host, port),
            cache,
            registry,
            [binding]);

        await driver.StartAsync();
        var initial = await WaitForValueAsync(
            cache,
            tag.Id,
            static value => value.Quality == TagQuality.Good && value.Value is short,
            TimeSpan.FromSeconds(12));
        Assert.Equal((short)1234, Assert.IsType<short>(initial.Value));

        await driver.WriteAsync(tag.Id, (short)2222);
        var written = await WaitForValueAsync(
            cache,
            tag.Id,
            static value => value.Quality == TagQuality.Good && value.Value is short current && current == 2222,
            TimeSpan.FromSeconds(5));
        Assert.Equal((short)2222, Assert.IsType<short>(written.Value));

        var runtimeDiagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal("rockwell.logix.eip", runtimeDiagnostics.DriverType);
        Assert.Equal(1, runtimeDiagnostics.AssociatedTagCount);
        Assert.True(runtimeDiagnostics.Counters.ReadOperations > 0);
        Assert.True(runtimeDiagnostics.Counters.WriteOperations > 0);

        await driver.StopAsync();

        await using var verifier = new LogixEtherNetIpClient();
        await verifier.ConnectAsync(CreateOptions(host, port));
        var readback = Assert.Single(await verifier.ReadManyAsync([reference]));
        Assert.True(readback.Succeeded, readback.Message);
        Assert.Equal((short)2222, Assert.IsType<short>(readback.NativeValue));
        await verifier.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "CipL2Integration")]
    public async Task Driver_DetectsPeerOutageAndReconnectsAfterRealProcessRestart()
    {
        if (!TryGetEndpoint(out var host, out var port)) return;
        var container = Environment.GetEnvironmentVariable("ELITESCADA_CIP_L2_RESTART_CONTAINER")?.Trim();
        if (string.IsNullOrWhiteSpace(container)) return;

        var tag = TagDefinition.Create(
            "StatusWord",
            "CIP.StatusWord",
            TagDataType.Int32,
            source: "AB-L2-RECONNECT",
            readOnly: true);
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "StatusWord", LogixNativeType.Dint);
        var binding = new LogixTagBinding(
            tag,
            reference,
            Writable: false,
            ExternalAccess: LogixExternalAccess.ReadOnly);

        var cache = new TestCurrentTagCache();
        var registry = new TestTagRegistry();
        await using var driver = new AllenBradleyLogixDriver(
            "AB-L2-RECONNECT",
            "Independent CIP L2 reconnect",
            CreateOptions(host, port),
            cache,
            registry,
            [binding]);

        await driver.StartAsync();
        var initial = await WaitForValueAsync(
            cache,
            tag.Id,
            static value => value.Quality == TagQuality.Good && value.Value is int current && current == 65290,
            TimeSpan.FromSeconds(12));
        Assert.Equal(65290, Assert.IsType<int>(initial.Value));

        var before = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Healthy, before.State);

        TagValue? failed = null;
        CommunicationDriverDiagnosticSnapshot? outage = null;
        await RunDockerAsync("stop", "-t", "1", container);
        try
        {
            failed = await WaitForValueAsync(
                cache,
                tag.Id,
                static value => value.Quality == TagQuality.BadCommunication,
                TimeSpan.FromSeconds(12));
            outage = driver.GetCommunicationDiagnostics();
        }
        finally
        {
            await RunDockerAsync("start", container);
        }

        Assert.NotNull(failed);
        Assert.Equal(TagQuality.BadCommunication, failed.Quality);
        Assert.NotNull(outage);
        Assert.True(outage.LastFailedCommunicationAt.HasValue);
        Assert.Equal(1, outage.TagQuality.BadCommunication);

        var recovered = await WaitForValueAsync(
            cache,
            tag.Id,
            static value => value.Quality == TagQuality.Good && value.Value is int current && current == 65290,
            TimeSpan.FromSeconds(20));
        Assert.Equal(65290, Assert.IsType<int>(recovered.Value));

        var after = await WaitForDiagnosticsAsync(
            driver,
            diagnostics => diagnostics.State == CommunicationDriverOperationalState.Healthy &&
                           diagnostics.Counters.Connections > before.Counters.Connections &&
                           diagnostics.Counters.Reconnects > before.Counters.Reconnects,
            TimeSpan.FromSeconds(10));
        Assert.True(after.Counters.Disconnections > before.Counters.Disconnections);
        Assert.True(after.LastSuccessfulCommunicationAt > before.LastSuccessfulCommunicationAt);

        await driver.StopAsync();
    }

    private static AllenBradleyLogixOptions CreateOptions(string host, int port) =>
        new(
            host,
            port,
            LogixControllerProfile.ControlLogix,
            ScanInterval: TimeSpan.FromMilliseconds(150),
            RequestTimeout: TimeSpan.FromSeconds(3),
            ReconnectMinimum: TimeSpan.FromMilliseconds(100),
            ReconnectMaximum: TimeSpan.FromSeconds(1),
            MaxBatchSize: 1);

    private static bool TryGetEndpoint(out string host, out int port)
    {
        host = Environment.GetEnvironmentVariable("ELITESCADA_CIP_L2_HOST")?.Trim() ?? string.Empty;
        var rawPort = Environment.GetEnvironmentVariable("ELITESCADA_CIP_L2_PORT")?.Trim();
        if (string.IsNullOrWhiteSpace(host) || !int.TryParse(rawPort, out port) || port is < 1 or > 65535)
        {
            port = 0;
            return false;
        }

        return true;
    }

    private static async Task RunDockerAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start docker for CIP L2 restart orchestration.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"docker {string.Join(' ', arguments)} failed with exit code {process.ExitCode}: {stderr.Trim()} {stdout.Trim()}".Trim());
    }

    private static async Task<TagValue> WaitForValueAsync(
        TestCurrentTagCache cache,
        Guid tagId,
        Func<TagValue, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (cache.TryGet(tagId, out var value) && value is not null && predicate(value))
                return value;
            await Task.Delay(50);
        }

        throw new TimeoutException($"Timed out waiting for TAG '{tagId}' to reach the expected CIP L2 state.");
    }

    private static async Task<CommunicationDriverDiagnosticSnapshot> WaitForDiagnosticsAsync(
        AllenBradleyLogixDriver driver,
        Func<CommunicationDriverDiagnosticSnapshot, bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var diagnostics = driver.GetCommunicationDiagnostics();
            if (predicate(diagnostics)) return diagnostics;
            await Task.Delay(50);
        }

        throw new TimeoutException("Timed out waiting for Driver 5 diagnostics to reach the expected reconnect state.");
    }

    private sealed class TestCurrentTagCache : ICurrentTagCache
    {
        private readonly Dictionary<Guid, TagValue> _values = new();
        private readonly object _gate = new();

        public bool TryGet(Guid tagId, out TagValue? value)
        {
            lock (_gate)
            {
                var found = _values.TryGetValue(tagId, out var current);
                value = current;
                return found;
            }
        }

        public IReadOnlyCollection<TagValue> Snapshot()
        {
            lock (_gate) return _values.Values.ToArray();
        }

        public ValueTask<TagValue?> UpdateAsync(TagDefinition tag, TagValue value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _values.TryGetValue(tag.Id, out var previous);
                _values[tag.Id] = value;
                return ValueTask.FromResult<TagValue?>(previous);
            }
        }
    }

    private sealed class TestTagRegistry : ITagRegistry
    {
        private readonly Dictionary<Guid, TagDefinition> _tags = new();

        public TagDefinition Register(TagDefinition tag)
        {
            if (!_tags.TryAdd(tag.Id, tag)) throw new InvalidOperationException("TAG already registered.");
            return tag;
        }

        public TagDefinition Upsert(TagDefinition tag)
        {
            _tags[tag.Id] = tag;
            return tag;
        }

        public bool TryGet(Guid tagId, out TagDefinition? tag)
        {
            var found = _tags.TryGetValue(tagId, out var current);
            tag = current;
            return found;
        }

        public bool TryGetByPath(string path, out TagDefinition? tag)
        {
            tag = _tags.Values.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
            return tag is not null;
        }

        public IReadOnlyCollection<TagDefinition> Snapshot() => _tags.Values.ToArray();
    }
}
