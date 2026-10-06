using System.Text;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HostResources;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Tests;

public sealed class ManagedSidecarCheckpoint3Tests
{
    [Fact]
    public async Task SystemProcess_RealSpawnNegotiatesIdentitySanitizesOutputAndStopsGracefully()
    {
        if (!OperatingSystem.IsLinux()) return;

        const string secret = "fixture-secret-value";
        var definition = Definition(withProtectedSetting: false);
        var factory = new SystemManagedSidecarProcessFactory(
            (sidecar, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var script =
                    "printf '%s\\n' '{\"type\":\"ready\",\"artifactId\":\"elite.test-sidecar\",\"artifactVersion\":\"1.0.0\",\"runtimeVersion\":\"runtime-1\",\"schemaVersion\":3,\"message\":\"fixture ready\"}'; " +
                    "printf '%s\\n' \"stdout-secret=$SIDE_SECRET\"; " +
                    "printf '%s\\n' \"stderr-secret=$SIDE_SECRET\" >&2; " +
                    "while IFS= read -r line; do [ \"$line\" = \"STOP\" ] && exit 0; done";

                return ValueTask.FromResult(new ManagedSidecarProcessStartSpec(
                    "/bin/sh",
                    new[] { "-c", script },
                    Environment: new Dictionary<string, string>
                    {
                        ["SIDE_SECRET"] = secret
                    },
                    SensitiveValues: new[] { secret },
                    GracefulStopInput: "STOP"));
            });

        await using var process = await factory.StartAsync(definition);
        var ready = await process.WaitForReadyAsync();

        Assert.Equal(definition.ExpectedArtifact, ready.Artifact);
        Assert.Equal("fixture ready", ready.SanitizedMessage);

        var output = Assert.IsAssignableFrom<IManagedSidecarProcessOutput>(process);
        await EventuallyAsync(() => output.SanitizedOutput.Count >= 2);

        var joined = string.Join("|", output.SanitizedOutput);
        Assert.DoesNotContain(secret, joined, StringComparison.Ordinal);
        Assert.Contains("***", joined, StringComparison.Ordinal);

        await process.StopAsync();
        Assert.Equal(0, await process.WaitForExitAsync());
    }

    [Fact]
    public async Task SystemProcess_ForcedCrashRestartsThroughSupervisor()
    {
        if (!OperatingSystem.IsLinux()) return;

        var starts = 0;
        var definition = Definition(withProtectedSetting: false);
        var factory = new SystemManagedSidecarProcessFactory(
            (sidecar, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var start = Interlocked.Increment(ref starts);
                var ready =
                    "printf '%s\\n' '{\"type\":\"ready\",\"artifactId\":\"elite.test-sidecar\",\"artifactVersion\":\"1.0.0\",\"runtimeVersion\":\"runtime-1\",\"schemaVersion\":3,\"message\":\"ready\"}'; ";
                var script = start == 1
                    ? ready + "sleep 0.05; exit 23"
                    : ready + "while IFS= read -r line; do [ \"$line\" = \"STOP\" ] && exit 0; done";

                return ValueTask.FromResult(new ManagedSidecarProcessStartSpec(
                    "/bin/sh",
                    new[] { "-c", script },
                    GracefulStopInput: "STOP"));
            });

        await using var supervisor = new ManagedSidecarSupervisor(
            definition,
            factory,
            FastPolicy(),
            new SystemManagedSidecarDelay());

        await supervisor.StartAsync();

        await EventuallyAsync(() =>
            starts >= 2 &&
            supervisor.Snapshot.RestartCount >= 1 &&
            supervisor.Snapshot.State == ManagedSidecarLifecycleState.Ready,
            attempts: 300);

        Assert.Equal(23, supervisor.Snapshot.LastExitCode);
        Assert.True(supervisor.Snapshot.RestartCount >= 1);

        await supervisor.StopAsync();
        Assert.Equal(ManagedSidecarLifecycleState.Stopped, supervisor.Snapshot.State);
    }

