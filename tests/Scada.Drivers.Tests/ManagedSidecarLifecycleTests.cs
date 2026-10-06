using Scada.DriverHost.Sidecars;
using Scada.Drivers.HostResources;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Tests;

public sealed class ManagedSidecarLifecycleTests
{
    [Fact]
    public void Compatibility_ValidatesArtifactRuntimeAndSchema()
    {
        var compatibility = Compatibility();

        Assert.True(compatibility.IsCompatible(Artifact(), out var compatibleError));
        Assert.Null(compatibleError);

        Assert.False(
            compatibility.IsCompatible(Artifact() with { SchemaVersion = 4 }, out var schemaError));
        Assert.Equal("SIDECAR_SCHEMA_VERSION_MISMATCH", schemaError);

        Assert.False(
            compatibility.IsCompatible(Artifact() with { ArtifactVersion = "2.0.0" }, out var artifactError));
        Assert.Equal("SIDECAR_ARTIFACT_VERSION_MISMATCH", artifactError);
    }

    [Fact]
    public void Configuration_SeparatesProtectedReferencesAndRedactsSensitiveLogMaterial()
    {
        var config = Configuration();
        config.Validate();

        Assert.Equal(
            "protected-material-v1:abcdef",
            Assert.Single(config.ProtectedReferences));

        const string secret = "super-secret-value";
        var sanitized = ManagedSidecarLogSanitizer.Sanitize(
            "failure\ncredential=super-secret-value ref=protected-material-v1:abcdef",
            new[] { secret, "protected-material-v1:abcdef" });

        Assert.NotNull(sanitized);
        Assert.DoesNotContain(secret, sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("protected-material-v1:abcdef", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', sanitized);
        Assert.Contains("***", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void Configuration_RejectsSameSettingAsPublicAndProtected()
    {
        var config = new ManagedSidecarConfiguration(
            new Dictionary<string, string>
            {
                ["credential"] = "not-allowed-here"
            },
            new[]
            {
                new ManagedSidecarProtectedSettingReference(
                    "credential",
                    "Integration",
                    "sidecar-a",
                    "ConnectionCredential",
                    "protected-material-v1:abcdef")
            });

        Assert.Throws<ArgumentException>(config.Validate);
    }

    [Fact]
    public void Definition_CarriesPackagingRollbackAndPersistentStateOwnership()
    {
        var definition = Definition();

        definition.Validate();

        Assert.Equal(
            new[]
            {
                ManagedSidecarPackageTargets.WindowsX64,
                ManagedSidecarPackageTargets.LinuxX64,
                ManagedSidecarPackageTargets.LinuxArm64,
                ManagedSidecarPackageTargets.ContainerX64,
                ManagedSidecarPackageTargets.ContainerArm64
            },
            definition.Packaging.SupportedTargets);
        Assert.Equal("0.9.0", definition.Compatibility.RollbackArtifactVersion);
        Assert.Equal("zwave-controller-main", definition.PersistentState!.BoundResourceId!.Value);
        Assert.True(definition.PersistentState.RequiresBackupBeforeUpgrade);
        Assert.Equal(ManagedSidecarControlPlaneExposure.LocalOnly, definition.ControlPlaneExposure);
    }

    [Fact]
    public async Task Supervisor_StartReadyStop_UsesGracefulStateMachine()
    {
        var process = new FakeSidecarProcess(Artifact());
        var factory = new FakeSidecarProcessFactory(process);
        await using var supervisor = new ManagedSidecarSupervisor(
            Definition(),
            factory,
            FastPolicy(),
            new ImmediateDelay());

        await supervisor.StartAsync();

        Assert.Equal(ManagedSidecarLifecycleState.Ready, supervisor.Snapshot.State);
        Assert.Equal(1, factory.StartCount);

        await supervisor.StopAsync();

        Assert.True(process.StopCalled);
        Assert.True(process.Disposed);
        Assert.Equal(ManagedSidecarLifecycleState.Stopped, supervisor.Snapshot.State);
    }

    [Fact]
    public async Task Supervisor_ReadinessTimeout_FailsClosed()
    {
        var process = new FakeSidecarProcess(Artifact(), neverReady: true);
        var factory = new FakeSidecarProcessFactory(process);
        await using var supervisor = new ManagedSidecarSupervisor(
            Definition(),
            factory,
            FastPolicy() with { ReadinessTimeout = TimeSpan.FromMilliseconds(30) },
            new ImmediateDelay());

        var error = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await supervisor.StartAsync());

        Assert.Contains("readiness", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ManagedSidecarLifecycleState.Faulted, supervisor.Snapshot.State);
        Assert.True(process.StopCalled);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task Supervisor_CrashRecovery_UsesBoundedExponentialBackoff()
    {
        var first = new FakeSidecarProcess(Artifact());
        var second = new FakeSidecarProcess(Artifact());
        var third = new FakeSidecarProcess(Artifact());
        var factory = new FakeSidecarProcessFactory(first, second, third);
        var delay = new ImmediateDelay();

        await using var supervisor = new ManagedSidecarSupervisor(
            Definition(),
            factory,
            FastPolicy() with
            {
                InitialRestartDelay = TimeSpan.FromSeconds(1),
                MaximumRestartDelay = TimeSpan.FromSeconds(2),
                CrashLoopThreshold = 5
            },
            delay);

        await supervisor.StartAsync();
        first.Exit(17);
        await EventuallyAsync(() =>
            factory.StartCount >= 2 &&
            supervisor.Snapshot.State == ManagedSidecarLifecycleState.Ready);

        second.Exit(18);
        await EventuallyAsync(() =>
            factory.StartCount >= 3 &&
            supervisor.Snapshot.State == ManagedSidecarLifecycleState.Ready);

        Assert.Equal(2, supervisor.Snapshot.RestartCount);
        Assert.Equal(
            new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) },
            delay.Delays.Take(2).ToArray());
    }

    [Fact]
    public async Task Supervisor_RepeatedCrash_EntersCrashLoopWithoutUnboundedRestart()
    {
        var first = new FakeSidecarProcess(Artifact());
        var second = new FakeSidecarProcess(Artifact());
        var third = new FakeSidecarProcess(Artifact());
        var factory = new FakeSidecarProcessFactory(first, second, third);

        await using var supervisor = new ManagedSidecarSupervisor(
            Definition(),
            factory,
            FastPolicy() with { CrashLoopThreshold = 3 },
            new ImmediateDelay());

        await supervisor.StartAsync();

        first.Exit(10);
        await EventuallyAsync(() => factory.StartCount >= 2);
        second.Exit(11);
        await EventuallyAsync(() => factory.StartCount >= 3);
        third.Exit(12);

        await EventuallyAsync(() =>
            supervisor.Snapshot.State == ManagedSidecarLifecycleState.CrashLoop);

        var snapshot = supervisor.Snapshot;
        Assert.Equal(3, snapshot.FailuresInCrashWindow);
        Assert.Equal(2, snapshot.RestartCount);
        Assert.Equal("SIDECAR_CRASH_LOOP", snapshot.LastSanitizedError);
        Assert.Equal(3, factory.StartCount);
    }

    [Fact]
    public async Task Supervisor_IncompatibleReadyArtifact_IsRejectedAndStopped()
    {
        var incompatible = new FakeSidecarProcess(
            Artifact() with { RuntimeVersion = "runtime-2" });
        var factory = new FakeSidecarProcessFactory(incompatible);

        await using var supervisor = new ManagedSidecarSupervisor(
            Definition(),
            factory,
            FastPolicy(),
            new ImmediateDelay());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await supervisor.StartAsync());

        Assert.Contains("SIDECAR_RUNTIME_VERSION_MISMATCH", error.Message, StringComparison.Ordinal);
        Assert.True(incompatible.StopCalled);
        Assert.True(incompatible.Disposed);
        Assert.Equal(ManagedSidecarLifecycleState.Faulted, supervisor.Snapshot.State);
    }

    [Fact]
    public void HostResource_DisappearReappearAtNewLocator_PreservesPersistentStateBinding()
    {
        var registry = new HostResourceRegistry();
        var original = registry.Observe(Controller("COM3"));
        registry.MarkUnavailable(original.ResourceId);
        var leases = new HostResourceLeaseCoordinator(registry);

        Assert.Throws<InvalidOperationException>(() =>
            leases.Acquire(original.ResourceId, "sidecar-a", () => true));

        var moved = registry.Observe(Controller("COM7"));
        using var lease = leases.Acquire(moved.ResourceId, "sidecar-a", () => true);

        var persistent = Definition().PersistentState!;

        Assert.Equal(original.ResourceId, moved.ResourceId);
        Assert.Equal(moved.ResourceId, persistent.BoundResourceId);
        Assert.Equal("usb:zwave:0001", lease.PhysicalIdentityKey);
        Assert.Equal("COM7", moved.Locator.Value);
    }

    [Fact]
    public void HostResource_WrongReplacementAfterDisappear_IsRejected()
    {
        var registry = new HostResourceRegistry();
        var original = registry.Observe(Controller("COM3"));
        registry.MarkUnavailable(original.ResourceId);

        var wrong = Controller("COM7") with
        {
            PhysicalIdentity = new HostResourcePhysicalIdentity("usb:zwave:9999")
        };

        Assert.Throws<HostResourceIdentityMismatchException>(() =>
            registry.Observe(wrong));
    }

    private static ManagedSidecarArtifactIdentity Artifact() =>
        new("elite.test-sidecar", "1.0.0", "runtime-1", 3);

    private static ManagedSidecarCompatibilityMetadata Compatibility() =>
        new(
            "elite.test-sidecar",
            new[] { "0.9.0", "1.0.0" },
            "runtime-1",
            MinimumSchemaVersion: 2,
            MaximumSchemaVersion: 3,
            RollbackArtifactVersion: "0.9.0");

    private static ManagedSidecarConfiguration Configuration() =>
        new(
            new Dictionary<string, string>
            {
                ["controlSocket"] = "local://sidecar-a",
                ["mode"] = "fixture"
            },
            new[]
            {
                new ManagedSidecarProtectedSettingReference(
                    "credential",
                    "Integration",
                    "sidecar-a",
                    "ConnectionCredential",
                    "protected-material-v1:abcdef")
            });

    private static ManagedSidecarDefinition Definition() =>
        new(
            "sidecar-a",
            Artifact(),
            Compatibility(),
            new ManagedSidecarPackagingMetadata(
                new[]
                {
                    ManagedSidecarPackageTargets.WindowsX64,
                    ManagedSidecarPackageTargets.LinuxX64,
                    ManagedSidecarPackageTargets.LinuxArm64,
                    ManagedSidecarPackageTargets.ContainerX64,
                    ManagedSidecarPackageTargets.ContainerArm64
                }),
            Configuration(),
            new ManagedSidecarPersistentStateOwnership(
                "sidecar-a-state",
                StateSchemaVersion: 3,
                BackupKey: "sidecar-a-backup",
                BoundResourceId: new HostResourceId("zwave-controller-main")));

    private static ManagedSidecarRestartPolicy FastPolicy() =>
        new(
            ReadinessTimeout: TimeSpan.FromMilliseconds(250),
            GracefulStopTimeout: TimeSpan.FromMilliseconds(250),
            InitialRestartDelay: TimeSpan.FromMilliseconds(1),
            MaximumRestartDelay: TimeSpan.FromMilliseconds(4),
            CrashLoopWindow: TimeSpan.FromMinutes(1),
            CrashLoopThreshold: 4);

    private static HostResourceDescriptor Controller(string locator) =>
        ZWaveControllerHostResource.Create(
            "zwave-controller-main",
            new HostSerialLineSettings(locator),
            new HostResourcePhysicalIdentity("usb:zwave:0001"),
            family: "700-series",
            model: "ZSTICK-7",
            firmware: "7.19.3",
            rfRegion: "EU",
            nvmIdentity: "homeid:0xA1B2C3D4");

    private static async Task EventuallyAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (condition()) return;
            await Task.Delay(10);
        }

