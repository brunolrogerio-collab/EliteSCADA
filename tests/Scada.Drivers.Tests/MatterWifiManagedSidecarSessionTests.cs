using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HostResources;

namespace Scada.Drivers.Tests;

public sealed class MatterWifiManagedSidecarSessionTests
{
    [Fact]
    public async Task Session_waits_for_authenticated_bridge_after_child_readiness_and_stops_cleanly()
    {
        var rpc = new MatterWifiRpcServer();
        var processFactory = new GatedProcessFactory(rpc);
        var definition = new ManagedSidecarDefinition(
            "matter-wifi-test",
            new ManagedSidecarArtifactIdentity("elitescada.matter.wifi", "1.0.0", "node-22.13.0-or-24-plus", 1),
            new ManagedSidecarCompatibilityMetadata("elitescada.matter.wifi", ["1.0.0"], "node-22.13.0-or-24-plus", 1, 1),
            new ManagedSidecarPackagingMetadata([ManagedSidecarPackageTargets.LinuxX64]),
            new ManagedSidecarConfiguration());
        var services = new CommunicationDriverRuntimeServices("test-project", new FakeCache(), new InMemoryTagRegistry());
        var resources = new HostResourceRegistry();
        var leases = new HostResourceLeaseCoordinator(resources);
        var supervisor = new ManagedSidecarSupervisor(definition, processFactory);
        var binding = new ManagedSidecarRuntimeBinding("Matter test", definition, services, resources, leases, supervisor);
        await using var session = new MatterWifiManagedSidecarSession(binding, rpc);

        var starting = session.StartAsync().AsTask();
        await processFactory.ProcessCreated.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(starting.IsCompleted);

        processFactory.AllowBridgeConnection();
        await starting.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(rpc.IsConnected);

        await session.StopAsync();
        Assert.False(rpc.IsConnected);
    }

    private sealed class GatedProcessFactory(MatterWifiRpcServer rpc) : IManagedSidecarProcessFactory
    {
        private readonly TaskCompletionSource<bool> _allowConnection = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IManagedSidecarProcess> ProcessCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void AllowBridgeConnection() => _allowConnection.TrySetResult(true);

        public ValueTask<IManagedSidecarProcess> StartAsync(ManagedSidecarDefinition definition, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var process = new FakeProcess(rpc, _allowConnection.Task);
            ProcessCreated.TrySetResult(process);
            return ValueTask.FromResult<IManagedSidecarProcess>(process);
        }
    }

    private sealed class FakeProcess : IManagedSidecarProcess
    {
        private readonly MatterWifiRpcServer _rpc;
        private readonly Task _allowConnection;
        private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TcpClient? _client;
        private int _stopped;

        public FakeProcess(MatterWifiRpcServer rpc, Task allowConnection)
        {
            _rpc = rpc;
            _allowConnection = allowConnection;
            _ = ConnectAsync();
        }

        public string ProcessIdentity => "fake-matter-sidecar";

        public ValueTask<ManagedSidecarReadyReport> WaitForReadyAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ManagedSidecarReadyReport(
                new ManagedSidecarArtifactIdentity("elitescada.matter.wifi", "1.0.0", "node-22.13.0-or-24-plus", 1)));

        public ValueTask<int> WaitForExitAsync(CancellationToken cancellationToken = default) =>
            new(_exit.Task.WaitAsync(cancellationToken));

        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
            _client?.Dispose();
            _exit.TrySetResult(0);
            await Task.CompletedTask;
        }

        private async Task ConnectAsync()
        {
            try
            {
                await _allowConnection;
                if (Volatile.Read(ref _stopped) != 0) return;
                var client = new TcpClient();
                _client = client;
                await client.ConnectAsync(IPAddress.Loopback, _rpc.Port);
                var stream = client.GetStream();
                var hello = JsonSerializer.Serialize(new { type = "hello", protocolVersion = 1, token = _rpc.Token }) + "\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(hello));
                var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var welcome = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
                if (welcome is null || !welcome.Contains("welcome", StringComparison.Ordinal))
                    throw new IOException("Fake bridge did not receive its welcome frame.");
            }
            catch
            {
                _client?.Dispose();
                _exit.TrySetResult(1);
            }
        }

        public ValueTask DisposeAsync() => StopAsync();
    }

    private sealed class FakeCache : ICurrentTagCache
    {
        public bool TryGet(Guid tagId, out TagValue? value) { value = null; return false; }
        public IReadOnlyCollection<TagValue> Snapshot() => Array.Empty<TagValue>();
        public ValueTask<TagValue?> UpdateAsync(TagDefinition tag, TagValue value, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<TagValue?>(null);
    }
}