    [Fact]
    public async Task ProtectedMaterialFactory_ReusesExistingHostResolverAndDisposesLease()
    {
        var resolver = new CaptureResolver("resolved-secret");
        var services = Services(effectAuthority: true, resolver);
        var inner = new CaptureResolvedProcessFactory(Artifact());
        var factory = new ManagedSidecarProtectedMaterialProcessFactory(services, inner);

        await using var process = await factory.StartAsync(Definition(withProtectedSetting: true));

        var request = Assert.Single(resolver.Requests);
        Assert.Equal("project-home", request.ProjectKey);
        Assert.Equal("sidecar-a", request.DataSourceKey);
        Assert.Equal("Integration", request.DriverType);
        Assert.Equal("ConnectionCredential", request.Purpose);
        Assert.Equal("protected-material-v1:fixture", request.Reference);
        Assert.Equal("resolved-secret", inner.LastMaterial);
        Assert.True(resolver.LastLease!.Disposed);

        await process.StopAsync();
    }

    [Fact]
    public async Task RuntimeBinding_NonAuthoritativeRuntimeCannotAcquireExternalResource()
    {
        var registry = new HostResourceRegistry();
        var resource = registry.Observe(ZWaveController("COM3"));
        var leases = new HostResourceLeaseCoordinator(registry);
        var services = Services(effectAuthority: false);
        var process = new FakeProcess(Artifact());
        await using var supervisor = new ManagedSidecarSupervisor(
            Definition(withProtectedSetting: false),
            new QueueProcessFactory(process),
            FastPolicy(),
            new ImmediateDelay());
        await using var binding = new ManagedSidecarRuntimeBinding(
            "Managed sidecar",
            Definition(withProtectedSetting: false),
            services,
            registry,
            leases,
            supervisor);

        var error = await Assert.ThrowsAsync<HostResourceAuthorityDeniedException>(async () =>
            await binding.StartAsync());

        Assert.Equal(resource.ResourceId, error.ResourceId);
        Assert.False(process.StartObserved);
    }

    [Fact]
    public async Task RuntimeBinding_ActiveOwnsResourceProjectsCommonDiagnosticsAndReacquiresAfterRestart()
    {
        var registry = new HostResourceRegistry();
        var resource = registry.Observe(ZWaveController("COM3"));
        var leases = new HostResourceLeaseCoordinator(registry);
        var services = Services(effectAuthority: true);

        var firstProcess = new FakeProcess(Artifact());
        await using (var firstSupervisor = new ManagedSidecarSupervisor(
                         Definition(withProtectedSetting: false),
                         new QueueProcessFactory(firstProcess),
                         FastPolicy(),
                         new ImmediateDelay()))
        await using (var firstBinding = new ManagedSidecarRuntimeBinding(
                         "Managed sidecar",
                         Definition(withProtectedSetting: false),
                         services,
                         registry,
                         leases,
                         firstSupervisor))
        {
            await firstBinding.StartAsync();

            var diagnostics = firstBinding.GetCommunicationDiagnostics();
            Assert.Equal(ManagedSidecarRuntimeBinding.DiagnosticsDriverType, diagnostics.DriverType);
            Assert.Equal(CommunicationDriverOperationalState.Healthy, diagnostics.State);
            Assert.Equal("zwave-controller-main", diagnostics.ProtocolDetails!["resource.id"]);
            Assert.Equal("zwave-controller", diagnostics.ProtocolDetails["resource.kind"]);
            Assert.Equal("Available", diagnostics.ProtocolDetails["resource.availability"]);
            Assert.Equal("local-only", diagnostics.ProtocolDetails["sidecar.controlPlane"]);
            Assert.Null(diagnostics.LastError);

            Assert.Throws<InvalidOperationException>(() =>
                leases.Acquire(resource.ResourceId, "second-owner", () => true));

            await firstBinding.StopAsync();
        }

        var secondProcess = new FakeProcess(Artifact());
        await using var secondSupervisor = new ManagedSidecarSupervisor(
            Definition(withProtectedSetting: false),
            new QueueProcessFactory(secondProcess),
            FastPolicy(),
            new ImmediateDelay());
        await using var secondBinding = new ManagedSidecarRuntimeBinding(
            "Managed sidecar restarted",
            Definition(withProtectedSetting: false),
            services,
            registry,
            leases,
            secondSupervisor);

        await secondBinding.StartAsync();

        Assert.True(secondProcess.StartObserved);
        using var deniedWhileRunning = Assert.Throws<InvalidOperationException>(() =>
            leases.Acquire(resource.ResourceId, "third-owner", () => true));
    }