        Assert.True(condition(), "Condition did not become true.");
    }

    private sealed class ImmediateDelay : IManagedSidecarDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public ValueTask DelayAsync(
            TimeSpan delay,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSidecarProcessFactory : IManagedSidecarProcessFactory
    {
        private readonly Queue<FakeSidecarProcess> _processes;

        public FakeSidecarProcessFactory(params FakeSidecarProcess[] processes) =>
            _processes = new Queue<FakeSidecarProcess>(processes);

        public int StartCount { get; private set; }

        public ValueTask<IManagedSidecarProcess> StartAsync(
            ManagedSidecarDefinition definition,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            definition.Validate();
            StartCount++;
            if (_processes.Count == 0)
                throw new InvalidOperationException("No fake sidecar process remains.");
            return ValueTask.FromResult<IManagedSidecarProcess>(_processes.Dequeue());
        }
    }

    private sealed class FakeSidecarProcess : IManagedSidecarProcess
    {
        private readonly TaskCompletionSource<ManagedSidecarReadyReport> _ready =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<int> _exit =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FakeSidecarProcess(
            ManagedSidecarArtifactIdentity artifact,
            bool neverReady = false)
        {
            Artifact = artifact;
            if (!neverReady)
                _ready.TrySetResult(new ManagedSidecarReadyReport(artifact, "fixture ready"));
        }

        public ManagedSidecarArtifactIdentity Artifact { get; }
        public string ProcessIdentity => $"fake:{Artifact.ArtifactId}";
        public bool StopCalled { get; private set; }
        public bool Disposed { get; private set; }

        public async ValueTask<ManagedSidecarReadyReport> WaitForReadyAsync(
            CancellationToken cancellationToken = default) =>
            await _ready.Task.WaitAsync(cancellationToken);

        public async ValueTask<int> WaitForExitAsync(
            CancellationToken cancellationToken = default) =>
            await _exit.Task.WaitAsync(cancellationToken);

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCalled = true;
            _exit.TrySetResult(0);
            return ValueTask.CompletedTask;
        }

        public void Exit(int exitCode) => _exit.TrySetResult(exitCode);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
