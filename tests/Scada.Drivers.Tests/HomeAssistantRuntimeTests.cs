using System.Text.Json;
using System.Threading.Channels;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HomeAssistant;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class HomeAssistantRuntimeTests
{
    [Fact]
    public async Task Write_DoesNotPublishRequestedValue_WithoutAuthoritativeReadback()
    {
        var bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        var registry = new InMemoryTagRegistry();
        var tag = SwitchTag("HA.Pump.State");
        var peer = new RuntimePeer(State("switch.pump", "off"), applyServiceToState: false);

        await using var driver = new HomeAssistantDriver(
            "ha-main",
            "Home Assistant",
            FastSettings(),
            cache,
            registry,
            [new HomeAssistantPoint(tag, "switch.pump", "state", HomeAssistantWriteMode.SwitchState)],
            _ => peer,
            _ => ValueTask.FromResult(new HomeAssistantResolvedCredential("token"u8.ToArray())));

        await driver.StartAsync();
        Assert.True(cache.TryGet(tag.Id, out var initial));
        Assert.False(Assert.IsType<bool>(initial!.Value));

        await driver.WriteAsync(tag.Id, true);

        Assert.Equal(("switch", "turn_on", "switch.pump"), peer.LastService);
        Assert.True(cache.TryGet(tag.Id, out var after));
        Assert.False(Assert.IsType<bool>(after!.Value));
        Assert.Equal(TagQuality.Good, after.Quality);
        Assert.Equal("authoritative-state-readback",
            driver.GetCommunicationDiagnostics().ProtocolDetails!["writeTruth"]);
    }

    [Fact]
    public async Task Reconnect_PerformsFullResync_AndReturnsQualityToGood()
    {
        var bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        var registry = new InMemoryTagRegistry();
        var tag = SwitchTag("HA.Reconnect.State");
        var first = new RuntimePeer(State("switch.pump", "off"), failReceiveOnce: true);
        var second = new RuntimePeer(State("switch.pump", "on"));
        var peers = new Queue<RuntimePeer>([first, second]);

        await using var driver = new HomeAssistantDriver(
            "ha-main",
            "Home Assistant",
            FastSettings(),
            cache,
            registry,
            [new HomeAssistantPoint(tag, "switch.pump", "state", HomeAssistantWriteMode.SwitchState)],
            _ => peers.Dequeue(),
            _ => ValueTask.FromResult(new HomeAssistantResolvedCredential("token"u8.ToArray())));

        await driver.StartAsync();

        await EventuallyAsync(() =>
        {
            var diagnostics = driver.GetCommunicationDiagnostics();
            return diagnostics.Counters.Reconnects >= 1 &&
                   cache.TryGet(tag.Id, out var sample) &&
                   sample?.Quality == TagQuality.Good &&
                   sample.Value is true;
        });

        var final = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Healthy, final.State);
        Assert.True(final.Counters.Disconnections >= 1);
        Assert.True(final.Counters.Reconnects >= 1);
        Assert.True(second.SubscribeCalls >= 1);
        Assert.True(second.GetStatesCalls >= 1);
    }

    [Fact]
    public void Planner_RejectsWritableTagOutsideBoundedServiceProfiles()
    {
        var dataSourceId = Guid.NewGuid();
        var dataSource = DataSource(dataSourceId);
        var binding = Binding("entity:lock.front:state");
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Lock",
            "HA.Lock.State",
            TagDataType.String,
            Source: dataSource.Key,
            Address: binding.PortableAddress,
            ReadOnly: false,
            CommunicationBinding: binding,
            DataSourceId: dataSourceId);
        var package = new EngineeringPackage(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

        var result = new HomeAssistantCommunicationRuntimePlanner().Plan(package, dataSource);

        Assert.False(result.CanActivate);
        Assert.Contains(result.Issues, x => x.Code == "HA_WRITE_PROFILE_UNSUPPORTED");
    }

    [Fact]
    public async Task EngineeringRuntime_ActivatesHomeAssistant_AndRoutesBoundedWriteWithProtectedToken()
    {
        var dataSourceId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var dataSource = DataSource(dataSourceId);
        var binding = Binding("entity:switch.pump:state");
        var tag = new TagEngineeringDto(
            tagId,
            "Pump",
            "HA.Pump.State",
            TagDataType.Boolean,
            Source: dataSource.Key,
            Address: binding.PortableAddress,
            ReadOnly: false,
            CommunicationBinding: binding,
            DataSourceId: dataSourceId);
        var package = new EngineeringPackage(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

        var peer = new RuntimePeer(State("switch.pump", "off"), applyServiceToState: true);
        var resolver = new CaptureResolver("llat-runtime-token");
        var components = new CommunicationDriverRuntimeComponentRegistry();
        components.Register(new CommunicationDriverRuntimeComponentRegistration(
            new HomeAssistantCommunicationRuntimePlanner(),
            new HomeAssistantCommunicationRuntimeFactory(_ => peer),
            new HomeAssistantDriverDescriptorProvider().Descriptor));

        await using var runtime = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(3),
            communicationComponents: components,
            protectedMaterialResolver: resolver);

        var activation = await runtime.ActivateAsync("project-ha-l3", 1, package);

        Assert.True(
            activation.Activated,
            string.Join(" | ", activation.CompilationIssues.Select(x => x.Message)
                .Concat(activation.RuntimeIssues.Select(x => x.Message))));
        Assert.True(runtime.TryGetCurrent(tagId, out var initial));
        Assert.False(Assert.IsType<bool>(initial!.Value));

        await runtime.WriteAsync(tagId, true);

        Assert.Equal(("switch", "turn_on", "switch.pump"), peer.LastService);
        Assert.True(runtime.TryGetCurrent(tagId, out var current));
        Assert.True(Assert.IsType<bool>(current!.Value));
        Assert.Equal(TagQuality.Good, current.Quality);

        var diagnostics = Assert.Single(runtime.Describe().CommunicationDrivers);
        Assert.Equal(HomeAssistantContract.DriverType, diagnostics.DriverType);
        Assert.Equal("ha.main", diagnostics.DataSourceKey);
        Assert.True(diagnostics.Counters.WriteOperations >= 1);
        Assert.Equal("protected-reference", diagnostics.ProtocolDetails!["auth"]);
        Assert.Equal(HomeAssistantContract.AccessTokenPurpose, resolver.Requests.Last().Purpose);
        Assert.DoesNotContain("llat-runtime-token",
            string.Join("|", diagnostics.ProtocolDetails.Values));
    }

    [Fact]
    public void Planner_ParsesOnlyCanonicalEntityAddresses()
    {
        Assert.True(HomeAssistantCommunicationRuntimePlanner.TryParsePortableAddress(
            "entity:light.office:attribute:brightness",
            out var entity,
            out var field));
        Assert.Equal("light.office", entity);
        Assert.Equal("brightness", field);

        Assert.False(HomeAssistantCommunicationRuntimePlanner.TryParsePortableAddress(
            "service:light:turn_on",
            out _,
            out _));
        Assert.False(HomeAssistantCommunicationRuntimePlanner.TryParsePortableAddress(
            "entity:light.office:attribute:",
            out _,
            out _));
    }

    private static HomeAssistantConnectionSettings FastSettings() =>
        new(
            "ha.local",
            8123,
            UseTls: false,
            RequestTimeout: TimeSpan.FromMilliseconds(500),
            ReconnectMinimumDelay: TimeSpan.FromMilliseconds(1),
            ReconnectMaximumDelay: TimeSpan.FromMilliseconds(10));

    private static TagDefinition SwitchTag(string path) =>
        TagDefinition.Create(
            "Pump",
            path,
            TagDataType.Boolean,
            source: "ha-main",
            readOnly: false,
            communicationBinding: Binding("entity:switch.pump:state"));

    private static CommunicationTagBinding Binding(string address) =>
        new(
            CommunicationTagBinding.CurrentContractVersion,
            HomeAssistantContract.SchemaId,
            HomeAssistantContract.SchemaVersion,
            address);

    private static DataSourceEngineeringDto DataSource(Guid id) =>
        new(
            id,
            "ha.main",
            "Home Assistant",
            HomeAssistantContract.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "ha.local",
                ["port"] = "8123",
                ["reconnectMinimumMilliseconds"] = "1",
                ["reconnectMaximumMilliseconds"] = "10"
            },
            SecretReferences: new Dictionary<string, string>
            {
                [HomeAssistantEngineeringProvider.AccessTokenReferenceKey] = "vault://home/assistant/token"
            });

    private static HomeAssistantState State(string entityId, string state) =>
        new(
            entityId,
            state,
            JsonSerializer.SerializeToElement(new { friendly_name = entityId }),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(new { id = "ctx" }));

    private static async Task EventuallyAsync(Func<bool> predicate)
    {
        for (var i = 0; i < 200; i++)
        {
            if (predicate()) return;
            await Task.Delay(10);
        }
        Assert.True(predicate(), "Condition did not become true.");
    }

    private sealed class RuntimePeer : IHomeAssistantClient
    {
        private readonly Channel<HomeAssistantStateChangedEvent> _events = Channel.CreateUnbounded<HomeAssistantStateChangedEvent>();
        private readonly bool _applyServiceToState;
        private bool _failReceiveOnce;
        private HomeAssistantState _state;

        public RuntimePeer(
            HomeAssistantState state,
            bool applyServiceToState = false,
            bool failReceiveOnce = false)
        {
            _state = state;
            _applyServiceToState = applyServiceToState;
            _failReceiveOnce = failReceiveOnce;
        }

        public bool Connected { get; private set; }
        public bool Authenticated { get; private set; }
        public string? HomeAssistantVersion => "2026.10.test";
        public int SubscribeCalls { get; private set; }
        public int GetStatesCalls { get; private set; }
        public (string Domain, string Service, string EntityId)? LastService { get; private set; }

        public ValueTask ConnectAndAuthenticateAsync(
            ReadOnlyMemory<byte> accessToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (accessToken.IsEmpty) throw new InvalidOperationException("Token required.");
            Connected = true;
            Authenticated = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<HomeAssistantState>> GetStatesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetStatesCalls++;
            return ValueTask.FromResult<IReadOnlyList<HomeAssistantState>>([_state]);
        }

        public ValueTask<IReadOnlyList<HomeAssistantRegistryDisplayEntry>> GetEntityRegistryForDisplayAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<HomeAssistantRegistryDisplayEntry>>([]);

        public ValueTask<int> SubscribeStateChangedAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SubscribeCalls++;
            return ValueTask.FromResult(7);
        }

        public ValueTask CallServiceAsync(
            string domain,
            string service,
            string entityId,
            IReadOnlyDictionary<string, object?>? serviceData = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastService = (domain, service, entityId);
            if (_applyServiceToState)
            {
                var next = service switch
                {
                    "turn_on" => "on",
                    "turn_off" => "off",
                    _ => _state.State
                };
                _state = _state with
                {
                    State = next,
                    LastChanged = DateTimeOffset.UtcNow,
                    LastUpdated = DateTimeOffset.UtcNow
                };
            }
            return ValueTask.CompletedTask;
        }

        public async ValueTask<HomeAssistantStateChangedEvent> ReceiveStateChangedAsync(
            CancellationToken cancellationToken = default)
        {
            if (_failReceiveOnce)
            {
                _failReceiveOnce = false;
                throw new IOException("simulated disconnect");
            }
            return await _events.Reader.ReadAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            Connected = false;
            Authenticated = false;
            _events.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CaptureResolver(string token) : ICommunicationDriverProtectedMaterialResolver
    {
        private readonly byte[] _token = System.Text.Encoding.UTF8.GetBytes(token);
        public List<CommunicationDriverProtectedMaterialRequest> Requests { get; } = [];

        public ValueTask<ICommunicationDriverProtectedMaterialLease> ResolveAsync(
            CommunicationDriverProtectedMaterialRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return ValueTask.FromResult<ICommunicationDriverProtectedMaterialLease>(
                new TestLease(_token.ToArray()));
        }
    }

    private sealed class TestLease(byte[] material) : ICommunicationDriverProtectedMaterialLease
    {
        private byte[]? _material = material;
        public ReadOnlyMemory<byte> Material => _material ?? ReadOnlyMemory<byte>.Empty;
        public string? ContentType => "text/plain";

        public ValueTask DisposeAsync()
        {
            if (_material is { } bytes)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
                _material = null;
            }
            return ValueTask.CompletedTask;
        }
    }
}