    [Fact]
    public void ZigbeeCoordinatorFixture_RepresentsSerialAndTcpIdentityWithoutProtocolStack()
    {
        var serial = ZigbeeCoordinatorHostResource.Create(
            "zigbee-coordinator-main",
            HostResourceLocator.FromSerialPort(new HostSerialLineSettings("/dev/ttyUSB0")),
            new HostResourcePhysicalIdentity(
                "usb:zigbee:0001",
                new Dictionary<string, string>
                {
                    ["usbSerial"] = "0001",
                    ["vidPid"] = "10C4:EA60"
                }),
            adapterFamily: "ember",
            model: "fixture-serial",
            firmware: "1.2.3",
            networkIdentity: "pan:1a2b/extpan:00124b0000000001");

        var tcp = ZigbeeCoordinatorHostResource.Create(
            "zigbee-coordinator-tcp",
            new HostResourceLocator(
                HostResourceLocatorKind.TcpEndpoint,
                "coordinator.local:6638",
                "coordinator.local:6638"),
            new HostResourcePhysicalIdentity(
                "tcp-device:zigbee:alpha",
                new Dictionary<string, string>
                {
                    ["certificateFingerprint"] = "fixture-fingerprint"
                }),
            adapterFamily: "ezsp-network",
            model: "fixture-tcp",
            firmware: "4.5.6",
            networkIdentity: "pan:2b3c/extpan:00124b0000000002");

        Assert.Equal(HostResourceKinds.ZigbeeCoordinator, serial.ResourceKind);
        Assert.Equal(HostResourceLocatorKind.SerialPort, serial.Locator.Kind);
        Assert.Equal("ember", serial.Family);
        Assert.Equal(
            "pan:1a2b/extpan:00124b0000000001",
            serial.Metadata![ZigbeeCoordinatorHostResource.NetworkIdentityMetadataKey]);

        Assert.Equal(HostResourceKinds.ZigbeeCoordinator, tcp.ResourceKind);
        Assert.Equal(HostResourceLocatorKind.TcpEndpoint, tcp.Locator.Kind);
        Assert.Equal("ezsp-network", tcp.Family);
        Assert.Equal("tcp-device:zigbee:alpha", tcp.PhysicalIdentity.StableKey);
    }

    private static ManagedSidecarArtifactIdentity Artifact() =>
        new("elite.test-sidecar", "1.0.0", "runtime-1", 3);

    private static ManagedSidecarDefinition Definition(bool withProtectedSetting)
    {
        IReadOnlyCollection<ManagedSidecarProtectedSettingReference>? protectedSettings =
            withProtectedSetting
                ? new[]
                {
                    new ManagedSidecarProtectedSettingReference(
                        "credential",
                        "Integration",
                        "sidecar-a",
                        "ConnectionCredential",
                        "protected-material-v1:fixture")
                }
                : null;

        return new ManagedSidecarDefinition(
            "sidecar-a",
            Artifact(),
            new ManagedSidecarCompatibilityMetadata(
                "elite.test-sidecar",
                new[] { "1.0.0" },
                "runtime-1",
                MinimumSchemaVersion: 3,
                MaximumSchemaVersion: 3),
            new ManagedSidecarPackagingMetadata(
                new[]
                {
                    ManagedSidecarPackageTargets.WindowsX64,
                    ManagedSidecarPackageTargets.LinuxX64,
                    ManagedSidecarPackageTargets.LinuxArm64
                }),
            new ManagedSidecarConfiguration(
                new Dictionary<string, string>
                {
                    ["mode"] = "fixture"
                },
                protectedSettings),
            new ManagedSidecarPersistentStateOwnership(
                "sidecar-a-state",
                3,
                "sidecar-a-backup",
                new HostResourceId("zwave-controller-main")));
    }

    private static ManagedSidecarRestartPolicy FastPolicy() =>
        new(
            ReadinessTimeout: TimeSpan.FromSeconds(2),
            GracefulStopTimeout: TimeSpan.FromSeconds(2),
            InitialRestartDelay: TimeSpan.FromMilliseconds(10),
            MaximumRestartDelay: TimeSpan.FromMilliseconds(50),
            CrashLoopWindow: TimeSpan.FromMinutes(1),
            CrashLoopThreshold: 4);

    private static CommunicationDriverRuntimeServices Services(
        bool effectAuthority,
        ICommunicationDriverProtectedMaterialResolver? resolver = null) =>
        new(
            "project-home",
            new CurrentTagCache(new InMemoryScadaEventBus()),
            new InMemoryTagRegistry(),
            resolver,
            EffectAuthority: () => effectAuthority);

    private static HostResourceDescriptor ZWaveController(string locator) =>
        ZWaveControllerHostResource.Create(
            "zwave-controller-main",
            new HostSerialLineSettings(locator),
            new HostResourcePhysicalIdentity("usb:zwave:0001"),
            family: "700-series",
            model: "fixture",
            firmware: "7.19.3",
            rfRegion: "EU",
            nvmIdentity: "homeid:0xA1B2C3D4");

    private static async Task EventuallyAsync(
        Func<bool> predicate,
        int attempts = 100)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (predicate()) return;
            await Task.Delay(10);
        }

        Assert.True(predicate(), "Condition did not become true.");
    }

    private sealed class ImmediateDelay : IManagedSidecarDelay
    {
        public ValueTask DelayAsync(
            TimeSpan delay,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class QueueProcessFactory(params FakeProcess[] processes)
        : IManagedSidecarProcessFactory
    {
        private readonly Queue<FakeProcess> _processes = new(processes);

        public ValueTask<IManagedSidecarProcess> StartAsync(
            ManagedSidecarDefinition definition,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_processes.Count == 0)
                throw new InvalidOperationException("No fake process remains.");

            var process = _processes.Dequeue();
            process.StartObserved = true;
            return ValueTask.FromResult<IManagedSidecarProcess>(process);
        }
    }

    private sealed class CaptureResolvedProcessFactory(
        ManagedSidecarArtifactIdentity artifact)
        : IManagedSidecarResolvedProcessFactory
    {
        public string? LastMaterial { get; private set; }

        public ValueTask<IManagedSidecarProcess> StartAsync(
            ManagedSidecarDefinition definition,
            IReadOnlyDictionary<string, ReadOnlyMemory<byte>> protectedSettings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMaterial = Encoding.UTF8.GetString(protectedSettings["credential"].Span);
            return ValueTask.FromResult<IManagedSidecarProcess>(new FakeProcess(artifact)
            {
                StartObserved = true
            });
        }
    }

    private sealed class FakeProcess(
        ManagedSidecarArtifactIdentity artifact)
        : IManagedSidecarProcess
    {
        private readonly TaskCompletionSource<int> _exit =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string ProcessIdentity => "fake-sidecar";
        public bool StartObserved { get; set; }

        public ValueTask<ManagedSidecarReadyReport> WaitForReadyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new ManagedSidecarReadyReport(artifact, "ready"));
        }

        public async ValueTask<int> WaitForExitAsync(
            CancellationToken cancellationToken = default) =>
            await _exit.Task.WaitAsync(cancellationToken);

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _exit.TrySetResult(0);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _exit.TrySetResult(0);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CaptureResolver(string material)
        : ICommunicationDriverProtectedMaterialResolver
    {
        private readonly byte[] _material = Encoding.UTF8.GetBytes(material);

        public List<CommunicationDriverProtectedMaterialRequest> Requests { get; } = [];
        public CaptureLease? LastLease { get; private set; }

        public ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
            CommunicationDriverProtectedMaterialRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            LastLease = new CaptureLease(_material.ToArray());
            return ValueTask.FromResult<ICommunicationDriverProtectedMaterialLease>(LastLease);
        }
    }

    private sealed class CaptureLease(byte[] material)
        : ICommunicationDriverProtectedMaterialLease
    {
        private byte[]? _material = material;

        public bool Disposed { get; private set; }
        public ReadOnlyMemory<byte> Material => _material ?? ReadOnlyMemory<byte>.Empty;
        public string? ContentType => "text/plain";

        public ValueTask DisposeAsync()
        {
            if (_material is { } bytes)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
                _material = null;
            }

            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
